#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's Crouch Run: that player's copy of the levels, a ninja that always moves right, and a side camera
    /// that scrolls with it into that player's screen region. Standing runs, crouching walks; running past the windows of
    /// a talking zone gets the ninja seen (back to the zone start, time off the clock). Plays levels in order until one
    /// fails or all clear.
    /// </summary>
    public class CrouchLane : MonoBehaviour
    {
        public readonly struct LevelResult
        {
            public readonly int levelIndex;
            public readonly bool cleared;
            public readonly float timeLeft;
            public readonly int timesHeard;

            public LevelResult(int levelIndex, bool cleared, float timeLeft, int timesHeard)
            {
                this.levelIndex = levelIndex;
                this.cleared = cleared;
                this.timeLeft = timeLeft;
                this.timesHeard = timesHeard;
            }
        }

        [Header("Ninja")]
        [SerializeField] CrouchNinjaRig ninja = null!;
        [Header("Level Builder")]
        [SerializeField] CrouchLevelBuilder levelBuilder = null!;
        [Header("Lane Camera")]
        [SerializeField] Camera laneCamera = null!;
        [Header("Camera Offset")]
        [Tooltip("From the ninja: ahead (so the next zone shows), up and out to the side.")]
        [SerializeField] Vector3 cameraOffset = new(4f, 2.2f, -11.5f);
        [Header("Solo Field Of View")]
        [SerializeField] float soloFieldOfView = 34f;
        [Header("Split Field Of View")]
        [Tooltip("Each half is a wide strip in two-player mode; a tighter view keeps the ninja readable.")]
        [SerializeField] float splitFieldOfView = 26f;
        [Header("Run Step Volume")]
        [SerializeField, Range(0f, 1f)] float runStepVolume = 1f;
        [Header("Sneak Step Volume")]
        [SerializeField, Range(0f, 1f)] float sneakStepVolume = 0.3f;

        readonly CrouchDetector crouch = new();
        readonly List<LevelResult> results = new();
        PlayerBody body = null!;
        CrouchRunConfig config = null!;
        CrouchHud hud = null!;
        float distance;
        float speed;
        float stepDistance;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        public IReadOnlyList<LevelResult> Results => results;
        public bool ClearedAll { get; private set; }
        /// <summary>The level being played (0-based).</summary>
        public int LevelIndex { get; private set; }
        /// <summary>Meters the ninja has covered in the current level.</summary>
        public float Distance => distance;

        #region Initialization

        public void Initialize(PlayerBody aBody, CrouchRunConfig aConfig, CrouchHud aHud, RenderTexture target, bool splitScreen)
        {
            body = aBody;
            config = aConfig;
            hud = aHud;
            laneCamera.targetTexture = target;
            laneCamera.fieldOfView = splitScreen ? splitFieldOfView : soloFieldOfView;
            ninja.Initialize(body.PlayerIndex, body.Color);
            hud.Initialize(body.PlayerIndex, body.Color, splitScreen);
            hud.HideZone();
            hud.SetLevel(1);
            hud.SetClock(config.GetLevel(0).timerSeconds);
            PlaceNinja(0f);
        }

        #endregion

        #region Levels

        /// <summary>Plays levels from startLevel until one fails or the last one is cleared.</summary>
        public async UniTask RunAsync(int startLevel, CancellationToken cancellationToken)
        {
            for (var levelIndex = startLevel; levelIndex < config.LevelCount; levelIndex++)
            {
                LevelIndex = levelIndex;
                var gate = levelBuilder.Build(config, levelIndex);
                PlaceNinja(0f);
                hud.SetLevel(levelIndex + 1);
                hud.SetClock(config.GetLevel(levelIndex).timerSeconds);
                hud.HideZone();
                hud.ShowLevelBanner(levelIndex + 1);
                await WaitAsync(config.LevelIntroSeconds, cancellationToken);
                hud.HideCenter();
                var result = await PlayLevelAsync(levelIndex, cancellationToken);
                results.Add(result);
                if (result.cleared)
                {
                    hud.ShowCleared();
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.GateCleared, transform.TransformPoint(gate) + Vector3.up * 2f);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                }
                else
                {
                    hud.ShowTimeUp();
                }

                await WaitAsync(config.LevelOutroSeconds, cancellationToken);
                if (!result.cleared) return;
            }

            ClearedAll = true;
        }

        async UniTask<LevelResult> PlayLevelAsync(int levelIndex, CancellationToken cancellationToken)
        {
            var level = config.GetLevel(levelIndex);
            var length = level.Length;
            var timeLeft = level.timerSeconds;
            var timesHeard = 0;
            var zone = -1;
            var runningInTalking = 0f;
            var frozen = 0f;
            speed = 0f;
            while (true)
            {
                var deltaTime = Time.deltaTime;
                timeLeft -= deltaTime;
                var crouching = UpdateStance();
                var moved = 0f;
                if (frozen > 0f)
                {
                    frozen -= deltaTime;
                    speed = 0f;
                }
                else
                {
                    var target = crouching ? config.CrouchSpeed : config.RunSpeed;
                    var acceleration = config.SpeedChangeSeconds > 0f ? Mathf.Abs(config.RunSpeed - config.CrouchSpeed) / config.SpeedChangeSeconds : float.MaxValue;
                    speed = Mathf.MoveTowards(speed, target, acceleration * deltaTime);
                    moved = speed * deltaTime;
                }

                PlaceNinja(distance + moved);
                ninja.Tick(moved, crouching, deltaTime);
                PlaySteps(moved, crouching);
                var current = level.ZoneAt(distance);
                if (current != zone)
                {
                    zone = current;
                    hud.ShowZone(config.GetLook(level.zones[zone].state));
                    CrouchRunAnalytics.ZoneEntered(PlayerIndex, levelIndex + 1, zone, level.zones[zone].state, crouching, timeLeft);
                }

                var talking = level.zones[zone].state == CrouchZoneState.Talking;
                runningInTalking = talking && !crouching && frozen <= 0f ? runningInTalking + deltaTime : 0f;
                if (runningInTalking > config.HeardGraceSeconds && distance < length)
                {
                    timesHeard++;
                    timeLeft -= config.HeardPenaltySeconds;
                    runningInTalking = 0f;
                    frozen = config.HeardFreezeSeconds;
                    OnHeard(levelIndex, zone, timesHeard, timeLeft, level.ZoneStart(zone));
                }

                hud.SetClock(timeLeft);
                if (distance >= length)
                {
                    CrouchRunAnalytics.LevelCleared(PlayerIndex, levelIndex + 1, timesHeard, timeLeft);
                    return new LevelResult(levelIndex, true, timeLeft, timesHeard);
                }

                if (timeLeft <= 0f)
                {
                    CrouchRunAnalytics.LevelFailed(PlayerIndex, levelIndex + 1, timesHeard, distance);
                    return new LevelResult(levelIndex, false, 0f, timesHeard);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        void OnHeard(int levelIndex, int zone, int timesHeard, float timeLeft, float zoneStart)
        {
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.Heard, ninja.HeadPosition);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Spotted);
            hud.FlashHeard();
            hud.SetHeardCount(timesHeard);
            ninja.PlayHeard();
            CrouchRunAnalytics.Heard(PlayerIndex, levelIndex + 1, zone, timesHeard, timeLeft);
            PlaceNinja(zoneStart);
        }

        #endregion

        #region Helpers

        // Untracked frames keep the last stance.
        bool UpdateStance()
        {
            if (!body.TryGetLeanInches(out var lean)) return crouch.IsCrouching;
            return crouch.Update(-lean.y, Time.time, config.CrouchDropInches, config.StandDropInches, config.StanceHoldSeconds);
        }

        void PlaceNinja(float x)
        {
            distance = x;
            ninja.transform.localPosition = new Vector3(x, 0f, 0f);
            laneCamera.transform.localPosition = new Vector3(x + cameraOffset.x, cameraOffset.y, cameraOffset.z);
        }

        // Loud steps for the run, quiet steps for the crouch, one per half stride.
        void PlaySteps(float moved, bool crouching)
        {
            stepDistance += moved;
            var stepLength = crouching ? 0.45f : 0.9f;
            if (stepDistance < stepLength) return;
            stepDistance = 0f;
            if (crouching) SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.SneakStep, 1f, sneakStepVolume);
            else SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.RunStep, 1f, runStepVolume);
        }

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion
    }
}
