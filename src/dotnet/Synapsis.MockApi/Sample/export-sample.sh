#!/usr/bin/env bash
# Writes records.json, the mock's sample: real records from the local MongoDB (database synapsis_v2), every state,
# in the shape of the "records" collection. Run from the repository root with the core MongoDB up:
#   bash src/dotnet/Synapsis.MockApi/Sample/export-sample.sh
# What it keeps:
#   1. GTD and UCDP (points and participants) and USGS (points, no participants): every record.
#   2. KEV (participants, no points): the events of three vendors (Apache, Cisco, Microsoft).
#   3. The events of other sources about the same entities (EPSS scores, Hacker News posts, NVD).
#   4. Every entity these events name (from every source that states it) and every relation that touches them,
#      and the conclusions about them.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
read -r -d '' script <<'JS' || true
const records = db.records;
const kept = new Map();
function keep(documents) { documents.forEach(function (document) { kept.set(document._id, document); }); }

// 1. GTD, UCDP, USGS: every record.
keep(records.find({ source: { $in: ["src:start:gtd", "src:ucdp:ged", "src:usgs:comcat"] } }).toArray());

// 2. KEV: the events of three vendors.
const vendors = ["ent:kev-vendor:apache", "ent:kev-vendor:cisco", "ent:kev-vendor:microsoft"];
const kevEvents = records.find({ source: "src:cisa:kev", kind: "event", "record.participants.entity": { $in: vendors } }).toArray();
keep(kevEvents);
const named = new Set();
kevEvents.forEach(function (event) { event.record.participants.forEach(function (p) { named.add(p.entity); }); });

// 3. The events of other sources about the same entities.
keep(records.find({ kind: "event", source: { $ne: "src:cisa:kev" }, "record.participants.entity": { $in: [...named] } }).toArray());

// 4. Every entity the kept events name, the relations and conclusions that touch them.
kept.forEach(function (document) {
  if (document.kind === "event") { (document.record.participants || []).forEach(function (p) { named.add(p.entity); }); }
});
const ids = [...named];
keep(records.find({ kind: "entity", id: { $in: ids } }).toArray());
keep(records.find({ kind: "relation", $or: [{ "record.from": { $in: ids } }, { "record.to": { $in: ids } }] }).toArray());
keep(records.find({ kind: "conclusion", "record.about": { $in: ids } }).toArray());

// 5. One record per line, sorted by key, dates as UTC text.
const lines = [...kept.values()].sort(function (a, b) { return a._id < b._id ? -1 : 1; }).map(function (d) {
  return EJSON.stringify({ key: d._id, id: d.id, source: d.source, kind: d.kind, type: d.type, state: d.state,
    known_at: d.known_at.toISOString(), received_at: d.received_at.toISOString(), batch_id: d.batch_id,
    producer: d.producer, record: d.record }, { relaxed: true });
});
print("[\n" + lines.join(",\n") + "\n]");
JS
docker exec synapsis-core-mongo-1 mongosh --quiet synapsis_v2 --eval "$script" > "$here/records.json"
echo "wrote $here/records.json ($(grep -c '"key"' "$here/records.json") records)"
