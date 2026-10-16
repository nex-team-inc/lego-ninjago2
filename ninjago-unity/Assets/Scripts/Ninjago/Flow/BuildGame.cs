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
    /// Build: a children's scene where a calm, repeatable click (hands together) adds one brick at a time to a bridge,
    /// and a finished bridge opens the gate. The result answers whether the 2D reading kept up: claps counted, near claps
    /// not counted, the time spent and whether the way opened.
    /// </summary>
    public class BuildGame : NinjagoGame
    {
        [Header("Build Config")]
        [SerializeField] BuildConfig config = null!;
        [Header("Play View")]
        [SerializeField] BuildView viewPrefab = null!;
        [Header("Result: Open Title")]
        [SerializeField] LocalizedString openTitle = new();
        [Header("Result: Shut Title")]
        [SerializeField] LocalizedString shutTitle = new();
        [Header("Result: Claps Line")]
        [Tooltip("Smart string with {player} {counted} {near}.")]
        [SerializeField] LocalizedString clapsLine = new();
        [Header("Result: Open Line")]
        [Tooltip("Smart string with {placed} {total} {time}.")]
        [SerializeField] LocalizedString openLine = new();
        [Header("Result: Shut Line")]
        [Tooltip("Smart string with {placed} {total} {time}.")]
        [SerializeField] LocalizedString shutLine = new();
        [Header("Result: Reading Line")]
        [Tooltip("Smart string with {percent} {perMinute}.")]
        [SerializeField] LocalizedString readingLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {time}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.Build;
        protected override bool SetupStoresRestJoints => true;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.Build, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Scene);
        }

        NinjagoOutcome BuildOutcome(BuildScene scene)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            int counted = 0, near = 0;
            foreach (var player in scene.Players)
            {
                var number = player.PlayerIndex + 1;
                counted += player.Count(BuildGesture.Counted);
                near += player.Count(BuildGesture.Near);
                lines.Add(new ResultLine.Data(clapsLine, player.Color, ("player", number), ("counted", player.Count(BuildGesture.Counted)),
                    ("near", player.Count(BuildGesture.Near))));
                details[$"p{number}_counted"] = player.Count(BuildGesture.Counted);
                details[$"p{number}_near"] = player.Count(BuildGesture.Near);
            }

            var time = Mathf.Round(scene.Elapsed * 10f) / 10f;
            lines.Add(new ResultLine.Data(scene.IsOpen ? openLine : shutLine, Color.white, ("placed", scene.Placed), ("total", scene.Total), ("time", time)));
            var percent = counted + near > 0 ? Mathf.RoundToInt(100f * counted / (counted + near)) : 0;
            var perMinute = scene.Elapsed > 0f ? Mathf.RoundToInt(counted * 60f / scene.Elapsed) : 0;
            lines.Add(new ResultLine.Data(readingLine, Color.white, ("percent", percent), ("perMinute", perMinute)));
            details["placed"] = scene.Placed;
            details["total"] = scene.Total;
            details["elapsed_s"] = time;
            details["way_open"] = scene.IsOpen;
            details["read_percent"] = percent;

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = scene.IsOpen && progress.buildBestSeconds >= 0f && scene.Elapsed < progress.buildBestSeconds;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.buildRuns++;
                if (scene.IsOpen && (saved.buildBestSeconds < 0f || scene.Elapsed < saved.buildBestSeconds)) saved.buildBestSeconds = scene.Elapsed;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            if (progress.buildBestSeconds >= 0f) lines.Add(new ResultLine.Data(bestLine, Color.white, ("time", Mathf.Round(progress.buildBestSeconds * 10f) / 10f)));
            return new NinjagoOutcome(scene.IsOpen ? openTitle : shutTitle, lines, scene.IsOpen ? "win" : "lose", details);
        }

        #endregion
    }
}
