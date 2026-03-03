using System;
using UnityEngine;

namespace Voxel
{
    /// <summary>Serializable placed object for world persistence. Registry-driven.</summary>
    [Serializable]
    public struct PlacedObjectData
    {
        public string EntryName;
        public int BlockX;
        public int BlockY;
        public int BlockZ;
        public float RotationY;

        public PlacedObjectData(string entryName, int blockX, int blockY, int blockZ, float rotationY)
        {
            EntryName = entryName ?? "";
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
