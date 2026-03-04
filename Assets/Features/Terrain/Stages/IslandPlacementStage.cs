using System;
using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    public class IslandPlacementStage : ITerrainStage
    {
        private readonly IslandStageConfig _config;

        public IslandPlacementStage(IslandStageConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Execute(TerrainPipelineContext ctx)
        {
            var buffer = ctx.HeightBuffer;
            int width = buffer.Width;
            int depth = buffer.Depth;
            int seed = ctx.GetSeedForStage("IslandPlacement");
            var rng = new System.Random(seed);

            int margin = Mathf.Max(1, (int)(Mathf.Min(width, depth) * _config.CornerMarginPercent));
            int minX = margin;
            int maxX = Mathf.Max(margin + 1, width - margin);
            int minZ = margin;
            int maxZ = Mathf.Max(margin + 1, depth - margin);

            int islandCount = Mathf.Max(1, (width * depth) / _config.IslandDensity);

            var centers = new List<(int cx, int cz, int radius)>();
            int maxAttempts = islandCount * 100;
            int attempts = 0;
            int minGap = _config.MinGapBetweenIslands;
            const float maxRadiusExtent = 1.5f;

            while (centers.Count < islandCount && attempts < maxAttempts)
            {
                int radius = rng.Next(_config.MinIslandRadius, _config.MaxIslandRadius + 1);
                int cx = rng.Next(minX, maxX);
                int cz = rng.Next(minZ, maxZ);

                bool tooClose = false;
                foreach (var (ocx, ocz, oradius) in centers)
                {
                    float d = Mathf.Sqrt((cx - ocx) * (cx - ocx) + (cz - ocz) * (cz - ocz));
                    float minSeparation = (oradius + radius) * maxRadiusExtent + minGap;
                    if (d < minSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    centers.Add((cx, cz, radius));
                attempts++;
            }

            int baseHeight = _config.BaseIslandHeight;
            float irregularity = _config.ShapeIrregularity;
            var noise = new FractalNoise(0.08f, 4, 2f, 0.5f, seed + 1);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float minDistSq = float.MaxValue;
                    int nearestRadius = 0;

                    foreach (var (cx, cz, radius) in centers)
                    {
                        float dx = x - cx;
                        float dz = z - cz;
                        float distSq = dx * dx + dz * dz;
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            nearestRadius = radius;
                        }
                    }

                    float dist = Mathf.Sqrt(minDistSq);
                    float n = noise.Sample(x, z);
                    float radiusVariation = Mathf.Clamp(1f + irregularity * (2f * n - 1f), 0.5f, 1.5f);
                    float effectiveRadius = nearestRadius * radiusVariation;
                    buffer.Set(x, z, dist <= effectiveRadius ? baseHeight : 0f);
                }
            }
        }
    }
}
