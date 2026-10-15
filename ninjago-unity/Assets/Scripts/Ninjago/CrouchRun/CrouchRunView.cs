#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Crouch Run on screen: one side-scrolling lane per active player, the full screen alone or a wide strip each
    /// (player 1 on top) for two, each with that player's HUD, and one corner camera preview showing every player.
    /// </summary>
    public class CrouchRunView : SimpleCanvasView
    {
        [Header("Solo Feed")]
        [SerializeField] RawImage soloFeed = null!;
        [Header("Top Feed")]
        [SerializeField] RawImage topFeed = null!;
        [Header("Bottom Feed")]
        [SerializeField] RawImage bottomFeed = null!;
        [Header("Split Divider")]
        [SerializeField] GameObject splitDivider = null!;
        [Header("HUD Prefab")]
        [SerializeField] CrouchHud hudPrefab = null!;
        [Header("Lane Prefab")]
        [SerializeField] CrouchLane lanePrefab = null!;
        [Header("Lane Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 laneOrigin = new(8000f, 0f, 0f);
        [Header("Lane Spacing")]
        [Tooltip("Out of each other's view: the side cameras look along +Z.")]
        [SerializeField] Vector3 laneSpacing = new(0f, 0f, 400f);
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly List<CrouchLane> lanes = new();
        readonly List<RenderTexture> textures = new();
        readonly UniTaskCompletionSource quitSource = new();

        public IReadOnlyList<CrouchLane> Lanes => lanes;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.CrouchRun;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "crouch-run";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, CrouchRunConfig config, DetectionManager detection, int requestedPlayers)
        {
            var split = players.Count > 1;
            soloFeed.gameObject.SetActive(!split);
            topFeed.gameObject.SetActive(split);
            bottomFeed.gameObject.SetActive(split);
            splitDivider.SetActive(split);

            var indicatorPlayers = new List<int>(players.Count);
            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                var feed = split ? slot == 0 ? topFeed : bottomFeed : soloFeed;
                var texture = new RenderTexture(Screen.width, split ? Screen.height / 2 : Screen.height, 24) { name = $"CrouchLane_P{player.PlayerIndex + 1}" };
                textures.Add(texture);
                feed.texture = texture;
                var lane = Instantiate(lanePrefab, laneOrigin + laneSpacing * player.PlayerIndex, Quaternion.identity);
                lane.Initialize(player, config, Instantiate(hudPrefab, feed.rectTransform), texture, split);
                lanes.Add(lane);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
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

        /// <summary>Plays every lane from startLevel to its end; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(int startLevel, CancellationToken cancellationToken)
        {
            using var laneCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var laneTasks = new UniTask[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                laneTasks[i] = lanes[i].RunAsync(startLevel, laneCancellation.Token);
            }

            var winner = await UniTask.WhenAny(UniTask.WhenAll(laneTasks), quitSource.Task);
            if (winner == 0) return true;
            laneCancellation.Cancel();
            return false;
        }

        #endregion
    }
}
