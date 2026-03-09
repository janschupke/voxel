using System;
using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pure;

namespace Voxel
{
    public enum TerrainGenerationMode { PerlinNoise, IslandPipeline }

    public class WorldBootstrap : MonoBehaviour
    {
        /// <summary>Fired when the world is fully initialized. Subscribe in Awake; handler runs when ready (or immediately if already ready).</summary>
        public static event System.Action<WorldBootstrap> WorldReady;

        /// <summary>Set when Start completes. Use to check if world is ready before WorldReady fires.</summary>
        public static WorldBootstrap Instance { get; private set; }

        [SerializeField] private TerrainGenerationMode terrainMode = TerrainGenerationMode.PerlinNoise;
        [SerializeField] private NoiseParameters noiseParameters;
        [SerializeField] private IslandPipelineConfig islandPipelineConfig;
        [SerializeField] private WaterConfig waterConfig;
        [SerializeField] private WorldParameters worldParameters;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TopDownCamera topDownCamera;

        [SerializeField] private PlacedObjectRegistry placedObjectRegistry;
        [SerializeField] private ItemRegistry itemRegistry;
        [SerializeField] private RoadConfig roadConfig;
        [SerializeField] private ActorSpawner actorSpawner;

        [Header("Debug")]
        [Tooltip("When false, hides debug controls (Clear Inventory, Clear All Inventories) in the sidebar.")]
        [SerializeField] private bool showDebugControls = true;

        [Header("Storage")]
        [Tooltip("Per-item capacity for global storage. Used when no Warehouse entry defines it.")]
        [SerializeField] private int storagePerItemCapacity = 100;

        private VoxelGrid _grid;
        private VoxelGridRenderer _renderer;
        private PlacedObjectManager _placedObjectManager;
        private StorageInventory _storageInventory;

        private void Start()
        {
            if (actorSpawner == null)
                actorSpawner = GetComponent<ActorSpawner>();
            if (actorSpawner == null)
                actorSpawner = gameObject.AddComponent<ActorSpawner>();

            _placedObjectManager = new PlacedObjectManager(transform);
            _placedObjectManager.Initialize(placedObjectRegistry, worldParameters, islandPipelineConfig, waterConfig);

            EnsureStorageInventory();

            if (WorldPersistenceService.WorldExists())
            {
                var (grid, placedObjects, buildingInventories, actorData, globalStorageItems, saveVersion) = WorldPersistenceService.Load();
                _grid = grid;
                var inventoryLookup = BuildInventoryLookup(buildingInventories, itemRegistry);
                _placedObjectManager.LoadPlacedObjects(placedObjects, _grid, terrainMode, inventoryLookup, saveVersion, (go, e) =>
                {
                    if (e?.CritterSpawnerConfig == null) return;
                    var s = go.GetComponent<CritterSpawner>();
                    if (s == null) s = go.AddComponent<CritterSpawner>();
                    s.Initialize(e.CritterSpawnerConfig, this);
                });
                if (globalStorageItems != null && globalStorageItems.Count > 0 && itemRegistry != null)
                {
                    var items = new List<(Item, int)>();
                    foreach (var (itemId, count) in globalStorageItems)
                    {
                        if (count <= 0) continue;
                        if (itemRegistry.TryGetByStableId(itemId, out var item))
                            items.Add((item, count));
                    }
                    if (items.Count > 0)
                        _storageInventory.LoadFrom(items);
                }
                if (actorSpawner != null)
                    actorSpawner.SetSavedActorData(actorData);
            }
            else
            {
                _grid = CreateNewWorld();
                SaveWorld();
            }

            _renderer = GetComponent<VoxelGridRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<VoxelGridRenderer>();

            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, waterConfig, mountainMaterial, _placedObjectManager.RoadOverlay, roadConfig);

            SetupCamera(restoreCamera: true);

            Instance = this;
            actorSpawner?.SpawnActorsForBuildings();
            WorldReady?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit()
        {
            SaveWorld();
            if (topDownCamera != null)
            {
                topDownCamera.GetPositionAndZoom(out float x, out float z, out float blocks);
                GameSettings.SaveCamera(x, z, blocks);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                SaveWorld();
        }

        private VoxelGrid CreateNewWorld()
        {
            _placedObjectManager.Clear();
            var treeParent = _placedObjectManager.GetOrCreateParentForEntry(PlacedObjectKeys.Tree);
            return WorldCreationService.CreateNewWorld(
                terrainMode, worldParameters, noiseParameters,
                islandPipelineConfig, waterConfig, treeParent);
        }

        private void SetupCamera(bool restoreCamera = false)
        {
            var cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null || _grid == null) return;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var topDown = topDownCamera != null ? topDownCamera : cam.GetComponent<TopDownCamera>();
            if (topDown == null)
                topDown = cam.gameObject.AddComponent<TopDownCamera>();

            topDown.enabled = true;
            topDown.Initialize(_grid, worldScale);
            topDown.FrameWorld(_grid, worldScale);
            if (restoreCamera && GameSettings.TryLoadCamera(out float savedX, out float savedZ, out float savedBlocksVisible))
                topDown.RestorePosition(savedX, savedZ, savedBlocksVisible);
        }

        public void SaveWorld()
        {
            if (_grid != null)
            {
                var globalStorageItems = CollectGlobalStorageForSave();
                WorldPersistenceService.Save(
                    _grid,
                    _placedObjectManager.CollectPlacedObjectsForSave(),
                    _placedObjectManager.CollectBuildingInventoriesForSave(itemRegistry),
                    CollectActorDataForSave(),
                    globalStorageItems);
            }
        }

        private void EnsureStorageInventory()
        {
            if (_storageInventory != null) return;
            _storageInventory = GetComponent<StorageInventory>();
            if (_storageInventory == null)
                _storageInventory = gameObject.AddComponent<StorageInventory>();
            int capacity = storagePerItemCapacity;
            var globalStorageEntry = placedObjectRegistry?.GetGlobalStorageEntry();
            if (globalStorageEntry != null && globalStorageEntry.InventoryCapacity > 0)
                capacity = globalStorageEntry.InventoryCapacity;
            _storageInventory.Initialize(capacity);
        }

        private List<(string ItemId, int Count)> CollectGlobalStorageForSave()
        {
            var list = new List<(string ItemId, int Count)>();
            if (_storageInventory == null || itemRegistry == null) return list;
            foreach (var (item, count) in _storageInventory.GetAllItems())
            {
                if (count > 0)
                    list.Add((itemRegistry.GetStableId(item), count));
            }
            return list;
        }

        private static Dictionary<(string, int, int, int), List<(Item, int)>> BuildInventoryLookup(
            IReadOnlyList<BuildingInventorySaveData> buildingInventories,
            IItemRegistry itemRegistry)
        {
            var lookup = new Dictionary<(string, int, int, int), List<(Item, int)>>();
            if (buildingInventories == null || itemRegistry == null) return lookup;
            foreach (var inv in buildingInventories)
            {
                var items = new List<(Item, int)>();
                if (inv.Items != null)
                {
                    foreach (var (itemId, count) in inv.Items)
                    {
                        if (count <= 0) continue;
                        if (itemRegistry.TryGetByStableId(itemId, out var item))
                            items.Add((item, count));
                    }
                }
                if (items.Count > 0)
                    lookup[(inv.EntryName, inv.BlockX, inv.BlockY, inv.BlockZ)] = items;
            }
            return lookup;
        }

        public List<ActorSaveData> CollectActorDataForSave()
        {
            var list = new List<ActorSaveData>();
            var actorsParent = _placedObjectManager.GetParentByEntryName(PlacedObjectKeys.Actors);
            if (actorsParent == null) return list;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            foreach (var ab in actorsParent.GetComponentsInChildren<ActorBehavior>(includeInactive: true))
            {
                if (ab == null || ab.HomeBuildingTransform == null) continue;
                var (hx, hy, hz) = worldScale.WorldToBlock(ab.HomeBuildingTransform.position);
                var homeEntryName = _placedObjectManager.GetEntryNameForTransform(ab.HomeBuildingTransform) ?? "";
                var actorTypeName = ab.ActorTypeNameForSave;
                var carriedItemId = "";
                var carriedCount = 1;
                if (ab is CarrierActorBehavior carrier && carrier.CarriedItem.HasValue && itemRegistry != null)
                {
                    carriedItemId = itemRegistry.GetStableId(carrier.CarriedItem.Value);
                    carriedCount = carrier.CarriedCount > 0 ? carrier.CarriedCount : 1;
                }
                else if (ab is CollectorActorBehavior collector && collector.CarriedItem.HasValue && itemRegistry != null)
                {
                    carriedItemId = itemRegistry.GetStableId(collector.CarriedItem.Value);
                    carriedCount = collector.CarriedCount > 0 ? collector.CarriedCount : 1;
                }

                list.Add(new ActorSaveData(
                    actorTypeName,
                    homeEntryName,
                    hx, hy, hz,
                    ab.transform.position.x, ab.transform.position.y, ab.transform.position.z,
                    (int)ab.CurrentState,
                    carriedItemId,
                    carriedCount));
            }
            return list;
        }

        public void SpawnActorsForBuildings()
        {
            if (actorSpawner != null)
                actorSpawner.SpawnActorsForBuildings();
        }

        public void SpawnActorForBuildingIfNeeded(PlacedObjectEntry entry, Transform building)
        {
            if (actorSpawner != null)
                actorSpawner.SpawnActorForBuildingIfNeeded(entry, building);
        }

        public void RegenerateWorld()
        {
            WorldPersistenceService.DeleteWorld();
            _storageInventory?.Clear();
            _grid = CreateNewWorld();
            SaveWorld();
            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, waterConfig, mountainMaterial, _placedObjectManager.RoadOverlay, roadConfig);
            SetupCamera(restoreCamera: false);
        }

        public VoxelGrid Grid => _grid;
        public VoxelGridRenderer Renderer => _renderer;
        public PlacedObjectRegistry PlacedObjectRegistry => placedObjectRegistry;
        public ItemRegistry ItemRegistry => itemRegistry;
        public IslandPipelineConfig IslandPipelineConfig => islandPipelineConfig;
        public TopDownCamera TopDownCamera => topDownCamera;

        public void CenterCameraOnPosition(Vector3 worldPosition)
        {
            if (topDownCamera != null)
                topDownCamera.CenterOnPosition(worldPosition);
        }

        public Transform GetParentForEntry(PlacedObjectEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Name)) return null;
            return _placedObjectManager.GetOrCreateParentForEntry(entry.Name);
        }

        /// <summary>Registers a placed object in the spatial index. Call after instantiating.</summary>
        public void NotifyObjectPlaced(string entryName, Transform transform)
        {
            _placedObjectManager?.RegisterPlacedObject(entryName, transform);
        }

        public Transform GetOrCreateParentForEntry(string entryName) =>
            _placedObjectManager.GetOrCreateParentForEntry(entryName);

        public void AddRoadAt(int x, int y, int z) => _placedObjectManager.AddRoadAt(x, y, z);
        public void RemoveRoadAt(int x, int y, int z) => _placedObjectManager.RemoveRoadAt(x, y, z);
        public bool HasRoadAt(int x, int y, int z) => _placedObjectManager.HasRoadAt(x, y, z);

        /// <summary>Removes all placed objects and roads at the block. Call SaveAndRefresh after batch operations.</summary>
        public bool RemoveAtBlock(int bx, int by, int bz)
        {
            bool hadRoad = _placedObjectManager.HasRoadAt(bx, by, bz);
            bool removed = _placedObjectManager.RemoveAtBlock(bx, by, bz);
            if (removed && hadRoad)
                Renderer?.InvalidateChunkAt(bx, by - 1, bz);
            return removed;
        }

        /// <summary>Destroys actors whose home building was removed. Call before SaveAndRefreshAfterRemoval.</summary>
        public void DestroyOrphanedActors()
        {
            if (actorSpawner != null)
                actorSpawner.DestroyOrphanedActors();
        }

        /// <summary>Call after removal operations to refresh actors. Does not save; use Save button or exit to persist.</summary>
        public void SaveAndRefreshAfterRemoval()
        {
            GameDebugLogger.Log("[WorldBootstrap] SaveAndRefreshAfterRemoval: SpawnActorsForBuildings");
            SpawnActorsForBuildings();
        }

        public void GetTransformsAtBlock(int bx, int by, int bz, System.Collections.Generic.List<Transform> outTransforms) =>
            _placedObjectManager.GetTransformsAtBlock(bx, by, bz, outTransforms);

        public void GetFootprintBlocksForTransform(Transform transform, System.Collections.Generic.List<(int x, int y, int z)> outBlocks) =>
            _placedObjectManager.GetFootprintBlocksForTransform(transform, outBlocks);

        /// <summary>Returns the world position of the footprint center for a placed object, or null if not registered.</summary>
        public Vector3? GetFootprintCenterWorld(Transform transform) =>
            _placedObjectManager.GetFootprintCenterWorld(transform);

        public bool HasRemovableAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.HasRemovableAtBlock(bx, by, bz);
        public RoadOverlay GetRoadOverlay() => _placedObjectManager.RoadOverlay;

        public bool HasBlockingObjectAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.HasBlockingObjectAtBlock(bx, by, bz);

        public bool BlocksPathingAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.BlocksPathingAtBlock(bx, by, bz);

        public bool HasEnvironmentAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.HasEnvironmentAtBlock(bx, by, bz);

        public void RemoveEnvironmentAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.RemoveEnvironmentAtBlock(bx, by, bz);

        public bool HasEntryAtBlock(string entryName, int bx, int by, int bz) =>
            _placedObjectManager.HasEntryAtBlock(entryName, bx, by, bz);

        public Transform GetParentByEntryName(string entryName) =>
            _placedObjectManager.GetParentByEntryName(entryName);

        public string GetEntryNameForTransform(Transform child) =>
            _placedObjectManager.GetEntryNameForTransform(child);

        public WaterConfig WaterConfig => waterConfig;
        public WorldParameters WorldParameters => worldParameters;
        public bool ShowDebugControls => showDebugControls;
        public IStorageInventory StorageInventory => _storageInventory;
    }
}
