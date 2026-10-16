#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Camera setup before every mini-game: every player stands in frame and holds still, and that player's body
    /// origin (chest, and resting knees for kicks) is stored; One Punch also stores the shoulders, wrists and hips and
    /// waits until they are seen. Games with hand cursors then confirm each player's hands drive a cursor; Robot Mecha
    /// has each player hold the L with both arms instead. In two-player mode a player who never appears is dropped
    /// after the solo-fallback wait.
    /// </summary>
    public class NinjagoSetupView : SimpleCanvasView
    {
        // Frames of shoulders, wrists and hips the hold needs before it may store them.
        const int minRestJointSamples = 5;

        [Header("Camera Previews Prefab")]
        [Tooltip("Instantiated as its own root: its canvas is a screen-space overlay and does not size as a nested canvas.")]
        [SerializeField] PreviewsManager previewsManagerPrefab = null!;
        [Header("Player Status Rows")]
        [SerializeField] SetupPlayerStatus[] playerStatuses = null!;
        [Header("Hand Cursor Check")]
        [Tooltip("Only in the setup view of the hand cursor games; runs after the body calibration.")]
        [SerializeField] HandCursorCheck? handCheck;
        [Header("Robot Arms Check")]
        [Tooltip("Only in the setup view of Robot Mecha; runs after the body calibration.")]
        [SerializeField] RobotArmsCheck? robotArmsCheck;
        [Header("Prompt: Stand In Frame")]
        [SerializeField] LocalizedString standPrompt = new();
        [Header("Prompt: Hold Still")]
        [SerializeField] LocalizedString holdPrompt = new();
        [Header("Prompt: Ready")]
        [SerializeField] LocalizedString readyPrompt = new();
        [Header("Prompt: Starting Solo")]
        [SerializeField] LocalizedString soloPrompt = new();
        [Header("Prompt: Show Hands And Hips")]
        [Tooltip("Games that store the rest joints: shown while a holding player's wrists or hips are out of view.")]
        [SerializeField] LocalizedString jointsPrompt = new();

        struct Hold
        {
            public bool active;
            public float startTime;
            public Vector2 anchor;
            public Vector2 sum;
            public int count;
            public Vector2 kneeDropSum;
            public int kneeDropCount;
            public BodyShape shapeSum;
            public int shapeCount;
            public RestJoints restSum;
            public int restCount;
        }

        IReadOnlyList<PlayerBody> bodies = null!;
        DetectionManager detection = null!;
        HandCursorTracker? cursors;
        RobotArmsTracker? robotArms;
        PreviewsManager previewsManager = null!;
        NinjagoPlayersConfig config = null!;
        bool[] inGoodPosition = null!;
        Hold[] holds = null!;
        bool storeRestJoints;
        bool[] missingJoints = null!;
        LocalizedString? shownPrompt;
        bool quitRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.NinjagoSetup;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "ninjago-setup";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitRequested = true;
        }

        // The previews are a screen-space overlay, which would draw over any view pushed on top (Debug Settings).
        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            previewsManager.gameObject.SetActive(false);
            await base.EnterBackground(childViewIdentifier, animate);
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            await base.EnterForeground(childViewIdentifier, animate);
            previewsManager.gameObject.SetActive(true);
        }

        #endregion

        #region Initialization

        /// <param name="aCursors">The game's hand cursors, or null for games without them (no hand check).</param>
        /// <param name="aRobotArms">Robot Mecha's arms tracker, or null for the other games (no robot arms hold).</param>
        /// <param name="aStoreRestJoints">True when the hold must also see and store each player's shoulders, wrists and hips.</param>
        public void Initialize(IReadOnlyList<PlayerBody> aBodies, DetectionManager aDetection, NinjagoPlayersConfig aConfig, HandCursorTracker? aCursors,
            RobotArmsTracker? aRobotArms, bool aStoreRestJoints)
        {
            bodies = aBodies;
            detection = aDetection;
            config = aConfig;
            cursors = aCursors;
            robotArms = aRobotArms;
            storeRestJoints = aStoreRestJoints;
            inGoodPosition = new bool[bodies.Count];
            holds = new Hold[bodies.Count];
            missingJoints = new bool[bodies.Count];
            previewsManager = Instantiate(previewsManagerPrefab);
            for (var i = 0; i < playerStatuses.Length; i++)
            {
                var used = i < bodies.Count;
                playerStatuses[i].gameObject.SetActive(used);
                if (used) playerStatuses[i].Initialize(i, bodies[i].Color);
            }
        }

        void OnDestroy()
        {
            if (previewsManager != null) Destroy(previewsManager.gameObject);
        }

        #endregion

        #region Public API

        /// <summary>Calibrates the players; returns the ones with a chest origin, or null when the player backed out.</summary>
        public async UniTask<List<PlayerBody>?> RunAsync(CancellationToken cancellationToken)
        {
            var setupStateManager = detection.SetupStateManager;
            detection.ConfigForSetup();
            previewsManager.Initialize(bodies.Count, detection.BodyPoseDetectionManager, detection.PlayAreaController, setupStateManager);
            setupStateManager.PlayerTrackerUpdated += HandlePlayerTrackerUpdated;
            foreach (var body in bodies) body.ClearOrigin();
            try
            {
                await previewsManager.MoveIn(true);
                var startTime = Time.unscaledTime;
                var calibrated = await CalibrateAsync(cancellationToken);
                if (calibrated == null) return null;

                ShowPrompt(calibrated.Count < bodies.Count ? soloPrompt : readyPrompt);
                await UniTask.Delay((int)(config.ReadyHoldSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                await previewsManager.MoveOut(true);
                detection.ConfigForGameplay();
                if (cursors != null)
                {
                    calibrated = await ConfirmHandsAsync(cursors, handCheck!, calibrated, cancellationToken);
                    if (calibrated == null) return null;
                }

                if (robotArms != null)
                {
                    calibrated = await HoldRobotArmsAsync(robotArms, robotArmsCheck!, calibrated, cancellationToken);
                    if (calibrated == null) return null;
                }

                NinjagoAnalytics.SetupComplete(bodies.Count, calibrated.Count, Time.unscaledTime - startTime);
                return calibrated;
            }
            finally
            {
                setupStateManager.PlayerTrackerUpdated -= HandlePlayerTrackerUpdated;
            }
        }

        #endregion

        #region Calibration

        async UniTask<List<PlayerBody>?> CalibrateAsync(CancellationToken cancellationToken)
        {
            var startTime = Time.unscaledTime;
            float? firstReadyTime = null;
            var missingSeenTime = float.NegativeInfinity;
            while (!quitRequested)
            {
                var now = Time.unscaledTime;
                var readyCount = 0;
                var anyHolding = false;
                var anyMissingJoints = false;
                for (var i = 0; i < bodies.Count; i++)
                {
                    if (!bodies[i].IsCalibrated && UpdateHold(i, now))
                    {
                        firstReadyTime ??= now;
                        NinjagoAnalytics.PlayerCalibrated(i, now - startTime);
                        SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                    }

                    if (bodies[i].IsCalibrated) readyCount++;
                    else if (bodies[i].TryGetChest(out _)) missingSeenTime = now;
                    anyHolding |= holds[i].active;
                    anyMissingJoints |= missingJoints[i];
                }

                if (readyCount == bodies.Count || SoloTimeUp(now, firstReadyTime, missingSeenTime)) return CollectCalibrated();
                ShowPrompt(anyMissingJoints ? jointsPrompt : anyHolding ? holdPrompt : standPrompt);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return null;
        }

        // Returns true on the frame this player's origin is stored.
        bool UpdateHold(int playerIndex, float now)
        {
            var body = bodies[playerIndex];
            ref var hold = ref holds[playerIndex];
            missingJoints[playerIndex] = false;
            if (SimulatedBody.IsEnabled)
            {
                body.SetOrigin(body.TryGetChest(out var simulatedChest) ? simulatedChest : Vector2.zero, null, null);
                playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Ready);
                return true;
            }

            if (!inGoodPosition[playerIndex] || !body.TryGetChest(out var chest))
            {
                hold.active = false;
                playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Waiting);
                return false;
            }

            if (!hold.active || Vector2.Distance(chest, hold.anchor) / body.RawPpi > config.CalibrationStillnessInches)
            {
                hold = new Hold { active = true, startTime = now, anchor = chest };
            }

            hold.sum += chest;
            hold.count++;
            if (body.TryGetKneeDrops(out var kneeDrops))
            {
                hold.kneeDropSum += kneeDrops;
                hold.kneeDropCount++;
            }

            if (body.TrySampleShape(out var shape))
            {
                hold.shapeSum = hold.shapeSum.Plus(shape);
                hold.shapeCount++;
            }

            if (storeRestJoints && body.TrySampleRestJoints(out var joints))
            {
                hold.restSum = hold.restSum.Plus(joints);
                hold.restCount++;
            }

            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Holding);
            if (now - hold.startTime < config.CalibrationSeconds) return false;
            if (storeRestJoints && hold.restCount < minRestJointSamples)
            {
                missingJoints[playerIndex] = true;
                return false;
            }

            body.SetOrigin(hold.sum / hold.count, hold.kneeDropCount > 0 ? hold.kneeDropSum / hold.kneeDropCount : null,
                hold.shapeCount > 0 ? hold.shapeSum.Scaled(1f / hold.shapeCount) : null);
            if (storeRestJoints) body.SetRestJoints(hold.restSum.Scaled(1f / hold.restCount));
            hold.active = false;
            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Ready);
            return true;
        }

        // Every calibrated player fills their ring with a cursor.
        UniTask<List<PlayerBody>?> ConfirmHandsAsync(HandCursorTracker handCursors, HandCursorCheck check, List<PlayerBody> calibrated,
            CancellationToken cancellationToken)
        {
            handCursors.SetDetecting(true);
            check.Show(calibrated, handCursors, detection, bodies.Count);
            return ConfirmEachAsync(calibrated, slot => check.Tick(slot, Time.unscaledDeltaTime, config.HandCheckSeconds),
                (player, seconds) => NinjagoAnalytics.PlayerHandsConfirmed(player.PlayerIndex, seconds), check.ShowReady, cancellationToken);
        }

        // Every calibrated player holds the L with both arms; the hold stores that player's hand origin and elbow reference.
        UniTask<List<PlayerBody>?> HoldRobotArmsAsync(RobotArmsTracker tracker, RobotArmsCheck check, List<PlayerBody> calibrated,
            CancellationToken cancellationToken)
        {
            tracker.SetDetecting(true);
            check.Show(calibrated, tracker, detection, bodies.Count);
            return ConfirmEachAsync(calibrated, slot => check.Tick(slot, Time.unscaledDeltaTime),
                (player, seconds) => MechaAnalytics.SetupHold(player.PlayerIndex, seconds, tracker.NeutralOffset(player.PlayerIndex),
                    tracker.TorsoReference(player.PlayerIndex)), check.ShowReady, cancellationToken);
        }

        // tick(slot) is true on the frame that player passes the game's check; in two-player mode a player who never does
        // is dropped after the solo-fallback wait once they are out of view, like a body that never appears.
        async UniTask<List<PlayerBody>?> ConfirmEachAsync(List<PlayerBody> calibrated, Func<int, bool> tick, Action<PlayerBody, float> onConfirmed,
            Action showReady, CancellationToken cancellationToken)
        {
            var confirmed = new List<PlayerBody>(calibrated.Count);
            var done = new bool[calibrated.Count];
            var startTime = Time.unscaledTime;
            float? firstConfirmTime = null;
            var missingSeenTime = float.NegativeInfinity;
            while (!quitRequested)
            {
                var now = Time.unscaledTime;
                for (var slot = 0; slot < calibrated.Count; slot++)
                {
                    if (done[slot]) continue;
                    if (!tick(slot))
                    {
                        if (calibrated[slot].TryGetChest(out _)) missingSeenTime = now;
                        continue;
                    }

                    done[slot] = true;
                    confirmed.Add(calibrated[slot]);
                    firstConfirmTime ??= now;
                    onConfirmed(calibrated[slot], now - startTime);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                }

                if (confirmed.Count == calibrated.Count || SoloTimeUp(now, firstConfirmTime, missingSeenTime))
                {
                    showReady();
                    await UniTask.Delay((int)(config.ReadyHoldSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                    confirmed.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
                    return confirmed;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return null;
        }

        // Two-player mode closes the empty half only for a player who is out of view: anyone still in frame is waited for.
        bool SoloTimeUp(float now, float? firstDoneTime, float missingSeenTime)
        {
            return firstDoneTime.HasValue && now - Mathf.Max(firstDoneTime.Value, missingSeenTime) >= config.SoloFallbackSeconds;
        }

        List<PlayerBody> CollectCalibrated()
        {
            var calibrated = new List<PlayerBody>(bodies.Count);
            foreach (var body in bodies)
            {
                if (body.IsCalibrated) calibrated.Add(body);
            }

            return calibrated;
        }

        void HandlePlayerTrackerUpdated((int playerIndex, SetupSummary setupSummary) update)
        {
            if (update.playerIndex >= inGoodPosition.Length) return;
            var state = update.setupSummary.setupStateType;
            inGoodPosition[update.playerIndex] = state is SetupStateType.WaitingForRaisingHand or SetupStateType.Playing;
        }

        void ShowPrompt(LocalizedString prompt)
        {
            if (shownPrompt == prompt) return;
            shownPrompt = prompt;
            previewsManager.SetPromptText(prompt.GetLocalizedString());
        }

        #endregion
    }
}
