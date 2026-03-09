using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>Shared utility for world surface raycasting, footprint calculations, and prefab placement.</summary>
    public static class PlacementUtility
    {
        public const int DefaultVoxelsPerBlockAxis = 16;
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

        /// <summary>Footprint origin so the object centers on the cursor block. Use Floor so cursor block is in footprint and offset is minimized.</summary>
        public static (int originX, int originZ) GetFootprintOrigin(int cursorBx, int cursorBz, int sizeX, int sizeZ)
        {
            int originX = Mathf.FloorToInt(cursorBx - (sizeX - 1) / 2f);
            int originZ = Mathf.FloorToInt(cursorBz - (sizeZ - 1) / 2f);
            return (originX, originZ);
        }

        /// <summary>Geometric center of footprint in block coords (for pivot placement). Aligned to world cells.</summary>
        public static (float centerX, float centerZ) GetFootprintCenter(int originX, int originZ, int sizeX, int sizeZ)
        {
            float centerX = originX + (sizeX - 1) / 2f + 0.5f;
            float centerZ = originZ + (sizeZ - 1) / 2f + 0.5f;
            return (centerX, centerZ);
        }

        /// <summary>World-space offset so the model's geometric center aligns with the placement position in XZ. Y excluded so models stay on surface. Returns zero when pivot is already at center (bounds.center ≈ 0 in XZ).</summary>
        public static Vector3 PivotOffsetForCenteringXZ(Bounds meshBounds, Vector3 scale)
        {
            const float centerEpsilon = 0.01f;
            if (Mathf.Abs(meshBounds.center.x) < centerEpsilon && Mathf.Abs(meshBounds.center.z) < centerEpsilon)
                return Vector3.zero;
            return new Vector3(meshBounds.center.x * scale.x, 0f, meshBounds.center.z * scale.z);
        }

        /// <summary>Footprint origin from center (inverse of GetFootprintCenter). Used when deriving origin from world position.</summary>
        public static (int originX, int originZ) GetFootprintOriginFromCenter(float centerX, float centerZ, int sizeX, int sizeZ)
        {
            int originX = Mathf.FloorToInt(centerX - (sizeX - 1) / 2f - 0.5f);
            int originZ = Mathf.FloorToInt(centerZ - (sizeZ - 1) / 2f - 0.5f);
            return (originX, originZ);
        }

        /// <summary>Fills outBlocks with all blocks in the footprint. Reusable for registration, removal preview, environment hiding.</summary>
        public static void GetFootprintBlocks(int originX, int originZ, int baseY, int sizeX, int sizeZ, List<(int x, int y, int z)> outBlocks)
        {
            outBlocks.Clear();
            for (int dx = 0; dx < sizeX; dx++)
            {
                for (int dz = 0; dz < sizeZ; dz++)
                {
                    outBlocks.Add((originX + dx, baseY, originZ + dz));
                }
            }
        }

        /// <summary>Bounds for a voxel prefab. Uses mesh bounds if available, else fallback from size and voxelsPerBlock.</summary>
        public static Bounds GetPrefabBounds(GameObject prefab, int sizeX, int sizeZ, float heightInBlocks, int voxelsPerBlock = DefaultVoxelsPerBlockAxis)
        {
            if (prefab != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    return mf.sharedMesh.bounds;
            }
            var fallbackSize = new Vector3(sizeX * voxelsPerBlock, heightInBlocks * voxelsPerBlock, sizeZ * voxelsPerBlock);
            return new Bounds(Vector3.zero, fallbackSize);
        }
    }
}
