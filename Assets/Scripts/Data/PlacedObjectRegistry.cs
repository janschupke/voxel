using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "PlacedObjectRegistry", menuName = "Voxel/Placed Object Registry")]
    public class PlacedObjectRegistry : ScriptableObject
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
    }
}
