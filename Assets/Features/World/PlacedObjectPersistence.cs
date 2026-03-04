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

            foreach (var kv in parentsByEntryName)
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    if (child == null) continue;
                    var (bx, by, bz) = worldScale.WorldToBlock(child.position);
                    list.Add(new PlacedObjectData(kv.Key, bx, by, bz, child.eulerAngles.y));
                }
            }

            foreach (var (x, y, z) in roadOverlay.GetAllBlocks())
            {
                list.Add(new PlacedObjectData("Road", x, y, z, 0f));
            }

            return list.Count > 0 ? list : null;
        }

        public static List<BuildingInventorySaveData> CollectBuildingInventoriesForSave(
            IReadOnlyDictionary<string, Transform> parentsByEntryName,
            PlacedObjectRegistry registry,
            WorldParameters worldParameters)
        {
            var worldScale = new WorldScale(worldParameters != null ? worldParameters.BlockScale : 1f);
            var list = new List<BuildingInventorySaveData>();

            foreach (var kv in parentsByEntryName)
            {
                if (kv.Value == null || kv.Key == "Road") continue;
                var entry = registry?.GetByName(kv.Key);
                if (entry != null && entry.UsesGlobalStorage) continue;
                for (int i = 0; i < kv.Value.childCount; i++)
                {
                    var child = kv.Value.GetChild(i);
                    if (child == null) continue;
                    var inv = child.GetComponent<BuildingInventory>();
                    if (inv == null || inv.GetTotalCount() <= 0) continue;

                    var (bx, by, bz) = worldScale.WorldToBlock(child.position);
                    var items = new List<(int ItemId, int Count)>();
                    foreach (var (item, count) in inv.GetAllItems())
                    {
                        if (count > 0)
                            items.Add(((int)item, count));
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
            System.Action<VoxelGrid> runTreePlacement)
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
                if (p.EntryName == "Road")
                {
                    addRoadAt?.Invoke(p.BlockX, p.BlockY, p.BlockZ);
                    continue;
                }

                var entry = registry.GetByName(p.EntryName);
                var prefab = entry?.Prefab ?? (p.EntryName == "Tree" ? islandPipelineConfig?.TreeScatterConfig?.TreePrefab : null);
                if (prefab == null) continue;

                var parent = getOrCreateParent(p.EntryName);
                if (parent == null) continue;

                float prefabHeight = entry != null && entry.PrefabHeightInUnits > 0 ? entry.PrefabHeightInUnits : 2f;
                float scaleMult = entry != null && entry.ScaleMultiplier > 0 ? entry.ScaleMultiplier : 1f;
                var scale = worldScale.ScaleVectorForBlockSizedPrefab(prefabHeight) * scaleMult;

                var instance = Object.Instantiate(prefab, p.ToWorldPosition(worldScale), p.ToRotation(), parent);
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
                var treeParent = getParentByName("Tree");
                if (treeParent == null || treeParent.childCount == 0)
                    runTreePlacement?.Invoke(grid);
            }
        }
    }
}
