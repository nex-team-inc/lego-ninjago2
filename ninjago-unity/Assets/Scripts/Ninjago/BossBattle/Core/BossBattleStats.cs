#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// Counters of one battle (summed across continues through the save): hits per hazard, pickups, rockets, laser bolts,
    /// minions, claps and play time. The result view, the bests and the analytics read them.
    /// </summary>
    public sealed class BossBattleStats
    {
        // BossHazard values are 0..4.
        const int HazardSlots = (int)BossHazard.SpikeWall + 1;

        readonly int[] hitsByHazard = new int[HazardSlots];

        public int pickups;
        public int rocketsFired;
        public int boltsFired;
        public int boltsHit;
        public int minionsCleared;
        public int minionsFailed;
        public int clapsSurvived;
        /// <summary>Play time in seconds (scaled time, cutscenes included), summed across continues.</summary>
        public float elapsedSeconds;
        /// <summary>Hits taken before a continue (the save keeps only the total).</summary>
        public int restoredHits;

        public int HitsTaken
        {
            get
            {
                var total = restoredHits;
                foreach (var hits in hitsByHazard) total += hits;
                return total;
            }
        }

        /// <summary>Laser bolts on target / fired (0 before the first bolt).</summary>
        public float Accuracy => boltsFired > 0 ? (float)boltsHit / boltsFired : 0f;

        public void AddHit(BossHazard hazard)
        {
            var slot = (int)hazard;
            if (slot >= 0 && slot < hitsByHazard.Length) hitsByHazard[slot]++;
        }

        public int HitsBy(BossHazard hazard)
        {
            var slot = (int)hazard;
            return slot >= 0 && slot < hitsByHazard.Length ? hitsByHazard[slot] : 0;
        }

        public void Reset()
        {
            for (var i = 0; i < hitsByHazard.Length; i++) hitsByHazard[i] = 0;
            pickups = 0;
            rocketsFired = 0;
            boltsFired = 0;
            boltsHit = 0;
            minionsCleared = 0;
            minionsFailed = 0;
            clapsSurvived = 0;
            elapsedSeconds = 0f;
            restoredHits = 0;
        }
    }
}
