using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Voxel;
using Voxel.Core;

namespace Voxel.Rendering
{
    public class ChunkManager
    {
        private static readonly Color[] DefaultBandColors =
        {
            new(0.2f, 0.6f, 0.2f),  // grass
            new(0.5f, 0.5f, 0.5f),  // mountain
            new(0.9f, 0.9f, 1f)     // snow
        };

        private readonly VoxelGrid _grid;
        private readonly Transform _parent;
        private readonly Material[] _materials;
        private readonly TerrainMaterialConfig _terrainConfig;
        private readonly WaterConfig _waterConfig;
        private readonly Material _mountainMaterial;
        private readonly Dictionary<(int, int, int), ChunkRenderer> _chunks = new();
        private readonly HashSet<(int, int, int)> _dirtyChunks = new();
        private readonly Queue<(int, int, int)> _dirtyQueue = new();
        private const int RebuildsPerFrame = 8;

        public int ChunkCountX => (_grid.Width + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountY => (_grid.Height + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountZ => (_grid.Depth + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;

        private readonly WorldScale _worldScale;

        public ChunkManager(VoxelGrid grid, Transform parent, Material material, WorldScale worldScale = default,
            TerrainMaterialConfig terrainConfig = null, WaterConfig waterConfig = null, Material mountainMaterial = null)
        {
            _grid = grid;
            _parent = parent;
            _terrainConfig = terrainConfig;
            _waterConfig = waterConfig;
            _mountainMaterial = mountainMaterial;
            _worldScale = worldScale.BlockScale > 0f ? worldScale : new WorldScale(1f);
            _materials = ResolveMaterials(material, terrainConfig, waterConfig, mountainMaterial, _worldScale.BlockScale);
        }

        private static Material[] ResolveMaterials(Material fallbackMaterial, TerrainMaterialConfig config,
            WaterConfig waterConfig, Material mountainMaterial, float blockScale = 1f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material[] terrainMaterials;
            if (config == null || config.Bands == null || config.Bands.Count == 0)
                terrainMaterials = new[] { fallbackMaterial };
            else
            {
                terrainMaterials = config.Bands.Select((band, i) =>
                {
                    if (band.material != null)
                        return band.material;
                    var mat = new Material(shader);
                    mat.color = i < DefaultBandColors.Length ? DefaultBandColors[i] : Color.gray;
                    return mat;
                }).ToArray();
            }

            var list = new List<Material>(terrainMaterials);

            if (mountainMaterial != null)
            {
                list.Add(mountainMaterial);
            }

            if (waterConfig != null && waterConfig.Enabled)
            {
                var waterMat = waterConfig.Material;
                if (waterMat == null)
                {
                    var waterShader = Shader.Find("Voxel/Water");
                    waterMat = waterShader != null
                        ? new Material(waterShader)
                        : new Material(shader) { color = new Color(0.2f, 0.5f, 0.9f, 0.7f) };
                }
                ApplyWaterConfigToMaterial(waterMat, waterConfig, blockScale);
                list.Add(waterMat);
            }

            return list.ToArray();
        }

        private static void ApplyWaterConfigToMaterial(Material waterMat, WaterConfig waterConfig, float blockScale = 1f)
        {
            if (waterMat == null || waterConfig == null) return;
            float waveAmp = waterConfig.WaveAmplitude * blockScale;
            if (waterMat.HasProperty("_WaveAmplitude"))
                waterMat.SetFloat("_WaveAmplitude", waveAmp);
            if (waterMat.HasProperty("_WaveFrequency"))
                waterMat.SetFloat("_WaveFrequency", waterConfig.WaveFrequency);
            if (waterMat.HasProperty("_WaveSpeed"))
                waterMat.SetFloat("_WaveSpeed", waterConfig.WaveSpeed);
            if (waterMat.HasProperty("_RefractionStrength"))
                waterMat.SetFloat("_RefractionStrength", waterConfig.RefractionStrength);
            if (waterMat.HasProperty("_RefractionEnabled"))
                waterMat.SetFloat("_RefractionEnabled", waterConfig.RefractionEnabled ? 1f : 0f);
        }

        public void BuildAllChunks()
        {
            for (int cx = 0; cx < ChunkCountX; cx++)
            {
                for (int cy = 0; cy < ChunkCountY; cy++)
                {
                    for (int cz = 0; cz < ChunkCountZ; cz++)
                    {
                        BuildChunk(cx, cy, cz);
                    }
                }
            }
        }

        public void InvalidateChunkAt(int x, int y, int z)
        {
            int cx = x / ChunkMeshBuilder.ChunkSize;
            int cy = y / ChunkMeshBuilder.ChunkSize;
            int cz = z / ChunkMeshBuilder.ChunkSize;
            if (cx < 0 || cx >= ChunkCountX || cy < 0 || cy >= ChunkCountY || cz < 0 || cz >= ChunkCountZ)
                return;
            var key = (cx, cy, cz);
            if (_chunks.ContainsKey(key))
                _dirtyChunks.Add(key);
        }

        public void InvalidateAll()
        {
            for (int cx = 0; cx < ChunkCountX; cx++)
            {
                for (int cy = 0; cy < ChunkCountY; cy++)
                {
                    for (int cz = 0; cz < ChunkCountZ; cz++)
                    {
                        _dirtyChunks.Add((cx, cy, cz));
                    }
                }
            }
        }

        public void RebuildDirtyChunks()
        {
            foreach (var key in _dirtyChunks)
                _dirtyQueue.Enqueue(key);
            _dirtyChunks.Clear();

            int count = 0;
            while (_dirtyQueue.Count > 0 && count < RebuildsPerFrame)
            {
                var (cx, cy, cz) = _dirtyQueue.Dequeue();
                BuildChunk(cx, cy, cz);
                count++;
            }
        }

        private void BuildChunk(int cx, int cy, int cz)
        {
            var key = (cx, cy, cz);
            if (!_chunks.TryGetValue(key, out var renderer))
            {
                var go = new GameObject($"Chunk_{cx}_{cy}_{cz}");
                go.transform.SetParent(_parent);
                renderer = go.AddComponent<ChunkRenderer>();
                renderer.Initialize(_materials);
                renderer.SetPosition(cx, cy, cz, _worldScale.BlockScale);
                _chunks[key] = renderer;
            }

            var meshes = ChunkMeshBuilder.Build(_grid, cx, cy, cz, _worldScale.BlockScale, _terrainConfig, _waterConfig, _mountainMaterial);
            renderer.SetMeshes(meshes);
        }
    }
}
