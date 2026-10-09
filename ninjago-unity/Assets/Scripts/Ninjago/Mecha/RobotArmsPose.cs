#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The robot arms rule, judged from hand positions against the torso in frame space (y up): elbows down by the sides,
    /// forearms bent up in front. A hand that sinks closer to the hips than to the chest is slacking.
    /// </summary>
    public static class RobotArmsPose
    {
        /// <summary>How far a hand has sunk down the torso: 0 at the chest, 1 at the hips; negative above the chest.</summary>
        public static float HandDrop(float handY, float chestY, float hipY) => (chestY - handY) / Mathf.Max(chestY - hipY, 1e-5f);

        /// <summary>The hand is up in front: above slackLine (0 chest, 1 hips; 0.5 = closer to the chest than to the hips).</summary>
        public static bool IsRaised(float handY, float chestY, float hipY, float slackLine) => HandDrop(handY, chestY, hipY) < slackLine;

        /// <summary>
        /// Where the hands point relative to the body: x is the hands' midpoint from the shoulders' midpoint, y is the
        /// hands' midpoint from the chest. The whole body swaying moves neither.
        /// </summary>
        public static Vector2 BodyOffset(Vector2 leftHand, Vector2 rightHand, Vector2 leftShoulder, Vector2 rightShoulder, Vector2 chest)
        {
            var hands = (leftHand + rightHand) * 0.5f;
            return new Vector2(hands.x - (leftShoulder.x + rightShoulder.x) * 0.5f, hands.y - chest.y);
        }
    }
}
