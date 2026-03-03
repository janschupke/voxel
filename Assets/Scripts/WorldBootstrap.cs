using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public enum CameraMode { TopDown, FreeFly }

    public enum TerrainGenerationMode { PerlinNoise, IslandPipeline }

    [DefaultExecutionOrder(-100)]
    public class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private TerrainGenerationMode terrainMode = TerrainGenerationMode.PerlinNoise;
        [SerializeField] private NoiseParameters noiseParameters;
        [SerializeField] private IslandPipelineConfig islandPipelineConfig;
        [SerializeField] private WaterConfig waterConfig;
        [SerializeField] private WorldParameters worldParameters;
        [SerializeField] private CameraMode cameraMode = CameraMode.TopDown;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private TopDownCamera topDownCamera;
        [SerializeField] private FreeFlyCamera freeFlyCamera;

        private VoxelGrid _grid;
        private VoxelGridRenderer _renderer;
        private Transform _treesParent;

        private void Start()
        {
            if (WorldPersistenceService.WorldExists())
            {
                var (grid, trees) = WorldPersistenceService.Load();
                _grid = grid;
                LoadTrees(trees);
            }
            else
            {
                _grid = CreateNewWorld();
                WorldPersistenceService.Save(_grid, CollectTreesForSave());
            }

            _renderer = GetComponent<VoxelGridRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<VoxelGridRenderer>();

            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, mountainMaterial);

            SetupCamera();
        }

        private VoxelGrid CreateNewWorld()
        {
            if (_treesParent != null)
            {
                Destroy(_treesParent.gameObject);
                _treesParent = null;
            }

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
                _treesParent = new GameObject("Trees").transform;
                _treesParent.SetParent(transform);

                var heightBuffer = new HeightBuffer(width, depth);
                var context = new TerrainPipelineContext(heightBuffer, grid, waterLevelY, islandPipelineConfig.MasterSeed);
                var stages = islandPipelineConfig.BuildStages(_treesParent, worldScale);

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

        private void SetupCamera()
        {
            var cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null || _grid == null) return;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);

            var topDown = topDownCamera != null ? topDownCamera : cam.GetComponent<TopDownCamera>();
            var freeFly = freeFlyCamera != null ? freeFlyCamera : cam.GetComponent<FreeFlyCamera>();

            if (cameraMode == CameraMode.TopDown)
            {
                if (topDown == null)
                    topDown = cam.gameObject.AddComponent<TopDownCamera>();
                topDown.enabled = true;
                if (freeFly != null) freeFly.enabled = false;
                topDown.Initialize(_grid, worldScale);
                topDown.FrameWorld(_grid, worldScale);
            }
            else
            {
                if (freeFly != null) freeFly.enabled = true;
                if (topDown != null) topDown.enabled = false;
                float centerX = _grid.Width * 0.5f * worldScale.BlockScale;
                float centerZ = _grid.Depth * 0.5f * worldScale.BlockScale;
                float centerY = _grid.Height * 0.5f * worldScale.BlockScale;
                float distance = Mathf.Max(_grid.Width, _grid.Depth) * 0.5f * worldScale.BlockScale;
                cam.transform.position = new Vector3(centerX, centerY + distance, centerZ);
                cam.transform.LookAt(new Vector3(centerX, centerY, centerZ));
                cam.orthographic = false;
                cam.farClipPlane = Mathf.Max(2000f, distance * 3f);
            }
        }

        public void RegenerateWorld()
        {
            WorldPersistenceService.DeleteWorld();
            _grid = CreateNewWorld();
            WorldPersistenceService.Save(_grid, CollectTreesForSave());
            var mountainMaterial = terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null
                ? islandPipelineConfig.MountainStageConfig?.Material
                : null;
            _renderer.Initialize(_grid, worldParameters, mountainMaterial);
            SetupCamera();
        }

        public VoxelGrid Grid => _grid;
        public VoxelGridRenderer Renderer => _renderer;

        private List<TreePlacementData> CollectTreesForSave()
        {
            if (_treesParent == null) return null;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var list = new List<TreePlacementData>();

            for (int i = 0; i < _treesParent.childCount; i++)
            {
                var child = _treesParent.GetChild(i);
                var (bx, by, bz) = worldScale.WorldToBlock(child.position);
                list.Add(new TreePlacementData(bx, by, bz, child.eulerAngles.y));
            }

            return list.Count > 0 ? list : null;
        }

        private void LoadTrees(IReadOnlyList<TreePlacementData> trees)
        {
            if (terrainMode != TerrainGenerationMode.IslandPipeline || islandPipelineConfig == null)
                return;

            var treeConfig = islandPipelineConfig.TreeScatterConfig;
            if (treeConfig == null || treeConfig.TreePrefab == null)
                return;

            if (_treesParent != null)
                Destroy(_treesParent.gameObject);

            _treesParent = new GameObject("Trees").transform;
            _treesParent.SetParent(transform);

            if (trees == null || trees.Count == 0)
            {
                RunTreePlacement();
                return;
            }

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var scale = worldScale.ScaleVectorForBlockSizedPrefab(treeConfig.PrefabHeightInUnits) * treeConfig.ScaleMultiplier;

            foreach (var t in trees)
            {
                var instance = Object.Instantiate(treeConfig.TreePrefab, t.ToWorldPosition(worldScale), t.ToRotation(), _treesParent);
                instance.name = treeConfig.TreePrefab.name;
                instance.transform.localScale = scale;
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

            var heightBuffer = new HeightBuffer(_grid.Width, _grid.Depth);
            var context = new TerrainPipelineContext(heightBuffer, _grid, waterLevelY, islandPipelineConfig.MasterSeed);
            var stage = new TreeScatterStage(treeConfig, _treesParent, worldScale);
            stage.Execute(context);
        }
    }
}
