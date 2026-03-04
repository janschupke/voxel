using System.Collections.Generic;

namespace Voxel.Pure
{
    public static class TerrainPipeline
    {
        public static void Execute(IReadOnlyList<ITerrainStage> stages, TerrainPipelineContext context)
        {
            foreach (var stage in stages)
            {
                stage.Execute(context);
            }
        }
    }
}
