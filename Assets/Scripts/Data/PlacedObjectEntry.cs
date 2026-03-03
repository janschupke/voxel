using UnityEngine;

namespace Voxel
{
    public enum PlacementMode
    {
        Single,
        Area,
        Line
    }

    [CreateAssetMenu(fileName = "PlacedObject", menuName = "Voxel/Placed Object Entry")]
    public class PlacedObjectEntry : ScriptableObject
    {
        [Tooltip("UI label (e.g. House, Tree)")]
        public string Name;

        [Tooltip("Prefab to instantiate when placing")]
        public GameObject Prefab;

        [Tooltip("When placing, remove trees at the target block(s)")]
        public bool CanReplaceTrees;

        [Tooltip("Blocks placement of other objects at the same block (e.g. buildings). False for vegetation like trees.")]
        public bool IsBlocking = true;

        [Tooltip("When true, no 3D prefab is instantiated; placement adds to a data structure (e.g. RoadOverlay) instead.")]
        public bool IsSurfaceOverlay;

        [Tooltip("Placement mode: Single (click), Area (drag rectangle), Line (drag path).")]
        public PlacementMode PlacementMode = PlacementMode.Single;

        [Min(0.01f)] [Tooltip("Prefab height in its local units (used to scale to 1 block)")]
        public float PrefabHeightInUnits = 2f;

        [Min(0.01f)] [Tooltip("Scale multiplier")]
        public float ScaleMultiplier = 1f;

        [Tooltip("Apply random Y rotation for variety (e.g. for trees)")]
        public bool RandomRotation;

        [Tooltip("Can be clicked to select and show in SelectionDetail UI")]
        public bool IsSelectable;

        [Header("Operational Range")]
        [Tooltip("Range in blocks. 0 = no range. When set, shows outline on terrain when selected.")]
        [Min(0f)]
        public float OperationalRangeInBlocks;

        public bool HasOperationalRange => OperationalRangeInBlocks > 0.001f;
    }
}
