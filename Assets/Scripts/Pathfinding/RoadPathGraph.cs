using System.Collections.Generic;
using Voxel;
using Voxel.Core;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// Grid graph for actor pathfinding on roads only. Path must be entirely on road blocks.
    /// </summary>
    public class RoadPathGraph : IGridGraph<GridNode>
    {
        private readonly VoxelGrid _grid;
        private readonly int _waterLevelY;
        private readonly RoadOverlay _roadOverlay;

        public RoadPathGraph(VoxelGrid grid, int waterLevelY, RoadOverlay roadOverlay)
        {
            _grid = grid;
            _waterLevelY = waterLevelY;
            _roadOverlay = roadOverlay;
        }

        public IEnumerable<GridNode> GetNeighbors(GridNode node)
        {
            yield return new GridNode(node.X + 1, node.Z);
            yield return new GridNode(node.X - 1, node.Z);
            yield return new GridNode(node.X, node.Z + 1);
            yield return new GridNode(node.X, node.Z - 1);
        }

        public float GetCost(GridNode from, GridNode to) => 1f;

        public bool IsWalkable(GridNode node)
        {
            if (node.X < 0 || node.X >= _grid.Width || node.Z < 0 || node.Z >= _grid.Depth)
                return false;

            int topY = PlacementUtility.GetTopSolidY(_grid, node.X, node.Z, _grid.Height);
            if (topY < 0 || topY < _waterLevelY)
                return false;

            int surfaceY = topY + 1;
            return _roadOverlay.Contains(node.X, surfaceY, node.Z);
        }

        public float GetHeuristic(GridNode from, GridNode goal) =>
            System.Math.Abs(from.X - goal.X) + System.Math.Abs(from.Z - goal.Z);
    }
}
