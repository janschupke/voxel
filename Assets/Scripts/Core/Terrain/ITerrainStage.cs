namespace Voxel.Core
{
    public interface ITerrainStage
    {
        void Execute(TerrainPipelineContext ctx);
    }
}
