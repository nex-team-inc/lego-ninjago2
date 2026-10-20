#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Chapter analytics through AnalyticsManager. Every event carries the mode id, whether it is built-in or
    /// player-made, the level and the block; player events add the player index. Names and keys are snake_case.
    /// </summary>
    public sealed class ChapterLog
    {
        readonly string modeId;
        readonly bool builtIn;

        public int Level { get; set; } = -1;
        public int Block { get; set; } = -1;
        public BlockType BlockType { get; set; }

        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        public ChapterLog(string modeId, bool builtIn)
        {
            this.modeId = modeId;
            this.builtIn = builtIn;
        }

        #region Session

        public void ModeStart(int requestedPlayers) => Analytics.TrackGameStart("chapter", requestedPlayers, modeId, Props());

        public void ModeEnd(string result)
        {
            var props = Props();
            props["result"] = result;
            Analytics.TrackEvent("chapter_mode_end", props);
            Analytics.TrackGameStop(new GameAnalyticsProperties { ["result"] = result, ["mode_id"] = modeId });
        }

        public void LevelStart(int activePlayers)
        {
            var props = Props();
            props["active_players"] = activePlayers;
            Analytics.TrackEvent("chapter_level_start", props);
        }

        public void LevelEnd(int stars, int heartsLeft, int restarts, float seconds)
        {
            var props = Props();
            props["stars"] = stars;
            props["hearts_left"] = heartsLeft;
            props["block_restarts"] = restarts;
            props["seconds"] = Round(seconds);
            Analytics.TrackEvent("chapter_level_end", props);
        }

        public void BlockStart(bool restart) => Analytics.TrackEvent("chapter_block_start", With(Props(), "restart", restart));

        public void BlockEnd(string result, float seconds)
        {
            var props = Props();
            props["result"] = result;
            props["seconds"] = Round(seconds);
            Analytics.TrackEvent("chapter_block_end", props);
        }

        public void BlockRestart(string reason) => Analytics.TrackEvent("chapter_block_restart", With(Props(), "reason", reason));

        public void LinkStart(LinkEntry link)
        {
            var props = Props();
            props["link_state"] = link.state.ToString();
            props["moment"] = link.moment.ToString();
            props["camera_move"] = link.cameraMove.ToString();
            Analytics.TrackEvent("chapter_link_start", props);
        }

        public void LinkEnd() => Analytics.TrackEvent("chapter_link_end", Props());

        public void RestTime(float seconds) => Analytics.TrackEvent("chapter_rest_time", With(Props(), "seconds", Round(seconds)));

        public void ConfirmPressed(string screen) => Analytics.TrackEvent("chapter_confirm_pressed", With(Props(), "screen", screen));

        public void Recalibrate(int lostPlayers) => Analytics.TrackEvent("chapter_recalibrate", With(Props(), "lost_players", lostPlayers));

        #endregion

        #region Gameplay

        /// <param name="reason">Why a QTE failed ("wrong_way", "too_slow", "no_slash", ...); empty on success.</param>
        public void Qte(int playerIndex, Gesture gesture, bool success, float reactionSeconds = -1f, string reason = "")
        {
            var props = Props(playerIndex);
            props["gesture"] = gesture.ToString();
            props["success"] = success;
            props["reaction_s"] = Round(reactionSeconds);
            props["reason"] = reason;
            Analytics.TrackEvent(success ? "chapter_qte_success" : "chapter_qte_fail", props);
        }

        public void Heard(int playerIndex, int zone, float timeLeft)
        {
            var props = Props(playerIndex);
            props["zone"] = zone;
            props["time_left"] = Round(timeLeft);
            Analytics.TrackEvent("chapter_heard", props);
        }

        public void ObstacleHit(VehicleType vehicle, int dominantPlayer) =>
            Analytics.TrackEvent("chapter_obstacle_hit", With(Props(dominantPlayer), "vehicle", vehicle.ToString()));

        public void Pinned(int playerIndex) => Analytics.TrackEvent("chapter_pinned", Props(playerIndex));

        public void ShookFree(int playerIndex, float seconds) =>
            Analytics.TrackEvent("chapter_shook_free", With(Props(playerIndex), "seconds", Round(seconds)));

        public void SlashFrenzy(int playerIndex, int slashes, int stones)
        {
            var props = Props(playerIndex);
            props["slashes"] = slashes;
            props["stones"] = stones;
            Analytics.TrackEvent("chapter_slash_frenzy", props);
        }

        #endregion

        #region Helpers

        GameAnalyticsProperties Props(int playerIndex = -1) => new()
        {
            ["mode_id"] = modeId,
            ["built_in"] = builtIn,
            ["level"] = Level + 1,
            ["block"] = Block + 1,
            ["block_type"] = BlockType.ToString(),
            ["player_index"] = playerIndex,
        };

        static GameAnalyticsProperties With(GameAnalyticsProperties props, string key, GameAnalyticsValue value)
        {
            props[key] = value;
            return props;
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        #endregion
    }
}
