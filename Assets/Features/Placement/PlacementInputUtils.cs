using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Shared input utilities for placement and removal: block-under-mouse, pointer-over-UI.</summary>
    public static class PlacementInputUtils
    {
        /// <summary>Returns (bx, bz) of block under mouse, or null if no hit.</summary>
        public static (int x, int z)? GetBlockUnderMouse(Camera cam, VoxelGrid grid, WorldScale scale)
        {
            if (cam == null || grid == null) return null;
            if (PlacementUtility.TryRaycastTopSurface(cam, grid, scale, out var block))
                return (block.bx, block.bz);
            return null;
        }
    }
}
