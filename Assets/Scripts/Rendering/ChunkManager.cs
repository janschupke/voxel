using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel.Rendering
{
    public class ChunkManager
    {
        private readonly VoxelGrid _grid;
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly Dictionary<(int, int, int), ChunkRenderer> _chunks = new();
        private readonly HashSet<(int, int, int)> _dirtyChunks = new();
        private readonly Queue<(int, int, int)> _dirtyQueue = new();
        private const int RebuildsPerFrame = 8;

        public int ChunkCountX => (_grid.Width + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountY => (_grid.Height + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;
        public int ChunkCountZ => (_grid.Depth + ChunkMeshBuilder.ChunkSize - 1) / ChunkMeshBuilder.ChunkSize;

        private readonly float _voxelScale;

        public ChunkManager(VoxelGrid grid, Transform parent, Material material, float voxelScale = 1f)
        {
            _grid = grid;
            _parent = parent;
            _material = material;
            _voxelScale = voxelScale;
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
                renderer.Initialize(_material);
                renderer.SetPosition(cx, cy, cz, _voxelScale);
                _chunks[key] = renderer;
            }

            var mesh = ChunkMeshBuilder.Build(_grid, cx, cy, cz, _voxelScale);
            renderer.SetMesh(mesh);
        }
    }
}
