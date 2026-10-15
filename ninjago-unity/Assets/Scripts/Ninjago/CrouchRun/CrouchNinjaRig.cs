#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The block ninja in profile, facing +X: legs and arms swing with the distance it covers (long strides upright,
    /// short ones crouched), the body sinks and leans forward to crouch, and it staggers when heard.
    /// </summary>
    public class CrouchNinjaRig : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Everything above the hips; sinks and leans forward to crouch.")]
        [SerializeField] Transform body = null!;
        [Header("Front Leg")]
        [SerializeField] Transform frontLeg = null!;
        [Header("Back Leg")]
        [SerializeField] Transform backLeg = null!;
        [Header("Front Arm")]
        [SerializeField] Transform frontArm = null!;
        [Header("Back Arm")]
        [SerializeField] Transform backArm = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Head Point")]
        [Tooltip("Where the HEARD effect plays.")]
        [SerializeField] Transform headPoint = null!;
        [Header("Crouch Sink")]
        [Tooltip("Meters the hips sink when crouched.")]
        [SerializeField] float crouchSink = 0.32f;
        [Header("Crouch Lean Degrees")]
        [SerializeField] float crouchLeanDegrees = 28f;
        [Header("Crouch Blend Seconds")]
        [SerializeField] float crouchBlendSeconds = 0.12f;
        [Header("Run Stride")]
        [Tooltip("Meters per full gait cycle while upright.")]
        [SerializeField] float runStrideMeters = 1.8f;
        [Header("Crouch Stride")]
        [SerializeField] float crouchStrideMeters = 0.9f;
        [Header("Heard Stagger Seconds")]
        [SerializeField] float staggerSeconds = 0.6f;

        Vector3 bodyRest;
        float crouchBlend;
        float cycle;
        float staggerElapsed = float.PositiveInfinity;

        public Vector3 HeadPosition => headPoint.position;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers)
            {
                colorRenderer.material.color = color;
            }

            marker.Initialize(playerIndex, color);
            bodyRest = body.localPosition;
        }

        #endregion

        #region Public API

        public void PlayHeard()
        {
            staggerElapsed = 0f;
        }

        /// <summary>Animates one frame; distance is how far the ninja moved this frame (meters).</summary>
        public void Tick(float distance, bool crouching, float deltaTime)
        {
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, deltaTime / crouchBlendSeconds);
            var stride = Mathf.Lerp(runStrideMeters, crouchStrideMeters, crouchBlend);
            cycle = (cycle + distance / stride * Mathf.PI * 2f) % (Mathf.PI * 2f);
            var moving = distance > 1e-4f ? 1f : 0f;
            var swing = Mathf.Sin(cycle) * Mathf.Lerp(42f, 22f, crouchBlend) * moving;
            var spread = Mathf.Lerp(0f, 38f, crouchBlend);
            // Rotating about +Z tips a limb's foot toward -X; the ninja faces +X.
            frontLeg.localRotation = Quaternion.Euler(0f, 0f, -spread + swing);
            backLeg.localRotation = Quaternion.Euler(0f, 0f, spread - swing);
            frontArm.localRotation = Quaternion.Euler(0f, 0f, -swing * 1.1f + Mathf.Lerp(0f, -50f, crouchBlend));
            backArm.localRotation = Quaternion.Euler(0f, 0f, swing * 1.1f + Mathf.Lerp(0f, -30f, crouchBlend));

            staggerElapsed += deltaTime;
            var stagger = 1f - Mathf.Clamp01(staggerElapsed / staggerSeconds);
            var hop = stagger > 0f ? 0.25f * Mathf.Sin(stagger * Mathf.PI) : 0f;
            var wobble = stagger > 0f ? 25f * stagger * Mathf.Sin(staggerElapsed * 30f) : 0f;
            var bob = Mathf.Abs(Mathf.Cos(cycle)) * 0.06f * moving * (1f - crouchBlend);
            body.localPosition = bodyRest + new Vector3(0f, bob - crouchSink * crouchBlend + hop, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, -crouchLeanDegrees * crouchBlend + wobble);
        }

        #endregion
    }
}
