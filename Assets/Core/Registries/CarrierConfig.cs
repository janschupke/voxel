using UnityEngine;

namespace Voxel
{
    [CreateAssetMenu(fileName = "CarrierConfig", menuName = "Voxel/Actor/Carrier Config")]
    public class CarrierConfig : ActorCategoryConfig
    {
        [Tooltip("Max units of one item type to carry per trip")]
        [Min(1)]
        public int CapacityPerTrip = 1;

        [Tooltip("When true, prioritize final items when picking")]
        public bool PrioritizeFinalItems = true;

        [Tooltip("When true, take non-final items when building inventory is full (overflow)")]
        public bool TakeNonFinalWhenBuildingFull = true;
    }
}
