using UnityEngine;
using UnityEngine.InputSystem;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Shared utility for world surface raycasting and block queries.</summary>
    public static class PlacementUtility
    {
        public static bool TryRaycastTopSurface(Camera cam, VoxelGrid grid, WorldScale scale, int waterLevelY,
            out (int bx, int by, int bz) block, out bool valid)
        {
            block = default;
            valid = false;

            if (Mouse.current == null) return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            float blockScale = scale.BlockScale;
            if (blockScale <= 0f) return false;

            for (int surfaceY = grid.Height; surfaceY >= 1; surfaceY--)
            {
                float planeY = surfaceY * blockScale;
                float dy = ray.direction.y;
                if (Mathf.Abs(dy) < 0.0001f) continue;

                float t = (planeY - ray.origin.y) / dy;
                if (t <= 0f) continue;

                Vector3 hitWorld = ray.origin + ray.direction * t;
                var (bx, _, bz) = scale.WorldToBlock(hitWorld);

                if (bx < 0 || bx >= grid.Width || bz < 0 || bz >= grid.Depth)
                    continue;

                int topY = GetTopSolidY(grid, bx, bz, grid.Height);
                if (topY < 0 || topY + 1 != surfaceY)
                    continue;

                block = (bx, topY + 1, bz);
                valid = topY >= waterLevelY;
                return true;
            }

            return false;
        }

        public static bool TryRaycastTopSurface(Camera cam, VoxelGrid grid, WorldScale scale,
            out (int bx, int by, int bz) block)
        {
            return TryRaycastTopSurface(cam, grid, scale, 0, out block, out _);
        }

        public static int GetTopSolidY(VoxelGrid grid, int x, int z, int gridHeight) =>
            Voxel.Pure.BlockQueries.GetTopSolidY(grid, x, z, gridHeight);
    }
}
