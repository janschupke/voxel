using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "WorldParameters", menuName = "Voxel/World Parameters")]
    public class WorldParameters : ScriptableObject
    {
        [SerializeField] [Min(16)] private int width = 1000;
        [SerializeField] [Min(16)] private int depth = 1000;
        [SerializeField] [Min(16)] private int height = 50;
        [SerializeField] [Range(0.25f, 16f)] [Tooltip("Unity world units per block. 1 = 1 unit per block (standard). No sub-block subdivisions.")]
        private float blockScale = 1f;
        [SerializeField] [Range(1, 64)] [Tooltip("MagicaVoxel voxels per block on one axis. Models use this convention for scaling. Default 16.")]
        private int voxelsPerBlockAxis = 16;

        public int Width => width;
        public int Depth => depth;
        public int Height => height;
        public float BlockScale => blockScale;
        public int VoxelsPerBlockAxis => voxelsPerBlockAxis > 0 ? voxelsPerBlockAxis : 16;
    }
}
