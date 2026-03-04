using UnityEngine;
using Voxel.Pure;

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
        [Tooltip("When Line placement: allow path through existing roads; skip preview and placement on those blocks (extend/repair).")]
        public bool LinePlacementExtendThroughExisting;

        [Min(0.01f)] [Tooltip("Prefab height in its local units (used to scale to 1 block)")]
        public float PrefabHeightInUnits = 2f;

        [Min(0.01f)] [Tooltip("Scale multiplier")]
        public float ScaleMultiplier = 1f;

        [Tooltip("Apply random Y rotation for variety (e.g. for trees)")]
        public bool RandomRotation;

        [Tooltip("Can be clicked to select and show in SelectionDetail UI")]
        public bool IsSelectable;

        [Header("Operational Range")]
        [Tooltip("Range in grid cells. 0 = no range. When set, shows outline on terrain when selected.")]
        [Min(0f)]
        public float OperationalRangeInBlocks;
        [Tooltip("Square = Chebyshev (axis-aligned). GridCircle = Euclidean (cell centers).")]
        public OperationalRangeType OperationalRangeType = OperationalRangeType.Square;

        public bool HasOperationalRange => OperationalRangeInBlocks > 0.001f;

        /// <summary>Range as integer cell count for use with OperationalRange utility.</summary>
        public int OperationalRangeCells => OperationalRangeInBlocks > 0.001f ? (int)(OperationalRangeInBlocks + 0.5f) : 0;

        [Header("Actor")]
        [Tooltip("When set, an actor of this type operates within the building's operational range.")]
        public ActorDefinition AssignedActor;

        [Header("Inventory")]
        [Tooltip("Max items this building can hold. 0 = no inventory.")]
        [Min(0)]
        public int InventoryCapacity;
        [Tooltip("When true, this building uses global storage instead of per-building inventory. No BuildingInventory is added.")]
        public bool UsesGlobalStorage;
    }
}
