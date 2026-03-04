using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Carrier actor: finds buildings in range with inventory items, walks to them, picks up 1 unit,
    /// returns to warehouse, deposits. No work outside; warehouse deposits do not emit floating text.
    /// </summary>
    public class CarrierActorBehavior : ActorBehavior
    {
        private IBuildingInventory _cachedSourceInventory;
        private Transform _sourceBuilding;
        private Item? _carriedItem;
        private readonly List<Transform> _candidatesBuffer = new List<Transform>(32);

        public Item? CarriedItem => _carriedItem;

        public void SetCarriedItem(Item? item)
        {
            _carriedItem = item;
        }

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
            if (_sourceBuilding == null || _cachedSourceInventory == null) return;

            var inv = _cachedSourceInventory;
            if (inv.GetTotalCount() <= 0) return;

            foreach (var (item, count) in inv.GetAllItems())
            {
                if (count <= 0) continue;
                var taken = inv.TryTakeOne(item);
                if (taken.HasValue)
                {
                    _carriedItem = taken.Value.Item;
                    GameDebugLogger.Log($"[Carrier] {gameObject.name} picked up 1 {_carriedItem} from {_sourceBuilding.name}");
                    break;
                }
            }
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            _sourceBuilding = null;
            _cachedSourceInventory = null;

            var registry = WorldBootstrap?.PlacedObjectRegistry;
            if (registry == null) return (null, false);

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

                    var (bx, _, bz) = worldScale.WorldToBlock(building.position);
                    if (OperationalRange.IsCellInRange(hx, hz, bx, bz, RangeCells, RangeType))
                        _candidatesBuffer.Add(building);
                }
            }

            if (_candidatesBuffer.Count == 0)
            {
                GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: no buildings with items in range");
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
            if (storage != null && _carriedItem.HasValue && storage.HasSpaceFor(_carriedItem.Value, 1))
            {
                storage.AddItem(_carriedItem.Value, 1);
            }
            _carriedItem = null;
        }

        protected override bool IsBuildingInventoryFull()
        {
            var storage = GetStorageInventory();
            return storage != null && _carriedItem.HasValue && !storage.HasSpaceFor(_carriedItem.Value, 1);
        }

    }
}
