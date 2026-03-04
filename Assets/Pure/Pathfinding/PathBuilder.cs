using System.Collections.Generic;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// Builds paths for line placement. Prefers 1-turn (L-shaped) path when possible,
    /// falls back to A* when obstacles block the direct path.
    /// </summary>
    public static class PathBuilder
    {
        /// <summary>
        /// Returns a path from start to end. Prefers 1-turn path; uses A* if blocked.
        /// Returns null if no path exists.
        /// </summary>
        public static IReadOnlyList<GridNode> BuildPath(IGridGraph<GridNode> graph, GridNode start, GridNode end)
        {
            var oneTurn = TryOneTurnPath(graph, start, end);
            if (oneTurn != null)
                return oneTurn;

            return AStarPathfinder.FindPath(graph, start, end);
        }

        /// <summary>
        /// Tries the two possible L-shaped (1-turn) paths. Returns the first valid one, or null.
        /// </summary>
        private static IReadOnlyList<GridNode> TryOneTurnPath(IGridGraph<GridNode> graph, GridNode start, GridNode end)
        {
            var path1 = BuildLPath(start.X, start.Z, end.X, end.Z, horizontalFirst: true);
            if (IsPathValid(graph, path1))
                return path1;

            var path2 = BuildLPath(start.X, start.Z, end.X, end.Z, horizontalFirst: false);
            if (IsPathValid(graph, path2))
                return path2;

            return null;
        }

        private static List<GridNode> BuildLPath(int sx, int sz, int ex, int ez, bool horizontalFirst)
        {
            var path = new List<GridNode>();

            if (horizontalFirst)
            {
                int stepX = ex > sx ? 1 : -1;
                for (int x = sx; x != ex; x += stepX)
                    path.Add(new GridNode(x, sz));
                int stepZ = ez > sz ? 1 : -1;
                for (int z = sz; z != ez; z += stepZ)
                    path.Add(new GridNode(ex, z));
            }
            else
            {
                int stepZ = ez > sz ? 1 : -1;
                for (int z = sz; z != ez; z += stepZ)
                    path.Add(new GridNode(sx, z));
                int stepX = ex > sx ? 1 : -1;
                for (int x = sx; x != ex; x += stepX)
                    path.Add(new GridNode(x, ez));
            }

            path.Add(new GridNode(ex, ez));
            return path;
        }

        private static bool IsPathValid(IGridGraph<GridNode> graph, List<GridNode> path)
        {
            foreach (var node in path)
            {
                if (!graph.IsWalkable(node))
                    return false;
            }
            return true;
        }
    }
}
