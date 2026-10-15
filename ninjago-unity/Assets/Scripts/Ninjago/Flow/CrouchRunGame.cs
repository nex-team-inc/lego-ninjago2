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
    /// Crouch Run: a side-view stealth run where standing runs and crouching walks quietly past talking monsters. Three
    /// levels in a row; a failed level ends that player's run, and Retry starts again from the level that failed.
    /// </summary>
    public class CrouchRunGame : NinjagoGame
    {
        [Header("Run View")]
        [SerializeField] CrouchRunView viewPrefab = null!;
        [Header("Crouch Run Config")]
        [SerializeField] CrouchRunConfig config = null!;
        [Header("Result: All Clear Title")]
        [SerializeField] LocalizedString allClearTitle = new();
        [Header("Result: Failed Title")]
        [SerializeField] LocalizedString failedTitle = new();
        [Header("Result: Cleared Line")]
        [Tooltip("Smart string with {player} {level} {time} {heard}.")]
        [SerializeField] LocalizedString clearedLine = new();
        [Header("Result: Failed Line")]
        [Tooltip("Smart string with {player} {level} {heard}.")]
        [SerializeField] LocalizedString failedLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {level} {time}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        int startLevel;

        protected override GameModeType Mode => GameModeType.CrouchRun;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.CrouchRun, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(startLevel, cancellationToken)) return null;
            return BuildOutcome(view.Lanes);
        }

        NinjagoOutcome BuildOutcome(IReadOnlyList<CrouchLane> lanes)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var anyClearedAll = false;
            var furthest = 0;
            var furthestTimeLeft = -1f;
            // Retry starts from the earliest level a player failed, or from the start after a full clear.
            var nextStart = config.LevelCount;
            foreach (var lane in lanes)
            {
                anyClearedAll |= lane.ClearedAll;
                var player = lane.PlayerIndex + 1;
                var heardTotal = 0;
                foreach (var result in lane.Results)
                {
                    var level = result.levelIndex + 1;
                    heardTotal += result.timesHeard;
                    if (result.cleared)
                    {
                        lines.Add(new ResultLine.Data(clearedLine, lane.PlayerColor, ("player", player), ("level", level), ("time", result.timeLeft),
                            ("heard", result.timesHeard)));
                        if (level > furthest || (level == furthest && result.timeLeft > furthestTimeLeft))
                        {
                            furthest = level;
                            furthestTimeLeft = result.timeLeft;
                        }
                    }
                    else
                    {
                        lines.Add(new ResultLine.Data(failedLine, lane.PlayerColor, ("player", player), ("level", level), ("heard", result.timesHeard)));
                        nextStart = Mathf.Min(nextStart, result.levelIndex);
                    }
                }

                var prefix = $"p{player}_";
                details[prefix + "levels_cleared"] = lane.ClearedAll ? lane.Results.Count : lane.Results.Count - 1;
                details[prefix + "times_heard"] = heardTotal;
                details[prefix + "cleared_all"] = lane.ClearedAll;
            }

            details["start_level"] = startLevel + 1;
            startLevel = nextStart >= config.LevelCount ? 0 : nextStart;

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var improved = furthest > 0 && (furthest > progress.crouchRunFurthestLevel ||
                (furthest == progress.crouchRunFurthestLevel && furthestTimeLeft > progress.crouchRunBestTimeLeft));
            var isNewBest = improved && progress.crouchRunRuns > 0;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.crouchRunRuns++;
                if (!improved) return;
                saved.crouchRunFurthestLevel = furthest;
                saved.crouchRunBestTimeLeft = furthestTimeLeft;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            if (progress.crouchRunFurthestLevel > 0)
            {
                lines.Add(new ResultLine.Data(bestLine, Color.white, ("level", progress.crouchRunFurthestLevel), ("time", progress.crouchRunBestTimeLeft)));
            }

            return new NinjagoOutcome(anyClearedAll ? allClearTitle : failedTitle, lines, anyClearedAll ? "win" : "lose", details);
        }

        #endregion
    }
}
