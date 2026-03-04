using UnityEngine;

namespace Voxel
{
    public enum ActorBehaviorKind
    {
        Woodchuck,
        WheatFarm,
        Carrier
    }

    [CreateAssetMenu(fileName = "ActorDefinition", menuName = "Voxel/Actor Definition")]
    public class ActorDefinition : ScriptableObject
    {
        [Tooltip("Display name (e.g. Woodchuck)")]
        public string Name;

        [Tooltip("Prefab to instantiate (e.g. ActorPlaceholder)")]
        public GameObject Prefab;

        [Tooltip("Behavior component to add at runtime.")]
        [SerializeField] private ActorBehaviorKind behaviorKind = ActorBehaviorKind.Woodchuck;

        [Tooltip("Optional. Override behavior type by assembly-qualified name (e.g. Voxel.CarrierActorBehavior, Assembly-CSharp). Leave empty to use BehaviorKind.")]
        [SerializeField] private string behaviorTypeName;

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

        [Tooltip("When Idle, wait this many seconds before checking for targets again. Higher values reduce CPU load.")]
        [Min(0.1f)]
        public float IdleCheckCooldownSeconds = 0.5f;

        public ActorBehaviorKind BehaviorKind => behaviorKind;

        /// <summary>When non-empty, used instead of BehaviorKind to resolve the behavior Type. Enables adding new behaviors without code changes.</summary>
        public string BehaviorTypeName => behaviorTypeName;
    }
}
