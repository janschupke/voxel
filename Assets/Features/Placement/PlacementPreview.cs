using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;
using Voxel.Pathfinding;

namespace Voxel
{
    /// <summary>Generic placement preview for any prefab. Supports single-block and area placement with valid/invalid tinting.</summary>
    public class PlacementPreview
    {
        private readonly GameObject _prefab;
        private readonly WorldScale _worldScale;
        private readonly float _prefabHeightInUnits;
        private readonly float _scaleMultiplier;
        private readonly List<GameObject> _instances = new();
        private readonly List<(int x, int y, int z)> _previewBlocks = new();
        private const int MaxAreaPreviews = 256;

        public GameObject Prefab => _prefab;

        /// <summary>Blocks currently shown in the preview (for hiding environment underneath).</summary>
        public IReadOnlyList<(int x, int y, int z)> PreviewBlocks => _previewBlocks;

        public PlacementPreview(GameObject prefab, WorldScale worldScale, float prefabHeightInUnits = 2f, float scaleMultiplier = 1f)
        {
            _prefab = prefab;
            _worldScale = worldScale;
            _prefabHeightInUnits = prefabHeightInUnits;
            _scaleMultiplier = scaleMultiplier;
        }

        public void SetSingle((int x, int y, int z) block, float rotationY, bool valid)
        {
            Clear();
            if (_prefab == null) return;

            var instance = CreatePreviewInstance(block, rotationY, valid);
            if (instance != null)
            {
                _instances.Add(instance);
                _previewBlocks.Add(block);
            }
        }

        public void SetArea((int x, int z) start, (int x, int z) end, VoxelGrid grid, int waterLevelY,
            System.Func<int, int, int, bool> isBlockValid, bool randomRotation)
        {
            Clear();
            if (_prefab == null || grid == null) return;

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            int count = 0;
            for (int x = minX; x <= maxX && count < MaxAreaPreviews; x++)
            {
                for (int z = minZ; z <= maxZ && count < MaxAreaPreviews; z++)
                {
                    if (x < 0 || x >= grid.Width || z < 0 || z >= grid.Depth) continue;

                    int topY = PlacementUtility.GetTopSolidY(grid, x, z, grid.Height);
                    if (topY < 0 || topY < waterLevelY) continue;

                    int surfaceY = topY + 1;
                    if (!isBlockValid(x, surfaceY, z)) continue;

                    float rot = randomRotation ? Random.Range(0f, 360f) : 0f;
                    var instance = CreatePreviewInstance((x, surfaceY, z), rot, true);
                    if (instance != null)
                    {
                        _instances.Add(instance);
                        _previewBlocks.Add((x, surfaceY, z));
                        count++;
                    }
                }
            }
        }

        public void SetLine((int x, int z) start, (int x, int z) end, VoxelGrid grid, int waterLevelY,
            System.Func<int, int, int, bool> isBlockValid,
            System.Func<int, int, int, bool> skipPreviewForBlock = null)
        {
            Clear();
            if (_prefab == null || grid == null) return;

            var graph = new SurfacePathGraph(grid, waterLevelY, isBlockValid);
            var path = PathBuilder.BuildPath(graph, new GridNode(start.x, start.z), new GridNode(end.x, end.z));
            if (path == null || path.Count == 0) return;

            int count = 0;
            foreach (var node in path)
            {
                if (count >= MaxAreaPreviews) break;

                int topY = PlacementUtility.GetTopSolidY(grid, node.X, node.Z, grid.Height);
                if (topY < 0) continue;

                int surfaceY = topY + 1;
                if (skipPreviewForBlock != null && skipPreviewForBlock(node.X, surfaceY, node.Z))
                    continue;

                bool valid = topY >= waterLevelY && isBlockValid(node.X, surfaceY, node.Z);

                var instance = CreatePreviewInstance((node.X, surfaceY, node.Z), 0f, valid);
                if (instance != null)
                {
                    _instances.Add(instance);
                    _previewBlocks.Add((node.X, surfaceY, node.Z));
                    count++;
                }
            }
        }

        public void SetAreaWithValidity((int x, int z) start, (int x, int z) end, VoxelGrid grid, int waterLevelY,
            System.Func<int, int, int, bool> isBlockValid, bool randomRotation)
        {
            Clear();
            if (_prefab == null || grid == null) return;

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            int count = 0;
            for (int x = minX; x <= maxX && count < MaxAreaPreviews; x++)
            {
                for (int z = minZ; z <= maxZ && count < MaxAreaPreviews; z++)
                {
                    if (x < 0 || x >= grid.Width || z < 0 || z >= grid.Depth) continue;

                    int topY = PlacementUtility.GetTopSolidY(grid, x, z, grid.Height);
                    if (topY < 0) continue;

                    int surfaceY = topY + 1;
                    bool valid = topY >= waterLevelY && isBlockValid(x, surfaceY, z);

                    float rot = randomRotation ? Random.Range(0f, 360f) : 0f;
                    var instance = CreatePreviewInstance((x, surfaceY, z), rot, valid);
                    if (instance != null)
                    {
                        _instances.Add(instance);
                        _previewBlocks.Add((x, surfaceY, z));
                        count++;
                    }
                }
            }
        }

        public void Clear()
        {
            foreach (var go in _instances)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _instances.Clear();
            _previewBlocks.Clear();
        }

        private GameObject CreatePreviewInstance((int x, int y, int z) block, float rotationY, bool valid)
        {
            var instance = Object.Instantiate(_prefab);
            instance.name = _prefab.name + "_Preview";

            var scale = _worldScale.ScaleVectorForBlockSizedPrefab(_prefabHeightInUnits) * _scaleMultiplier;
            instance.transform.localScale = scale;

            var pos = _worldScale.BlockToWorld(block.x + 0.5f, block.y, block.z + 0.5f);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            ApplyPreviewMaterials(instance, valid);

            foreach (var col in instance.GetComponentsInChildren<Collider>())
                col.enabled = false;

            return instance;
        }

        private static void ApplyPreviewMaterials(GameObject go, bool valid)
        {
            Color tint = valid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.sharedMaterial == null) continue;
                var mat = new Material(mr.sharedMaterial);
                mr.sharedMaterial = mat;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", tint);
                else
                    mat.color = tint;
                mat.renderQueue = 3000;
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
            }
        }

        public void UpdateSingleTint(bool valid)
        {
            if (_instances.Count == 0) return;

            Color tint = valid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);

            foreach (var go in _instances)
            {
                if (go == null) continue;
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var mat = mr.sharedMaterial;
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", tint);
                    else
                        mat.color = tint;
                }
            }
        }

        public void UpdateSinglePositionAndRotation((int x, int y, int z) block, float rotationY)
        {
            if (_instances.Count == 0) return;

            var pos = _worldScale.BlockToWorld(block.x + 0.5f, block.y, block.z + 0.5f);
            _instances[0].transform.position = pos;
            _instances[0].transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }
}
