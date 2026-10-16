#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The One Punch view (the street feed, a corner panel per player, the wave, banners, the wiggle meter and one corner
    /// camera preview) and its result view, whose lines are compact so two players' counts fit. Setup reuses the plain
    /// Ninjago setup view.
    /// </summary>
    public static class OnePunchViewsBuilder
    {
        public const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/OnePunchView.prefab";
        public const string ResultViewPath = NinjagoViewsBuilder.ViewsRoot + "/OnePunchResultView.prefab";
        const string ResultLinePath = NinjagoViewsBuilder.WidgetsRoot + "/OnePunchResultLine.prefab";
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f, 0.85f);
        static readonly Color hintYellow = new(1f, 0.9f, 0.3f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var line = BuildResultLine();
                NinjagoViewsBuilder.BuildView<OnePunchView>(ViewPath, BuildView);
                NinjagoViewsBuilder.BuildView<NinjagoResultView>(ResultViewPath, (view, ui) => NinjagoViewsBuilder.BuildResult(view, ui, line));
            });
        }

        #endregion

        #region View

        static void BuildView(OnePunchView view, RectTransform ui)
        {
            var outline = OutlineTextMaterial();
            var feed = Feed(ui, "StreetFeed", Vector2.zero, Vector2.one);
            var hudRect = Stretch(NewRect("HUD", ui));
            var hud = hudRect.gameObject.AddComponent<OnePunchHud>();
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var topCenter = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            var left = BuildPanel(hudRect, "LeftPanel", topLeft, new Vector2(24f, -20f), outline);
            var right = BuildPanel(hudRect, "RightPanel", topRight, new Vector2(-24f, -20f), outline);
            var wave = Label(hudRect, "Wave", null, "Wave 1/5   5 left", 64f, Color.white, outline);
            Place((RectTransform)wave.transform, topCenter, topCenter, topCenter, new Vector2(0f, -22f), new Vector2(800f, 90f));
            var banner = Label(hudRect, "CenterBanner", null, "WAVE 1", 150f, Color.white, outline);
            Place((RectTransform)banner.transform, center, center, center, new Vector2(0f, 150f), new Vector2(1500f, 190f));
            var hint = Label(hudRect, "PunchHint", "ninjago.punch.hint", "Short punch forward!", 84f, hintYellow, outline);
            Place((RectTransform)hint.transform, center, center, center, new Vector2(0f, 20f), new Vector2(1500f, 110f));
            var pin = Label(hudRect, "PinPrompt", "ninjago.punch.wiggle", "WIGGLE YOUR HIPS!", 118f, hintYellow, outline);
            Place((RectTransform)pin.transform, center, center, center, new Vector2(0f, 230f), new Vector2(1600f, 150f));
            var meter = NewImage(hudRect, "WiggleMeter", GetSprite("Panel"), panelDark);
            meter.type = Image.Type.Sliced;
            Place(meter.rectTransform, center, center, center, new Vector2(0f, 120f), new Vector2(760f, 64f));
            var fill = NewImage(meter.rectTransform, "Fill", GetSprite("Panel"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));
            // Bottom left: the ninjas stand bottom center and the counts panels sit up top.
            var pip = NinjagoViewsBuilder.CameraPreview(ui, Vector2.zero, new Vector2(24f, 24f), new Vector2(384f, 216f), out var pipFrame);

            Set(hud, "panels", property =>
            {
                property.arraySize = 2;
                property.GetArrayElementAtIndex(0).objectReferenceValue = left;
                property.GetArrayElementAtIndex(1).objectReferenceValue = right;
            });
            Set(hud, "waveLabel", wave);
            SetLocalized(hud, "waveString", "ninjago.punch.wave");
            Set(hud, "centerBanner", banner);
            SetLocalized(hud, "waveBannerString", "ninjago.punch.wave_banner");
            SetLocalized(hud, "waveClearedString", "ninjago.punch.wave_cleared");
            SetLocalized(hud, "streetClearedString", "ninjago.punch.street_cleared");
            SetLocalized(hud, "downString", "ninjago.punch.down");
            Set(hud, "punchHint", hint.gameObject);
            Set(hud, "pinPrompt", pin.gameObject);
            Set(hud, "wiggleMeter", meter.gameObject);
            Set(hud, "wiggleFill", fill);

            Set(view, "feed", feed);
            Set(view, "hud", hud);
            Set(view, "streetPrefab", LoadComponent<OnePunchStreet>(OnePunchWorldBuilder.StreetPath));
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        // Tag and health bar on top, the four try counts under them, and the last try's word below in its color.
        static OnePunchPlayerPanel BuildPanel(RectTransform hud, string name, Vector2 corner, Vector2 position, Material outline)
        {
            var rightSide = corner.x > 0.5f;
            var alignment = rightSide ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            var rect = NewRect(name, hud);
            Place(rect, corner, corner, corner, position, new Vector2(640f, 230f));
            var panel = rect.gameObject.AddComponent<OnePunchPlayerPanel>();
            var edge = new Vector2(rightSide ? 1f : 0f, 1f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(rect, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, edge, edge, edge, new Vector2(0f, 0f), new Vector2(130f, 86f));
            var bar = NewImage(rect, "HealthBar", GetSprite("Panel"), panelDark);
            bar.type = Image.Type.Sliced;
            Place(bar.rectTransform, edge, edge, edge, new Vector2(rightSide ? -150f : 150f, -14f), new Vector2(470f, 58f));
            var fill = NewImage(bar.rectTransform, "Fill", GetSprite("Panel"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = rightSide ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            var counts = Label(rect, "Counts", null, "Ext 0  Cam 0  Slash 0  None 0", 46f, Color.white, outline, alignment);
            Place((RectTransform)counts.transform, edge, edge, edge, new Vector2(0f, -94f), new Vector2(640f, 60f));
            var signal = Label(rect, "Signal", "ninjago.punch.signal_extension", "EXTENSION!", 70f, Color.white, outline, alignment);
            Place((RectTransform)signal.transform, edge, edge, edge, new Vector2(0f, -150f), new Vector2(640f, 86f));

            Set(panel, "playerTag", tag);
            Set(panel, "healthFill", fill);
            Set(panel, "countsLabel", counts);
            SetLocalized(panel, "countsString", "ninjago.punch.counts");
            Set(panel, "signalLabel", signal);
            Set(panel, "signalText", signal.GetComponent<TextMeshProUGUI>());
            return panel;
        }

        #endregion

        #region Result

        // Smaller than the shared result line, and it shrinks a long line to fit rather than spill.
        static ResultLine BuildResultLine()
        {
            var root = NewRect("OnePunchResultLine", null);
            root.sizeDelta = new Vector2(1760f, 60f);
            var line = root.gameObject.AddComponent<ResultLine>();
            var label = Label(root, "Text", null, "P1    Extension 0    Toward camera 0    Side slash rejected 0    Nothing 0", 44f, Color.white,
                OutlineTextMaterial());
            Stretch((RectTransform)label.transform);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 44f;
            Set(line, "label", label);
            Set(line, "text", text);
            return SavePrefab(root.gameObject, ResultLinePath).GetComponent<ResultLine>();
        }

        #endregion
    }
}
