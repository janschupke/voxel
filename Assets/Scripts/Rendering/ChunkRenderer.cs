using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel.Rendering
{
    public class ChunkRenderer : MonoBehaviour
    {
        private readonly List<MeshFilter> _meshFilters = new();
        private readonly List<MeshRenderer> _meshRenderers = new();
        private readonly List<Mesh> _meshes = new();

        public void Initialize(Material[] materials)
        {
            while (_meshFilters.Count < materials.Length)
            {
                var child = new GameObject($"Band_{_meshFilters.Count}");
                child.transform.SetParent(transform, false);
                var mf = child.AddComponent<MeshFilter>();
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterial = materials[_meshFilters.Count];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                _meshFilters.Add(mf);
                _meshRenderers.Add(mr);
                _meshes.Add(null);
            }
            while (_meshFilters.Count > materials.Length)
            {
                var last = _meshFilters.Count - 1;
                if (_meshes[last] != null) Destroy(_meshes[last]);
                Destroy(_meshFilters[last].gameObject);
                _meshFilters.RemoveAt(last);
                _meshRenderers.RemoveAt(last);
                _meshes.RemoveAt(last);
            }
            for (int i = 0; i < materials.Length; i++)
                _meshRenderers[i].sharedMaterial = materials[i];
        }

        public void SetMeshes(Mesh[] meshes)
        {
            if (meshes == null) return;
            for (int i = 0; i < meshes.Length && i < _meshFilters.Count; i++)
            {
                if (i < _meshes.Count && _meshes[i] != null)
                    Destroy(_meshes[i]);
                while (_meshes.Count <= i)
                    _meshes.Add(null);
                _meshes[i] = meshes[i];
                _meshFilters[i].sharedMesh = meshes[i] != null ? meshes[i] : null;
            }
        }

        public void SetPosition(int chunkX, int chunkY, int chunkZ, float voxelScale = 1f)
        {
            float s = ChunkMeshBuilder.ChunkSize * voxelScale;
            transform.position = new Vector3(chunkX * s, chunkY * s, chunkZ * s);
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes)
                if (m != null) Destroy(m);
        }
    }
}
