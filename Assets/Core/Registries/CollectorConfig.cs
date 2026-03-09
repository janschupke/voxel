using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "CollectorConfig", menuName = "Voxel/Actor/Collector Config")]
    public class CollectorConfig : ActorCategoryConfig
    {
        [Tooltip("Max units of one item type to carry per trip")]
        [Min(1)]
        public int CapacityPerTrip = 1;

        [Tooltip("When true, only pick non-final items (intermediate products)")]
        public bool OnlyNonFinalItems = true;
    }
}
