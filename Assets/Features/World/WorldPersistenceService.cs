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
        private const int SaveVersion = 4;
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

        public static void Save(VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects = null,
            IReadOnlyList<BuildingInventorySaveData> buildingInventories = null,
            IReadOnlyList<ActorSaveData> actorData = null,
            IReadOnlyList<(int ItemId, int Count)> globalStorageItems = null)
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

            int invCount = buildingInventories?.Count ?? 0;
            writer.Write(invCount);
            if (invCount > 0)
            {
                for (int i = 0; i < invCount; i++)
                {
                    var inv = buildingInventories[i];
                    writer.Write(inv.EntryName ?? "");
                    writer.Write(inv.BlockX);
                    writer.Write(inv.BlockY);
                    writer.Write(inv.BlockZ);
                    int itemCount = inv.Items?.Count ?? 0;
                    writer.Write(itemCount);
                    if (inv.Items != null)
                    {
                        for (int j = 0; j < itemCount; j++)
                        {
                            var (itemId, amt) = inv.Items[j];
                            writer.Write(itemId);
                            writer.Write(amt);
                        }
                    }
                }
            }

            int actorCount = actorData?.Count ?? 0;
            writer.Write(actorCount);
            if (actorCount > 0)
            {
                for (int i = 0; i < actorCount; i++)
                {
                    var a = actorData[i];
                    writer.Write(a.ActorTypeName ?? "");
                    writer.Write(a.HomeEntryName ?? "");
                    writer.Write(a.HomeBlockX);
                    writer.Write(a.HomeBlockY);
                    writer.Write(a.HomeBlockZ);
                    writer.Write(a.PosX);
                    writer.Write(a.PosY);
                    writer.Write(a.PosZ);
                    writer.Write(a.StateId);
                    writer.Write(a.CarriedItemId);
                }
            }

            int globalCount = globalStorageItems?.Count ?? 0;
            writer.Write(globalCount);
            if (globalCount > 0 && globalStorageItems != null)
            {
                for (int i = 0; i < globalCount; i++)
                {
                    var (itemId, amt) = globalStorageItems[i];
                    writer.Write(itemId);
                    writer.Write(amt);
                }
            }
        }

        public static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects,
            IReadOnlyList<BuildingInventorySaveData> buildingInventories,
            IReadOnlyList<ActorSaveData> actorData,
            IReadOnlyList<(int ItemId, int Count)> globalStorageItems) Load()
        {
            if (!WorldExists())
                throw new FileNotFoundException("No saved world found", WorldPath);

            using var file = File.OpenRead(WorldPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);

            using var reader = new BinaryReader(gzip);
            int version = reader.ReadInt32();

            if (version >= 4)
                return LoadV4(reader);
            if (version == 3)
            {
                var (g, p, b, a) = LoadV3(reader);
                return (g, p, b, a, new List<(int, int)>());
            }
            if (version < 100)
            {
                var (g, p) = LoadV2Core(reader);
                return (g, p, new List<BuildingInventorySaveData>(), new List<ActorSaveData>(), new List<(int, int)>());
            }

            var (grid, placedObjects) = LoadV1(reader, version);
            return (grid, placedObjects, new List<BuildingInventorySaveData>(), new List<ActorSaveData>(), new List<(int, int)>());
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects,
            IReadOnlyList<BuildingInventorySaveData> buildingInventories,
            IReadOnlyList<ActorSaveData> actorData,
            IReadOnlyList<(int ItemId, int Count)> globalStorageItems) LoadV4(BinaryReader reader)
        {
            var (grid, placedObjects, buildingInventories, actorData) = LoadV3(reader);
            var globalStorageItems = new List<(int ItemId, int Count)>();
            try
            {
                int globalCount = reader.ReadInt32();
                if (globalCount > 0 && globalCount < 100000)
                {
                    for (int i = 0; i < globalCount; i++)
                    {
                        globalStorageItems.Add((reader.ReadInt32(), reader.ReadInt32()));
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }
            return (grid, placedObjects, buildingInventories, actorData, globalStorageItems);
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects,
            IReadOnlyList<BuildingInventorySaveData> buildingInventories,
            IReadOnlyList<ActorSaveData> actorData) LoadV3(BinaryReader reader)
        {
            var (grid, placedObjects) = LoadV2Core(reader);

            var buildingInventories = new List<BuildingInventorySaveData>();
            var actorData = new List<ActorSaveData>();

            try
            {
                int invCount = reader.ReadInt32();
                if (invCount > 0 && invCount < 100000)
                {
                    for (int i = 0; i < invCount; i++)
                    {
                        var entryName = reader.ReadString();
                        var bx = reader.ReadInt32();
                        var by = reader.ReadInt32();
                        var bz = reader.ReadInt32();
                        int itemCount = reader.ReadInt32();
                        var items = new List<(int ItemId, int Count)>();
                        for (int j = 0; j < itemCount && j < 10000; j++)
                        {
                            items.Add((reader.ReadInt32(), reader.ReadInt32()));
                        }
                        buildingInventories.Add(new BuildingInventorySaveData(entryName, bx, by, bz, items));
                    }
                }

                int actorCount = reader.ReadInt32();
                if (actorCount > 0 && actorCount < 100000)
                {
                    for (int i = 0; i < actorCount; i++)
                    {
                        actorData.Add(new ActorSaveData(
                            reader.ReadString(),
                            reader.ReadString(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadInt32(),
                            reader.ReadInt32()));
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }

            return (grid, placedObjects, buildingInventories, actorData);
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects,
            IReadOnlyList<BuildingInventorySaveData> buildingInventories,
            IReadOnlyList<ActorSaveData> actorData) LoadV2(BinaryReader reader)
        {
            var (grid, placedObjects) = LoadV2Core(reader);
            return (grid, placedObjects, new List<BuildingInventorySaveData>(), new List<ActorSaveData>());
        }

        private static (VoxelGrid grid, IReadOnlyList<PlacedObjectData> placedObjects) LoadV2Core(BinaryReader reader)
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
