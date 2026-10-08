"""Template for a new feeder. Copy to sources/<name>.py, write schemas/seed/<package>.yaml, import the module in
sources/__init__.py, add a service to docker-compose.yml. Nothing else in the framework changes.

Checklist (the API enforces most of it and says which record broke which rule):
  * ids from the source's own identifiers, so a re-run sends the same ids (and gets 'unchanged')
  * time with the precision the source really has; known_at by the rule written in the package, with its basis
  * a value the source does not give is left out, never sent as zero (M-NULL)
  * nothing fixed silently: send what the source says and let the API reject what breaks a rule
  * an entity goes out with the first record that names it and is known no later than that record (walk the source
    oldest first); where pages spell one name differently, keep the first spelling, or every re-read makes a version
  * a Checkpoint after each unit of work (a day, a file, a page), carrying everything the next run needs
  * anything else remembered between runs goes in ctx.save_memo, called after the Checkpoint it belongs to"""
from __future__ import annotations

from datetime import date, timedelta

from ..base import Checkpoint, Feeder  # add `register` to the import and decorate the class to enable it
from ..sil import event, measures, point, time_spec, utc

API = "https://example.org/api/items"


class TemplateFeeder(Feeder):
    name = "template"                 # the CLI name: python -m synapsis_feeders run template
    package = "template"              # schemas/seed/template.yaml
    title = "Short description shown by 'list'"
    keys = ()                         # environment variables the source needs, for example ("EXAMPLE_KEY",)
    requires = ()                     # shared packages to register first, for example ("health",)
    min_interval = 1.0                # seconds between requests to the source

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:example:items")            # reliability, basis and lag come from the package
        last = ctx.cursor.get("last_day")
        day = ctx.since or (date.fromisoformat(last) + timedelta(days=1) if last else date(2024, 1, 1))
        end = ctx.until or ctx.started_at.date()
        while day <= end:
            for row in ctx.http.json(API, params={"date": day.isoformat()}) or []:
                t = utc(row["time"])
                yield event(f"evt:example:{row['id']}", "EXAMPLE.EVENT", src, time_spec(t, "second"),
                            point(row["lon"], row["lat"], 5.0), t + timedelta(days=src["publication_lag_days"]),
                            "proxy_publication_lag", measures=measures(("count", row.get("n"), "count")))
            yield Checkpoint({"last_day": day.isoformat()})
            day += timedelta(days=1)
