#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A slash from one player's arms: either wrist crosses a circle centered on the middle of the shoulder line (radius
    /// a share of the shoulder span) fast, read by Stone Kick's SlashDetector in body inches. Samples once per camera
    /// frame, so speed is distance over capture time. A resting or slowly raised hand never slashes. The simulated body
    /// slashes with its punch keys.
    /// </summary>
    public sealed class ShoulderSlash
    {
        readonly SlashDetector[] detectors = { new(), new() };
        readonly Vector2[] lastWrist = new Vector2[2];
        readonly bool[] hasLast = new bool[2];
        readonly int[] continuity = new int[2];
        readonly int[] seenPunches = new int[2];
        double lastFrameTime = -1d;

        /// <summary>The fastest wrist speed of the last frame, body inches per second.</summary>
        public float LastSpeed { get; private set; }

        #region Public API

        public void Begin(PlayerBody body)
        {
            for (var hand = 0; hand < 2; hand++)
            {
                detectors[hand].Reset();
                hasLast[hand] = false;
                continuity[hand]++;
                seenPunches[hand] = SimulatedBody.GetPunchPulses(body.PlayerIndex, hand);
            }

            lastFrameTime = -1d;
        }

        /// <returns>True on the frame a wrist leaves the circle after a fast crossing.</returns>
        public bool Poll(PlayerBody body, float speedThreshold, float maxCrossSeconds, float radiusShoulders)
        {
            if (SimulatedBody.IsEnabled) return PollSimulated(body.PlayerIndex);
            var frameTime = body.BodyFrameTime;
            if (frameTime <= lastFrameTime) return false;
            var deltaTime = lastFrameTime < 0d ? 0f : (float)(frameTime - lastFrameTime);
            lastFrameTime = frameTime;
            LastSpeed = 0f;
            var slashed = false;
            for (var hand = 0; hand < 2; hand++)
            {
                var left = hand == 0;
                if (!body.TryGetArmInches(left, out var shoulder, out var otherShoulder, out var wrist))
                {
                    hasLast[hand] = false;
                    continuity[hand]++;
                    continue;
                }

                var speed = hasLast[hand] && deltaTime > 0f ? Vector2.Distance(wrist, lastWrist[hand]) / deltaTime : 0f;
                lastWrist[hand] = wrist;
                hasLast[hand] = true;
                LastSpeed = Mathf.Max(LastSpeed, speed);
                var center = (shoulder + otherShoulder) * 0.5f;
                var radius = Vector2.Distance(shoulder, otherShoulder) * radiusShoulders;
                slashed |= detectors[hand].Update(wrist, speed, continuity[hand], center, radius, (float)frameTime, speedThreshold, maxCrossSeconds);
            }

            return slashed;
        }

        #endregion

        #region Helpers

        bool PollSimulated(int playerIndex)
        {
            var slashed = false;
            for (var hand = 0; hand < 2; hand++)
            {
                var pulses = SimulatedBody.GetPunchPulses(playerIndex, hand);
                if (pulses == seenPunches[hand]) continue;
                seenPunches[hand] = pulses;
                slashed = true;
            }

            return slashed;
        }

        #endregion
    }
}
