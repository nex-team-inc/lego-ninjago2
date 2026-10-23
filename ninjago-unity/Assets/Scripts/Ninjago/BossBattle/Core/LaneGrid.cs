#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Lane and sky-grid geometry, the same as RunnerWorld's CellCenter/Overlaps. Masks are 9-bit over a 3x3 grid:
    /// bit = row * 3 + column, column 0 = left, row 0 = low; the road only uses the low row (bit = column).
    /// </summary>
    public static class LaneGrid
    {
        public const int Columns = ObstaclePatterns.Columns;
        public const int Rows = ObstaclePatterns.Rows;

        public static bool IsBlocked(int mask, int column, int row) => ObstaclePatterns.IsBlocked(mask, column, row);

        /// <summary>Centre of a cell: x = (column - 1) × cellWidth; road y = 0, sky y = centre + (row - 1) × cellHeight.</summary>
        public static Vector2 CellCenter(ChaseConfig.VehicleSettings settings, int column, int row, bool isSky)
        {
            var x = (column - 1) * settings.cellWidth;
            var y = isSky ? settings.centerHeight + (row - 1) * settings.cellHeight : 0f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Whether the vehicle box at position overlaps a blocked cell (each cell shrunk to cellFill). On the road a blocked
        /// lane always hits whatever the height (the car never leaves the road).
        /// </summary>
        public static bool Overlaps(ChaseConfig.VehicleSettings settings, Vector2 position, int mask, bool isSky, float cellFill)
        {
            var rowCount = isSky ? Rows : 1;
            var halfCell = new Vector2(settings.cellWidth, settings.cellHeight) * (cellFill * 0.5f);
            for (var row = 0; row < rowCount; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (!IsBlocked(mask, column, row)) continue;
                    var center = CellCenter(settings, column, row, isSky);
                    var overlapX = Mathf.Abs(position.x - center.x) < halfCell.x + settings.vehicleHalfExtents.x;
                    var overlapY = !isSky || Mathf.Abs(position.y - center.y) < halfCell.y + settings.vehicleHalfExtents.y;
                    if (overlapX && overlapY) return true;
                }
            }

            return false;
        }

        /// <summary>The column (0..2) nearest to x.</summary>
        public static int ColumnOf(ChaseConfig.VehicleSettings settings, float x)
        {
            return Mathf.Clamp(Mathf.RoundToInt(x / Mathf.Max(0.01f, settings.cellWidth)) + 1, 0, Columns - 1);
        }

        /// <summary>The sky row (0..2) nearest to y.</summary>
        public static int RowOf(ChaseConfig.VehicleSettings settings, float y)
        {
            return Mathf.Clamp(Mathf.RoundToInt((y - settings.centerHeight) / Mathf.Max(0.01f, settings.cellHeight)) + 1, 0, Rows - 1);
        }

        /// <summary>Open cells of a mask (road: of the low row only).</summary>
        public static int FreeCells(int mask, bool isSky)
        {
            var free = 0;
            var rowCount = isSky ? Rows : 1;
            for (var row = 0; row < rowCount; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (!IsBlocked(mask, column, row)) free++;
                }
            }

            return free;
        }
    }
}
