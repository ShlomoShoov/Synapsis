#!/usr/bin/env bash
# The order of tonight's full load (docs/tonight). Each step is a one-off `docker compose run` of the feeder's own
# service (its keys, state and cache volumes), not its long-running `serve`.
#   deploy/scripts/night-load.sh rehearsal   every feeder with --limit 200, one at a time
#   deploy/scripts/night-load.sh A           the proof sources, in full, up to 4 at a time
#   deploy/scripts/night-load.sh B           the others, newest first: a recent range, the older range, then a plain
#                                            run that realigns the cursor
#   deploy/scripts/night-load.sh C           EPSS, newest month first, while the disk keeps 15 GB free
#   deploy/scripts/night-load.sh all         A, then B, then C
# Output: docs/tonight/load/<feeder>.log; one line per run (start, end, exit code, seconds) in docs/tonight/load/runs.log.
set -u
cd "$(dirname "$0")/../.." || exit 1
LOG=docs/tonight/load
mkdir -p "$LOG"
RECENT=${RECENT:-2025-01-01}          # phase B: the recent range starts here; the older range ends here
PARALLEL=${PARALLEL:-4}

feed() {  # feed <feeder> [arguments of `run`]
  local name=$1 t0 rc
  shift
  t0=$(date +%s)
  echo "$(date '+%F %T') start $name $*" >> "$LOG/runs.log"
  docker compose -f deploy/feeders/compose.yml run --rm --no-deps -T "feeder-$name" run "$name" "$@" >> "$LOG/$name.log" 2>&1
  rc=$?
  echo "$(date '+%F %T') end   $name $* rc=$rc $(( $(date +%s) - t0 ))s" >> "$LOG/runs.log"
  return $rc
}

slot() {  # wait while PARALLEL jobs run
  while [ "$(jobs -rp | wc -l)" -ge "$PARALLEL" ]; do wait -n; done
}

ranged() {  # ranged <feeder> <first day of the source>: recent range, older range, plain run
  feed "$1" --since "$RECENT"
  feed "$1" --since "$2" --until "$RECENT"
  feed "$1"
}

phase_a() {
  echo "$(date '+%F %T') phase A" >> "$LOG/runs.log"
  slot; { feed kev; feed kev-classes; } &
  for f in ucdp fdic gtd usgs ransomware-live wikipedia-elections polymarket; do slot; feed "$f" & done
  wait
  echo "$(date '+%F %T') phase A sent" >> "$LOG/runs.log"
}

phase_b() {
  echo "$(date '+%F %T') phase B" >> "$LOG/runs.log"
  slot; ranged nvd 1999-01-01 &
  slot; ranged gdelt 2021-01-06 &
  slot; ranged firms 2021-01-01 &
  slot; ranged hackernews 2021-11-01 &
  for f in reddit jhu-covid delphi gfw fred opensanctions xz-git; do slot; feed "$f" & done
  wait
  echo "$(date '+%F %T') phase B sent" >> "$LOG/runs.log"
}

phase_c() {
  echo "$(date '+%F %T') phase C" >> "$LOG/runs.log"
  local m
  m=$(date +%Y-%m-01)
  while [[ "$m" > "2022-01-31" ]]; do
    if [ "$(df --output=avail -BG / | tail -1 | tr -dc 0-9)" -lt 15 ]; then
      echo "$(date '+%F %T') phase C stopped: less than 15 GB free" >> "$LOG/runs.log"
      break
    fi
    if [ -e "$LOG/stop-c" ]; then
      echo "$(date '+%F %T') phase C stopped: $LOG/stop-c exists" >> "$LOG/runs.log"
      break
    fi
    feed epss --since "$m" --until "$m"
    m=$(date -d "$m -1 month" +%Y-%m-01)
  done
  echo "$(date '+%F %T') phase C ended" >> "$LOG/runs.log"
}

rehearsal() {
  echo "$(date '+%F %T') rehearsal" >> "$LOG/runs.log"
  for f in kev kev-classes usgs ransomware-live gtd wikipedia-elections polymarket ucdp fdic \
           nvd reddit hackernews gdelt jhu-covid delphi firms gfw fred opensanctions xz-git epss; do
    if [ "$f" = kev-classes ]; then feed "$f" --limit 200 --set wait_minutes=2; else feed "$f" --limit 200; fi
  done
  echo "$(date '+%F %T') rehearsal sent" >> "$LOG/runs.log"
}

case "${1:-}" in
  rehearsal) rehearsal ;;
  A) phase_a ;;
  B) phase_b ;;
  C) phase_c ;;
  all) phase_a; phase_b; phase_c ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac
