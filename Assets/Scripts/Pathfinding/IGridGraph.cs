using System.Collections.Generic;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// Interface for a grid-based graph used by pathfinding algorithms.
    /// </summary>
    /// <typeparam name="TNode">Node type (e.g. (int x, int z) for 2D surface).</typeparam>
    public interface IGridGraph<TNode> where TNode : struct
    {
        IEnumerable<TNode> GetNeighbors(TNode node);
        float GetCost(TNode from, TNode to);
        bool IsWalkable(TNode node);
        float GetHeuristic(TNode from, TNode goal);
    }
}
