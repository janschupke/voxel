using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Validates block placement. Extracted from ObjectPlacementController.
    /// </summary>
    public static class PlacementValidator
    {
        public static bool IsBlockValidForPlacement(
            WorldBootstrap worldBootstrap,
            int x, int y, int z,
            PlacedObjectEntry entry)
        {
            if (worldBootstrap == null) return false;
            if (worldBootstrap.HasBlockingObjectAtBlock(x, y, z)) return false;
            if (entry != null && !entry.CanReplaceTrees && worldBootstrap.HasEntryAtBlock("Tree", x, y, z))
                return false;
            return true;
        }

        public static System.Func<int, int, int, bool> CreateBlockValidator(WorldBootstrap worldBootstrap)
        {
            return (x, y, z) => worldBootstrap != null && !worldBootstrap.HasBlockingObjectAtBlock(x, y, z);
        }

        /// <summary>
        /// Validator for road line placement when extending through existing roads.
        /// Allows path through blocks that already have roads; still blocks buildings.
        /// </summary>
        public static System.Func<int, int, int, bool> CreateBlockValidatorForRoadExtend(WorldBootstrap worldBootstrap)
        {
            return (x, y, z) => worldBootstrap != null &&
                (!worldBootstrap.HasBlockingObjectAtBlock(x, y, z) || worldBootstrap.HasRoadAt(x, y, z));
        }
    }
}
