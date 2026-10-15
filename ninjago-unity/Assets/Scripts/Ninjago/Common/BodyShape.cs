#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A player's body at the setup hold: each point relative to the chest in body inches (x = the player's right, y up).</summary>
    public readonly struct BodyShape
    {
        public readonly Vector2 leftShoulder;
        public readonly Vector2 rightShoulder;
        public readonly Vector2 head;
        public readonly Vector2 leftHand;
        public readonly Vector2 rightHand;

        public BodyShape(Vector2 leftShoulder, Vector2 rightShoulder, Vector2 head, Vector2 leftHand, Vector2 rightHand)
        {
            this.leftShoulder = leftShoulder;
            this.rightShoulder = rightShoulder;
            this.head = head;
            this.leftHand = leftHand;
            this.rightHand = rightHand;
        }

        /// <summary>Adult proportions, for a setup that never saw these nodes (the simulated body).</summary>
        public static BodyShape Typical => new(new Vector2(-7f, 2f), new Vector2(7f, 2f), new Vector2(0f, 11f), new Vector2(-8f, -20f), new Vector2(8f, -20f));

        public BodyShape Plus(BodyShape other) => new(leftShoulder + other.leftShoulder, rightShoulder + other.rightShoulder, head + other.head,
            leftHand + other.leftHand, rightHand + other.rightHand);

        public BodyShape Scaled(float factor) => new(leftShoulder * factor, rightShoulder * factor, head * factor, leftHand * factor, rightHand * factor);
    }
}
