using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pure;
using Voxel.Pathfinding;

namespace Voxel
{
    /// <summary>
    /// Performs placement operations: PlaceSingle, PlaceInLine, PlaceInArea.
    /// Handles road vs prefab, extend-through logic, tree removal.
    /// </summary>
    public class PlacementExecutor
    {
        private readonly WorldBootstrap _worldBootstrap;

        public PlacementExecutor(WorldBootstrap worldBootstrap)
        {
            _worldBootstrap = worldBootstrap;
        }

        public void PlaceSingle((int x, int y, int z) block, PlacedObjectEntry entry, float rotationY)
        {
            if (_worldBootstrap == null || entry == null)
            {
                GameDebugLogger.Log($"[PlacementExecutor] PlaceSingle skipped: worldBootstrap={_worldBootstrap != null}, entry={entry != null}");
                return;
            }

            if (entry.IsSurfaceOverlay)
            {
                if (entry.CanReplaceEnvironment)
                    _worldBootstrap.RemoveEnvironmentAtBlock(block.x, block.y, block.z);
                _worldBootstrap.AddRoadAt(block.x, block.y, block.z);
                _worldBootstrap.SaveWorld();
                _worldBootstrap.Renderer?.InvalidateChunkAt(block.x, block.y - 1, block.z);
                return;
            }

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null)
            {
                GameDebugLogger.Log($"[PlacementExecutor] PlaceSingle skipped at {block}: parent={parent != null}, prefab={entry.Prefab != null}");
                return;
            }

            if (entry.CanReplaceEnvironment)
                _worldBootstrap.RemoveEnvironmentAtBlock(block.x, block.y, block.z);

            var worldScale = GetWorldScale();
            var pos = worldScale.BlockToWorld(block.x + 0.5f, block.y, block.z + 0.5f);
            var rotation = Quaternion.Euler(0f, rotationY, 0f);
            float prefabHeight = entry.PrefabHeightInUnits > 0 ? entry.PrefabHeightInUnits : 2f;
            float scaleMult = entry.ScaleMultiplier > 0 ? entry.ScaleMultiplier : 1f;
            var scale = worldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

            var instance = Object.Instantiate(entry.Prefab, pos, rotation, parent);
            instance.name = entry.Prefab.name;
            instance.transform.localScale = scale;
            TryAddBuildingInventory(instance, entry);

            _worldBootstrap.SaveWorld();
            _worldBootstrap.SpawnActorsForBuildings();
            GameDebugLogger.Log($"[PlacementExecutor] PlaceSingle OK: '{entry.Name}' at {block}");
        }

        public void PlaceInLine((int x, int z) start, (int x, int z) end, PlacedObjectEntry entry)
        {
            if (_worldBootstrap == null || entry == null) return;

            var grid = _worldBootstrap.Grid;
            var waterConfig = _worldBootstrap.WaterConfig;
            if (grid == null || waterConfig == null) return;

            int waterLevelY = waterConfig.GetWaterLevelY(grid.Height);
            var isBlockValid = PlacementValidator.GetBlockValidatorForPlacement(_worldBootstrap, entry);
            var path = PlacementBlockService.GetPathForLine(start, end, grid, waterLevelY, isBlockValid);
            if (path == null || path.Count == 0) return;

            bool skipExisting = PlacementValidator.ShouldSkipPreviewOnExistingRoads(entry);

            if (entry.IsSurfaceOverlay)
            {
                int roadPlaced = 0;
                foreach (var node in path)
                {
                    int topY = PlacementUtility.GetTopSolidY(grid, node.X, node.Z, grid.Height);
                    if (topY < 0 || topY < waterLevelY) continue;

                    int surfaceY = topY + 1;
                    if (skipExisting && _worldBootstrap.HasRoadAt(node.X, surfaceY, node.Z)) continue;
                    if (_worldBootstrap.HasBlockingObjectAtBlock(node.X, surfaceY, node.Z)) continue;

                    if (entry.CanReplaceEnvironment)
                        _worldBootstrap.RemoveEnvironmentAtBlock(node.X, surfaceY, node.Z);

                    _worldBootstrap.AddRoadAt(node.X, surfaceY, node.Z);
                    _worldBootstrap.Renderer?.InvalidateChunkAt(node.X, topY, node.Z);
                    roadPlaced++;
                }
                if (roadPlaced > 0)
                    _worldBootstrap.SaveWorld();
                return;
            }

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null) return;

            _worldBootstrap.GetOrCreateParentForEntry(entry.Name);
            float prefabHeight = entry.PrefabHeightInUnits > 0 ? entry.PrefabHeightInUnits : 2f;
            float scaleMult = entry.ScaleMultiplier > 0 ? entry.ScaleMultiplier : 1f;
            var worldScale = GetWorldScale();
            var scale = worldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

            int placed = 0;
            foreach (var node in path)
            {
                int topY = PlacementUtility.GetTopSolidY(grid, node.X, node.Z, grid.Height);
                if (topY < 0 || topY < waterLevelY) continue;

                int surfaceY = topY + 1;
                if (_worldBootstrap.HasBlockingObjectAtBlock(node.X, surfaceY, node.Z)) continue;

                if (entry.CanReplaceEnvironment)
                    _worldBootstrap.RemoveEnvironmentAtBlock(node.X, surfaceY, node.Z);

                var pos = worldScale.BlockToWorld(node.X + 0.5f, surfaceY, node.Z + 0.5f);
                var rotation = entry.RandomRotation
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Quaternion.identity;
                var instance = Object.Instantiate(entry.Prefab, pos, rotation, parent);
                instance.name = entry.Prefab.name;
                instance.transform.localScale = scale;
                TryAddBuildingInventory(instance, entry);
                placed++;
            }
            if (placed > 0)
            {
                _worldBootstrap.SaveWorld();
                _worldBootstrap.SpawnActorsForBuildings();
            }
        }

        public void PlaceInArea((int x, int z) start, (int x, int z) end, PlacedObjectEntry entry)
        {
            if (_worldBootstrap == null || entry == null) return;

            var grid = _worldBootstrap.Grid;
            var waterConfig = _worldBootstrap.WaterConfig;
            if (grid == null || waterConfig == null) return;

            int waterLevelY = waterConfig.GetWaterLevelY(grid.Height);

            if (entry.IsSurfaceOverlay)
            {
                int roadPlaced = 0;
                foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, grid, waterLevelY))
                {
                    if (_worldBootstrap.HasBlockingObjectAtBlock(x, surfaceY, z)) continue;

                    if (entry.CanReplaceEnvironment)
                        _worldBootstrap.RemoveEnvironmentAtBlock(x, surfaceY, z);

                    _worldBootstrap.AddRoadAt(x, surfaceY, z);
                    _worldBootstrap.Renderer?.InvalidateChunkAt(x, surfaceY - 1, z);
                    roadPlaced++;
                }

                if (roadPlaced > 0)
                    _worldBootstrap.SaveWorld();
                return;
            }

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null) return;

            _worldBootstrap.GetOrCreateParentForEntry(entry.Name);

            float prefabHeight = entry.PrefabHeightInUnits > 0 ? entry.PrefabHeightInUnits : 2f;
            float scaleMult = entry.ScaleMultiplier > 0 ? entry.ScaleMultiplier : 1f;
            var worldScale = GetWorldScale();
            var scale = worldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

            int placed = 0;
            foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, grid, waterLevelY))
            {
                if (_worldBootstrap.HasBlockingObjectAtBlock(x, surfaceY, z)) continue;

                if (entry.CanReplaceEnvironment)
                    _worldBootstrap.RemoveEnvironmentAtBlock(x, surfaceY, z);

                var pos = worldScale.BlockToWorld(x + 0.5f, surfaceY, z + 0.5f);
                var rotation = entry.RandomRotation
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Quaternion.identity;
                var instance = Object.Instantiate(entry.Prefab, pos, rotation, parent);
                instance.name = entry.Prefab.name;
                instance.transform.localScale = scale;
                TryAddBuildingInventory(instance, entry);
                placed++;
            }

            if (placed > 0)
            {
                _worldBootstrap.SaveWorld();
                _worldBootstrap.SpawnActorsForBuildings();
            }
        }

        private static void TryAddBuildingInventory(GameObject instance, PlacedObjectEntry entry)
        {
            if (entry == null || entry.InventoryCapacity <= 0 || entry.UsesGlobalStorage) return;
            var inv = instance.GetComponent<BuildingInventory>();
            if (inv == null) inv = instance.AddComponent<BuildingInventory>();
            inv.Initialize(entry.Name, entry.InventoryCapacity);
        }

        private WorldScale GetWorldScale()
        {
            var wp = _worldBootstrap?.WorldParameters;
            return new WorldScale(wp != null ? wp.BlockScale : 1f);
        }
    }
}
