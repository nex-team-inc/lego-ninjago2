#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// Gust masks of the devil wind barrages (GDD §6.1), seeded. Masks are 9-bit over the 3x3 sky grid (bit = row × 3 +
    /// column, column 0 = left, row 0 = low; 1 = gust) and every one leaves at least one cell free. The first barrage uses
    /// single-axis masks only (climb, dive, go left, go right); a barrage never repeats the previous row; under
    /// noClimbDiveBelow seconds of spacing there is no climb &lt;-&gt; dive back to back.
    /// </summary>
    public sealed class WindPatternPicker
    {
        public const int Climb = 0b000_111_111;   // low and middle rows blown: climb to the top row
        public const int Dive = 0b111_111_000;    // high and middle rows blown: dive to the low row
        public const int GoRight = 0b011_011_011; // left and centre columns blown
        public const int GoLeft = 0b110_110_110;  // centre and right columns blown
        public const int Ring = 0b111_101_111;    // everything but the middle cell
        public const int LowLeft = 0b001_001_111; // low row and left column blown
        public const int HighRight = 0b111_100_100; // high row and right column blown

        static readonly int[] all = { Climb, Dive, GoRight, GoLeft, Ring, LowLeft, HighRight };

        readonly System.Random random;
        readonly float noClimbDiveBelow;
        readonly int[] candidates = new int[all.Length];
        int last = -1;
        int lastBarrage = -1;

        public WindPatternPicker(int seed) : this(seed, 1.8f)
        {
        }

        /// <param name="noClimbDiveBelow">WindSettings.noClimbDiveBelow.</param>
        public WindPatternPicker(int seed, float noClimbDiveBelow)
        {
            random = new System.Random(seed);
            this.noClimbDiveBelow = noClimbDiveBelow;
        }

        #region Public API

        /// <param name="barrageIndex">Barrage of the run, 0-based (0 = single-axis masks only).</param>
        /// <param name="spacing">Seconds since the previous row of this barrage.</param>
        public int Next(int barrageIndex, float spacing)
        {
            var sameBarrage = barrageIndex == lastBarrage && last >= 0;
            var count = 0;
            foreach (var mask in all)
            {
                if (barrageIndex == 0 && !IsSingleAxis(mask)) continue;
                if (sameBarrage && mask == last) continue;
                if (sameBarrage && spacing < noClimbDiveBelow && IsClimbDiveSwap(last, mask)) continue;
                candidates[count++] = mask;
            }

            var next = count == 0 ? GoLeft : candidates[random.Next(count)];
            last = next;
            lastBarrage = barrageIndex;
            return next;
        }

        /// <summary>Masks that only ask for one axis of movement (whole rows or whole columns).</summary>
        public static bool IsSingleAxis(int mask) => mask is Climb or Dive or GoRight or GoLeft;

        public static bool IsClimbDiveSwap(int from, int to) => (from == Climb && to == Dive) || (from == Dive && to == Climb);

        /// <summary>Open cells of a sky mask.</summary>
        public static int FreeCells(int mask) => LaneGrid.FreeCells(mask, true);

        #endregion
    }
}
