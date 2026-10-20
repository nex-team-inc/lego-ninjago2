#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Stealth on a side-on path, like a 2D platformer. The ninjas move right on their own: a player who crouches (the
    /// chest clearly below its calibrated standing height, with Crouch Run's hysteresis) walks silently, standing runs
    /// and makes noise. Two players sneak in single file and the team walks while anyone crouches; anyone standing in a
    /// talking zone gets the team heard (back to the zone start, time off the clock). Out of time fails the section.
    /// </summary>
    public class StealthBlock : ChapterBlock
    {
        [Serializable]
        public class ZoneLook
        {
            public LocalizedString title = new();
            public LocalizedString instruction = new();
            public Color color = Color.white;
        }

        [Header("Path Root")]
        [Tooltip("The path runs along this transform's +X; the camera looks along its +Z.")]
        [SerializeField] Transform pathRoot = null!;
        [Header("Level Builder")]
        [SerializeField] CrouchLevelBuilder levelBuilder = null!;
        [Header("Crouch Run Config")]
        [Tooltip("Owner of the crouch rule, speeds and heard penalty.")]
        [SerializeField] CrouchRunConfig crouchConfig = null!;
        [Header("Camera Offset")]
        [Tooltip("From the lead ninja in path space: ahead, up and out to the side.")]
        [SerializeField] Vector3 cameraOffset = new(4f, 2.2f, -11.5f);
        [Header("Zone Sign")]
        [Tooltip("CLEAR / STAND TO RUN or TALKING / CROUCH over the lead ninja.")]
        [SerializeField] WorldPrompt zoneSign = null!;
        [Header("Zone Sign Height")]
        [SerializeField] float zoneSignHeight = 3.1f;
        [Header("Clock")]
        [SerializeField] WorldPrompt clock = null!;
        [Header("Clock Height")]
        [SerializeField] float clockHeight = 4.3f;
        [Header("Zone Looks")]
        [SerializeField] EnumDictionary<CrouchZoneState, ZoneLook> looks = new();
        [Header("Clock Text")]
        [Tooltip("Smart string with {seconds}.")]
        [SerializeField] LocalizedString clockText = new();
        [Header("Clock Color")]
        [SerializeField] Color clockColor = new(0.2f, 0.2f, 0.25f);
        [Header("Heard Text")]
        [SerializeField] LocalizedString heardText = new();
        [Header("Heard Color")]
        [SerializeField] Color heardColor = new(1f, 0.25f, 0.2f);
        [Header("Time Up Text")]
        [SerializeField] LocalizedString timeUpText = new();

        readonly List<CrouchDetector> stances = new();
        readonly CrouchRunConfig.Level level = new();
        Vector3 gate;
        float distance;
        int shownSeconds = -1;

        public override Vector3 FocusPoint => pathRoot.TransformPoint(new Vector3(0f, 1f, 0f));
        public override float RouteLength => base.RouteLength + level.Length;

        #region Initialization

        public override void Initialize(BlockContext context, BlockEntry entry, int seed)
        {
            base.Initialize(context, entry, seed);
            zoneSign.Initialize(context.Camera);
            clock.Initialize(context.Camera);
            level.timerSeconds = entry.targetSeconds;
            level.zones.Clear();
            var clearMeters = context.Tuning.ClearZoneMeters;
            level.zones.Add(new CrouchRunConfig.Zone { state = CrouchZoneState.Clear, length = clearMeters * 0.7f });
            for (var i = 0; i < Mathf.Max(1, entry.difficulty.count); i++)
            {
                level.zones.Add(new CrouchRunConfig.Zone { state = CrouchZoneState.Talking, length = entry.difficulty.zoneLength });
                level.zones.Add(new CrouchRunConfig.Zone { state = CrouchZoneState.Clear, length = clearMeters });
            }

            gate = levelBuilder.Build(level, seed);
        }

        #endregion

        #region Block

        public override void Stage()
        {
            stances.Clear();
            foreach (var _ in Context.Players) stances.Add(new CrouchDetector());
            shownSeconds = -1;
            zoneSign.Hide();
            clock.Hide();
            PlaceTeam(0f);
        }

        public override Pose ArrivalPose(int slot) => new(PathPoint(-slot * Context.Tuning.SneakSpacing), NinjaFacing);

        public override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            var length = level.Length;
            var timeLeft = level.timerSeconds;
            var zone = -1;
            var runningInTalking = 0f;
            var frozen = 0f;
            var speed = 0f;
            var crouching = new bool[Context.Players.Count];
            while (true)
            {
                var deltaTime = Time.deltaTime;
                timeLeft -= deltaTime;
                var anyStanding = false;
                var anyCrouching = false;
                for (var i = 0; i < crouching.Length; i++)
                {
                    crouching[i] = UpdateStance(i);
                    anyStanding |= !crouching[i];
                    anyCrouching |= crouching[i];
                }

                var moved = 0f;
                if (frozen > 0f)
                {
                    frozen -= deltaTime;
                    speed = 0f;
                }
                else
                {
                    var target = anyCrouching ? crouchConfig.CrouchSpeed : crouchConfig.RunSpeed;
                    var acceleration = crouchConfig.SpeedChangeSeconds > 0f
                        ? Mathf.Abs(crouchConfig.RunSpeed - crouchConfig.CrouchSpeed) / crouchConfig.SpeedChangeSeconds
                        : float.MaxValue;
                    speed = Mathf.MoveTowards(speed, target, acceleration * deltaTime);
                    moved = speed * deltaTime;
                }

                PlaceTeam(distance + moved);
                for (var i = 0; i < crouching.Length; i++) Context.Ninjas[i].TickGait(moved, crouching[i], deltaTime);
                var current = level.ZoneAt(distance);
                var talking = level.zones[current].state == CrouchZoneState.Talking;
                if (current != zone)
                {
                    zone = current;
                    var look = looks[level.zones[zone].state];
                    zoneSign.Show(look.title, look.color, 0f, look.instruction);
                    if (talking) TryHint(Gesture.Crouch);
                }

                runningInTalking = talking && anyStanding && frozen <= 0f ? runningInTalking + deltaTime : 0f;
                if (runningInTalking > crouchConfig.HeardGraceSeconds && distance < length)
                {
                    timeLeft -= crouchConfig.HeardPenaltySeconds;
                    runningInTalking = 0f;
                    frozen = crouchConfig.HeardFreezeSeconds;
                    OnHeard(crouching, zone, timeLeft);
                    PlaceTeam(level.ZoneStart(zone));
                }

                UpdateClock(timeLeft);
                if (distance >= length)
                {
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.GateCleared, pathRoot.TransformPoint(gate) + Vector3.up * 2f);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.WayOpen);
                    LogCrouches(true);
                    zoneSign.Hide();
                    clock.Hide();
                    return true;
                }

                if (timeLeft <= 0f)
                {
                    LogCrouches(false);
                    zoneSign.Show(timeUpText, heardColor, Context.Tuning.TryAgainSeconds);
                    clock.Hide();
                    return false;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public override void Finish()
        {
            zoneSign.Hide();
            clock.Hide();
        }

        #endregion

        #region Helpers

        Quaternion NinjaFacing => pathRoot.rotation * Quaternion.Euler(0f, 90f, 0f);

        Vector3 PathPoint(float x) => pathRoot.TransformPoint(new Vector3(x, 0f, 0f));

        // Untracked frames keep the last stance.
        bool UpdateStance(int slot)
        {
            var body = Context.Players[slot];
            var stance = stances[slot];
            if (!body.TryGetLeanInches(out var lean)) return stance.IsCrouching;
            return stance.Update(-lean.y, Time.time, crouchConfig.CrouchDropInches, crouchConfig.StandDropInches, crouchConfig.StanceHoldSeconds);
        }

        void PlaceTeam(float x)
        {
            distance = x;
            var spacing = Context.Tuning.SneakSpacing;
            for (var i = 0; i < Context.Ninjas.Count; i++) Context.Ninjas[i].Place(PathPoint(x - i * spacing), NinjaFacing);
            ViewCamera.transform.SetPositionAndRotation(pathRoot.TransformPoint(new Vector3(x + cameraOffset.x, cameraOffset.y, cameraOffset.z)),
                pathRoot.rotation);
            zoneSign.transform.position = PathPoint(x) + Vector3.up * zoneSignHeight;
            clock.transform.position = PathPoint(x) + Vector3.up * clockHeight;
        }

        void UpdateClock(float timeLeft)
        {
            var seconds = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            clock.Show(LocalizedStrings.WithArguments(clockText, ("seconds", seconds)), clockColor);
        }

        // Every player standing in the talking zone is heard.
        void OnHeard(bool[] crouching, int zone, float timeLeft)
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Spotted);
            for (var i = 0; i < crouching.Length; i++)
            {
                var ninja = Context.Ninjas[i];
                ninja.PlayHeard();
                if (crouching[i]) continue;
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.Heard, ninja.Center + Vector3.up * 0.6f);
                ninja.Status.Show(heardText, heardColor, 1.2f);
                Context.Log.Heard(Context.Players[i].PlayerIndex, zone, timeLeft);
                Context.Log.Qte(Context.Players[i].PlayerIndex, Gesture.Crouch, false, -1f, "heard");
            }
        }

        void LogCrouches(bool cleared)
        {
            foreach (var player in Context.Players)
            {
                if (cleared) Context.Log.Qte(player.PlayerIndex, Gesture.Crouch, true);
                else Context.Log.Qte(player.PlayerIndex, Gesture.Crouch, false, -1f, "time_up");
            }
        }

        #endregion
    }
}
