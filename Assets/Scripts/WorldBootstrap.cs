using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    [DefaultExecutionOrder(-100)]
    public class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private NoiseParameters noiseParameters;
        [SerializeField] private WorldParameters worldParameters;

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

            _renderer.Initialize(_grid);

            SetupOverheadCamera();
        }

        private VoxelGrid CreateNewWorld()
        {
            int width = worldParameters != null ? worldParameters.Width : 1000;
            int depth = worldParameters != null ? worldParameters.Depth : 1000;
            int height = worldParameters != null ? worldParameters.Height : 50;
            float heightScale = worldParameters != null ? worldParameters.HeightScale : 1f;
            float heightOffset = worldParameters != null ? worldParameters.HeightOffset : 0f;
            byte blockType = worldParameters != null ? worldParameters.BlockType : BlockType.Ground;

            var grid = new VoxelGrid(width, depth, height);

            int seed = noiseParameters != null ? noiseParameters.Seed : 12345;
            float frequency = noiseParameters != null ? noiseParameters.Frequency : 0.04f;
            int octaves = noiseParameters != null ? noiseParameters.Octaves : 5;
            float lacunarity = noiseParameters != null ? noiseParameters.Lacunarity : 2f;
            float persistence = noiseParameters != null ? noiseParameters.Persistence : 0.5f;

            var fractalNoise = new FractalNoise(frequency, octaves, lacunarity, persistence, seed);
            var terrainGen = new TerrainGenerator(grid, fractalNoise, heightScale, heightOffset, blockType);
            terrainGen.Generate();

            return grid;
        }

        private void SetupOverheadCamera()
        {
            var cam = Camera.main;
            if (cam == null || _grid == null) return;

            float centerX = _grid.Width * 0.5f;
            float centerZ = _grid.Depth * 0.5f;
            float centerY = _grid.Height * 0.5f;
            float distance = Mathf.Max(_grid.Width, _grid.Depth) * 0.5f;

            cam.transform.position = new Vector3(centerX, centerY + distance, centerZ);
            cam.transform.LookAt(new Vector3(centerX, centerY, centerZ));
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(_grid.Width, _grid.Depth) * 0.5f;
        }

        public VoxelGrid Grid => _grid;
        public VoxelGridRenderer Renderer => _renderer;
    }
}
