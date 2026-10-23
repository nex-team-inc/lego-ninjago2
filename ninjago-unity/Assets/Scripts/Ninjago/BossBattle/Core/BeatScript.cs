#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Walks a phase's beat list. Index is the script index the next Next() returns (what a checkpoint saves); a looping
    /// script wraps to loopFrom, a non-looping one becomes Finished after its last beat. A missed power-up crate is
    /// requeued: after the next attack beat (and the entry it overlaps, if any), Next() hands out one extra PowerUp
    /// (unless the script's next beat already is one).
    /// </summary>
    public sealed class BeatScript
    {
        readonly IReadOnlyList<BeatEntry> script;
        readonly int loopFrom;
        readonly bool loops;
        bool reachedEnd;
        bool requeueArmed;
        bool insertPowerUp;

        public BeatScript(IReadOnlyList<BeatEntry> script, int loopFrom, int startIndex, bool loops)
        {
            this.script = script;
            this.loops = loops;
            this.loopFrom = script.Count == 0 ? 0 : Mathf.Clamp(loopFrom, 0, script.Count - 1);
            Index = Mathf.Max(0, startIndex);
            if (Index >= script.Count) WrapOrFinish();
        }

        /// <summary>The script index of the beat Next() returns (a requeued power-up does not move it).</summary>
        public int Index { get; private set; }

        /// <summary>No beat left (a non-looping script past its end, nothing requeued).</summary>
        public bool Finished => reachedEnd && !insertPowerUp;

        /// <summary>True between a RequeuePowerUpAfterNextAttack() and the requeued crate being handed out.</summary>
        public bool PowerUpRequeued => requeueArmed || insertPowerUp;

        #region Public API

        /// <summary>The next beat (a Breather once Finished).</summary>
        public BeatEntry Next()
        {
            if (insertPowerUp)
            {
                insertPowerUp = false;
                return BeatEntry.Of(BossBeat.PowerUp);
            }

            if (reachedEnd) return BeatEntry.Of(BossBeat.Breather);
            var entry = script[Index];
            Index++;
            if (Index >= script.Count) WrapOrFinish();
            // An attack that overlaps the next entry (slam + rock) is finished only after that entry.
            if (requeueArmed && IsAttack(entry.beat) && !entry.overlapNext)
            {
                requeueArmed = false;
                insertPowerUp = reachedEnd || script[Index].beat != BossBeat.PowerUp;
            }

            return entry;
        }

        /// <summary>The crate was missed: one more PowerUp right after the next attack beat.</summary>
        public void RequeuePowerUpAfterNextAttack() => requeueArmed = true;

        /// <summary>Beats where the boss attacks (a requeued crate waits for one of these).</summary>
        public static bool IsAttack(BossBeat beat)
        {
            return beat is BossBeat.GroundSlam or BossBeat.RockDrop or BossBeat.MinionThrow or BossBeat.DevilWind or BossBeat.Clap;
        }

        #endregion

        #region Helpers

        void WrapOrFinish()
        {
            if (loops && script.Count > 0)
            {
                Index = loopFrom;
                return;
            }

            Index = script.Count;
            reachedEnd = true;
        }

        #endregion
    }
}
