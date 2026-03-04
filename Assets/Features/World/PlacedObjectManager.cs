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
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        toRemove.Add(child);
                }
                foreach (var t in toRemove)
                {
                    Object.DestroyImmediate(t.gameObject);
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
                    if (child == null) continue;
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
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Returns true if placement is blocked (road or building). Used for placement validation.</summary>
        public bool HasBlockingObjectAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            if (_registry == null) return false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var entry in _registry.Entries)
            {
                if (entry == null) continue;
                if (entry.StructureType == Pure.StructureType.Road) continue; // Road is in overlay, not parents
                if (!entry.BlocksPlacement && !entry.IsBlocking) continue; // Backward compat: IsBlocking
                if (!_parentsByEntryName.TryGetValue(entry.Name, out var parent) || parent == null) continue;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz) return true;
                }
            }
            return false;
        }

        /// <summary>Returns true if actor pathing is blocked. Only buildings block; roads and environment are walkable.</summary>
        public bool BlocksPathingAtBlock(int bx, int by, int bz)
        {
            if (_registry == null) return false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var entry in _registry.Entries)
            {
                if (entry == null || !entry.BlocksPathing) continue;
                if (!_parentsByEntryName.TryGetValue(entry.Name, out var parent) || parent == null) continue;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz) return true;
                }
            }
            return false;
        }

        /// <summary>Returns true if any environment object (Tree, Wheat, etc.) is at the block.</summary>
        public bool HasEnvironmentAtBlock(int bx, int by, int bz)
        {
            if (_registry == null) return false;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var entry in _registry.Entries)
            {
                if (entry == null || entry.StructureType != Pure.StructureType.Environment) continue;
                if (!_parentsByEntryName.TryGetValue(entry.Name, out var parent) || parent == null) continue;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz) return true;
                }
            }
            return false;
        }

        /// <summary>Removes all environment objects at the block. Used when placing over environment.</summary>
        public void RemoveEnvironmentAtBlock(int bx, int by, int bz)
        {
            if (_registry == null) return;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var entry in _registry.Entries)
            {
                if (entry == null || entry.StructureType != Pure.StructureType.Environment) continue;
                if (!_parentsByEntryName.TryGetValue(entry.Name, out var parent) || parent == null) continue;
                var toRemove = new List<Transform>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child == null) continue;
                    var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                    if (hx == bx && hy == by && hz == bz)
                        toRemove.Add(child);
                }
                foreach (var t in toRemove)
                    Object.DestroyImmediate(t.gameObject);
            }
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
                var child = parent.GetChild(i);
                if (child == null) continue;
                var (hx, hy, hz) = worldScale.WorldToBlock(child.position);
                if (hx == bx && hy == by && hz == bz)
                    return true;
            }
            return false;
        }

        public List<PlacedObjectData> CollectPlacedObjectsForSave()
        {
            return PlacedObjectPersistence.CollectPlacedObjectsForSave(_parentsByEntryName, _registry, _roadOverlay, _worldParameters);
        }

        public List<BuildingInventorySaveData> CollectBuildingInventoriesForSave()
        {
            return PlacedObjectPersistence.CollectBuildingInventoriesForSave(_parentsByEntryName, _registry, _worldParameters);
        }

        public void LoadPlacedObjects(IReadOnlyList<PlacedObjectData> placedObjects, VoxelGrid grid, TerrainGenerationMode terrainMode,
            IReadOnlyDictionary<(string EntryName, int BlockX, int BlockY, int BlockZ), List<(Item Item, int Count)>> inventoryLookup = null)
        {
            PlacedObjectPersistence.LoadPlacedObjects(
                placedObjects, grid, terrainMode, inventoryLookup,
                _registry, _worldParameters, _islandPipelineConfig,
                GetOrCreateParentForEntry, GetParentByEntryName,
                (x, y, z) => _roadOverlay.Add(x, y, z),
                RunTreePlacement);
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
