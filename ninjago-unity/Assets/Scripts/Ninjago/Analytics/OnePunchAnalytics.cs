#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>One Punch gameplay events over AnalyticsManager.TrackEvent; every player event carries the player index and the wave (1-based).</summary>
    public static class OnePunchAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        public static void SetupJoints(int playerIndex, RestJoints joints)
        {
            Analytics.TrackEvent("punch_setup_joints", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex,
                ["left_arm_in"] = Round(joints.ArmLength(true)),
                ["right_arm_in"] = Round(joints.ArmLength(false)),
                ["shoulder_width_in"] = Round(joints.rightShoulder.x - joints.leftShoulder.x),
                ["hips_below_chest_in"] = Round(-joints.hips.y),
            });
        }

        /// <param name="hand">0 = left, 1 = right.</param>
        public static void PunchTry(int playerIndex, int wave, int hand, PunchDetector.Try attempt)
        {
            var props = Player(playerIndex, wave);
            props["hand"] = hand == 0 ? "left" : "right";
            props["signal"] = attempt.signal.ToString();
            props["counted"] = attempt.IsPunch;
            props["growth_in"] = Round(attempt.growth);
            props["growth_speed_in_s"] = Round(attempt.growthSpeed);
            props["scale_growth"] = Round(attempt.scaleGrowth);
            props["side_travel_in"] = Round(attempt.sideTravel);
            Analytics.TrackEvent("punch_try", props);
        }

        /// <param name="method">"punch" (a free monster), "rescue" (the monster on the partner) or "wiggle" (the pinned player broke free).</param>
        public static void Thrown(int playerIndex, int wave, int monster, string method, float distanceMeters)
        {
            var props = Player(playerIndex, wave);
            props["monster"] = monster;
            props["method"] = method;
            props["distance_m"] = Round(distanceMeters);
            Analytics.TrackEvent("punch_monster_thrown", props);
        }

        public static void PinStart(int playerIndex, int wave, int monster, float health)
        {
            var props = Player(playerIndex, wave);
            props["monster"] = monster;
            props["health"] = Round(health);
            Analytics.TrackEvent("punch_pin_start", props);
        }

        /// <summary>The wiggle meter filled.</summary>
        public static void Wiggle(int playerIndex, int wave, float pinnedSeconds, float health)
        {
            var props = Player(playerIndex, wave);
            props["pinned_s"] = Round(pinnedSeconds);
            props["health"] = Round(health);
            Analytics.TrackEvent("punch_wiggle", props);
        }

        /// <param name="method">"wiggle" or "partner_punch".</param>
        public static void PinEscape(int playerIndex, int wave, string method, float pinnedSeconds, float health)
        {
            var props = Player(playerIndex, wave);
            props["method"] = method;
            props["pinned_s"] = Round(pinnedSeconds);
            props["health"] = Round(health);
            Analytics.TrackEvent("punch_pin_escape", props);
        }

        /// <summary>One whole point of health drained away while pinned.</summary>
        public static void HealthLoss(int playerIndex, int wave, float health)
        {
            var props = Player(playerIndex, wave);
            props["health"] = Round(health);
            Analytics.TrackEvent("punch_health_loss", props);
        }

        public static void Down(int playerIndex, int wave, float pinnedSeconds)
        {
            var props = Player(playerIndex, wave);
            props["pinned_s"] = Round(pinnedSeconds);
            Analytics.TrackEvent("punch_down", props);
        }

        /// <param name="result">"cleared" or "down".</param>
        /// <param name="details">Per-player counts for the wave, keyed p1_*, p2_*.</param>
        public static void WaveEnd(int wave, string result, float seconds, GameAnalyticsProperties details)
        {
            details["wave"] = wave;
            details["result"] = result;
            details["seconds"] = Round(seconds);
            Analytics.TrackEvent("punch_wave_end", details);
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Player(int playerIndex, int wave)
        {
            return new GameAnalyticsProperties { ["player_index"] = playerIndex, ["wave"] = wave };
        }
    }
}
