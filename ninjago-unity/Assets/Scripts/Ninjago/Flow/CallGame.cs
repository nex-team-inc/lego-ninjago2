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
    /// Call: a children's scene where the player does not do the fighting: a hand raised high calls a big golden teammate
    /// who spins down and clears the goblins. The result answers whether the 2D call was readable (calls understood and
    /// missed) and whether a child could finish without wild movement (average hand speed).
    /// </summary>
    public class CallGame : NinjagoGame
    {
        [Header("Call Config")]
        [SerializeField] CallConfig config = null!;
        [Header("Play View")]
        [SerializeField] CallView viewPrefab = null!;
        [Header("Result: Title")]
        [SerializeField] LocalizedString title = new();
        [Header("Result: Calls Line")]
        [Tooltip("Smart string with {player} {understood} {missed} {team}.")]
        [SerializeField] LocalizedString callsLine = new();
        [Header("Result: Calm Line")]
        [Tooltip("Smart string with {player} {speed}.")]
        [SerializeField] LocalizedString calmLine = new();
        [Header("Result: Busy Line")]
        [Tooltip("Smart string with {player} {speed}.")]
        [SerializeField] LocalizedString busyLine = new();
        [Header("Result: Waves Line")]
        [Tooltip("Smart string with {waves} {total} {piles} {auto}.")]
        [SerializeField] LocalizedString wavesLine = new();
        [Header("Result: Calm Finish Line")]
        [SerializeField] LocalizedString calmFinishLine = new();
        [Header("Result: Not Calm Finish Line")]
        [SerializeField] LocalizedString notCalmFinishLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {piles}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.Call;
        protected override bool SetupStoresRestJoints => true;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.Call, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Scene);
        }

        NinjagoOutcome BuildOutcome(CallScene scene)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var allCalm = true;
            foreach (var player in scene.Players)
            {
                var number = player.PlayerIndex + 1;
                var speed = Mathf.RoundToInt(player.AverageHandSpeed);
                var calm = player.AverageHandSpeed <= config.CalmHandSpeed;
                allCalm &= calm;
                lines.Add(new ResultLine.Data(callsLine, player.Color, ("player", number), ("understood", player.Understood),
                    ("missed", player.Count(CallResult.Missed)), ("team", player.Count(CallResult.Team))));
                lines.Add(new ResultLine.Data(calm ? calmLine : busyLine, player.Color, ("player", number), ("speed", speed)));
                var prefix = $"p{number}_";
                details[prefix + "understood"] = player.Understood;
                details[prefix + "queued"] = player.Count(CallResult.Queued);
                details[prefix + "team"] = player.Count(CallResult.Team);
                details[prefix + "missed"] = player.Count(CallResult.Missed);
                details[prefix + "hand_speed_in_s"] = Mathf.Round(player.AverageHandSpeed * 10f) / 10f;
                details[prefix + "calm"] = calm;
            }

            lines.Add(new ResultLine.Data(wavesLine, Color.white, ("waves", scene.WavesCleared), ("total", scene.WaveCount), ("piles", scene.Piles),
                ("auto", scene.AutoHelps)));
            var calmFinish = allCalm && scene.WavesCleared == scene.WaveCount;
            lines.Add(new ResultLine.Data(calmFinish ? calmFinishLine : notCalmFinishLine, Color.white));
            details["waves_cleared"] = scene.WavesCleared;
            details["piles"] = scene.Piles;
            details["auto_helps"] = scene.AutoHelps;
            details["team_slams"] = scene.TeamSlams;
            details["calm_finish"] = calmFinish;

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var finished = scene.WavesCleared == scene.WaveCount;
            var isNewBest = finished && progress.callFewestPiles >= 0 && scene.Piles < progress.callFewestPiles;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.callRuns++;
                if (finished && (saved.callFewestPiles < 0 || scene.Piles < saved.callFewestPiles)) saved.callFewestPiles = scene.Piles;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            if (progress.callFewestPiles >= 0) lines.Add(new ResultLine.Data(bestLine, Color.white, ("piles", progress.callFewestPiles)));
            return new NinjagoOutcome(title, lines, finished ? "win" : "lose", details);
        }

        #endregion
    }
}
