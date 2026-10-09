#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Robot Mecha on screen: one courtyard per active player, each camera rendering into its own half (or the full screen
    /// alone) with that player's HUD on it, the large camera preview in a corner with player indicators, and the laser hum.
    /// </summary>
    public class MechaFightView : SimpleCanvasView
    {
        [Header("Solo Feed")]
        [SerializeField] RawImage soloFeed = null!;
        [Header("Left Feed")]
        [SerializeField] RawImage leftFeed = null!;
        [Header("Right Feed")]
        [SerializeField] RawImage rightFeed = null!;
        [Header("Split Divider")]
        [SerializeField] GameObject splitDivider = null!;
        [Header("HUD Prefab")]
        [SerializeField] MechaHud hudPrefab = null!;
        [Header("Lane Prefab")]
        [SerializeField] MechaLane lanePrefab = null!;
        [Header("Lane Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 laneOrigin = new(5000f, 0f, 0f);
        [Header("Lane Spacing")]
        [SerializeField] Vector3 laneSpacing = new(300f, 0f, 0f);
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;
        [Header("Laser Hum Volume")]
        [SerializeField, Range(0f, 1f)] float humVolume = 0.6f;

        readonly List<MechaLane> lanes = new();
        readonly List<RenderTexture> textures = new();
        readonly UniTaskCompletionSource quitSource = new();
        RobotArmsTracker arms = null!;
        float endHoldSeconds;
        AudioSource? hum;

        public IReadOnlyList<MechaLane> Lanes => lanes;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.MechaFight;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "robot-mecha";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, RobotArmsTracker aArms, RobotArmsConfig armsConfig, MechaFightConfig config,
            DetectionManager detection, int requestedPlayers)
        {
            arms = aArms;
            endHoldSeconds = config.EndHoldSeconds;
            var split = players.Count > 1;
            soloFeed.gameObject.SetActive(!split);
            leftFeed.gameObject.SetActive(split);
            rightFeed.gameObject.SetActive(split);
            splitDivider.SetActive(split);

            var indicatorPlayers = new List<int>(players.Count);
            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                var feed = split ? slot == 0 ? leftFeed : rightFeed : soloFeed;
                var texture = new RenderTexture(split ? Screen.width / 2 : Screen.width, Screen.height, 24) { name = $"MechaLane_P{player.PlayerIndex + 1}" };
                textures.Add(texture);
                feed.texture = texture;
                var lane = Instantiate(lanePrefab, laneOrigin + laneSpacing * player.PlayerIndex, Quaternion.identity);
                lane.Initialize(player, arms, armsConfig, config, Instantiate(hudPrefab, feed.rectTransform), texture, split);
                lanes.Add(lane);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            if (hum != null) SfxManager.Instance.StopLoop(hum);
            foreach (var lane in lanes)
            {
                if (lane != null) Destroy(lane.gameObject);
            }

            foreach (var texture in textures)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        #endregion

        #region Public API

        /// <summary>Plays every lane's wave to its end; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var laneCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            arms.SetDetecting(true);
            hum = SfxManager.Instance.StartLoop(SfxManager.SoundEffect.LaserHum, humVolume);
            var laneTasks = new UniTask[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                laneTasks[i] = lanes[i].RunAsync(laneCancellation.Token);
            }

            var winner = await UniTask.WhenAny(UniTask.WhenAll(laneTasks), quitSource.Task);
            if (hum != null) SfxManager.Instance.StopLoop(hum);
            hum = null;
            arms.SetDetecting(false);
            if (winner != 0)
            {
                laneCancellation.Cancel();
                return false;
            }

            var hold = await UniTask.WhenAny(UniTask.Delay((int)(endHoldSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: laneCancellation.Token),
                quitSource.Task);
            return hold == 0;
        }

        #endregion
    }
}
