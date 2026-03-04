namespace Voxel.Pure
{
    /// <summary>
    /// Pure block queries for voxel grid. No Unity dependencies.
    /// </summary>
    public static class BlockQueries
    {
        public static int GetTopSolidY(VoxelGrid grid, int x, int z, int gridHeight)
        {
            for (int y = gridHeight - 1; y >= 0; y--)
            {
                if (grid.IsSolid(x, y, z))
                    return y;
            }
            return -1;
        }
    }
}
