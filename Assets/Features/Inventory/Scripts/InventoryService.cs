using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Pure domain logic for inventory: add/remove items, capacity checks.
    /// No Unity dependencies. Used by BuildingInventory MonoBehaviour.
    /// </summary>
    public class InventoryService
    {
        private readonly Dictionary<Item, int> _items = new Dictionary<Item, int>();
        private int _maxCapacity;

        public int MaxCapacity => _maxCapacity;

        public void Initialize(int capacity)
        {
            _maxCapacity = capacity > 0 ? capacity : 0;
        }

        public void AddItem(Item item, int amount)
        {
            if (amount <= 0) return;
            int current = GetCount(item);
            int total = current + amount;
            int allowed = total < _maxCapacity ? total : _maxCapacity;
            int toAdd = allowed - current;
            if (toAdd <= 0) return;

            _items[item] = current + toAdd;
        }

        public int GetCount(Item item)
        {
            return _items.TryGetValue(item, out int count) ? count : 0;
        }

        public int GetTotalCount()
        {
            int total = 0;
            foreach (var count in _items.Values)
                total += count;
            return total;
        }

        public bool HasSpaceFor(int additional)
        {
            return GetTotalCount() + additional <= _maxCapacity;
        }

        public IEnumerable<(Item Item, int Count)> GetAllItems()
        {
            foreach (var kv in _items)
            {
                if (kv.Value > 0)
                    yield return (kv.Key, kv.Value);
            }
        }
    }
}
