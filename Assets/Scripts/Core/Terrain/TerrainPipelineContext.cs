using System;
using System.Collections.Generic;

namespace Voxel.Core
{
    public class TerrainPipelineContext
    {
        public HeightBuffer HeightBuffer { get; }
        public VoxelGrid Grid { get; }
        public int WaterLevelY { get; }
        public int MasterSeed { get; }

        private readonly Dictionary<string, int> _stageSeeds = new Dictionary<string, int>();

        public TerrainPipelineContext(HeightBuffer heightBuffer, VoxelGrid grid, int waterLevelY, int masterSeed)
        {
            HeightBuffer = heightBuffer;
            Grid = grid;
            WaterLevelY = waterLevelY;
            MasterSeed = masterSeed;
        }

        /// <summary>
        /// Returns a deterministically derived seed for the given stage. Same master seed + stageId always yields same result.
        /// </summary>
        public int GetSeedForStage(string stageId)
        {
            if (_stageSeeds.TryGetValue(stageId, out int seed))
                return seed;

            int hash = HashString(stageId);
            int derived = unchecked(MasterSeed * 31 + hash);
            _stageSeeds[stageId] = derived;
            return derived;
        }

        private static int HashString(string s)
        {
            int h = 0;
            foreach (char c in s)
                h = unchecked(h * 31 + c);
            return h;
        }
    }
}
