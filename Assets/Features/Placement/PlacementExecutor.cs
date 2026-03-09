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
        private readonly List<(int x, int y, int z)> _footprintBlocksBuffer = new(32);

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
                PlaceRoadAtBlock(block.x, block.y, block.z, entry);
                return;
            }

            var (sizeX, sizeZ) = entry.GetEffectiveArea(rotationY);
            int originX = block.x;
            int originZ = block.z;
            int baseY = block.y;

            RemoveEnvironmentInFootprint(originX, originZ, sizeX, sizeZ, baseY, entry);

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null)
            {
                GameDebugLogger.Log($"[PlacementExecutor] PlaceSingle skipped at {block}: parent={parent != null}, prefab={entry.Prefab != null}");
                return;
            }

            var worldScale = GetWorldScale();
            var (centerX, centerZ) = PlacementUtility.GetFootprintCenter(originX, originZ, sizeX, sizeZ);
            var pos = worldScale.BlockToWorld(centerX, baseY, centerZ);
            var rotation = Quaternion.Euler(0f, rotationY, 0f);
            var scale = GetScaleForEntry(entry, entry.AreaSizeX, entry.AreaSizeZ);

            PlacePrefabInstance(entry, parent, pos, rotation, scale, sizeX, sizeZ);
            GameDebugLogger.Log($"[PlacementExecutor] PlaceSingle OK: '{entry.Name}' at origin ({originX},{baseY},{originZ}) size {sizeX}x{sizeZ}");
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
                foreach (var node in path)
                {
                    int topY = PlacementUtility.GetTopSolidY(grid, node.X, node.Z, grid.Height);
                    if (topY < 0 || topY < waterLevelY) continue;

                    int surfaceY = topY + 1;
                    if (skipExisting && _worldBootstrap.HasRoadAt(node.X, surfaceY, node.Z)) continue;
                    if (_worldBootstrap.HasBlockingObjectAtBlock(node.X, surfaceY, node.Z)) continue;

                    PlaceRoadAtBlock(node.X, surfaceY, node.Z, entry);
                }
                return;
            }

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null) return;

            _worldBootstrap.GetOrCreateParentForEntry(entry.Name);
            var worldScale = GetWorldScale();
            var scale = GetScaleForEntry(entry, 1, 1);

            foreach (var node in path)
            {
                int topY = PlacementUtility.GetTopSolidY(grid, node.X, node.Z, grid.Height);
                if (topY < 0 || topY < waterLevelY) continue;

                int surfaceY = topY + 1;
                if (_worldBootstrap.HasBlockingObjectAtBlock(node.X, surfaceY, node.Z)) continue;

                RemoveEnvironmentAtBlockIfNeeded(node.X, surfaceY, node.Z, entry);
                PlacePrefabAtBlockCenter(entry, parent, node.X, surfaceY, node.Z, worldScale, scale, 1, 1);
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
                foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, grid, waterLevelY))
                {
                    if (_worldBootstrap.HasBlockingObjectAtBlock(x, surfaceY, z)) continue;
                    PlaceRoadAtBlock(x, surfaceY, z, entry);
                }
                return;
            }

            var parent = _worldBootstrap.GetParentForEntry(entry);
            if (parent == null || entry.Prefab == null) return;

            _worldBootstrap.GetOrCreateParentForEntry(entry.Name);
            var worldScale = GetWorldScale();
            var scale = GetScaleForEntry(entry, 1, 1);

            foreach (var (x, surfaceY, z) in PlacementBlockService.GetBlocksForArea(start, end, grid, waterLevelY))
            {
                if (_worldBootstrap.HasBlockingObjectAtBlock(x, surfaceY, z)) continue;

                RemoveEnvironmentAtBlockIfNeeded(x, surfaceY, z, entry);
                PlacePrefabAtBlockCenter(entry, parent, x, surfaceY, z, worldScale, scale, 1, 1);
            }
        }

        private void PlaceRoadAtBlock(int x, int y, int z, PlacedObjectEntry entry)
        {
            if (entry.CanReplaceEnvironment)
                _worldBootstrap.RemoveEnvironmentAtBlock(x, y, z);

            _worldBootstrap.AddRoadAt(x, y, z);
            _worldBootstrap.Renderer?.InvalidateChunkAt(x, y - 1, z);
        }

        private void RemoveEnvironmentInFootprint(int originX, int originZ, int sizeX, int sizeZ, int baseY, PlacedObjectEntry entry)
        {
            if (!entry.CanReplaceEnvironment) return;

            _footprintBlocksBuffer.Clear();
            PlacementUtility.GetFootprintBlocks(originX, originZ, baseY, sizeX, sizeZ, _footprintBlocksBuffer);
            foreach (var (bx, by, bz) in _footprintBlocksBuffer)
                _worldBootstrap.RemoveEnvironmentAtBlock(bx, by, bz);
        }

        private void RemoveEnvironmentAtBlockIfNeeded(int x, int y, int z, PlacedObjectEntry entry)
        {
            if (entry.CanReplaceEnvironment)
                _worldBootstrap.RemoveEnvironmentAtBlock(x, y, z);
        }

        private void PlacePrefabAtBlockCenter(PlacedObjectEntry entry, Transform parent, int x, int y, int z,
            WorldScale worldScale, Vector3 scale, int sizeX, int sizeZ)
        {
            var pos = worldScale.BlockToWorld(x + 0.5f, y, z + 0.5f);
            var rotation = entry.RandomRotation
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;
            PlacePrefabInstance(entry, parent, pos, rotation, scale, sizeX, sizeZ);
        }

        private void PlacePrefabInstance(PlacedObjectEntry entry, Transform parent, Vector3 pos, Quaternion rotation, Vector3 scale, int sizeX, int sizeZ)
        {
            var bounds = PlacementUtility.GetPrefabBounds(entry.Prefab, entry.AreaSizeX, entry.AreaSizeZ, entry.HeightInBlocks, _worldBootstrap?.WorldParameters?.VoxelsPerBlockAxis ?? PlacementUtility.DefaultVoxelsPerBlockAxis);
            pos -= PlacementUtility.PivotOffsetForCenteringXZ(bounds, scale);

            var instance = Object.Instantiate(entry.Prefab, pos, rotation, parent);
            instance.name = entry.Prefab.name;
            instance.transform.localScale = scale;
            TryAddBuildingInventory(instance, entry);
            TryAddCritterSpawner(instance, entry);
            _worldBootstrap.NotifyObjectPlaced(entry.Name, instance.transform);
            _worldBootstrap.SpawnActorForBuildingIfNeeded(entry, instance.transform);
        }

        private Vector3 GetScaleForEntry(PlacedObjectEntry entry, int sizeX, int sizeZ)
        {
            var worldScale = GetWorldScale();
            int voxelsPerBlock = _worldBootstrap?.WorldParameters?.VoxelsPerBlockAxis ?? PlacementUtility.DefaultVoxelsPerBlockAxis;
            var bounds = PlacementUtility.GetPrefabBounds(entry.Prefab, sizeX, sizeZ, entry.HeightInBlocks, voxelsPerBlock);
            return worldScale.ScaleForVoxelModel(sizeX, sizeZ, entry.HeightInBlocks, bounds);
        }

        private static void TryAddBuildingInventory(GameObject instance, PlacedObjectEntry entry)
        {
            if (entry == null || entry.InventoryCapacity <= 0 || entry.UsesGlobalStorage) return;
            var inv = instance.GetComponent<BuildingInventory>();
            if (inv == null) inv = instance.AddComponent<BuildingInventory>();
            inv.Initialize(entry.Name, entry.InventoryCapacity);
        }

        private void TryAddCritterSpawner(GameObject instance, PlacedObjectEntry entry)
        {
            if (entry == null || entry.CritterSpawnerConfig == null || _worldBootstrap == null) return;
            var spawner = instance.GetComponent<CritterSpawner>();
            if (spawner == null) spawner = instance.AddComponent<CritterSpawner>();
            spawner.Initialize(entry.CritterSpawnerConfig, _worldBootstrap);
        }

        private WorldScale GetWorldScale()
        {
            var wp = _worldBootstrap?.WorldParameters;
            return new WorldScale(wp != null ? wp.BlockScale : 1f);
        }
    }
}
