#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Shoulders, wrists and head against the chest in body inches (x = the player's right, y up), as a flat camera sees them.</summary>
    public readonly struct UpperBody
    {
        public readonly Vector2 leftShoulder;
        public readonly Vector2 rightShoulder;
        public readonly Vector2 leftWrist;
        public readonly Vector2 rightWrist;
        public readonly Vector2 head;

        public UpperBody(Vector2 leftShoulder, Vector2 rightShoulder, Vector2 leftWrist, Vector2 rightWrist, Vector2 head)
        {
            this.leftShoulder = leftShoulder;
            this.rightShoulder = rightShoulder;
            this.leftWrist = leftWrist;
            this.rightWrist = rightWrist;
            this.head = head;
        }

        public float ShoulderY => (leftShoulder.y + rightShoulder.y) * 0.5f;
        /// <summary>Never below 6 inches, so a turned body does not divide by almost nothing.</summary>
        public float ShoulderWidth => Mathf.Max(6f, Mathf.Abs(rightShoulder.x - leftShoulder.x));

        /// <param name="hand">0 = left, 1 = right.</param>
        public Vector2 Wrist(int hand) => hand == 0 ? leftWrist : rightWrist;

        public Vector2 Shoulder(int hand) => hand == 0 ? leftShoulder : rightShoulder;

        /// <summary>Adult proportions with the arms hanging.</summary>
        public static UpperBody Rest => new(new Vector2(-7f, 2f), new Vector2(7f, 2f), new Vector2(-8f, -20f), new Vector2(8f, -20f), new Vector2(0f, 11f));

        public UpperBody WithWrists(Vector2 left, Vector2 right) => new(leftShoulder, rightShoulder, left, right, head);

        /// <summary>Each point's per-axis median of three frames.</summary>
        public static UpperBody Median(in UpperBody a, in UpperBody b, in UpperBody c) => new(Median(a.leftShoulder, b.leftShoulder, c.leftShoulder),
            Median(a.rightShoulder, b.rightShoulder, c.rightShoulder), Median(a.leftWrist, b.leftWrist, c.leftWrist),
            Median(a.rightWrist, b.rightWrist, c.rightWrist), Median(a.head, b.head, c.head));

        static Vector2 Median(Vector2 a, Vector2 b, Vector2 c) => new(Median(a.x, b.x, c.x), Median(a.y, b.y, c.y));

        static float Median(float a, float b, float c) => Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
    }
}
