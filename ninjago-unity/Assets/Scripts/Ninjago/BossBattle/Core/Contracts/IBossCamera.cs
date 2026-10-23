#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The world camera director (TDD §3.4, implemented by BossCameraDirector): writes only the rig pivot as base pose
    /// (per phase, BossBattleVisualConfig) + override (cutscenes) + shake. Positions and angles are arena local.
    /// </summary>
    public interface IBossCamera
    {
        void SetBasePose(BossPhase phase, float blendSeconds);
        void Cut(Vector3 localPosition, Vector3 euler, float fieldOfView);
        UniTask MoveAsync(Vector3 localPosition, Vector3 euler, float fieldOfView, float seconds, bool easeInOut, CancellationToken cancellationToken);
        UniTask PlayShotsAsync(IReadOnlyList<CameraShot> shots, CancellationToken cancellationToken);
        /// <summary>Back to the base pose.</summary>
        void ClearOverride(float blendSeconds);
        /// <summary>Scaled by the PlayerPreference screen-shake setting.</summary>
        void Shake(float amount, float seconds);
        /// <summary>PixelWorldDisplay.SetFocusPoint target (null = the vehicle).</summary>
        void SetFocus(Transform? subject);
    }
}
