#nullable enable

using System;
using Nex.KeyboardNavigation;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The breath between levels. Nothing is asked of the body: the progress path shows how close the Boss is, the
    /// level just played shows its hearts left and stars, and the player confirms to go on. There is no timer.
    /// </summary>
    public class ChapterRestView : SimpleCanvasView
    {
        [Header("Path")]
        [SerializeField] ProgressPath path = null!;
        [Header("Title")]
        [SerializeField] NexLocalizedString title = null!;
        [Header("Title Text")]
        [Tooltip("Smart string with {level}.")]
        [SerializeField] LocalizedString titleText = new();
        [Header("Hearts Label")]
        [SerializeField] NexLocalizedString heartsLabel = null!;
        [Header("Hearts Text")]
        [Tooltip("Smart string with {hearts} {max}.")]
        [SerializeField] LocalizedString heartsText = new();
        [Header("Stars")]
        [SerializeField] Image[] stars = null!;
        [Header("Star Off Color")]
        [SerializeField] Color starOffColor = new(1f, 1f, 1f, 0.15f);
        [Header("Ahead Label")]
        [SerializeField] NexLocalizedString aheadLabel = null!;
        [Header("Levels Ahead Text")]
        [Tooltip("Smart string with {count}: levels left before the Boss.")]
        [SerializeField] LocalizedString levelsAheadText = new();
        [Header("Boss Next Text")]
        [SerializeField] LocalizedString bossNextText = new();
        [Header("Confirm Button")]
        [SerializeField] Button confirmButton = null!;

        float shownAt;
        bool confirmed;

        public event Action? Confirmed;
        public event Action? QuitRequested;
        /// <summary>Seconds from becoming the top view to confirm.</summary>
        public float SecondsShown => Time.unscaledTime - shownAt;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ChapterRest;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "chapter-rest";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            QuitRequested?.Invoke();
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            shownAt = Time.unscaledTime;
        }

        #endregion

        #region Initialization

        /// <param name="playedLevel">The level just finished.</param>
        public void Initialize(ChapterData chapter, ForgeConfig names, int playedLevel, LocalizedString playedName, int heartsLeft, int levelStars,
            ModeProgress progress)
        {
            var next = playedLevel + 1;
            path.Show(chapter, names, next, progress);
            title.StringReference = LocalizedStrings.WithArguments(titleText, ("level", playedName.GetLocalizedString()));
            heartsLabel.StringReference = LocalizedStrings.WithArguments(heartsText, ("hearts", heartsLeft), ("max", chapter.levels[playedLevel].hearts));
            for (var i = 0; i < stars.Length; i++) stars[i].color = i < levelStars ? new Color(1f, 0.85f, 0.25f) : starOffColor;
            var normalsAhead = 0;
            for (var i = next; i < chapter.levels.Count; i++)
            {
                if (!chapter.levels[i].IsBoss) normalsAhead++;
            }

            aheadLabel.StringReference = normalsAhead == 0 ? bossNextText : LocalizedStrings.WithArguments(levelsAheadText, ("count", normalsAhead));
            confirmButton.onClick.AddListener(HandleConfirm);
        }

        #endregion

        #region Unity Events

        void HandleConfirm()
        {
            if (!IsActive || confirmed) return;
            confirmed = true;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "confirm");
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            Confirmed?.Invoke();
        }

        #endregion
    }
}
