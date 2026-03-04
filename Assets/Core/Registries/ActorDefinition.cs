using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "ActorDefinition", menuName = "Voxel/Actor Definition")]
    public class ActorDefinition : ScriptableObject
    {
        [Tooltip("Display name (e.g. Woodchuck)")]
        public string Name;

        [Tooltip("Prefab to instantiate (e.g. ActorPlaceholder)")]
        public GameObject Prefab;

        [Tooltip("Pathing mode: Road (road only), Free (shortest land path), Smart (prefer road, any path)")]
        public ActorPathingMode PathingMode = ActorPathingMode.Free;

        [Tooltip("Seconds spent working at target (e.g. at tree)")]
        [Min(0.1f)]
        public float WorkDurationOutside = 3f;

        [Tooltip("Seconds spent working at home building")]
        [Min(0.1f)]
        public float WorkDurationInside = 2f;

        [Tooltip("Blocks per second movement speed")]
        [Min(0.1f)]
        public float MoveSpeed = 2f;

        [Tooltip("When Blocked (trees in range but no path), retry after this many seconds")]
        [Min(0.5f)]
        public float BlockedRetryDelaySeconds = 5f;
    }
}
