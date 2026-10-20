#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The ride, seen from behind the vehicle: Chest Chase's RunnerWorld with this entry's vehicles, length, pace and
    /// obstacle count. Every active chest steers the one vehicle through SharedSteer; an obstacle shows about 1.5 s
    /// before it arrives and a hit is a bump and a small speed loss, never a fail. With the Boss flag the Boss runs
    /// ahead: clean dodges close in, bumps fall back, and at the end it always escapes.
    /// </summary>
    public class RunnerBlock : ChapterBlock
    {
        [Header("Runner World")]
        [SerializeField] RunnerWorld world = null!;
        [Header("Chase Config")]
        [Tooltip("Owner of the steering rule and each vehicle's base settings.")]
        [SerializeField] ChaseConfig chaseConfig = null!;
        [Header("Vehicle Prompt")]
        [Tooltip("Hints and the escape line over the shared vehicle.")]
        [SerializeField] WorldPrompt vehiclePrompt = null!;
        [Header("Vehicle Prompt Height")]
        [SerializeField] float promptHeight = 2.4f;
        [Header("Boss Runner")]
        [Tooltip("The Boss running ahead in a chase.")]
        [SerializeField] Transform bossRunner = null!;
        [Header("Boss Flyer")]
        [Tooltip("What the Boss leaps onto to escape.")]
        [SerializeField] Transform bossFlyer = null!;
        [Header("Escape Text")]
        [SerializeField] LocalizedString escapeText = new();
        [Header("Escape Color")]
        [SerializeField] Color escapeColor = new(0.55f, 0.25f, 0.85f);

        readonly Dictionary<VehicleType, ChaseConfig.VehicleSettings> settings = new();
        float bossGap;
        bool riding;
        int dodges;
        int hits;

        /// <summary>How close the ninjas got to the Boss at the end, 0 (start gap) to 1 (as close as it allows).</summary>
        public float ChaseCloseness { get; private set; }
        public override Vector3 FocusPoint => world.Vehicle.Position + Vector3.up;

        #region Initialization

        public override void Initialize(BlockContext context, BlockEntry entry, int seed)
        {
            base.Initialize(context, entry, seed);
            vehiclePrompt.Initialize(context.Camera);
            world.Initialize(context.Players, chaseConfig, null);
            world.ObstacleResolved += HandleObstacleResolved;
            var vehicles = entry.ride.Vehicles();
            var rideSeconds = Mathf.Max(10f, entry.targetSeconds - (vehicles.Count - 1) * chaseConfig.TransitionSeconds);
            foreach (var vehicle in vehicles)
            {
                var copy = chaseConfig.GetVehicle(vehicle).Copy();
                copy.segmentSeconds = rideSeconds / vehicles.Count;
                copy.speed *= entry.difficulty.speedScale;
                copy.obstacleSpacingSeconds = Mathf.Clamp(10f / Mathf.Max(1, entry.difficulty.count), 0.8f, 6f);
                copy.openingSeconds = context.Tuning.RideOpeningSeconds;
                settings[vehicle] = copy;
            }
        }

        void OnDestroy()
        {
            world.ObstacleResolved -= HandleObstacleResolved;
        }

        #endregion

        #region Block

        public override void Stage()
        {
            riding = false;
            dodges = hits = 0;
            world.Show(Entry.ride.Vehicles()[0]);
            vehiclePrompt.Hide();
            bossGap = Context.Tuning.BossStartGap;
            bossRunner.gameObject.SetActive(Entry.boss);
            bossFlyer.gameObject.SetActive(false);
            PlaceBoss(0f);
        }

        public override Pose ArrivalPose(int slot)
        {
            var side = (slot - (Context.Ninjas.Count - 1) * 0.5f) * 0.6f;
            return new Pose(world.Vehicle.Position + world.transform.right * side + Vector3.up * 0.5f, world.transform.rotation);
        }

        public override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            foreach (var ninja in Context.Ninjas)
            {
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaBrickPop, ninja.Center);
                ninja.SetVisible(false);
            }

            TryHint(Gesture.Lean, vehiclePrompt, Color.white);
            riding = true;
            await world.RunAsync(Entry.ride.Vehicles(), vehicle => settings[vehicle], 0f, cancellationToken);
            riding = false;
            ChaseCloseness = Mathf.InverseLerp(Context.Tuning.BossStartGap, Context.Tuning.BossMinGap, bossGap);
            if (Entry.boss) await EscapeAsync(cancellationToken);
            foreach (var player in Context.Players) Context.Log.Qte(player.PlayerIndex, Gesture.Lean, hits == 0, -1f, hits == 0 ? "" : $"hits_{hits}");
            ShowNinjas();
            return true;
        }

        public override void Finish()
        {
            vehiclePrompt.Hide();
            bossRunner.gameObject.SetActive(false);
            bossFlyer.gameObject.SetActive(false);
        }

        #endregion

        #region Boss Chase

        void Update()
        {
            vehiclePrompt.transform.position = world.Vehicle.Position + Vector3.up * promptHeight;
            if (!riding || !Entry.boss) return;
            PlaceBoss(Time.time);
        }

        void PlaceBoss(float time)
        {
            var vehicle = world.Vehicle.transform.localPosition;
            var bob = Mathf.Abs(Mathf.Sin(time * 7f)) * 0.25f;
            bossRunner.localPosition = new Vector3(Mathf.Sin(time * 0.6f) * 1.2f, vehicle.y + bob, bossGap);
        }

        // The Boss leaps onto its flyer and is gone into the clouds; the ride ends on that escape, not on a loss.
        async UniTask EscapeAsync(CancellationToken cancellationToken)
        {
            BgmManager.Instance.PlayStinger(BgmManager.StingerType.BossAppear);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.FlyAway);
            var start = bossRunner.localPosition;
            bossFlyer.gameObject.SetActive(true);
            bossFlyer.localPosition = start + new Vector3(0f, 3f, 6f);
            vehiclePrompt.Show(escapeText, escapeColor, Context.Tuning.BossEscapeSeconds + 1f);
            var seconds = Context.Tuning.BossEscapeSeconds;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                var leap = Mathf.Clamp01(t * 3f);
                bossRunner.localPosition = Vector3.Lerp(start, bossFlyer.localPosition + Vector3.up * 1.2f, Mathf.SmoothStep(0f, 1f, leap));
                if (t > 0.33f)
                {
                    var away = (t - 0.33f) / 0.67f;
                    bossFlyer.localPosition = start + new Vector3(0f, 3f + away * away * 40f, 6f + away * 70f);
                    bossRunner.localPosition = bossFlyer.localPosition + Vector3.up * 1.2f;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, bossFlyer.position);
            bossRunner.gameObject.SetActive(false);
            bossFlyer.gameObject.SetActive(false);
        }

        void HandleObstacleResolved(VehicleType vehicle, bool hit, int dominantPlayer)
        {
            if (hit)
            {
                hits++;
                Context.Log.ObstacleHit(vehicle, dominantPlayer);
            }
            else
            {
                dodges++;
            }

            if (!Entry.boss) return;
            bossGap = hit ? bossGap + Context.Tuning.BossGapPerHit : Mathf.Max(Context.Tuning.BossMinGap, bossGap - Context.Tuning.BossGapPerDodge);
        }

        void ShowNinjas()
        {
            for (var i = 0; i < Context.Ninjas.Count; i++)
            {
                var ninja = Context.Ninjas[i];
                var pose = ArrivalPose(i);
                ninja.Place(pose.position, pose.rotation);
                ninja.SetVisible(true);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaBrickPop, ninja.Center);
            }
        }

        #endregion
    }
}
