#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The shuriken's throw for one hand: the hand is cocked behind its shoulder (wound back well outside it in the camera
    /// picture, not hanging), then crosses in front of that shoulder toward the body's middle, fast. One flick, one
    /// throw: the hand must cock again before the next.
    /// </summary>
    public sealed class FlickDetector
    {
        public readonly struct Settings
        {
            public readonly float cockOutside;
            public readonly float cockBelowShoulder;
            public readonly float minSpeed;
            public readonly float windowSeconds;

            public Settings(float cockOutside, float cockBelowShoulder, float minSpeed, float windowSeconds)
            {
                this.cockOutside = cockOutside;
                this.cockBelowShoulder = cockBelowShoulder;
                this.minSpeed = minSpeed;
                this.windowSeconds = windowSeconds;
            }
        }

        bool hasSample;
        float lastOutside;
        float lastTime;
        float cockedTime = float.NegativeInfinity;

        /// <summary>The hand is cocked and a fast crossing now would throw.</summary>
        public bool IsCocked { get; private set; }

        #region Public API

        /// <param name="hand">That hand in body inches (x = the player's right, y up).</param>
        /// <param name="shoulder">The same side's shoulder in the same space.</param>
        /// <param name="outward">-1 for the left hand (outside is further left), +1 for the right hand.</param>
        public bool Update(Vector2 hand, Vector2 shoulder, float outward, float time, in Settings settings)
        {
            var outside = (hand.x - shoulder.x) * outward;
            if (outside >= settings.cockOutside && hand.y >= shoulder.y - settings.cockBelowShoulder) cockedTime = time;
            IsCocked = time - cockedTime <= settings.windowSeconds;
            var thrown = false;
            if (hasSample && time > lastTime && lastOutside >= 0f && outside < 0f && IsCocked)
            {
                var inwardSpeed = (lastOutside - outside) / (time - lastTime);
                if (inwardSpeed >= settings.minSpeed)
                {
                    thrown = true;
                    cockedTime = float.NegativeInfinity;
                    IsCocked = false;
                }
            }

            hasSample = true;
            lastOutside = outside;
            lastTime = time;
            return thrown;
        }

        public void Reset()
        {
            hasSample = false;
            cockedTime = float.NegativeInfinity;
            IsCocked = false;
        }

        #endregion
    }
}
