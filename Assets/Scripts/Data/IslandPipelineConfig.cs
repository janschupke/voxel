using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;

namespace Voxel
{
    [CreateAssetMenu(fileName = "IslandPipelineConfig", menuName = "Voxel/Island Pipeline Config")]
    public class IslandPipelineConfig : ScriptableObject
    {
        [SerializeField] [Tooltip("Master seed for entire pipeline; all stages derive deterministically from this")]
        private int masterSeed = 12345;

        [SerializeField] private IslandStageConfig islandStageConfig;
        [SerializeField] private ConnectionStageConfig connectionStageConfig;
        [SerializeField] private MountainStageConfig mountainStageConfig;

        public int MasterSeed => masterSeed;

        public List<ITerrainStage> BuildStages()
        {
            var stages = new List<ITerrainStage>();

            if (islandStageConfig != null)
                stages.Add(new IslandPlacementStage(islandStageConfig));

            if (connectionStageConfig != null)
                stages.Add(new ConnectionTerrainStage(connectionStageConfig));

            if (mountainStageConfig != null)
                stages.Add(new MountainStage(mountainStageConfig));

            return stages;
        }
    }
}
