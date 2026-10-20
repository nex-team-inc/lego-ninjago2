#nullable enable

using System;
using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    public enum ForgeHomeAction
    {
        Template = 0,
        Empty = 1,
        Surprise = 2,
        PreviousMode = 3,
        NextMode = 4,
        Edit = 5,
        Rename = 6,
        Copy = 7,
        Delete = 8,
    }

    /// <summary>
    /// The Forge's first screen: start from a built-in template, start empty, or Surprise me; and look through every
    /// mode to edit, rename, copy or delete it. Built-in modes can be copied but not renamed or deleted.
    /// </summary>
    public class ForgeHomeView : SimpleCanvasView
    {
        [Header("Template Buttons")]
        [SerializeField] ForgeButton[] templates = null!;
        [Header("Empty Button")]
        [SerializeField] ForgeButton empty = null!;
        [Header("Surprise Button")]
        [SerializeField] ForgeButton surprise = null!;
        [Header("Previous Mode Button")]
        [SerializeField] ForgeButton previousMode = null!;
        [Header("Mode Name Button")]
        [SerializeField] ForgeButton modeName = null!;
        [Header("Next Mode Button")]
        [SerializeField] ForgeButton nextMode = null!;
        [Header("Edit Button")]
        [SerializeField] ForgeButton edit = null!;
        [Header("Rename Button")]
        [SerializeField] ForgeButton rename = null!;
        [Header("Copy Button")]
        [SerializeField] ForgeButton copy = null!;
        [Header("Delete Button")]
        [SerializeField] ForgeButton delete = null!;
        [Header("Status Label")]
        [SerializeField] NexLocalizedString status = null!;
        [Header("Empty Text")]
        [SerializeField] LocalizedString emptyText = new();
        [Header("Surprise Text")]
        [SerializeField] LocalizedString surpriseText = new();
        [Header("Edit Text")]
        [SerializeField] LocalizedString editText = new();
        [Header("Rename Text")]
        [SerializeField] LocalizedString renameText = new();
        [Header("Copy Text")]
        [SerializeField] LocalizedString copyText = new();
        [Header("Delete Text")]
        [SerializeField] LocalizedString deleteText = new();
        [Header("Previous Text")]
        [SerializeField] LocalizedString previousText = new();
        [Header("Next Text")]
        [SerializeField] LocalizedString nextText = new();
        [Header("Dash Text")]
        [SerializeField] LocalizedString dashText = new();

        public event Action<ForgeHomeAction, int>? ActionRequested;
        public event Action? BackRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ForgeHome;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "forge-home";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            BackRequested?.Invoke();
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            for (var i = 0; i < templates.Length; i++)
            {
                var index = i;
                templates[i].Clicked += () => Request(ForgeHomeAction.Template, index);
            }

            empty.Clicked += () => Request(ForgeHomeAction.Empty, 0);
            surprise.Clicked += () => Request(ForgeHomeAction.Surprise, 0);
            previousMode.Clicked += () => Request(ForgeHomeAction.PreviousMode, 0);
            modeName.Clicked += () => Request(ForgeHomeAction.Edit, 0);
            nextMode.Clicked += () => Request(ForgeHomeAction.NextMode, 0);
            edit.Clicked += () => Request(ForgeHomeAction.Edit, 0);
            rename.Clicked += () => Request(ForgeHomeAction.Rename, 0);
            copy.Clicked += () => Request(ForgeHomeAction.Copy, 0);
            delete.Clicked += () => Request(ForgeHomeAction.Delete, 0);
            empty.Set(emptyText);
            surprise.Set(surpriseText);
            previousMode.Set(previousText);
            nextMode.Set(nextText);
        }

        #endregion

        #region Public API

        /// <param name="selected">The mode under the selector (built-in or player-made).</param>
        public void Show(IReadOnlyList<ModeData> builtIns, ModeData? selected, Func<ModeData, LocalizedString> nameOf, LocalizedString? statusLine)
        {
            for (var i = 0; i < templates.Length; i++)
            {
                var exists = i < builtIns.Count;
                templates[i].Set(exists ? nameOf(builtIns[i]) : dashText, exists);
            }

            var playerMade = selected is { builtIn: false };
            modeName.Set(selected != null ? nameOf(selected) : dashText, selected != null, true);
            edit.Set(editText, selected != null);
            copy.Set(copyText, selected != null);
            rename.Set(renameText, playerMade);
            delete.Set(deleteText, playerMade);
            status.gameObject.SetActive(statusLine != null);
            if (statusLine != null) status.StringReference = statusLine;
        }

        #endregion

        #region Helpers

        void Request(ForgeHomeAction action, int index)
        {
            if (!IsActive) return;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            NinjagoAnalytics.UiAction(AnalyticsScreenName, action.ToString(), index);
            ActionRequested?.Invoke(action, index);
        }

        #endregion
    }
}
