using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    [CreateAssetMenu(fileName = "GathererConfig", menuName = "Voxel/Actor/Gatherer Config")]
    public class GathererConfig : ActorCategoryConfig
    {
        [Tooltip("Placed object entries to gather from (e.g. Tree, Wheat Field)")]
        public PlacedObjectEntry[] TargetEntries = System.Array.Empty<PlacedObjectEntry>();

        [Tooltip("Item to add to building inventory when work completes")]
        public Item ProducedItem = Item.Wood;
    }
}
