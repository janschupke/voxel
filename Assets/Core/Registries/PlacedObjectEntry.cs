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

        [Tooltip("Category for building menu grouping (e.g. Producers, Other)")]
        [SerializeField] private PlacedObjectCategory category;

        /// <summary>Category for UI grouping. Configure in inspector.</summary>
        public PlacedObjectCategory Category => category;

        /// <summary>Category display name for UI. Falls back to "Other" if not set.</summary>
        public string CategoryDisplayName => category != null ? category.DisplayName : "Other";

        [Tooltip("Prefab to instantiate when placing")]
        public GameObject Prefab;

        [Tooltip("Optional sprite for UI (building menu, selection detail). When unset, a fallback may be used.")]
        [SerializeField] private Sprite sprite;

        /// <summary>Sprite for UI display (placement buttons, selection detail). Null when not configured.</summary>
        public Sprite Sprite => sprite;

        [Tooltip("When placing, remove trees at the target block(s). Deprecated: use StructureType.")]
        public bool CanReplaceTrees;

        [Tooltip("Blocks placement of other objects at the same block (e.g. buildings). Deprecated: use StructureType.")]
        public bool IsBlocking = true;

        [Tooltip("Classification for placement and pathing rules.")]
        public StructureType StructureType = StructureType.Building;

        [Tooltip("When true, no 3D prefab is instantiated; placement adds to a data structure (e.g. RoadOverlay) instead.")]
        public bool IsSurfaceOverlay;

        [Tooltip("Placement mode: Single (click), Area (drag rectangle), Line (drag path).")]
        public PlacementMode PlacementMode = PlacementMode.Single;
        [Tooltip("When Line placement: allow path through existing roads; skip preview and placement on those blocks (extend/repair).")]
        public bool LinePlacementExtendThroughExisting;

        [Header("Footprint")]
        [Min(1)] [Tooltip("Ground footprint in blocks (x). For Single placement, cursor snaps to center.")]
        public int AreaSizeX = 1;

        [Min(1)] [Tooltip("Ground footprint in blocks (z). For Single placement, cursor snaps to center.")]
        public int AreaSizeZ = 1;

        [Min(0.01f)] [Tooltip("Height in world block units. Model scale is derived from area and height.")]
        public float HeightInBlocks = 1f;

        /// <summary>Returns area dimensions, swapping x/z when rotation is 90° or 270°.</summary>
        public (int sizeX, int sizeZ) GetEffectiveArea(float rotationY)
        {
            var normalized = ((int)Mathf.Repeat(rotationY, 360f) / 90) % 2;
            return normalized == 0 ? (AreaSizeX, AreaSizeZ) : (AreaSizeZ, AreaSizeX);
        }

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

        private const float OperationalRangeEpsilon = 0.001f;

        public bool HasOperationalRange => OperationalRangeInBlocks > OperationalRangeEpsilon;

        /// <summary>Range as integer cell count for use with OperationalRange utility.</summary>
        public int OperationalRangeCells => OperationalRangeInBlocks > OperationalRangeEpsilon ? (int)(OperationalRangeInBlocks + 0.5f) : 0;

        [Header("Actor")]
        [Tooltip("When set, an actor of this type operates within the building's operational range.")]
        public ActorDefinition AssignedActor;

        [Header("Inventory")]
        [Tooltip("Max items this building can hold. 0 = no inventory.")]
        [Min(0)]
        public int InventoryCapacity;
        [Tooltip("When true, this building uses global storage instead of per-building inventory. No BuildingInventory is added.")]
        public bool UsesGlobalStorage;

        [Header("Actor Targeting")]
        [Tooltip("When true, gatherers can target this object type (e.g. Tree, Wheat).")]
        public bool IsGatherSite;
        [Tooltip("When true, collectors consider this building. Default: all buildings with inventory.")]
        public bool CollectorTarget = true;

        [Header("Critter Spawning")]
        [Tooltip("When set, a CritterSpawner is added when placing this object.")]
        public CritterSpawnerConfig CritterSpawnerConfig;

        [Header("Production")]
        [Tooltip("When set, BuildingProduction is added and runs recipes when inputs and output space available.")]
        public ProductionTreeConfig ProductionTree;

        /// <summary>Blocks placement of other objects at the same block. Building and Road block; Environment does not.</summary>
        public bool BlocksPlacement => StructureType == StructureType.Building || StructureType == StructureType.Road;

        /// <summary>Blocks actor pathing. Only buildings block; Road and Environment are walkable.</summary>
        public bool BlocksPathing => StructureType == StructureType.Building;

        /// <summary>When placing, can replace environment (Tree, Wheat, etc.) at the target block(s). Buildings, roads, and environment can replace.</summary>
        public bool CanReplaceEnvironment => StructureType == StructureType.Building || StructureType == StructureType.Road || StructureType == StructureType.Environment;
    }
}
