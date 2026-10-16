#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The brick click, read from a flat camera: the two wrists come together in front of the body, like pressing two
    /// bricks together (a clap), after having been apart. Distances are in shoulder widths, so player size and distance
    /// cancel out. Hands that come close but not together and then part again are a near clap: a try the reading did
    /// not count. The hands must part again before the next click.
    /// </summary>
    public sealed class ClapReader
    {
        public enum Event
        {
            None,
            Clap,
            NearClap,
        }

        public readonly struct Settings
        {
            public readonly float togetherRatio;
            public readonly float apartRatio;
            public readonly float nearRatio;
            public readonly float minIntervalSeconds;
            public readonly float lowestWrist;

            public Settings(float togetherRatio, float apartRatio, float nearRatio, float minIntervalSeconds, float lowestWrist)
            {
                this.togetherRatio = togetherRatio;
                this.apartRatio = apartRatio;
                this.nearRatio = nearRatio;
                this.minIntervalSeconds = minIntervalSeconds;
                this.lowestWrist = lowestWrist;
            }
        }

        bool armed;
        float closest = float.PositiveInfinity;
        float lastClap = float.NegativeInfinity;

        #region Public API

        public Event Update(in UpperBody pose, float time, in Settings settings)
        {
            var distance = (pose.leftWrist - pose.rightWrist).magnitude / pose.ShoulderWidth;
            if (distance >= settings.apartRatio)
            {
                var near = armed && closest <= settings.nearRatio;
                armed = true;
                closest = float.PositiveInfinity;
                return near ? Event.NearClap : Event.None;
            }

            // Hands hanging low together (or out of view below) are not pressing bricks.
            if (!armed || pose.leftWrist.y < settings.lowestWrist || pose.rightWrist.y < settings.lowestWrist) return Event.None;
            closest = Mathf.Min(closest, distance);
            if (distance > settings.togetherRatio || time - lastClap < settings.minIntervalSeconds) return Event.None;
            armed = false;
            lastClap = time;
            closest = float.PositiveInfinity;
            return Event.Clap;
        }

        public void Reset()
        {
            armed = false;
            closest = float.PositiveInfinity;
        }

        #endregion
    }
}
