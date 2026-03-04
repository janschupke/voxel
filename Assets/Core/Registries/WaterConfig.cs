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

        [SerializeField] [Tooltip("Only render top face (Minecraft-style)")]
        private bool surfaceOnly = true;

        [SerializeField] [Range(0f, 0.5f)] [Tooltip("Vertex Y displacement per voxel scale (scaled by block scale at runtime)")]
        private float waveAmplitude = 0.15f;

        [SerializeField] [Range(0.01f, 1f)] [Tooltip("Spatial frequency for wave animation (lower = larger waves)")]
        private float waveFrequency = 0.1f;

        [SerializeField] [Range(0.1f, 3f)] [Tooltip("Animation speed for wave effect")]
        private float waveSpeed = 1f;

        [SerializeField] [Range(0f, 0.1f)] [Tooltip("Distortion amount for refraction")]
        private float refractionStrength = 0.02f;

        [SerializeField] [Tooltip("Toggle refraction effect")]
        private bool refractionEnabled = true;

        public bool Enabled => enabled;
        public int WaterLevelY => waterLevelY;
        public Material Material => material;
        public bool SurfaceOnly => surfaceOnly;
        public float WaveAmplitude => waveAmplitude;
        public float WaveFrequency => waveFrequency;
        public float WaveSpeed => waveSpeed;
        public float RefractionStrength => refractionStrength;
        public bool RefractionEnabled => refractionEnabled;

        /// <summary>
        /// Returns the water level Y, clamped to valid grid bounds.
        /// </summary>
        public int GetWaterLevelY(int gridHeight)
        {
            return Mathf.Clamp(waterLevelY, 0, gridHeight - 1);
        }
    }
}
