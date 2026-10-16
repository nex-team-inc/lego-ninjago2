#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The block ninja seen from behind, facing +Z down the street. A punch snaps that arm straight forward and long on
    /// the same frame, holds it, and pulls it back; the other arm stays down. Pinned, ropes wrap the body and the body
    /// sways with the player's hips.
    /// </summary>
    public class StreetNinja : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Everything above the hips; twists into a punch and sways with the hips while pinned.")]
        [SerializeField] Transform body = null!;
        [Header("Left Arm")]
        [Tooltip("Shoulder pivot with the arm hanging along -Y.")]
        [SerializeField] Transform leftArm = null!;
        [Header("Right Arm")]
        [SerializeField] Transform rightArm = null!;
        [Header("Left Fist")]
        [SerializeField] Transform leftFist = null!;
        [Header("Right Fist")]
        [SerializeField] Transform rightFist = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Ropes")]
        [Tooltip("Shown while pinned.")]
        [SerializeField] GameObject ropes = null!;
        [Header("Seat")]
        [Tooltip("Where a pinning monster sits, on the shoulders.")]
        [SerializeField] Transform seat = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Rest Arm Euler")]
        [Tooltip("Left arm at rest; the right arm mirrors it.")]
        [SerializeField] Vector3 restArmEuler = new(-14f, 0f, -7f);
        [Header("Punch Arm Euler")]
        [Tooltip("Left arm at full punch: forward, out to its side and a little up, so from behind the glove clears the body.")]
        [SerializeField] Vector3 punchArmEuler = new(-105f, 0f, -24f);
        [Header("Punch Arm Stretch")]
        [Tooltip("Arm length multiplier at full punch, so the strike reads from behind.")]
        [SerializeField] float punchStretch = 1.6f;
        [Header("Punch Twist Degrees")]
        [SerializeField] float punchTwistDegrees = 14f;
        [Header("Punch Hold Seconds")]
        [SerializeField] float punchHoldSeconds = 0.16f;
        [Header("Punch Return Seconds")]
        [SerializeField] float punchReturnSeconds = 0.2f;
        [Header("Sway Meters Per Inch")]
        [SerializeField] float swayMetersPerInch = 0.03f;
        [Header("Max Sway Meters")]
        [SerializeField] float maxSwayMeters = 0.3f;

        readonly float[] punchElapsed = { float.PositiveInfinity, float.PositiveInfinity };
        Vector3 bodyRest;
        float sway;

        public Transform Seat => seat;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers) colorRenderer.material.color = color;
            marker.Initialize(playerIndex, color);
            bodyRest = body.localPosition;
            ropes.SetActive(false);
            Pose();
        }

        #endregion

        #region Public API

        /// <param name="hand">0 = left, 1 = right.</param>
        public void PlayPunch(int hand)
        {
            punchElapsed[hand] = 0f;
            Pose();
        }

        public Vector3 FistPosition(int hand) => hand == 0 ? leftFist.position : rightFist.position;

        public void SetPinned(bool pinned)
        {
            ropes.SetActive(pinned);
            if (!pinned) sway = 0f;
        }

        /// <summary>Hip offset from the setup hold in body inches; only x moves the ninja.</summary>
        public void SetSway(Vector2 hipInches)
        {
            sway = Mathf.Clamp(hipInches.x * swayMetersPerInch, -maxSwayMeters, maxSwayMeters);
        }

        #endregion

        #region Animation

        void Update()
        {
            punchElapsed[0] += Time.deltaTime;
            punchElapsed[1] += Time.deltaTime;
            Pose();
        }

        void Pose()
        {
            var left = Strike(punchElapsed[0]);
            var right = Strike(punchElapsed[1]);
            PoseArm(leftArm, left, 1f);
            PoseArm(rightArm, right, -1f);
            body.localPosition = bodyRest + new Vector3(sway, 0f, 0f);
            // A positive turn about Y swings the right shoulder back, so a right punch turns negative.
            body.localRotation = Quaternion.Euler(0f, (left - right) * punchTwistDegrees, -sway * 30f);
        }

        // 1 from the punch frame through the hold, easing back to 0.
        float Strike(float elapsed)
        {
            if (elapsed <= punchHoldSeconds) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (elapsed - punchHoldSeconds) / punchReturnSeconds);
        }

        // side: 1 for the left arm, -1 mirrors it for the right.
        void PoseArm(Transform arm, float strike, float side)
        {
            var rest = new Vector3(restArmEuler.x, restArmEuler.y * side, restArmEuler.z * side);
            var punch = new Vector3(punchArmEuler.x, punchArmEuler.y * side, punchArmEuler.z * side);
            arm.localRotation = Quaternion.Slerp(Quaternion.Euler(rest), Quaternion.Euler(punch), strike);
            arm.localScale = new Vector3(1f, Mathf.Lerp(1f, punchStretch, strike), 1f);
        }

        #endregion
    }
}
