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
    /// Robot Mecha: hold both arms in a robot L to steer a brick mecha whose pink laser never stops, and survive the wave
    /// of rushers and archers. Setup adds a robot arms hold; the result shows time, kills, hearts lost and pose percent.
    /// </summary>
    public class RobotMechaGame : NinjagoGame
    {
        [Header("Robot Arms Tracker")]
        [SerializeField] RobotArmsTracker arms = null!;
        [Header("Robot Arms Config")]
        [SerializeField] RobotArmsConfig armsConfig = null!;
        [Header("Fight View")]
        [SerializeField] MechaFightView viewPrefab = null!;
        [Header("Fight Config")]
        [SerializeField] MechaFightConfig config = null!;
        [Header("Result: Survived Title")]
        [SerializeField] LocalizedString survivedTitle = new();
        [Header("Result: Down Title")]
        [SerializeField] LocalizedString downTitle = new();
        [Header("Result: Player Line")]
        [Tooltip("Smart string with {player} {time} {kills} {lost} {pose}.")]
        [SerializeField] LocalizedString playerLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {time} {pose}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.RobotMecha;
        protected override RobotArmsTracker? SetupRobotArms => arms;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            base.Initialize(detectionManager, viewManager);
            arms.Initialize(detectionManager, armsConfig, Bodies);
        }

        #endregion

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, arms, armsConfig, config, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.RobotMecha, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Lanes);
        }

        NinjagoOutcome BuildOutcome(IReadOnlyList<MechaLane> lanes)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var anySurvived = false;
            var bestTime = 0f;
            var bestPose = 0;
            foreach (var lane in lanes)
            {
                anySurvived |= !lane.IsDown;
                bestTime = Mathf.Max(bestTime, lane.SurvivedSeconds);
                bestPose = Mathf.Max(bestPose, lane.PosePercent);
                var player = lane.PlayerIndex + 1;
                lines.Add(new ResultLine.Data(playerLine, lane.PlayerColor, ("player", player), ("time", lane.SurvivedSeconds), ("kills", lane.Kills),
                    ("lost", lane.HeartsLost), ("pose", lane.PosePercent)));
                var prefix = $"p{player}_";
                details[prefix + "survive_s"] = Mathf.Round(lane.SurvivedSeconds * 10f) / 10f;
                details[prefix + "kills"] = lane.Kills;
                details[prefix + "hearts_lost"] = lane.HeartsLost;
                details[prefix + "pose_percent"] = lane.PosePercent;
                details[prefix + "down"] = lane.IsDown;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.robotMechaRuns > 0 && (bestTime > progress.robotMechaBestSurviveSeconds || bestPose > progress.robotMechaBestPosePercent);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.robotMechaRuns++;
                saved.robotMechaBestSurviveSeconds = Mathf.Max(saved.robotMechaBestSurviveSeconds, bestTime);
                saved.robotMechaBestPosePercent = Mathf.Max(saved.robotMechaBestPosePercent, bestPose);
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("time", progress.robotMechaBestSurviveSeconds), ("pose", progress.robotMechaBestPosePercent)));
            details["wave_s"] = config.WaveSeconds;
            return new NinjagoOutcome(anySurvived ? survivedTitle : downTitle, lines, anySurvived ? "win" : "lose", details);
        }

        #endregion
    }
}
