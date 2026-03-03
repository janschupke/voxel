using UnityEngine;
using Voxel.Core;
using Voxel.Rendering;

namespace Voxel
{
    public class VoxelGridRenderer : MonoBehaviour
    {
        [SerializeField] private Material chunkMaterial;
        [SerializeField] private TerrainMaterialConfig terrainMaterialConfig;
        [SerializeField] private WaterConfig waterConfig;

        private ChunkManager _chunkManager;
        private VoxelGrid _grid;
        private Transform _chunkParent;

        public void Initialize(VoxelGrid grid, WorldParameters worldParameters = null, Material mountainMaterial = null)
        {
            if (_chunkParent != null)
                Destroy(_chunkParent.gameObject);

            _grid = grid;
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);

            Material mat = chunkMaterial;
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            }

            _chunkParent = new GameObject("VoxelWorld").transform;
            _chunkManager = new ChunkManager(_grid, _chunkParent, mat, worldScale, terrainMaterialConfig, waterConfig, mountainMaterial);
            _chunkManager.BuildAllChunks();

            var debugger = GetComponent<Voxel.Debug.TerrainMaterialDebugger>();
            if (debugger != null)
                debugger.OnTerrainInitialized(_grid, _chunkManager, terrainMaterialConfig);
        }

        public void InvalidateChunkAt(int x, int y, int z)
        {
            _chunkManager?.InvalidateChunkAt(x, y, z);
        }

        public void InvalidateAll()
        {
            _chunkManager?.InvalidateAll();
        }

        private void Update()
        {
            _chunkManager?.RebuildDirtyChunks();
        }
    }
}
