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
        private int _perItemCapacity;

        public int PerItemCapacity => _perItemCapacity;

        public void Initialize(int perItemCapacity)
        {
            _perItemCapacity = perItemCapacity > 0 ? perItemCapacity : 0;
        }

        public void AddItem(Item item, int amount)
        {
            if (amount <= 0) return;
            int current = GetCount(item);
            int total = current + amount;
            int allowed = total < _perItemCapacity ? total : _perItemCapacity;
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

        public bool HasSpaceFor(Item item, int amount)
        {
            return GetCount(item) + amount <= _perItemCapacity;
        }

        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>Load items from persistence. Clears existing and adds each pair, respecting capacity. No event emission.</summary>
        public void LoadFrom(IEnumerable<(Item Item, int Count)> items)
        {
            _items.Clear();
            if (items == null) return;
            foreach (var (item, count) in items)
            {
                if (count <= 0) continue;
                AddItem(item, count);
            }
        }

        /// <summary>Removes up to amount of item. Returns actual count removed.</summary>
        public int RemoveItem(Item item, int amount)
        {
            if (amount <= 0) return 0;
            int current = GetCount(item);
            if (current <= 0) return 0;
            int toRemove = amount < current ? amount : current;
            _items[item] = current - toRemove;
            if (_items[item] <= 0)
                _items.Remove(item);
            return toRemove;
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
