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
        private readonly PlacedObjectEntry _entry;
        private readonly int _voxelsPerBlockAxis;
        private readonly List<GameObject> _instances = new();
        private readonly List<(int x, int y, int z)> _previewBlocks = new();
        private readonly Dictionary<Material, Material> _validMaterialCache = new();
        private readonly Dictionary<Material, Material> _invalidMaterialCache = new();
        private readonly List<Collider> _collidersBuffer = new List<Collider>(16);
        private readonly List<MeshRenderer> _renderersBuffer = new List<MeshRenderer>(16);
        private const int MaxAreaPreviews = 256;

        public GameObject Prefab => _prefab;

        /// <summary>Blocks currently shown in the preview (for hiding environment underneath).</summary>
        public IReadOnlyList<(int x, int y, int z)> PreviewBlocks => _previewBlocks;

        public PlacementPreview(GameObject prefab, WorldScale worldScale, PlacedObjectEntry entry, int voxelsPerBlockAxis = 16)
        {
            _prefab = prefab;
            _worldScale = worldScale;
            _entry = entry;
            _voxelsPerBlockAxis = voxelsPerBlockAxis > 0 ? voxelsPerBlockAxis : 16;
        }

        /// <summary>Single placement: one instance at center of footprint. PreviewBlocks = all blocks in footprint.</summary>
        public void SetSingle(int originX, int originZ, int baseY, int sizeX, int sizeZ, float rotationY, bool valid)
        {
            Clear();
            if (_prefab == null || _entry == null) return;

            var (centerX, centerZ) = PlacementUtility.GetFootprintCenter(originX, originZ, sizeX, sizeZ);

            var instance = CreatePreviewInstance(centerX, baseY, centerZ, rotationY, valid, sizeX, sizeZ);
            if (instance != null)
            {
                _instances.Add(instance);
                PlacementUtility.GetFootprintBlocks(originX, originZ, baseY, sizeX, sizeZ, _previewBlocks);
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
                    var instance = CreatePreviewInstance(x + 0.5f, surfaceY, z + 0.5f, rot, true, 1, 1);
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

                var instance = CreatePreviewInstance(node.X + 0.5f, surfaceY, node.Z + 0.5f, 0f, valid, 1, 1);
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
                    var instance = CreatePreviewInstance(x + 0.5f, surfaceY, z + 0.5f, rot, valid, 1, 1);
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

        /// <summary>Releases cached materials and clears instances. Call when discarding the preview (e.g. switching prefab).</summary>
        public void Release()
        {
            Clear();
            foreach (var mat in _validMaterialCache.Values)
            {
                if (mat != null)
                    Object.Destroy(mat);
            }
            _validMaterialCache.Clear();
            foreach (var mat in _invalidMaterialCache.Values)
            {
                if (mat != null)
                    Object.Destroy(mat);
            }
            _invalidMaterialCache.Clear();
        }

        private GameObject CreatePreviewInstance(float centerX, float centerY, float centerZ, float rotationY, bool valid, int sizeX, int sizeZ)
        {
            if (_entry == null) return null;

            var instance = Object.Instantiate(_prefab);
            instance.name = _prefab.name + "_Preview";

            var bounds = PlacementUtility.GetPrefabBounds(_prefab, _entry.AreaSizeX, _entry.AreaSizeZ, _entry.HeightInBlocks, _voxelsPerBlockAxis);
            var scale = _worldScale.ScaleForVoxelModel(_entry.AreaSizeX, _entry.AreaSizeZ, _entry.HeightInBlocks, bounds);
            instance.transform.localScale = scale;

            var pos = _worldScale.BlockToWorld(centerX, centerY, centerZ);
            pos -= PlacementUtility.PivotOffsetForCenteringXZ(bounds, scale);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            ApplyPreviewMaterials(instance, valid);

            _collidersBuffer.Clear();
            instance.GetComponentsInChildren(false, _collidersBuffer);
            foreach (var col in _collidersBuffer)
                col.enabled = false;

            return instance;
        }

        private void ApplyPreviewMaterials(GameObject go, bool valid)
        {
            var cache = valid ? _validMaterialCache : _invalidMaterialCache;
            Color tint = valid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);

            _renderersBuffer.Clear();
            go.GetComponentsInChildren(false, _renderersBuffer);
            foreach (var mr in _renderersBuffer)
            {
                var source = mr.sharedMaterial;
                if (source == null) continue;
                if (!cache.TryGetValue(source, out var mat))
                {
                    mat = new Material(source);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", tint);
                    else
                        mat.color = tint;
                    mat.renderQueue = 3000;
                    if (mat.HasProperty("_Surface"))
                        mat.SetFloat("_Surface", 1f);
                    cache[source] = mat;
                }
                mr.sharedMaterial = mat;
            }
        }

        public void UpdateSingleTint(bool valid)
        {
            if (_instances.Count == 0) return;

            Color tint = valid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);

            foreach (var go in _instances)
            {
                if (go == null) continue;
                _renderersBuffer.Clear();
                go.GetComponentsInChildren(false, _renderersBuffer);
                foreach (var mr in _renderersBuffer)
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

        public void UpdateSinglePositionAndRotation(int originX, int originZ, int baseY, int sizeX, int sizeZ, float rotationY)
        {
            if (_instances.Count == 0) return;

            var (centerX, centerZ) = PlacementUtility.GetFootprintCenter(originX, originZ, sizeX, sizeZ);
            var pos = _worldScale.BlockToWorld(centerX, baseY, centerZ);
            var bounds = PlacementUtility.GetPrefabBounds(_prefab, _entry.AreaSizeX, _entry.AreaSizeZ, _entry.HeightInBlocks, _voxelsPerBlockAxis);
            var scale = _instances[0].transform.localScale;
            pos -= PlacementUtility.PivotOffsetForCenteringXZ(bounds, scale);
            _instances[0].transform.position = pos;
            _instances[0].transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }
}
