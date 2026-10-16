#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// One Punch: monsters walk up a street toward the ninjas and one forward punch throws the nearest one away; a
    /// monster that arrives pins its ninja until the hips wiggle it off. Setup also stores the shoulders, wrists and hips.
    /// The result answers the detection question: how each player's tries were read, and whether a toward-camera cue existed.
    /// </summary>
    public class OnePunchGame : NinjagoGame
    {
        [Header("Punch Tracker")]
        [SerializeField] PunchTracker tracker = null!;
        [Header("One Punch Config")]
        [SerializeField] OnePunchConfig config = null!;
        [Header("Street View")]
        [SerializeField] OnePunchView viewPrefab = null!;
        [Header("Result: Cleared Title")]
        [SerializeField] LocalizedString clearedTitle = new();
        [Header("Result: Down Title")]
        [SerializeField] LocalizedString downTitle = new();
        [Header("Result: Tries Line")]
        [Tooltip("Smart string with {player} {ext} {cam} {slash} {none}.")]
        [SerializeField] LocalizedString triesLine = new();
        [Header("Result: Player Line")]
        [Tooltip("Smart string with {player} {thrown} {pinned} {wiggles} {health} {max}.")]
        [SerializeField] LocalizedString playerLine = new();
        [Header("Result: Waves Line With Cue")]
        [Tooltip("Smart string with {waves} {total}; says the hand scale was the toward-camera cue.")]
        [SerializeField] LocalizedString wavesCueLine = new();
        [Header("Result: Waves Line Without Cue")]
        [Tooltip("Smart string with {waves} {total}; says there was no depth cue.")]
        [SerializeField] LocalizedString wavesNoCueLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {waves} {ext} {cam} {slash} {none} (tries over every run).")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.OnePunch;
        protected override bool SetupStoresRestJoints => true;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            base.Initialize(detectionManager, viewManager);
            tracker.Initialize(detectionManager, config, Bodies);
        }

        #endregion

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            foreach (var player in activePlayers) OnePunchAnalytics.SetupJoints(player.PlayerIndex, player.Rest);
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, tracker, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.OnePunch, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Street, view.HadDepthCue);
        }

        NinjagoOutcome BuildOutcome(OnePunchStreet street, bool hadDepthCue)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            int extension = 0, towardCamera = 0, sideSlash = 0, nothing = 0;
            foreach (var player in street.Players)
            {
                var number = player.PlayerIndex + 1;
                var ext = player.Tries(PunchSignal.Extension);
                var cam = player.Tries(PunchSignal.TowardCamera);
                var slash = player.Tries(PunchSignal.SideSlash);
                var none = player.Tries(PunchSignal.None);
                extension += ext;
                towardCamera += cam;
                sideSlash += slash;
                nothing += none;
                lines.Add(new ResultLine.Data(triesLine, player.Color, ("player", number), ("ext", ext), ("cam", cam), ("slash", slash), ("none", none)));
                lines.Add(new ResultLine.Data(playerLine, player.Color, ("player", number), ("thrown", player.Thrown), ("pinned", player.TimesPinned),
                    ("wiggles", player.Wiggles), ("health", Mathf.CeilToInt(player.Health)), ("max", Mathf.CeilToInt(player.MaxHealth))));
                var prefix = $"p{number}_";
                details[prefix + "extension"] = ext;
                details[prefix + "toward_camera"] = cam;
                details[prefix + "side_slash"] = slash;
                details[prefix + "nothing"] = none;
                details[prefix + "thrown"] = player.Thrown;
                details[prefix + "pinned"] = player.TimesPinned;
                details[prefix + "wiggles"] = player.Wiggles;
                details[prefix + "health"] = Mathf.Round(player.Health * 100f) / 100f;
            }

            lines.Add(new ResultLine.Data(hadDepthCue ? wavesCueLine : wavesNoCueLine, Color.white, ("waves", street.WavesCleared), ("total", config.WaveCount)));
            details["waves_cleared"] = street.WavesCleared;
            details["depth_cue"] = hadDepthCue;
            details["fight_s"] = Mathf.Round(street.FightSeconds * 10f) / 10f;

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.onePunchRuns > 0 && street.WavesCleared > progress.onePunchBestWaves;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.onePunchRuns++;
                saved.onePunchBestWaves = Mathf.Max(saved.onePunchBestWaves, street.WavesCleared);
                saved.onePunchExtensionTries += extension;
                saved.onePunchTowardCameraTries += towardCamera;
                saved.onePunchSideSlashTries += sideSlash;
                saved.onePunchNothingTries += nothing;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("waves", progress.onePunchBestWaves), ("ext", progress.onePunchExtensionTries),
                ("cam", progress.onePunchTowardCameraTries), ("slash", progress.onePunchSideSlashTries), ("none", progress.onePunchNothingTries)));
            return street.IsDown
                ? new NinjagoOutcome(downTitle, lines, "lose", details)
                : new NinjagoOutcome(clearedTitle, lines, "win", details);
        }

        #endregion
    }
}
