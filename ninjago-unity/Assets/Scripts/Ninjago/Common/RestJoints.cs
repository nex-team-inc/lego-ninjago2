#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A player's shoulders, wrists and hip center at the setup hold, each against the chest in body inches (x = the player's right, y up).</summary>
    public readonly struct RestJoints
    {
        public readonly Vector2 leftShoulder;
        public readonly Vector2 rightShoulder;
        public readonly Vector2 leftWrist;
        public readonly Vector2 rightWrist;
        public readonly Vector2 hips;

        public RestJoints(Vector2 leftShoulder, Vector2 rightShoulder, Vector2 leftWrist, Vector2 rightWrist, Vector2 hips)
        {
            this.leftShoulder = leftShoulder;
            this.rightShoulder = rightShoulder;
            this.leftWrist = leftWrist;
            this.rightWrist = rightWrist;
            this.hips = hips;
        }

        /// <summary>Adult proportions with the arms hanging, for a setup that never saw these joints (the simulated body).</summary>
        public static RestJoints Typical => new(new Vector2(-7f, 2f), new Vector2(7f, 2f), new Vector2(-9f, -20f), new Vector2(9f, -20f), new Vector2(0f, -18f));

        public float ArmLength(bool left) => left ? (leftWrist - leftShoulder).magnitude : (rightWrist - rightShoulder).magnitude;

        public RestJoints Plus(RestJoints other) => new(leftShoulder + other.leftShoulder, rightShoulder + other.rightShoulder, leftWrist + other.leftWrist,
            rightWrist + other.rightWrist, hips + other.hips);

        public RestJoints Scaled(float factor) => new(leftShoulder * factor, rightShoulder * factor, leftWrist * factor, rightWrist * factor, hips * factor);
    }
}
