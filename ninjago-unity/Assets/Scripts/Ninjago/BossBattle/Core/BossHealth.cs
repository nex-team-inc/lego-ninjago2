#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A boss bar: current HP out of Max. Damage can stop at a floor (the next wake notch in phase 2), so a laser window
    /// never skips a notch. Changed fires on every change (the HUD bar listens).
    /// </summary>
    public sealed class BossHealth
    {
        const float DepletedEpsilon = 1e-4f;

        public BossHealth(float max)
        {
            Max = Mathf.Max(0f, max);
            Current = Max;
        }

        public float Max { get; private set; }
        public float Current { get; private set; }
        public float Fraction => Max > 0f ? Current / Max : 0f;
        public bool IsDepleted => Current <= DepletedEpsilon;

        public event Action? Changed;

        #region Public API

        /// <summary>Removes up to amount, never going below floorFraction × Max. Returns the HP actually removed.</summary>
        public float Damage(float amount, float floorFraction = 0f)
        {
            if (amount <= 0f) return 0f;
            var floor = Mathf.Clamp01(floorFraction) * Max;
            if (Current <= floor) return 0f;
            var next = Mathf.Max(floor, Current - amount);
            var dealt = Current - next;
            Current = next;
            Changed?.Invoke();
            return dealt;
        }

        public void SetFraction(float fraction) => SetCurrent(Mathf.Clamp01(fraction) * Max);

        public void SetCurrent(float hp)
        {
            Current = Mathf.Clamp(hp, 0f, Max);
            Changed?.Invoke();
        }

        /// <summary>New maximum; refill = start full, else the current HP is clamped to it.</summary>
        public void SetMax(float max, bool refill)
        {
            Max = Mathf.Max(0f, max);
            Current = refill ? Max : Mathf.Min(Current, Max);
            Changed?.Invoke();
        }

        #endregion
    }
}
