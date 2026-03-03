using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    public static class WorldPersistenceService
    {
        private const string WorldFileName = "world.dat";
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

        public static void Save(VoxelGrid grid, IReadOnlyList<TreePlacementData> trees = null, IReadOnlyList<HousePlacementData> houses = null)
        {
            var dir = Path.GetDirectoryName(WorldPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var file = File.Create(WorldPath);
            using var gzip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);

            using var writer = new BinaryWriter(gzip);
            writer.Write(grid.Width);
            writer.Write(grid.Depth);
            writer.Write(grid.Height);

            var blocks = grid.GetBlocksCopy();
            writer.Write(blocks.Length);
            writer.Write(blocks);

            int treeCount = trees?.Count ?? 0;
            writer.Write(treeCount);
            if (treeCount > 0)
            {
                for (int i = 0; i < treeCount; i++)
                {
                    var t = trees[i];
                    writer.Write(t.BlockX);
                    writer.Write(t.BlockY);
                    writer.Write(t.BlockZ);
                    writer.Write(t.RotationY);
                }
            }

            int houseCount = houses?.Count ?? 0;
            writer.Write(houseCount);
            if (houseCount > 0)
            {
                for (int i = 0; i < houseCount; i++)
                {
                    var h = houses[i];
                    writer.Write(h.BlockX);
                    writer.Write(h.BlockY);
                    writer.Write(h.BlockZ);
                    writer.Write(h.RotationY);
                }
            }
        }

        public static (VoxelGrid grid, IReadOnlyList<TreePlacementData> trees, IReadOnlyList<HousePlacementData> houses) Load()
        {
            if (!WorldExists())
                throw new FileNotFoundException("No saved world found", WorldPath);

            using var file = File.OpenRead(WorldPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);

            using var reader = new BinaryReader(gzip);
            int width = reader.ReadInt32();
            int depth = reader.ReadInt32();
            int height = reader.ReadInt32();
            int blockCount = reader.ReadInt32();

            var grid = new VoxelGrid(width, depth, height);
            var blocks = reader.ReadBytes(blockCount);
            grid.LoadBlocks(blocks);

            List<TreePlacementData> trees = null;
            List<HousePlacementData> houses = null;
            try
            {
                int treeCount = reader.ReadInt32();
                if (treeCount > 0 && treeCount < 1000000)
                {
                    trees = new List<TreePlacementData>(treeCount);
                    for (int i = 0; i < treeCount; i++)
                    {
                        trees.Add(new TreePlacementData(
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle()));
                    }
                }

                int houseCount = reader.ReadInt32();
                if (houseCount > 0 && houseCount < 1000000)
                {
                    houses = new List<HousePlacementData>(houseCount);
                    for (int i = 0; i < houseCount; i++)
                    {
                        houses.Add(new HousePlacementData(
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle()));
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // Old format, no trees or houses
            }

            return (grid, trees, houses);
        }
    }
}
