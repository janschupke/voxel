using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    [CreateAssetMenu(fileName = "MountainStageConfig", menuName = "Voxel/Mountain Stage Config")]
    public class MountainStageConfig : ScriptableObject
    {
        [SerializeField] [Min(1)] [Tooltip("Minimum mountain height in blocks. Only place if noise yields at least this many blocks.")]
        private int minMountainHeight = 3;

        [SerializeField] [Min(1)] [Tooltip("Maximum mountain height in blocks where noise = 1.")]
        private int maxMountainHeight = 12;

        [SerializeField] [Min(1)] [Tooltip("Smooth noise over this radius. Larger = gentler slopes.")]
        private int smoothRadius = 3;

        [SerializeField] private float noiseFrequency = 0.02f;
        [SerializeField] [Min(1)] private int octaves = 3;
        [SerializeField] private float lacunarity = 2f;
        [SerializeField] [Range(0f, 1f)] private float persistence = 0.5f;

        public int MinMountainHeight => minMountainHeight;
        public int MaxMountainHeight => maxMountainHeight;
        public int SmoothRadius => smoothRadius;
        public float NoiseFrequency => noiseFrequency;
        public int Octaves => octaves;
        public float Lacunarity => lacunarity;
        public float Persistence => persistence;
    }
}
