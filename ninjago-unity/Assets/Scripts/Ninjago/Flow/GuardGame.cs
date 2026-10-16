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
    /// Guard: a children's scene where goblins lob big bricks and the player protects instead of attacking, with a
    /// small repeatable shield guard (both hands up in front of the face). The result answers whether the 2D reading
    /// matched what the child meant: blocks, and each miss sorted into too late, held too long, one hand, or no try.
    /// </summary>
    public class GuardGame : NinjagoGame
    {
        [Header("Guard Config")]
        [SerializeField] GuardConfig config = null!;
        [Header("Play View")]
        [SerializeField] GuardView viewPrefab = null!;
        [Header("Result: Good Title")]
        [SerializeField] LocalizedString goodTitle = new();
        [Header("Result: Try Again Title")]
        [SerializeField] LocalizedString tryAgainTitle = new();
        [Header("Result: Outcomes Line")]
        [Tooltip("Smart string with {player} {saved} {late} {held} {half} {missed}.")]
        [SerializeField] LocalizedString outcomesLine = new();
        [Header("Result: Reading Line")]
        [Tooltip("Smart string with {player} {matched} {tries} {alone}.")]
        [SerializeField] LocalizedString readingLine = new();
        [Header("Result: Big Bricks Line")]
        [Tooltip("Smart string with {saved} {total}.")]
        [SerializeField] LocalizedString bigLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {saves}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.Guard;
        protected override bool SetupStoresRestJoints => true;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.Guard, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Scene);
        }

        NinjagoOutcome BuildOutcome(GuardScene scene)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            int saves = 0, throwsAtPlayers = 0;
            foreach (var player in scene.Players)
            {
                var number = player.PlayerIndex + 1;
                var saved = player.Count(GuardOutcome.Saved);
                saves += saved;
                for (var outcome = 0; outcome < 5; outcome++) throwsAtPlayers += player.Count((GuardOutcome)outcome);
                lines.Add(new ResultLine.Data(outcomesLine, player.Color, ("player", number), ("saved", saved), ("late", player.Count(GuardOutcome.Late)),
                    ("held", player.Count(GuardOutcome.Held)), ("half", player.Count(GuardOutcome.Half)), ("missed", player.Count(GuardOutcome.Missed))));
                lines.Add(new ResultLine.Data(readingLine, player.Color, ("player", number), ("matched", saved), ("tries", player.Tries),
                    ("alone", player.AloneRaises)));
                var prefix = $"p{number}_";
                for (var outcome = 0; outcome < 5; outcome++) details[prefix + ((GuardOutcome)outcome).ToString().ToLowerInvariant()] = player.Count((GuardOutcome)outcome);
                details[prefix + "tries"] = player.Tries;
                details[prefix + "raise_alone"] = player.AloneRaises;
            }

            lines.Add(new ResultLine.Data(bigLine, Color.white, ("saved", scene.BigSaves), ("total", scene.BigThrows)));
            details["throws"] = scene.ThrowCount;
            details["big_saved"] = scene.BigSaves;
            details["big_total"] = scene.BigThrows;

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.guardRuns > 0 && saves > progress.guardBestSaves;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.guardRuns++;
                saved.guardBestSaves = Mathf.Max(saved.guardBestSaves, saves);
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("saves", progress.guardBestSaves)));
            var good = saves * 2 >= throwsAtPlayers;
            return new NinjagoOutcome(good ? goodTitle : tryAgainTitle, lines, good ? "win" : "lose", details);
        }

        #endregion
    }
}
