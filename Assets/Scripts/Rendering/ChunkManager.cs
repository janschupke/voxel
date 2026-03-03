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
        private readonly Dictionary<(int, int, int), ChunkRenderer> _chunks = new();
        private readonly HashSet<(int, int, int)> _dirtyChunks = new();
        private readonly Queue<(int, int, int)> _dirtyQueue = new();
        private const int RebuildsPerFrame = 8;

        public int ChunkCountX => (_grid.Width + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountY => (_grid.Height + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountZ => (_grid.Depth + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;

        private readonly float _voxelScale;

        public ChunkManager(VoxelGrid grid, Transform parent, Material material, float voxelScale = 1f,
            TerrainMaterialConfig terrainConfig = null)
        {
            _grid = grid;
            _parent = parent;
            _terrainConfig = terrainConfig;
            _voxelScale = voxelScale;
            _materials = ResolveMaterials(material, terrainConfig);
        }

        private static Material[] ResolveMaterials(Material fallbackMaterial, TerrainMaterialConfig config)
        {
            if (config == null || config.Bands == null || config.Bands.Count == 0)
                return new[] { fallbackMaterial };

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var baseShader = shader != null ? shader : Shader.Find("Sprites/Default");

            return config.Bands.Select((band, i) =>
            {
                if (band.material != null)
                    return band.material;
                var mat = new Material(baseShader);
                mat.SetColor("_BaseColor", i < DefaultBandColors.Length ? DefaultBandColors[i] : Color.gray);
                return mat;
            }).ToArray();
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
                renderer.SetPosition(cx, cy, cz, _voxelScale);
                _chunks[key] = renderer;
            }

            var mesh = ChunkMeshBuilder.Build(_grid, cx, cy, cz, _voxelScale, _terrainConfig);
            renderer.SetMesh(mesh);
        }
    }
}
