#nullable enable

using System.Collections.Generic;
using Nex.KeyboardNavigation;
using Nex.Localization;
using Nex.Util;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The Forge's views and game prefab. Every control is authored here in fixed rows (a vertical key group of
    /// horizontal key groups), so the keyboard navigator moves through them with the arrow keys and Enter changes them.
    /// </summary>
    public static class ForgeViewsBuilder
    {
        public const string HomeViewPath = NinjagoViewsBuilder.ViewsRoot + "/ForgeHomeView.prefab";
        public const string EditorViewPath = NinjagoViewsBuilder.ViewsRoot + "/ForgeEditorView.prefab";
        static readonly Color gold = new(1f, 0.85f, 0.3f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                NinjagoViewsBuilder.BuildView<ForgeHomeView>(HomeViewPath, BuildHome);
                NinjagoViewsBuilder.BuildView<ForgeEditorView>(EditorViewPath, BuildEditor);
            });
            var root = new GameObject("ForgeGame");
            var game = root.AddComponent<ForgeGame>();
            ChapterGamesBuilder.WireGame(game, root.transform);
            Set(game, "homeViewPrefab", LoadComponent<ForgeHomeView>(HomeViewPath));
            Set(game, "editorViewPrefab", LoadComponent<ForgeEditorView>(EditorViewPath));
            SetLocalized(game, "savedText", "forge.saved");
            SetLocalized(game, "fixProblemsText", "forge.fix");
            SetLocalized(game, "builtInLockedText", "forge.home.locked");
            SetLocalized(game, "deletedText", "forge.home.deleted");
            SavePrefab(root, ChapterGamesBuilder.ForgeGamePath);
            ChapterGamesBuilder.Register(GameModeType.Forge, ChapterGamesBuilder.ForgeGamePath, "ninjago.mode.forge");
        }

        #endregion

        #region Home

        static void BuildHome(ForgeHomeView view, RectTransform ui)
        {
            NinjagoViewsBuilder.Title(ui, "forge.home.title", "The Forge", new Vector2(0f, 400f));
            var column = Column(ui, new Vector2(0f, 230f), 34f);
            var templates = Row(column, "Templates", "forge.home.start_from", "Start from", out var templatesRow);
            var templateButtons = new List<Object>();
            for (var i = 0; i < 3; i++) templateButtons.Add(Button(templates, $"Template{i}", "chapter.mode.proof", "Ninja Chapter Proof", 320f));
            var empty = Button(templates, "Empty", "forge.home.empty", "Start empty", 250f);
            var surprise = Button(templates, "Surprise", "forge.home.surprise", "Surprise me", 250f);
            var selector = Row(column, "Selector", "forge.home.your_modes", "Every mode", out var selectorRow);
            var previous = Button(selector, "Previous", "forge.home.previous", "<", 120f);
            var name = Button(selector, "ModeName", "chapter.mode.proof", "Ninja Chapter Proof", 620f);
            var next = Button(selector, "Next", "forge.home.next", ">", 120f);
            var actions = Row(column, "Actions", null, "", out var actionsRow);
            var edit = Button(actions, "Edit", "forge.home.edit", "Edit");
            var rename = Button(actions, "Rename", "forge.home.rename", "Rename");
            var copy = Button(actions, "Copy", "forge.home.copy", "Copy");
            var delete = Button(actions, "Delete", "forge.home.delete", "Delete");
            var status = Label(ui, "Status", null, "Saved!", 46f, gold, OutlineTextMaterial());
            Place((RectTransform)status.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1600f, 70f));

            var groups = new[]
            {
                Group(templatesRow, templateButtons, empty, surprise),
                Group(selectorRow, new List<Object>(), previous, name, next),
                Group(actionsRow, new List<Object>(), edit, rename, copy, delete),
            };
            Set(view, "keyResponder", Vertical(column, groups));
            SetArray(view, "templates", templateButtons);
            Set(view, "empty", empty);
            Set(view, "surprise", surprise);
            Set(view, "previousMode", previous);
            Set(view, "modeName", name);
            Set(view, "nextMode", next);
            Set(view, "edit", edit);
            Set(view, "rename", rename);
            Set(view, "copy", copy);
            Set(view, "delete", delete);
            Set(view, "status", status);
            SetLocalized(view, "emptyText", "forge.home.empty");
            SetLocalized(view, "surpriseText", "forge.home.surprise");
            SetLocalized(view, "editText", "forge.home.edit");
            SetLocalized(view, "renameText", "forge.home.rename");
            SetLocalized(view, "copyText", "forge.home.copy");
            SetLocalized(view, "deleteText", "forge.home.delete");
            SetLocalized(view, "previousText", "forge.home.previous");
            SetLocalized(view, "nextText", "forge.home.next");
            SetLocalized(view, "dashText", "forge.dash");
        }

        #endregion

        #region Editor

        static void BuildEditor(ForgeEditorView view, RectTransform ui)
        {
            var outline = OutlineTextMaterial();
            var title = Label(ui, "Title", null, "Harbor Night Run", 70f, Color.white, outline);
            Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1600f, 90f));
            var status = Label(ui, "Status", null, "Seed 4821", 44f, gold, outline);
            Place((RectTransform)status.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(1600f, 60f));
            var column = Column(ui, new Vector2(0f, 340f), 14f);
            var controls = new Dictionary<ForgeEditorAction, ForgeButton>();
            var levelRow = Row(column, "Level", null, "", out var levelRect);
            controls[ForgeEditorAction.PreviousLevel] = Button(levelRow, "PreviousLevel", "forge.control.previous_level", "< Level", 190f);
            var levelLabel = Label(levelRect, "LevelLabel", null, "Level 1 of 4 - Town at Dusk", 42f, gold, outline);
            ((RectTransform)levelLabel.transform).sizeDelta = new Vector2(520f, 80f);
            var levelText = levelLabel.GetComponent<TextMeshProUGUI>();
            levelText.enableAutoSizing = true;
            levelText.fontSizeMax = 42f;
            controls[ForgeEditorAction.NextLevel] = Button(levelRow, "NextLevel", "forge.control.next_level", "Level >", 190f);
            controls[ForgeEditorAction.AddLevel] = Button(levelRow, "AddLevel", "forge.control.add_level", "+ Level", 190f);
            controls[ForgeEditorAction.RemoveLevel] = Button(levelRow, "RemoveLevel", "forge.control.remove_level", "- Level", 190f);
            controls[ForgeEditorAction.Theme] = Button(levelRow, "Theme", "forge.control.theme", "Theme", 420f);
            var stripRow = Row(column, "Strip", null, "", out var stripRect);
            var slots = new List<Object>();
            for (var i = 0; i < ChapterValidator.MaxBlocks; i++) slots.Add(Button(stripRow, $"Slot{i}", "forge.empty_slot", "+ Block"));
            var blockRow = Row(column, "Block", null, "", out var blockRect);
            foreach (var action in new[] { ForgeEditorAction.Type, ForgeEditorAction.Ride, ForgeEditorAction.Preset, ForgeEditorAction.Hints, ForgeEditorAction.Boss, ForgeEditorAction.Mix })
            {
                controls[action] = Button(blockRow, action.ToString(), ControlKey(action), action.ToString(), 290f);
            }

            var fineRow = Row(column, "Fine", null, "", out var fineRect);
            foreach (var action in new[] { ForgeEditorAction.Window, ForgeEditorAction.Speed, ForgeEditorAction.Count, ForgeEditorAction.Zone, ForgeEditorAction.Length })
            {
                controls[action] = Button(fineRow, action.ToString(), ControlKey(action), action.ToString(), 320f);
            }

            var editRow = Row(column, "Edit", null, "", out var editRect);
            foreach (var action in new[] { ForgeEditorAction.MoveLeft, ForgeEditorAction.MoveRight, ForgeEditorAction.Remove, ForgeEditorAction.Link })
            {
                controls[action] = Button(editRow, action.ToString(), ControlKey(action), action.ToString(), action == ForgeEditorAction.Link ? 480f : 260f);
            }

            var modeRow = Row(column, "Mode", null, "", out var modeRect);
            foreach (var action in new[] { ForgeEditorAction.Test, ForgeEditorAction.Save, ForgeEditorAction.Name })
            {
                controls[action] = Button(modeRow, action.ToString(), ControlKey(action), action.ToString(), action == ForgeEditorAction.Name ? 640f : 340f);
            }

            var problems = NewRect("Problems", ui);
            Place(problems, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1700f, 220f));
            var problemLayout = problems.gameObject.AddComponent<VerticalLayoutGroup>();
            problemLayout.childAlignment = TextAnchor.LowerCenter;
            problemLayout.childControlHeight = false;
            problemLayout.childForceExpandHeight = false;
            var lines = new List<Object>();
            var texts = new List<Object>();
            for (var i = 0; i < 4; i++)
            {
                var line = Label(problems, $"Problem{i}", null, "Level 1 needs 2 to 6 blocks.", 38f, Color.white, outline);
                ((RectTransform)line.transform).sizeDelta = new Vector2(1700f, 50f);
                lines.Add(line);
                texts.Add(line.GetComponent<TextMeshProUGUI>());
            }

            Order(controls, levelRect, stripRect, blockRect, fineRect, editRect, modeRect, slots, out var groups);
            Set(view, "keyResponder", Vertical(column, groups));
            Set(view, "title", title);
            Set(view, "levelLabel", levelLabel);
            Set(view, "status", status);
            var controlMap = ChapterAssetsBuilder.Field<EnumDictionary<ForgeEditorAction, ForgeButton?>>(view, "controls");
            var names = ChapterAssetsBuilder.Field<EnumDictionary<ForgeEditorAction, LocalizedString>>(view, "names");
            foreach (ForgeEditorAction action in System.Enum.GetValues(typeof(ForgeEditorAction)))
            {
                controlMap[action] = controls.TryGetValue(action, out var button) ? button : null;
                names[action] = ChapterAssetsBuilder.Loc(ControlKey(action));
            }

            SetArray(view, "slots", slots);
            SetArray(view, "problemLines", lines);
            SetArray(view, "problemTexts", texts);
            SetLocalized(view, "fieldFormat", "forge.field");
            SetLocalized(view, "levelFormat", "forge.level");
            SetLocalized(view, "onText", "forge.on");
            SetLocalized(view, "offText", "forge.off");
            SetLocalized(view, "emptySlotText", "forge.empty_slot");
            SetLocalized(view, "lastBlockText", "forge.last_block");
            SetLocalized(view, "readyText", "forge.ready");
            SetLocalized(view, "moreText", "forge.more");
            SetLocalized(view, "seedText", "forge.seed");
            SetLocalized(view, "keepText", "forge.keep");
            SetLocalized(view, "dashText", "forge.dash");
        }

        // Row key groups in on-screen order; the strip row holds the slots.
        static void Order(Dictionary<ForgeEditorAction, ForgeButton> controls, RectTransform level, RectTransform strip, RectTransform block, RectTransform fine,
            RectTransform edit, RectTransform mode, List<Object> slots, out GroupKeyResponder[] groups)
        {
            ForgeButton[] Of(params ForgeEditorAction[] actions) => System.Array.ConvertAll(actions, action => controls[action]);
            groups = new[]
            {
                Group(level, new List<Object>(), Of(ForgeEditorAction.PreviousLevel, ForgeEditorAction.NextLevel, ForgeEditorAction.AddLevel, ForgeEditorAction.RemoveLevel,
                    ForgeEditorAction.Theme)),
                Group(strip, slots),
                Group(block, new List<Object>(), Of(ForgeEditorAction.Type, ForgeEditorAction.Ride, ForgeEditorAction.Preset, ForgeEditorAction.Hints, ForgeEditorAction.Boss,
                    ForgeEditorAction.Mix)),
                Group(fine, new List<Object>(), Of(ForgeEditorAction.Window, ForgeEditorAction.Speed, ForgeEditorAction.Count, ForgeEditorAction.Zone, ForgeEditorAction.Length)),
                Group(edit, new List<Object>(), Of(ForgeEditorAction.MoveLeft, ForgeEditorAction.MoveRight, ForgeEditorAction.Remove, ForgeEditorAction.Link)),
                Group(mode, new List<Object>(), Of(ForgeEditorAction.Test, ForgeEditorAction.Save, ForgeEditorAction.Name)),
            };
        }

        static string ControlKey(ForgeEditorAction action) => action switch
        {
            ForgeEditorAction.PreviousLevel => "forge.control.previous_level",
            ForgeEditorAction.NextLevel => "forge.control.next_level",
            ForgeEditorAction.AddLevel => "forge.control.add_level",
            ForgeEditorAction.RemoveLevel => "forge.control.remove_level",
            ForgeEditorAction.MoveLeft => "forge.control.move_left",
            ForgeEditorAction.MoveRight => "forge.control.move_right",
            _ => "forge.control." + action.ToString().ToLowerInvariant(),
        };

        #endregion

        #region Helpers

        static RectTransform Column(RectTransform ui, Vector2 top, float spacing)
        {
            var column = NewRect("Rows", ui);
            Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), top, new Vector2(1840f, 640f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            return column;
        }

        /// <summary>A row of buttons, with an optional caption on its left.</summary>
        static RectTransform Row(RectTransform column, string name, string? captionKey, string caption, out RectTransform row)
        {
            row = NewRect(name, column);
            row.sizeDelta = new Vector2(1840f, 96f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            if (captionKey != null)
            {
                var label = Label(row, "Caption", captionKey, caption, 40f, gold, OutlineTextMaterial(), TextAlignmentOptions.Right);
                ((RectTransform)label.transform).sizeDelta = new Vector2(260f, 80f);
            }

            return row;
        }

        // Fixed widths: the label changes with every edit, and a row must not move under the keyboard focus.
        static ForgeButton Button(RectTransform row, string name, string key, string preview, float width = 280f)
        {
            var go = NinjagoViewsBuilder.CloneButton(row, name, key, preview, Vector2.zero);
            Object.DestroyImmediate(go.GetComponent<ContentSizeFitter>());
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 84f);
            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = 84f;
            var text = go.transform.Find("Text");
            var tmp = text.GetComponent<TextMeshProUGUI>();
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 20f;
            tmp.fontSizeMax = 38f;
            var button = go.AddComponent<ForgeButton>();
            Set(button, "button", go.GetComponent<Button>());
            Set(button, "label", text.GetComponent<NexLocalizedString>());
            Set(button, "background", go.GetComponent<Image>());
            return button;
        }

        static GroupKeyResponder Group(RectTransform row, List<Object> leading, params ForgeButton[] buttons)
        {
            var all = new List<GameObject>();
            foreach (var item in leading) all.Add(((Component)item).gameObject);
            foreach (var button in buttons) all.Add(button.gameObject);
            return NinjagoViewsBuilder.Group(row, all.ToArray());
        }

        static GroupKeyResponder Vertical(RectTransform column, GroupKeyResponder[] rows)
        {
            var group = column.gameObject.AddComponent<GroupKeyResponder>();
            SetArray(group, "responders", rows);
            Set(group, "axis", property => property.enumValueIndex = 1);
            return group;
        }

        #endregion
    }
}
