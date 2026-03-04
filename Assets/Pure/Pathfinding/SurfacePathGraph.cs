using System.Collections.Generic;
using Voxel;
using Voxel.Pure;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// Grid graph for surface placement pathfinding. Uses 4-connected neighbors (cardinal directions).
    /// </summary>
    public class SurfacePathGraph : IGridGraph<GridNode>
    {
        private readonly VoxelGrid _grid;
        private readonly int _waterLevelY;
        private readonly System.Func<int, int, int, bool> _isBlockValid;

        public SurfacePathGraph(VoxelGrid grid, int waterLevelY, System.Func<int, int, int, bool> isBlockValid)
        {
            _grid = grid;
            _waterLevelY = waterLevelY;
            _isBlockValid = isBlockValid;
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

            int topY = BlockQueries.GetTopSolidY(_grid, node.X, node.Z, _grid.Height);
            if (topY < 0 || topY < _waterLevelY)
                return false;

            int surfaceY = topY + 1;
            return _isBlockValid(node.X, surfaceY, node.Z);
        }

        public float GetHeuristic(GridNode from, GridNode goal) =>
            System.Math.Abs(from.X - goal.X) + System.Math.Abs(from.Z - goal.Z);
    }
}
