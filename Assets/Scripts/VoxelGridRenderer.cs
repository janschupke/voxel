using UnityEngine;
using Voxel.Core;
using Voxel.Rendering;

namespace Voxel
{
    public class VoxelGridRenderer : MonoBehaviour
    {
        [SerializeField] private Material chunkMaterial;

        private ChunkManager _chunkManager;
        private VoxelGrid _grid;
        private Transform _chunkParent;

        public void Initialize(VoxelGrid grid)
        {
            if (_chunkParent != null)
                Destroy(_chunkParent.gameObject);

            _grid = grid;

            Material mat = chunkMaterial;
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            }

            _chunkParent = new GameObject("VoxelWorld").transform;
            _chunkManager = new ChunkManager(_grid, _chunkParent, mat);
            _chunkManager.BuildAllChunks();
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
