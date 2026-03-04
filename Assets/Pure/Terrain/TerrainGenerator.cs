namespace Voxel.Pure
{
    public class TerrainGenerator
    {
        private readonly VoxelGrid _grid;
        private readonly FractalNoise _noise;
        private readonly float _heightScale;
        private readonly float _heightOffset;
        private readonly byte _blockType;

        public TerrainGenerator(VoxelGrid grid, FractalNoise noise, float heightScale = 1f, float heightOffset = 0f, byte blockType = BlockType.Ground)
        {
            _grid = grid;
            _noise = noise;
            _heightScale = heightScale;
            _heightOffset = heightOffset;
            _blockType = blockType;
        }

        public void Generate()
        {
            for (int z = 0; z < _grid.Depth; z++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    float sample = _noise.Sample(x, z);
                    float heightNorm = sample * _heightScale + _heightOffset;
                    int height = (int)(heightNorm * (_grid.Height - 1));
                    if (height < 0) height = 0;
                    if (height >= _grid.Height) height = _grid.Height - 1;

                    for (int y = 0; y <= height; y++)
                        _grid.SetBlock(x, y, z, _blockType);
                }
            }
        }
    }
}
