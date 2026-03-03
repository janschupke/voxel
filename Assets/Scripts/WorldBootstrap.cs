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

        private void Start()
        {
            if (WorldPersistenceService.WorldExists())
            {
                _grid = WorldPersistenceService.Load();
            }
            else
            {
                _grid = CreateNewWorld();
                WorldPersistenceService.Save(_grid);
            }

            _renderer = GetComponent<VoxelGridRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<VoxelGridRenderer>();

            _renderer.Initialize(_grid, worldParameters);

            SetupCamera();
        }

        private VoxelGrid CreateNewWorld()
        {
            int width = worldParameters != null ? worldParameters.Width : 1000;
            int depth = worldParameters != null ? worldParameters.Depth : 1000;
            int height = worldParameters != null ? worldParameters.Height : 50;

            var grid = new VoxelGrid(width, depth, height);

            if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
            {
                int waterLevelY = waterConfig != null
                    ? waterConfig.GetWaterLevelY(height)
                    : Mathf.Clamp(15, 0, height - 1);

                var heightBuffer = new HeightBuffer(width, depth);
                var context = new TerrainPipelineContext(heightBuffer, grid, waterLevelY, islandPipelineConfig.MasterSeed);
                var stages = islandPipelineConfig.BuildStages();

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

            float scale = worldParameters != null ? worldParameters.BlockScale : 1f;

            var topDown = topDownCamera != null ? topDownCamera : cam.GetComponent<TopDownCamera>();
            var freeFly = freeFlyCamera != null ? freeFlyCamera : cam.GetComponent<FreeFlyCamera>();

            if (cameraMode == CameraMode.TopDown)
            {
                if (topDown == null)
                    topDown = cam.gameObject.AddComponent<TopDownCamera>();
                topDown.enabled = true;
                if (freeFly != null) freeFly.enabled = false;
                topDown.Initialize(_grid, scale);
                topDown.FrameWorld(_grid, scale);
            }
            else
            {
                if (freeFly != null) freeFly.enabled = true;
                if (topDown != null) topDown.enabled = false;
                float centerX = _grid.Width * 0.5f * scale;
                float centerZ = _grid.Depth * 0.5f * scale;
                float centerY = _grid.Height * 0.5f * scale;
                float distance = Mathf.Max(_grid.Width, _grid.Depth) * 0.5f * scale;
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
            WorldPersistenceService.Save(_grid);
            _renderer.Initialize(_grid, worldParameters);
            SetupCamera();
        }

        public VoxelGrid Grid => _grid;
        public VoxelGridRenderer Renderer => _renderer;
    }
}
