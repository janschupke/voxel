#!/bin/bash
# Full MagicaVoxel → Unity pipeline:
# 1. Copy exports from MagicaVoxel to art/vox
# 2. Sync to Assets/Models (fix OBJ/MTL paths)
# 3. Open Unity and run Voxel > Generate Prefabs from Models

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

./magica.sh
./sync-vox-to-assets.sh

echo ""
echo "Pipeline complete. In Unity: Voxel > Generate Prefabs from Models"
