using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pathfinding;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Woodchuck actor: selects a random tree in range, paths to it, works, returns home, works at home, repeats.
    /// </summary>
    public class WoodchuckActorBehavior : ActorBehavior
    {
        private BuildingInventory _cachedInventory;

        private BuildingInventory GetHomeInventory()
        {
            if (_cachedInventory == null && HomeBuilding != null)
                _cachedInventory = HomeBuilding.GetComponent<BuildingInventory>();
            return _cachedInventory;
        }

        protected override (Vector3? Target, bool HadCandidates) TryGetReachableTarget()
        {
            var treeParent = WorldBootstrap.GetParentByEntryName("Tree");
            if (treeParent == null || treeParent.childCount == 0)
            {
                GameDebugLogger.Log($"[Woodchuck] {gameObject.name} TryGetTarget: no trees (parent={treeParent != null}, count={treeParent?.childCount ?? 0})");
                return (null, false);
            }

            var worldScale = WorldScale;
            var (hx, hy, hz) = worldScale.WorldToBlock(HomeBuilding.position);

            var candidates = new List<Vector3>();
            for (int i = 0; i < treeParent.childCount; i++)
            {
                var tree = treeParent.GetChild(i);
                var (tx, ty, tz) = worldScale.WorldToBlock(tree.position);
                if (OperationalRange.IsCellInRange(hx, hz, tx, tz, RangeCells, RangeType))
                    candidates.Add(tree.position);
            }

            if (candidates.Count == 0)
            {
                GameDebugLogger.Log($"[Woodchuck] {gameObject.name} TryGetTarget: {treeParent.childCount} trees total, 0 in range (home=({hx},{hz}) range={RangeCells} cells)");
                return (null, false);
            }

            Shuffle(candidates);

            foreach (var treePos in candidates)
            {
                var path = BuildPathTo(HomeBuilding.position, treePos, fromIsBuilding: true, toIsBuilding: false);
                if (path != null && path.Count > 0)
                {
                    GameDebugLogger.Log($"[Woodchuck] {gameObject.name} TryGetTarget: found path to tree at {treePos} (path len={path.Count})");
                    return (treePos, true);
                }
            }

            GameDebugLogger.Log($"[Woodchuck] {gameObject.name} TryGetTarget: {candidates.Count} trees in range but no valid path to any");
            return (null, true);
        }

        protected override void OnWorkCompletedInside()
        {
            var inventory = GetHomeInventory();
            if (inventory != null && inventory.HasSpaceFor(1))
                inventory.AddItem(Item.Wood, 1);
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
