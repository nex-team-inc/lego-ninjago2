#nullable enable

using UnityEngine.Scripting;

namespace Nex.Ninjago
{
    /// <summary>
    /// Boss Battle checkpoint (GDD §9), saved by PlayerDataManager under its own key "bossBattleSave" (Easy Save) at calm
    /// moments only: phase start, the breather after a volley, a wake roar, a clap recovery, quit and app pause. Public
    /// fields only; append new fields, never rename; bump version when a field changes meaning (Load drops other
    /// versions). Enums are stored as their int values.
    /// </summary>
    [Preserve]
    public class BossBattleSave
    {
        /// <summary>The version this build writes and accepts (a property, so Easy Save does not store it).</summary>
        public static int CurrentVersion => 1;

        public int version = 1;
        // Joins the analytics sessions of a continued battle.
        public string runId = "";
        public int numPlayers = 1;
        public BossDifficulty difficulty = BossDifficulty.Normal;
        public BossPhase phase = BossPhase.Road;
        // Script index of the next beat of the phase (the continue resumes there, after the ready beat).
        public int beatIndex;
        public int pickupsCollected;
        public int wakesPassed;
        public int clapsDone;
        public int minionsThrown;
        public float skyHpFraction = 1f;
        public float elapsedSeconds;
        public int hitsTaken;
        public int rocketsFired;
        public int boltsFired;
        public int boltsHit;
        // "Continue on Easy" is offered after 2 knockouts in the same phase.
        public int knockoutsThisPhase;
        public long savedAtUtcTicks;
    }
}
