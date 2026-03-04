using System;

namespace Voxel.Pathfinding
{
    /// <summary>
    /// 2D grid node for pathfinding. Implements IEquatable for use with A*.
    /// </summary>
    public struct GridNode : IEquatable<GridNode>
    {
        public int X;
        public int Z;

        public GridNode(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridNode other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridNode n && Equals(n);
        public override int GetHashCode() => HashCode.Combine(X, Z);
    }
}
