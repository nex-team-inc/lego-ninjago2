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
    /// The play view of a children's game: one 3D scene seen from its still camera behind the ninjas, the shared HUD,
    /// and one corner camera preview showing every player with their indicator. Back quits.
    /// </summary>
    public abstract class KidsSceneView : SimpleCanvasView
    {
        [Header("World Feed")]
        [SerializeField] RawImage feed = null!;
        [Header("HUD")]
        [SerializeField] KidsHud hud = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly UniTaskCompletionSource quitSource = new();

        protected KidsHud Hud => hud;
        protected RenderTexture Target { get; private set; } = null!;

        #region View Implementation

        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        protected void InitializeFeed(IReadOnlyList<PlayerBody> players, DetectionManager detection, int requestedPlayers)
        {
            Target = new RenderTexture(Screen.width, Screen.height, 24) { name = $"{AnalyticsScreenName}-scene" };
            feed.texture = Target;
            var indicatorPlayers = new List<int>(players.Count);
            foreach (var player in players) indicatorPlayers.Add(player.PlayerIndex);
            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        protected virtual void OnDestroy()
        {
            if (Target == null) return;
            Target.Release();
            Destroy(Target);
        }

        #endregion

        #region Play

        /// <summary>Runs the scene, then holds its last banner; false when the player backed out.</summary>
        protected async UniTask<bool> RunSceneAsync(Func<CancellationToken, UniTask> runScene, float endHoldSeconds, CancellationToken cancellationToken)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            var winner = await UniTask.WhenAny(runScene(runCancellation.Token), quitSource.Task);
            if (winner == 0)
            {
                winner = await UniTask.WhenAny(UniTask.Delay(TimeSpan.FromSeconds(endHoldSeconds), DelayType.DeltaTime, cancellationToken: runCancellation.Token),
                    quitSource.Task);
            }

            if (winner == 0) return true;
            runCancellation.Cancel();
            return false;
        }

        #endregion
    }
}
