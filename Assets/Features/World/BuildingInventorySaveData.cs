using System.Collections.Generic;

namespace Voxel
{
    /// <summary>Serializable building inventory for world persistence.</summary>
    public struct BuildingInventorySaveData
    {
        public string EntryName;
        public int BlockX;
        public int BlockY;
        public int BlockZ;
        public List<(int ItemId, int Count)> Items;

        public BuildingInventorySaveData(string entryName, int blockX, int blockY, int blockZ, List<(int ItemId, int Count)> items)
        {
            EntryName = entryName ?? "";
            BlockX = blockX;
            BlockY = blockY;
            BlockZ = blockZ;
            Items = items ?? new List<(int, int)>();
        }
    }
}
