using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "NoiseParameters", menuName = "Voxel/Noise Parameters")]
    public class NoiseParameters : ScriptableObject
    {
        [SerializeField] private int seed = 12345;
        [SerializeField] [Range(0.001f, 0.2f)] private float frequency = 0.04f;
        [SerializeField] [Min(1)] private int octaves = 5;
        [SerializeField] [Range(1f, 4f)] private float lacunarity = 2f;
        [SerializeField] [Range(0f, 1f)] private float persistence = 0.5f;

        public int Seed => seed;
        public float Frequency => frequency;
        public int Octaves => octaves;
        public float Lacunarity => lacunarity;
        public float Persistence => persistence;
    }
}
