#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Base camera pose of a phase, in arena local space (vehicle at z = 0, +z ahead): the rig pivot position, its pitch
    /// (degrees, positive looks down) and the vertical field of view. Written only by the camera director (IBossCamera).
    /// </summary>
    [Serializable]
    public struct CameraPose
    {
        public Vector3 position;
        [Tooltip("Degrees around x; positive looks down.")]
        public float pitch;
        [Tooltip("Vertical field of view in degrees.")]
        public float fov;

        public Vector3 Euler => new(pitch, 0f, 0f);

        public static CameraPose Of(Vector3 position, float pitch, float fov) => new() { position = position, pitch = pitch, fov = fov };
    }

    /// <summary>
    /// One shot of a cutscene camera list (IBossCamera.PlayShotsAsync): cut or move (eased) to the pose over the seconds,
    /// arena local space. Keep rotation changes under 10 degrees per second (pixel crawl, GDD §5).
    /// </summary>
    [Serializable]
    public struct CameraShot
    {
        public Vector3 position;
        public Vector3 euler;
        public float fov;
        [Tooltip("Seconds of the move, or how long a cut holds.")]
        public float seconds;
        [Tooltip("Cut to the pose instead of moving there.")]
        public bool cut;

        public static CameraShot Of(Vector3 position, Vector3 euler, float fov, float seconds, bool cut = false) =>
            new() { position = position, euler = euler, fov = fov, seconds = seconds, cut = cut };
    }
}
