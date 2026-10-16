#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Build gameplay events over AnalyticsManager.TrackEvent; every player event carries the player index.</summary>
    public static class BuildAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        /// <param name="sinceLastSeconds">Seconds since this player's previous counted clap; -1 for the first.</param>
        public static void Clap(int playerIndex, int brick, float sinceLastSeconds, float elapsedSeconds)
        {
            Analytics.TrackEvent("build_clap", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["brick"] = brick, ["since_last_s"] = Round(sinceLastSeconds), ["elapsed_s"] = Round(elapsedSeconds),
            });
        }

        /// <summary>The hands came close but not together: a try the reading did not count.</summary>
        public static void NearClap(int playerIndex, float elapsedSeconds)
        {
            Analytics.TrackEvent("build_near_clap", new GameAnalyticsProperties { ["player_index"] = playerIndex, ["elapsed_s"] = Round(elapsedSeconds) });
        }

        public static void RowDone(int row, float elapsedSeconds)
        {
            Analytics.TrackEvent("build_row_done", new GameAnalyticsProperties { ["row"] = row, ["elapsed_s"] = Round(elapsedSeconds) });
        }

        /// <param name="open">True when the bridge was finished; false when time ran out.</param>
        public static void End(bool open, int placed, int total, float elapsedSeconds)
        {
            Analytics.TrackEvent("build_end", new GameAnalyticsProperties
            {
                ["way_open"] = open, ["placed"] = placed, ["total"] = total, ["elapsed_s"] = Round(elapsedSeconds),
            });
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;
    }
}
