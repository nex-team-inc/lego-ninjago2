#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Arena lighting and post (TDD §3.4, implemented by BossWorldLook): look blends (volumes, sun, ambient, fog, clear
    /// colour), flashes, lightning, the tilt-shift band and pooled point lights.
    /// </summary>
    public interface IBossLook
    {
        BossLook Current { get; }
        void Apply(BossLook look, float seconds);
        void Flash(float intensity, float seconds);
        void Lightning();
        /// <summary>Phase 2 follows the plane (smoothed by the implementation).</summary>
        void SetTiltCenter(float viewportY);
        /// <summary>Pooled no-shadow point lights.</summary>
        void PulseLight(Vector3 worldPoint, Color color, float intensity, float seconds);
    }
}
