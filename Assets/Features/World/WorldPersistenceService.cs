using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    public static class WorldPersistenceService
    {
        private const string WorldFileName = "world.dat";
        private const int SaveVersion = 2;
        private static string WorldPath => Path.Combine(Application.persistentDataPath, "World", WorldFileName);

        public static bool WorldExists()
        {
            return File.Exists(WorldPath);
        }

        public static void DeleteWorld()
        {
            if (File.Exists(WorldPath))
                File.Delete(WorldPath);
        }

        public static void Save(VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects = null)
        {
            var dir = Path.GetDirectoryName(WorldPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var file = File.Create(WorldPath);
            using var gzip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);

            using var writer = new BinaryWriter(gzip);
            writer.Write(SaveVersion);
            writer.Write(grid.Width);
            writer.Write(grid.Depth);
            writer.Write(grid.Height);

            var blocks = grid.GetBlocksCopy();
            writer.Write(blocks.Length);
            writer.Write(blocks);

            int count = placedObjects?.Count ?? 0;
            writer.Write(count);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = placedObjects[i];
                    writer.Write(p.EntryName ?? "");
                    writer.Write(p.BlockX);
                    writer.Write(p.BlockY);
                    writer.Write(p.BlockZ);
                    writer.Write(p.RotationY);
                }
            }
        }

        public static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects) Load()
        {
            if (!WorldExists())
                throw new FileNotFoundException("No saved world found", WorldPath);

            using var file = File.OpenRead(WorldPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);

            using var reader = new BinaryReader(gzip);
            int version = reader.ReadInt32();

            if (version < 100)
            {
                return LoadV2(reader);
            }

            return LoadV1(reader, version);
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects) LoadV2(BinaryReader reader)
        {
            int width = reader.ReadInt32();
            int depth = reader.ReadInt32();
            int height = reader.ReadInt32();
            int blockCount = reader.ReadInt32();

            var grid = new VoxelGrid(width, depth, height);
            var blocks = reader.ReadBytes(blockCount);
            grid.LoadBlocks(blocks);

            List<PlacedObjectData> placedObjects = null;
            try
            {
                int count = reader.ReadInt32();
                if (count > 0 && count < 1000000)
                {
                    placedObjects = new List<PlacedObjectData>(count);
                    for (int i = 0; i < count; i++)
                    {
                        placedObjects.Add(new PlacedObjectData(
                            reader.ReadString(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle()));
                    }
                }
            }
            catch (EndOfStreamException)
            {
                placedObjects = new List<PlacedObjectData>();
            }

            return (grid, placedObjects ?? new List<PlacedObjectData>());
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects) LoadV1(BinaryReader reader, int width)
        {
            int depth = reader.ReadInt32();
            int height = reader.ReadInt32();
            int blockCount = reader.ReadInt32();

            var grid = new VoxelGrid(width, depth, height);
            var blocks = reader.ReadBytes(blockCount);
            grid.LoadBlocks(blocks);

            var placedObjects = new List<PlacedObjectData>();
            try
            {
                int treeCount = reader.ReadInt32();
                if (treeCount > 0 && treeCount < 1000000)
                {
                    for (int i = 0; i < treeCount; i++)
                    {
                        placedObjects.Add(new PlacedObjectData("Tree",
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle()));
                    }
                }

                int houseCount = reader.ReadInt32();
                if (houseCount > 0 && houseCount < 1000000)
                {
                    for (int i = 0; i < houseCount; i++)
                    {
                        placedObjects.Add(new PlacedObjectData("House",
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle()));
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }

            return (grid, placedObjects);
        }
    }
}
