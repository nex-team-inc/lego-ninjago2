#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The call, read from a flat camera: a hand raised high, above the head ("pick me!"). Either hand counts; the higher
    /// wrist is the one read, so both hands up is one call. After a call the hand must come back down below the try line
    /// before the next one. A hand that comes up past the try line (around the chin) and goes back down without getting
    /// above the head is a missed call: a try the reading did not take.
    /// </summary>
    public sealed class CallReader
    {
        public enum Event
        {
            None,
            Call,
            Missed,
        }

        public readonly struct Settings
        {
            public readonly float aboveHead;
            public readonly float tryAboveShoulder;

            public Settings(float aboveHead, float tryAboveShoulder)
            {
                this.aboveHead = aboveHead;
                this.tryAboveShoulder = tryAboveShoulder;
            }
        }

        bool armed = true;
        bool reaching;

        /// <summary>The hand of the last call or missed call: 0 = left, 1 = right.</summary>
        public int Hand { get; private set; }

        #region Public API

        public Event Update(in UpperBody pose, in Settings settings)
        {
            var hand = pose.rightWrist.y >= pose.leftWrist.y ? 1 : 0;
            var high = pose.Wrist(hand).y;
            if (high >= pose.head.y + settings.aboveHead)
            {
                reaching = false;
                if (!armed) return Event.None;
                armed = false;
                Hand = hand;
                return Event.Call;
            }

            if (high >= pose.ShoulderY + settings.tryAboveShoulder)
            {
                if (armed) reaching = true;
                Hand = hand;
                return Event.None;
            }

            armed = true;
            if (!reaching) return Event.None;
            reaching = false;
            return Event.Missed;
        }

        public void Reset()
        {
            armed = true;
            reaching = false;
        }

        #endregion
    }
}
