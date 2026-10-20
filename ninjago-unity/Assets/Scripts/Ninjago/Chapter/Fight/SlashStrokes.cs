#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slash strokes from one player's arms, in any direction: a stroke is a wrist (against the chest, so leaning does
    /// not count) speeding up past the slash speed, at most once per stroke gap per hand. A slashing frenzy is many
    /// strokes in a row; a resting or slowly raised hand never strokes. Samples once per camera frame, so speed is distance
    /// over capture time. The simulated body slashes with its punch keys.
    /// </summary>
    public sealed class SlashStrokes
    {
        readonly Vector2[] lastWrist = new Vector2[2];
        readonly bool[] hasLast = new bool[2];
        readonly bool[] fast = new bool[2];
        readonly float[] lastStroke = new float[2];
        readonly int[] seenPunches = new int[2];
        double lastFrameTime = -1d;

        /// <summary>The fastest wrist speed of the last camera frame, body inches per second.</summary>
        public float LastSpeed { get; private set; }

        #region Public API

        public void Begin(PlayerBody body)
        {
            for (var hand = 0; hand < 2; hand++)
            {
                hasLast[hand] = false;
                fast[hand] = false;
                lastStroke[hand] = float.NegativeInfinity;
                seenPunches[hand] = SimulatedBody.GetPunchPulses(body.PlayerIndex, hand);
            }

            lastFrameTime = -1d;
        }

        /// <returns>Strokes that started this frame (0, 1 or 2).</returns>
        public int Poll(PlayerBody body, float speedThreshold, float strokeGapSeconds)
        {
            if (SimulatedBody.IsEnabled) return PollSimulated(body.PlayerIndex);
            var frameTime = body.BodyFrameTime;
            if (frameTime <= lastFrameTime) return 0;
            var deltaTime = lastFrameTime < 0d ? 0f : (float)(frameTime - lastFrameTime);
            lastFrameTime = frameTime;
            LastSpeed = 0f;
            var strokes = 0;
            for (var hand = 0; hand < 2; hand++)
            {
                if (!body.TryGetArmInches(hand == 0, out _, out _, out var wrist))
                {
                    hasLast[hand] = false;
                    fast[hand] = false;
                    continue;
                }

                var speed = hasLast[hand] && deltaTime > 0f ? Vector2.Distance(wrist, lastWrist[hand]) / deltaTime : 0f;
                lastWrist[hand] = wrist;
                hasLast[hand] = true;
                LastSpeed = Mathf.Max(LastSpeed, speed);
                var wasFast = fast[hand];
                // Hysteresis: a stroke ends below half the slash speed, so one fast swing is one stroke.
                fast[hand] = wasFast ? speed > speedThreshold * 0.5f : speed >= speedThreshold;
                if (!fast[hand] || wasFast || (float)frameTime - lastStroke[hand] < strokeGapSeconds) continue;
                lastStroke[hand] = (float)frameTime;
                strokes++;
            }

            return strokes;
        }

        #endregion

        #region Helpers

        int PollSimulated(int playerIndex)
        {
            var strokes = 0;
            for (var hand = 0; hand < 2; hand++)
            {
                var pulses = SimulatedBody.GetPunchPulses(playerIndex, hand);
                strokes += pulses - seenPunches[hand];
                seenPunches[hand] = pulses;
            }

            return strokes;
        }

        #endregion
    }
}
