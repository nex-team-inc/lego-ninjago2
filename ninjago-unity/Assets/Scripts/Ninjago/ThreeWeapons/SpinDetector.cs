#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The nunchucks' burst for one hand. Not a fitted circle: within the window the hand travels a long path, every
    /// point of that path stays inside a small radius of its middle, and that middle sits in the called area. Wild
    /// motion anywhere else does not count.
    /// </summary>
    public sealed class SpinDetector
    {
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

        /// <summary>True when the samples in the window are a burst in the area; the window then starts over.</summary>
        public bool TryBurst(Vector2 areaCenter, float areaRadius, float minPath, float maxRadius)
        {
            if (samples.Count < 3) return false;
            var path = 0f;
            var sum = samples[0].position;
            for (var i = 1; i < samples.Count; i++)
            {
                path += (samples[i].position - samples[i - 1].position).magnitude;
                sum += samples[i].position;
            }

            if (path < minPath) return false;
            var middle = sum / samples.Count;
            if ((middle - areaCenter).magnitude > areaRadius) return false;
            foreach (var sample in samples)
            {
                if ((sample.position - middle).magnitude > maxRadius) return false;
            }

            samples.Clear();
            return true;
        }

        public void Clear()
        {
            samples.Clear();
        }

        #endregion
    }
}
