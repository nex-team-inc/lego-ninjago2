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
    /// The one chapter runtime every mode runs through, built-in or player-made, with a different definition: start
    /// view, setup (calibration once), then each level in one scene, a rest view between levels (with a tracking check
    /// after confirm), and the ending view after the last. Progress is saved after every level; Continue resumes at the
    /// rest view before the next unplayed level.
    /// </summary>
    public class ChapterGame : NinjagoGame
    {
        [Header("Stage Prefab")]
        [SerializeField] ChapterStage stagePrefab = null!;
        [Header("Hand Storm Sampler")]
        [SerializeField] HandStormSampler storms = null!;
        [Header("Fight Config")]
        [Tooltip("The hand storm reads its thresholds from here.")]
        [SerializeField] FightConfig fightConfig = null!;
        [Header("Start View")]
        [SerializeField] ChapterStartView startViewPrefab = null!;
        [Header("Block View")]
        [SerializeField] ChapterBlockView blockViewPrefab = null!;
        [Header("Rest View")]
        [SerializeField] ChapterRestView restViewPrefab = null!;
        [Header("Ending View")]
        [SerializeField] ChapterEndingView endingViewPrefab = null!;
        [Header("Next Level Text")]
        [Tooltip("Smart string with {level}.")]
        [SerializeField] LocalizedString nextLevelText = new();
        [Header("Fresh Start Text")]
        [Tooltip("Smart string with {count}: normal levels before the Boss.")]
        [SerializeField] LocalizedString freshStartText = new();
        [Header("Try Again Text")]
        [SerializeField] LocalizedString tryAgainText = new();

        readonly PlayerHearts hearts = new();
        ChapterStage? stage;
        List<PlayerBody> currentPlayers = new();

        protected override GameModeType Mode => GameModeType.Chapter;
        protected override bool SetupStoresRestJoints => true;
        protected static ForgeCatalog Catalog => GameConfigsManager.Instance.ForgeCatalog;
        protected static ChapterTuning Tuning => GameConfigsManager.Instance.ChapterTuning;
        protected static ForgeConfig Names => GameConfigsManager.Instance.ForgeConfig;
        protected static ChapterModeLibrary Library => GameConfigsManager.Instance.ChapterModes;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            base.Initialize(detectionManager, viewManager);
            storms.Initialize(detectionManager, fightConfig);
        }

        #endregion

        #region Public API

        public override async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var save = PlayerDataManager.Instance.ChapterSave;
            var mode = ChapterModes.Find(GameConfigsManager.Instance.SelectedChapterModeId, Library, save) ?? Library.Modes[0].ToModeData();
            await PlayModeAsync(mode, cancellationToken);
            await BgmManager.Instance.FadeOut();
        }

        #endregion

        #region Chapter Flow

        async UniTask PlayModeAsync(ModeData mode, CancellationToken cancellationToken)
        {
            var log = new ChapterLog(mode.id, mode.builtIn);
            log.ModeStart(NumOfPlayers);
            var chapter = mode.chapter;
            var progress = PlayerDataManager.Instance.ChapterSave.FindProgress(mode.id);
            var canContinue = progress is { finished: false, nextLevel: > 0 } && progress.nextLevel < chapter.levels.Count;
            var continuing = await StartViewAsync(mode, canContinue ? progress!.nextLevel : -1, cancellationToken);
            if (continuing == null)
            {
                log.ModeEnd("abandon");
                return;
            }

            var startLevel = continuing.Value ? progress!.nextLevel : 0;
            var startBlock = 0;
            ApplyDebugJump(chapter, ref startLevel, ref startBlock);
            if (!continuing.Value) PlayerDataManager.Instance.ScopedChapterSaveUpdate(save => save.ProgressOf(mode.id).Restart());

            var players = await CalibrateAsync(false, cancellationToken);
            if (players == null)
            {
                log.ModeEnd("abandon");
                return;
            }

            var hints = new ChapterHints();
            MarkEarlierGestures(chapter, startLevel, hints);
            var runner = CreateRunner(players, log, hints);
            if (continuing.Value && progress!.lastLevel >= 0 && startBlock == 0)
            {
                var rested = await RestAsync(chapter, progress.lastLevel, progress.lastHeartsLeft, progress.lastStars, progress, log, cancellationToken);
                if (rested == null)
                {
                    log.ModeEnd("abandon");
                    return;
                }

                if (rested != players) runner = CreateRunner(players = rested, log, hints);
            }

            for (var level = startLevel; level < chapter.levels.Count; level++)
            {
                var result = await runner.RunAsync(chapter.levels[level], level, startBlock, chapter.stars, cancellationToken);
                startBlock = 0;
                if (result == null) break;
                stage!.SetVisible(false);
                var played = level;
                PlayerDataManager.Instance.ScopedChapterSaveUpdate(save =>
                    save.ProgressOf(mode.id).RecordLevel(played, result.stars, result.heartsLeft, chapter.levels.Count));
                var saved = PlayerDataManager.Instance.ChapterSave.ProgressOf(mode.id);
                if (level == chapter.levels.Count - 1)
                {
                    await EndingAsync(chapter, saved, result.chaseCloseness, cancellationToken);
                    log.ModeEnd("finished");
                    return;
                }

                var rested = await RestAsync(chapter, level, result.heartsLeft, result.stars, saved, log, cancellationToken);
                if (rested == null) break;
                if (rested != players) runner = CreateRunner(players = rested, log, hints);
            }

            log.ModeEnd("abandon");
        }

        async UniTask<bool?> StartViewAsync(ModeData mode, int nextLevel, CancellationToken cancellationToken)
        {
            var view = Instantiate(startViewPrefab);
            var line = nextLevel >= 0
                ? LocalizedStrings.WithArguments(nextLevelText, ("level", LevelName(mode.chapter, nextLevel).GetLocalizedString()))
                : LocalizedStrings.WithArguments(freshStartText, ("count", NormalLevels(mode.chapter)));
            view.Initialize(Names.NameOf(mode.name), line, nextLevel >= 0);
            var choice = new UniTaskCompletionSource<bool?>();
            view.Chosen += continuing => choice.TrySetResult(continuing);
            view.QuitRequested += () => choice.TrySetResult(null);
            await Views.PushView(view);
            return await choice.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <returns>The players to go on with (the same list unless tracking was lost and setup ran again), or null when the player backed out.</returns>
        async UniTask<List<PlayerBody>?> RestAsync(ChapterData chapter, int playedLevel, int heartsLeft, int stars, ModeProgress progress, ChapterLog log,
            CancellationToken cancellationToken)
        {
            var view = Instantiate(restViewPrefab);
            view.Initialize(chapter, Names, playedLevel, LevelName(chapter, playedLevel), heartsLeft, stars, progress);
            var confirmed = new UniTaskCompletionSource<bool>();
            view.Confirmed += () => confirmed.TrySetResult(true);
            view.QuitRequested += () => confirmed.TrySetResult(false);
            await Views.ReplaceView(view);
            var goOn = await confirmed.Task.AttachExternalCancellation(cancellationToken);
            log.Level = playedLevel;
            log.RestTime(view.SecondsShown);
            if (!goOn) return null;
            log.ConfirmPressed(view.AnalyticsScreenName);
            var players = CurrentPlayers;
            var lost = await CountLostPlayersAsync(players, chapter.rest, cancellationToken);
            if (lost == 0) return players;
            log.Recalibrate(lost);
            return await CalibrateAsync(false, cancellationToken);
        }

        async UniTask EndingAsync(ChapterData chapter, ModeProgress progress, float chaseCloseness, CancellationToken cancellationToken)
        {
            var view = Instantiate(endingViewPrefab);
            view.Initialize(chapter, Names, progress, chaseCloseness);
            var done = new UniTaskCompletionSource();
            view.Done += () => done.TrySetResult();
            await Views.ReplaceView(view);
            await done.Task.AttachExternalCancellation(cancellationToken);
        }

        #endregion

        #region Session

        protected List<PlayerBody> CurrentPlayers => currentPlayers;

        /// <summary>A level runner for these players; the stage is created on first use and keeps one ninja per player.</summary>
        protected LevelRunner CreateRunner(List<PlayerBody> players, ChapterLog log, ChapterHints hints)
        {
            currentPlayers = players;
            if (stage == null)
            {
                stage = Instantiate(stagePrefab, transform);
                stage.Initialize(Views.RootViewCamera, Detection, NumOfPlayers, hearts);
            }

            stage.SetPlayers(players);
            var context = new BlockContext(players, stage.Ninjas, hearts, hints, log, Tuning, Catalog, stage.WorldCamera, storms);
            return new LevelRunner(stage, new LinkPlayer(stage, Tuning, log), context, Views, blockViewPrefab, tryAgainText);
        }

        protected void HideStage()
        {
            if (stage != null) stage.SetVisible(false);
        }

        protected static LocalizedString LevelName(ChapterData chapter, int levelIndex)
        {
            if (chapter.levels[levelIndex].IsBoss) return Names.BossLevelName;
            var number = 0;
            for (var i = 0; i <= levelIndex; i++)
            {
                if (!chapter.levels[i].IsBoss) number++;
            }

            return Names.LevelName(number);
        }

        static int NormalLevels(ChapterData chapter)
        {
            var count = 0;
            foreach (var level in chapter.levels)
            {
                if (!level.IsBoss) count++;
            }

            return count;
        }

        // A player counts as lost when their chest is never seen during the check.
        static async UniTask<int> CountLostPlayersAsync(List<PlayerBody> players, RestRules rules, CancellationToken cancellationToken)
        {
            if (!rules.checkTracking || SimulatedBody.IsEnabled) return 0;
            var seen = new bool[players.Count];
            var elapsed = 0f;
            while (elapsed < rules.trackingCheckSeconds)
            {
                var lost = 0;
                for (var i = 0; i < players.Count; i++)
                {
                    seen[i] |= players[i].TryGetChest(out _);
                    if (!seen[i]) lost++;
                }

                if (lost == 0) return 0;
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            var missing = 0;
            foreach (var wasSeen in seen)
            {
                if (!wasSeen) missing++;
            }

            return missing;
        }

        static void MarkEarlierGestures(ChapterData chapter, int startLevel, ChapterHints hints)
        {
            var gestures = new List<Gesture>();
            for (var level = 0; level < startLevel && level < chapter.levels.Count; level++)
            {
                foreach (var block in chapter.levels[level].blocks)
                {
                    Catalog.GesturesOf(block, gestures);
                    hints.MarkSeen(gestures);
                }
            }
        }

        static void ApplyDebugJump(ChapterData chapter, ref int startLevel, ref int startBlock)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            var debug = PlayerDataManager.Instance.DebugSettings;
            if (debug.chapterJumpLevel < 0) return;
            startLevel = Mathf.Clamp(debug.chapterJumpLevel, 0, chapter.levels.Count - 1);
            startBlock = Mathf.Clamp(debug.chapterJumpBlock, 0, chapter.levels[startLevel].blocks.Count - 1);
#endif
        }

        // The chapter runs its own flow in RunAsync; NinjagoGame's round loop, the only caller of this, never runs.
        protected override UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        #endregion
    }
}
