#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The big golden teammate. Called, it spins down out of the sky onto a spot, arms up, and lands with a hop (the
    /// scene does the slam); then it jumps back up out of view. It waits in the sky between calls.
    /// </summary>
    public class CallTeammate : MonoBehaviour
    {
        enum State
        {
            Away,
            Arriving,
            Standing,
            Leaving,
        }

        [Header("Ninja")]
        [SerializeField] KidNinja ninja = null!;
        [Header("Sky Height")]
        [Tooltip("Meters above its spot the teammate waits, out of the camera's view.")]
        [SerializeField] float skyHeight = 14f;
        [Header("Spin Degrees")]
        [Tooltip("Degrees per second it spins while it comes and goes.")]
        [SerializeField] float spinDegrees = 1080f;
        [Header("Color")]
        [SerializeField] Color color = new(1f, 0.8f, 0.15f);

        State state;
        Vector3 from;
        Vector3 to;
        float elapsed;
        float seconds = 1f;

        public event Action? Landed;
        public event Action? Left;

        public bool IsAway => state == State.Away;
        public bool IsArriving => state == State.Arriving;
        /// <summary>Where it lands (or landed), in scene space.</summary>
        public Vector3 Spot => to;

        #region Initialization

        public void Initialize()
        {
            ninja.Initialize(0, color);
            transform.localPosition = new Vector3(0f, skyHeight, 0f);
        }

        #endregion

        #region Public API

        public void Arrive(Vector3 spot, float aSeconds)
        {
            state = State.Arriving;
            from = spot + Vector3.up * skyHeight;
            to = spot;
            elapsed = 0f;
            seconds = aSeconds;
            ninja.CopyArms(UpperBody.Rest.WithWrists(new Vector2(-12f, 16f), new Vector2(12f, 16f)));
        }

        /// <summary>A second caller joined in while it was on the way: it lands between them instead.</summary>
        public void Retarget(Vector3 spot)
        {
            from += spot - to;
            to = spot;
        }

        public void Leave(float aSeconds)
        {
            state = State.Leaving;
            from = transform.localPosition;
            to = from + Vector3.up * skyHeight;
            elapsed = 0f;
            seconds = aSeconds;
            ninja.CopyArms(UpperBody.Rest.WithWrists(new Vector2(-4f, 18f), new Vector2(4f, 18f)));
        }

        #endregion

        #region Animation

        void Update()
        {
            if (state is State.Away or State.Standing) return;
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / seconds);
            // Falls faster and faster onto the spot; leaves fast, then slows.
            transform.localPosition = Vector3.Lerp(from, to, state == State.Arriving ? t * t : 1f - (1f - t) * (1f - t));
            transform.localRotation = t < 1f ? Quaternion.Euler(0f, elapsed * spinDegrees, 0f) : Quaternion.identity;
            if (t < 1f) return;
            if (state == State.Arriving)
            {
                state = State.Standing;
                ninja.Hop(0.5f, 0.35f);
                ninja.ReleaseArms();
                Landed?.Invoke();
                return;
            }

            state = State.Away;
            Left?.Invoke();
        }

        #endregion
    }
}
