using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Reusable gatherer behavior: selects a random target object in range (e.g. Tree, Wheat),
    /// paths to it, works, returns home, works at home, produces item into building inventory.
    /// Subclass and override TargetEntryName and ProducedItem.
    /// </summary>
    public abstract class GathererActorBehavior : ActorBehavior
    {
        private IBuildingInventory _cachedInventory;

        /// <summary>Registry entry name for the objects to gather (e.g. "Tree", "Wheat").</summary>
        protected abstract string TargetEntryName { get; }

        /// <summary>Item to add to building inventory when work completes inside.</summary>
        protected abstract Item ProducedItem { get; }

        /// <summary>Display name for debug logs (e.g. "Woodchuck", "WheatFarm").</summary>
        protected abstract string GathererName { get; }

        private IBuildingInventory GetHomeInventory()
        {
            if (_cachedInventory == null && HomeBuilding != null)
                _cachedInventory = HomeBuilding.GetComponent<BuildingInventory>();
            return _cachedInventory;
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            var targetParent = WorldBootstrap.GetParentByEntryName(TargetEntryName);
            if (targetParent == null || targetParent.childCount == 0)
            {
                GameDebugLogger.Log($"[{GathererName}] {gameObject.name} TryGetTarget: no {TargetEntryName}s (parent={targetParent != null}, count={targetParent?.childCount ?? 0})");
                return (null, false);
            }

            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            var candidates = new List<Vector3>();
            for (int i = 0; i < targetParent.childCount; i++)
            {
                var target = targetParent.GetChild(i);
                var (tx, ty, tz) = worldScale.WorldToBlock(target.position);
                if (OperationalRange.IsCellInRange(hx, hz, tx, tz, RangeCells, RangeType))
                    candidates.Add(target.position);
            }

            if (candidates.Count == 0)
            {
                GameDebugLogger.Log($"[{GathererName}] {gameObject.name} TryGetTarget: {targetParent.childCount} {TargetEntryName}s total, 0 in range (home=({hx},{hz}) range={RangeCells} cells)");
                return (null, false);
            }

            candidates.Shuffle();

            foreach (var targetPos in candidates)
            {
                var path = BuildPathTo(HomeBuilding.position, targetPos, fromIsBuilding: true, toIsBuilding: false);
                if (path != null && path.Count > 0)
                {
                    GameDebugLogger.Log($"[{GathererName}] {gameObject.name} TryGetTarget: found path to {TargetEntryName} at {targetPos} (path len={path.Count})");
                    return (targetPos, true);
                }
            }

            GameDebugLogger.Log($"[{GathererName}] {gameObject.name} TryGetTarget: {candidates.Count} {TargetEntryName}s in range but no valid path to any");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var inventory = GetHomeInventory();
            if (inventory != null && inventory.HasSpaceFor(1))
                inventory.AddItem(ProducedItem, 1);
        }

        protected override bool IsBuildingInventoryFull()
        {
            var inventory = GetHomeInventory();
            return inventory != null && !inventory.HasSpaceFor(1);
        }
    }
}
