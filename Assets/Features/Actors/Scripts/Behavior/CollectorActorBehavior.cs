using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Collector actor: finds buildings in range with non-final items, picks up (configurable capacity),
    /// returns home, deposits into own building's inventory for further processing.
    /// </summary>
    public class CollectorActorBehavior : ActorBehavior
    {
        private IBuildingInventory _cachedSourceInventory;
        private IBuildingInventory _cachedHomeInventory;
        private Transform _sourceBuilding;
        private Item? _carriedItem;
        private int _carriedCount;
        private const int CandidatesBufferCapacity = 32;
        private readonly List<Transform> _candidatesBuffer = new List<Transform>(CandidatesBufferCapacity);
        private readonly HashSet<Item> _neededItemsBuffer = new HashSet<Item>();

        public Item? CarriedItem => _carriedItem;
        public int CarriedCount => _carriedCount;

        public void SetCarriedItem(Item? item, int count = 1)
        {
            _carriedItem = item;
            _carriedCount = count > 0 ? count : 0;
        }

        private CollectorConfig Config => Definition?.CategoryConfig as CollectorConfig;
        private int CapacityPerTrip => Config?.CapacityPerTrip ?? 1;

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
            if (config == null || itemRegistry == null || !config.OnlyNonFinalItems) return;

            var homeInv = GetHomeInventory();
            var homeProd = HomeBuilding?.GetComponent<BuildingProduction>();
            if (homeProd != null)
                homeProd.GetNeededInputItemsWithZero(_neededItemsBuffer);
            else
                _neededItemsBuffer.Clear();

            var capacity = CapacityPerTrip;
            foreach (var (item, count) in inv.GetAllItems())
            {
                if (count <= 0) continue;
                if (itemRegistry.IsFinal(item)) continue;
                if (_neededItemsBuffer.Count > 0 && !_neededItemsBuffer.Contains(item)) continue;
                if (homeInv == null || !homeInv.HasSpaceFor(item, 1)) continue;

                var take = Mathf.Min(count, capacity);
                var taken = inv.TryTake(item, take);
                if (taken.HasValue)
                {
                    _carriedItem = taken.Value.Item;
                    _carriedCount = taken.Value.Count;
                    GameDebugLogger.Log($"[Collector] {gameObject.name} picked up {_carriedCount} {_carriedItem} from {_sourceBuilding.name}");
                    break;
                }
            }
        }

        private IBuildingInventory GetHomeInventory()
        {
            if (_cachedHomeInventory == null && HomeBuilding != null)
                _cachedHomeInventory = HomeBuilding.GetComponent<BuildingInventory>();
            return _cachedHomeInventory;
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            _sourceBuilding = null;
            _cachedSourceInventory = null;

            var registry = WorldBootstrap?.PlacedObjectRegistry;
            var itemRegistry = WorldBootstrap?.ItemRegistry;
            if (registry == null || itemRegistry == null) return (null, false);

            var config = Config;
            if (config == null || !config.OnlyNonFinalItems) return (null, false);

            var homeInv = GetHomeInventory();
            if (homeInv == null) return (null, false);

            var homeProd = HomeBuilding?.GetComponent<BuildingProduction>();
            if (homeProd != null)
            {
                homeProd.GetNeededInputItemsWithZero(_neededItemsBuffer);
                if (_neededItemsBuffer.Count == 0)
                    return (null, false);

                bool hasSpaceForAnyNeeded = false;
                foreach (var item in _neededItemsBuffer)
                {
                    if (homeInv.HasSpaceFor(item, 1))
                    {
                        hasSpaceForAnyNeeded = true;
                        break;
                    }
                }
                if (!hasSpaceForAnyNeeded)
                    return (null, false);
            }
            else
            {
                _neededItemsBuffer.Clear();
            }

            var homeEntryName = WorldBootstrap.GetEntryNameForTransform(HomeBuilding);
            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            _candidatesBuffer.Clear();
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

                    bool hasNeededItem = false;
                    foreach (var (item, count) in inv.GetAllItems())
                    {
                        if (count <= 0 || itemRegistry.IsFinal(item)) continue;
                        if (_neededItemsBuffer.Count == 0 || _neededItemsBuffer.Contains(item))
                        {
                            if (homeInv.HasSpaceFor(item, 1))
                            {
                                hasNeededItem = true;
                                break;
                            }
                        }
                    }
                    if (!hasNeededItem) continue;

                    var (bx, _, bz) = worldScale.WorldToBlock(building.position);
                    if (OperationalRange.IsCellInRange(hx, hz, bx, bz, RangeCells, RangeType))
                        _candidatesBuffer.Add(building);
                }
            }

            if (_candidatesBuffer.Count == 0)
            {
                GameDebugLogger.Log($"[Collector] {gameObject.name} TryGetTarget: no buildings with needed items in range");
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
                    GameDebugLogger.Log($"[Collector] {gameObject.name} TryGetTarget: found path to {building.name} (path len={path.Count})");
                    return (building.position, true);
                }
            }

            GameDebugLogger.Log($"[Collector] {gameObject.name} TryGetTarget: {_candidatesBuffer.Count} buildings in range but no valid path");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var inventory = GetHomeInventory();
            if (inventory != null && _carriedItem.HasValue && _carriedCount > 0 && inventory.HasSpaceFor(_carriedItem.Value, _carriedCount))
            {
                inventory.AddItem(_carriedItem.Value, _carriedCount, emitUnitProduced: false);
            }
            _carriedItem = null;
            _carriedCount = 0;
        }

        protected override bool IsBuildingInventoryFull()
        {
            var inventory = GetHomeInventory();
            return inventory != null && _carriedItem.HasValue && _carriedCount > 0 && !inventory.HasSpaceFor(_carriedItem.Value, _carriedCount);
        }
    }
}
