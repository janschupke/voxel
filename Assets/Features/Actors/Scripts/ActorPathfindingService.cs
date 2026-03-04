using System.Collections.Generic;
using UnityEngine;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Pure pathfinding logic for actors. Builds paths, resolves graphs, finds walkable nodes.
    /// Extracted from ActorBehavior.
    /// </summary>
    public class ActorPathfindingService
    {
        private readonly WorldBootstrap _worldBootstrap;

        public ActorPathfindingService(WorldBootstrap worldBootstrap)
        {
            _worldBootstrap = worldBootstrap;
        }

        public IReadOnlyList<GridNode> BuildPathTo(Vector3 fromWorld, Vector3 toWorld, WorldScale worldScale,
            ActorDefinition definition, bool fromIsBuilding = false, bool toIsBuilding = false)
        {
            var graph = GetPathGraph(definition);
            return BuildPathToInternal(fromWorld, toWorld, fromIsBuilding, toIsBuilding, worldScale, graph);
        }

        public IReadOnlyList<GridNode> BuildPathToHomeWithFallback(Vector3 fromWorld, Vector3 homeWorld, WorldScale worldScale,
            ActorDefinition definition)
        {
            var path = BuildPathToInternal(fromWorld, homeWorld, fromIsBuilding: false, toIsBuilding: true, worldScale, GetPathGraph(definition));
            if (path != null && path.Count > 0) return path;
            if (definition.PathingMode == ActorPathingMode.Road)
            {
                path = BuildPathToInternal(fromWorld, homeWorld, fromIsBuilding: false, toIsBuilding: true, worldScale, GetFallbackPathGraph(definition));
            }
            return path;
        }

        public bool IsNodeWalkable(GridNode node, ActorDefinition definition)
        {
            return GetPathGraph(definition).IsWalkable(node);
        }

        public IGridGraph<GridNode> GetPathGraph(ActorDefinition definition)
        {
            var grid = _worldBootstrap.Grid;
            var roadOverlay = _worldBootstrap.GetRoadOverlay();
            int waterLevelY = _worldBootstrap.WaterConfig != null
                ? _worldBootstrap.WaterConfig.GetWaterLevelY(grid.Height)
                : 0;

            bool isBlockValid(int x, int y, int z) =>
                !_worldBootstrap.BlocksPathingAtBlock(x, y, z);

            return definition.PathingMode switch
            {
                ActorPathingMode.Road => new RoadPathGraph(grid, waterLevelY, roadOverlay),
                ActorPathingMode.Free => new SurfacePathGraph(grid, waterLevelY, isBlockValid),
                ActorPathingMode.Smart => new SmartSurfacePathGraph(grid, waterLevelY, roadOverlay, isBlockValid),
                _ => new SurfacePathGraph(grid, waterLevelY, isBlockValid)
            };
        }

        public IGridGraph<GridNode> GetFallbackPathGraph(ActorDefinition definition)
        {
            var grid = _worldBootstrap.Grid;
            var roadOverlay = _worldBootstrap.GetRoadOverlay();
            int waterLevelY = _worldBootstrap.WaterConfig != null
                ? _worldBootstrap.WaterConfig.GetWaterLevelY(grid.Height)
                : 0;
            bool isBlockValid(int x, int y, int z) =>
                !_worldBootstrap.BlocksPathingAtBlock(x, y, z);
            return new SmartSurfacePathGraph(grid, waterLevelY, roadOverlay, isBlockValid);
        }

        private IReadOnlyList<GridNode> BuildPathToInternal(Vector3 fromWorld, Vector3 toWorld, bool fromIsBuilding, bool toIsBuilding,
            WorldScale worldScale, IGridGraph<GridNode> graph)
        {
            if (graph == null) return null;

            var (fx, _, fz) = worldScale.WorldToBlock(fromWorld);
            var (tx, _, tz) = worldScale.WorldToBlock(toWorld);

            var start = fromIsBuilding ? FindOptimalWalkableAdjacentCardinal(fx, fz, graph, tx, tz) : FindWalkableAdjacent(fx, fz, graph);
            var goal = toIsBuilding ? FindOptimalWalkableAdjacentCardinal(tx, tz, graph, fx, fz) : FindWalkableAdjacent(tx, tz, graph);

            if (!start.HasValue || !goal.HasValue)
                return null;

            return AStarPathfinder.FindPath(graph, start.Value, goal.Value);
        }

        private static GridNode? FindWalkableAdjacent(int bx, int bz, IGridGraph<GridNode> graph)
        {
            var node = new GridNode(bx, bz);
            if (graph.IsWalkable(node))
                return node;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var n = new GridNode(bx + dx, bz + dz);
                    if (graph.IsWalkable(n))
                        return n;
                }
            }
            return null;
        }

        private static GridNode? FindOptimalWalkableAdjacentCardinal(int bx, int bz, IGridGraph<GridNode> graph, int targetBx, int targetBz)
        {
            GridNode? best = null;
            float bestDistSq = float.MaxValue;

            foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new GridNode(bx + dx, bz + dz);
                if (!graph.IsWalkable(n)) continue;

                int dx2 = (bx + dx) - targetBx;
                int dz2 = (bz + dz) - targetBz;
                float distSq = dx2 * dx2 + dz2 * dz2;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = n;
                }
            }
            return best;
        }
    }
}
