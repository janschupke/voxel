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
        private readonly Dictionary<(int x, int y, int z), List<Transform>> _blockToTransforms = new Dictionary<(int x, int y, int z), List<Transform>>();

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
            _blockToTransforms.Clear();
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value.gameObject);
            }
            _parentsByEntryName.Clear();
        }

        /// <summary>Registers a placed object in the block spatial index. Call after instantiating.</summary>
        public void RegisterPlacedObject(string entryName, Transform transform)
        {
            if (transform == null || string.IsNullOrEmpty(entryName) || entryName == "Road") return;
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            var (bx, by, bz) = worldScale.WorldToBlock(transform.position);
            var key = (bx, by, bz);
            if (!_blockToTransforms.TryGetValue(key, out var list))
            {
                list = new List<Transform>();
                _blockToTransforms[key] = list;
            }
            list.Add(transform);
        }

        private void UnregisterTransformAtBlock(int bx, int by, int bz, Transform transform)
        {
            var key = (bx, by, bz);
            if (_blockToTransforms.TryGetValue(key, out var list))
            {
                list.Remove(transform);
                if (list.Count == 0)
                    _blockToTransforms.Remove(key);
            }
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
            var key = (bx, by, bz);
            if (_blockToTransforms.TryGetValue(key, out var list))
            {
                foreach (var t in list)
                {
                    if (t != null)
                        Object.Destroy(t.gameObject);
                    removed = true;
                }
                _blockToTransforms.Remove(key);
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
            if (_blockToTransforms.TryGetValue((bx, by, bz), out var list))
            {
                foreach (var t in list)
                {
                    if (t != null)
                        outTransforms.Add(t);
                }
            }
        }

        /// <summary>Returns true if there is anything removable at the block (building, tree, or road).</summary>
        public bool HasRemovableAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            return _blockToTransforms.TryGetValue((bx, by, bz), out var list) && list.Count > 0;
        }

        /// <summary>Returns true if placement is blocked (road or building). Used for placement validation.</summary>
        public bool HasBlockingObjectAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            if (_registry == null || !_blockToTransforms.TryGetValue((bx, by, bz), out var list)) return false;
            foreach (var t in list)
            {
                if (t == null) continue;
                var entryName = GetEntryNameForTransform(t);
                var entry = string.IsNullOrEmpty(entryName) ? null : _registry.GetByName(entryName);
                if (entry != null && (entry.BlocksPlacement || entry.IsBlocking))
                    return true;
            }
            return false;
        }

        /// <summary>Returns true if actor pathing is blocked. Only buildings block; roads and environment are walkable.</summary>
        public bool BlocksPathingAtBlock(int bx, int by, int bz)
        {
            if (_registry == null || !_blockToTransforms.TryGetValue((bx, by, bz), out var list)) return false;
            foreach (var t in list)
            {
                if (t == null) continue;
                var entryName = GetEntryNameForTransform(t);
                var entry = string.IsNullOrEmpty(entryName) ? null : _registry.GetByName(entryName);
                if (entry != null && entry.BlocksPathing)
                    return true;
            }
            return false;
        }

        /// <summary>Returns true if any environment object (Tree, Wheat, etc.) is at the block.</summary>
        public bool HasEnvironmentAtBlock(int bx, int by, int bz)
        {
            if (_registry == null || !_blockToTransforms.TryGetValue((bx, by, bz), out var list)) return false;
            foreach (var t in list)
            {
                if (t == null) continue;
                var entryName = GetEntryNameForTransform(t);
                var entry = string.IsNullOrEmpty(entryName) ? null : _registry.GetByName(entryName);
                if (entry != null && entry.StructureType == Pure.StructureType.Environment)
                    return true;
            }
            return false;
        }

        /// <summary>Removes all environment objects at the block. Used when placing over environment.</summary>
        public void RemoveEnvironmentAtBlock(int bx, int by, int bz)
        {
            if (_registry == null || !_blockToTransforms.TryGetValue((bx, by, bz), out var list)) return;
            var toRemove = new List<Transform>();
            foreach (var t in list)
            {
                if (t == null) continue;
                var entryName = GetEntryNameForTransform(t);
                var entry = string.IsNullOrEmpty(entryName) ? null : _registry.GetByName(entryName);
                if (entry != null && entry.StructureType == Pure.StructureType.Environment)
                    toRemove.Add(t);
            }
            foreach (var t in toRemove)
            {
                UnregisterTransformAtBlock(bx, by, bz, t);
                Object.Destroy(t.gameObject);
            }
        }

        public bool HasEntryAtBlock(string entryName, int bx, int by, int bz)
        {
            if (string.IsNullOrEmpty(entryName)) return false;
            if (entryName == "Road") return _roadOverlay.Contains(bx, by, bz);
            if (!_blockToTransforms.TryGetValue((bx, by, bz), out var list)) return false;
            foreach (var t in list)
            {
                if (t == null) continue;
                if (GetEntryNameForTransform(t) == entryName)
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
            RebuildBlockIndex();
        }

        private void RebuildBlockIndex()
        {
            _blockToTransforms.Clear();
            var worldScale = new WorldScale(_worldParameters != null ? _worldParameters.BlockScale : 1f);
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    if (child == null) continue;
                    RegisterPlacedObject(kv.Key, child);
                }
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
