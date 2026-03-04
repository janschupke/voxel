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

        public void AddItem(Item item, int amount) => _service.AddItem(item, amount);
        public int GetCount(Item item) => _service.GetCount(item);
        public int GetTotalCount() => _service.GetTotalCount();
        public bool HasSpaceFor(int additional) => _service.HasSpaceFor(additional);
        public IEnumerable<(Item Item, int Count)> GetAllItems() => _service.GetAllItems();
    }
}
