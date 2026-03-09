using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Voxel/Item Registry")]
    public class ItemRegistry : ScriptableObject, IItemRegistry
    {
        [SerializeField] private List<ItemDefinition> definitions = new();

        [Tooltip("Category display order in inventory UI. Categories not listed appear last.")]
        [SerializeField] private List<ItemCategory> categoryDisplayOrder = new();

        public IReadOnlyList<string> CategoryDisplayOrder
        {
            get
            {
                if (categoryDisplayOrder == null) return new List<string>();
                var list = new List<string>(categoryDisplayOrder.Count);
                foreach (var cat in categoryDisplayOrder)
                {
                    if (cat != null)
                        list.Add(cat.DisplayName);
                }
                return list;
            }
        }

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

        public bool IsFinal(Item item)
        {
            var def = GetDefinition(item);
            return def != null && def.IsFinal;
        }
    }
}
