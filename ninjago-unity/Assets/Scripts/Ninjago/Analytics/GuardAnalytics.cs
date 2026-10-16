#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Guard gameplay events over AnalyticsManager.TrackEvent; every player event carries the player index.</summary>
    public static class GuardAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        /// <param name="guardAgeSeconds">For a block: seconds since the guard went up; -1 otherwise.</param>
        public static void Throw(int playerIndex, int throwNumber, bool big, GuardOutcome outcome, float guardAgeSeconds)
        {
            Analytics.TrackEvent("guard_throw", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["throw"] = throwNumber, ["big"] = big, ["outcome"] = outcome.ToString(),
                ["read_matched"] = outcome == GuardOutcome.Saved, ["guard_age_s"] = Round(guardAgeSeconds),
            });
        }

        /// <summary>A guard read while nothing was coming at that player.</summary>
        public static void RaiseAlone(int playerIndex, int throwNumber)
        {
            Analytics.TrackEvent("guard_raise_alone", new GameAnalyticsProperties { ["player_index"] = playerIndex, ["throw"] = throwNumber });
        }

        public static void BigThrow(int throwNumber, bool saved, int players)
        {
            Analytics.TrackEvent("guard_big_throw", new GameAnalyticsProperties { ["throw"] = throwNumber, ["saved"] = saved, ["players"] = players });
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;
    }
}
