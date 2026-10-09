#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Robot Mecha gameplay events over AnalyticsManager.TrackEvent; every event carries the player index.</summary>
    public static class MechaAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        #region Setup

        /// <param name="elbowReference">Elbow angles in degrees while holding the L at setup (x left arm, y right arm).</param>
        public static void SetupHold(int playerIndex, float seconds, Vector2 elbowReference)
        {
            Analytics.TrackEvent("mecha_setup_hold", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["seconds"] = Round(seconds),
                ["left_elbow_deg"] = Round(elbowReference.x), ["right_elbow_deg"] = Round(elbowReference.y),
            });
        }

        #endregion

        #region Pose

        public static void PoseGained(int playerIndex, float waveSeconds, float yaw)
        {
            var props = Player(playerIndex, waveSeconds);
            props["yaw_deg"] = Round(yaw);
            Analytics.TrackEvent("mecha_pose_gained", props);
        }

        /// <param name="reason">"left_arm", "right_arm", "both_arms" or "not_tracked".</param>
        public static void PoseLost(int playerIndex, float waveSeconds, float heldSeconds, string reason)
        {
            var props = Player(playerIndex, waveSeconds);
            props["held_s"] = Round(heldSeconds);
            props["reason"] = reason;
            Analytics.TrackEvent("mecha_pose_lost", props);
        }

        #endregion

        #region Fight

        /// <param name="source">"rusher" or "arrow".</param>
        public static void Damage(int playerIndex, float waveSeconds, string source, int heartsLeft)
        {
            var props = Player(playerIndex, waveSeconds);
            props["source"] = source;
            props["hearts_left"] = heartsLeft;
            Analytics.TrackEvent("mecha_damage", props);
        }

        public static void Kill(int playerIndex, float waveSeconds, MechaEnemyType enemy, int kills, bool cancelledArrow)
        {
            var props = Player(playerIndex, waveSeconds);
            props["enemy"] = enemy.ToString();
            props["kills"] = kills;
            props["cancelled_arrow"] = cancelledArrow;
            Analytics.TrackEvent("mecha_kill", props);
        }

        public static void Down(int playerIndex, float waveSeconds, int kills)
        {
            var props = Player(playerIndex, waveSeconds);
            props["kills"] = kills;
            Analytics.TrackEvent("mecha_down", props);
        }

        /// <param name="result">"survived" or "down".</param>
        public static void WaveEnd(int playerIndex, float surviveSeconds, string result, int kills, int heartsLost, int posePercent)
        {
            var props = Player(playerIndex, surviveSeconds);
            props["result"] = result;
            props["kills"] = kills;
            props["hearts_lost"] = heartsLost;
            props["pose_percent"] = posePercent;
            Analytics.TrackEvent("mecha_wave_end", props);
        }

        #endregion

        #region Helpers

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Player(int playerIndex, float waveSeconds)
        {
            return new GameAnalyticsProperties { ["player_index"] = playerIndex, ["wave_s"] = Round(waveSeconds) };
        }

        #endregion
    }
}
