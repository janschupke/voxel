#!/bin/bash
# Syncs MagicaVoxel exports (OBJ + MTL + PNG) from art/vox/exports to Assets/Features/Structures/Models.
# Auto-discovers all *.vox.obj files. Fixes OBJ mtllib and MTL map_Kd paths so Unity finds assets.
#
# Model name = PascalCase of export base (e.g. tree.vox.obj -> Tree)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
EXPORTS="$PROJECT_ROOT/art/vox/exports"
MODELS="$PROJECT_ROOT/Assets/Features/Structures/Models"

mkdir -p "$MODELS"

# PascalCase: first char upper, rest unchanged
pascal_case() {
  local s="$1"
  if [[ -z "$s" ]]; then echo ""; return; fi
  echo "$(echo "${s:0:1}" | tr '[:lower:]' '[:upper:]')${s:1}"
}

count=0
for src_obj in "$EXPORTS"/*.vox.obj; do
  [[ -f "$src_obj" ]] || continue

  base=$(basename "$src_obj" .vox.obj)
  unity=$(pascal_case "$base")

  src_mtl="$EXPORTS/${base}.vox.mtl"
  src_png="$EXPORTS/${base}.vox.png"

  dst_obj="$MODELS/${unity}.obj"
  dst_mtl="$MODELS/${unity}.mtl"
  dst_png="$MODELS/${unity}.png"

  sed "s/mtllib ${base}\\.vox\\.mtl/mtllib ${unity}.mtl/g" "$src_obj" > "$dst_obj"
  echo "Copied and fixed $src_obj -> $dst_obj"

  if [[ -f "$src_png" ]]; then
    cp "$src_png" "$dst_png"
    echo "Copied $src_png -> $dst_png"
  fi

  if [[ -f "$src_mtl" ]]; then
    sed "s/map_Kd ${base}\\.vox\\.png/map_Kd ${unity}.png/g" "$src_mtl" > "$dst_mtl"
    echo "Copied and fixed $src_mtl -> $dst_mtl"
  fi

  ((count++)) || true
done

echo "Sync complete: $count model(s). Reimport in Unity or run Voxel > Generate Prefabs from Models."
