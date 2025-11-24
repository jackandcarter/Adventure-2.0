using System;
using System.Collections.Generic;
using System.Numerics;

namespace Adventure.Server.Simulation
{
    public class RoomLayout
    {
        private readonly HashSet<(int, int)> blockedCells;
        private readonly Vector2 size;
        private readonly float cellSize;

        public RoomLayout(Vector2 size, float cellSize, IEnumerable<(int x, int y)> blocked)
        {
            this.size = size;
            this.cellSize = cellSize;
            blockedCells = new HashSet<(int, int)>(blocked);
        }

        public bool IsInsideBounds(Vector3 position)
        {
            return position.X >= 0 && position.Z >= 0 && position.X <= size.X && position.Z <= size.Y;
        }

        public bool IsWalkable(Vector3 position)
        {
            if (!IsInsideBounds(position))
            {
                return false;
            }

            var cell = ToCell(position);
            return !blockedCells.Contains(cell);
        }

        public bool TryResolveMovement(Vector3 start, Vector3 desired, out Vector3 resolved)
        {
            if (!IsWalkable(desired))
            {
                resolved = start;
                return false;
            }

            resolved = desired;
            return true;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            if (!IsInsideBounds(from) || !IsInsideBounds(to))
            {
                return false;
            }

            var steps = (int)Math.Ceiling(Vector3.Distance(from, to) / cellSize);
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var point = Vector3.Lerp(from, to, t);
                if (!IsWalkable(point))
                {
                    return false;
                }
            }

            return true;
        }

        private (int, int) ToCell(Vector3 position)
        {
            var x = (int)Math.Floor(position.X / cellSize);
            var y = (int)Math.Floor(position.Z / cellSize);
            return (x, y);
        }
    }
}
