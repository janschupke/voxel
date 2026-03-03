using UnityEngine;
using Voxel.Core;

namespace Voxel.Rendering
{
    public class ChunkRenderer : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;

        public void Initialize(Material[] materials)
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterials = materials;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _meshRenderer.receiveShadows = true;
        }

        public void SetMesh(Mesh mesh)
        {
            if (_mesh != null)
                Destroy(_mesh);
            _mesh = mesh;
            _meshFilter.sharedMesh = mesh;
        }

        public void SetPosition(int chunkX, int chunkY, int chunkZ, float voxelScale = 1f)
        {
            float s = ChunkMeshBuilder.ChunkSize * voxelScale;
            transform.position = new Vector3(chunkX * s, chunkY * s, chunkZ * s);
        }
    }
}
