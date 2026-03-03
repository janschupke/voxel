using System;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public class MountainStage : ITerrainStage
    {
        private readonly MountainStageConfig _config;

        public MountainStage(MountainStageConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Execute(TerrainPipelineContext ctx)
        {
            var grid = ctx.Grid;
            int width = grid.Width;
            int depth = grid.Depth;
            int gridHeight = grid.Height;
            int waterLevelY = ctx.WaterLevelY;
            int seed = ctx.GetSeedForStage("Mountain");

            var noise = new FractalNoise(
                _config.NoiseFrequency,
                _config.Octaves,
                _config.Lacunarity,
                _config.Persistence,
                seed);

            float threshold = 1f - _config.Density;
            int minH = _config.MinMountainHeight;
            int maxH = _config.MaxMountainHeight;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int topY = GetTopSolidY(grid, x, z, gridHeight);
                    if (topY < waterLevelY) continue;

                    float sample = noise.Sample(x, z);
                    if (sample <= threshold) continue;

                    float t = (sample - threshold) / (1f - threshold);
                    int mountainHeight = minH + (int)(t * (maxH - minH));
                    if (mountainHeight < 1) mountainHeight = 1;

                    for (int y = topY + 1; y <= topY + mountainHeight && y < gridHeight; y++)
                        grid.SetBlock(x, y, z, BlockType.Stone);
                }
            }
        }

        private static int GetTopSolidY(VoxelGrid grid, int x, int z, int gridHeight)
        {
            for (int y = gridHeight - 1; y >= 0; y--)
            {
                if (grid.IsSolid(x, y, z))
                    return y;
            }
            return -1;
        }
    }
}
