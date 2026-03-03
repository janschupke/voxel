using System.Collections.Generic;

namespace Voxel.Core
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
