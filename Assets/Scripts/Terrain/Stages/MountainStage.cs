using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public class MountainStage : ITerrainStage
    {
        private readonly MountainStageConfig _config;

        public MountainStage(MountainStageConfig config)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
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

            int minH = _config.MinMountainHeight;
            int maxH = _config.MaxMountainHeight;
            float threshold = (float)minH / maxH;

            var noiseMap = new float[width, depth];
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int topY = GetTopSolidY(grid, x, z, gridHeight);
                    if (topY < waterLevelY)
                    {
                        noiseMap[x, z] = 0f;
                        continue;
                    }
                    noiseMap[x, z] = noise.Sample(x, z);
                }
            }

            var smoothed = SmoothNoise(noiseMap, width, depth, _config.SmoothRadius);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int topY = GetTopSolidY(grid, x, z, gridHeight);
                    if (topY < waterLevelY) continue;

                    float n = smoothed[x, z];
                    if (n <= threshold) continue;

                    float t = (n - threshold) / (1f - threshold);
                    int h = Mathf.RoundToInt(t * maxH);
                    if (h < minH) continue;

                    for (int y = topY + 1; y <= topY + h && y < gridHeight; y++)
                        grid.SetBlock(x, y, z, BlockType.Stone);
                }
            }
        }

        private static float[,] SmoothNoise(float[,] map, int width, int depth, int radius)
        {
            var result = new float[width, depth];
            int r = Mathf.Max(1, radius);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    int count = 0;
                    for (int dz = -r; dz <= r; dz++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int nx = x + dx;
                            int nz = z + dz;
                            if (nx >= 0 && nx < width && nz >= 0 && nz < depth)
                            {
                                sum += map[nx, nz];
                                count++;
                            }
                        }
                    }
                    result[x, z] = count > 0 ? sum / count : map[x, z];
                }
            }
            return result;
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
