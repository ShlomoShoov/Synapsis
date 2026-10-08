"""FeederState: what a feeder remembers between runs, in one folder per feeder under FEEDER_STATE_DIR.

  cursor.json        where the next run starts (written atomically, only after the API acknowledged the data)
  memo/              what a feeder must remember besides the cursor (EPSS: the CVE entities already sent;
                     Wikipedia: the spelling chosen for each pollster); cleared with the cursor by --reset
  observed/          the feeder's own observations that the source cannot give again (OpenSanctions: the day each
                     listing was first seen); kept by --reset, so back it up with the database
  rejections.jsonl   every record the API rejected: batch, index, id, code, path, message
  runs.jsonl         one line per run: counts, codes, duration, outcome
The API's idempotency means a lost state folder costs time, never correctness: a re-run sends the same records and
gets 'unchanged' back."""
from __future__ import annotations

import json
import os
import shutil
from datetime import datetime, timezone
from pathlib import Path


class FeederState:
    def __init__(self, root: Path, feeder: str):
        self.dir = root / feeder         # created on the first write, so a dry run leaves nothing behind
        self._cursor_path = self.dir / "cursor.json"
        self.cursor: dict = json.loads(self._cursor_path.read_text()) if self._cursor_path.exists() else {}

    def save_cursor(self, cursor: dict) -> None:
        self.cursor = cursor
        self.dir.mkdir(parents=True, exist_ok=True)
        tmp = self._cursor_path.with_suffix(".tmp")
        tmp.write_text(json.dumps(cursor, indent=1, sort_keys=True, default=str))
        os.replace(tmp, self._cursor_path)

    @property
    def memo_dir(self) -> Path:
        return self.dir / "memo"

    def reset(self) -> None:
        """Start the source again from its beginning: forget the cursor and the memo (records already held come back
        from the API as unchanged). Rejection and run logs are kept."""
        self.save_cursor({})
        shutil.rmtree(self.memo_dir, ignore_errors=True)

    def log_rejections(self, batch_id: str, rejected: list[dict]) -> None:
        if not rejected:
            return
        self.dir.mkdir(parents=True, exist_ok=True)
        with open(self.dir / "rejections.jsonl", "a", encoding="utf-8") as fh:
            for r in rejected:
                fh.write(json.dumps({"batch_id": batch_id, **r}, ensure_ascii=False) + "\n")

    def log_run(self, summary: dict) -> None:
        self.dir.mkdir(parents=True, exist_ok=True)
        with open(self.dir / "runs.jsonl", "a", encoding="utf-8") as fh:
            fh.write(json.dumps({"logged_at": datetime.now(timezone.utc).isoformat(), **summary}, default=str) + "\n")
