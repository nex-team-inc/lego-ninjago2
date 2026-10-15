#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Three Weapons on screen: one gauntlet per active player, the full screen alone or a half each for two, with that
    /// player's HUD, the weapon cards and cursors over the feeds, and one corner camera preview showing every player.
    /// </summary>
    public class ThreeWeaponsView : SimpleCanvasView
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
        [SerializeField] GauntletHud hudPrefab = null!;
        [Header("Lane Prefab")]
        [SerializeField] GauntletLane lanePrefab = null!;
        [Header("Lane Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 laneOrigin = new(10000f, 0f, 0f);
        [Header("Lane Spacing")]
        [SerializeField] Vector3 laneSpacing = new(200f, 0f, 0f);
        [Header("Cards Layer")]
        [Tooltip("Full-screen rect, aligned with the cursor layer, holding the weapon cards.")]
        [SerializeField] RectTransform cardsLayer = null!;
        [Header("Card Prefab")]
        [SerializeField] WeaponCard cardPrefab = null!;
        [Header("Cursor Layer")]
        [SerializeField] HandCursorLayer cursorLayer = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly List<GauntletLane> lanes = new();
        readonly List<RenderTexture> textures = new();
        readonly UniTaskCompletionSource quitSource = new();
        HandCursorTracker cursors = null!;
        float endHoldSeconds;

        public IReadOnlyList<GauntletLane> Lanes => lanes;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ThreeWeapons;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "three-weapons";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, ThreeWeaponsConfig config, HandCursorTracker aCursors, DetectionManager detection,
            int requestedPlayers)
        {
            cursors = aCursors;
            endHoldSeconds = config.EndHoldSeconds;
            var split = players.Count > 1;
            soloFeed.gameObject.SetActive(!split);
            leftFeed.gameObject.SetActive(split);
            rightFeed.gameObject.SetActive(split);
            splitDivider.SetActive(split);
            cursorLayer.Initialize(cursors, players);

            var indicatorPlayers = new List<int>(players.Count);
            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                var feed = split ? slot == 0 ? leftFeed : rightFeed : soloFeed;
                var region = SplitScreen.Region(slot, players.Count);
                cursors.SetRegion(player.PlayerIndex, region);
                cursorLayer.SetPlayerVisible(player.PlayerIndex, false);
                var texture = new RenderTexture(split ? Screen.width / 2 : Screen.width, Screen.height, 24) { name = $"GauntletLane_P{player.PlayerIndex + 1}" };
                textures.Add(texture);
                feed.texture = texture;
                var lane = Instantiate(lanePrefab, laneOrigin + laneSpacing * player.PlayerIndex, Quaternion.identity);
                lane.Initialize(player, config, cursors, cursorLayer, cardsLayer, cardPrefab, Instantiate(hudPrefab, feed.rectTransform), texture, region);
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

        /// <summary>Plays every gauntlet to its end; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var laneCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            cursors.SetDetecting(true);
            var laneTasks = new UniTask[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                laneTasks[i] = lanes[i].RunAsync(laneCancellation.Token);
            }

            var winner = await UniTask.WhenAny(UniTask.WhenAll(laneTasks), quitSource.Task);
            cursors.SetDetecting(false);
            if (winner != 0)
            {
                laneCancellation.Cancel();
                return false;
            }

            var hold = await UniTask.WhenAny(UniTask.Delay(TimeSpan.FromSeconds(endHoldSeconds), DelayType.DeltaTime, PlayerLoopTiming.Update,
                laneCancellation.Token), quitSource.Task);
            return hold == 0;
        }

        #endregion
    }
}
