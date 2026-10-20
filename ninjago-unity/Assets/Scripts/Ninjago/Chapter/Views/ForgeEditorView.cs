#nullable enable

using System;
using Nex.Localization;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    // Keys of the editor's control and label dictionaries: explicit values, append only.
    public enum ForgeEditorAction
    {
        PreviousLevel = 0,
        NextLevel = 1,
        AddLevel = 2,
        RemoveLevel = 3,
        Theme = 4,
        Slot = 5,
        Type = 6,
        Ride = 7,
        Preset = 8,
        Hints = 9,
        Boss = 10,
        Mix = 11,
        Window = 12,
        Speed = 13,
        Count = 14,
        Zone = 15,
        Length = 16,
        MoveLeft = 17,
        MoveRight = 18,
        Remove = 19,
        Link = 20,
        Test = 21,
        Save = 22,
        Name = 23,
    }

    /// <summary>
    /// Making a mode, keyboard only: the level row (pick, add, remove, theme), the level's strip of up to six blocks
    /// (an empty slot adds one), the selected block's type, ride, preset, hints, Boss and mix, its fine values, moving
    /// and removing it, the link after it, and Test, Save as mode and the generated name. Problems show live.
    /// </summary>
    public class ForgeEditorView : SimpleCanvasView
    {
        [Header("Title")]
        [SerializeField] NexLocalizedString title = null!;
        [Header("Level Label")]
        [SerializeField] NexLocalizedString levelLabel = null!;
        [Header("Status Label")]
        [SerializeField] NexLocalizedString status = null!;
        [Header("Controls")]
        [SerializeField] EnumDictionary<ForgeEditorAction, ForgeButton?> controls = new();
        [Header("Strip Slots")]
        [SerializeField] ForgeButton[] slots = null!;
        [Header("Problem Lines")]
        [SerializeField] NexLocalizedString[] problemLines = null!;
        [Header("Problem Line Texts")]
        [Tooltip("The text components of the problem lines, same order.")]
        [SerializeField] TMPro.TMP_Text[] problemTexts = null!;
        [Header("Control Names")]
        [SerializeField] EnumDictionary<ForgeEditorAction, LocalizedString> names = new();
        [Header("Field Format")]
        [Tooltip("Smart string with {name} {value}.")]
        [SerializeField] LocalizedString fieldFormat = new();
        [Header("Level Format")]
        [Tooltip("Smart string with {level} {count} {theme}.")]
        [SerializeField] LocalizedString levelFormat = new();
        [Header("On Text")]
        [SerializeField] LocalizedString onText = new();
        [Header("Off Text")]
        [SerializeField] LocalizedString offText = new();
        [Header("Empty Slot Text")]
        [SerializeField] LocalizedString emptySlotText = new();
        [Header("Last Block Text")]
        [SerializeField] LocalizedString lastBlockText = new();
        [Header("Ready Text")]
        [SerializeField] LocalizedString readyText = new();
        [Header("More Problems Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString moreText = new();
        [Header("Seed Text")]
        [Tooltip("Smart string with {seed}.")]
        [SerializeField] LocalizedString seedText = new();
        [Header("Keep Text")]
        [SerializeField] LocalizedString keepText = new();
        [Header("Dash Text")]
        [SerializeField] LocalizedString dashText = new();
        [Header("Problem Color")]
        [SerializeField] Color problemColor = new(1f, 0.55f, 0.5f);
        [Header("Warning Color")]
        [SerializeField] Color warningColor = new(1f, 0.85f, 0.4f);

        public event Action<ForgeEditorAction, int>? ActionRequested;
        public event Action? BackRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.ForgeEditor;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "forge-editor";

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
            foreach (var pair in controls)
            {
                if (pair.Value == null) continue;
                var action = pair.Key;
                pair.Value.Clicked += () => Request(action, 0);
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = i;
                slots[i].Clicked += () => Request(ForgeEditorAction.Slot, slot);
            }
        }

        #endregion

        #region Public API

        /// <param name="statusLine">A short line under the title (saved, seed, why an action did nothing), or null.</param>
        public void Show(ForgeDocument document, ForgeCatalog catalog, ForgeConfig config, LocalizedString modeName, LocalizedString? statusLine)
        {
            title.StringReference = modeName;
            var level = document.CurrentLevel;
            levelLabel.StringReference = LocalizedStrings.WithArguments(levelFormat, ("level", document.Level + 1), ("count", document.Mode.chapter.levels.Count),
                ("theme", catalog.Theme(level.theme).displayName.GetLocalizedString()));
            status.gameObject.SetActive(statusLine != null || document.Seed >= 0);
            if (statusLine != null) status.StringReference = statusLine;
            else if (document.Seed >= 0) status.StringReference = LocalizedStrings.WithArguments(seedText, ("seed", document.Seed));
            for (var i = 0; i < slots.Length; i++)
            {
                var filled = i < level.blocks.Count;
                slots[i].Set(filled ? catalog.Block(level.blocks[i].type).displayName : emptySlotText, filled, filled && i == document.Block);
                if (filled && i != document.Block) slots[i].Tint(Color.Lerp(Color.white, catalog.Block(level.blocks[i].type).color, 0.45f));
            }

            ShowLevelControls(level, catalog);
            ShowBlockControls(document, catalog, config);
            Label(ForgeEditorAction.Test, null);
            Label(ForgeEditorAction.Save, null, document.IsValid);
            if (document.Seed >= 0 && string.IsNullOrEmpty(document.EditingId)) controls[ForgeEditorAction.Save]!.Set(keepText, document.IsValid);
            Field(ForgeEditorAction.Name, config.NameOf(document.Mode.name).GetLocalizedString());
            ShowProblems(document, config);
        }

        #endregion

        #region Helpers

        void ShowLevelControls(LevelData level, ForgeCatalog catalog)
        {
            Label(ForgeEditorAction.PreviousLevel, null);
            Label(ForgeEditorAction.NextLevel, null);
            Label(ForgeEditorAction.AddLevel, null);
            Label(ForgeEditorAction.RemoveLevel, null);
            Field(ForgeEditorAction.Theme, catalog.Theme(level.theme).displayName.GetLocalizedString());
        }

        void ShowBlockControls(ForgeDocument document, ForgeCatalog catalog, ForgeConfig config)
        {
            var block = document.CurrentBlock;
            var has = block != null;
            var spec = has ? catalog.Block(block!.type) : null;
            Field(ForgeEditorAction.Type, has ? spec!.displayName.GetLocalizedString() : Dash, has);
            Field(ForgeEditorAction.Ride, has && spec!.usesVehicle ? config.RideName(block!.ride).GetLocalizedString() : Dash, has && spec!.usesVehicle);
            Field(ForgeEditorAction.Preset, has ? config.PresetName(block!.preset).GetLocalizedString() : Dash, has);
            Field(ForgeEditorAction.Hints, has ? config.HintName(block!.hints).GetLocalizedString() : Dash, has);
            Field(ForgeEditorAction.Boss, has ? OnOff(block!.boss) : Dash, has);
            Field(ForgeEditorAction.Mix, has && spec!.isFight ? OnOff(block!.mix.Count > 0) : Dash, has && spec!.isFight);
            Field(ForgeEditorAction.Window, has ? $"x{block!.difficulty.windowScale:0.0}" : Dash, has);
            Field(ForgeEditorAction.Speed, has ? $"x{block!.difficulty.speedScale:0.0}" : Dash, has);
            Field(ForgeEditorAction.Count, has ? block!.difficulty.count.ToString() : Dash, has);
            Field(ForgeEditorAction.Zone, has && block!.type == BlockType.Stealth ? $"{block.difficulty.zoneLength:0}" : Dash, has && block!.type == BlockType.Stealth);
            Field(ForgeEditorAction.Length, has ? $"{block!.targetSeconds:0}" : Dash, has);
            Label(ForgeEditorAction.MoveLeft, null, has && document.Block > 0);
            Label(ForgeEditorAction.MoveRight, null, has && document.Block < document.CurrentLevel.blocks.Count - 1);
            Label(ForgeEditorAction.Remove, null, has);
            var link = document.CurrentLink;
            Field(ForgeEditorAction.Link, link != null ? config.MomentName(link.moment).GetLocalizedString() : lastBlockText.GetLocalizedString(), link != null);
        }

        void ShowProblems(ForgeDocument document, ForgeConfig config)
        {
            var problems = document.Problems;
            for (var i = 0; i < problemLines.Length; i++)
            {
                var line = problemLines[i];
                var label = problemTexts[i];
                if (problems.Count == 0 && i == 0)
                {
                    line.gameObject.SetActive(true);
                    line.StringReference = readyText;
                    label.color = Color.white;
                    continue;
                }

                var last = i == problemLines.Length - 1 && problems.Count > problemLines.Length;
                line.gameObject.SetActive(i < problems.Count);
                if (i >= problems.Count) continue;
                line.StringReference = last ? LocalizedStrings.WithArguments(moreText, ("count", problems.Count - i)) : ChapterValidator.Describe(problems[i], config);
                label.color = problems[i].isWarning ? warningColor : problemColor;
            }
        }

        string Dash => dashText.GetLocalizedString();

        string OnOff(bool on) => (on ? onText : offText).GetLocalizedString();

        void Field(ForgeEditorAction action, string value, bool available = true)
        {
            controls[action]!.Set(LocalizedStrings.WithArguments(fieldFormat, ("name", names[action].GetLocalizedString()), ("value", value)), available);
        }

        void Label(ForgeEditorAction action, LocalizedString? text, bool available = true)
        {
            controls[action]!.Set(text ?? names[action], available);
        }

        void Request(ForgeEditorAction action, int index)
        {
            if (!IsActive) return;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiMove);
            NinjagoAnalytics.UiAction(AnalyticsScreenName, action.ToString(), index);
            ActionRequested?.Invoke(action, index);
        }

        #endregion
    }
}
