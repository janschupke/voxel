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

        [Tooltip("Category-specific config (GathererConfig, CollectorConfig, etc.). Required. Behavior type is inferred from config.")]
        [SerializeField] private ActorCategoryConfig categoryConfig;

        [Tooltip("Optional. Override behavior type by assembly-qualified name. Leave empty to use Category.")]
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

        /// <summary>Category-specific config. Required. Behavior type is inferred from config type.</summary>
        public ActorCategoryConfig CategoryConfig => categoryConfig;

        /// <summary>When non-empty, used instead of Category to resolve the behavior Type.</summary>
        public string BehaviorTypeName => behaviorTypeName;
    }
}
