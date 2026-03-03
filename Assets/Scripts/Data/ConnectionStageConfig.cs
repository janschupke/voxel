using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ConnectionStageConfig", menuName = "Voxel/Connection Stage Config")]
    public class ConnectionStageConfig : ScriptableObject
    {
        [SerializeField] [Min(0)] [Tooltip("Y-level of sea bed")]
        private int seaBedHeight = 0;

        [SerializeField] [Min(1)] [Tooltip("How quickly terrain descends from island edges (higher = gentler slope)")]
        private float falloffDistance = 15f;

        private byte _blockType = Voxel.Core.BlockType.Ground;

        public int SeaBedHeight => seaBedHeight;
        public float FalloffDistance => falloffDistance;
        public byte BlockType => _blockType;
    }
}
