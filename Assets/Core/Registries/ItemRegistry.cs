using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Voxel/Item Registry")]
    public class ItemRegistry : ScriptableObject, IItemRegistry
    {
        [SerializeField] private List<ItemDefinition> definitions = new();

        public ItemDefinition GetDefinition(Item item)
        {
            if (definitions == null) return null;
            foreach (var def in definitions)
            {
                if (def != null && def.ItemId == item)
                    return def;
            }
            return null;
        }

        public bool TryGetByStableId(string stableId, out Item item)
        {
            item = default;
            if (string.IsNullOrEmpty(stableId) || definitions == null) return false;
            foreach (var def in definitions)
            {
                if (def != null && def.StableId == stableId)
                {
                    item = def.ItemId;
                    return true;
                }
            }
            return false;
        }

        public string GetStableId(Item item)
        {
            var def = GetDefinition(item);
            return def != null ? def.StableId : item.ToString();
        }
    }
}
