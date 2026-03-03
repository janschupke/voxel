using System.Collections.Generic;
using UnityEngine;
using Voxel;
using Voxel.Core;

namespace Voxel.Rendering
{
    /// <summary>
    /// Builds chunk meshes from a voxel grid. Each chunk is split into separate meshes per
    /// material band (height-based), so each band gets its own MeshRenderer for correct opaque rendering.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public const int ChunkSize = 16;

        /// <summary>
        /// Unit-cube vertex positions. Indexed by CubeFaces to form quad vertices for each face.
        /// </summary>
        private static readonly Vector3[] CubeVertices =
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)
        };

        /// <summary>
        /// For each of the 6 cube faces, the 4 vertex indices (into CubeVertices) that form the quad.
        /// Order ensures correct winding for Unity's clockwise front-face culling.
        /// </summary>
        private static readonly int[][] CubeFaces =
        {
            new[] { 0, 1, 2, 3 }, // -Z
            new[] { 5, 4, 7, 6 }, // +Z
            new[] { 4, 0, 3, 7 }, // -X
            new[] { 1, 5, 6, 2 }, // +X
            new[] { 0, 4, 5, 1 }, // -Y
            new[] { 3, 2, 6, 7 }  // +Y
        };

        /// <summary>
        /// Outward-facing normal for each cube face. Matches CubeFaces index.
        /// </summary>
        private static readonly Vector3[] FaceNormals =
        {
            new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, -1, 0), new(0, 1, 0)
        };

        /// <summary>
        /// Builds separate meshes per material band to avoid multi-submesh transparency issues with URP.
        /// Returns one mesh per band; when terrainConfig is null, returns a single mesh.
        /// When waterConfig is provided and enabled, appends a water mesh at the end.
        /// </summary>
        /// <param name="grid">The voxel grid to sample.</param>
        /// <param name="chunkX">Chunk index in X.</param>
        /// <param name="chunkY">Chunk index in Y.</param>
        /// <param name="chunkZ">Chunk index in Z.</param>
        /// <param name="voxelScale">Scale factor for vertex positions (e.g. 8 for 8 units per voxel).</param>
        /// <param name="terrainConfig">Optional height-based material config. Null = single band.</param>
        /// <param name="waterConfig">Optional water config. When enabled, adds water mesh for air below water level.</param>
        /// <param name="mountainMaterial">Optional material for Stone blocks (mountain stage). When set, Stone blocks use this instead of height bands.</param>
        /// <param name="roadOverlay">Optional road overlay. When set with roadConfig, adds road mesh on top faces.</param>
        /// <param name="roadConfig">Optional road config for material and block tints.</param>
        public static Mesh[] Build(VoxelGrid grid, int chunkX, int chunkY, int chunkZ, float voxelScale = 1f,
            TerrainMaterialConfig terrainConfig = null, WaterConfig waterConfig = null, Material mountainMaterial = null,
            RoadOverlay roadOverlay = null, RoadConfig roadConfig = null)
        {
            var (ox, oy, oz) = GetChunkOrigin(chunkX, chunkY, chunkZ);
            int terrainBandCount = terrainConfig != null ? terrainConfig.BandCount : 1;
            int bandCount = terrainBandCount + (mountainMaterial != null ? 1 : 0);

            var verticesPerBand = CreateBandBuffers<Vector3>(bandCount);
            var normalsPerBand = CreateBandBuffers<Vector3>(bandCount);
            var trianglesPerBand = CreateBandBuffers<int>(bandCount);

            CollectVisibleFaces(grid, ox, oy, oz, terrainBandCount, bandCount, terrainConfig, mountainMaterial, voxelScale,
                verticesPerBand, normalsPerBand, trianglesPerBand);

            var meshes = CreateMeshesFromBands(verticesPerBand, normalsPerBand, trianglesPerBand, bandCount);

            if (waterConfig != null && waterConfig.Enabled)
            {
                var waterVertices = new List<Vector3>();
                var waterNormals = new List<Vector3>();
                var waterTriangles = new List<int>();
                CollectWaterFaces(grid, ox, oy, oz, waterConfig, voxelScale,
                    waterVertices, waterNormals, waterTriangles);
                Mesh waterMesh = waterTriangles.Count > 0
                    ? CreateMesh(waterVertices, waterNormals, waterTriangles)
                    : null;
                var combined = new Mesh[meshes.Length + 1];
                for (int i = 0; i < meshes.Length; i++)
                    combined[i] = meshes[i];
                combined[meshes.Length] = waterMesh;
                meshes = combined;
            }

            if (roadOverlay != null && roadConfig != null && roadConfig.RoadMaterial != null)
            {
                var roadVertices = new List<Vector3>();
                var roadNormals = new List<Vector3>();
                var roadUVs = new List<Vector2>();
                var roadColors = new List<Color>();
                var roadTriangles = new List<int>();
                CollectRoadFaces(grid, roadOverlay, roadConfig, ox, oy, oz, voxelScale,
                    roadVertices, roadNormals, roadUVs, roadColors, roadTriangles);
                Mesh roadMesh = roadTriangles.Count > 0
                    ? CreateMeshWithUVsAndColors(roadVertices, roadNormals, roadUVs, roadColors, roadTriangles)
                    : null;
                var combined = new Mesh[meshes.Length + 1];
                for (int i = 0; i < meshes.Length; i++)
                    combined[i] = meshes[i];
                combined[meshes.Length] = roadMesh;
                meshes = combined;
            }

            return meshes;
        }

        /// <summary>
        /// Returns the world-space origin of the chunk (min corner in voxel coordinates).
        /// </summary>
        private static (int ox, int oy, int oz) GetChunkOrigin(int chunkX, int chunkY, int chunkZ)
        {
            return (
                chunkX * ChunkSize,
                chunkY * ChunkSize,
                chunkZ * ChunkSize
            );
        }

        /// <summary>
        /// Creates one list per band for storing mesh data. Used for vertices, normals, and triangles.
        /// </summary>
        private static List<T>[] CreateBandBuffers<T>(int bandCount)
        {
            var buffers = new List<T>[bandCount];
            for (int i = 0; i < bandCount; i++)
                buffers[i] = new List<T>();
            return buffers;
        }

        /// <summary>
        /// Iterates over all blocks in the chunk and adds visible faces to the appropriate band buffers.
        /// Faces are only added when the adjacent block is air (greedy meshing / face culling).
        /// Stone blocks use mountain band when mountainMaterial is provided; others use height-based bands.
        /// </summary>
        private static void CollectVisibleFaces(
            VoxelGrid grid, int ox, int oy, int oz, int terrainBandCount, int bandCount,
            TerrainMaterialConfig terrainConfig, Material mountainMaterial, float voxelScale,
            List<Vector3>[] verticesPerBand, List<Vector3>[] normalsPerBand, List<int>[] trianglesPerBand)
        {
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

                        int bandIndex;
                        if (mountainMaterial != null && grid.GetBlock(wx, wy, wz) == BlockType.Stone)
                            bandIndex = terrainBandCount;
                        else
                            bandIndex = GetBandIndex(wy, grid.Height, terrainBandCount, terrainConfig);

                        for (int face = 0; face < 6; face++)
                        {
                            if (!IsFaceVisible(grid, wx, wy, wz, face))
                                continue;

                            AddFaceToBand(
                                x, y, z, face, voxelScale,
                                verticesPerBand[bandIndex],
                                normalsPerBand[bandIndex],
                                trianglesPerBand[bandIndex]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the material band index for a block at the given world Y.
        /// Uses normalized height (0–1) so band thresholds work across different grid sizes.
        /// </summary>
        private static int GetBandIndex(int worldY, int gridHeight, int terrainBandCount, TerrainMaterialConfig terrainConfig)
        {
            if (terrainConfig == null) return 0;

            float normalizedY = gridHeight > 0
                ? Mathf.Clamp01((worldY + 0.5f) / gridHeight)
                : 0f;
            int index = terrainConfig.GetMaterialIndex(normalizedY);
            return index >= terrainBandCount ? terrainBandCount - 1 : index;
        }

        /// <summary>
        /// Adds a single quad face to the band's vertex, normal, and triangle lists.
        /// Triangle winding is reversed so the front face (outside the block) is visible with Unity's CW culling.
        /// </summary>
        /// <param name="yOffset">Optional Y offset in blocks (e.g. 0.5 to raise water surface).</param>
        private static void AddFaceToBand(
            int x, int y, int z, int face, float voxelScale,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float yOffset = 0f)
        {
            int baseIndex = vertices.Count;
            var faceVertIndices = CubeFaces[face];
            var normal = FaceNormals[face];

            foreach (int vi in faceVertIndices)
            {
                vertices.Add(new Vector3(
                    (x + CubeVertices[vi].x) * voxelScale,
                    (y + CubeVertices[vi].y + yOffset) * voxelScale,
                    (z + CubeVertices[vi].z) * voxelScale));
                normals.Add(normal);
            }

            // Two triangles: (0,2,1) and (0,3,2). Reversed winding for correct front-face culling.
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);
        }

        /// <summary>
        /// Builds one Unity Mesh per band from the collected vertices, normals, and triangles.
        /// Returns null for bands with no geometry (e.g. no snow in a low-elevation chunk).
        /// </summary>
        private static Mesh[] CreateMeshesFromBands(
            List<Vector3>[] verticesPerBand, List<Vector3>[] normalsPerBand, List<int>[] trianglesPerBand,
            int bandCount)
        {
            var meshes = new Mesh[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                if (trianglesPerBand[i].Count == 0)
                {
                    meshes[i] = null;
                    continue;
                }
                meshes[i] = CreateMesh(verticesPerBand[i], normalsPerBand[i], trianglesPerBand[i]);
            }
            return meshes;
        }

        /// <summary>
        /// Creates a Unity Mesh from vertex, normal, and triangle data.
        /// </summary>
        private static Mesh CreateMesh(List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a Unity Mesh with UVs and vertex colors (for road overlay).
        /// </summary>
        private static Mesh CreateMeshWithUVsAndColors(
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Collects road overlay quads for top faces of solid blocks where a road exists.
        /// Road at (wx, wy+1, wz) means road on top face of block (wx, wy, wz).
        /// </summary>
        private static void CollectRoadFaces(
            VoxelGrid grid, RoadOverlay roadOverlay, RoadConfig roadConfig,
            int ox, int oy, int oz, float voxelScale,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            const float roadYOffset = 0.01f;
            const float uvScale = 0.25f;
            Color tintGround = roadConfig.TintForGround;
            Color tintStone = roadConfig.TintForStone;

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
                        if (!IsFaceVisible(grid, wx, wy, wz, 5))
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

        /// <summary>
        /// Returns true if the face should be rendered. A face is visible only when the adjacent
        /// block is air (or out of bounds), so we don't draw interior faces between solid blocks.
        /// </summary>
        /// <param name="face">0=-Z, 1=+Z, 2=-X, 3=+X, 4=-Y, 5=+Y</param>
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

        /// <summary>
        /// Collects water faces for air voxels below water level. When surfaceOnly is true,
        /// only the +Y face is added (Minecraft-style). Otherwise all visible faces are added.
        /// </summary>
        private static void CollectWaterFaces(
            VoxelGrid grid, int ox, int oy, int oz, WaterConfig waterConfig, float voxelScale,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            int waterLevelY = waterConfig.GetWaterLevelY(grid.Height);
            bool surfaceOnly = waterConfig.SurfaceOnly;

            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        int wx = ox + x;
                        int wy = oy + y;
                        int wz = oz + z;

                        if (grid.IsSolid(wx, wy, wz))
                            continue;
                        if (wy > waterLevelY)
                            continue;

                        const float waterSurfaceOffset = -0.5f;
                        if (surfaceOnly)
                        {
                            if (!IsWaterFaceVisible(grid, wx, wy, wz, 5, waterLevelY))
                                continue;
                            AddFaceToBand(x, y, z, 5, voxelScale, vertices, normals, triangles, waterSurfaceOffset);
                        }
                        else
                        {
                            for (int face = 0; face < 6; face++)
                            {
                                if (!IsWaterFaceVisible(grid, wx, wy, wz, face, waterLevelY))
                                    continue;
                                AddFaceToBand(x, y, z, face, voxelScale, vertices, normals, triangles, waterSurfaceOffset);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a water face should be rendered. A face is visible when the adjacent
        /// voxel is not water (i.e. solid, or air above water level, or out of bounds).
        /// </summary>
        /// <param name="face">0=-Z, 1=+Z, 2=-X, 3=+X, 4=-Y, 5=+Y</param>
        private static bool IsWaterFaceVisible(VoxelGrid grid, int x, int y, int z, int face, int waterLevelY)
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
