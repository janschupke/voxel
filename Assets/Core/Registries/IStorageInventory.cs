using System;
using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Global storage inventory. Carriers deposit items here; UI displays counts.
    /// </summary>
    public interface IStorageInventory
    {
        event Action StorageChanged;

        int GetCount(Item item);
        bool HasSpaceFor(Item item, int amount);
        void AddItem(Item item, int amount);
        IEnumerable<(Item Item, int Count)> GetAllItems();
        void Clear();
    }
}
