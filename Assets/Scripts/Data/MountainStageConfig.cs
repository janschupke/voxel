using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    [CreateAssetMenu(fileName = "MountainStageConfig", menuName = "Voxel/Mountain Stage Config")]
    public class MountainStageConfig : ScriptableObject
    {
        [SerializeField] [Range(0f, 1f)] [Tooltip("Fraction of above-water terrain that gets mountains")]
        private float density = 0.15f;

        [SerializeField] [Min(1)] [Tooltip("Minimum mountain height in blocks")]
        private int minMountainHeight = 2;

        [SerializeField] [Min(1)] [Tooltip("Maximum mountain height in blocks")]
        private int maxMountainHeight = 12;

        [SerializeField] private float noiseFrequency = 0.03f;
        [SerializeField] [Min(1)] private int octaves = 4;
        [SerializeField] private float lacunarity = 2f;
        [SerializeField] [Range(0f, 1f)] private float persistence = 0.5f;

        public float Density => density;
        public int MinMountainHeight => minMountainHeight;
        public int MaxMountainHeight => maxMountainHeight;
        public float NoiseFrequency => noiseFrequency;
        public int Octaves => octaves;
        public float Lacunarity => lacunarity;
        public float Persistence => persistence;
    }
}
