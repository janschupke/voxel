using System;

namespace Voxel.Pure
{
    public static class BlockType
    {
        public const byte Air = 0;
        public const byte Ground = 1;
        public const byte Stone = 2;
    }

    public class VoxelGrid
    {
        public int Width { get; }
        public int Depth { get; }
        public int Height { get; }

        private readonly byte[] _blocks;

        public VoxelGrid(int width = 1000, int depth = 1000, int height = 50)
        {
            Width = width;
            Depth = depth;
            Height = height;
            _blocks = new byte[Width * Depth * Height];
        }

        private int Index(int x, int y, int z)
        {
            return x + z * Width + y * Width * Depth;
        }

        public byte GetBlock(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return BlockType.Air;
            return _blocks[Index(x, y, z)];
        }

        public void SetBlock(int x, int y, int z, byte blockType)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return;
            _blocks[Index(x, y, z)] = blockType;
        }

        public bool IsSolid(int x, int y, int z)
        {
            return GetBlock(x, y, z) != BlockType.Air;
        }

        public byte[] GetBlocksCopy()
        {
            return (byte[])_blocks.Clone();
        }

        public void LoadBlocks(byte[] blocks)
        {
            if (blocks == null || blocks.Length != _blocks.Length)
                throw new ArgumentException($"Block array size must be {_blocks.Length}, got {blocks?.Length ?? 0}");
            Array.Copy(blocks, _blocks, _blocks.Length);
        }
    }
}
