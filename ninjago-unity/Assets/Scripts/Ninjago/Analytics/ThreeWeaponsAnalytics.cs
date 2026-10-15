#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Three Weapons gameplay events over AnalyticsManager.TrackEvent; every event carries the player index and fight (1-based).</summary>
    public static class ThreeWeaponsAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        public static void WeaponChosen(int playerIndex, int fight, WeaponType weapon, float seconds)
        {
            var props = Fight(playerIndex, fight);
            props["weapon"] = weapon.ToString();
            props["seconds"] = Round(seconds);
            Analytics.TrackEvent("weapon_chosen", props);
        }

        /// <param name="hand">0 = left, 1 = right.</param>
        public static void Slash(int playerIndex, int fight, int hand, int count)
        {
            var props = Fight(playerIndex, fight);
            props["hand"] = hand;
            props["count"] = count;
            Analytics.TrackEvent("weapon_slash", props);
        }

        public static void SpinBurst(int playerIndex, int fight, int hand, SpinArea area, int count)
        {
            var props = Fight(playerIndex, fight);
            props["hand"] = hand;
            props["area"] = area.ToString();
            props["count"] = count;
            Analytics.TrackEvent("weapon_spin_burst", props);
        }

        public static void Throw(int playerIndex, int fight, int hand, int count)
        {
            var props = Fight(playerIndex, fight);
            props["hand"] = hand;
            props["count"] = count;
            Analytics.TrackEvent("weapon_throw", props);
        }

        public static void MonsterKilled(int playerIndex, int fight, WeaponType weapon, float seconds, int gestures)
        {
            var props = Fight(playerIndex, fight);
            props["weapon"] = weapon.ToString();
            props["seconds"] = Round(seconds);
            props["gestures"] = gestures;
            Analytics.TrackEvent("monster_killed", props);
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Fight(int playerIndex, int fight)
        {
            return new GameAnalyticsProperties { ["player_index"] = playerIndex, ["fight"] = fight };
        }
    }
}
