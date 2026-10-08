"""KEV product classes: 720 labels a teacher model (Claude) produced once in round 3, kept as what they are.

Each label is a classification conclusion about one product, in the closed space cyber.product_class (rule A-CLOSED:
the API refuses a class that is not registered). Its premise is the product's first catalog entry, so the feeder
first waits until Synapsis holds the KEV catalog (the kev feeder sends it), up to --set wait_minutes=120. The labels
are a fixed dataset shipped in feeders/data; the feeder sends them once."""
from __future__ import annotations

import json
from pathlib import Path

from ..base import Checkpoint, Feeder, register
from ..sil import conclusion
from .kev import product_id

DATA = Path(__file__).resolve().parents[2] / "data" / "kev_product_classes.json"
LABELLED_ON = "2026-10-02"


@register
class KevClassesFeeder(Feeder):
    name = "kev-classes"
    package = "kev-classes"
    title = "KEV product classes labelled once by a teacher model"

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:synapsis:kev-classes")
        labels = json.loads(DATA.read_text())
        catalog = ctx.http.json("https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json")
        first_entry = {}
        for v in sorted(catalog["vulnerabilities"], key=lambda v: (v["dateAdded"], v["cveID"])):
            first_entry.setdefault(product_id(v["vendorProject"].strip(), v["product"].strip()), v["cveID"].strip())
        wanted = sorted({f"evt:kev:{cve}" for cve in first_entry.values()} | set(first_entry))
        sample = wanted[:3] + wanted[-3:]
        ctx.wait_for_records(sample, float(ctx.options.get("wait_minutes", 120)), "the KEV catalog")
        missing = 0
        for key, cls in sorted(labels.items()):
            vendor, product = key.split("|", 1)
            pid = product_id(vendor.strip(), product.strip())
            if pid not in first_entry:
                missing += 1
                continue
            yield conclusion(
                f"con:kev-classes:{pid.split(':', 2)[2]}", "classification", src, [pid],
                {"predicate": "in_logical_space", "object": f"cyber.product_class/{cls}"},
                [f"evt:kev:{first_entry[pid]}"],
                {"by": "agent", "name": "teacher:claude-sonnet-5-5", "version": "r3"}, 90,
                f"{LABELLED_ON}T00:00:00Z", "observed")
        if missing:
            ctx.log.warning("%d labelled products no longer appear in the catalog", missing)
        yield Checkpoint({"labels": len(labels), "missing": missing})
