using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ItemCategory", menuName = "Voxel/Item Category")]
    public class ItemCategory : ScriptableObject
    {
        [Tooltip("Display name in inventory UI (e.g. Building Materials)")]
        [SerializeField] private string displayName = "Other";

        public string DisplayName => string.IsNullOrEmpty(displayName) ? "Other" : displayName;
    }
}
