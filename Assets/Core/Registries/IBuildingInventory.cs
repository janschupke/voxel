using System;
using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Abstraction for building inventory. Domain/actor code depends on this, not MonoBehaviour.
    /// </summary>
    public interface IBuildingInventory
    {
        event Action InventoryChanged;

        int MaxCapacity { get; }
        int GetCount(Item item);
        int GetTotalCount();
        bool HasSpaceFor(int additional);
        IEnumerable<(Item Item, int Count)> GetAllItems();
        (Item Item, int Count)? TryTakeOne(Item item);
        /// <summary>Tries to remove up to amount of the specified item. Returns (item, actualCount) if successful.</summary>
        (Item Item, int Count)? TryTake(Item item, int amount);
        void AddItem(Item item, int amount, bool emitUnitProduced = true);
        void ClearInventory();
    }
}
