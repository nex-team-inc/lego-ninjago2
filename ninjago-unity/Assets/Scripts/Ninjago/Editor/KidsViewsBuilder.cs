#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Views shared by the children's games: each game's play view (the scene feed, the shared HUD with a corner panel
    /// per player, and one corner camera preview) and one compact result view. Setup reuses the plain Ninjago setup view.
    /// </summary>
    public static class KidsViewsBuilder
    {
        public const string ResultViewPath = NinjagoViewsBuilder.ViewsRoot + "/KidsResultView.prefab";
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f, 0.85f);
        static readonly Color hintYellow = new(1f, 0.9f, 0.3f);

        #region Entry Point

        /// <summary>Builds a play view; wireScene sets the game's own fields (its scene prefab).</summary>
        public static void BuildSceneView<T>(string path, Action<T> wireScene) where T : KidsSceneView
        {
            NinjagoViewsBuilder.WithTemplate(() => NinjagoViewsBuilder.BuildView<T>(path, (view, ui) =>
            {
                var feed = Feed(ui, "SceneFeed", Vector2.zero, Vector2.one);
                var hud = BuildHud(ui);
                // Bottom left: the ninjas stand bottom center and the panels sit up top.
                var pip = NinjagoViewsBuilder.CameraPreview(ui, Vector2.zero, new Vector2(24f, 24f), new Vector2(384f, 216f), out var pipFrame);
                Set(view, "feed", feed);
                Set(view, "hud", hud);
                Set(view, "pipFrame", pipFrame);
                Set(view, "pipIndicators", pip);
                Set(view, "keyResponder", BackProxy(ui));
                wireScene(view);
            }));
        }

        public static void BuildResultView()
        {
            var line = LoadComponent<ResultLine>(OnePunchViewsBuilder.ResultLinePath);
            NinjagoViewsBuilder.WithTemplate(() =>
                NinjagoViewsBuilder.BuildView<NinjagoResultView>(ResultViewPath, (view, ui) => NinjagoViewsBuilder.BuildResult(view, ui, line)));
        }

        #endregion

        #region HUD

        static KidsHud BuildHud(RectTransform ui)
        {
            var outline = OutlineTextMaterial();
            var hudRect = Stretch(NewRect("HUD", ui));
            var hud = hudRect.gameObject.AddComponent<KidsHud>();
            var topCenter = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            var bottomCenter = new Vector2(0.5f, 0f);

            var left = BuildPanel(hudRect, "LeftPanel", new Vector2(0f, 1f), new Vector2(24f, -20f), outline);
            var right = BuildPanel(hudRect, "RightPanel", new Vector2(1f, 1f), new Vector2(-24f, -20f), outline);
            var status = Label(hudRect, "Status", null, "Throw 1/13", 64f, Color.white, outline);
            Place((RectTransform)status.transform, topCenter, topCenter, topCenter, new Vector2(0f, -22f), new Vector2(760f, 90f));
            var meter = NewImage(hudRect, "Meter", GetSprite("Panel"), panelDark);
            meter.type = Image.Type.Sliced;
            Place(meter.rectTransform, topCenter, topCenter, topCenter, new Vector2(0f, -116f), new Vector2(560f, 46f));
            var fill = NewImage(meter.rectTransform, "Fill", GetSprite("Panel"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            var banner = Label(hudRect, "Banner", null, "Keep your friend safe!", 130f, Color.white, outline);
            Place((RectTransform)banner.transform, center, center, center, new Vector2(0f, 170f), new Vector2(1700f, 170f));
            // High up, clear of the ninjas in the middle where the save happens.
            var prompt = Label(hudRect, "Prompt", null, "BLOCK!", 150f, Color.white, outline);
            Place((RectTransform)prompt.transform, topCenter, topCenter, topCenter, new Vector2(0f, -110f), new Vector2(1400f, 190f));
            var hint = Label(hudRect, "Hint", null, "Both hands up, like a shield!", 70f, hintYellow, outline);
            Place((RectTransform)hint.transform, bottomCenter, bottomCenter, bottomCenter, new Vector2(80f, 40f), new Vector2(1300f, 100f));

            Set(hud, "panels", property =>
            {
                property.arraySize = 2;
                property.GetArrayElementAtIndex(0).objectReferenceValue = left;
                property.GetArrayElementAtIndex(1).objectReferenceValue = right;
            });
            Set(hud, "statusLabel", status);
            Set(hud, "banner", banner);
            Set(hud, "prompt", prompt);
            Set(hud, "promptText", prompt.GetComponent<TextMeshProUGUI>());
            Set(hud, "hint", hint);
            Set(hud, "meter", meter.gameObject);
            Set(hud, "meterFill", fill);
            return hud;
        }

        // The tag on top, the count under it and the last gesture's word below, all against that corner.
        static KidsPlayerPanel BuildPanel(RectTransform hud, string name, Vector2 corner, Vector2 position, Material outline)
        {
            var rightSide = corner.x > 0.5f;
            var alignment = rightSide ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            var rect = NewRect(name, hud);
            Place(rect, corner, corner, corner, position, new Vector2(560f, 250f));
            var panel = rect.gameObject.AddComponent<KidsPlayerPanel>();
            var edge = new Vector2(rightSide ? 1f : 0f, 1f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(rect, Vector2.zero, 76f);
            Place((RectTransform)tag.transform, edge, edge, edge, Vector2.zero, new Vector2(130f, 90f));
            var count = Label(rect, "Count", null, "Blocks 0", 60f, Color.white, outline, alignment);
            Place((RectTransform)count.transform, edge, edge, edge, new Vector2(rightSide ? -150f : 150f, -6f), new Vector2(410f, 80f));
            var flash = Label(rect, "Flash", null, "BLOCKED!", 76f, Color.white, outline, alignment);
            Place((RectTransform)flash.transform, edge, edge, edge, new Vector2(0f, -100f), new Vector2(560f, 96f));
            Set(panel, "playerTag", tag);
            Set(panel, "countLabel", count);
            Set(panel, "flashLabel", flash);
            Set(panel, "flashText", flash.GetComponent<TextMeshProUGUI>());
            return panel;
        }

        #endregion
    }
}
