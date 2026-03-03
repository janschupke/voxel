using System;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public class ConnectionTerrainStage : ITerrainStage
    {
        private readonly ConnectionStageConfig _config;

        public ConnectionTerrainStage(ConnectionStageConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Execute(TerrainPipelineContext ctx)
        {
            var buffer = ctx.HeightBuffer;
            var grid = ctx.Grid;
            int width = buffer.Width;
            int depth = buffer.Depth;
            int gridHeight = grid.Height;
            float falloff = _config.FalloffDistance;
            int seaBed = _config.SeaBedHeight;
            byte blockType = _config.BlockType;

            float baseIslandHeight = FindMaxIslandHeight(buffer, width, depth);
            var distanceField = ComputeDistanceToIsland(buffer, width, depth);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = buffer.Get(x, z);
                    if (h <= 0f)
                    {
                        float distToIsland = distanceField[x, z];
                        h = seaBed + (baseIslandHeight - seaBed) * Mathf.Exp(-distToIsland / falloff);
                    }

                    int heightInt = Mathf.Clamp((int)Mathf.Round(h), 0, gridHeight - 1);

                    for (int y = 0; y <= heightInt; y++)
                        grid.SetBlock(x, y, z, blockType);
                }
            }
        }

        private static float[,] ComputeDistanceToIsland(HeightBuffer buffer, int width, int depth)
        {
            const float inf = 1e9f;
            var dist = new float[width, depth];

            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                    dist[x, z] = buffer.Get(x, z) > 0f ? 0f : inf;

            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    if (dist[x, z] <= 0f) continue;
                    float minN = inf;
                    if (x > 0) minN = Mathf.Min(minN, dist[x - 1, z]);
                    if (z > 0) minN = Mathf.Min(minN, dist[x, z - 1]);
                    if (x > 0 && z > 0) minN = Mathf.Min(minN, dist[x - 1, z - 1]);
                    if (x > 0 && z < depth - 1) minN = Mathf.Min(minN, dist[x - 1, z + 1]);
                    dist[x, z] = Mathf.Min(dist[x, z], minN + 1f);
                }

            for (int z = depth - 1; z >= 0; z--)
                for (int x = width - 1; x >= 0; x--)
                {
                    if (dist[x, z] <= 0f) continue;
                    float minN = inf;
                    if (x < width - 1) minN = Mathf.Min(minN, dist[x + 1, z]);
                    if (z < depth - 1) minN = Mathf.Min(minN, dist[x, z + 1]);
                    if (x < width - 1 && z < depth - 1) minN = Mathf.Min(minN, dist[x + 1, z + 1]);
                    if (x < width - 1 && z > 0) minN = Mathf.Min(minN, dist[x + 1, z - 1]);
                    dist[x, z] = Mathf.Min(dist[x, z], minN + 1f);
                }

            return dist;
        }

        private static float FindMaxIslandHeight(HeightBuffer buffer, int width, int depth)
        {
            float max = 0f;
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    float h = buffer.Get(x, z);
                    if (h > max) max = h;
                }
            return max;
        }
    }
}
