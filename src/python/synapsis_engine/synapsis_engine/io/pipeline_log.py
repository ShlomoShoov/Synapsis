"""The engine's lines in synapsis-pipeline-log (contracts/pipeline-log.md): one `run` document per source and step.
A write that fails is printed and dropped: the log never stops the engine."""
import datetime
import os

import requests


def run(step, source, processed, written, failed=0, message=None):
    url = os.environ.get("ENGINE_ES_URL", "http://localhost:9200").rstrip("/") + "/synapsis-pipeline-log/_doc"
    doc = {"at": datetime.datetime.now(datetime.UTC).strftime("%Y-%m-%dT%H:%M:%S.%fZ")[:-4] + "Z",
           "service": "engine", "event": "run", "source": source, "processed": int(processed),
           "written": int(written), "skipped": 0, "failed": int(failed)}
    if message:
        doc["message"] = f"{step}: {message}"[:500]
    try:
        requests.post(url, json=doc, timeout=5).raise_for_status()
    except Exception as e:  # noqa: BLE001: the log is best effort
        print(f"pipeline log not written ({source}, {step}): {e}", flush=True)
