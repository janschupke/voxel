using System;
using UnityEngine;

namespace Voxel
{
    /// <summary>Serializable house placement for world persistence.</summary>
    [Serializable]
    public struct HousePlacementData
    {
        public int BlockX;
        public int BlockY;
        public int BlockZ;
        public float RotationY;

        public HousePlacementData(int blockX, int blockY, int blockZ, float rotationY)
        {
            BlockX = blockX;
            BlockY = blockY;
            BlockZ = blockZ;
            RotationY = rotationY;
        }

        public Vector3 ToWorldPosition(WorldScale worldScale) =>
            worldScale.BlockToWorld(BlockX + 0.5f, BlockY, BlockZ + 0.5f);

        public Quaternion ToRotation() =>
            Quaternion.Euler(0f, RotationY, 0f);
    }
}
