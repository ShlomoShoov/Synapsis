"""The xz-utils git history: a blob-less clone (commits and file names only), then one event per commit.

Each commit is placed at the deepest folder holding every file it changed (at most four levels). Authors and
committers are Person entities keyed by a hash of their email address; the address itself is not stored."""
from __future__ import annotations

import hashlib
import os
import subprocess
from datetime import datetime, timezone

from ..base import Checkpoint, Feeder, register
from ..sil import attributes, entity, event, logical, measures, participant, slug, text, time_spec

REPO = "https://github.com/tukaani-project/xz.git"
SEP = "\x1f"


def person_id(email: str) -> str:
    return "ent:git-author:" + hashlib.sha256(email.strip().lower().encode("utf-8")).hexdigest()[:16]


def common_folder(paths: list[str], depth: int = 4) -> list[str]:
    parts = [p.split("/")[:-1] for p in paths] or [[]]
    out = []
    for level in zip(*parts):
        if len(set(level)) != 1:
            break
        out.append(level[0])
    return out[:depth - 1]


@register
class XzGitFeeder(Feeder):
    name = "xz-git"
    package = "xz-git"
    title = "xz-utils git history"

    def records(self):
        ctx = self.ctx
        repo = ctx.options.get("repo", REPO)
        name = repo.rstrip("/").split("/")[-1].removesuffix(".git")
        path = ctx.settings.cache_dir / self.name / f"{name}.git"
        if path.exists():
            subprocess.run(["git", "-C", str(path), "fetch", "--quiet", "origin", "+refs/heads/*:refs/heads/*"], check=True)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            subprocess.run(["git", "clone", "--quiet", "--bare", "--filter=blob:none", repo, str(path)], check=True)
        fmt = SEP.join(["%H", "%at", "%ct", "%an", "%ae", "%cn", "%ce", "%s"])
        log = subprocess.run(["git", "-C", str(path), "log", "--all", "--reverse", "--date-order", f"--pretty=format:@@@{fmt}",
                              "--name-only", "--no-renames"], check=True, capture_output=True, text=True, errors="replace").stdout
        src = ctx.source("src:tukaani:xz-git")
        last = ctx.cursor.get("last_commit")
        skipping = bool(last) and ctx.since is None
        for chunk in log.split("@@@")[1:]:
            head, *files = chunk.strip("\n").split("\n")
            sha, at, ct, an, ae, cn, ce, subject = head.split(SEP)
            if skipping:
                skipping = sha != last
                continue
            authored = datetime.fromtimestamp(int(at), timezone.utc)
            committed = max(authored, datetime.fromtimestamp(int(ct), timezone.utc))
            files = [f for f in files if f.strip()]
            parts = []
            for role, n, e in (("author", an, ae), ("committer", cn, ce)):
                pid = person_id(e)
                if ctx.once(pid):
                    yield entity(pid, "Person", n or "unknown", src, committed, "proxy_event_end")
                parts.append(participant(pid, role))
            folder = common_folder(files)
            yield event(f"evt:xz-git:{sha}", "CODE.COMMIT", src, time_spec(authored, "second"),
                        logical("code.repo", name, *folder), committed, "proxy_event_end", participants=parts,
                        measures=measures(("files_changed", len(files), "count")), text=text(subject, lang=None),
                        attributes=attributes(commit=sha, areas=sorted({f.split("/")[0] for f in files})[:50]),
                        evidence={"url": f"https://github.com/tukaani-project/xz/commit/{sha}"})
            last = sha
        yield Checkpoint({"last_commit": last, "repo": repo})
