#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One rock's reticle timeline (GDD §4.1), from the throw's release: Hidden (rock in the air; X tracks the car so the
    /// ring appears under it) → Following (ring and "!" follow the car with a SmoothDamp) → Locked (ring stays, blinks) →
    /// Falling → Done, with ImpactNow true for exactly the one Tick that lands. Follow and lock times are scaled by the
    /// difficulty timing scale; air and fall are not.
    /// </summary>
    public sealed class RockReticle
    {
        float followSmooth;
        float hiddenEnd;
        float followEnd;
        float lockEnd;
        float fallEnd;
        float elapsed;
        float velocity;

        public ReticleState State { get; private set; } = ReticleState.Done;
        /// <summary>Ring centre x in arena local space.</summary>
        public float X { get; private set; }
        /// <summary>True for the single Tick in which the rock lands.</summary>
        public bool ImpactNow { get; private set; }
        public bool IsActive => State != ReticleState.Done;
        /// <summary>Seconds from Start to the impact.</summary>
        public float TotalSeconds => fallEnd;
        public float TimeToImpact => Mathf.Max(0f, fallEnd - elapsed);
        public float Elapsed => elapsed;
        /// <summary>0..1 through the current state (the lock blink and the shadow growth use it).</summary>
        public float StateProgress
        {
            get
            {
                return State switch
                {
                    ReticleState.Hidden => Progress(0f, hiddenEnd),
                    ReticleState.Following => Progress(hiddenEnd, followEnd),
                    ReticleState.Locked => Progress(followEnd, lockEnd),
                    ReticleState.Falling => Progress(lockEnd, fallEnd),
                    _ => 1f,
                };
            }
        }

        #region Public API

        /// <param name="first">First rock of the run: the longer lock.</param>
        /// <param name="extraFollowSeconds">Stretches the follow so the impact lands where RockSchedule placed it.</param>
        public void Start(float carX, RockSettings settings, bool first, float timingScale, float extraFollowSeconds = 0f)
        {
            followSmooth = Mathf.Max(0.01f, settings.followSmoothSeconds);
            hiddenEnd = Mathf.Max(0f, settings.airSeconds);
            followEnd = hiddenEnd + Mathf.Max(0f, settings.followSeconds * timingScale + extraFollowSeconds);
            lockEnd = followEnd + Mathf.Max(0f, settings.LockSeconds(first) * timingScale);
            fallEnd = lockEnd + Mathf.Max(0f, settings.fallSeconds);
            elapsed = 0f;
            velocity = 0f;
            X = carX;
            ImpactNow = false;
            State = ReticleState.Hidden;
        }

        public void Tick(float deltaTime, float carX)
        {
            ImpactNow = false;
            if (State == ReticleState.Done) return;
            elapsed += deltaTime;
            if (elapsed < hiddenEnd)
            {
                State = ReticleState.Hidden;
                X = carX;
                velocity = 0f;
            }
            else if (elapsed < followEnd)
            {
                State = ReticleState.Following;
                X = Mathf.SmoothDamp(X, carX, ref velocity, followSmooth, Mathf.Infinity, deltaTime);
            }
            else if (elapsed < lockEnd)
            {
                State = ReticleState.Locked;
            }
            else if (elapsed < fallEnd)
            {
                State = ReticleState.Falling;
            }
            else
            {
                State = ReticleState.Done;
                ImpactNow = true;
            }
        }

        /// <summary>Ring touches the car: |carX - X| &lt; rockRadius + carHalfWidth (0.55 + 0.7 = 1.25 by default).</summary>
        public bool IsHit(float carX, float carHalfWidth, float rockRadius) => Mathf.Abs(carX - X) < rockRadius + carHalfWidth;

        /// <summary>Drops the rock without an impact (cancel/cleanup).</summary>
        public void Stop()
        {
            State = ReticleState.Done;
            ImpactNow = false;
        }

        #endregion

        #region Helpers

        float Progress(float from, float to) => to > from ? Mathf.Clamp01((elapsed - from) / (to - from)) : 1f;

        #endregion
    }
}
