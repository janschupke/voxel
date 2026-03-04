using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pure;

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
        [SerializeField] private TreeScatterConfig treeScatterConfig;

        public int MasterSeed => masterSeed;
        public TreeScatterConfig TreeScatterConfig => treeScatterConfig;
        public MountainStageConfig MountainStageConfig => mountainStageConfig;

        public List<ITerrainStage> BuildStages(Transform treeParent = null, WorldScale worldScale = default)
        {
            var stages = new List<ITerrainStage>();

            if (islandStageConfig != null)
                stages.Add(new IslandPlacementStage(islandStageConfig));

            if (connectionStageConfig != null)
                stages.Add(new ConnectionTerrainStage(connectionStageConfig));

            if (mountainStageConfig != null)
                stages.Add(new MountainStage(mountainStageConfig));

            if (treeScatterConfig != null && treeParent != null)
                stages.Add(new TreeScatterStage(treeScatterConfig, treeParent, worldScale));
            else if (treeScatterConfig != null && treeParent == null)
                GameDebugLogger.LogWarning("[TreeScatter] TreeScatterConfig is set but TreeParent is null - trees will not be placed");

            return stages;
        }
    }
}
