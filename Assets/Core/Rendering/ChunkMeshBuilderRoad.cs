using System.Collections.Generic;
using UnityEngine;
using Voxel;
using Voxel.Pure;

namespace Voxel.Rendering
{
    /// <summary>
    /// Collects road overlay mesh faces for chunks. Extracted from ChunkMeshBuilder.
    /// </summary>
    public static class ChunkMeshBuilderRoad
    {
        private static readonly int[][] CubeFaces = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 0, 4, 5, 1 }, new[] { 3, 2, 6, 7 } };
        private static readonly Vector3[] FaceNormals = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, -1, 0), new(0, 1, 0) };
        private static readonly Vector3[] CubeVertices = { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1) };

        /// <summary>
        /// Collects road overlay quads for top faces of solid blocks where a road exists.
        /// </summary>
        public static void CollectRoadFaces(
            VoxelGrid grid, RoadOverlay roadOverlay, RoadConfig roadConfig,
            int ox, int oy, int oz, float voxelScale,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            const float roadYOffset = 0.01f;
            const float uvScale = 0.25f;
            Color tintGround = roadConfig.TintForGround;
            Color tintStone = roadConfig.TintForStone;

            for (int x = 0; x < ChunkMeshBuilder.ChunkSize; x++)
            {
                for (int y = 0; y < ChunkMeshBuilder.ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkMeshBuilder.ChunkSize; z++)
                    {
                        int wx = ox + x;
                        int wy = oy + y;
                        int wz = oz + z;

                        if (!grid.IsSolid(wx, wy, wz))
                            continue;
                        if (!ChunkMeshBuilder.IsFaceVisible(grid, wx, wy, wz, 5))
                            continue;

                        int surfaceY = wy + 1;
                        if (!roadOverlay.Contains(wx, surfaceY, wz))
                            continue;

                        byte blockType = grid.GetBlock(wx, wy, wz);
                        Color tint = blockType == BlockType.Stone ? tintStone : tintGround;

                        int baseIndex = vertices.Count;
                        var faceVertIndices = CubeFaces[5];
                        var normal = FaceNormals[5];

                        foreach (int vi in faceVertIndices)
                        {
                            var cv = CubeVertices[vi];
                            vertices.Add(new Vector3(
                                (x + cv.x) * voxelScale,
                                (y + cv.y + roadYOffset) * voxelScale,
                                (z + cv.z) * voxelScale));
                            normals.Add(normal);
                            uvs.Add(new Vector2((wx + cv.x) * uvScale, (wz + cv.z) * uvScale));
                            colors.Add(tint);
                        }

                        triangles.Add(baseIndex);
                        triangles.Add(baseIndex + 2);
                        triangles.Add(baseIndex + 1);
                        triangles.Add(baseIndex);
                        triangles.Add(baseIndex + 3);
                        triangles.Add(baseIndex + 2);
                    }
                }
            }
        }
    }
}
