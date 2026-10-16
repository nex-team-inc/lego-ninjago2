#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// One Punch on one shared screen: the street seen from its still camera behind the ninjas, the HUD, and one
    /// corner camera preview showing every player with their indicator.
    /// </summary>
    public class OnePunchView : SimpleCanvasView
    {
        [Header("Street Feed")]
        [SerializeField] RawImage feed = null!;
        [Header("HUD")]
        [SerializeField] OnePunchHud hud = null!;
        [Header("Street Prefab")]
        [SerializeField] OnePunchStreet streetPrefab = null!;
        [Header("Street Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 streetOrigin = new(11000f, 0f, 0f);
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly UniTaskCompletionSource quitSource = new();
        OnePunchStreet street = null!;
        RenderTexture texture = null!;
        PunchTracker tracker = null!;
        OnePunchConfig config = null!;
        int handScaleSamplesAtStart;

        public OnePunchStreet Street => street;
        /// <summary>The hand detector reported a hand scale during the fight: the toward-camera cue existed.</summary>
        public bool HadDepthCue => tracker.HandScaleSamples > handScaleSamplesAtStart;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.OnePunch;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "one-punch";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, OnePunchConfig aConfig, PunchTracker aTracker, DetectionManager detection, int requestedPlayers)
        {
            config = aConfig;
            tracker = aTracker;
            texture = new RenderTexture(Screen.width, Screen.height, 24) { name = "OnePunchStreet" };
            feed.texture = texture;
            street = Instantiate(streetPrefab, streetOrigin, Quaternion.identity);
            street.Initialize(players, config, tracker, hud, texture);

            var indicatorPlayers = new List<int>(players.Count);
            foreach (var player in players) indicatorPlayers.Add(player.PlayerIndex);
            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            if (street != null) Destroy(street.gameObject);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        #endregion

        #region Public API

        /// <summary>Plays every wave, then holds the CLEARED or DOWN banner; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            handScaleSamplesAtStart = tracker.HandScaleSamples;
            tracker.SetDetecting(true);
            try
            {
                var winner = await UniTask.WhenAny(street.RunAsync(runCancellation.Token), quitSource.Task);
                if (winner == 0)
                {
                    winner = await UniTask.WhenAny(UniTask.Delay((int)(config.EndHoldSeconds * 1000f), DelayType.DeltaTime, cancellationToken: runCancellation.Token),
                        quitSource.Task);
                }

                if (winner == 0) return true;
                runCancellation.Cancel();
                return false;
            }
            finally
            {
                tracker.SetDetecting(false);
            }
        }

        #endregion
    }
}
