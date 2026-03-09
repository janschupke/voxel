using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Handles save/load of placed objects and building inventories. Extracted from PlacedObjectManager.
    /// </summary>
    public static class PlacedObjectPersistence
    {
        public static List<PlacedObjectData> CollectPlacedObjectsForSave(
            IReadOnlyDictionary<string, Transform> parentsByEntryName,
            PlacedObjectRegistry registry,
            RoadOverlay roadOverlay,
            WorldParameters worldParameters)
        {
            if (registry == null) return null;

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var list = new List<PlacedObjectData>();

            int voxelsPerBlock = worldParameters?.VoxelsPerBlockAxis ?? 16;
            foreach (var kv in parentsByEntryName)
            {
                if (kv.Value == null || kv.Key == PlacedObjectKeys.Road) continue;
                var entry = registry?.GetByName(kv.Key);
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    if (child == null) continue;
                    var (bx, by, bz) = entry != null && entry.Prefab != null
                        ? GetFootprintOriginFromTransform(child, entry, worldScale, voxelsPerBlock)
                        : worldScale.WorldToBlock(child.position);
                    list.Add(new PlacedObjectData(kv.Key, bx, by, bz, child.eulerAngles.y));
                }
            }

            foreach (var (x, y, z) in roadOverlay.GetAllBlocks())
            {
                list.Add(new PlacedObjectData(PlacedObjectKeys.Road, x, y, z, 0f));
            }

            return list.Count > 0 ? list : null;
        }

        public static List<BuildingInventorySaveData> CollectBuildingInventoriesForSave(
            IReadOnlyDictionary<string, Transform> parentsByEntryName,
            PlacedObjectRegistry registry,
            WorldParameters worldParameters,
            IItemRegistry itemRegistry)
        {
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var list = new List<BuildingInventorySaveData>();

            foreach (var kv in parentsByEntryName)
            {
                if (kv.Value == null || kv.Key == PlacedObjectKeys.Road) continue;
                var entry = registry?.GetByName(kv.Key);
                if (entry != null && entry.UsesGlobalStorage) continue;
                int voxelsPerBlock = worldParameters?.VoxelsPerBlockAxis ?? 16;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    if (child == null) continue;
                    var inv = child.GetComponent<BuildingInventory>();
                    if (inv == null || inv.GetTotalCount() <= 0) continue;

                    var (bx, by, bz) = entry != null && entry.Prefab != null
                        ? GetFootprintOriginFromTransform(child, entry, worldScale, voxelsPerBlock)
                        : worldScale.WorldToBlock(child.position);
                    var items = new List<(string ItemId, int Count)>();
                    foreach (var (item, count) in inv.GetAllItems())
                    {
                        if (count > 0 && itemRegistry != null)
                            items.Add((itemRegistry.GetStableId(item), count));
                    }
                    if (items.Count > 0)
                        list.Add(new BuildingInventorySaveData(kv.Key, bx, by, bz, items));
                }
            }

            return list;
        }

        public static void LoadPlacedObjects(
            IReadOnlyList<PlacedObjectData> placedObjects,
            VoxelGrid grid,
            TerrainGenerationMode terrainMode,
            IReadOnlyDictionary<(string EntryName, int BlockX, int BlockY, int BlockZ), List<(Item Item, int Count)>> inventoryLookup,
            PlacedObjectRegistry registry,
            WorldParameters worldParameters,
            IslandPipelineConfig islandPipelineConfig,
            System.Func<string, Transform> getOrCreateParent,
            System.Func<string, Transform> getParentByName,
            System.Action<int, int, int> addRoadAt,
            System.Action<VoxelGrid> runTreePlacement,
            int saveVersion = 5)
        {
            if (registry == null || placedObjects == null || placedObjects.Count == 0)
            {
                if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
                    runTreePlacement?.Invoke(grid);
                return;
            }

            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);

            foreach (var p in placedObjects)
            {
                if (p.EntryName == PlacedObjectKeys.Road)
                {
                    addRoadAt?.Invoke(p.BlockX, p.BlockY, p.BlockZ);
                    continue;
                }

                var entry = registry.GetByName(p.EntryName);
                var prefab = entry?.Prefab ?? (p.EntryName == PlacedObjectKeys.Tree ? islandPipelineConfig?.TreeScatterConfig?.TreePrefab : null);
                if (prefab == null) continue;

                var parent = getOrCreateParent(p.EntryName);
                if (parent == null) continue;

                var (sizeX, sizeZ) = entry != null ? entry.GetEffectiveArea(p.RotationY) : (1, 1);
                float heightInBlocks = entry != null && entry.HeightInBlocks > 0 ? entry.HeightInBlocks : 1f;
                int voxelsPerBlock = worldParameters?.VoxelsPerBlockAxis ?? 16;
                var bounds = GetPrefabBounds(prefab, sizeX, sizeZ, heightInBlocks, voxelsPerBlock);
                var scale = worldScale.ScaleForVoxelModel(sizeX, sizeZ, heightInBlocks, bounds);

                float centerX, centerZ;
                if (saveVersion >= 5)
                {
                    var (cx, cz) = PlacementUtility.GetFootprintCenter(p.BlockX, p.BlockZ, sizeX, sizeZ);
                    centerX = cx;
                    centerZ = cz;
                }
                else
                {
                    centerX = p.BlockX + 0.5f;
                    centerZ = p.BlockZ + 0.5f;
                }
                var pos = worldScale.BlockToWorld(centerX, p.BlockY, centerZ);
                pos -= PlacementUtility.PivotOffsetForCenteringXZ(bounds, scale);

                var instance = Object.Instantiate(prefab, pos, p.ToRotation(), parent);
                instance.name = prefab.name;
                instance.transform.localScale = scale;
                if (entry != null && entry.InventoryCapacity > 0 && !entry.UsesGlobalStorage)
                {
                    var inv = instance.GetComponent<BuildingInventory>();
                    if (inv == null) inv = instance.AddComponent<BuildingInventory>();
                    inv.Initialize(p.EntryName, entry.InventoryCapacity);
                    var key = (p.EntryName, p.BlockX, p.BlockY, p.BlockZ);
                    if (inventoryLookup != null && inventoryLookup.TryGetValue(key, out var items) && items != null && items.Count > 0)
                        inv.LoadFrom(items);
                }
            }

            if (terrainMode == TerrainGenerationMode.IslandPipeline && islandPipelineConfig != null)
            {
                var treeParent = getParentByName(PlacedObjectKeys.Tree);
                if (treeParent == null || treeParent.childCount == 0)
                    runTreePlacement?.Invoke(grid);
            }
        }

        private static (int originX, int baseY, int originZ) GetFootprintOriginFromTransform(Transform child, PlacedObjectEntry entry, WorldScale worldScale, int voxelsPerBlock)
        {
            int sizeX = entry.AreaSizeX;
            int sizeZ = entry.AreaSizeZ;
            float heightInBlocks = entry.HeightInBlocks > 0 ? entry.HeightInBlocks : 1f;
            var bounds = GetPrefabBounds(entry.Prefab, sizeX, sizeZ, heightInBlocks, voxelsPerBlock);
            var scale = worldScale.ScaleForVoxelModel(sizeX, sizeZ, heightInBlocks, bounds);
            var centerWorld = child.position + PlacementUtility.PivotOffsetForCenteringXZ(bounds, scale);
            float centerX = centerWorld.x / worldScale.BlockScale;
            float centerY = centerWorld.y / worldScale.BlockScale;
            float centerZ = centerWorld.z / worldScale.BlockScale;
            int originX = Mathf.FloorToInt(centerX - (sizeX - 1) / 2f - 0.5f);
            int baseY = Mathf.FloorToInt(centerY);
            int originZ = Mathf.FloorToInt(centerZ - (sizeZ - 1) / 2f - 0.5f);
            return (originX, baseY, originZ);
        }

        private static Bounds GetPrefabBounds(GameObject prefab, int sizeX, int sizeZ, float heightInBlocks, int voxelsPerBlock)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return mf.sharedMesh.bounds;
            var fallbackSize = new Vector3(sizeX * voxelsPerBlock, heightInBlocks * voxelsPerBlock, sizeZ * voxelsPerBlock);
            return new Bounds(Vector3.zero, fallbackSize);
        }
    }
}
