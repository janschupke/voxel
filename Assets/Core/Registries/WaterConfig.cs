using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "WaterConfig", menuName = "Voxel/Water Config")]
    public class WaterConfig : ScriptableObject
    {
        [SerializeField] [Tooltip("Toggle water rendering")]
        private bool enabled = true;

        [SerializeField] [Min(0)] [Tooltip("Absolute Y height in voxels; air below this gets water")]
        private int waterLevelY = 15;

        [SerializeField] [Tooltip("Water material (transparent/semi-transparent)")]
        private Material material;

        [SerializeField] [Range(0f, 0.5f)] [Tooltip("Vertex Y displacement per voxel scale (scaled by block scale at runtime)")]
        private float waveAmplitude = 0.15f;

        [SerializeField] [Range(0.01f, 1f)] [Tooltip("Spatial frequency for wave animation (lower = larger waves)")]
        private float waveFrequency = 0.1f;

        [SerializeField] [Range(0.1f, 3f)] [Tooltip("Animation speed for wave effect")]
        private float waveSpeed = 1f;

        public bool Enabled => enabled;
        public int WaterLevelY => waterLevelY;
        public Material Material => material;
        public float WaveAmplitude => waveAmplitude;
        public float WaveFrequency => waveFrequency;
        public float WaveSpeed => waveSpeed;

        /// <summary>
        /// Returns the water level Y, clamped to valid grid bounds.
        /// </summary>
        public int GetWaterLevelY(int gridHeight)
        {
            return Mathf.Clamp(waterLevelY, 0, gridHeight - 1);
        }
    }
}
