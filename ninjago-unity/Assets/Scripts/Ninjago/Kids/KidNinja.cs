#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The block ninja of the children's games, seen from behind and facing +Z. Its arms copy the player's arms as the
    /// camera sees them (shoulder-to-wrist direction), so a child sees the reading live; with no reading they hang. It
    /// can hop, wobble dizzy, get shoved back, and swing its legs while it walks.
    /// </summary>
    public class KidNinja : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Everything above the legs; hops, wobbles and gets shoved.")]
        [SerializeField] Transform body = null!;
        [Header("Left Arm")]
        [Tooltip("Shoulder pivot with the arm hanging along -Y.")]
        [SerializeField] Transform leftArm = null!;
        [Header("Right Arm")]
        [SerializeField] Transform rightArm = null!;
        [Header("Left Leg")]
        [SerializeField] Transform leftLeg = null!;
        [Header("Right Leg")]
        [SerializeField] Transform rightLeg = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Head Point")]
        [Tooltip("Top of the hood: dizzy stars and brick hats go here.")]
        [SerializeField] Transform headPoint = null!;
        [Header("Front Point")]
        [Tooltip("In front of the face, where a shield is held and an incoming brick aims.")]
        [SerializeField] Transform frontPoint = null!;
        [Header("Arm Follow Rate")]
        [Tooltip("How quickly the arms catch up with the player's arms (per second).")]
        [SerializeField] float armFollowRate = 14f;
        [Header("Arm Forward Tilt")]
        [Tooltip("Degrees the arms lean forward, so from behind they stay clear of the body.")]
        [SerializeField] float armForwardTilt = 22f;
        [Header("Stride Seconds")]
        [SerializeField] float strideSeconds = 0.4f;

        Quaternion leftTarget;
        Quaternion rightTarget;
        Vector3 bodyRest;
        float hopElapsed = float.PositiveInfinity;
        float hopSeconds;
        float hopHeight;
        float wobbleLeft;
        float shoveElapsed = float.PositiveInfinity;
        float shoveMeters;
        bool stepping;
        float stepCycle;

        public Vector3 HeadPosition => headPoint.position;
        public Vector3 FrontPosition => frontPoint.position;
        public Transform HeadPoint => headPoint;
        public Transform FrontPoint => frontPoint;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers) colorRenderer.material.color = color;
            marker.Initialize(playerIndex, color);
            bodyRest = body.localPosition;
            ReleaseArms();
            leftArm.localRotation = leftTarget;
            rightArm.localRotation = rightTarget;
        }

        #endregion

        #region Public API

        /// <summary>Points each arm the way the player's arm points (x = the player's right, matching the ninja's right from behind).</summary>
        public void CopyArms(in UpperBody pose)
        {
            leftTarget = ArmRotation(pose.leftWrist - pose.leftShoulder, -1f);
            rightTarget = ArmRotation(pose.rightWrist - pose.rightShoulder, 1f);
        }

        public void ReleaseArms()
        {
            leftTarget = ArmRotation(new Vector2(-0.15f, -1f), -1f);
            rightTarget = ArmRotation(new Vector2(0.15f, -1f), 1f);
        }

        public void Hop(float height, float seconds)
        {
            hopHeight = height;
            hopSeconds = seconds;
            hopElapsed = 0f;
        }

        public void Wobble(float seconds)
        {
            wobbleLeft = seconds;
        }

        /// <summary>Slides back by meters and returns, like a hit taken on a shield.</summary>
        public void Shove(float meters)
        {
            shoveMeters = meters;
            shoveElapsed = 0f;
        }

        public void SetStepping(bool isStepping)
        {
            stepping = isStepping;
        }

        #endregion

        #region Animation

        void Update()
        {
            var deltaTime = Time.deltaTime;
            var follow = 1f - Mathf.Exp(-armFollowRate * deltaTime);
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftTarget, follow);
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightTarget, follow);

            hopElapsed += deltaTime;
            var hop = hopElapsed < hopSeconds ? hopHeight * Mathf.Sin(hopElapsed / hopSeconds * Mathf.PI) : 0f;
            shoveElapsed += deltaTime;
            var shove = shoveElapsed < 0.45f ? -shoveMeters * Mathf.Sin(Mathf.Clamp01(shoveElapsed / 0.45f) * Mathf.PI) : 0f;
            wobbleLeft = Mathf.Max(0f, wobbleLeft - deltaTime);
            var wobble = wobbleLeft > 0f ? Mathf.Sin(Time.time * 14f) * 14f * Mathf.Min(1f, wobbleLeft) : 0f;
            body.localPosition = bodyRest + new Vector3(0f, hop, shove);
            body.localRotation = Quaternion.Euler(0f, 0f, wobble);

            stepCycle = stepping ? stepCycle + deltaTime / strideSeconds * Mathf.PI * 2f : 0f;
            var swing = stepping ? Mathf.Sin(stepCycle) * 30f : 0f;
            leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }

        // An arm hanging along -Y turned about Z to point along direction; side -1 left, 1 right (only for a zero direction).
        Quaternion ArmRotation(Vector2 direction, float side)
        {
            if (direction.sqrMagnitude < 0.25f) direction = new Vector2(0.15f * side, -1f);
            var angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            // Lean forward first, then swing about Z: the lean stays forward whichever way the arm points.
            return Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(-armForwardTilt, 0f, 0f);
        }

        #endregion
    }
}
