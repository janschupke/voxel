using System;
using System.Collections.Generic;

namespace Voxel.Pure
{
    /// <summary>Shape of the operational range: square (Chebyshev) or grid circle (Euclidean).</summary>
    public enum OperationalRangeType
    {
        Square,
        GridCircle
    }

    /// <summary>
    /// Single source of truth for operational range in grid cells.
    /// Square: Chebyshev distance (axis-aligned). GridCircle: Euclidean distance (cell centers).
    /// </summary>
    public static class OperationalRange
    {
        /// <summary>Returns true if cell (cellX, cellZ) is within rangeCells of center (centerX, centerZ).</summary>
        public static bool IsCellInRange(int centerX, int centerZ, int cellX, int cellZ, int rangeCells, OperationalRangeType type = OperationalRangeType.Square)
        {
            if (rangeCells < 0) return false;
            return type switch
            {
                OperationalRangeType.Square => IsCellInRangeSquare(centerX, centerZ, cellX, cellZ, rangeCells),
                OperationalRangeType.GridCircle => IsCellInRangeGridCircle(centerX, centerZ, cellX, cellZ, rangeCells),
                _ => IsCellInRangeSquare(centerX, centerZ, cellX, cellZ, rangeCells)
            };
        }

        private static bool IsCellInRangeSquare(int centerX, int centerZ, int cellX, int cellZ, int rangeCells)
        {
            int dx = cellX - centerX;
            int dz = cellZ - centerZ;
            return Abs(dx) <= rangeCells && Abs(dz) <= rangeCells;
        }

        private static bool IsCellInRangeGridCircle(int centerX, int centerZ, int cellX, int cellZ, int rangeCells)
        {
            int dx = cellX - centerX;
            int dz = cellZ - centerZ;
            int radiusSq = rangeCells * rangeCells;
            return dx * dx + dz * dz <= radiusSq;
        }

        /// <summary>
        /// Returns outline vertices in block coordinates. Square: 4 corners. GridCircle: traced boundary.
        /// </summary>
        public static void GetOutlineVertices(int centerX, int centerZ, int rangeCells, OperationalRangeType type,
            int gridWidth, int gridDepth, List<(int x, int z)> outVertices)
        {
            outVertices.Clear();
            if (rangeCells < 0) return;

            if (type == OperationalRangeType.Square)
            {
                GetOutlineSquare(centerX, centerZ, rangeCells, outVertices);
            }
            else
            {
                GetOutlineGridCircle(centerX, centerZ, rangeCells, gridWidth, gridDepth, outVertices);
            }
        }

        private static void GetOutlineSquare(int centerX, int centerZ, int rangeCells, List<(int x, int z)> outCorners)
        {
            int minX = centerX - rangeCells;
            int maxX = centerX + rangeCells + 1;
            int minZ = centerZ - rangeCells;
            int maxZ = centerZ + rangeCells + 1;
            outCorners.Add((minX, minZ));
            outCorners.Add((maxX, minZ));
            outCorners.Add((maxX, maxZ));
            outCorners.Add((minX, maxZ));
        }

        private static void GetOutlineGridCircle(int centerX, int centerZ, int rangeCells, int gridWidth, int gridDepth, List<(int x, int z)> outVertices)
        {
            int radiusSq = rangeCells * rangeCells;

            bool IsInRange(int ix, int iz)
            {
                if (ix < 0 || ix >= gridWidth || iz < 0 || iz >= gridDepth) return false;
                int dx = ix - centerX;
                int dz = iz - centerZ;
                return dx * dx + dz * dz <= radiusSq;
            }

            int radiusInt = rangeCells + 1;
            int minIx = Math.Max(0, centerX - radiusInt);
            int maxIx = Math.Min(gridWidth - 1, centerX + radiusInt);
            int minIz = Math.Max(0, centerZ - radiusInt);
            int maxIz = Math.Min(gridDepth - 1, centerZ + radiusInt);

            var edges = new List<((int x, int z) from, (int x, int z) to)>();

            for (int iz = minIz; iz <= maxIz; iz++)
            {
                for (int ix = minIx; ix <= maxIx; ix++)
                {
                    if (!IsInRange(ix, iz)) continue;

                    if (!IsInRange(ix - 1, iz)) edges.Add(((ix, iz), (ix, iz + 1)));
                    if (!IsInRange(ix + 1, iz)) edges.Add(((ix + 1, iz), (ix + 1, iz + 1)));
                    if (!IsInRange(ix, iz - 1)) edges.Add(((ix, iz), (ix + 1, iz)));
                    if (!IsInRange(ix, iz + 1)) edges.Add(((ix, iz + 1), (ix + 1, iz + 1)));
                }
            }

            if (edges.Count == 0) return;

            TraceContour(edges, outVertices);
        }

        private static void TraceContour(List<((int x, int z) from, (int x, int z) to)> edges, List<(int x, int z)> ordered)
        {
            var adj = new Dictionary<(int x, int z), List<(int x, int z)>>();
            foreach (var (from, to) in edges)
            {
                if (!adj.TryGetValue(from, out var list)) { list = new List<(int x, int z)>(); adj[from] = list; }
                list.Add(to);
                if (!adj.TryGetValue(to, out var listTo)) { listTo = new List<(int x, int z)>(); adj[to] = listTo; }
                listTo.Add(from);
            }

            var start = edges[0].from;
            var prev = start;
            var cur = adj[start][0];

            ordered.Add(start);
            while (true)
            {
                ordered.Add(cur);
                if (cur == start) break;
                var nexts = adj[cur];
                if (nexts.Count < 2) break;
                var next = nexts[0] == prev ? nexts[1] : nexts[0];
                prev = cur;
                cur = next;
            }
        }

        private static int Abs(int v) => v >= 0 ? v : -v;
    }
}
