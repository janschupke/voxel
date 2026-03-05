using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Wheat Farm actor: gathers wheat in range, paths to it, works, returns home, produces wheat.
    /// </summary>
    public class WheatFarmActorBehavior : GathererActorBehavior
    {
        protected override string TargetEntryName => "Wheat";
        protected override Item ProducedItem => Item.Wheat;
        protected override string GathererName => "WheatFarm";
    }
}
