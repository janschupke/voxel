using System;
using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public class TreeScatterStage : ITerrainStage
    {
        private readonly TreeScatterConfig _config;
        private readonly Transform _treeParent;
        private readonly WorldScale _worldScale;

        private const int SpatialCellSize = 16;

        public TreeScatterStage(TreeScatterConfig config, Transform treeParent = null, WorldScale worldScale = default)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _treeParent = treeParent;
            _worldScale = worldScale.BlockScale > 0f ? worldScale : new WorldScale(1f);
        }

        public void Execute(TerrainPipelineContext ctx)
        {
            if (_treeParent == null)
            {
                UnityEngine.Debug.LogWarning("[TreeScatter] Skipped: TreeParent is null");
                return;
            }
            if (_config.TreePrefab == null)
            {
                UnityEngine.Debug.LogWarning("[TreeScatter] Skipped: Tree prefab is not assigned in TreeScatterConfig");
                return;
            }

            var grid = ctx.Grid;
            int width = grid.Width;
            int depth = grid.Depth;
            int gridHeight = grid.Height;
            int waterLevelY = ctx.WaterLevelY;
            int seed = ctx.GetSeedForStage("TreeScatter");
            var rng = new System.Random(seed);

            var candidates = new List<(int x, int y, int z)>();
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int topY = GetTopSolidY(grid, x, z, gridHeight);
                    if (topY < waterLevelY)
                        continue;

                    if (_config.ExcludeMountains && grid.GetBlock(x, topY, z) == BlockType.Stone)
                        continue;

                    candidates.Add((x, topY + 1, z));
                }
            }

            if (candidates.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[TreeScatter] No eligible candidates (all below water or excluded)");
                return;
            }

            var densityNoise = new FractalNoise(
                _config.DensityNoiseFrequency,
                4, 2f, 0.5f,
                seed);
            var clusterNoise = new FractalNoise(
                _config.ClusterNoiseFrequency,
                3, 2f, 0.5f,
                seed + 1);

            Shuffle(candidates, rng);

            int cellsX = (width / SpatialCellSize) + 2;
            int cellsZ = (depth / SpatialCellSize) + 2;
            var spatialGrid = new List<(int x, int y, int z)>[cellsX, cellsZ];
            for (int cx = 0; cx < cellsX; cx++)
                for (int cz = 0; cz < cellsZ; cz++)
                    spatialGrid[cx, cz] = new List<(int x, int y, int z)>();

            foreach (var (x, y, z) in candidates)
            {
                float densityVal = densityNoise.Sample(x, z);
                if (densityVal < _config.ClearThreshold)
                    continue;

                float clusterVal = clusterNoise.Sample(x, z);
                float localDensity = Mathf.Clamp01((densityVal - _config.ClearThreshold) / (1f - _config.ClearThreshold));
                localDensity *= (0.5f + 0.5f * clusterVal) * _config.ClusterDensityBoost;

                float p = Mathf.Max(0.3f, localDensity * _config.Density);
                if ((float)rng.NextDouble() >= p)
                    continue;

                float minDist = Mathf.Lerp(_config.MaxTreeDistance, _config.MinTreeDistance, localDensity);
                int cellX = x / SpatialCellSize + 1;
                int cellZ = z / SpatialCellSize + 1;

                bool tooClose = false;
                for (int dcx = -1; dcx <= 1 && !tooClose; dcx++)
                {
                    for (int dcz = -1; dcz <= 1 && !tooClose; dcz++)
                    {
                        int nx = cellX + dcx;
                        int nz = cellZ + dcz;
                        if (nx < 0 || nx >= cellsX || nz < 0 || nz >= cellsZ)
                            continue;

                        foreach (var (ox, oy, oz) in spatialGrid[nx, nz])
                        {
                            float dx = x - ox;
                            float dz = z - oz;
                            if (dx * dx + dz * dz < minDist * minDist)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                    }
                }

                if (tooClose)
                    continue;

                Vector3 worldPos = _worldScale.BlockToWorld(x + 0.5f, y, z + 0.5f);
                Quaternion rotation = _config.RandomRotation
                    ? Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f)
                    : Quaternion.identity;

                var instance = UnityEngine.Object.Instantiate(_config.TreePrefab, worldPos, rotation, _treeParent);
                instance.name = _config.TreePrefab.name;
                instance.transform.localScale = _worldScale.ScaleVectorForBlockSizedPrefab(_config.PrefabHeightInUnits) * _config.ScaleMultiplier;

                spatialGrid[cellX, cellZ].Add((x, y, z));
            }

            int placedCount = 0;
            for (int cx = 0; cx < spatialGrid.GetLength(0); cx++)
                for (int cz = 0; cz < spatialGrid.GetLength(1); cz++)
                    placedCount += spatialGrid[cx, cz].Count;
            UnityEngine.Debug.Log($"[TreeScatter] Placed {placedCount} trees from {candidates.Count} candidates");
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

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
