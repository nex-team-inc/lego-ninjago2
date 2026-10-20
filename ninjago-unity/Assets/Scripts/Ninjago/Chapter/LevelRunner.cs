#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    public sealed class LevelResult
    {
        public int stars;
        public int heartsLeft;
        public int restarts;
        public float seconds;
        /// <summary>How close the ninjas got to the escaping Boss (0..1); -1 without a Boss chase.</summary>
        public float chaseCloseness = -1f;
    }

    /// <summary>
    /// Plays one level in one scene: its blocks back to back, each joined to the next by a link with no rest, result
    /// screen, menu or loading in between. The next block's first tell starts just before the link's camera settles.
    /// At 0 hearts (or out of time in stealth) a short "try again" beat restarts only the current block.
    /// </summary>
    public sealed class LevelRunner
    {
        readonly ChapterStage stage;
        readonly LinkPlayer links;
        readonly BlockContext context;
        readonly ViewManager views;
        readonly ChapterBlockView viewPrefab;
        readonly LocalizedString tryAgainText;
        CancellationTokenSource? quit;

        public LevelRunner(ChapterStage stage, LinkPlayer links, BlockContext context, ViewManager views, ChapterBlockView viewPrefab, LocalizedString tryAgainText)
        {
            this.stage = stage;
            this.links = links;
            this.context = context;
            this.views = views;
            this.viewPrefab = viewPrefab;
            this.tryAgainText = tryAgainText;
        }

        #region Public API

        /// <returns>Null when the player backed out.</returns>
        public async UniTask<LevelResult?> RunAsync(LevelData level, int levelIndex, int startBlock, StarRules starRules, CancellationToken cancellationToken)
        {
            using var levelQuit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            quit = levelQuit;
            var log = context.Log;
            log.Level = levelIndex;
            context.Hearts.Reset(level.hearts);
            context.Hints.BeginLevel();
            var blocks = stage.BuildLevel(level, context.Catalog, context, levelIndex + 1);
            var first = blocks[startBlock];
            first.Host(level.blocks[startBlock]);
            first.Stage();
            for (var slot = 0; slot < stage.Ninjas.Count; slot++)
            {
                var pose = first.ArrivalPose(slot);
                stage.Ninjas[slot].SetVisible(true);
                stage.Ninjas[slot].Place(pose.position, pose.rotation);
            }

            stage.CutTo(first);
            stage.SetVisible(true);
            BgmManager.Instance.CrossFadeTo(level.music, cancellationToken: cancellationToken).Forget();
            log.LevelStart(context.Players.Count);
            var startTime = Time.unscaledTime;
            var restarts = 0;
            try
            {
                for (var i = startBlock; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    log.Block = i;
                    log.BlockType = level.blocks[i].type;
                    stage.Hud.SetCurrent(i);
                    block.Host(level.blocks[i]);
                    await ShowViewAsync(level.blocks[i].type);
                    UniTask<bool> run;
                    if (i > startBlock)
                    {
                        var link = links.PlayAsync(blocks[i - 1], block, level.links[i - 1], levelQuit.Token);
                        await UniTask.Delay(TimeSpan.FromSeconds(context.Tuning.LinkSeconds - context.Tuning.TellLeadSeconds), DelayType.DeltaTime,
                            PlayerLoopTiming.Update, levelQuit.Token);
                        run = RunBlockAsync(block, false, levelQuit.Token);
                        await link;
                    }
                    else
                    {
                        run = RunBlockAsync(block, false, levelQuit.Token);
                    }

                    while (!await run)
                    {
                        restarts++;
                        log.BlockRestart(level.blocks[i].type == BlockType.Stealth ? "time_up" : "out_of_hearts");
                        await links.TryAgainAsync(block, tryAgainText, levelQuit.Token);
                        context.Hearts.Reset(level.hearts);
                        run = RunBlockAsync(block, true, levelQuit.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            finally
            {
                quit = null;
            }

            var result = new LevelResult
            {
                heartsLeft = LowestHearts(),
                restarts = restarts,
                seconds = Time.unscaledTime - startTime,
                chaseCloseness = blocks[^1] is RunnerBlock runner && level.blocks[^1].boss ? runner.ChaseCloseness : -1f,
            };
            result.stars = Stars(starRules, level.hearts, result);
            log.LevelEnd(result.stars, result.heartsLeft, restarts, result.seconds);
            return result;
        }

        public static int Stars(StarRules rules, int levelHearts, LevelResult result)
        {
            var stars = rules.starForFinish ? 1 : 0;
            if (result.heartsLeft >= Mathf.CeilToInt(rules.heartsShareForStar * levelHearts)) stars++;
            if (rules.starForNoRetry && result.restarts == 0) stars++;
            return stars;
        }

        #endregion

        #region Helpers

        async UniTask ShowViewAsync(BlockType type)
        {
            var view = UnityEngine.Object.Instantiate(viewPrefab);
            view.Initialize(type);
            view.QuitRequested += HandleQuitRequested;
            await views.ReplaceView(view, false);
        }

        void HandleQuitRequested()
        {
            quit?.Cancel();
        }

        async UniTask<bool> RunBlockAsync(ChapterBlock block, bool restart, CancellationToken cancellationToken)
        {
            context.Log.BlockStart(restart);
            var start = Time.unscaledTime;
            var cleared = await block.RunAsync(cancellationToken);
            context.Log.BlockEnd(cleared ? "cleared" : "failed", Time.unscaledTime - start);
            return cleared;
        }

        int LowestHearts()
        {
            var lowest = int.MaxValue;
            foreach (var player in context.Players) lowest = Mathf.Min(lowest, context.Hearts.Get(player.PlayerIndex));
            return lowest == int.MaxValue ? 0 : lowest;
        }

        #endregion
    }
}
