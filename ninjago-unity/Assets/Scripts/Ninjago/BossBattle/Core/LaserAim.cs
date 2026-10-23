#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Laser window aiming (GDD §6.2): the plane is on target when it is inside the ring (2D distance in the plane's
    /// x/y); the radius grows as an assist late in a long window; the boss Core is placed on the camera ray through the
    /// ring so on screen the boss flies the same infinity.
    /// </summary>
    public static class LaserAim
    {
        public static bool OnTarget(Vector2 plane, Vector2 target, float radius) => (plane - target).sqrMagnitude <= radius * radius;

        /// <summary>baseRadius × 1 until assistStart, growing linearly to × assistScale at assistFull.</summary>
        public static float AssistRadius(float baseRadius, float elapsed, LaserSettings settings)
        {
            var t = settings.assistFullSeconds > settings.assistStartSeconds
                ? Mathf.Clamp01((elapsed - settings.assistStartSeconds) / (settings.assistFullSeconds - settings.assistStartSeconds))
                : elapsed >= settings.assistStartSeconds ? 1f : 0f;
            return baseRadius * Mathf.Lerp(1f, settings.assistScale, t);
        }

        /// <summary>Where the ray from the camera through the ring crosses z = bossZ (all arena local).</summary>
        public static Vector3 CoreOnCameraRay(Vector3 cameraLocal, Vector3 ringLocal, float bossZ)
        {
            var direction = ringLocal - cameraLocal;
            if (Mathf.Abs(direction.z) < 1e-4f) return new Vector3(ringLocal.x, ringLocal.y, bossZ);
            var t = (bossZ - cameraLocal.z) / direction.z;
            return cameraLocal + direction * t;
        }
    }
}
