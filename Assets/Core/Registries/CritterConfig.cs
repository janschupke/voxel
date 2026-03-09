using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "CritterConfig", menuName = "Voxel/Actor/Critter Config")]
    public class CritterConfig : ActorCategoryConfig
    {
        [Tooltip("Blocks to wander from spawn point")]
        [Min(1f)]
        public float WanderRadiusBlocks = 5f;

        [Tooltip("Seconds to idle before picking next wander target")]
        [Min(0.1f)]
        public float IdleDurationSeconds = 2f;
    }
}
