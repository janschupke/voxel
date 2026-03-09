using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Voxel;

namespace Voxel.Editor
{
    /// <summary>
    /// Generates prefabs from MagicaVoxel OBJ models in Structures/Models.
    /// Auto-discovers all .obj files. Overrides (prefab name, shader) from VoxelModelMapping.json.
    /// </summary>
    public static class VoxelPrefabGenerator
    {
        private const string ModelsPath = "Assets/Features/Structures/Models";
        private const string MaterialsPath = "Assets/Features/Structures/Materials";
        private const string PrefabsPath = "Assets/Features/Structures/Prefabs";
        private const string ConfigPath = "Assets/Features/Structures/Config/VoxelModelMapping.json";

        [Serializable]
        private class MappingEntry
        {
            public string model;
            public string prefab;
            public string shader;
        }

        [Serializable]
        private class MappingConfig
        {
            public MappingEntry[] overrides;
        }

        private static IReadOnlyDictionary<string, (string PrefabName, string ShaderOverride)> LoadMapping()
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

            string path = Path.Combine(Application.dataPath, "..", ConfigPath.Replace('/', Path.DirectorySeparatorChar));
            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                return result;

            try
            {
                string json = File.ReadAllText(path);
                var config = JsonUtility.FromJson<MappingConfig>(json);
                if (config?.overrides == null) return result;

                foreach (var e in config.overrides)
                {
                    if (string.IsNullOrEmpty(e?.model)) continue;
                    result[e.model] = (e.prefab ?? e.model, string.IsNullOrEmpty(e.shader) ? null : e.shader);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VoxelModelMapping: {ex.Message}");
            }

            return result;
        }

        [MenuItem("Voxel/Generate Prefabs from Models")]
        public static void GenerateAll()
        {
            if (!Directory.Exists(ModelsPath))
            {
                Debug.LogWarning($"Models path not found: {ModelsPath}");
                return;
            }

            AssetDatabase.Refresh();

            var mapping = LoadMapping();
            string modelsFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ModelsPath));
            if (!Directory.Exists(modelsFullPath))
            {
                Debug.LogWarning($"Models path not found: {modelsFullPath}");
                return;
            }

            string[] objFiles = Directory.GetFiles(modelsFullPath, "*.obj");
            int count = 0;

            foreach (string objPath in objFiles)
            {
                string fileName = Path.GetFileName(objPath);
                string modelName = Path.GetFileNameWithoutExtension(objPath);
                string assetPath = $"{ModelsPath}/{fileName}";

                string prefabName = modelName;
                string shaderOverride = null;

                if (mapping.TryGetValue(modelName, out var entry))
                {
                    prefabName = entry.PrefabName;
                    shaderOverride = entry.ShaderOverride;
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                string texPath = $"{ModelsPath}/{modelName}.png";
                string texFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", texPath.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(texFullPath))
                    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

                if (GeneratePrefab(assetPath, modelName, prefabName, shaderOverride))
                    count++;
            }

            RepairPlacedObjectReferences();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"VoxelPrefabGenerator: Generated {count} prefab(s).");
        }

        private static void RepairPlacedObjectReferences()
        {
            var registry = AssetDatabase.LoadAssetAtPath<PlacedObjectRegistry>("Assets/Core/Registries/Data/PlacedObjectRegistry.asset");
            if (registry == null) return;

            foreach (var entry in registry.Entries)
            {
                if (entry == null || entry.Prefab != null) continue;

                string prefabName = entry.Name?.Replace(" ", "") ?? "";
                if (string.IsNullOrEmpty(prefabName)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/{prefabName}.prefab");
                if (prefab != null)
                {
                    using (var so = new SerializedObject(entry))
                    {
                        var prefabProp = so.FindProperty("Prefab");
                        if (prefabProp != null)
                        {
                            prefabProp.objectReferenceValue = prefab;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
        }

        private static bool GeneratePrefab(string objPath, string modelName, string prefabName, string shaderOverride)
        {
            Mesh mesh = LoadMeshFromModel(objPath);
            if (mesh == null)
            {
                Debug.LogWarning($"Could not load mesh from {objPath}");
                return false;
            }

            Material material = GetOrCreateMaterial(modelName, prefabName, shaderOverride);
            if (material == null)
            {
                Debug.LogWarning($"Could not create material for {modelName}");
                return false;
            }

            GameObject go = EditorUtility.CreateGameObjectWithHideFlags(prefabName, HideFlags.HideInHierarchy);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var bounds = mesh.bounds;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = bounds.size;
            bc.center = bounds.center;

            string prefabPath = $"{PrefabsPath}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            if (prefab != null)
            {
                Debug.Log($"Generated {prefabPath}");
                return true;
            }

            return false;
        }

        private static Mesh LoadMeshFromModel(string modelPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            foreach (var a in assets)
            {
                if (a is Mesh mesh)
                    return mesh;
            }
            return null;
        }

        private static Material GetOrCreateMaterial(string modelName, string prefabName, string shaderOverride)
        {
            string matPath = $"{MaterialsPath}/{prefabName}Material.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            Shader shader = shaderOverride != null
                ? Shader.Find(shaderOverride)
                : Shader.Find("Voxel/BaseVoxel");

            if (shader == null)
            {
                Debug.LogWarning($"Shader not found: {shaderOverride ?? "Voxel/BaseVoxel"}");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            bool useTexture = shaderOverride == null || shaderOverride == "Voxel/BaseVoxel";
            string texPath = $"{ModelsPath}/{modelName}.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = $"{prefabName}Material";
                if (useTexture && tex != null)
                    mat.SetTexture("_BaseMap", tex);
                if (!Directory.Exists(MaterialsPath))
                    Directory.CreateDirectory(MaterialsPath);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (useTexture && tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }
    }
}
