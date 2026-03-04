using System.Collections.Generic;
using Voxel;
using Voxel.Core;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// Surface path graph that prefers road blocks (lower cost) but allows any walkable path.
    /// </summary>
    public class SmartSurfacePathGraph : IGridGraph<GridNode>
    {
        private readonly VoxelGrid _grid;
        private readonly int _waterLevelY;
        private readonly RoadOverlay _roadOverlay;
        private readonly System.Func<int, int, int, bool> _isBlockValid;

        private const float RoadCost = 0.5f;
        private const float LandCost = 1f;

        public SmartSurfacePathGraph(VoxelGrid grid, int waterLevelY, RoadOverlay roadOverlay,
            System.Func<int, int, int, bool> isBlockValid)
        {
            _grid = grid;
            _waterLevelY = waterLevelY;
            _roadOverlay = roadOverlay;
            _isBlockValid = isBlockValid;
        }

        public IEnumerable<GridNode> GetNeighbors(GridNode node)
        {
            yield return new GridNode(node.X + 1, node.Z);
            yield return new GridNode(node.X - 1, node.Z);
            yield return new GridNode(node.X, node.Z + 1);
            yield return new GridNode(node.X, node.Z - 1);
        }

        public float GetCost(GridNode from, GridNode to)
        {
            int topY = PlacementUtility.GetTopSolidY(_grid, to.X, to.Z, _grid.Height);
            if (topY < 0) return LandCost;
            int surfaceY = topY + 1;
            return _roadOverlay.Contains(to.X, surfaceY, to.Z) ? RoadCost : LandCost;
        }

        public bool IsWalkable(GridNode node)
        {
            if (node.X < 0 || node.X >= _grid.Width || node.Z < 0 || node.Z >= _grid.Depth)
                return false;

            int topY = PlacementUtility.GetTopSolidY(_grid, node.X, node.Z, _grid.Height);
            if (topY < 0 || topY < _waterLevelY)
                return false;

            int surfaceY = topY + 1;
            return _isBlockValid(node.X, surfaceY, node.Z);
        }

        public float GetHeuristic(GridNode from, GridNode goal) =>
            System.Math.Abs(from.X - goal.X) + System.Math.Abs(from.Z - goal.Z);
    }
}
