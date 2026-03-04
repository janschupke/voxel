using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Per-building inventory. Attached at runtime when placing or loading buildings with InventoryCapacity > 0.
    /// Wires Unity to InventoryService (domain logic).
    /// </summary>
    public class BuildingInventory : MonoBehaviour
    {
        private readonly InventoryService _service = new InventoryService();

        public int MaxCapacity => _service.MaxCapacity;

        public void Initialize(string entryName, int capacity)
        {
            _service.Initialize(Mathf.Max(0, capacity));
        }

        public void AddItem(Item item, int amount, bool emitUnitProduced = true)
        {
            var before = GetTotalCount();
            _service.AddItem(item, amount);
            var after = GetTotalCount();
            var added = after - before;
            if (added > 0 && emitUnitProduced)
                WorldObjectEventBus.Raise(new WorldObjectEvent(transform, WorldObjectEventTypes.UnitProduced, (item, added)));
        }

        public void ClearInventory()
        {
            _service.Clear();
        }

        /// <summary>Load items from persistence. No WorldObjectEventBus emission.</summary>
        public void LoadFrom(IEnumerable<(Item Item, int Count)> items)
        {
            _service.LoadFrom(items);
        }

        public int RemoveItem(Item item, int amount)
        {
            return _service.RemoveItem(item, amount);
        }

        /// <summary>Tries to remove 1 of the specified item. Returns (item, 1) if successful, null otherwise.</summary>
        public (Item Item, int Count)? TryTakeOne(Item item)
        {
            var removed = _service.RemoveItem(item, 1);
            return removed > 0 ? (item, removed) : null;
        }

        public int GetCount(Item item) => _service.GetCount(item);
        public int GetTotalCount() => _service.GetTotalCount();
        public bool HasSpaceFor(int additional) => _service.HasSpaceFor(additional);
        public IEnumerable<(Item Item, int Count)> GetAllItems() => _service.GetAllItems();
    }
}
