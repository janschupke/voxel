using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Creates new voxel worlds. Extracted from WorldBootstrap for single responsibility.
    /// </summary>
    public static class WorldCreationService
    {
        public static VoxelGrid CreateNewWorld(
            TerrainGenerationMode terrainMode,
            WorldParameters worldParameters,
            NoiseParameters noiseParameters,
            IslandPipelineConfig islandPipelineConfig,
            WaterConfig waterConfig,
            Transform treeParent)
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

                var worldScale = new WorldScale(worldParameters != null && worldParameters.BlockScale > 0 ? worldParameters.BlockScale : 1f);
                int voxelsPerBlock = worldParameters?.VoxelsPerBlockAxis ?? 16;
                var heightBuffer = new HeightBuffer(width, depth);
                var context = new TerrainPipelineContext(heightBuffer, grid, waterLevelY, islandPipelineConfig.MasterSeed);
                var stages = islandPipelineConfig.BuildStages(treeParent, worldScale, voxelsPerBlock);

                if (stages != null && stages.Count > 0)
                    TerrainPipeline.Execute(stages, context);
            }
            else
            {
                int seed = noiseParameters != null ? noiseParameters.Seed : 12345;
                float frequency = noiseParameters != null ? noiseParameters.Frequency : 0.04f;
                int octaves = noiseParameters != null ? noiseParameters.Octaves : 5;
                float lacunarity = noiseParameters != null ? noiseParameters.Lacunarity : 2f;
                float persistence = noiseParameters != null ? noiseParameters.Persistence : 0.5f;

                var fractalNoise = new FractalNoise(frequency, octaves, lacunarity, persistence, seed);
                var terrainGen = new TerrainGenerator(grid, fractalNoise);
                terrainGen.Generate();
            }

            return grid;
        }
    }
}
