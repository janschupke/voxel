using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Pure domain logic for global storage: per-item capacity (same cap for all item types).
    /// No Unity dependencies. Used by StorageInventory MonoBehaviour.
    /// </summary>
    public class StorageService
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

        public bool HasSpaceFor(Item item, int amount)
        {
            return GetCount(item) + amount <= _perItemCapacity;
        }

        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>Load items from persistence. Clears existing and adds each pair, respecting per-item capacity.</summary>
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
