#!/usr/bin/env bash
# The whole picture of the load in at most 25 lines. Read only: docker ps, counts, the pipeline log, the feeders' runs.
#   deploy/scripts/night-status.sh
# Per source: "api" = new + changed states the API stored (synapsis-ingest-log, = MongoDB documents), "es" = search
# documents (relations are not indexed). Rates come from the last 30-second pipeline-log document of each service.
cd "$(dirname "$0")/../.." || exit 1
ES=${ES:-http://localhost:9200}
NEO_PASSWORD=$(sed -n 's/^CORE_NEO4J_PASSWORD=//p' deploy/core/.env)

echo "$(date '+%F %T') | disk free $(df -h --output=avail / | tail -1 | tr -d ' ') | mem available $(free -g | awk '/Mem/{print $7}') GB, swap used $(free -g | awk '/Swap/{print $3}') GB"
echo "core: $(docker ps -a --filter label=com.docker.compose.project=synapsis-core --format '{{.Label "com.docker.compose.service"}}={{.State}}{{if (.Status | printf "%s" | len)}}:{{.Status}}{{end}}' | sed -E 's/:Up ([^(]*)(\(healthy\))?/:\1\2/; s/ \(healthy\)/+/; s/:Exited \(([0-9]+)\).*/:exit\1/; s/ (seconds|minutes|hours|days)/\1/; s/ //g' | sort | tr '\n' ' ' | cut -c1-230)"
echo "feeders running: $(docker ps --filter label=com.docker.compose.project=synapsis-feeders --format '{{.Command}}' | grep -oE 'run [a-z-]+' | cut -d' ' -f2 | sort | tr '\n' ' ')"

mongo=$(docker exec synapsis-core-mongo-1 mongosh synapsis_v2 --quiet --eval 'db.records.estimatedDocumentCount()' 2>/dev/null)
es=$(curl -s "$ES/synapsis-records/_count" | jq -r '.count // "?"')
neo=$(docker exec synapsis-core-neo4j-1 cypher-shell -u neo4j -p "$NEO_PASSWORD" --format plain "MATCH (n) RETURN count(n)" 2>/dev/null | tail -1)
echo "totals: mongo $mongo documents | es $es documents | neo4j ${neo:-?} nodes"

# The last document of each service, and its rate.
curl -s "$ES/synapsis-pipeline-log/_search" -H 'content-type: application/json' -d '{"size":0,"aggs":{"s":{"terms":{"field":"service","size":10},"aggs":{"last":{"top_hits":{"size":1,"sort":[{"at":"desc"}]}}}}}}' \
  | jq -r '.aggregations.s.buckets[]? | .last.hits.hits[0]._source | "\(.service): \(.event) \(.at[11:19])Z processed \(.processed) (\((.processed/30*10|floor)/10)/s) written \(.written) skipped \(.skipped) failed \(.failed)\(if .lag != null then " lag \(.lag)" else "" end)\(if .message then " | \(.message[0:90])" else "" end)"' 2>/dev/null
for g in synapsis-elasticsearch synapsis-neo4j; do
  printf "%s lag: " "$g"; docker exec synapsis-core-kafka-1 /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group "$g" 2>/dev/null | awk '$6 ~ /^[0-9]+$/ {s += $6} END {print s + 0}'
done | paste -sd' ' -

# Per source: API stored (ingest log) vs Elasticsearch documents, and refusals.
{ curl -s "$ES/synapsis-ingest-log/_search" -H 'content-type: application/json' -d '{"size":0,"aggs":{"s":{"terms":{"field":"source","size":60},"aggs":{"n":{"sum":{"script":"doc[\"new\"].value + doc[\"changed\"].value"}},"r":{"sum":{"field":"rejected"}}}}}}' | jq -r '.aggregations.s.buckets[]? | "A \(.key) \(.n.value|floor) \(.r.value|floor)"'
  curl -s "$ES/synapsis-records/_search" -H 'content-type: application/json' -d '{"size":0,"aggs":{"s":{"terms":{"field":"source","size":60}}}}' | jq -r '.aggregations.s.buckets[]? | "E \(.key) \(.doc_count)"'
} | awk '{k=$2; sub(/^src:/,"",k); if ($1=="A") {a[k]=$3; r[k]=$4} else e[k]=$3; seen[k]=1}
       END {for (k in seen) printf "%s %s/%s%s;\n", k, a[k]+0, e[k]+0, (r[k]>0 ? " rej " r[k] : "")}' | sort | tr '\n' ' ' \
  | sed 's/^/per source (api\/es): /' | fold -s -w 200 | head -6

# The feeders' last run each: outcome and records sent.
echo "feeder runs: $(docker run --rm --entrypoint sh -v synapsis-feeders_feeder-state:/state:ro synapsis/feeders:dev -c 'for f in /state/*/runs.jsonl; do tail -n1 "$f"; done' 2>/dev/null \
  | jq -r '"\(.feeder)=\(.outcome | sub("stopped at --limit "; "limit"))/\(.records_sent)\(if (.rejected_by_code|length) > 0 then "/rej" else "" end)"' | tr '\n' ' ')" | fold -s -w 200 | head -4
