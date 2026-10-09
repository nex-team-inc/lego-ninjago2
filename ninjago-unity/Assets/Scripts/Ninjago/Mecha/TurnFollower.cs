#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One heavy turn axis (degrees): speeds up and brakes at a limited acceleration, never beyond its top speed, and
    /// arrives at the target without overshooting it.
    /// </summary>
    public struct TurnFollower
    {
        public float Value;
        public float Velocity;

        #region Public API

        public void Step(float target, float maxSpeed, float acceleration, float deltaTime)
        {
            var error = target - Value;
            // The fastest speed that can still stop at the target with this acceleration.
            var wanted = Mathf.Sign(error) * Mathf.Min(maxSpeed, Mathf.Sqrt(2f * acceleration * Mathf.Abs(error)));
            Velocity = Mathf.MoveTowards(Velocity, wanted, acceleration * deltaTime);
            var step = Velocity * deltaTime;
            if (error != 0f && Mathf.Sign(step) == Mathf.Sign(error) && Mathf.Abs(step) >= Mathf.Abs(error))
            {
                Value = target;
                Velocity = 0f;
                return;
            }

            Value += step;
        }

        public void Stop()
        {
            Velocity = 0f;
        }

        #endregion
    }
}
