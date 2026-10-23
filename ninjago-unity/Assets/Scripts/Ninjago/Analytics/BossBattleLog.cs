#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Boss Battle analytics (GDD §14) through AnalyticsManager, in the style of ChapterLog. Bind() the attempt's
    /// BossRunState and every event carries run_id, phase, boss_hp (percent of the current bar), hearts, elapsed_s,
    /// players, difficulty and is_continue. Names and keys are snake_case. One event per attack, pickup, window or clap:
    /// never per bolt, tick or frame. Every call is a no-op when the AnalyticsManager is gone (scene teardown, tests).
    /// Screens are tracked by ViewManager from each view's AnalyticsScreenName.
    /// </summary>
    public sealed class BossBattleLog
    {
        public const string ContentName = "boss_battle";

        BossRunState? state;

        public BossRunState? State => state;

        /// <summary>The attempt whose state fills the common properties.</summary>
        public void Bind(BossRunState? runState) => state = runState;

        #region Session

        /// <summary>
        /// Once per attempt: TrackGameStart ("new" or "continue"), the PLAY-scope run_id / is_continue, then the
        /// minigame_start event (it carries the PLAY properties and shows in the Editor's [Analytics] mirror).
        /// </summary>
        public void MiniGameStart(int requestedPlayers)
        {
            if (!TryGetManager(out var manager)) return;
            var isContinue = state is { isContinue: true };
            var startPhase = state == null ? "road" : PhaseName(state.phase);
            var props = Props();
            props["start_phase"] = startPhase;
            manager.TrackGameStart(ContentName, requestedPlayers, isContinue ? "continue" : "new", props);
            // PLAY-scope properties must be registered after TrackGameStart (the start resets them).
            var analytics = GameAnalytics.Instance;
            if (analytics != null && state != null)
            {
                analytics.RegisterSessionProperties("run_id", state.runId, GameAnalytics.SessionPropertiesScope.PLAY);
                analytics.RegisterSessionProperties("is_continue", isContinue, GameAnalytics.SessionPropertiesScope.PLAY);
            }

            var startProps = Props();
            startProps["minigame"] = ContentName;
            startProps["start_phase"] = startPhase;
            startProps["requested_players"] = requestedPlayers;
            startProps["is_continue"] = isContinue;
            manager.TrackEvent("minigame_start", startProps);
        }

        /// <param name="result">"win", "lose" or "abandon".</param>
        public void MiniGameEnd(string result, int activePlayers, GameAnalyticsProperties? details = null)
        {
            if (!TryGetManager(out var manager)) return;
            var props = Props();
            if (details != null)
            {
                foreach (var pair in details) props[pair.Key] = pair.Value;
            }

            props["minigame"] = ContentName;
            props["result"] = result;
            props["active_players"] = activePlayers;
            if (state != null)
            {
                var stats = state.Stats;
                props["hits_taken"] = stats.HitsTaken;
                props["pickups"] = state.pickupsCollected;
                props["rockets"] = stats.rocketsFired;
                props["wakes"] = state.wakesPassed;
                props["laser_accuracy"] = Round(stats.Accuracy);
            }

            manager.TrackEvent("minigame_end", props);
            manager.TrackGameStop(new GameAnalyticsProperties { ["result"] = result });
        }

        /// <summary>ui_button_click for any boss view button (back, skip, difficulty, secret_code ...).</summary>
        public static void UiAction(string screen, string button, int index = -1)
        {
            Track("ui_button_click", new GameAnalyticsProperties { ["screen"] = screen, ["button"] = button, ["index"] = index });
        }

        public static void SettingChanged(string setting, string value)
        {
            Track("settings_changed", new GameAnalyticsProperties { ["setting"] = setting, ["new_value"] = value });
        }

        public void Pause(string screen)
        {
            if (!TryGetManager(out var manager)) return;
            manager.TrackEvent("boss_pause", With(Props(), "screen", screen));
            manager.TrackPause();
        }

        public void Resume()
        {
            if (!TryGetManager(out var manager)) return;
            manager.TrackEvent("boss_resume", Props());
            manager.TrackResume();
        }

        public void TrackingLost(int playerIndex) => Track("boss_tracking_lost", With(Props(), "player_index", playerIndex));

        public void CheckpointSave(string reason, int nextBeatIndex)
        {
            var props = Props();
            props["reason"] = reason;
            props["beat_index"] = nextBeatIndex;
            Track("boss_checkpoint_save", props);
        }

        public void CheckpointContinue()
        {
            var props = Props();
            if (state != null)
            {
                props["beat_index"] = state.beatIndex;
                props["pickups"] = state.pickupsCollected;
                props["wakes"] = state.wakesPassed;
            }

            Track("boss_checkpoint_continue", props);
        }

        #endregion

        #region Phases And Cutscenes

        public void PhaseStart(BossPhase phase) => Track("boss_phase_start", With(Props(), "phase_name", PhaseName(phase)));

        public void PhaseEnd(BossPhase phase, BossPhaseOutcome outcome, float seconds, int hitsTaken)
        {
            var props = Props();
            props["phase_name"] = PhaseName(phase);
            props["result"] = OutcomeName(outcome);
            props["seconds"] = Round(seconds);
            props["phase_hits"] = hitsTaken;
            Track("boss_phase_end", props);
        }

        public void Cutscene(BossCutscene cutscene, float seconds, bool skipped)
        {
            var props = Props();
            props["name"] = CutsceneName(cutscene);
            props["seconds"] = Round(seconds);
            props["skipped"] = skipped;
            Track("boss_cutscene", props);
        }

        /// <summary>boss_defeat: the Titan went down (before the finale).</summary>
        public void Defeat(float seconds) => Track("boss_defeat", With(Props(), "seconds", Round(seconds)));

        #endregion

        #region Attacks

        /// <param name="index">Attack number of this kind in the run (1-based).</param>
        public void AttackStart(BossBeat attack, int index, int count, MinionSides sides = default)
        {
            var props = Props();
            props["attack"] = BeatName(attack);
            props["index"] = index;
            props["count"] = count;
            props["side"] = SidesName(sides);
            Track("boss_attack_start", props);
        }

        /// <param name="result">"dodged", "hit", "cleared", "failed" ...</param>
        public void AttackResult(BossBeat attack, int index, string result, int damage, float reactionSeconds = -1f)
        {
            var props = Props();
            props["attack"] = BeatName(attack);
            props["index"] = index;
            props["result"] = result;
            props["damage"] = damage;
            props["reaction_s"] = Round(reactionSeconds);
            Track("boss_attack_result", props);
        }

        public void MinionCleared(MinionSides side, float secondsOnCar, int damageTaken, bool soloSmash, int playerIndex)
        {
            var props = Props();
            props["side"] = SidesName(side);
            props["seconds_on_car"] = Round(secondsOnCar);
            props["damage_taken"] = damageTaken;
            props["solo_smash"] = soloSmash;
            props["player_index"] = playerIndex;
            Track("boss_minion_cleared", props);
        }

        /// <param name="pickup">1-based pickup number.</param>
        public void PowerUp(int pickup, int rockets, float damageMultiplier, float bossHpBefore)
        {
            var props = Props();
            props["pickup"] = pickup;
            props["rockets"] = rockets;
            props["damage_multiplier"] = Round(damageMultiplier);
            props["boss_hp_before"] = Round(bossHpBefore);
            Track("boss_powerup", props);
        }

        /// <summary>A crate went past the car without being collected (it returns after the next attack).</summary>
        public void PowerUpMissed(int pickup) => Track("boss_powerup_missed", With(Props(), "pickup", pickup));

        public void Wake(int wake, float threshold, bool timedOut)
        {
            var props = Props();
            props["wake"] = wake;
            props["threshold"] = Round(threshold);
            props["timed_out"] = timedOut;
            Track("boss_wake", props);
        }

        public void LaserWindow(int window, float seconds, int boltsFired, int boltsHit, float damage, bool timedOut)
        {
            var props = Props();
            props["window"] = window;
            props["seconds"] = Round(seconds);
            props["bolts_fired"] = boltsFired;
            props["bolts_hit"] = boltsHit;
            props["hit_ratio"] = Round(boltsFired > 0 ? (float)boltsHit / boltsFired : 0f);
            props["damage"] = Round(damage);
            props["timed_out"] = timedOut;
            Track("boss_laser_window", props);
        }

        /// <param name="result">"dodged" or "hit".</param>
        public void Clap(int clap, string result, float band)
        {
            var props = Props();
            props["clap"] = clap;
            props["result"] = result;
            props["band"] = Round(band);
            Track("boss_clap", props);
        }

        /// <param name="dominantPlayer">Who steered harder at the hit (-1 = nobody).</param>
        public void VehicleHit(BossHazard hazard, int damage, int dominantPlayer)
        {
            var props = Props();
            props["hazard"] = HazardName(hazard);
            props["damage"] = damage;
            props["player_index"] = dominantPlayer;
            Track("boss_vehicle_hit", props);
        }

        #endregion

        #region Names

        public static string PhaseName(BossPhase phase) => phase == BossPhase.Sky ? "sky" : "road";

        public static string DifficultyName(BossDifficulty difficulty) => difficulty == BossDifficulty.Easy ? "easy" : "normal";

        public static string OutcomeName(BossPhaseOutcome outcome)
        {
            return outcome switch
            {
                BossPhaseOutcome.Cleared => "cleared",
                BossPhaseOutcome.KnockedOut => "knocked_out",
                _ => "quit",
            };
        }

        public static string CutsceneName(BossCutscene cutscene)
        {
            return cutscene switch
            {
                BossCutscene.Intro => "intro",
                BossCutscene.Transform => "transform",
                BossCutscene.Finale => "finale",
                BossCutscene.Knockout => "knockout",
                BossCutscene.SkyRejoin => "sky_rejoin",
                _ => "other",
            };
        }

        public static string BeatName(BossBeat beat)
        {
            return beat switch
            {
                BossBeat.Breather => "breather",
                BossBeat.GroundSlam => "ground_slam",
                BossBeat.RockDrop => "rock_drop",
                BossBeat.MinionThrow => "minion_throw",
                BossBeat.PowerUp => "power_up",
                BossBeat.Ready => "ready",
                BossBeat.SteerCheck => "steer_check",
                BossBeat.DevilWind => "devil_wind",
                BossBeat.LaserWindow => "laser_window",
                BossBeat.Clap => "clap",
                BossBeat.RiseDuck => "rise_duck",
                _ => "other",
            };
        }

        public static string HazardName(BossHazard hazard)
        {
            return hazard switch
            {
                BossHazard.Brick => "brick",
                BossHazard.Rock => "rock",
                BossHazard.Minion => "minion",
                BossHazard.Wind => "wind",
                BossHazard.SpikeWall => "spike_wall",
                _ => "other",
            };
        }

        public static string SidesName(MinionSides sides)
        {
            return sides switch
            {
                MinionSides.Left => "left",
                MinionSides.Right => "right",
                MinionSides.Both => "both",
                _ => "none",
            };
        }

        #endregion

        #region Helpers

        /// <summary>False once the singleton is gone (scene teardown reaches it before the last events; EditMode tests).</summary>
        static bool TryGetManager(out AnalyticsManager manager)
        {
            manager = AnalyticsManager.Instance;
            return manager != null;
        }

        static void Track(string eventName, GameAnalyticsProperties props)
        {
            if (TryGetManager(out var manager)) manager.TrackEvent(eventName, props);
        }

        GameAnalyticsProperties Props()
        {
            var props = new GameAnalyticsProperties();
            if (state == null) return props;
            props["run_id"] = state.runId;
            props["phase"] = PhaseName(state.phase);
            props["boss_hp"] = state.BossHpPercent;
            props["hearts"] = state.hearts;
            props["elapsed_s"] = Round(state.Stats.elapsedSeconds);
            props["players"] = state.numPlayers;
            props["difficulty"] = DifficultyName(state.difficulty);
            props["is_continue"] = state.isContinue;
            return props;
        }

        static GameAnalyticsProperties With(GameAnalyticsProperties props, string key, GameAnalyticsValue value)
        {
            props[key] = value;
            return props;
        }

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        #endregion
    }
}
