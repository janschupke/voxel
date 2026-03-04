using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// MonoBehaviour wrapper for global storage. Wires Unity to StorageService (domain logic).
    /// Created and held by WorldBootstrap.
    /// </summary>
    public class StorageInventory : MonoBehaviour, IStorageInventory
    {
        private readonly StorageService _service = new StorageService();

        public void Initialize(int perItemCapacity)
        {
            _service.Initialize(perItemCapacity);
        }

        public void AddItem(Item item, int amount)
        {
            _service.AddItem(item, amount);
        }

        public void Clear()
        {
            _service.Clear();
        }

        /// <summary>Load items from persistence. No event emission.</summary>
        public void LoadFrom(IEnumerable<(Item Item, int Count)> items)
        {
            _service.LoadFrom(items);
        }

        public int GetCount(Item item) => _service.GetCount(item);
        public bool HasSpaceFor(Item item, int amount) => _service.HasSpaceFor(item, amount);
        public IEnumerable<(Item Item, int Count)> GetAllItems() => _service.GetAllItems();
    }
}
