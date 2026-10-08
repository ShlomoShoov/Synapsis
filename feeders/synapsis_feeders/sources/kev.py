"""CISA Known Exploited Vulnerabilities: the whole catalog, every run (about 1,500 entries; edits become versions).

Records per entry: the vulnerability (shared namespace cve), the vendor and the product (entities), PRODUCT_OF
(relation), and the catalog event. An entity is known from the first day the catalog named it."""
from __future__ import annotations

from ..base import Checkpoint, Feeder, register
from ..fetch import SourceError
from ..sil import attributes, entity, event, logical, participant, relation, slug, text, time_spec, utc
from datetime import timedelta

URL = "https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json"
MIRROR = "https://raw.githubusercontent.com/cisagov/kev-data/develop/known_exploited_vulnerabilities.json"
CATALOG_PAGE = "https://www.cisa.gov/known-exploited-vulnerabilities-catalog"


def vendor_id(vendor: str) -> str:
    return f"ent:kev-vendor:{slug(vendor)}"


def product_id(vendor: str, product: str) -> str:
    return f"ent:kev-product:{slug(vendor)}/{slug(product)}"


@register
class KevFeeder(Feeder):
    name = "kev"
    package = "kev"
    title = "CISA Known Exploited Vulnerabilities catalog"

    def records(self):
        ctx = self.ctx
        src = ctx.source("src:cisa:kev")
        try:
            catalog = ctx.http.json(URL)
        except SourceError as exc:
            ctx.log.warning("CISA feed failed (%s); using the cisagov/kev-data mirror", exc)
            catalog = ctx.http.json(MIRROR)
        version = catalog["catalogVersion"]
        if ctx.cursor.get("catalog_version") == version and ctx.since is None:
            ctx.log.info("catalog %s already loaded; nothing to send", version)
            return
        entries = sorted(catalog["vulnerabilities"], key=lambda v: (v["dateAdded"], v["cveID"]))

        # an entity is known from the first day any entry named it (computed over the whole catalog)
        first_vendor, first_product = {}, {}
        for v in entries:
            vend, prod = v["vendorProject"].strip(), v["product"].strip()
            first_vendor.setdefault(vendor_id(vend), (v["dateAdded"], vend))
            first_product.setdefault(product_id(vend, prod), (v["dateAdded"], prod, vend))

        for v in entries:
            added = utc(v["dateAdded"])
            if (ctx.since and added.date() < ctx.since) or (ctx.until and added.date() > ctx.until):
                continue
            known = added + timedelta(days=1)
            vend, prod, cve = v["vendorProject"].strip(), v["product"].strip(), v["cveID"].strip()
            vid, pid, cid = vendor_id(vend), product_id(vend, prod), f"ent:cve:{cve}"
            if ctx.once(cid):
                yield entity(cid, "Vulnerability", cve, src, known, "published")
            if ctx.once(vid):
                d, name = first_vendor[vid]
                yield entity(vid, "Vendor", name, src, utc(d) + timedelta(days=1), "published")
            if ctx.once(pid):
                d, name, _ = first_product[pid]
                pknown = utc(d) + timedelta(days=1)
                yield entity(pid, "Product", name, src, pknown, "published")
                yield relation(f"rel:kev:product-of/{pid.split(':', 2)[2]}", "PRODUCT_OF", src, pid, vid, pknown, "published")
            yield event(
                f"evt:kev:{cve}", "CYB.VULN.KNOWN_EXPLOITED", src, time_spec(added, "day"),
                logical("cyber.vendor_product", vend, prod), known, "published",
                participants=[participant(cid, "subject"), participant(pid, "affected_product"), participant(vid, "vendor")],
                text=text(v.get("vulnerabilityName"), v.get("shortDescription")),
                attributes=attributes(required_action=v.get("requiredAction"), due_date=v.get("dueDate"),
                                      known_ransomware_campaign_use=v.get("knownRansomwareCampaignUse"),
                                      cwes=[c for c in v.get("cwes") or [] if c], notes=v.get("notes")),
                evidence={"url": CATALOG_PAGE, "ref": cve})
        yield Checkpoint({"catalog_version": version, "date_released": catalog.get("dateReleased"), "entries": len(entries)})
