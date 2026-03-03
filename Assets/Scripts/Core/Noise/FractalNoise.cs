namespace Voxel.Core
{
    public class FractalNoise
    {
        private readonly PerlinNoise _perlin;
        private readonly float _frequency;
        private readonly int _octaves;
        private readonly float _lacunarity;
        private readonly float _persistence;

        public FractalNoise(float frequency, int octaves, float lacunarity, float persistence, int seed)
        {
            _perlin = new PerlinNoise(seed);
            _frequency = frequency;
            _octaves = octaves;
            _lacunarity = lacunarity;
            _persistence = persistence;
        }

        public float Sample(float x, float y)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = _frequency;
            float maxValue = 0f;

            for (int i = 0; i < _octaves; i++)
            {
                value += amplitude * _perlin.Sample(x * frequency, y * frequency);
                maxValue += amplitude;
                amplitude *= _persistence;
                frequency *= _lacunarity;
            }

            if (maxValue <= 0) return 0f;
            float normalized = value / maxValue;
            return (normalized + 1f) * 0.5f;
        }
    }
}
