using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    [CreateAssetMenu(fileName = "WorldParameters", menuName = "Voxel/World Parameters")]
    public class WorldParameters : ScriptableObject
    {
        [SerializeField] [Min(16)] private int width = 1000;
        [SerializeField] [Min(16)] private int depth = 1000;
        [SerializeField] [Min(16)] private int height = 50;
        [SerializeField] [Range(0.25f, 16f)] [Tooltip("Unity world units per block. 1 = 1 unit per block (standard). Use WorldScale utility for conversions.")]
        private float blockScale = 1f;
        [SerializeField] [Range(0.1f, 2f)] private float heightScale = 1f;
        [SerializeField] [Range(-0.5f, 0.5f)] private float heightOffset;
        [SerializeField] private byte blockType = Voxel.Pure.BlockType.Ground;

        public int Width => width;
        public int Depth => depth;
        public int Height => height;
        public float BlockScale => blockScale;
        public float HeightScale => heightScale;
        public float HeightOffset => heightOffset;
        public byte BlockType => blockType;
    }
}
