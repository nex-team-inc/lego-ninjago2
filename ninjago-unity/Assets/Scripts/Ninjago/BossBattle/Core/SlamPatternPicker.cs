#nullable enable

using System.Collections.Generic;

namespace Nex.Ninjago
{
    /// <summary>
    /// Brick-row masks of the ground slams (GDD §4.3), seeded. Rows are 2-lane barriers (bit = column, 1 = brick):
    /// 110 open left, 101 open centre, 011 open right. The first slam of the run uses fixed masks (101, then 110 or its
    /// mirror 011); later rows never repeat the previous row, and under maxLaneJumpBelow seconds of spacing the open lane
    /// moves by at most one lane (no 011 &lt;-&gt; 110 back to back).
    /// </summary>
    public sealed class SlamPatternPicker
    {
        public const int OpenLeft = 0b110;
        public const int OpenCenter = 0b101;
        public const int OpenRight = 0b011;

        static readonly int[] masks = { OpenLeft, OpenCenter, OpenRight };
        static readonly int[] defaultFirstMasks = { OpenCenter, OpenLeft };

        readonly System.Random random;
        readonly IReadOnlyList<int> firstMasks;
        readonly float maxLaneJumpBelow;
        readonly int[] candidates = new int[3];
        int last = -1;

        public SlamPatternPicker(int seed) : this(seed, null, 1.8f)
        {
        }

        /// <param name="firstMasks">SlamSettings.firstMasks (null = 101, 110).</param>
        /// <param name="maxLaneJumpBelow">SlamSettings.maxLaneJumpBelow.</param>
        public SlamPatternPicker(int seed, IReadOnlyList<int>? firstMasks, float maxLaneJumpBelow)
        {
            random = new System.Random(seed);
            this.firstMasks = firstMasks is { Count: > 0 } ? firstMasks : defaultFirstMasks;
            this.maxLaneJumpBelow = maxLaneJumpBelow;
        }

        #region Public API

        /// <param name="spacingSeconds">Seconds between this row and the previous row of the same slam.</param>
        /// <param name="slamIndexInRun">0 for the first slam of the run (fixed masks).</param>
        /// <param name="rowIndex">Row of this slam, 0-based (row 0 has no back-to-back rule).</param>
        public int Next(float spacingSeconds, int slamIndexInRun, int rowIndex)
        {
            int mask;
            if (slamIndexInRun == 0 && rowIndex < firstMasks.Count)
            {
                mask = firstMasks[rowIndex];
                if (rowIndex > 0 && random.Next(2) == 1) mask = Mirror(mask);
            }
            else
            {
                var count = 0;
                foreach (var candidate in masks)
                {
                    if (rowIndex > 0 && candidate == last) continue;
                    if (rowIndex > 0 && last >= 0 && spacingSeconds < maxLaneJumpBelow && LaneJump(last, candidate) > 1) continue;
                    candidates[count++] = candidate;
                }

                mask = count == 0 ? OpenCenter : candidates[random.Next(count)];
            }

            last = mask;
            return mask;
        }

        /// <summary>The open lane (0 left, 1 centre, 2 right) of a road mask, or -1 when none is open.</summary>
        public static int OpenLane(int mask)
        {
            for (var column = 0; column < 3; column++)
            {
                if ((mask & (1 << column)) == 0) return column;
            }

            return -1;
        }

        /// <summary>How many lanes the open lane moves between two masks.</summary>
        public static int LaneJump(int from, int to) => System.Math.Abs(OpenLane(from) - OpenLane(to));

        /// <summary>Left-right mirror of a road mask (110 &lt;-&gt; 011; 101 stays).</summary>
        public static int Mirror(int mask) => ((mask & 1) << 2) | (mask & 2) | ((mask & 4) >> 2);

        #endregion
    }
}
