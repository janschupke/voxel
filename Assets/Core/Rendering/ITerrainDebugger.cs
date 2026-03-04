using Voxel.Pure;

namespace Voxel.Rendering
{
    /// <summary>
    /// Optional debug callback for terrain initialization. Implement on a MonoBehaviour
    /// attached to the same GameObject as VoxelGridRenderer (e.g. TerrainMaterialDebugger).
    /// </summary>
    public interface ITerrainDebugger
    {
        void OnTerrainInitialized(VoxelGrid grid, ChunkManager chunkManager, TerrainMaterialConfig config);
    }
}
