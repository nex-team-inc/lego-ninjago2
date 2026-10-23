#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The only writer of Time.timeScale while the Boss Battle runs (TDD §1): Time.timeScale = paused ? 0 : slowMo × hitStop.
    /// Gameplay and cutscenes run on scaled time, so pause and slow motion freeze/slow them; views and MMF players use
    /// unscaled time. Overlapping hit-stops keep the freeze until the last one ends. Reset() when the mode ends.
    /// </summary>
    public static class BossTimeScale
    {
        static bool paused;
        static float slowMo = 1f;
        static int hitStops;

        public static bool IsPaused => paused;
        public static float SlowMo => slowMo;
        public static bool IsHitStopped => hitStops > 0;
        /// <summary>The value written to Time.timeScale.</summary>
        public static float Current => paused ? 0f : hitStops > 0 ? 0f : slowMo;

        #region Public API

        public static void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        /// <summary>Slow motion factor (1 = normal speed), e.g. 0.35 in the transform cutscene.</summary>
        public static void SetSlowMo(float scale)
        {
            slowMo = Mathf.Clamp(scale, 0.01f, 4f);
            Apply();
        }

        /// <summary>Freezes scaled time for this many real seconds (vehicle hit 0.06 s, rocket hit 0.04 s).</summary>
        public static async UniTask HitStopAsync(float seconds, CancellationToken cancellationToken = default)
        {
            if (seconds <= 0f) return;
            hitStops++;
            Apply();
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, cancellationToken);
            }
            finally
            {
                hitStops = Mathf.Max(0, hitStops - 1);
                Apply();
            }
        }

        /// <summary>Unpaused, normal speed, no hit-stop: Time.timeScale = 1.</summary>
        public static void Reset()
        {
            paused = false;
            slowMo = 1f;
            hitStops = 0;
            Apply();
        }

        #endregion

        #region Helpers

        static void Apply() => Time.timeScale = Current;

        #endregion
    }
}
