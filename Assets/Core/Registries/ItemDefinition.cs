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

        [Tooltip("2D sprite for UI display")]
        public Sprite Sprite;
    }
}
