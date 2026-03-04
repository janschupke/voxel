using System;
using System.Collections.Generic;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// A* pathfinding algorithm. Reusable for any graph implementing IGridGraph.
    /// </summary>
    public static class AStarPathfinder
    {
        /// <summary>
        /// Finds the shortest path from start to goal. Returns null if no path exists.
        /// </summary>
        public static IReadOnlyList<TNode> FindPath<TNode>(IGridGraph<TNode> graph, TNode start, TNode goal)
            where TNode : struct, IEquatable<TNode>
        {
            if (!graph.IsWalkable(start) || !graph.IsWalkable(goal))
                return null;

            var openSet = new PriorityQueue<TNode>();
            var cameFrom = new Dictionary<TNode, TNode>();
            var gScore = new Dictionary<TNode, float>();
            var closedSet = new HashSet<TNode>();

            openSet.Enqueue(start, 0f);
            gScore[start] = 0f;

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                if (current.Equals(goal))
                    return ReconstructPath(cameFrom, current);

                if (closedSet.Contains(current))
                    continue;
                closedSet.Add(current);

                float currentG = gScore[current];

                foreach (var neighbor in graph.GetNeighbors(current))
                {
                    if (!graph.IsWalkable(neighbor) || closedSet.Contains(neighbor))
                        continue;

                    float moveCost = graph.GetCost(current, neighbor);
                    float tentativeG = currentG + moveCost;

                    if (!gScore.TryGetValue(neighbor, out float neighborG) || tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float f = tentativeG + graph.GetHeuristic(neighbor, goal);
                        openSet.Enqueue(neighbor, f);
                    }
                }
            }

            return null;
        }

        private static IReadOnlyList<TNode> ReconstructPath<TNode>(Dictionary<TNode, TNode> cameFrom, TNode current)
            where TNode : struct, IEquatable<TNode>
        {
            var path = new List<TNode> { current };
            while (cameFrom.TryGetValue(current, out var prev))
            {
                path.Add(prev);
                current = prev;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Simple min-heap priority queue for A*.
        /// </summary>
        private class PriorityQueue<T>
        {
            private readonly List<(T item, float priority)> _heap = new();

            public int Count => _heap.Count;

            public void Enqueue(T item, float priority)
            {
                _heap.Add((item, priority));
                int i = _heap.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_heap[parent].priority <= _heap[i].priority) break;
                    (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                    i = parent;
                }
            }

            public T Dequeue()
            {
                var top = _heap[0].item;
                _heap[0] = _heap[_heap.Count - 1];
                _heap.RemoveAt(_heap.Count - 1);

                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;
                    int smallest = i;
                    if (left < _heap.Count && _heap[left].priority < _heap[smallest].priority)
                        smallest = left;
                    if (right < _heap.Count && _heap[right].priority < _heap[smallest].priority)
                        smallest = right;
                    if (smallest == i) break;
                    (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                    i = smallest;
                }
                return top;
            }
        }
    }
}
