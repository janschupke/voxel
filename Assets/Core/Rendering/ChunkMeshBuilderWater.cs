using System.Collections.Generic;
using UnityEngine;
using Voxel;
using Voxel.Pure;

namespace Voxel.Rendering
{
    /// <summary>
    /// Collects water mesh faces for chunks. Extracted from ChunkMeshBuilder.
    /// </summary>
    public static class ChunkMeshBuilderWater
    {
        /// <summary>
        /// Collects water faces for air voxels below water level.
        /// Only the +Y face is added (Minecraft-style surface-only).
        /// </summary>
        public static void CollectWaterFaces(
            VoxelGrid grid, int ox, int oy, int oz, WaterConfig waterConfig, float voxelScale,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            int waterLevelY = waterConfig.GetWaterLevelY(grid.Height);

            for (int x = 0; x < ChunkMeshBuilder.ChunkSize; x++)
            {
                for (int y = 0; y < ChunkMeshBuilder.ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkMeshBuilder.ChunkSize; z++)
                    {
                        int wx = ox + x;
                        int wy = oy + y;
                        int wz = oz + z;

                        if (grid.IsSolid(wx, wy, wz))
                            continue;
                        if (wy > waterLevelY)
                            continue;

                        const float waterSurfaceOffset = -0.5f;
                        if (!IsWaterFaceVisible(grid, wx, wy, wz, 5, waterLevelY))
                            continue;
                        ChunkMeshBuilder.AddFaceToBand(x, y, z, 5, voxelScale, vertices, normals, triangles, waterSurfaceOffset);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a water face should be rendered.
        /// </summary>
        public static bool IsWaterFaceVisible(VoxelGrid grid, int x, int y, int z, int face, int waterLevelY)
        {
            (int nx, int ny, int nz) = face switch
            {
                0 => (x, y, z - 1),
                1 => (x, y, z + 1),
                2 => (x - 1, y, z),
                3 => (x + 1, y, z),
                4 => (x, y - 1, z),
                5 => (x, y + 1, z),
                _ => (x, y, z)
            };

            if (nx < 0 || nx >= grid.Width || ny < 0 || ny >= grid.Height || nz < 0 || nz >= grid.Depth)
                return true;

            if (grid.IsSolid(nx, ny, nz))
                return true;
            if (ny > waterLevelY)
                return true;
            return false;
        }
    }
}
