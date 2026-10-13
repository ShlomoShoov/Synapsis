#!/usr/bin/env bash
# Builds the Analyze map's own basemap (assets/geo/*.json) from Natural Earth (public domain), so the globe draws
# with no tile server: the oceans, lakes, rivers, borders and the label points. Land is what the oceans leave: the
# relief shading (from the terrain tiles) is drawn under the oceans, so it shows on land only. Run it again only to change what the files
# hold; the output is committed.
#
#   bash scripts/make-basemap.sh            (from src/ui; needs curl, unzip and npx)
#
# Coordinates keep 3 decimals (about 100 m) and lines are simplified, which is finer than the scales the files are
# drawn at. Field names are shortened to what the style reads (src/analyze/map/basemap.ts).
set -euo pipefail

here="$(cd "$(dirname "$0")/.." && pwd)"
out="$here/assets/geo"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$out"

fetch() { # fetch <scale>_<theme>/<name>: download and unzip one Natural Earth layer into $work/<name>/
  local name="${1#*/}"
  curl -fsSL "https://naturalearth.s3.amazonaws.com/$1.zip" -o "$work/$name.zip"
  mkdir -p "$work/$name" && unzip -oq "$work/$name.zip" -d "$work/$name"
}

for layer in 110m_physical/ne_110m_ocean 50m_physical/ne_50m_ocean 10m_physical/ne_10m_ocean \
  10m_physical/ne_10m_lakes 50m_physical/ne_50m_rivers_lake_centerlines \
  10m_cultural/ne_10m_admin_0_boundary_lines_land 10m_cultural/ne_10m_admin_1_states_provinces_lines \
  50m_cultural/ne_50m_admin_0_countries 10m_cultural/ne_10m_populated_places_simple \
  10m_physical/ne_10m_geography_marine_polys; do
  fetch "$layer"
done

ms() { npx --yes mapshaper@0.7.84 "$@"; }
shp() { echo "$work/$1/$1.shp"; }
json='format=geojson precision=0.001'

# Oceans: three scales, so the whole globe draws light and a close view stays true to the coast.
ms "$(shp ne_110m_ocean)" -drop fields='*' -o "$out/ocean-110m.json" $json
ms "$(shp ne_50m_ocean)" -simplify 50% keep-shapes -drop fields='*' -o "$out/ocean-50m.json" $json
ms "$(shp ne_10m_ocean)" -simplify 20% keep-shapes -drop fields='*' -o "$out/ocean-10m.json" $json
