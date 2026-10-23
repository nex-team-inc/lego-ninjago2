#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The fight's world (TDD §3.4, implemented by BossArena): vehicles, boss, pools, camera, look, HUD and the shared
    /// rules (hits, heals, fx, checkpoints). Arena local space: the vehicle at z = 0, +z ahead, x right, y up; the ground
    /// moves toward -z at GroundSpeed. Phase runners, attacks and cutscenes only talk to the world through this.
    /// </summary>
    public interface IBossArena
    {
        BossBattleConfig Config { get; }
        DifficultySettings Difficulty { get; }
        BossRunState State { get; }
        BossPhase Phase { get; }
        /// <summary>Arena local space: vehicle at z = 0, +z ahead, x right, y up.</summary>
        Transform Root { get; }
        IReadOnlyList<PlayerBody> Players { get; }
        IBossActor Boss { get; }
        VehicleRig CarRig { get; }
        VehicleRig PlaneRig { get; }
        /// <summary>The active rig.</summary>
        VehicleRig Vehicle { get; }
        VehicleSteerer Steerer { get; }
        Vector2 VehiclePosition { get; }
        /// <summary>m/s the ground moves toward -z (bumps included).</summary>
        float GroundSpeed { get; }
        /// <summary>False: rigs are not posed by the steerer (cutscenes); true resets the steerer to the rig pose.</summary>
        bool SteeringEnabled { get; set; }
        bool IsInvulnerable { get; }
        /// <summary>Difficulty timing × enrage (wind-ups only).</summary>
        float WindupScale { get; }
        /// <summary>Difficulty timing (telegraph, lock, warning, spacing).</summary>
        float TimingScale { get; }
        IBossPools Pools { get; }
        IBossCamera Camera { get; }
        IBossLook Look { get; }
        IBossHud Hud { get; }
        BossBattleLog Log { get; }

        event Action<BossHazard>? VehicleHit;
        event Action? HeartsChanged;
        /// <summary>Player index unseen for at least trackingLostSeconds.</summary>
        event Action<int>? TrackingLost;

        /// <summary>Resets props, hearts, the boss visual and the look; enters the state's phase instantly.</summary>
        void BeginAttempt(IReadOnlyList<PlayerBody> players, BossRunState state);
        /// <summary>Vehicle, scenery, base camera pose and look of the phase.</summary>
        void EnterPhase(BossPhase phase, bool instant);
        /// <summary>Hearts (god mode and i-frames aware), bump, hit-stop, fx and log.</summary>
        void HitVehicle(BossHazard hazard, Vector3 localPoint);
        void Heal(int hearts);
        void PlayFx(BossFxCue cue, Vector3 localPoint, float scale = 1f, bool groundAnchored = false);
        BossFxHandle StartFx(BossFxCue cue, Transform follow, Vector3 localOffset);
        void StopFx(ref BossFxHandle handle);
        Vector3 ToWorld(Vector3 localPoint);
        Vector3 ToLocal(Vector3 worldPoint);
        /// <summary>Scaled time (pause and slow-mo aware).</summary>
        UniTask WaitAsync(float seconds, CancellationToken cancellationToken);
        /// <summary>Saves the checkpoint with the next beat index; only at calm moments.</summary>
        void SaveCheckpoint(int nextBeatIndex);
    }
}
