using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Config-driven gatherer: selects closest target in range, paths to it, works,
    /// returns home, produces configured item into building inventory.
    /// </summary>
    public class GathererActorBehavior : ActorBehavior
    {
        private IBuildingInventory _cachedInventory;
        private const int CandidatesBufferCapacity = 32;
        private readonly List<Vector3> _candidatesBuffer = new List<Vector3>(CandidatesBufferCapacity);

        private GathererConfig Config => Definition?.CategoryConfig as GathererConfig;

        private (PlacedObjectEntry[] targetEntries, Item producedItem) GetGatherParams()
        {
            var config = Config;
            if (config != null && config.TargetEntries != null && config.TargetEntries.Length > 0)
                return (config.TargetEntries, config.ProducedItem);
            return (null, Item.Wood);
        }

        private IBuildingInventory GetHomeInventory()
        {
            if (_cachedInventory == null && HomeBuilding != null)
                _cachedInventory = HomeBuilding.GetComponent<BuildingInventory>();
            return _cachedInventory;
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            var (targetEntries, _) = GetGatherParams();
            if (targetEntries == null || targetEntries.Length == 0)
            {
                GameDebugLogger.Log("[Gatherer] No target entries configured");
                return (null, false);
            }

            _candidatesBuffer.Clear();
            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            foreach (var entry in targetEntries)
            {
                if (entry == null) continue;
                var targetParent = WorldBootstrap.GetParentByEntryName(entry.Name);
                if (targetParent == null || targetParent.childCount == 0) continue;

                for (int i = 0; i < targetParent.childCount; i++)
                {
                    var target = targetParent.GetChild(i);
                    var (tx, ty, tz) = worldScale.WorldToBlock(target.position);
                    if (OperationalRange.IsCellInRange(hx, hz, tx, tz, RangeCells, RangeType))
                        _candidatesBuffer.Add(target.position);
                }
            }

            if (_candidatesBuffer.Count == 0)
            {
                GameDebugLogger.Log($"[Gatherer] {gameObject.name} TryGetTarget: 0 targets in range");
                return (null, false);
            }

            var homePos = HomeBuilding.position;
            _candidatesBuffer.Sort((a, b) =>
                Vector3.SqrMagnitude(a - homePos).CompareTo(Vector3.SqrMagnitude(b - homePos)));

            foreach (var targetPos in _candidatesBuffer)
            {
                var path = BuildPathTo(HomeBuilding.position, targetPos, fromIsBuilding: true, toIsBuilding: false);
                if (path != null && path.Count > 0)
                {
                    GameDebugLogger.Log($"[Gatherer] {gameObject.name} TryGetTarget: found path (len={path.Count})");
                    return (targetPos, true);
                }
            }

            GameDebugLogger.Log($"[Gatherer] {gameObject.name} TryGetTarget: {_candidatesBuffer.Count} in range but no valid path");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var (_, producedItem) = GetGatherParams();

            var inventory = GetHomeInventory();
            if (inventory != null && inventory.HasSpaceFor(1))
                inventory.AddItem(producedItem, 1);
        }

        protected override bool IsBuildingInventoryFull()
        {
            var inventory = GetHomeInventory();
            return inventory != null && !inventory.HasSpaceFor(1);
        }
    }
}
