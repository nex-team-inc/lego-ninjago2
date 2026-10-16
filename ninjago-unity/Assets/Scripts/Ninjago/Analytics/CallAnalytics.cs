#nullable enable

using Nex.Platform;

namespace Nex.Ninjago
{
    /// <summary>Call gameplay events over AnalyticsManager.TrackEvent; every player event carries the player index and the wave (1-based).</summary>
    public static class CallAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        /// <param name="goblinsNear">Goblins walking within the prompt distance of the caller's ninja.</param>
        public static void Call(int playerIndex, int wave, CallResult result, int hand, int goblinsNear, int pile)
        {
            Analytics.TrackEvent("call_gesture", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["wave"] = wave, ["result"] = result.ToString(), ["understood"] = result != CallResult.Missed,
                ["hand"] = hand == 0 ? "left" : "right", ["goblins_near"] = goblinsNear, ["pile"] = pile,
            });
        }

        /// <param name="callerIndex">Player index of the caller; -1 when the teammate came by itself.</param>
        public static void Slam(int wave, int callerIndex, bool team, int cleared)
        {
            Analytics.TrackEvent("call_slam", new GameAnalyticsProperties
            {
                ["wave"] = wave, ["player_index"] = callerIndex, ["team"] = team, ["cleared"] = cleared,
            });
        }

        public static void Pile(int playerIndex, int wave, int pileInWave)
        {
            Analytics.TrackEvent("call_goblin_pile", new GameAnalyticsProperties { ["player_index"] = playerIndex, ["wave"] = wave, ["pile_in_wave"] = pileInWave });
        }

        public static void AutoHelp(int wave)
        {
            Analytics.TrackEvent("call_auto_help", new GameAnalyticsProperties { ["wave"] = wave });
        }
    }
}
