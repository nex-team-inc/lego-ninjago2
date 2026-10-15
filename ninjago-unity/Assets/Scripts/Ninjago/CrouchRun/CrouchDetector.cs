#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// Crouch from the chest's drop below its calibrated standing height, with hysteresis (crouch past one line, stand
    /// only above a higher one) and a short hold, so a nod or a flicker around the line never changes the stance.
    /// </summary>
    public sealed class CrouchDetector
    {
        bool candidate;
        float candidateSince;

        public bool IsCrouching { get; private set; }

        #region Public API

        /// <param name="dropInches">Body inches the chest sits below its standing height (positive = lower).</param>
        public bool Update(float dropInches, float time, float crouchDropInches, float standDropInches, float holdSeconds)
        {
            var raw = IsCrouching ? dropInches > standDropInches : dropInches >= crouchDropInches;
            if (raw != candidate)
            {
                candidate = raw;
                candidateSince = time;
            }

            if (candidate != IsCrouching && time - candidateSince >= holdSeconds) IsCrouching = candidate;
            return IsCrouching;
        }

        #endregion
    }
}
