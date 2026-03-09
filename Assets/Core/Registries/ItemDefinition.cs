using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Voxel/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Item enum id - ties this definition to the Item enum value")]
        public Item ItemId;

        /// <summary>Stable string ID for save/load. Uses enum name; never changes when enum is reordered.</summary>
        public string StableId => ItemId.ToString();

        [Tooltip("Display name (e.g. Wood)")]
        public string Name;

        [Tooltip("Category for inventory UI grouping")]
        [SerializeField] private ItemCategory category;

        /// <summary>Category for UI grouping. Configure in inspector.</summary>
        public ItemCategory Category => category;

        /// <summary>Category display name for UI. Falls back to "Other" if not set.</summary>
        public string CategoryDisplayName => category != null ? category.DisplayName : "Other";

        [Tooltip("2D sprite for UI display")]
        public Sprite Sprite;
    }
}
