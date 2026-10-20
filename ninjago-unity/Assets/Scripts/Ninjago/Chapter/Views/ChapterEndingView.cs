#nullable enable

using System;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>After the last level: the escape line, the chapter summary and a to-be-continued feel. A cliffhanger, not a loss.</summary>
    public class ChapterEndingView : SimpleCanvasView
    {
        [Header("Path")]
        [SerializeField] ProgressPath path = null!;
        [Header("Escape Line")]
        [SerializeField] NexLocalizedString escapeLine = null!;
        [Header("Escape Text")]
        [Tooltip("Shown when the last level ended with the Boss escaping.")]
        [SerializeField] LocalizedString escapeText = new();
        [Header("Finished Text")]
        [Tooltip("Shown when the mode has no Boss chase.")]
        [SerializeField] LocalizedString finishedText = new();
        [Header("Stars Label")]
        [SerializeField] NexLocalizedString starsLabel = null!;
        [Header("Stars Text")]
        [Tooltip("Smart string with {stars} {max}.")]
        [SerializeField] LocalizedString starsText = new();
        [Header("Chase Label")]
        [SerializeField] NexLocalizedString chaseLabel = null!;
        [Header("Chase Text")]
        [Tooltip("Smart string with {percent}: how close the ninjas got.")]
        [SerializeField] LocalizedString chaseText = new();
        [Header("Continued Label")]
        [SerializeField] GameObject continuedLabel = null!;
        [Header("Menu Button")]
        [SerializeField] Button menuButton = null!;

        bool done;

        public event Action? Done;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ChapterEnding;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "chapter-ending";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            HandleMenu();
        }

        #endregion

        #region Initialization

        /// <param name="chaseCloseness">0..1 from the Boss chase; negative without one.</param>
        public void Initialize(ChapterData chapter, ForgeConfig names, ModeProgress progress, float chaseCloseness)
        {
            path.Show(chapter, names, chapter.levels.Count, progress);
            var escaped = chaseCloseness >= 0f;
            escapeLine.StringReference = escaped ? escapeText : finishedText;
            continuedLabel.SetActive(escaped);
            chaseLabel.gameObject.SetActive(escaped);
            if (escaped) chaseLabel.StringReference = LocalizedStrings.WithArguments(chaseText, ("percent", Mathf.RoundToInt(chaseCloseness * 100f)));
            var total = 0;
            for (var i = 0; i < chapter.levels.Count; i++) total += progress.BestStars(i);
            starsLabel.StringReference = LocalizedStrings.WithArguments(starsText, ("stars", total), ("max", chapter.levels.Count * 3));
            menuButton.onClick.AddListener(HandleMenu);
        }

        #endregion

        #region Unity Events

        void HandleMenu()
        {
            if (!IsActive || done) return;
            done = true;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "menu");
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            Done?.Invoke();
        }

        #endregion
    }
}
