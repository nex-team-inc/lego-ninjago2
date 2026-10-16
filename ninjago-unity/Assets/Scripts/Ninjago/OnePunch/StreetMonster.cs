#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A blocky monster, its face on +Z. The street walks it toward a ninja; it freezes pale while someone is pinned,
    /// jumps onto a ninja's shoulders to pin him, and once thrown it flies its own long spinning arc with a brick
    /// trail, bursts into bricks and returns to its pool.
    /// </summary>
    public class StreetMonster : MonoBehaviour, IPoolableObject
    {
        enum State
        {
            Walking,
            Jumping,
            Pinning,
            Flying,
        }

        [Header("Body")]
        [Tooltip("Everything above the legs; bobs and waddles while walking.")]
        [SerializeField] Transform body = null!;
        [Header("Left Leg")]
        [SerializeField] Transform leftLeg = null!;
        [Header("Right Leg")]
        [SerializeField] Transform rightLeg = null!;
        [Header("Left Arm")]
        [SerializeField] Transform leftArm = null!;
        [Header("Right Arm")]
        [SerializeField] Transform rightArm = null!;
        [Header("Tint Renderers")]
        [SerializeField] Renderer[] tintRenderers = null!;
        [Header("Frozen Tint")]
        [SerializeField] Color frozenTint = new(0.78f, 0.88f, 1f);
        [Header("Frozen Tint Blend")]
        [SerializeField, Range(0f, 1f)] float frozenBlend = 0.6f;
        [Header("Stride Meters")]
        [Tooltip("Meters per full walk cycle.")]
        [SerializeField] float strideMeters = 1.1f;
        [Header("Jump Height")]
        [SerializeField] float jumpHeight = 1.4f;
        [Header("Flight Scale")]
        [Tooltip("Size the monster grows to by the end of its flight, so the arc still reads as it gets further away.")]
        [SerializeField] float flightScale = 1.5f;
        [Header("Trail Offset")]
        [Tooltip("Where the brick trail and the burst play, from the monster's root.")]
        [SerializeField] Vector3 centerOffset = new(0f, 1f, 0f);
        [Header("Top")]
        [Tooltip("On top of the head, where the next monster of a pile sits.")]
        [SerializeField] Transform top = null!;

        State state;
        Color color;
        float cycle;
        Transform home = null!;
        Transform? seat;
        Vector3 jumpFrom;
        float jumpElapsed;
        float jumpSeconds;
        Vector3 velocity;
        float gravity;
        Vector3 spinAxis;
        float spinDegrees;
        float flightElapsed;
        float flightSeconds;
        float launchScale = 1f;
        ParticleSystem? trail;

        public event Action<Component>? OnRelease;

        /// <summary>1-based place in its wave.</summary>
        public int Number { get; private set; }
        /// <summary>Street slot of the ninja it walks to.</summary>
        public int TargetSlot { get; private set; }
        public bool IsWalking => state == State.Walking;
        public Vector3 LocalPosition => transform.localPosition;
        public Transform Top => top;

        #region Public API

        /// <param name="aHome">Its parent in street space; it returns there after a pin.</param>
        /// <param name="scale">Size against the prefab, e.g. smaller goblins.</param>
        public void Spawn(Transform aHome, int number, int targetSlot, Vector3 localPosition, Color aColor, float scale = 1f)
        {
            home = aHome;
            Number = number;
            TargetSlot = targetSlot;
            color = aColor;
            state = State.Walking;
            cycle = 0f;
            transform.SetParent(home, false);
            transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, 180f, 0f));
            transform.localScale = Vector3.one * scale;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
            SetFrozen(false);
            PoseLimbs(0f, 0f);
        }

        /// <summary>Walks up to step meters toward target (street space, on the ground); returns the distance left.</summary>
        public float StepToward(Vector3 target, float step)
        {
            var offset = target - transform.localPosition;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance > 1e-4f)
            {
                var moved = Mathf.Min(step, distance);
                transform.localPosition += offset / distance * moved;
                transform.localRotation = Quaternion.LookRotation(offset / distance);
                cycle = (cycle + moved / strideMeters * Mathf.PI * 2f) % (Mathf.PI * 2f);
                PoseLimbs(Mathf.Sin(cycle), Mathf.Abs(Mathf.Cos(cycle)));
            }

            return distance - Mathf.Min(step, distance);
        }

        public void SetFrozen(bool frozen)
        {
            var tint = frozen ? Color.Lerp(color, frozenTint, frozenBlend) : color;
            foreach (var tintRenderer in tintRenderers) tintRenderer.material.color = tint;
        }

        /// <summary>Hops onto the seat over seconds and rides it, facing back up the street.</summary>
        public void JumpOn(Transform aSeat, float seconds)
        {
            seat = aSeat;
            state = State.Jumping;
            jumpFrom = transform.position;
            jumpElapsed = 0f;
            jumpSeconds = Mathf.Max(0.01f, seconds);
            PoseLimbs(0f, 0f);
            leftArm.localRotation = rightArm.localRotation = Quaternion.Euler(-150f, 0f, 0f);
        }

        /// <summary>Thrown: flies velocity (street space, m/s) under gravity, spinning, and bursts after seconds.</summary>
        public void Launch(Vector3 aVelocity, float aGravity, float aSpinDegrees, float seconds)
        {
            transform.SetParent(home, true);
            launchScale = transform.localScale.x;
            state = State.Flying;
            velocity = aVelocity;
            gravity = aGravity;
            spinDegrees = aSpinDegrees;
            spinAxis = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.3f, 0.3f), Mathf.Sign(aVelocity.x)).normalized;
            flightElapsed = 0f;
            flightSeconds = seconds;
            PoseLimbs(0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-160f, 0f, 30f);
            rightArm.localRotation = Quaternion.Euler(-160f, 0f, -30f);
            trail = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickTrail, Center, Quaternion.identity);
        }

        #endregion

        #region Animation

        Vector3 Center => transform.TransformPoint(centerOffset);

        void Update()
        {
            switch (state)
            {
                case State.Jumping:
                    UpdateJump();
                    break;
                case State.Flying:
                    UpdateFlight();
                    break;
            }
        }

        void UpdateJump()
        {
            jumpElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(jumpElapsed / jumpSeconds);
            var position = Vector3.Lerp(jumpFrom, seat!.position, t) + Vector3.up * (jumpHeight * Mathf.Sin(t * Mathf.PI));
            transform.position = position;
            if (t < 1f) return;
            state = State.Pinning;
            // World scale kept and facing back up the street, also when the seat is another (smaller) monster's top.
            transform.SetParent(seat, true);
            transform.localPosition = Vector3.zero;
            transform.rotation = home.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        void UpdateFlight()
        {
            var deltaTime = Time.deltaTime;
            flightElapsed += deltaTime;
            velocity.y -= gravity * deltaTime;
            transform.localPosition += velocity * deltaTime;
            transform.localRotation = Quaternion.AngleAxis(spinDegrees * deltaTime, spinAxis) * transform.localRotation;
            transform.localScale = Vector3.one * (launchScale * Mathf.Lerp(1f, flightScale, flightElapsed / flightSeconds));
            if (trail != null) trail.transform.position = Center;
            if (flightElapsed < flightSeconds) return;
            Burst();
        }

        void Burst()
        {
            if (trail != null) trail.Stop();
            trail = null;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, Center, Quaternion.identity, launchScale);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.EnemyExplosion);
            state = State.Walking;
            OnRelease?.Invoke(this);
        }

        // swing: -1..1 leg and arm swing; bob: 0..1 body lift.
        void PoseLimbs(float swing, float bob)
        {
            leftLeg.localRotation = Quaternion.Euler(swing * 32f, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing * 32f, 0f, 0f);
            // Arms reach forward like a zombie and pump against the legs.
            leftArm.localRotation = Quaternion.Euler(-80f - swing * 14f, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(-80f + swing * 14f, 0f, 0f);
            body.localPosition = new Vector3(0f, bob * 0.08f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, swing * 6f);
        }

        #endregion
    }
}
