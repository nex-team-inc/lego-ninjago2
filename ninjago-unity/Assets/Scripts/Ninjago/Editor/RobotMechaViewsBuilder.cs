#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The Robot Mecha views and widgets: the setup view with its robot arms hold, the fight view with the large corner
    /// camera preview, the per-player HUD, the edge arrow and the setup card. Views start from the shared template.
    /// </summary>
    public static class RobotMechaViewsBuilder
    {
        public const string SetupViewPath = NinjagoViewsBuilder.ViewsRoot + "/MechaSetupView.prefab";
        public const string FightViewPath = NinjagoViewsBuilder.ViewsRoot + "/MechaFightView.prefab";
        const string WidgetsRoot = NinjagoViewsBuilder.WidgetsRoot;
        static readonly Color laserPink = new(1f, 0.35f, 0.8f);
        static readonly Color promptWhite = new(1f, 0.92f, 0.98f);
        static readonly Color warnRed = new(1f, 0.3f, 0.25f);
        static readonly Color safeGreen = new(0.3f, 1f, 0.35f);
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f, 0.88f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var heart = LoadComponent<Image>(WidgetsRoot + "/Heart.prefab");
                var status = LoadComponent<SetupPlayerStatus>(WidgetsRoot + "/SetupPlayerStatus.prefab");
                var card = BuildCard();
                var hud = BuildHud(heart, BuildEdgeArrow());
                NinjagoViewsBuilder.BuildView<NinjagoSetupView>(SetupViewPath, (view, ui) => BuildSetup(view, ui, status, card));
                NinjagoViewsBuilder.BuildView<MechaFightView>(FightViewPath, (view, ui) => BuildFight(view, ui, hud));
            });
        }

        #endregion

        #region Views

        static void BuildSetup(NinjagoSetupView view, RectTransform ui, SetupPlayerStatus statusPrefab, RobotArmsCard cardPrefab)
        {
            NinjagoViewsBuilder.BuildSetup(view, ui, statusPrefab);
            var outline = OutlineTextMaterial();
            var host = Stretch(NewRect("RobotArmsCheck", ui));
            var check = host.gameObject.AddComponent<RobotArmsCheck>();
            var root = Stretch(NewRect("CheckRoot", host));
            var prompt = Label(root, "Prompt", null, "Make an L with both arms and hold it", 72f, Color.white, outline);
            Place((RectTransform)prompt.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1800f, 100f));
            LGlyph(root, "BigL", new Vector2(-520f, 190f), new Vector2(200f, 236f), false);
            var title = Label(root, "RobotArms", "ninjago.mecha.robot_arms", "ROBOT ARMS", 120f, laserPink, outline);
            Place((RectTransform)title.transform, Center, Center, Center, new Vector2(-520f, -10f), new Vector2(820f, 150f));
            var hint = Label(root, "Hint", "ninjago.mecha.setup_hint", "Elbows bent, hands up, like a robot!", 50f, promptWhite, outline);
            Place((RectTransform)hint.transform, Center, Center, Center, new Vector2(-520f, -100f), new Vector2(860f, 80f));
            var cards = CursorGamesViewsBuilder.Row(root, "Cards", Center, new Vector2(-520f, -250f), new Vector2(860f, 140f), 30f, TextAnchor.MiddleCenter);
            // Large, so each player can see their own arms while making the L.
            var pip = NinjagoViewsBuilder.CameraPreview(root, Center, new Vector2(450f, 10f), new Vector2(944f, 531f), out var pipFrame);
            root.gameObject.SetActive(false);

            Set(check, "root", root.gameObject);
            Set(check, "cardsRow", cards);
            Set(check, "cardPrefab", cardPrefab);
            Set(check, "promptLabel", prompt);
            SetLocalized(check, "holdPrompt", "ninjago.mecha.setup_hold");
            SetLocalized(check, "readyPrompt", "ninjago.setup.ready");
            Set(check, "pipFrame", pipFrame);
            Set(check, "pipIndicators", pip);
            Set(view, "robotArmsCheck", check);
        }

        static void BuildFight(MechaFightView view, RectTransform ui, MechaHud hudPrefab)
        {
            var solo = Feed(ui, "SoloFeed", Vector2.zero, Vector2.one);
            var left = Feed(ui, "LeftFeed", Vector2.zero, new Vector2(0.5f, 1f));
            var right = Feed(ui, "RightFeed", new Vector2(0.5f, 0f), Vector2.one);
            var divider = NewImage(ui, "SplitDivider", null, panelDark);
            Place(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));
            // A quarter of the screen width in the top-left corner: a recording must show the pose as well as the mecha.
            var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(464f, 261f), out var pipFrame);

            Set(view, "soloFeed", solo);
            Set(view, "leftFeed", left);
            Set(view, "rightFeed", right);
            Set(view, "splitDivider", divider.gameObject);
            Set(view, "hudPrefab", hudPrefab);
            Set(view, "lanePrefab", LoadComponent<MechaLane>(RobotMechaWorldBuilder.LanePath));
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        #endregion

        #region Widgets

        // One player's half: tag, hearts, arm icons and kills top right, time top center, the first prompt above the
        // mecha, banners, and the edge arrows drawn over everything else.
        static MechaHud BuildHud(Image heartPrefab, Image arrowPrefab)
        {
            var outline = OutlineTextMaterial();
            var root = NewRect("MechaHud", null);
            var hud = root.gameObject.AddComponent<MechaHud>();
            var flash = NewImage(root, "HitFlash", null, new Color(1f, 0.12f, 0.1f, 0f));
            Stretch(flash.rectTransform);
            var topRight = new Vector2(1f, 1f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, topRight, topRight, topRight, new Vector2(-30f, -20f), new Vector2(150f, 90f));
            var hearts = CursorGamesViewsBuilder.Row(root, "Hearts", topRight, new Vector2(-190f, -32f), new Vector2(360f, 64f), 8f, TextAnchor.MiddleRight);
            var leftArm = LGlyph(root, "LeftArmIcon", Vector2.zero, new Vector2(54f, 64f), false);
            Place(leftArm.rectTransform, topRight, topRight, topRight, new Vector2(-262f, -112f), new Vector2(54f, 64f));
            var rightArm = LGlyph(root, "RightArmIcon", Vector2.zero, new Vector2(54f, 64f), true);
            Place(rightArm.rectTransform, topRight, topRight, topRight, new Vector2(-190f, -112f), new Vector2(54f, 64f));
            var kills = Label(root, "Kills", null, "Kills 0", 54f, Color.white, outline, TextAlignmentOptions.Right);
            Place((RectTransform)kills.transform, topRight, topRight, topRight, new Vector2(-30f, -190f), new Vector2(320f, 70f));
            var time = Label(root, "TimeLeft", null, "70", 110f, Color.white, outline);
            Place((RectTransform)time.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(320f, 130f));

            var prompt = NewRect("RobotArmsPrompt", root);
            Place(prompt, Center, Center, Center, new Vector2(0f, 250f), new Vector2(1000f, 240f));
            LGlyph(prompt, "BigL", new Vector2(-330f, 0f), new Vector2(170f, 200f), false);
            var promptText = Label(prompt, "RobotArms", "ninjago.mecha.robot_arms", "ROBOT ARMS", 124f, laserPink, outline, TextAlignmentOptions.Left);
            Place((RectTransform)promptText.transform, Center, Center, new Vector2(0f, 0.5f), new Vector2(-210f, 0f), new Vector2(760f, 170f));
            var down = NinjagoViewsBuilder.Banner(root, "DownBanner", "ninjago.mecha.down", "DOWN", 170f, warnRed, new Vector2(0f, 80f));
            var survived = NinjagoViewsBuilder.Banner(root, "SurvivedBanner", "ninjago.mecha.survived", "SURVIVED!", 140f, safeGreen, new Vector2(0f, 80f));
            var edgeLayer = Stretch(NewRect("EdgeArrows", root));
            var edge = edgeLayer.gameObject.AddComponent<EdgeArrows>();
            Set(edge, "area", edgeLayer);
            Set(edge, "arrowPrefab", arrowPrefab);

            Set(hud, "playerTag", tag);
            Set(hud, "heartsRow", hearts);
            Set(hud, "heartPrefab", heartPrefab);
            Set(hud, "leftArmIcon", leftArm);
            Set(hud, "rightArmIcon", rightArm);
            Set(hud, "timeLabel", time);
            SetLocalized(hud, "timeText", "ninjago.mecha.time_left");
            Set(hud, "killsLabel", kills);
            SetLocalized(hud, "killsText", "ninjago.mecha.kills");
            Set(hud, "robotArmsPrompt", prompt.gameObject);
            Set(hud, "hitFlash", flash);
            Set(hud, "downBanner", down.gameObject);
            Set(hud, "survivedBanner", survived.gameObject);
            Set(hud, "edgeArrows", edge);
            Stretch(root);
            return SavePrefab(root.gameObject, WidgetsRoot + "/MechaHud.prefab").GetComponent<MechaHud>();
        }

        // Larger than any other HUD element, outlined so it reads over the bright courtyard.
        static Image BuildEdgeArrow()
        {
            var root = NewRect("EdgeArrow", null);
            Place(root, Center, Center, Center, Vector2.zero, new Vector2(300f, 220f));
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = GetSprite("Arrow");
            image.raycastTarget = false;
            var border = root.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            border.effectDistance = new Vector2(5f, -5f);
            return SavePrefab(root.gameObject, WidgetsRoot + "/EdgeArrow.prefab").GetComponent<Image>();
        }

        static RobotArmsCard BuildCard()
        {
            var root = NewRect("RobotArmsCard", null);
            root.sizeDelta = new Vector2(400f, 140f);
            var card = root.gameObject.AddComponent<RobotArmsCard>();
            var background = root.gameObject.AddComponent<Image>();
            background.sprite = GetSprite("Panel");
            background.type = Image.Type.Sliced;
            background.color = panelDark;
            background.raycastTarget = false;
            var left = new Vector2(0f, 0.5f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 64f);
            Place((RectTransform)tag.transform, left, left, left, new Vector2(16f, 0f), new Vector2(110f, 80f));
            var leftArm = LGlyph(root, "LeftArmIcon", Vector2.zero, new Vector2(60f, 70f), false);
            Place(leftArm.rectTransform, left, left, left, new Vector2(140f, 14f), new Vector2(60f, 70f));
            var rightArm = LGlyph(root, "RightArmIcon", Vector2.zero, new Vector2(60f, 70f), true);
            Place(rightArm.rectTransform, left, left, left, new Vector2(220f, 14f), new Vector2(60f, 70f));
            var track = NewImage(root, "HoldTrack", GetSprite("Panel"), new Color(0.25f, 0.25f, 0.3f));
            track.type = Image.Type.Sliced;
            Place(track.rectTransform, left, left, left, new Vector2(140f, -44f), new Vector2(160f, 20f));
            var fill = NewImage(root, "HoldFill", GetSprite("Panel"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Place(fill.rectTransform, left, left, left, new Vector2(140f, -44f), new Vector2(160f, 20f));
            var done = NewImage(root, "DoneMark", GetSprite("Circle"), safeGreen);
            Place(done.rectTransform, left, left, left, new Vector2(318f, 0f), new Vector2(64f, 64f));

            Set(card, "playerTag", tag);
            Set(card, "leftArmIcon", leftArm);
            Set(card, "rightArmIcon", rightArm);
            Set(card, "holdFill", fill);
            Set(card, "doneMark", done.gameObject);
            return SavePrefab(root.gameObject, WidgetsRoot + "/RobotArmsCard.prefab").GetComponent<RobotArmsCard>();
        }

        #endregion

        #region Helpers

        static Vector2 Center => new(0.5f, 0.5f);

        // The L shape: the player's left arm reads as an L in the mirrored camera picture, the right arm as its mirror.
        static Image LGlyph(RectTransform parent, string name, Vector2 position, Vector2 size, bool mirrored)
        {
            var glyph = NewImage(parent, name, GetSprite("LShape"), laserPink);
            Place(glyph.rectTransform, Center, Center, Center, position, size);
            if (mirrored) glyph.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return glyph;
        }

        #endregion
    }
}
