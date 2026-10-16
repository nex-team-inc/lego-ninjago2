#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The nunchucks' burst for one hand. Not a fitted circle: within the window the hand travels a long path, keeps
    /// travelling through both halves of it, and every point stays inside a radius of the path's middle. Wrist twirls,
    /// big arm circles and fast waves all count; a hand moving from one place to another does not, even as it slows to
    /// a stop at the new place. Where the middle must be is the caller's rule.
    /// </summary>
    public sealed class SpinDetector
    {
        // Share of the minimum path each half of the window needs; an even spin puts half in each.
        const float halfPathShare = 0.4f;
        // The path only grows in steps of at least this many body inches, so tracking jitter on a still hand adds nothing.
        const float pathStepInches = 1f;

        readonly List<(Vector2 position, float time)> samples = new();

        #region Public API

        public void Add(Vector2 position, float time, float windowSeconds)
        {
            samples.Add((position, time));
            var oldest = time - windowSeconds;
            var drop = 0;
            while (drop < samples.Count - 1 && samples[drop + 1].time <= oldest) drop++;
            if (drop > 0) samples.RemoveRange(0, drop);
        }

        /// <summary>True when the samples in the window are a burst; middle is their average position.</summary>
        public bool IsBurst(float minPath, float maxRadius, out Vector2 middle)
        {
            middle = default;
            if (samples.Count < 3) return false;
            var halfTime = (samples[0].time + samples[^1].time) * 0.5f;
            var firstHalf = 0f;
            var secondHalf = 0f;
            var anchor = samples[0].position;
            var sum = samples[0].position;
            for (var i = 1; i < samples.Count; i++)
            {
                var position = samples[i].position;
                sum += position;
                var step = (position - anchor).magnitude;
                if (step < pathStepInches) continue;
                anchor = position;
                if (samples[i].time <= halfTime) firstHalf += step;
                else secondHalf += step;
            }

            if (firstHalf + secondHalf < minPath || Mathf.Min(firstHalf, secondHalf) < minPath * halfPathShare) return false;
            middle = sum / samples.Count;
            foreach (var sample in samples)
            {
                if ((sample.position - middle).magnitude > maxRadius) return false;
            }

            return true;
        }

        public void Clear()
        {
            samples.Clear();
        }

        #endregion
    }
}
