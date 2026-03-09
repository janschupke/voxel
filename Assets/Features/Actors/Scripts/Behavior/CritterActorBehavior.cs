using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Critter actor: wanders within radius of spawn point. No items or production.
    /// </summary>
    public class CritterActorBehavior : ActorBehavior
    {
        private const int MaxWanderAttempts = 16;

        private CritterConfig Config => Definition?.CategoryConfig as CritterConfig;
        private float WanderRadiusBlocks => Config?.WanderRadiusBlocks ?? 5f;

        protected override bool SkipWorkOutside => true;

        protected override void OnArrivedAtTarget() { }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            if (HomeBuilding == null || WorldBootstrap?.Grid == null) return (null, false);

            var config = Config;
            if (config == null) return (null, false);

            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);
            var grid = WorldBootstrap.Grid;
            var radius = Mathf.Max(1, (int)(config.WanderRadiusBlocks + 0.5f));

            for (int attempt = 0; attempt < MaxWanderAttempts; attempt++)
            {
                int dx = Random.Range(-radius, radius + 1);
                int dz = Random.Range(-radius, radius + 1);
                if (dx == 0 && dz == 0) continue;

                int tx = hx + dx;
                int tz = hz + dz;
                if (tx < 0 || tx >= grid.Width || tz < 0 || tz >= grid.Depth) continue;

                int topY = PlacementUtility.GetTopSolidY(grid, tx, tz, grid.Height);
                if (topY < 0) continue;

                int surfaceY = topY + 1;
                var targetWorld = worldScale.BlockToWorld(tx + 0.5f, surfaceY, tz + 0.5f);

                var path = BuildPathTo(HomeBuilding.position, targetWorld, fromIsBuilding: false, toIsBuilding: false);
                if (path != null && path.Count > 0)
                {
                    GameDebugLogger.Log($"[Critter] {gameObject.name} TryGetTarget: wander to ({tx},{tz})");
                    return (targetWorld, true);
                }
            }

            return (null, false);
        }

        protected override void OnWorkCompletedInside() { }

        protected override bool IsBuildingInventoryFull() => false;
    }
}
