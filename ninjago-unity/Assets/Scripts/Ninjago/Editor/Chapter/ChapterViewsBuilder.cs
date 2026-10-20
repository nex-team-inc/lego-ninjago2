#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The chapter's view prefabs: the empty block view, the start card, the rest view and the ending view.</summary>
    public static class ChapterViewsBuilder
    {
        public const string BlockViewPath = NinjagoViewsBuilder.ViewsRoot + "/ChapterBlockView.prefab";
        public const string StartViewPath = NinjagoViewsBuilder.ViewsRoot + "/ChapterStartView.prefab";
        public const string RestViewPath = NinjagoViewsBuilder.ViewsRoot + "/ChapterRestView.prefab";
        public const string EndingViewPath = NinjagoViewsBuilder.ViewsRoot + "/ChapterEndingView.prefab";
        const string NodePath = NinjagoViewsBuilder.WidgetsRoot + "/ChapterRestNode.prefab";
        static readonly Color gold = new(1f, 0.85f, 0.25f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var node = BuildNode();
                NinjagoViewsBuilder.BuildView<ChapterBlockView>(BlockViewPath, (view, ui) => Set(view, "keyResponder", BackProxy(ui)));
                NinjagoViewsBuilder.BuildView<ChapterStartView>(StartViewPath, BuildStart);
                NinjagoViewsBuilder.BuildView<ChapterRestView>(RestViewPath, (view, ui) => BuildRest(view, ui, node));
                NinjagoViewsBuilder.BuildView<ChapterEndingView>(EndingViewPath, (view, ui) => BuildEnding(view, ui, node));
            });
        }

        #endregion

        #region Views

        static void BuildStart(ChapterStartView view, RectTransform ui)
        {
            var title = NinjagoViewsBuilder.Title(ui, "chapter.mode.proof", "Ninja Chapter Proof", new Vector2(0f, 220f));
            var progress = NinjagoViewsBuilder.Banner(ui, "Progress", "chapter.start.fresh", "3 levels, then the Boss", 72f, gold, new Vector2(0f, 60f));
            progress.StringReference = new UnityEngine.Localization.LocalizedString();
            var row = NewRect("Buttons", ui);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1000f, 200f));
            // A layout row, so Start centers when there is nothing to continue.
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 120f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var continueButton = NinjagoViewsBuilder.CloneButton(row, "Continue", "chapter.start.continue", "Continue", new Vector2(-240f, 0f));
            var startButton = NinjagoViewsBuilder.CloneButton(row, "Start", "chapter.start.start", "Start", new Vector2(240f, 0f));
            var group = NinjagoViewsBuilder.Group(row, continueButton, startButton);
            Set(view, "title", title);
            Set(view, "progressLabel", progress);
            Set(view, "continueButton", continueButton.GetComponent<Button>());
            Set(view, "startButton", startButton.GetComponent<Button>());
            Set(view, "buttons", group);
            Set(view, "keyResponder", group);
        }

        static void BuildRest(ChapterRestView view, RectTransform ui, RestNode nodePrefab)
        {
            var outline = OutlineTextMaterial();
            var title = NinjagoViewsBuilder.Title(ui, "chapter.rest.title", "Level 1 cleared!", new Vector2(0f, 400f));
            title.StringReference = new UnityEngine.Localization.LocalizedString();
            var path = BuildPath(ui, nodePrefab, new Vector2(0f, 170f));
            var hearts = Label(ui, "Hearts", null, "Hearts left 4/5", 64f, Color.white, outline);
            Place((RectTransform)hearts.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, -40f), new Vector2(700f, 90f));
            var stars = StarsRow(ui, new Vector2(300f, -40f));
            var ahead = Label(ui, "Ahead", null, "2 more before the Boss!", 72f, gold, outline);
            Place((RectTransform)ahead.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1500f, 100f));
            var row = NewRect("Buttons", ui);
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(800f, 160f));
            var confirm = NinjagoViewsBuilder.CloneButton(row, "Confirm", "chapter.rest.confirm", "Next level", Vector2.zero);
            var group = NinjagoViewsBuilder.Group(row, confirm);
            Set(view, "path", path);
            Set(view, "title", title);
            SetLocalized(view, "titleText", "chapter.rest.title");
            Set(view, "heartsLabel", hearts);
            SetLocalized(view, "heartsText", "chapter.rest.hearts");
            SetArray(view, "stars", stars);
            Set(view, "aheadLabel", ahead);
            SetLocalized(view, "levelsAheadText", "chapter.rest.ahead");
            SetLocalized(view, "bossNextText", "chapter.rest.boss_next");
            Set(view, "confirmButton", confirm.GetComponent<Button>());
            Set(view, "keyResponder", group);
        }

        static void BuildEnding(ChapterEndingView view, RectTransform ui, RestNode nodePrefab)
        {
            var outline = OutlineTextMaterial();
            var escape = NinjagoViewsBuilder.Title(ui, "chapter.ending.escape", "It got away...", new Vector2(0f, 400f));
            var path = BuildPath(ui, nodePrefab, new Vector2(0f, 170f));
            var stars = Label(ui, "Stars", null, "Stars 9/12", 64f, gold, outline);
            Place((RectTransform)stars.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1200f, 90f));
            var chase = Label(ui, "Chase", null, "You got 80% of the way to the Boss", 56f, Color.white, outline);
            Place((RectTransform)chase.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1500f, 80f));
            var continued = NinjagoViewsBuilder.Banner(ui, "Continued", "chapter.ending.continued", "To be continued...", 84f, new Color(0.75f, 0.5f, 1f), new Vector2(0f, -230f));
            var row = NewRect("Buttons", ui);
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(800f, 160f));
            var menu = NinjagoViewsBuilder.CloneButton(row, "Menu", "chapter.ending.menu", "Menu", Vector2.zero);
            var group = NinjagoViewsBuilder.Group(row, menu);
            Set(view, "path", path);
            Set(view, "escapeLine", escape);
            SetLocalized(view, "escapeText", "chapter.ending.escape");
            SetLocalized(view, "finishedText", "chapter.ending.finished");
            Set(view, "starsLabel", stars);
            SetLocalized(view, "starsText", "chapter.ending.stars");
            Set(view, "chaseLabel", chase);
            SetLocalized(view, "chaseText", "chapter.ending.chase");
            Set(view, "continuedLabel", continued.gameObject);
            Set(view, "menuButton", menu.GetComponent<Button>());
            Set(view, "keyResponder", group);
        }

        #endregion

        #region Widgets

        static ProgressPath BuildPath(RectTransform ui, RestNode nodePrefab, Vector2 position)
        {
            var rect = NewRect("ProgressPath", ui);
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(1600f, 240f));
            var line = NewImage(rect, "Line", null, new Color(1f, 1f, 1f, 0.25f));
            Place(line.rectTransform, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(0f, 10f));
            var nodes = NewRect("Nodes", rect);
            Stretch(nodes);
            var layout = nodes.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 60f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var path = rect.gameObject.AddComponent<ProgressPath>();
            Set(path, "nodesRoot", nodes);
            Set(path, "nodePrefab", nodePrefab);
            return path;
        }

        static RestNode BuildNode()
        {
            var outline = OutlineTextMaterial();
            var root = NewRect("ChapterRestNode", null);
            root.sizeDelta = new Vector2(220f, 220f);
            var node = root.gameObject.AddComponent<RestNode>();
            var highlight = NewImage(root, "NextHighlight", GetSprite("Ring"), new Color(1f, 0.9f, 0.4f));
            Place(highlight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(160f, 160f));
            var disc = NewImage(root, "Disc", GetSprite("Circle"), Color.white);
            Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(120f, 120f));
            var done = NewImage(root, "DoneMark", GetSprite("Check"), Color.white);
            Place(done.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(80f, 80f));
            var label = Label(root, "Label", null, "Level 1", 46f, Color.white, outline);
            Place((RectTransform)label.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(220f, 60f));
            var stars = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                var star = NewImage(root, $"Star{i}", GetSprite("Star"), gold);
                Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 46f, 0f), new Vector2(40f, 40f));
                stars.Add(star);
            }

            Set(node, "label", label);
            Set(node, "disc", disc);
            Set(node, "doneMark", done.gameObject);
            Set(node, "nextHighlight", highlight.gameObject);
            SetArray(node, "stars", stars);
            return SavePrefab(root.gameObject, NodePath).GetComponent<RestNode>();
        }

        static List<Object> StarsRow(RectTransform ui, Vector2 position)
        {
            var stars = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                var star = NewImage(ui, $"Star{i}", GetSprite("Star"), gold);
                Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2((i - 1) * 110f, 0f),
                    new Vector2(96f, 96f));
                stars.Add(star);
            }

            return stars;
        }

        #endregion
    }
}
