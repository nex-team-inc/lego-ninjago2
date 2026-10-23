#nullable enable

using System;
using System.Collections.Generic;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex
{
    public class GameModeSelectionView : SimpleCanvasView
    {
        [Header("Config")]
        [SerializeField] GameModeConfig gameModeConfig = null!;

        [Header("Grid")]
        [SerializeField] GridLayoutGroup gridLayout = null!;
        [SerializeField] GridKeyResponder gridKeyResponder = null!;
        [SerializeField] GameModeSelectionButton buttonPrefab = null!;
        [Header("Grid Scroll View")]
        [SerializeField] RectTransform gridScrollView = null!;
        [Header("Grid Right Margin (Beside Column, Full Width)")]
        [SerializeField] Vector2 gridRightMargin = new(474f, 100f);

        [Header("Playtest Column")]
        [Tooltip("Title, divider and list of the playtest entries; hidden when the list is empty or Playtest Only is on.")]
        [SerializeField] GameObject playtestColumn = null!;
        [Header("Playtest Grid")]
        [SerializeField] GridLayoutGroup playtestGrid = null!;
        [Header("Playtest Key Responder")]
        [SerializeField] GridKeyResponder playtestKeyResponder = null!;

        /// <summary>The mode, and the chapter mode id for Chapter entries (empty otherwise).</summary>
        public event Action<GameModeType, string>? ModeSelected;
        public event Action? ExitRequested;

        bool modeChosen;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.GameModeSelection;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;
        public override string AnalyticsScreenName => "game-mode-selection";

        public override bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind)
        {
            if (buttonKind != TopLevelControlPanel.ButtonKind.Exit) return base.OnControlButton(buttonKind);
            if (!IsActive) return false;

            ExitRequested?.Invoke();
            return true;
        }

        #endregion

        #region Initialization

        /// <param name="initialMode">Mode focused first (e.g. the last one played); ignored when not in the main grid.</param>
        /// <param name="chapterModes">Every chapter mode, built-in then player-made; each is its own entry before the other modes.</param>
        /// <param name="chapterNames">The title shown for a chapter mode.</param>
        public void Initialize(GameModeType initialMode, IReadOnlyList<Ninjago.ModeData> chapterModes, Func<Ninjago.ModeData, LocalizedString> chapterNames)
        {
            var main = new List<GameModeSelectionButton>();
            var playtest = new List<GameModeSelectionButton>();
            if (gameModeConfig.PlaytestOnly)
            {
                AddPlaytestButtons(gridLayout, chapterModes, chapterNames, main);
            }
            else
            {
                foreach (var chapterMode in chapterModes) main.Add(AddButton(gridLayout, GameModeType.Chapter, chapterNames(chapterMode), chapterMode.id));
                foreach (var mode in gameModeConfig.ModeOrders) main.Add(AddButton(gridLayout, mode, gameModeConfig.GetMode(mode).displayName, ""));
                AddPlaytestButtons(playtestGrid, chapterModes, chapterNames, playtest);
            }

            playtestColumn.SetActive(playtest.Count > 0);
            var rightMargin = playtestColumn.activeSelf ? gridRightMargin.x : gridRightMargin.y;
            gridScrollView.offsetMax = new Vector2(-rightMargin, gridScrollView.offsetMax.y);
            InitializeGrid(gridKeyResponder, gridLayout, main);
            InitializeGrid(playtestKeyResponder, playtestGrid, playtest);
            var initialIndex = IndexOf(main, initialMode);
            if (initialIndex >= 0) gridKeyResponder.SetInitialActiveIndex(initialIndex);
        }

        #endregion

        #region View Lifecycle

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            // Back from a follow-up step (player count) re-arms the selection.
            modeChosen = false;
        }

        #endregion

        #region Buttons

        GameModeSelectionButton AddButton(GridLayoutGroup grid, GameModeType mode, LocalizedString displayName, string chapterModeId)
        {
            var button = Instantiate(buttonPrefab, grid.transform);
            button.Initialize(mode, displayName, chapterModeId);
            button.Clicked += HandleModeClicked;
            return button;
        }

        // A chapter entry whose mode no longer exists (e.g. a deleted player-made mode) is skipped, so the menu still opens.
        void AddPlaytestButtons(GridLayoutGroup grid, IReadOnlyList<Ninjago.ModeData> chapterModes, Func<Ninjago.ModeData, LocalizedString> chapterNames,
            List<GameModeSelectionButton> added)
        {
            foreach (var entry in gameModeConfig.PlaytestEntries)
            {
                if (entry.mode != GameModeType.Chapter)
                {
                    added.Add(AddButton(grid, entry.mode, gameModeConfig.GetMode(entry.mode).displayName, ""));
                    continue;
                }

                var chapterMode = FindChapterMode(chapterModes, entry.chapterModeId);
                if (chapterMode == null)
                {
                    Debug.LogError($"[GameModeSelectionView] Playtest entry: no chapter mode with id '{entry.chapterModeId}'.", gameModeConfig);
                    continue;
                }

                added.Add(AddButton(grid, GameModeType.Chapter, chapterNames(chapterMode), chapterMode.id));
            }
        }

        static void InitializeGrid(GridKeyResponder keyResponder, GridLayoutGroup grid, List<GameModeSelectionButton> buttons)
        {
            keyResponder.NumColumns = grid.constraintCount;
            var responders = new List<KeyResponder>(buttons.Count);
            foreach (var button in buttons) responders.Add(button.KeyResponder);
            keyResponder.ReinitializeResponders(responders);
        }

        static Ninjago.ModeData? FindChapterMode(IReadOnlyList<Ninjago.ModeData> chapterModes, string id)
        {
            foreach (var chapterMode in chapterModes)
            {
                if (chapterMode.id == id) return chapterMode;
            }

            return null;
        }

        static int IndexOf(List<GameModeSelectionButton> buttons, GameModeType mode)
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Mode == mode && buttons[i].ChapterModeId == "") return i;
            }

            return -1;
        }

        #endregion

        #region Unity Events

        void HandleModeClicked(GameModeType mode, string chapterModeId)
        {
            // A second press during the scene transition must not load the game twice.
            if (!IsActive || modeChosen) return;
            modeChosen = true;

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            ModeSelected?.Invoke(mode, chapterModeId);
        }

        #endregion
    }
}
