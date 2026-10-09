#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The L rule for one arm, judged from its shoulder, elbow and wrist in frame space (y up).</summary>
    public static class RobotArmsPose
    {
        /// <summary>Degrees between the upper arm (elbow to shoulder) and the forearm (elbow to wrist); 180 is a straight arm.</summary>
        public static float ElbowAngle(Vector2 shoulder, Vector2 elbow, Vector2 wrist) => Vector2.Angle(shoulder - elbow, wrist - elbow);

        /// <summary>
        /// The arm holds the L: the elbow bends near a right angle and the wrist does not hang below the elbow.
        /// wristBelowElbow is the drop allowed, in the same units as the positions.
        /// </summary>
        public static bool IsArmL(Vector2 shoulder, Vector2 elbow, Vector2 wrist, float minAngle, float maxAngle, float wristBelowElbow)
        {
            var angle = ElbowAngle(shoulder, elbow, wrist);
            return angle >= minAngle && angle <= maxAngle && wrist.y >= elbow.y - wristBelowElbow;
        }
    }
}
