#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The power-up volley table (GDD §4.4): pickup i (0-based) fires RocketsFor(i) rockets of DamagePerRocket(i) each.
    /// Phase 1 HP is the table's sum, so the last pickup always empties the bar. Indices past the list reuse its last entry.
    /// </summary>
    public static class RocketVolleys
    {
        public static int RocketsFor(IReadOnlyList<RocketVolley> volleys, int pickupIndex)
        {
            return volleys.Count == 0 ? 0 : Mathf.Max(0, Entry(volleys, pickupIndex).rockets);
        }

        public static float DamagePerRocket(IReadOnlyList<RocketVolley> volleys, int pickupIndex, float baseDamage)
        {
            return volleys.Count == 0 ? 0f : baseDamage * Entry(volleys, pickupIndex).damageMultiplier;
        }

        /// <summary>Damage of the first count volleys together (count = pickups to phase 2: the phase 1 HP).</summary>
        public static float TotalHp(IReadOnlyList<RocketVolley> volleys, int count, float baseDamage)
        {
            var total = 0f;
            for (var i = 0; i < count; i++) total += RocketsFor(volleys, i) * DamagePerRocket(volleys, i, baseDamage);
            return total;
        }

        /// <summary>HP left after this many pickups, with phase 1 HP = the whole list's sum.</summary>
        public static float HpAfter(IReadOnlyList<RocketVolley> volleys, int pickups, float baseDamage)
        {
            return HpAfter(volleys, pickups, baseDamage, volleys.Count);
        }

        /// <summary>HP left after this many pickups, with phase 1 HP = the sum of the first totalPickups volleys.</summary>
        public static float HpAfter(IReadOnlyList<RocketVolley> volleys, int pickups, float baseDamage, int totalPickups)
        {
            var total = TotalHp(volleys, totalPickups, baseDamage);
            var dealt = TotalHp(volleys, Mathf.Clamp(pickups, 0, totalPickups), baseDamage);
            return Mathf.Max(0f, total - dealt);
        }

        static RocketVolley Entry(IReadOnlyList<RocketVolley> volleys, int pickupIndex)
        {
            return volleys[Mathf.Clamp(pickupIndex, 0, volleys.Count - 1)];
        }
    }
}
