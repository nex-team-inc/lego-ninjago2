#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The shared vehicle's steering, step for step the same as RunnerWorld.Step: every tracked player's steer is blended
    /// by SharedSteer, mapped to a target in the lane (x) and the sky grid (y), and the position follows the target with a
    /// per-axis SmoothDamp. On top of that: per-side reach (a walled side reaches further), forced targets per axis (the
    /// clap push, the solo smash drive, cutscene hand-offs) and a switch for the vertical axis. Pure C#; the arena poses
    /// the VehicleRig from Position and Velocity.
    /// </summary>
    public sealed class VehicleSteerer
    {
        readonly SharedSteer shared = new();
        readonly Vector2?[] playerSteers = new Vector2?[SimulatedBody.MaxPlayers];
        readonly float[] contributions = new float[SimulatedBody.MaxPlayers];
        ChaseConfig.VehicleSettings settings = new();
        SteerSettings steer = new();
        Vector2 position;
        Vector2 velocity;
        Vector2 target;

        #region Properties

        public ChaseConfig.VehicleSettings Settings => settings;
        /// <summary>Vehicle position in the lane plane (x right, y up), arena local.</summary>
        public Vector2 Position => position;
        /// <summary>SmoothDamp velocity per axis (m/s); the rig banks and pitches with it.</summary>
        public Vector2 Velocity => velocity;
        /// <summary>The target of the last Step (forced axes included).</summary>
        public Vector2 Target => target;
        /// <summary>When set, the x target ignores the steer (e.g. the solo-smash drive into a wall).</summary>
        public float? ForcedX { get; set; }
        /// <summary>When set, the y target ignores the steer (e.g. the clap push to the top).</summary>
        public float? ForcedY { get; set; }
        /// <summary>Reach (m, positive) of a full lean left; defaults to halfWidth, grows on a walled side.</summary>
        public float ReachLeft { get; set; }
        /// <summary>Reach (m, positive) of a full lean right; defaults to halfWidth, grows on a walled side.</summary>
        public float ReachRight { get; set; }
        /// <summary>Whether the blended y steer moves the target (defaults to usesVerticalLean).</summary>
        public bool VerticalEnabled { get; set; }
        /// <summary>The player who steered harder on the last Step, or -1 when nobody steered.</summary>
        public int DominantPlayer => shared.DominantPlayer;

        #endregion

        #region Public API

        /// <summary>New vehicle settings: reach back to halfWidth, vertical per usesVerticalLean, forced axes cleared.</summary>
        public void Configure(ChaseConfig.VehicleSettings vehicle, SteerSettings steerSettings)
        {
            settings = vehicle;
            steer = steerSettings;
            ReachLeft = vehicle.halfWidth;
            ReachRight = vehicle.halfWidth;
            VerticalEnabled = vehicle.usesVerticalLean;
            ForcedX = null;
            ForcedY = null;
        }

        /// <summary>Puts the vehicle at rest at this position (phase start, cutscene hand-back).</summary>
        public void Reset(Vector2 at)
        {
            position = at;
            velocity = Vector2.zero;
            target = at;
            for (var i = 0; i < playerSteers.Length; i++)
            {
                playerSteers[i] = null;
                contributions[i] = 0f;
            }
        }

        /// <param name="steersPerPlayer">ChestSteer.Steer per player index (length SimulatedBody.MaxPlayers); null when
        /// that player is inactive or untracked this frame.</param>
        public void Step(Vector2?[] steersPerPlayer, float deltaTime)
        {
            for (var i = 0; i < playerSteers.Length; i++) playerSteers[i] = i < steersPerPlayer.Length ? steersPerPlayer[i] : null;
            var blended = shared.Blend(playerSteers, steer.playerOneShare, deltaTime);
            UpdateContributions();

            var x = blended.x * (blended.x < 0f ? ReachLeft : ReachRight);
            var y = settings.centerHeight + (VerticalEnabled ? blended.y * settings.halfHeight : 0f);
            target = new Vector2(ForcedX ?? x, ForcedY ?? y);
            position.x = Mathf.SmoothDamp(position.x, target.x, ref velocity.x, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
            position.y = Mathf.SmoothDamp(position.y, target.y, ref velocity.y, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
        }

        /// <summary>This player's own steer on the last Step (null = untracked or inactive).</summary>
        public Vector2? PlayerSteer(int playerIndex) => playerIndex >= 0 && playerIndex < playerSteers.Length ? playerSteers[playerIndex] : null;

        /// <summary>Share (0..1) of all steering effort so far that came from this player.</summary>
        public float SteerShare(int playerIndex) => playerIndex >= 0 && playerIndex < contributions.Length ? shared.EffortShare(playerIndex) : 0f;

        /// <summary>This player's part of the last blend: blend weight × steer magnitude (0..1; 0.5 max each with two players).</summary>
        public float Contribution(int playerIndex) => playerIndex >= 0 && playerIndex < contributions.Length ? contributions[playerIndex] : 0f;

        #endregion

        #region Helpers

        // Same weights as SharedSteer (P1 = share, P2 = 1 - share, clamped away from 0 and 1, renormalized over the tracked).
        void UpdateContributions()
        {
            var share = Mathf.Clamp(steer.playerOneShare, 0.001f, 0.999f);
            var totalWeight = 0f;
            for (var i = 0; i < playerSteers.Length; i++)
            {
                if (playerSteers[i].HasValue) totalWeight += i == 0 ? share : 1f - share;
            }

            for (var i = 0; i < playerSteers.Length; i++)
            {
                var value = playerSteers[i];
                contributions[i] = value.HasValue && totalWeight > 0f ? (i == 0 ? share : 1f - share) / totalWeight * value.Value.magnitude : 0f;
            }
        }

        #endregion
    }
}
