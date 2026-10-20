#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Forge: a game mode that builds game modes out of the same pieces. Home (templates, empty, Surprise me, and
    /// edit, rename, copy or delete any mode) and the editor are views; Test plays the current level right away in the
    /// chapter runtime, calibrating only if nobody is calibrated yet, then comes back. Save as mode stores a player-made
    /// mode with a generated name; the main menu lists it from then on.
    /// </summary>
    public class ForgeGame : ChapterGame
    {
        [Header("Forge Home View")]
        [SerializeField] ForgeHomeView homeViewPrefab = null!;
        [Header("Forge Editor View")]
        [SerializeField] ForgeEditorView editorViewPrefab = null!;
        [Header("Saved Text")]
        [SerializeField] LocalizedString savedText = new();
        [Header("Fix Problems Text")]
        [SerializeField] LocalizedString fixProblemsText = new();
        [Header("Built-in Locked Text")]
        [SerializeField] LocalizedString builtInLockedText = new();
        [Header("Deleted Text")]
        [SerializeField] LocalizedString deletedText = new();

        readonly ForgeLog forgeLog = new();
        readonly System.Random random = new();
        int selectedMode;

        protected override GameModeType Mode => GameModeType.Forge;
        static float LinkSeconds => Tuning.LinkSeconds;

        #region Public API

        public override async UniTask RunAsync(CancellationToken cancellationToken)
        {
            forgeLog.Opened();
            var first = true;
            while (true)
            {
                var document = await HomeAsync(first, cancellationToken);
                first = false;
                if (document == null) break;
                await EditAsync(document, cancellationToken);
            }

            await BgmManager.Instance.FadeOut();
        }

        #endregion

        #region Home

        async UniTask<ForgeDocument?> HomeAsync(bool push, CancellationToken cancellationToken)
        {
            var view = Instantiate(homeViewPrefab);
            view.Initialize();
            var builtIns = BuiltIns();
            var request = new UniTaskCompletionSource<(ForgeHomeAction action, int index)?>();
            view.ActionRequested += (action, index) => request.TrySetResult((action, index));
            view.BackRequested += () => request.TrySetResult(null);
            ShowHome(view, builtIns, null);
            if (push) await Views.PushView(view);
            else await Views.ReplaceView(view);
            while (true)
            {
                var choice = await request.Task.AttachExternalCancellation(cancellationToken);
                if (choice == null) return null;
                request = new UniTaskCompletionSource<(ForgeHomeAction action, int index)?>();
                var modes = AllModes();
                var selected = modes.Count > 0 ? modes[Mathf.Clamp(selectedMode, 0, modes.Count - 1)] : null;
                LocalizedString? status = null;
                switch (choice.Value.action)
                {
                    case ForgeHomeAction.Template when choice.Value.index < builtIns.Count:
                        return Open(builtIns[choice.Value.index].CopyAsPlayerMade("", Names.RandomName(random)), "", -1);
                    case ForgeHomeAction.Empty:
                        return Open(new ModeData { name = Names.RandomName(random), icon = ModeIcon.Brick }, "", -1);
                    case ForgeHomeAction.Surprise:
                        var seed = random.Next(1, 100000);
                        ChapterDebugHooks.LastSeed = seed;
                        forgeLog.SeedGenerated(seed);
                        return Open(ChapterGenerator.Generate(seed, Catalog, Names, ""), "", seed);
                    case ForgeHomeAction.PreviousMode:
                        selectedMode = (selectedMode - 1 + Mathf.Max(1, modes.Count)) % Mathf.Max(1, modes.Count);
                        break;
                    case ForgeHomeAction.NextMode:
                        selectedMode = (selectedMode + 1) % Mathf.Max(1, modes.Count);
                        break;
                    case ForgeHomeAction.Edit when selected != null:
                        return selected.builtIn
                            ? Open(selected.CopyAsPlayerMade("", Names.RandomName(random)), "", -1)
                            : Open(selected.CopyAsPlayerMade(selected.id, selected.name.Copy()), selected.id, selected.name.seed);
                    case ForgeHomeAction.Rename when selected is { builtIn: false }:
                        PlayerDataManager.Instance.ScopedChapterSaveUpdate(save => save.FindPlayerMode(selected.id)!.name = Names.RandomName(random, selected.name.seed));
                        forgeLog.ModeRenamed(selected);
                        break;
                    case ForgeHomeAction.Copy when selected != null:
                        var copy = selected.CopyAsPlayerMade(NewId(), Names.RandomName(random, selected.name.seed));
                        PlayerDataManager.Instance.ScopedChapterSaveUpdate(save => save.playerModes.Add(copy));
                        forgeLog.ModeCopied(copy, selected.id);
                        selectedMode = AllModes().Count - 1;
                        break;
                    case ForgeHomeAction.Delete when selected is { builtIn: false }:
                        PlayerDataManager.Instance.ScopedChapterSaveUpdate(save =>
                        {
                            save.playerModes.RemoveAll(mode => mode.id == selected.id);
                            save.progress.RemoveAll(progress => progress.modeId == selected.id);
                        });
                        forgeLog.ModeDeleted(selected.id);
                        selectedMode = Mathf.Max(0, selectedMode - 1);
                        status = deletedText;
                        break;
                    case ForgeHomeAction.Rename:
                    case ForgeHomeAction.Delete:
                        status = builtInLockedText;
                        break;
                }

                ShowHome(view, builtIns, status);
            }
        }

        void ShowHome(ForgeHomeView view, List<ModeData> builtIns, LocalizedString? status)
        {
            var modes = AllModes();
            var selected = modes.Count > 0 ? modes[Mathf.Clamp(selectedMode, 0, modes.Count - 1)] : null;
            view.Show(builtIns, selected, mode => Names.NameOf(mode.name), status);
        }

        ForgeDocument Open(ModeData mode, string editingId, int seed) => new(mode, editingId, seed, Catalog, LinkSeconds);

        #endregion

        #region Editor

        async UniTask EditAsync(ForgeDocument document, CancellationToken cancellationToken)
        {
            var view = Instantiate(editorViewPrefab);
            view.Initialize();
            var request = new UniTaskCompletionSource<(ForgeEditorAction action, int index)?>();
            view.ActionRequested += (action, index) => request.TrySetResult((action, index));
            view.BackRequested += () => request.TrySetResult(null);
            Show(view, document, null);
            await Views.ReplaceView(view);
            while (true)
            {
                var choice = await request.Task.AttachExternalCancellation(cancellationToken);
                if (choice == null) return;
                request = new UniTaskCompletionSource<(ForgeEditorAction action, int index)?>();
                if (choice.Value.action == ForgeEditorAction.Test)
                {
                    await TestAsync(document, cancellationToken);
                    view = Instantiate(editorViewPrefab);
                    view.Initialize();
                    view.ActionRequested += (action, index) => request.TrySetResult((action, index));
                    view.BackRequested += () => request.TrySetResult(null);
                    Show(view, document, null);
                    await Views.ReplaceView(view);
                    continue;
                }

                Show(view, document, Apply(document, choice.Value.action, choice.Value.index));
            }
        }

        /// <returns>A status line to show, or null.</returns>
        LocalizedString? Apply(ForgeDocument document, ForgeEditorAction action, int index)
        {
            switch (action)
            {
                case ForgeEditorAction.PreviousLevel: document.SelectLevel(-1); break;
                case ForgeEditorAction.NextLevel: document.SelectLevel(1); break;
                case ForgeEditorAction.AddLevel: document.AddLevel(); break;
                case ForgeEditorAction.RemoveLevel: document.RemoveLevel(); break;
                case ForgeEditorAction.Theme: document.CycleTheme(); break;
                case ForgeEditorAction.Slot when index < document.CurrentLevel.blocks.Count: document.SelectBlock(index); break;
                case ForgeEditorAction.Slot:
                    if (document.AddBlock()) forgeLog.BlockAdded(document);
                    break;
                case ForgeEditorAction.Type: document.CycleType(); break;
                case ForgeEditorAction.Ride: document.CycleRide(); break;
                case ForgeEditorAction.Preset: document.CyclePreset(); break;
                case ForgeEditorAction.Hints: document.CycleHints(); break;
                case ForgeEditorAction.Boss: document.ToggleBoss(); break;
                case ForgeEditorAction.Mix: document.ToggleMix(); break;
                case ForgeEditorAction.Window: document.CycleWindow(); break;
                case ForgeEditorAction.Speed: document.CycleSpeed(); break;
                case ForgeEditorAction.Count: document.CycleCount(); break;
                case ForgeEditorAction.Zone: document.CycleZone(); break;
                case ForgeEditorAction.Length: document.CycleLength(); break;
                case ForgeEditorAction.MoveLeft:
                    if (document.MoveBlock(-1)) forgeLog.BlockMoved(document);
                    break;
                case ForgeEditorAction.MoveRight:
                    if (document.MoveBlock(1)) forgeLog.BlockMoved(document);
                    break;
                case ForgeEditorAction.Remove:
                    forgeLog.BlockRemoved(document);
                    document.RemoveBlock();
                    break;
                case ForgeEditorAction.Link: document.CycleLink(); break;
                case ForgeEditorAction.Save: return Save(document);
                case ForgeEditorAction.Name:
                    document.Rename(Names.RandomName(random, document.Seed));
                    if (string.IsNullOrEmpty(document.EditingId)) break;
                    PlayerDataManager.Instance.ScopedChapterSaveUpdate(save => save.FindPlayerMode(document.EditingId)!.name = document.Mode.name.Copy());
                    forgeLog.ModeRenamed(document.Mode);
                    break;
            }

            return null;
        }

        LocalizedString Save(ForgeDocument document)
        {
            if (!document.IsValid) return fixProblemsText;
            var isNew = string.IsNullOrEmpty(document.EditingId);
            var id = isNew ? NewId() : document.EditingId;
            document.SavedAs(id);
            var saved = document.Mode.CopyAsPlayerMade(id, document.Mode.name.Copy());
            PlayerDataManager.Instance.ScopedChapterSaveUpdate(save =>
            {
                save.playerModes.RemoveAll(mode => mode.id == id);
                save.playerModes.Add(saved);
            });
            forgeLog.ModeSaved(saved, document.Seed);
            if (isNew && document.Seed >= 0) forgeLog.SeedKept(document.Seed, id);
            selectedMode = AllModes().FindIndex(mode => mode.id == id);
            return savedText;
        }

        void Show(ForgeEditorView view, ForgeDocument document, LocalizedString? status)
        {
            view.Show(document, Catalog, Names, Names.NameOf(document.Mode.name), status);
            forgeLog.ProblemsShown(document.Problems);
        }

        #endregion

        #region Test

        // Plays the level being edited in the same chapter runtime; nobody calibrated yet means setup runs first.
        async UniTask TestAsync(ForgeDocument document, CancellationToken cancellationToken)
        {
            forgeLog.TestStarted(document);
            var players = new List<PlayerBody>();
            foreach (var body in Bodies)
            {
                if (body.IsCalibrated) players.Add(body);
            }

            if (players.Count == 0)
            {
                var calibrated = await CalibrateAsync(false, cancellationToken);
                if (calibrated == null) return;
                players = calibrated;
            }

            var log = new ChapterLog(string.IsNullOrEmpty(document.EditingId) ? "forge_test" : document.EditingId, false);
            var runner = CreateRunner(players, log, new ChapterHints());
            await runner.RunAsync(document.CurrentLevel.Copy(), document.Level, 0, document.Mode.chapter.stars, cancellationToken);
            HideStage();
        }

        #endregion

        #region Helpers

        static List<ModeData> BuiltIns()
        {
            var modes = new List<ModeData>();
            foreach (var definition in Library.Modes) modes.Add(definition.ToModeData());
            return modes;
        }

        static List<ModeData> AllModes() => ChapterModes.All(Library, PlayerDataManager.Instance.ChapterSave);

        static string NewId()
        {
            var number = 0;
            PlayerDataManager.Instance.ScopedChapterSaveUpdate(save => number = save.nextModeNumber++);
            return $"player_{number}";
        }

        #endregion
    }
}
