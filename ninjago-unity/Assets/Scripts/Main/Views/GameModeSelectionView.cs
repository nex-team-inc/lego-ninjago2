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

        /// <param name="initialMode">Mode focused first (e.g. the last one played); ignored when not in the menu.</param>
        /// <param name="chapterModes">Every chapter mode, built-in then player-made; each is its own entry before the other modes.</param>
        /// <param name="chapterNames">The title shown for a chapter mode.</param>
        public void Initialize(GameModeType initialMode, IReadOnlyList<Ninjago.ModeData> chapterModes, Func<Ninjago.ModeData, LocalizedString> chapterNames)
        {
            gridKeyResponder.NumColumns = gridLayout.constraintCount;

            var responders = new List<KeyResponder>(chapterModes.Count + gameModeConfig.ModeOrders.Count);
            foreach (var chapterMode in chapterModes)
            {
                var button = Instantiate(buttonPrefab, gridLayout.transform);
                button.Initialize(GameModeType.Chapter, chapterNames(chapterMode), chapterMode.id);
                button.Clicked += HandleModeClicked;
                responders.Add(button.KeyResponder);
            }

            foreach (var mode in gameModeConfig.ModeOrders)
            {
                var button = Instantiate(buttonPrefab, gridLayout.transform);
                button.Initialize(mode, gameModeConfig.GetMode(mode).displayName);
                button.Clicked += HandleModeClicked;
                responders.Add(button.KeyResponder);
            }

            gridKeyResponder.ReinitializeResponders(responders);
            var initialIndex = IndexOf(gameModeConfig.ModeOrders, initialMode);
            if (initialIndex >= 0) gridKeyResponder.SetInitialActiveIndex(chapterModes.Count + initialIndex);
        }

        #endregion

        #region View Lifecycle

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            // Back from a follow-up step (player count) re-arms the selection.
            modeChosen = false;
        }

        static int IndexOf(IReadOnlyList<GameModeType> modes, GameModeType mode)
        {
            for (var i = 0; i < modes.Count; i++)
            {
                if (modes[i] == mode) return i;
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
