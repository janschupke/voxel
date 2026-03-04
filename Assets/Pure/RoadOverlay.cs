using System;
using System.Collections.Generic;

namespace Voxel
{
    /// <summary>
    /// Lightweight storage for road block positions. A road at (x, y, z) means the road texture
    /// is on the top face of the solid block at (x, y-1, z) — y is the surface level above the block.
    /// </summary>
    public class RoadOverlay
    {
        private readonly HashSet<(int x, int y, int z)> _blocks = new();

        public void Add(int x, int y, int z)
        {
            _blocks.Add((x, y, z));
        }

        public void Remove(int x, int y, int z)
        {
            _blocks.Remove((x, y, z));
        }

        public bool Contains(int x, int y, int z)
        {
            return _blocks.Contains((x, y, z));
        }

        public IReadOnlyCollection<(int x, int y, int z)> GetAllBlocks()
        {
            return _blocks;
        }

        public void Clear()
        {
            _blocks.Clear();
        }
    }
}
