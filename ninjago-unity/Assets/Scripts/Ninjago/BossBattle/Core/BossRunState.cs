#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The live state of one attempt: phase, difficulty, hearts, both boss bars, progress counters, the beat index the
    /// next checkpoint saves, and the stats. New() starts a battle; FromSave() rebuilds a continue from a checkpoint
    /// (hearts full, phase 1 bar = the volley table after the saved pickups, phase 2 bar at the last wake notch passed);
    /// ToSave() writes the checkpoint.
    /// </summary>
    public sealed class BossRunState
    {
        public string runId = "";
        public BossPhase phase = BossPhase.Road;
        public BossDifficulty difficulty = BossDifficulty.Normal;
        public int numPlayers = 1;
        public int hearts;
        public int maxHearts;
        public int pickupsCollected;
        public int wakesPassed;
        public int clapsDone;
        public int minionsThrown;
        /// <summary>Script index of the next beat of the current phase (saved; the continue resumes there).</summary>
        public int beatIndex;
        public bool isContinue;
        /// <summary>The intro / transform cutscene was already seen (OK/Back skips it).</summary>
        public bool seenIntro;
        public bool seenTransform;
        public int knockoutsThisPhase;

        BossRunState(float roadHp, float skyHp)
        {
            RoadHealth = new BossHealth(roadHp);
            SkyHealth = new BossHealth(skyHp);
        }

        public BossHealth RoadHealth { get; }
        public BossHealth SkyHealth { get; }
        public BossBattleStats Stats { get; } = new();
        /// <summary>The bar of the current phase.</summary>
        public BossHealth ActiveHealth => phase == BossPhase.Sky ? SkyHealth : RoadHealth;

        #region Factories

        public static BossRunState New(BossBattleConfig config, BossDifficulty difficulty, int players)
        {
            var state = new BossRunState(config.RoadMaxHp, config.SkyMaxHp)
            {
                runId = Guid.NewGuid().ToString("N"),
                phase = BossPhase.Road,
                difficulty = difficulty,
                numPlayers = Mathf.Max(1, players),
            };
            state.maxHearts = Mathf.Max(1, config.Difficulty(difficulty).hearts);
            state.hearts = state.maxHearts;
            return state;
        }

        public static BossRunState FromSave(BossBattleSave save, BossBattleConfig config)
        {
            var powerUp = config.PowerUp;
            var state = new BossRunState(config.RoadMaxHp, config.SkyMaxHp)
            {
                runId = string.IsNullOrEmpty(save.runId) ? Guid.NewGuid().ToString("N") : save.runId,
                phase = save.phase,
                difficulty = save.difficulty,
                numPlayers = Mathf.Max(1, save.numPlayers),
                pickupsCollected = Mathf.Max(0, save.pickupsCollected),
                wakesPassed = Mathf.Max(0, save.wakesPassed),
                clapsDone = Mathf.Max(0, save.clapsDone),
                minionsThrown = Mathf.Max(0, save.minionsThrown),
                beatIndex = Mathf.Max(0, save.beatIndex),
                knockoutsThisPhase = Mathf.Max(0, save.knockoutsThisPhase),
                isContinue = true,
            };
            state.maxHearts = Mathf.Max(1, config.Difficulty(save.difficulty).hearts);
            state.hearts = state.maxHearts;

            if (state.phase == BossPhase.Road)
            {
                state.RoadHealth.SetCurrent(RocketVolleys.HpAfter(powerUp.volleys, state.pickupsCollected, powerUp.baseRocketDamage, powerUp.pickupsToPhase2));
            }
            else
            {
                state.RoadHealth.SetCurrent(0f);
            }

            var thresholds = config.Sky.wakeThresholds;
            var skyFraction = state.wakesPassed > 0 && thresholds.Count > 0 ? thresholds[Mathf.Min(state.wakesPassed, thresholds.Count) - 1] : 1f;
            state.SkyHealth.SetFraction(skyFraction);

            var stats = state.Stats;
            stats.elapsedSeconds = Mathf.Max(0f, save.elapsedSeconds);
            stats.restoredHits = Mathf.Max(0, save.hitsTaken);
            stats.pickups = state.pickupsCollected;
            stats.rocketsFired = Mathf.Max(0, save.rocketsFired);
            stats.boltsFired = Mathf.Max(0, save.boltsFired);
            stats.boltsHit = Mathf.Max(0, save.boltsHit);
            return state;
        }

        #endregion

        #region Public API

        public BossBattleSave ToSave()
        {
            return new BossBattleSave
            {
                version = BossBattleSave.CurrentVersion,
                runId = runId,
                numPlayers = numPlayers,
                difficulty = difficulty,
                phase = phase,
                beatIndex = beatIndex,
                pickupsCollected = pickupsCollected,
                wakesPassed = wakesPassed,
                clapsDone = clapsDone,
                minionsThrown = minionsThrown,
                skyHpFraction = SkyHealth.Fraction,
                elapsedSeconds = Stats.elapsedSeconds,
                hitsTaken = Stats.HitsTaken,
                rocketsFired = Stats.rocketsFired,
                boltsFired = Stats.boltsFired,
                boltsHit = Stats.boltsHit,
                knockoutsThisPhase = knockoutsThisPhase,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
            };
        }

        /// <summary>Hearts back to full (phase start, continue).</summary>
        public void RefillHearts() => hearts = maxHearts;

        /// <summary>Boss bar of the current phase in percent, rounded (analytics, debug line).</summary>
        public int BossHpPercent => Mathf.RoundToInt(ActiveHealth.Fraction * 100f);

        #endregion
    }
}
