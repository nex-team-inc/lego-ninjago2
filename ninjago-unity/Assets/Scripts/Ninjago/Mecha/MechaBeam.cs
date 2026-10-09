#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The laser hit test: a straight beam of some width against an upright enemy body.</summary>
    public static class MechaBeam
    {
        /// <summary>
        /// Distance along the beam to an enemy standing at feet with this body radius and height, or -1 when the beam
        /// misses it. direction must be normalized.
        /// </summary>
        public static float HitDistance(Vector3 origin, Vector3 direction, float range, float beamRadius, Vector3 feet, float bodyRadius, float bodyHeight)
        {
            var center = feet + Vector3.up * (bodyHeight * 0.5f);
            var along = Vector3.Dot(center - origin, direction);
            if (along < 0f || along > range) return -1f;
            var closest = origin + direction * along;
            var side = new Vector2(center.x - closest.x, center.z - closest.z).magnitude;
            var vertical = Mathf.Abs(center.y - closest.y);
            return side <= beamRadius + bodyRadius && vertical <= beamRadius + bodyHeight * 0.5f ? along : -1f;
        }

        /// <summary>Distance at which a beam aimed down meets the ground (y = 0), capped at range.</summary>
        public static float GroundDistance(Vector3 origin, Vector3 direction, float range)
        {
            return direction.y < -1e-4f ? Mathf.Min(range, origin.y / -direction.y) : range;
        }
    }
}
