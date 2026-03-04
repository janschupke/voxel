using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

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

        private VoxelGrid _grid;
        private VoxelGridRenderer _renderer;
        private readonly Dictionary<string, Transform> _parentsByEntryName = new Dictionary<string, Transform>();
        private readonly RoadOverlay _roadOverlay = new RoadOverlay();

        [SerializeField] private PlacedObjectRegistry placedObjectRegistry;
        [SerializeField] private ItemRegistry itemRegistry;
        [SerializeField] private RoadConfig roadConfig;
        [SerializeField] private ActorSpawner actorSpawner;

        private void Start()
        {
            if (actorSpawner == null)
                actorSpawner = GetComponent<ActorSpawner>();
            if (actorSpawner == null)
                actorSpawner = gameObject.AddComponent<ActorSpawner>();

            if (WorldPersistenceService.WorldExists())
            {
                var (grid, placedObjects) = WorldPersistenceService.Load();
                _grid = grid;
                LoadPlacedObjects(placedObjects);
            }
            else
            {
                _grid = CreateNewWorld();
                WorldPersistenceService.Save(_grid, CollectPlacedObjectsForSave());
            }

            _renderer = GetComponent<VoxelGridRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<VoxelGridRenderer>();

            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, mountainMaterial, _roadOverlay, roadConfig);

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
            _roadOverlay.Clear();
            foreach (var kv in _parentsByEntryName)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _parentsByEntryName.Clear();

            int width = worldParameters != null ? worldParameters.Width : 1000;
            int depth = worldParameters != null ? worldParameters.Depth : 1000;
            int height = worldParameters != null ? worldParameters.Height : 50;

            var grid = new VoxelGrid(width, depth, height);

            if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
            {
                int waterLevelY = waterConfig != null
                    ? waterConfig.GetWaterLevelY(height)
                    : Mathf.Clamp(15, 0, height - 1);

                var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
                var treesParent = GetOrCreateParentForEntry("Tree");

                var heightBuffer = new HeightBuffer(width, depth);
                var context = new TerrainPipelineContext(heightBuffer, grid, waterLevelY, islandPipelineConfig.MasterSeed);
                var stages = islandPipelineConfig.BuildStages(treesParent, worldScale);

                if (stages != null && stages.Count > 0)
                    TerrainPipeline.Execute(stages, context);
            }
            else
            {
                float heightScale = worldParameters != null ? worldParameters.HeightScale : 1f;
                float heightOffset = worldParameters != null ? worldParameters.HeightOffset : 0f;
                byte blockType = worldParameters != null ? worldParameters.BlockType : BlockType.Ground;

                int seed = noiseParameters != null ? noiseParameters.Seed : 12345;
                float frequency = noiseParameters != null ? noiseParameters.Frequency : 0.04f;
                int octaves = noiseParameters != null ? noiseParameters.Octaves : 5;
                float lacunarity = noiseParameters != null ? noiseParameters.Lacunarity : 2f;
                float persistence = noiseParameters != null ? noiseParameters.Persistence : 0.5f;

                var fractalNoise = new FractalNoise(frequency, octaves, lacunarity, persistence, seed);
                var terrainGen = new TerrainGenerator(grid, fractalNoise, heightScale, heightOffset, blockType);
                terrainGen.Generate();
            }

            return grid;
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
                WorldPersistenceService.Save(_grid, CollectPlacedObjectsForSave());
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
            WorldPersistenceService.Save(_grid, CollectPlacedObjectsForSave());
            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, mountainMaterial, _roadOverlay, roadConfig);
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
            return GetOrCreateParentForEntry(entry.Name);
        }

        public Transform GetOrCreateParentForEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return null;
            if (entryName == "Road") return null;
            if (_parentsByEntryName.TryGetValue(entryName, out var parent) && parent != null)
                return parent;
            var go = new GameObject(entryName + "s");
            go.transform.SetParent(transform);
            _parentsByEntryName[entryName] = go.transform;
            return go.transform;
        }

        public void AddRoadAt(int x, int y, int z)
        {
            _roadOverlay.Add(x, y, z);
        }

        public void RemoveRoadAt(int x, int y, int z)
        {
            _roadOverlay.Remove(x, y, z);
        }

        public bool HasRoadAt(int x, int y, int z)
        {
            return _roadOverlay.Contains(x, y, z);
        }

        public RoadOverlay GetRoadOverlay()
        {
            return _roadOverlay;
        }

        public bool HasBlockingObjectAtBlock(int bx, int by, int bz)
        {
            if (_roadOverlay.Contains(bx, by, bz)) return true;
            if (placedObjectRegistry == null) return false;
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            foreach (var entry in placedObjectRegistry.Entries)
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
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            for (int i = 0; i < parent.childCount; i++)
            {
                var (hx, hy, hz) = worldScale.WorldToBlock(parent.GetChild(i).position);
                if (hx == bx && hy == by && hz == bz) return true;
            }
            return false;
        }

        public Transform GetParentByEntryName(string entryName)
        {
            return _parentsByEntryName.TryGetValue(entryName ?? "", out var p) ? p : null;
        }

        public WaterConfig WaterConfig => waterConfig;
        public WorldParameters WorldParameters => worldParameters;

        private List<PlacedObjectData> CollectPlacedObjectsForSave()
        {
            if (placedObjectRegistry == null) return null;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
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

        private void LoadPlacedObjects(IReadOnlyList<PlacedObjectData> placedObjects)
        {
            if (placedObjectRegistry == null || placedObjects == null || placedObjects.Count == 0)
            {
                if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
                    RunTreePlacement();
                return;
            }

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);

            foreach (var p in placedObjects)
            {
                if (p.EntryName == "Road")
                {
                    _roadOverlay.Add(p.BlockX, p.BlockY, p.BlockZ);
                    continue;
                }

                var entry = placedObjectRegistry.GetByName(p.EntryName);
                var prefab = entry?.Prefab ?? (p.EntryName == "Tree" ? islandPipelineConfig?.TreeScatterConfig?.TreePrefab : null);
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

            if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
            {
                var treeParent = GetParentByEntryName("Tree");
                if (treeParent == null || treeParent.childCount == 0)
                    RunTreePlacement();
            }
        }

        private void RunTreePlacement()
        {
            if (terrainMode != TerrainGenerationMode.IslandPipeline || islandPipelineConfig == null)
                return;

            var treeConfig = islandPipelineConfig.TreeScatterConfig;
            if (treeConfig == null || treeConfig.TreePrefab == null)
                return;

            int height = _grid.Height;
            int waterLevelY = waterConfig != null ? waterConfig.GetWaterLevelY(height) : Mathf.Clamp(15, 0, height - 1);
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);

            var treeParent = GetOrCreateParentForEntry("Tree");
            var heightBuffer = new HeightBuffer(_grid.Width, _grid.Depth);
            var context = new TerrainPipelineContext(heightBuffer, _grid, waterLevelY, islandPipelineConfig.MasterSeed);
            var stage = new TreeScatterStage(treeConfig, treeParent, worldScale);
            stage.Execute(context);
        }
    }
}
