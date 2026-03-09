using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "PlacedObjectCategory", menuName = "Voxel/Placed Object Category")]
    public class PlacedObjectCategory : ScriptableObject
    {
        [Tooltip("Display name in building menu (e.g. Producers, Other)")]
        [SerializeField] private string displayName = "Other";

        public string DisplayName => string.IsNullOrEmpty(displayName) ? "Other" : displayName;
    }
}
