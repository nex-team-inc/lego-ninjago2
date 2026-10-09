#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Maps the averaged hands' offset from their calibrated origin to the mecha's desired yaw and pitch.</summary>
    public static class MechaAim
    {
        public readonly struct Settings
        {
            public readonly float deadzoneInches;
            public readonly float fullYawOffsetInches;
            public readonly float yawRangeDegrees;
            public readonly float fullPitchOffsetInches;
            public readonly float pitchClampDegrees;
            public readonly bool swapAxes;

            public Settings(float deadzoneInches, float fullYawOffsetInches, float yawRangeDegrees, float fullPitchOffsetInches, float pitchClampDegrees,
                bool swapAxes)
            {
                this.deadzoneInches = deadzoneInches;
                this.fullYawOffsetInches = fullYawOffsetInches;
                this.yawRangeDegrees = yawRangeDegrees;
                this.fullPitchOffsetInches = fullPitchOffsetInches;
                this.pitchClampDegrees = pitchClampDegrees;
                this.swapAxes = swapAxes;
            }
        }

        /// <summary>
        /// Desired yaw (x, + turns right) and pitch (y, + aims up) in degrees for a hand offset in body inches
        /// (x = the player's right, y up). Inside the deadzone an axis asks for 0.
        /// </summary>
        public static Vector2 Desired(Vector2 offsetInches, in Settings settings)
        {
            var turn = settings.swapAxes ? offsetInches.y : offsetInches.x;
            var tilt = settings.swapAxes ? offsetInches.x : offsetInches.y;
            return new Vector2(
                Shape(turn, settings.deadzoneInches, settings.fullYawOffsetInches) * settings.yawRangeDegrees,
                Shape(tilt, settings.deadzoneInches, settings.fullPitchOffsetInches) * settings.pitchClampDegrees);
        }

        static float Shape(float offset, float deadzone, float fullOffset) => Mathf.Sign(offset) * Mathf.Clamp01((Mathf.Abs(offset) - deadzone) / fullOffset);
    }
}
