#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The pinned player's way out. Within the wiggle window the hip center must travel a long path and stay inside a
    /// small area (the window rule of SpinDetector); while it does, the meter fills. A still stance adds no path, arm
    /// waves do not move the hips, and a step to a new spot leaves the area.
    /// </summary>
    public sealed class WiggleMeter
    {
        readonly SpinDetector window = new();
        float lastSampleTime = float.NegativeInfinity;

        /// <summary>0 to 1; full breaks the pin.</summary>
        public float Fill { get; private set; }
        public bool IsWiggling { get; private set; }

        #region Public API

        /// <summary>One body frame of the hip center in body inches.</summary>
        public void Add(Vector2 hip, float time, float windowSeconds)
        {
            window.Add(hip, time, windowSeconds);
            lastSampleTime = time;
        }

        /// <returns>True on the frame the meter fills.</returns>
        public bool Tick(float time, float deltaTime, float windowSeconds, float minPath, float maxRadius, float fillSeconds)
        {
            if (Fill >= 1f) return false;
            // A lost body keeps its last window: stale samples must not keep filling.
            IsWiggling = time - lastSampleTime <= windowSeconds * 0.5f && window.IsBurst(minPath, maxRadius, out _);
            if (IsWiggling) Fill = Mathf.Min(1f, Fill + deltaTime / fillSeconds);
            return Fill >= 1f;
        }

        public void Reset()
        {
            window.Clear();
            lastSampleTime = float.NegativeInfinity;
            Fill = 0f;
            IsWiggling = false;
        }

        #endregion
    }
}
