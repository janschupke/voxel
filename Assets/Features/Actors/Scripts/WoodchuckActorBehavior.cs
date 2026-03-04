using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Woodchuck actor: gathers trees in range, paths to them, works, returns home, produces wood.
    /// </summary>
    public class WoodchuckActorBehavior : GathererActorBehavior
    {
        protected override string TargetEntryName => "Tree";
        protected override Item ProducedItem => Item.Wood;
        protected override string GathererName => "Woodchuck";
    }
}
