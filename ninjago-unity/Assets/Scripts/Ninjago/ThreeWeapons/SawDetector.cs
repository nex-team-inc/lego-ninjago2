#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// The sword's slash for one arm: the hand crossing that arm's shoulder x, either way, fast enough. It fires on the
    /// crossing frame; the only cooldown is a tiny cap against counting one crossing twice, so sawing can be frantic.
    /// </summary>
    public sealed class SawDetector
    {
        bool hasSample;
        float lastX;
        float lastTime;
        float lastSlashTime = float.NegativeInfinity;

        #region Public API

        /// <param name="handX">That hand's x, in the same space and units as shoulderX (body inches).</param>
        public bool Update(float handX, float shoulderX, float time, float minSpeed, float cooldownSeconds)
        {
            var side = handX - shoulderX;
            var slash = false;
            if (hasSample && time > lastTime)
            {
                var lastSide = lastX - shoulderX;
                var crossed = (side >= 0f) != (lastSide >= 0f);
                var speed = System.Math.Abs((handX - lastX) / (time - lastTime));
                if (crossed && speed >= minSpeed && time - lastSlashTime >= cooldownSeconds)
                {
                    slash = true;
                    lastSlashTime = time;
                }
            }

            hasSample = true;
            lastX = handX;
            lastTime = time;
            return slash;
        }

        /// <summary>The hand was lost: the next sample starts fresh instead of reading a jump as a slash.</summary>
        public void Reset()
        {
            hasSample = false;
        }

        #endregion
    }
}
