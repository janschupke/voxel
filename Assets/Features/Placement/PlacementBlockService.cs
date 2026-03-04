using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;
using Voxel.Pathfinding;

namespace Voxel
{
    /// <summary>
    /// Path and block queries for placement: line pathfinding, area block enumeration.
    /// </summary>
    public static class PlacementBlockService
    {
        /// <summary>
        /// Builds a path for line placement from start to end. Returns null if no path exists.
        /// </summary>
        public static IReadOnlyList<GridNode> GetPathForLine(
            (int x, int z) start, (int x, int z) end,
            VoxelGrid grid, int waterLevelY,
            System.Func<int, int, int, bool> isBlockValid)
        {
            if (grid == null || isBlockValid == null) return null;

            var graph = new SurfacePathGraph(grid, waterLevelY, isBlockValid);
            return PathBuilder.BuildPath(graph, new GridNode(start.x, start.z), new GridNode(end.x, end.z));
        }

        /// <summary>
        /// Enumerates surface blocks in the rectangle for area placement. Yields (x, surfaceY, z) for valid blocks.
        /// </summary>
        public static IEnumerable<(int x, int y, int z)> GetBlocksForArea(
            (int x, int z) start, (int x, int z) end,
            VoxelGrid grid, int waterLevelY)
        {
            if (grid == null) yield break;

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (x < 0 || x >= grid.Width || z < 0 || z >= grid.Depth) continue;

                    int topY = PlacementUtility.GetTopSolidY(grid, x, z, grid.Height);
                    if (topY < 0 || topY < waterLevelY) continue;

                    int surfaceY = topY + 1;
                    yield return (x, surfaceY, z);
                }
            }
        }
    }
}
