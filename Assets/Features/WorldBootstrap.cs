using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    public enum TerrainGenerationMode { PerlinNoise, IslandPipeline }

    [DefaultExecutionOrder(-100)]
    public class WorldBootstrap : MonoBehaviour
    {
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

        private VoxelGrid _grid;
        private VoxelGridRenderer _renderer;
        private PlacedObjectManager _placedObjectManager;

        private void Start()
        {
            if (actorSpawner == null)
                actorSpawner = GetComponent<ActorSpawner>();
            if (actorSpawner == null)
                actorSpawner = gameObject.AddComponent<ActorSpawner>();

            _placedObjectManager = new PlacedObjectManager(transform);
            _placedObjectManager.Initialize(placedObjectRegistry, worldParameters, islandPipelineConfig, waterConfig);

            if (WorldPersistenceService.WorldExists())
            {
                var (grid, placedObjects) = WorldPersistenceService.Load();
                _grid = grid;
                _placedObjectManager.LoadPlacedObjects(placedObjects, _grid, terrainMode);
            }
            else
            {
                _grid = CreateNewWorld();
                WorldPersistenceService.Save(_grid, _placedObjectManager.CollectPlacedObjectsForSave());
            }

            _renderer = GetComponent<VoxelGridRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<VoxelGridRenderer>();

            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, waterConfig, mountainMaterial, _placedObjectManager.RoadOverlay, roadConfig);

            SetupCamera(restoreCamera: true);
        }

        private void OnApplicationQuit()
        {
            if (topDownCamera != null)
            {
                topDownCamera.GetPositionAndZoom(out float x, out float z, out float blocks);
                GameSettings.SaveCamera(x, z, blocks);
            }
        }

        private VoxelGrid CreateNewWorld()
        {
            _placedObjectManager.Clear();
            var treeParent = _placedObjectManager.GetOrCreateParentForEntry("Tree");
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
                WorldPersistenceService.Save(_grid, _placedObjectManager.CollectPlacedObjectsForSave());
        }

        public void SpawnActorsForBuildings()
        {
            if (actorSpawner != null)
                actorSpawner.SpawnActorsForBuildings();
        }

        public void RegenerateWorld()
        {
            WorldPersistenceService.DeleteWorld();
            _grid = CreateNewWorld();
            WorldPersistenceService.Save(_grid, _placedObjectManager.CollectPlacedObjectsForSave());
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

        public Transform GetOrCreateParentForEntry(string entryName) =>
            _placedObjectManager.GetOrCreateParentForEntry(entryName);

        public void AddRoadAt(int x, int y, int z) => _placedObjectManager.AddRoadAt(x, y, z);
        public void RemoveRoadAt(int x, int y, int z) => _placedObjectManager.RemoveRoadAt(x, y, z);
        public bool HasRoadAt(int x, int y, int z) => _placedObjectManager.HasRoadAt(x, y, z);
        public RoadOverlay GetRoadOverlay() => _placedObjectManager.RoadOverlay;

        public bool HasBlockingObjectAtBlock(int bx, int by, int bz) =>
            _placedObjectManager.HasBlockingObjectAtBlock(bx, by, bz);

        public bool HasEntryAtBlock(string entryName, int bx, int by, int bz) =>
            _placedObjectManager.HasEntryAtBlock(entryName, bx, by, bz);

        public Transform GetParentByEntryName(string entryName) =>
            _placedObjectManager.GetParentByEntryName(entryName);

        public string GetEntryNameForTransform(Transform child) =>
            _placedObjectManager.GetEntryNameForTransform(child);

        public WaterConfig WaterConfig => waterConfig;
        public WorldParameters WorldParameters => worldParameters;
        public bool ShowDebugControls => showDebugControls;
    }
}
