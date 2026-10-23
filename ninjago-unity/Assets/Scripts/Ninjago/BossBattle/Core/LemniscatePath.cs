#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The laser target ring's infinity path (GDD §6.2) at the plane's depth: centre + (hx·sin ωt, hy·sin 2ωt) with
    /// ω = 2π / period, so it stays exactly within ±halfSize of the centre (default (1.8, 1.0) around (0, 3.2), period 8 s).
    /// </summary>
    public readonly struct LemniscatePath
    {
        readonly Vector2 halfSize;
        readonly Vector2 centre;
        readonly float omega;

        public LemniscatePath(Vector2 halfSize, float period, Vector2 centre)
        {
            this.halfSize = halfSize;
            this.centre = centre;
            omega = period > 0f ? 2f * Mathf.PI / period : 0f;
        }

        public Vector2 HalfSize => halfSize;
        public Vector2 Centre => centre;

        public Vector2 Evaluate(float time)
        {
            var angle = omega * time;
            return new Vector2(centre.x + halfSize.x * Mathf.Sin(angle), centre.y + halfSize.y * Mathf.Sin(2f * angle));
        }

        public static LemniscatePath From(LaserSettings settings) => new(settings.pathHalfSize, settings.pathPeriodSeconds, settings.pathCentre);
    }
}
