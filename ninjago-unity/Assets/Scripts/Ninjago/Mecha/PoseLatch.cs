#nullable enable

namespace Nex.Ninjago
{
    /// <summary>Debounced "both arms hold the L": the raw reading must stay changed for a moment before the held state flips.</summary>
    public sealed class PoseLatch
    {
        public enum Change
        {
            None,
            Gained,
            Lost,
        }

        bool candidate;
        float candidateSince;

        public bool Held { get; private set; }

        #region Public API

        public Change Update(bool raw, float time, float gainSeconds, float lossSeconds)
        {
            if (raw != candidate)
            {
                candidate = raw;
                candidateSince = time;
            }

            if (candidate == Held || time - candidateSince < (candidate ? gainSeconds : lossSeconds)) return Change.None;
            Held = candidate;
            return Held ? Change.Gained : Change.Lost;
        }

        /// <summary>Drops the pose at once (the player went down or the fight ended).</summary>
        public void Release()
        {
            Held = false;
            candidate = false;
        }

        #endregion
    }
}
