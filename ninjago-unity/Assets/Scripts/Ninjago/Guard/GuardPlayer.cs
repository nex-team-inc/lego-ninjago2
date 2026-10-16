#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>One active player in Guard: that player's ninja and shield, the guard reading and the run counts.</summary>
    public sealed class GuardPlayer
    {
        // Seconds without a body frame before the guard drops and the ninja's arms hang.
        const float lostSeconds = 0.6f;

        readonly int[] outcomes = new int[5];
        readonly UpperBodyStream stream;
        float lastPoseTime = float.NegativeInfinity;

        public PlayerBody Body { get; }
        public KidNinja Ninja { get; }
        public Transform Shield { get; }
        public Transform Target { get; }
        /// <summary>0 for the left ninja (or the only one), 1 for the right.</summary>
        public int Slot { get; }
        public GuardReader Reader { get; } = new();
        public float LastHalfAt { get; private set; } = float.NegativeInfinity;
        /// <summary>Guards read while nothing was coming at this player.</summary>
        public int AloneRaises { get; private set; }
        public int PlayerIndex => Body.PlayerIndex;
        public Color Color => Body.Color;
        public int Tries => outcomes[(int)GuardOutcome.Saved] + outcomes[(int)GuardOutcome.Late] + outcomes[(int)GuardOutcome.Held] +
            outcomes[(int)GuardOutcome.Half];

        /// <param name="target">Where bricks thrown at this player aim (in front of the face).</param>
        public GuardPlayer(PlayerBody body, KidNinja ninja, Transform shield, Transform target, int slot)
        {
            Body = body;
            Ninja = ninja;
            Shield = shield;
            Target = target;
            Slot = slot;
            stream = new UpperBodyStream(body);
        }

        #region Public API

        public int Count(GuardOutcome outcome) => outcomes[(int)outcome];

        public void Record(GuardOutcome outcome) => outcomes[(int)outcome]++;

        public void CountAlone() => AloneRaises++;

        /// <summary>Reads this frame's body (when there is a new one) into the ninja's arms and the guard.</summary>
        public GuardReader.Event Read(float time, in GuardReader.Settings settings)
        {
            if (stream.TryRead(time, out var pose))
            {
                lastPoseTime = time;
                Ninja.CopyArms(pose);
                var guardEvent = Reader.Update(pose, time, settings);
                if (guardEvent == GuardReader.Event.HalfRaised) LastHalfAt = time;
                return guardEvent;
            }

            if (time - lastPoseTime <= lostSeconds) return GuardReader.Event.None;
            Reader.Reset();
            Ninja.ReleaseArms();
            return GuardReader.Event.None;
        }

        /// <summary>What a throw this player did not block was, judged once the late window after the hit has passed.</summary>
        public static GuardOutcome ClassifyMiss(float impact, bool heldAtImpact, float raisedAt, float halfAt, float lateWindow, float halfWindow)
        {
            if (raisedAt > impact && raisedAt <= impact + lateWindow) return GuardOutcome.Late;
            if (heldAtImpact) return GuardOutcome.Held;
            if (halfAt >= impact - halfWindow && halfAt <= impact + lateWindow) return GuardOutcome.Half;
            return GuardOutcome.Missed;
        }

        #endregion
    }
}
