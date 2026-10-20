#nullable enable

using System;
using Nex.KeyboardNavigation;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>The mode's title card: Continue (back to the rest view before the next unplayed level) or Start from Level 1.</summary>
    public class ChapterStartView : SimpleCanvasView
    {
        [Header("Title")]
        [SerializeField] NexLocalizedString title = null!;
        [Header("Progress Label")]
        [SerializeField] NexLocalizedString progressLabel = null!;
        [Header("Continue Button")]
        [SerializeField] Button continueButton = null!;
        [Header("Start Button")]
        [SerializeField] Button startButton = null!;
        [Header("Buttons")]
        [SerializeField] GroupKeyResponder buttons = null!;

        bool chosen;

        /// <summary>True to continue, false to start from the first level.</summary>
        public event Action<bool>? Chosen;
        public event Action? QuitRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ChapterStart;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "chapter-start";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            QuitRequested?.Invoke();
        }

        #endregion

        #region Initialization

        public void Initialize(LocalizedString modeTitle, LocalizedString progressLine, bool canContinue)
        {
            title.StringReference = modeTitle;
            progressLabel.StringReference = progressLine;
            continueButton.gameObject.SetActive(canContinue);
            continueButton.onClick.AddListener(HandleContinue);
            startButton.onClick.AddListener(HandleStart);
            buttons.SetInitialActiveIndex(canContinue ? 0 : 1);
        }

        #endregion

        #region Unity Events

        void HandleContinue() => Choose(true);

        void HandleStart() => Choose(false);

        void Choose(bool continuing)
        {
            if (!IsActive || chosen) return;
            chosen = true;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, continuing ? "continue" : "start");
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            Chosen?.Invoke(continuing);
        }

        #endregion
    }
}
