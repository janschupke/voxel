using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "PlacedObjectRegistry", menuName = "Voxel/Placed Object Registry")]
    public class PlacedObjectRegistry : ScriptableObject, IPlacedObjectRegistry
    {
        [SerializeField] private List<PlacedObjectEntry> entries = new();

        public IReadOnlyList<PlacedObjectEntry> Entries => entries;

        public PlacedObjectEntry GetByName(string name)
        {
            if (string.IsNullOrEmpty(name) || entries == null) return null;
            foreach (var e in entries)
            {
                if (e != null && e.Name == name)
                    return e;
            }
            return null;
        }

        /// <summary>Returns the first entry with UsesGlobalStorage and InventoryCapacity > 0, or null.</summary>
        public PlacedObjectEntry GetGlobalStorageEntry()
        {
            if (entries == null) return null;
            foreach (var e in entries)
            {
                if (e != null && e.UsesGlobalStorage && e.InventoryCapacity > 0)
                    return e;
            }
            return null;
        }
    }
}
