#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The shield guard, read from a flat camera: both wrists come up above the shoulder line and stay inside the
    /// body's width, like hands held up in front of the face. It goes down once a wrist drops well below the shoulders
    /// (a gap, so jitter does not flicker it). A guard is fresh for a short time after it goes up: holding the arms up
    /// does not keep blocking. One hand up, or both up but wide, is a half guard: a try the reading does not count.
    /// </summary>
    public sealed class GuardReader
    {
        public enum Event
        {
            None,
            Raised,
            Lowered,
            HalfRaised,
        }

        public readonly struct Settings
        {
            public readonly float minAboveShoulder;
            public readonly float maxOutsideShoulder;
            public readonly float lowerBelowShoulder;
            public readonly float freshSeconds;

            public Settings(float minAboveShoulder, float maxOutsideShoulder, float lowerBelowShoulder, float freshSeconds)
            {
                this.minAboveShoulder = minAboveShoulder;
                this.maxOutsideShoulder = maxOutsideShoulder;
                this.lowerBelowShoulder = lowerBelowShoulder;
                this.freshSeconds = freshSeconds;
            }
        }

        bool halfUp;

        public bool IsUp { get; private set; }
        public float RaisedAt { get; private set; } = float.NegativeInfinity;

        #region Public API

        /// <summary>The guard is up and was raised at most the fresh time ago.</summary>
        public bool IsFresh(float time, in Settings settings) => IsUp && time - RaisedAt <= settings.freshSeconds;

        public Event Update(in UpperBody pose, float time, in Settings settings)
        {
            if (IsUp)
            {
                var drop = pose.ShoulderY - settings.lowerBelowShoulder;
                if (pose.leftWrist.y >= drop && pose.rightWrist.y >= drop) return Event.None;
                IsUp = false;
                return Event.Lowered;
            }

            var line = pose.ShoulderY + settings.minAboveShoulder;
            var reach = pose.ShoulderWidth * 0.5f + settings.maxOutsideShoulder;
            var leftUp = pose.leftWrist.y >= line;
            var rightUp = pose.rightWrist.y >= line;
            if (leftUp && rightUp && Mathf.Abs(pose.leftWrist.x) <= reach && Mathf.Abs(pose.rightWrist.x) <= reach)
            {
                IsUp = true;
                halfUp = false;
                RaisedAt = time;
                return Event.Raised;
            }

            var half = leftUp || rightUp;
            if (half == halfUp) return Event.None;
            halfUp = half;
            return half ? Event.HalfRaised : Event.None;
        }

        /// <summary>Tracking lost: whatever was up is down.</summary>
        public void Reset()
        {
            IsUp = false;
            halfUp = false;
        }

        #endregion
    }
}
