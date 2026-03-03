using System;

namespace Voxel.Core
{
    /// <summary>
    /// 2D height buffer for terrain pipeline stages. Width x Depth, stores height per (x,z) column.
    /// </summary>
    public class HeightBuffer
    {
        public int Width { get; }
        public int Depth { get; }

        private readonly float[,] _heights;

        public HeightBuffer(int width, int depth)
        {
            Width = width;
            Depth = depth;
            _heights = new float[width, depth];
        }

        public float Get(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Depth)
                return 0f;
            return _heights[x, z];
        }

        public void Set(int x, int z, float height)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Depth)
                return;
            _heights[x, z] = height;
        }

        public void Clear()
        {
            Array.Clear(_heights, 0, _heights.Length);
        }
    }
}
