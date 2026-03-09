using UnityEditor;
using UnityEngine;

namespace Voxel.Editor
{
    /// <summary>
    /// Creates PlacedObjectCategory assets and assigns them to PlacedObjectEntry assets.
    /// Run via Voxel > Setup Placed Object Categories.
    /// </summary>
    public static class PlacedObjectCategorySetup
    {
        private const string CategoriesPath = "Assets/Core/Registries/Data/PlacedObjectCategories";
        private const string PlacedObjectRegistryPath = "Assets/Core/Registries/Data/PlacedObjectRegistry.asset";

        private static readonly (string Name, string DisplayName)[] CategoryDefinitions =
        {
            ("Producers", "Producers"),
            ("Other", "Other")
        };

        private static readonly string[] ProducerEntryNames =
        {
            "Tree", "Wheat Farm", "Wheat Field", "Hops Farm", "Hops Field", "Fruit Farm", "Fruit Field",
            "Jelly Farm", "Jelly Field", "Woodchuck", "Quarry", "Iron Mine"
        };

        [MenuItem("Voxel/Setup Placed Object Categories")]
        public static void SetupPlacedObjectCategories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Core/Registries/Data"))
                return;
            if (!AssetDatabase.IsValidFolder("Assets/Core/Registries/Data/PlacedObjectCategories"))
                AssetDatabase.CreateFolder("Assets/Core/Registries/Data", "PlacedObjectCategories");

            var categories = new System.Collections.Generic.Dictionary<string, PlacedObjectCategory>();

            foreach (var (name, displayName) in CategoryDefinitions)
            {
                var path = $"{CategoriesPath}/{name}.asset";
                var cat = AssetDatabase.LoadAssetAtPath<PlacedObjectCategory>(path);
                if (cat == null)
                {
                    cat = ScriptableObject.CreateInstance<PlacedObjectCategory>();
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

            var registry = AssetDatabase.LoadAssetAtPath<PlacedObjectRegistry>(PlacedObjectRegistryPath);
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

            var producerSet = new System.Collections.Generic.HashSet<string>(ProducerEntryNames, System.StringComparer.OrdinalIgnoreCase);

            if (registry != null)
            {
                foreach (var entry in registry.Entries)
                {
                    if (entry == null) continue;
                    var categoryName = producerSet.Contains(entry.Name) ? "Producers" : "Other";
                    if (!categories.TryGetValue(categoryName, out var cat)) continue;

                    using (var so = new SerializedObject(entry))
                    {
                        so.FindProperty("category").objectReferenceValue = cat;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(entry);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Placed object categories setup complete.");
        }
    }
}
