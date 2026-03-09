using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Carrier actor: finds buildings in range with inventory items, picks up (prioritizes final items),
    /// returns to warehouse, deposits to global storage. No work outside.
    /// </summary>
    public class CarrierActorBehavior : ActorBehavior
    {
        private IBuildingInventory _cachedSourceInventory;
        private Transform _sourceBuilding;
        private Item? _carriedItem;
        private int _carriedCount;
        private const int CandidatesBufferCapacity = 32;
        private readonly List<Transform> _candidatesBuffer = new List<Transform>(CandidatesBufferCapacity);

        public Item? CarriedItem => _carriedItem;
        public int CarriedCount => _carriedCount;

        public void SetCarriedItem(Item? item, int count = 1)
        {
            _carriedItem = item;
            _carriedCount = count > 0 ? count : 0;
        }

        private CarrierConfig Config => Definition?.CategoryConfig as CarrierConfig;
        private int CapacityPerTrip => Config?.CapacityPerTrip ?? 1;

        private IStorageInventory GetStorageInventory()
        {
            return WorldBootstrap?.StorageInventory;
        }

        protected override bool SkipWorkOutside => true;

        protected override void OnArrivedAtTarget()
        {
            TakeFromSource();
        }

        private void TakeFromSource()
        {
            _carriedItem = null;
            _carriedCount = 0;
            if (_sourceBuilding == null || _cachedSourceInventory == null) return;

            var inv = _cachedSourceInventory;
            if (inv.GetTotalCount() <= 0) return;

            var config = Config;
            var itemRegistry = WorldBootstrap?.ItemRegistry;
            var capacity = CapacityPerTrip;

            var (pickItem, pickCount) = PickItemFromBuilding(inv, config, itemRegistry, capacity);
            if (pickItem.HasValue && pickCount > 0)
            {
                var taken = inv.TryTake(pickItem.Value, pickCount);
                if (taken.HasValue)
                {
                    _carriedItem = taken.Value.Item;
                    _carriedCount = taken.Value.Count;
                    GameDebugLogger.Log($"[Carrier] {gameObject.name} picked up {_carriedCount} {_carriedItem} from {_sourceBuilding.name}");
                }
            }
        }

        private (Item? item, int count) PickItemFromBuilding(IBuildingInventory inv, CarrierConfig config, IItemRegistry itemRegistry, int capacity)
        {
            if (config == null || itemRegistry == null) return (null, 0);

            Item? pickItem = null;
            int pickCount = 0;

            foreach (var (item, count) in inv.GetAllItems())
            {
                if (count <= 0) continue;
                var take = Mathf.Min(count, capacity);
                if (itemRegistry.IsFinal(item))
                {
                    pickItem = item;
                    pickCount = take;
                    break;
                }
                var itemAtCapacity = !inv.HasSpaceFor(item, 1);
                if (pickItem == null && (config?.TakeNonFinalWhenBuildingFull ?? true) && itemAtCapacity)
                {
                    pickItem = item;
                    pickCount = take;
                    break;
                }
            }
            return (pickItem, pickCount);
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            _sourceBuilding = null;
            _cachedSourceInventory = null;

            var registry = WorldBootstrap?.PlacedObjectRegistry;
            var itemRegistry = WorldBootstrap?.ItemRegistry;
            if (registry == null || itemRegistry == null) return (null, false);

            var config = Config;
            var homeEntryName = WorldBootstrap.GetEntryNameForTransform(HomeBuilding);
            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            var finalBuildings = new List<Transform>();
            var overflowBuildings = new List<Transform>();

            foreach (var entry in registry.Entries)
            {
                if (entry == null || entry.InventoryCapacity <= 0 || entry.UsesGlobalStorage) continue;
                if (entry.Name == homeEntryName) continue;

                var parent = WorldBootstrap.GetParentByEntryName(entry.Name);
                if (parent == null) continue;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var building = parent.GetChild(i);
                    if (building == HomeBuilding) continue;

                    var inv = building.GetComponent<BuildingInventory>();
                    if (inv == null || inv.GetTotalCount() <= 0) continue;

                    var (bx, _, bz) = worldScale.WorldToBlock(building.position);
                    if (!OperationalRange.IsCellInRange(hx, hz, bx, bz, RangeCells, RangeType)) continue;

                    bool hasFinal = false;
                    bool hasNonFinalFull = false;
                    var takeNonFinalWhenFull = config?.TakeNonFinalWhenBuildingFull ?? true;
                    foreach (var (item, count) in inv.GetAllItems())
                    {
                        if (count <= 0) continue;
                        if (itemRegistry.IsFinal(item)) hasFinal = true;
                        else if (takeNonFinalWhenFull && !inv.HasSpaceFor(item, 1))
                            hasNonFinalFull = true;
                    }
                    if (hasFinal) finalBuildings.Add(building);
                    else if (hasNonFinalFull) overflowBuildings.Add(building);
                }
            }

            _candidatesBuffer.Clear();
            if (finalBuildings.Count > 0) _candidatesBuffer.AddRange(finalBuildings);
            else if (overflowBuildings.Count > 0) _candidatesBuffer.AddRange(overflowBuildings);

            if (_candidatesBuffer.Count == 0)
            {
                GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: no buildings with pickable items in range");
                return (null, false);
            }

            _candidatesBuffer.Shuffle();

            foreach (var building in _candidatesBuffer)
            {
                var path = BuildPathTo(HomeBuilding.position, building.position, fromIsBuilding: true, toIsBuilding: false);
                if (path != null && path.Count > 0)
                {
                    _sourceBuilding = building;
                    _cachedSourceInventory = building.GetComponent<BuildingInventory>();
                    GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: found path to {building.name} (path len={path.Count})");
                    return (building.position, true);
                }
            }

            GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: {_candidatesBuffer.Count} buildings in range but no valid path");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var storage = GetStorageInventory();
            if (storage != null && _carriedItem.HasValue && _carriedCount > 0 && storage.HasSpaceFor(_carriedItem.Value, _carriedCount))
            {
                storage.AddItem(_carriedItem.Value, _carriedCount);
            }
            _carriedItem = null;
            _carriedCount = 0;
        }

        protected override bool IsBuildingInventoryFull()
        {
            var storage = GetStorageInventory();
            return storage != null && _carriedItem.HasValue && _carriedCount > 0 && !storage.HasSpaceFor(_carriedItem.Value, _carriedCount);
        }
    }
}
