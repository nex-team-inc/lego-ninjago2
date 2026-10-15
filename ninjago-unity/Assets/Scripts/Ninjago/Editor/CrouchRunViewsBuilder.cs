#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The Crouch Run view (one strip per player, one corner camera preview) and its HUD. Setup reuses the plain Ninjago setup view.</summary>
    public static class CrouchRunViewsBuilder
    {
        public const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/CrouchRunView.prefab";
        const string HudPath = NinjagoViewsBuilder.WidgetsRoot + "/CrouchHud.prefab";
        static readonly Color warnRed = new(1f, 0.25f, 0.2f);
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f, 0.9f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var hud = BuildHud();
                NinjagoViewsBuilder.BuildView<CrouchRunView>(ViewPath, (view, ui) => BuildView(view, ui, hud));
            });
        }

        #endregion

        #region View

        static void BuildView(CrouchRunView view, RectTransform ui, CrouchHud hudPrefab)
        {
            var solo = Feed(ui, "SoloFeed", Vector2.zero, Vector2.one);
            var top = Feed(ui, "TopFeed", new Vector2(0f, 0.5f), Vector2.one);
            var bottom = Feed(ui, "BottomFeed", Vector2.zero, new Vector2(1f, 0.5f));
            var divider = NewImage(ui, "SplitDivider", null, panelDark);
            Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 10f));
            // One preview shows every player with their indicator; the top-left corner is sky behind the ninja in either layout.
            var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(384f, 216f), out var pipFrame);

            Set(view, "soloFeed", solo);
            Set(view, "topFeed", top);
            Set(view, "bottomFeed", bottom);
            Set(view, "splitDivider", divider.gameObject);
            Set(view, "hudPrefab", hudPrefab);
            Set(view, "lanePrefab", LoadComponent<CrouchLane>(CrouchRunWorldBuilder.LanePath));
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        #endregion

        #region HUD

        // The banner is the rule, so it is the biggest thing on screen after HEARD: top center, in the zone's color.
        static CrouchHud BuildHud()
        {
            var outline = OutlineTextMaterial();
            var root = NewRect("CrouchHud", null);
            var hud = root.gameObject.AddComponent<CrouchHud>();
            var topCenter = new Vector2(0.5f, 1f);
            var topRight = new Vector2(1f, 1f);
            var center = new Vector2(0.5f, 0.5f);

            var panel = NewImage(root, "Banner", GetSprite("Panel"), Color.white);
            panel.type = Image.Type.Sliced;
            Place(panel.rectTransform, topCenter, topCenter, topCenter, new Vector2(0f, -12f), new Vector2(980f, 196f));
            var title = Label(panel.rectTransform, "Title", null, "CLEAR", 116f, Color.white, outline);
            Place((RectTransform)title.transform, topCenter, topCenter, topCenter, new Vector2(0f, -4f), new Vector2(960f, 120f));
            var instruction = Label(panel.rectTransform, "Instruction", null, "STAND TO RUN", 62f, Color.white, outline);
            Place((RectTransform)instruction.transform, topCenter, topCenter, topCenter, new Vector2(0f, -116f), new Vector2(960f, 76f));

            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, topRight, topRight, topRight, new Vector2(-300f, -28f), new Vector2(150f, 90f));
            var clock = Label(root, "Clock", null, "45", 116f, Color.white, outline, TextAlignmentOptions.Right);
            Place((RectTransform)clock.transform, topRight, topRight, topRight, new Vector2(-30f, -10f), new Vector2(260f, 130f));
            var level = Label(root, "Level", null, "LEVEL 1", 52f, Color.white, outline, TextAlignmentOptions.Right);
            Place((RectTransform)level.transform, topRight, topRight, topRight, new Vector2(-30f, -138f), new Vector2(360f, 64f));
            var heardCount = Label(root, "HeardCount", null, "Heard 0", 48f, new Color(1f, 0.6f, 0.55f), outline, TextAlignmentOptions.Right);
            Place((RectTransform)heardCount.transform, topRight, topRight, topRight, new Vector2(-30f, -196f), new Vector2(360f, 60f));

            var heard = Label(root, "Heard", "ninjago.crouch.heard", "HEARD", 210f, warnRed, outline);
            Place((RectTransform)heard.transform, center, center, center, new Vector2(0f, 60f), new Vector2(1400f, 260f));
            var centerBanner = Label(root, "CenterBanner", null, "LEVEL 1", 140f, Color.white, outline);
            Place((RectTransform)centerBanner.transform, center, center, center, new Vector2(0f, -30f), new Vector2(1400f, 180f));

            Set(hud, "playerTag", tag);
            Set(hud, "bannerPanel", panel);
            Set(hud, "bannerTitle", title);
            Set(hud, "bannerInstruction", instruction);
            Set(hud, "clockLabel", clock);
            Set(hud, "clockText", clock.GetComponent<TextMeshProUGUI>());
            SetLocalized(hud, "clockString", "ninjago.crouch.clock");
            Set(hud, "levelLabel", level);
            SetLocalized(hud, "levelString", "ninjago.crouch.level");
            Set(hud, "heardCountLabel", heardCount);
            SetLocalized(hud, "heardCountString", "ninjago.crouch.heard_count");
            Set(hud, "heardFlash", (RectTransform)heard.transform);
            Set(hud, "centerBanner", centerBanner);
            SetLocalized(hud, "clearedString", "ninjago.crouch.cleared");
            SetLocalized(hud, "timeUpString", "ninjago.crouch.time_up");
            SetArray(hud, "compactElements", new Object[]
            {
                panel.rectTransform, (RectTransform)tag.transform, (RectTransform)clock.transform, (RectTransform)level.transform,
                (RectTransform)heardCount.transform, (RectTransform)centerBanner.transform,
            });
            Stretch(root);
            return SavePrefab(root.gameObject, HudPath).GetComponent<CrouchHud>();
        }

        #endregion
    }
}
