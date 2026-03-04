using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Performs removal of placed objects and roads. Supports single-block and area removal.</summary>
    public class RemovalExecutor
    {
        private readonly WorldBootstrap _worldBootstrap;

        public RemovalExecutor(WorldBootstrap worldBootstrap)
        {
            _worldBootstrap = worldBootstrap;
        }

        public bool RemoveAtBlock(int bx, int by, int bz)
        {
            if (_worldBootstrap == null) return false;
            bool removed = _worldBootstrap.RemoveAtBlock(bx, by, bz);
            if (removed)
                _worldBootstrap.SaveAndRefreshAfterRemoval();
            return removed;
        }

        public int RemoveInArea((int x, int z) start, (int x, int z) end)
        {
            if (_worldBootstrap?.Grid == null || _worldBootstrap.WaterConfig == null) return 0;

            var grid = _worldBootstrap.Grid;
            int waterLevelY = _worldBootstrap.WaterConfig.GetWaterLevelY(grid.Height);
            int removed = 0;

            foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, grid, waterLevelY))
            {
                if (_worldBootstrap.RemoveAtBlock(x, surfaceY, z))
                    removed++;
            }

            if (removed > 0)
                _worldBootstrap.SaveAndRefreshAfterRemoval();

            return removed;
        }
    }
}
