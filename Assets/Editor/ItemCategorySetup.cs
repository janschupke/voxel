using UnityEditor;
using UnityEngine;

namespace Voxel.Editor
{
    /// <summary>
    /// Creates ItemCategory assets and migrates ItemRegistry/ItemDefinitions to use them.
    /// Run via Voxel > Setup Item Categories.
    /// </summary>
    public static class ItemCategorySetup
    {
        private const string CategoriesPath = "Assets/Core/Registries/Data/Categories";
        private const string ItemRegistryPath = "Assets/Core/Registries/Data/ItemRegistry.asset";
        private const string InventoryDataPath = "Assets/Features/Inventory/Data";

        private static readonly (string Name, string DisplayName)[] CategoryDefinitions =
        {
            ("BuildingMaterials", "Building Materials"),
            ("RawMaterials", "Raw Materials"),
            ("Food", "Food"),
            ("Other", "Other")
        };

        private static readonly (string ItemAsset, string CategoryName)[] ItemCategoryMapping =
        {
            ("Wood.asset", "BuildingMaterials"),
            ("Brick.asset", "BuildingMaterials"),
            ("Stone.asset", "RawMaterials"),
            ("Wheat.asset", "RawMaterials"),
            ("Flour.asset", "RawMaterials"),
            ("Hops.asset", "RawMaterials"),
            ("IronOre.asset", "RawMaterials"),
            ("IronBar.asset", "RawMaterials"),
            ("Bread.asset", "Food"),
            ("Chicken.asset", "Food"),
            ("Fruit.asset", "Food"),
            ("Beer.asset", "Other"),
            ("Sword.asset", "Other"),
            ("Jelly.asset", "Other")
        };

        [MenuItem("Voxel/Setup Item Categories")]
        public static void SetupItemCategories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Core/Registries/Data"))
                return;
            if (!AssetDatabase.IsValidFolder("Assets/Core/Registries/Data/Categories"))
                AssetDatabase.CreateFolder("Assets/Core/Registries/Data", "Categories");

            var categories = new System.Collections.Generic.Dictionary<string, ItemCategory>();

            foreach (var (name, displayName) in CategoryDefinitions)
            {
                var path = $"{CategoriesPath}/{name}.asset";
                var cat = AssetDatabase.LoadAssetAtPath<ItemCategory>(path);
                if (cat == null)
                {
                    cat = ScriptableObject.CreateInstance<ItemCategory>();
                    using (var so = new SerializedObject(cat))
                    {
                        so.FindProperty("displayName").stringValue = displayName;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AssetDatabase.CreateAsset(cat, path);
                }
                else
                {
                    using (var so = new SerializedObject(cat))
                    {
                        so.FindProperty("displayName").stringValue = displayName;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(cat);
                }
                categories[name] = cat;
            }

            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(ItemRegistryPath);
            if (registry != null)
            {
                using (var so = new SerializedObject(registry))
                {
                    var orderProp = so.FindProperty("categoryDisplayOrder");
                    orderProp.ClearArray();
                    foreach (var (name, _) in CategoryDefinitions)
                    {
                        if (categories.TryGetValue(name, out var cat))
                        {
                            orderProp.arraySize++;
                            orderProp.GetArrayElementAtIndex(orderProp.arraySize - 1).objectReferenceValue = cat;
                        }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(registry);
            }

            foreach (var (itemAsset, categoryName) in ItemCategoryMapping)
            {
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{InventoryDataPath}/{itemAsset}");
                if (def == null) continue;
                if (!categories.TryGetValue(categoryName, out var cat)) continue;

                using (var so = new SerializedObject(def))
                {
                    so.FindProperty("category").objectReferenceValue = cat;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(def);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Item categories setup complete.");
        }
    }
}
