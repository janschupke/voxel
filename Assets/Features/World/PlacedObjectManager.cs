using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Manages placed objects (parents, save/load, blocking checks). Extracted from WorldBootstrap.
    /// </summary>
    public class PlacedObjectManager
    {
        private readonly Transform _root;
        private readonly Dictionary<string, Transform> _parentsByEntryName = new Dictionary<string, Transform>();
        private readonly RoadOverlay _roadOverlay = new RoadOverlay();

        private PlacedObjectRegistry _registry;
        private WorldParameters _worldParameters;
        private IslandPipelineConfig _islandPipelineConfig;
        private WaterConfig _waterConfig;

        public PlacedObjectManager(Transform root)
        {
            _root = root;
        }

        public void Initialize(PlacedObjectRegistry registry, WorldParameters worldParameters,
            IslandPipelineConfig islandPipelineConfig, WaterConfig waterConfig)
        {
            _registry = registry;
            _worldParameters = worldParameters;
            _islandPipelineConfig = islandPipelineConfig;
            _waterConfig = waterConfig;
        }

        public RoadOverlay RoadOverlay => _roadOverlay;

        public void Clear()
        {
            _roadOverlay.Clear();
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value.gameObject);
            }
            _parentsByEntryName.Clear();
        }

        public Transform GetOrCreateParentForEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return null;
            if (entryName == "Road") return null;
            if (_parentsByEntryName.TryGetValue(entryName, out var parent) && parent != null)
                return parent;
            var go = new GameObject(entryName + "s");
            go.transform.SetParent(_root);
            _parentsByEntryName[entryName] = go.transform;
            return go.transform;
        }

        public Transform GetParentByEntryName(string entryName)
        {
            return _parentsByEntryName.TryGetValue(entryName ?? "", out var p) ? p : null;
        }

        /// <summary>Returns the entry name for a placed object transform (e.g. building instance).</summary>
        public string GetEntryNameForTransform(Transform child)
        {
            if (child == null) return null;
            var parent = child.parent;
            if (parent == null) return null;
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == parent)
                    return kv.Key;
            }
            return null;
        }

        public void AddRoadAt(int x, int y, int z) => _roadOverlay.Add(x, y, z);
        public void RemoveRoadAt(int x, int y, int z) => _roadOverlay.Remove(x, y, z);
        public bool HasRoadAt(int x, int y, int z) => _roadOverlay.Contains(x, y, z);

        /// <summary>Removes all placed objects and roads at the given block. Returns true if anything was removed.</summary>
        public bool RemoveAtBlock(int bx, int by, int bz)
        {
            bool removed = false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == null) continue;
                var toRemove = new List<Transform>();
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        toRemove.Add(child);
                }
                foreach (var t in toRemove)
                {
                    Object.Destroy(t.gameObject);
                    removed = true;
                }
            }
            if (_roadOverlay.Contains(bx, by, bz))
            {
                _roadOverlay.Remove(bx, by, bz);
                removed = true;
            }
            return removed;
        }

        /// <summary>Returns all placed object transforms at the given block (buildings, trees). Does not include roads.</summary>
        public void GetTransformsAtBlock(int bx, int by, int bz, List<Transform> outTransforms)
        {
            outTransforms.Clear();
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        outTransforms.Add(child);
                }
            }
        }

        /// <summary>Returns true if there is anything removable at the block (building, tree, or road).</summary>
        public bool HasRemovableAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        return true;
                }
            }
            return false;
        }

        public bool HasBlockingObjectAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            if (_registry == null) return false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var entry in _registry.Entries)
            {
                if (entry == null || !entry.IsBlocking) continue;
                if (!_parentsByEntryName.TryGetValue(entry.Name, out var parent) || parent == null) continue;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var (hx, hy, hz) = worldScale.WorldToBlock(parent.GetChild(i).position);
                    if (hx == bx && hy == by && hz == bz) return true;
                }
            }
            return false;
        }

        public bool HasEntryAtBlock(string entryName, int bx, int by, int bz)
        {
            if (string.IsNullOrEmpty(entryName)) return false;
            if (entryName == "Road") return _roadOverlay.Contains(bx, by, bz);
            if (!_parentsByEntryName.TryGetValue(entryName, out var parent) || parent == null)
                return false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            for (int i = 0; i < parent.childCount; i++)
            {
                var (hx, hy, hz) = worldScale.WorldToBlock(parent.GetChild(i).position);
                if (hx == bx && hy == by && hz == bz) return true;
            }
            return false;
        }

        public List<PlacedObjectData> CollectPlacedObjectsForSave()
        {
            if (_registry == null) return null;

            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            var list = new List<PlacedObjectData>();

            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    var (bx, by, bz) = worldScale.WorldToBlock(child.position);
                    list.Add(new PlacedObjectData(kv.Key, bx, by, bz, child.eulerAngles.y));
                }
            }

            foreach (var (x, y, z) in _roadOverlay.GetAllBlocks())
            {
                list.Add(new PlacedObjectData("Road", x, y, z, 0f));
            }

            return list.Count > 0 ? list : null;
        }

        public void LoadPlacedObjects(IReadOnlyList<PlacedObjectData> placedObjects, VoxelGrid grid, TerrainGenerationMode terrainMode)
        {
            if (_registry == null || placedObjects == null || placedObjects.Count == 0)
            {
                if (terrainMode == TerrainGenerationMode.IslandPipeline && _islandPipelineConfig != null)
                    RunTreePlacement(grid);
                return;
            }

            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);

            foreach (var p in placedObjects)
            {
                if (p.EntryName == "Road")
                {
                    _roadOverlay.Add(p.BlockX, p.BlockY, p.BlockZ);
                    continue;
                }

                var entry = _registry.GetByName(p.EntryName);
                var prefab = entry?.Prefab ?? (p.EntryName == "Tree" ? _islandPipelineConfig?.TreeScatterConfig?.TreePrefab : null);
                if (prefab == null) continue;

                var parent = GetOrCreateParentForEntry(p.EntryName);
                if (parent == null) continue;

                float prefabHeight = entry != null && entry.PrefabHeightInUnits > 0 ? entry.PrefabHeightInUnits : 2f;
                float scaleMult = entry != null && entry.ScaleMultiplier > 0 ? entry.ScaleMultiplier : 1f;
                var scale = worldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

                var instance = Object.Instantiate(prefab, p.ToWorldPosition(worldScale), p.ToRotation(), parent);
                instance.name = prefab.name;
                instance.transform.localScale = scale;
                if (entry != null && entry.InventoryCapacity > 0)
                {
                    var inv = instance.GetComponent<BuildingInventory>();
                    if (inv == null) inv = instance.AddComponent<BuildingInventory>();
                    inv.Initialize(p.EntryName, entry.InventoryCapacity);
                }
            }

            if (terrainMode == TerrainGenerationMode.IslandPipeline && _islandPipelineConfig != null)
            {
                var treeParent = GetParentByEntryName("Tree");
                if (treeParent == null || treeParent.childCount == 0)
                    RunTreePlacement(grid);
            }
        }

        private void RunTreePlacement(VoxelGrid grid)
        {
            if (_islandPipelineConfig == null) return;

            var treeConfig = _islandPipelineConfig.TreeScatterConfig;
            if (treeConfig == null || treeConfig.TreePrefab == null) return;

            int height = grid.Height;
            int waterLevelY = _waterConfig != null
                ? _waterConfig.GetWaterLevelY(height)
                : Mathf.Clamp(15, 0, height - 1);

            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            var treeParent = GetOrCreateParentForEntry("Tree");
            var heightBuffer = new HeightBuffer(grid.Width, grid.Depth);
            var context = new TerrainPipelineContext(heightBuffer, grid, waterLevelY, _islandPipelineConfig.MasterSeed);
            var stage = new TreeScatterStage(treeConfig, treeParent, worldScale);
            stage.Execute(context);
        }
    }
}
