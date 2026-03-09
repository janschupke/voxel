using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "CritterSpawnerConfig", menuName = "Voxel/Actor/Critter Spawner Config")]
    public class CritterSpawnerConfig : ScriptableObject
    {
        [Tooltip("Actor definition for spawned critters")]
        public ActorDefinition CritterDefinition;

        [Tooltip("Number of critters to spawn per spawner object")]
        [Min(1)]
        public int SpawnCount = 1;

        [Tooltip("Optional. Override critter behavior config (wander radius, idle duration). Uses definition's CategoryConfig if null.")]
        public CritterConfig BehaviorConfigOverride;
    }
}
