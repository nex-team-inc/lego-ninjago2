#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's upper body for gesture readers. On each new body frame it gives the median of the last three
    /// frames, so one jittery or glitched frame never reads as a gesture; a gap in tracking starts over. The simulated
    /// body stands in with fixed poses: hands together for a moment after its knee pulse, a shield guard while its storm
    /// key is held, and a hand raised high for a moment after a punch key.
    /// </summary>
    public sealed class UpperBodyStream
    {
        const float maxGapSeconds = 0.3f;
        const float simulatedClapSeconds = 0.15f;
        const float simulatedCallSeconds = 0.35f;

        readonly PlayerBody body;
        readonly UpperBody[] recent = new UpperBody[3];
        readonly int[] seenPunches = new int[2];
        readonly float[] punchedAt = { float.NegativeInfinity, float.NegativeInfinity };
        int recentCount;
        double lastFrame = double.NaN;
        float lastTime = float.NegativeInfinity;
        int seenKnees;
        float kneeAt = float.NegativeInfinity;

        public UpperBodyStream(PlayerBody body)
        {
            this.body = body;
            seenKnees = SimulatedBody.GetKneePulses(body.PlayerIndex);
            for (var hand = 0; hand < 2; hand++) seenPunches[hand] = SimulatedBody.GetPunchPulses(body.PlayerIndex, hand);
        }

        #region Public API

        /// <summary>True when a new filtered pose is ready (every frame for the simulated body).</summary>
        public bool TryRead(float time, out UpperBody pose)
        {
            if (SimulatedBody.IsEnabled)
            {
                pose = Simulated(time);
                return true;
            }

            pose = default;
            if (body.BodyFrameTime == lastFrame) return false;
            lastFrame = body.BodyFrameTime;
            if (!body.TrySampleUpperBody(out var raw)) return false;
            if (time - lastTime > maxGapSeconds) recentCount = 0;
            lastTime = time;
            recent[recentCount % 3] = raw;
            recentCount++;
            if (recentCount < 3) return false;
            pose = UpperBody.Median(recent[0], recent[1], recent[2]);
            return true;
        }

        #endregion

        #region Simulated

        UpperBody Simulated(float time)
        {
            var playerIndex = body.PlayerIndex;
            var knees = SimulatedBody.GetKneePulses(playerIndex);
            if (knees != seenKnees)
            {
                seenKnees = knees;
                kneeAt = time;
            }

            for (var hand = 0; hand < 2; hand++)
            {
                var punches = SimulatedBody.GetPunchPulses(playerIndex, hand);
                if (punches == seenPunches[hand]) continue;
                seenPunches[hand] = punches;
                punchedAt[hand] = time;
            }

            var pose = UpperBody.Rest;
            if (time - kneeAt < simulatedClapSeconds) return pose.WithWrists(new Vector2(-1f, 0f), new Vector2(1f, 0f));
            if (SimulatedBody.IsStorming(playerIndex)) return pose.WithWrists(new Vector2(-3f, 8f), new Vector2(3f, 8f));
            var left = time - punchedAt[0] < simulatedCallSeconds ? new Vector2(-6f, 17f) : pose.leftWrist;
            var right = time - punchedAt[1] < simulatedCallSeconds ? new Vector2(6f, 17f) : pose.rightWrist;
            return pose.WithWrists(left, right);
        }

        #endregion
    }
}
