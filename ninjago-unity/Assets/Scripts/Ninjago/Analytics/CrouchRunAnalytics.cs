#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Crouch Run gameplay events over AnalyticsManager.TrackEvent; every event carries the player index and level (1-based).</summary>
    public static class CrouchRunAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        public static void ZoneEntered(int playerIndex, int level, int zone, CrouchZoneState state, bool crouching, float timeLeft)
        {
            var props = Level(playerIndex, level);
            props["zone"] = zone;
            props["state"] = state.ToString();
            props["crouching"] = crouching;
            props["time_left_s"] = Round(timeLeft);
            Analytics.TrackEvent("crouch_zone_entered", props);
        }

        public static void Heard(int playerIndex, int level, int zone, int timesHeard, float timeLeft)
        {
            var props = Level(playerIndex, level);
            props["zone"] = zone;
            props["times_heard"] = timesHeard;
            props["time_left_s"] = Round(timeLeft);
            Analytics.TrackEvent("crouch_heard", props);
        }

        public static void LevelFailed(int playerIndex, int level, int timesHeard, float distanceMeters)
        {
            var props = Level(playerIndex, level);
            props["times_heard"] = timesHeard;
            props["distance_m"] = Round(distanceMeters);
            Analytics.TrackEvent("crouch_level_failed", props);
        }

        public static void LevelCleared(int playerIndex, int level, int timesHeard, float timeLeft)
        {
            var props = Level(playerIndex, level);
            props["times_heard"] = timesHeard;
            props["time_left_s"] = Round(timeLeft);
            Analytics.TrackEvent("crouch_level_cleared", props);
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Level(int playerIndex, int level)
        {
            return new GameAnalyticsProperties { ["player_index"] = playerIndex, ["level"] = level };
        }
    }
}
