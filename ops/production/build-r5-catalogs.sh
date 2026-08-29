#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ $# -ne 5 ]]; then
  echo 'usage: build-r5-catalogs.sh OLD_PRIVATE OLD_VISUAL NEW_PRIVATE_901 NEW_PUBLIC_901 OUTPUT_DIR' >&2
  exit 2
fi
old_private=$(realpath --canonicalize-existing "$1")
old_visual=$(realpath --canonicalize-existing "$2")
new_private=$(realpath --canonicalize-existing "$3")
new_public=$(realpath --canonicalize-existing "$4")
output=$5
mkdir -p "$output/private" "$output/public"
chmod 0700 "$output" "$output/private" "$output/public"

private_output="$output/private/catalog.full.902.private.json"
visual_output="$output/public/catalog.visual.902.json"
delta_output="$output/public/delta.52.visual.json"

jq -S --slurpfile incoming "$new_private" '
  .enableTestDownloads=false | .testDownload=null |
  .items=($incoming[0].items + [.items[] |
    select(.id=="23e20ab4c26cadc22f631d2bdd724377")])
' "$old_private" > "$private_output"

jq -S --slurpfile incoming "$new_public" '
  (.items | map(.id)) as $existing |
  .items += [$incoming[0].items[] | select(.id as $id | $existing | index($id) | not)] |
  .categories = [.categories[] as $category |
    $category + {sourceItemCount:([.items[] | select(.categoryId==$category.id)]|length)}]
' "$old_visual" > "$visual_output"

jq -S --slurpfile old "$old_visual" '
  ($old[0].items | map(.id)) as $existing |
  {schemaVersion:.schemaVersion,items:[.items[] |
    select(.id as $id | $existing | index($id) | not)]}
' "$visual_output" > "$delta_output"

chmod 0600 "$private_output" "$visual_output" "$delta_output"

jq -e '(.items|length)==902 and ([.items[].id]|unique|length)==902 and
  ([.items[].id]|index("23e20ab4c26cadc22f631d2bdd724377"))!=null and
  ([.items[].title|ascii_downcase]|index("teste demo"))==null' "$private_output" >/dev/null
jq -e '(.items|length)==902 and ([.items[].id]|unique|length)==902 and
  ([.items[].id]|index("23e20ab4c26cadc22f631d2bdd724377"))!=null and
  ([.items[].title|ascii_downcase]|index("teste demo"))==null' "$visual_output" >/dev/null
jq -e '(.items|length)==52 and ([.items[].id]|unique|length)==52' "$delta_output" >/dev/null

private_ids=$(jq -r '.items[].id' "$private_output" | sort)
visual_ids=$(jq -r '.items[].id' "$visual_output" | sort)
[[ "$private_ids" == "$visual_ids" ]]
if jq -e '.. | strings | select(test("https?://";"i"))' "$visual_output" >/dev/null; then
  echo 'BLOCKED: public visual catalog contains a locator.' >&2
  exit 1
fi
sha256sum "$private_output" "$visual_output" "$delta_output"
echo 'R5_CATALOG_BUILD_OK items=902 delta=52 private_urls=not_printed'
