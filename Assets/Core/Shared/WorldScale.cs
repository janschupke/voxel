using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Reusable scaling utility for the voxel world. Converts between block (voxel) coordinates
    /// and Unity world space. Convention: 1 block = BlockScale world units (default 1 = 1 unit per block).
    /// </summary>
    public readonly struct WorldScale
    {
        /// <summary>World units per block. 1 = one Unity unit per voxel.</summary>
        public float BlockScale { get; }

        public WorldScale(float blockScale)
        {
            BlockScale = blockScale > 0f ? blockScale : 1f;
        }

        /// <summary>Convert block coordinates to world position.</summary>
        public Vector3 BlockToWorld(int x, int y, int z) =>
            new Vector3(x * BlockScale, y * BlockScale, z * BlockScale);

        /// <summary>Convert block coordinates (float) to world position.</summary>
        public Vector3 BlockToWorld(float x, float y, float z) =>
            new Vector3(x * BlockScale, y * BlockScale, z * BlockScale);

        /// <summary>Convert world position to block coordinates (floor).</summary>
        public (int x, int y, int z) WorldToBlock(Vector3 world) =>
            (Mathf.FloorToInt(world.x / BlockScale),
             Mathf.FloorToInt(world.y / BlockScale),
             Mathf.FloorToInt(world.z / BlockScale));

        /// <summary>Scale factor to make a prefab of given height (in its local units) appear 1 block tall.</summary>
        public float ScaleForBlockSizedPrefab(float prefabHeightInUnits) =>
            prefabHeightInUnits > 0f ? BlockScale / prefabHeightInUnits : BlockScale;

        /// <summary>Uniform scale vector for a prefab that should be 1 block tall (prefab height in local units).</summary>
        public Vector3 ScaleVectorForBlockSizedPrefab(float prefabHeightInUnits) =>
            Vector3.one * ScaleForBlockSizedPrefab(prefabHeightInUnits);
    }
}
