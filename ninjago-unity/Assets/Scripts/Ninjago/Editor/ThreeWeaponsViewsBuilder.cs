#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The Three Weapons view (feeds, cards layer, cursors, one corner preview), its HUD and the weapon card. Setup reuses the hand cursor setup view.</summary>
    public static class ThreeWeaponsViewsBuilder
    {
        public const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/ThreeWeaponsView.prefab";
        const string WidgetsRoot = NinjagoViewsBuilder.WidgetsRoot;
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f, 0.9f);
        static readonly Color safeGreen = new(0.3f, 1f, 0.35f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var mark = LoadComponent<HandCursorMark>(WidgetsRoot + "/HandCursorMark.prefab");
                var card = BuildCard();
                var hud = BuildHud();
                NinjagoViewsBuilder.BuildView<ThreeWeaponsView>(ViewPath, (view, ui) => BuildView(view, ui, hud, card, mark));
            });
        }

        #endregion

        #region View

        static void BuildView(ThreeWeaponsView view, RectTransform ui, GauntletHud hudPrefab, WeaponCard cardPrefab, HandCursorMark mark)
        {
            var solo = Feed(ui, "SoloFeed", Vector2.zero, Vector2.one);
            var left = Feed(ui, "LeftFeed", Vector2.zero, new Vector2(0.5f, 1f));
            var right = Feed(ui, "RightFeed", new Vector2(0.5f, 0f), Vector2.one);
            var divider = NewImage(ui, "SplitDivider", null, panelDark);
            Place(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));
            // One preview shows every player; the bottom-right corner is floor in either layout.
            var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(384f, 216f), out var pipFrame);
            var cards = Stretch(NewRect("Cards", ui));
            var layer = CursorGamesViewsBuilder.CursorLayer(ui, mark);

            Set(view, "soloFeed", solo);
            Set(view, "leftFeed", left);
            Set(view, "rightFeed", right);
            Set(view, "splitDivider", divider.gameObject);
            Set(view, "hudPrefab", hudPrefab);
            Set(view, "lanePrefab", LoadComponent<GauntletLane>(ThreeWeaponsWorldBuilder.LanePath));
            Set(view, "cardsLayer", cards);
            Set(view, "cardPrefab", cardPrefab);
            Set(view, "cursorLayer", layer);
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        #endregion

        #region Widgets

        static GauntletHud BuildHud()
        {
            var outline = OutlineTextMaterial();
            var root = NewRect("GauntletHud", null);
            var hud = root.gameObject.AddComponent<GauntletHud>();
            var topLeft = new Vector2(0f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, topLeft, topLeft, topLeft, new Vector2(30f, -20f), new Vector2(150f, 90f));
            var fight = Label(root, "Fight", null, "FIGHT 1/3", 60f, Color.white, outline, TextAlignmentOptions.Left);
            Place((RectTransform)fight.transform, topLeft, topLeft, topLeft, new Vector2(190f, -30f), new Vector2(420f, 76f));
            var pick = NinjagoViewsBuilder.Banner(root, "PickPrompt", "ninjago.weapons.pick", "PICK A WEAPON", 96f, Color.white, Vector2.zero);
            Place((RectTransform)pick.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1000f, 125f));
            var gesture = NinjagoViewsBuilder.Banner(root, "GesturePrompt", "ninjago.weapons.slash", "SLASH", 110f, Color.white, Vector2.zero);
            Place((RectTransform)gesture.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1000f, 140f));
            var cleared = NinjagoViewsBuilder.Banner(root, "ClearedBanner", "ninjago.weapons.cleared", "GAUNTLET CLEARED!", 110f, safeGreen, Vector2.zero);
            Place((RectTransform)cleared.transform, center, center, center, new Vector2(0f, 80f), new Vector2(1100f, 150f));

            Set(hud, "playerTag", tag);
            Set(hud, "fightLabel", fight);
            SetLocalized(hud, "fightString", "ninjago.weapons.fight");
            Set(hud, "pickPrompt", pick.gameObject);
            Set(hud, "gesturePrompt", gesture);
            Set(hud, "clearedBanner", cleared.gameObject);
            Stretch(root);
            return SavePrefab(root.gameObject, WidgetsRoot + "/GauntletHud.prefab").GetComponent<GauntletHud>();
        }

        // A tall card: the weapon's icon and word on its color, and a bar filling in the player's color.
        static WeaponCard BuildCard()
        {
            var root = NewRect("WeaponCard", null);
            root.sizeDelta = new Vector2(280f, 340f);
            var card = root.gameObject.AddComponent<WeaponCard>();
            var background = root.gameObject.AddComponent<Image>();
            background.sprite = GetSprite("Panel");
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            outline.effectDistance = new Vector2(5f, -5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var icon = NewImage(root, "Icon", GetSprite("SwordIcon"), Color.white);
            Place(icon.rectTransform, top, top, top, new Vector2(0f, -24f), new Vector2(190f, 190f));
            var label = Label(root, "Label", "ninjago.weapons.sword", "SWORD", 54f, Color.white, OutlineTextMaterial());
            Place((RectTransform)label.transform, bottom, bottom, bottom, new Vector2(0f, 52f), new Vector2(250f, 70f));
            var labelText = label.GetComponent<TMP_Text>();
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 28f;
            labelText.fontSizeMax = 54f;
            var track = NewImage(root, "HoldTrack", GetSprite("Panel"), panelDark);
            track.type = Image.Type.Sliced;
            Place(track.rectTransform, bottom, bottom, bottom, new Vector2(0f, 16f), new Vector2(230f, 26f));
            var fill = NewImage(root, "HoldFill", GetSprite("Panel"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Place(fill.rectTransform, bottom, bottom, bottom, new Vector2(0f, 16f), new Vector2(230f, 26f));
            Set(card, "rect", root);
            Set(card, "background", background);
            Set(card, "icon", icon);
            Set(card, "label", label);
            Set(card, "holdFill", fill);
            return SavePrefab(root.gameObject, WidgetsRoot + "/WeaponCard.prefab").GetComponent<WeaponCard>();
        }

        #endregion
    }
}
