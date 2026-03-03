using System.Collections.Generic;
using UnityEngine;
using Voxel;
using Voxel.Core;

namespace Voxel.Rendering
{
    public static class ChunkMeshBuilder
    {
        public const int ChunkSize = 16;

        private static readonly Vector3[] CubeVertices =
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)
        };

        private static readonly int[][] CubeFaces =
        {
            new[] { 0, 1, 2, 3 }, // -Z
            new[] { 5, 4, 7, 6 }, // +Z
            new[] { 4, 0, 3, 7 }, // -X
            new[] { 1, 5, 6, 2 }, // +X
            new[] { 0, 4, 5, 1 }, // -Y
            new[] { 3, 2, 6, 7 }  // +Y
        };

        private static readonly Vector3[] FaceNormals =
        {
            new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, -1, 0), new(0, 1, 0)
        };

        public static Mesh Build(VoxelGrid grid, int chunkX, int chunkY, int chunkZ, float voxelScale = 1f,
            TerrainMaterialConfig terrainConfig = null)
        {
            int ox = chunkX * ChunkSize;
            int oy = chunkY * ChunkSize;
            int oz = chunkZ * ChunkSize;

            int bandCount = terrainConfig != null ? terrainConfig.BandCount : 1;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var trianglesPerBand = new List<int>[bandCount];
            for (int i = 0; i < bandCount; i++)
                trianglesPerBand[i] = new List<int>();

            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        int wx = ox + x;
                        int wy = oy + y;
                        int wz = oz + z;

                        if (!grid.IsSolid(wx, wy, wz))
                            continue;

                        float normalizedY = (oy + y) / (float)grid.Height;
                        int bandIndex = terrainConfig != null ? terrainConfig.GetMaterialIndex(normalizedY) : 0;
                        if (bandIndex >= bandCount) bandIndex = bandCount - 1;

                        for (int f = 0; f < 6; f++)
                        {
                            if (!IsFaceVisible(grid, wx, wy, wz, f))
                                continue;

                            int baseIndex = vertices.Count;
                            foreach (int vi in CubeFaces[f])
                            {
                                vertices.Add(new Vector3(
                                    (x + CubeVertices[vi].x) * voxelScale,
                                    (y + CubeVertices[vi].y) * voxelScale,
                                    (z + CubeVertices[vi].z) * voxelScale));
                                normals.Add(FaceNormals[f]);
                            }
                            var triangles = trianglesPerBand[bandIndex];
                            triangles.Add(baseIndex);
                            triangles.Add(baseIndex + 1);
                            triangles.Add(baseIndex + 2);
                            triangles.Add(baseIndex);
                            triangles.Add(baseIndex + 2);
                            triangles.Add(baseIndex + 3);
                        }
                    }
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = bandCount;
            for (int i = 0; i < bandCount; i++)
                mesh.SetTriangles(trianglesPerBand[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool IsFaceVisible(VoxelGrid grid, int x, int y, int z, int face)
        {
            return face switch
            {
                0 => !grid.IsSolid(x, y, z - 1),
                1 => !grid.IsSolid(x, y, z + 1),
                2 => !grid.IsSolid(x - 1, y, z),
                3 => !grid.IsSolid(x + 1, y, z),
                4 => !grid.IsSolid(x, y - 1, z),
                5 => !grid.IsSolid(x, y + 1, z),
                _ => false
            };
        }
    }
}
