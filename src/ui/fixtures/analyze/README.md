# Analyze fixtures: real answers of the read API

Saved on 13 Oct 2026 from the local stack (`http://localhost:8080`, the local sample: 8,606 records; KEV in full,
about 200 records from each other source). Every file is the exact body the API returned, unchanged. Use them to build
and test the UI without the stores; Shlomo checks every stage against the real API. Tomorrow the real API holds all the
records (millions), so never rely on these sizes: always use `total`, `records.value` and `sum_other_doc_count`.

| File | Request |
| --- | --- |
| map-cells-r2-world.json | `GET /map/cells?resolution=2` |
| map-cells-r3-rat-islands-1965.json | `GET /map/cells?resolution=3&bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01` (a box across the 180th meridian) |
| map-points-rat-islands-1965.json | `GET /map/points?bbox=170,45,-170,55&from=1965-01-01&to=1965-04-01` |
| map-points-us-1970.json | `GET /map/points?bbox=-125,24,-66,50&from=1970-01-01&to=1971-01-01` (GTD attacks) |
| map-points-world.json | `GET /map/points?bbox=-180,-90,180,90` (every event with a point, all time) |
| graph-entity-log4j2.json | `GET /graph?id=ent:kev-product:apache/log4j2` |
| graph-event-cve-2021-44228.json | `GET /graph?id=evt:kev:CVE-2021-44228` |
| graph-event-ucdp-7578.json | `GET /graph?id=evt:ucdp:7578` (a conflict event and its two sides) |
| graph-hub-microsoft.json | `GET /graph?id=ent:kev-vendor:microsoft` (a hub: 200 links of total 460) |
| graph-not-found-gtd-197000000001.json | `GET /graph?id=evt:gtd:197000000001` → 404 (not every record is in the graph) |
| logical-top.json | `GET /logical` (the top level) |
| logical-cyber-vendor-product.json | `GET /logical?node=cyber.vendor_product` (the vendors) |
| logical-cyber-vendor-product-apache.json | `GET /logical?node=cyber.vendor_product/Apache` (Apache's products) |
| search-q-log4j.json | `GET /search?q=log4j` |
| search-semantic-rce.json | `GET /search?q=remote code execution&semantic=true` |
| search-source-kev.json | `GET /search?source=src:cisa:kev` (no q: the filtered list, newest first) |
| search-entity-log4j2.json | `GET /search?entity=ent:kev-product:apache/log4j2` (the events Log4j2 takes part in) |
| search-logical-apache.json | `GET /search?logical=cyber.vendor_product/Apache` (the records under a tree node) |
| search-kind-entity.json | `GET /search?kind=entity` |
| records-cve-2021-44228.json | `GET /records/evt:kev:CVE-2021-44228` (each source's full record, as stored) |
| records-log4j2.json | `GET /records/ent:kev-product:apache/log4j2` |
| records-ucdp-7578.json | `GET /records/evt:ucdp:7578` |
| catalog.json | `GET /catalog` (every source and type: the values for the source and type filters) |

**Conclusions (written by hand, 13 Oct 2026 night)**: the stack was being reloaded, so these follow the engine's
contract (EMERGENCY.md §12, "Conclusions in the read routes") with made-up numbers on real cells of the sample. Replace
them with saved answers once the API has conclusions.

| File | Request |
| --- | --- |
| map-points-conclusions.json | the map routes with `kind=conclusion` (answered over it like map-points-world.json) |
| search-kind-conclusion.json | `GET /search?kind=conclusion` |
| records-conclusion-forecast.json | `GET /records/con:engine:forecast:84bcf59ffffffff:1989-02` |
| records-conclusion-score.json | `GET /records/con:engine:forecast_score:ucdp-ged:1989` |
| graph-conclusion-forecast.json | `GET /graph?id=con:engine:forecast:84bcf59ffffffff:1989-02` (ABOUT the cell, PREMISE events and a spark) |
