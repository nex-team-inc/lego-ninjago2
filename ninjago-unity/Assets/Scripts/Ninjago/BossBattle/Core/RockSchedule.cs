#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Overlap rule (GDD §4.5 beat #11, Pillar 5): a rock dropped during a slam lands midway between two brick-row
    /// arrivals, at least minGap seconds from every arrival, so a dodge never runs into a brick. Rocks are only ever
    /// delayed (the runner stretches the reticle's follow time), never hurried.
    /// </summary>
    public static class RockSchedule
    {
        /// <param name="desired">Earliest impact time the rock can make (same clock as the arrivals).</param>
        /// <param name="rowArrivals">Times the brick rows reach the car, ascending.</param>
        /// <returns>The earliest valid impact at or after desired: desired itself before the first row, the midpoint of
        /// the first gap wide enough (or desired when it is later and still valid), else minGap after the last row.</returns>
        public static float PlaceImpact(float desired, IReadOnlyList<float> rowArrivals, float minGap)
        {
            var count = rowArrivals.Count;
            if (count == 0) return desired;
            if (desired <= rowArrivals[0] - minGap) return desired;
            for (var i = 0; i + 1 < count; i++)
            {
                var earliest = rowArrivals[i] + minGap;
                var latest = rowArrivals[i + 1] - minGap;
                if (latest < earliest || latest < desired) continue;
                var midpoint = (rowArrivals[i] + rowArrivals[i + 1]) * 0.5f;
                return Mathf.Max(desired, midpoint);
            }

            return Mathf.Max(desired, rowArrivals[count - 1] + minGap);
        }

        /// <summary>True when t is at least minGap from every arrival.</summary>
        public static bool IsClear(float t, IReadOnlyList<float> rowArrivals, float minGap)
        {
            for (var i = 0; i < rowArrivals.Count; i++)
            {
                if (Mathf.Abs(t - rowArrivals[i]) < minGap) return false;
            }

            return true;
        }
    }
}
