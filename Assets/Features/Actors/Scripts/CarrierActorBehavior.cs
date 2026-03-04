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
        private BuildingInventory _cachedInventory;
        private Transform _sourceBuilding;
        private Item? _carriedItem;

        private BuildingInventory GetHomeInventory()
        {
            if (_cachedInventory == null && HomeBuilding != null)
                _cachedInventory = HomeBuilding.GetComponent<BuildingInventory>();
            return _cachedInventory;
        }

        protected override bool SkipWorkOutside => true;

        protected override void OnArrivedAtTarget()
        {
            TakeFromSource();
        }

        private void TakeFromSource()
        {
            _carriedItem = null;
            if (_sourceBuilding == null) return;

            var inv = _sourceBuilding.GetComponent<BuildingInventory>();
            if (inv == null || inv.GetTotalCount() <= 0) return;

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

            var registry = WorldBootstrap?.PlacedObjectRegistry;
            if (registry == null) return (null, false);

            var homeEntryName = WorldBootstrap.GetEntryNameForTransform(HomeBuilding);
            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            var candidates = new List<Transform>();
            foreach (var entry in registry.Entries)
            {
                if (entry == null || entry.InventoryCapacity <= 0) continue;
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
                        candidates.Add(building);
                }
            }

            if (candidates.Count == 0)
            {
                GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: no buildings with items in range");
                return (null, false);
            }

            Shuffle(candidates);

            foreach (var building in candidates)
            {
                var path = BuildPathTo(HomeBuilding.position, building.position);
                if (path != null && path.Count > 0)
                {
                    _sourceBuilding = building;
                    GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: found path to {building.name} (path len={path.Count})");
                    return (building.position, true);
                }
            }

            GameDebugLogger.Log($"[Carrier] {gameObject.name} TryGetTarget: {candidates.Count} buildings in range but no valid path");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var inventory = GetHomeInventory();
            if (inventory != null && _carriedItem.HasValue && inventory.HasSpaceFor(1))
            {
                inventory.AddItem(_carriedItem.Value, 1, emitUnitProduced: false);
            }
            _carriedItem = null;
        }

        protected override bool IsBuildingInventoryFull()
        {
            var inventory = GetHomeInventory();
            return inventory != null && !inventory.HasSpaceFor(1);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
