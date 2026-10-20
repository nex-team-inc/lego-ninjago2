#nullable enable

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.ChapterWorldBuilder;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The stage every level plays on: the world camera with its Cinemachine brain and link camera, the sun, the route,
    /// the theme painter with every role material, and the stage canvas with the world feed, the danger glow and the HUD frame.
    /// </summary>
    public static class ChapterStageBuilder
    {
        public const string StagePath = ChapterActorsBuilder.Root + "/ChapterStage.prefab";
        const string GroundPath = ChapterActorsBuilder.Root + "/ChapterGround.prefab";
        const string HeartsRowPath = NinjagoViewsBuilder.WidgetsRoot + "/ChapterHeartsRow.prefab";
        const string StripIconPath = NinjagoViewsBuilder.WidgetsRoot + "/ChapterStripIcon.prefab";

        #region Stage

        public static void Build()
        {
            var ground = new GameObject("ChapterGround");
            Block(PrimitiveType.Cube, "Plane", ground.transform, Vector3.zero, new Vector3(1f, 0.04f, 1f), Role(ThemeRole.Ground));
            var groundPrefab = SavePrefab(ground, GroundPath).transform;
            // Clear in the middle, red toward the screen edges.
            BuildSprite("DangerGlow", 256, (u, v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)))),
                Vector4.zero);
            var heartsRow = BuildHeartsRow();
            var stripIcon = BuildStripIcon();

            var root = new GameObject("ChapterStage");
            var stage = root.AddComponent<ChapterStage>();
            var painter = root.AddComponent<ThemePainter>();
            var cameraObject = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 3f, -5f), Vector3.zero, 60f);
            cameraObject.name = "WorldCamera";
            cameraObject.farClipPlane = 400f;
            var brain = cameraObject.gameObject.AddComponent<CinemachineBrain>();
            var linkCamera = ViewCamera(root.transform, Vector3.zero, Vector3.zero, 60f);
            linkCamera.name = "LinkCamera";
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.8f;
            var route = Empty("Route", root.transform, Vector3.zero);

            var canvasRect = NewRect("StageCanvas", root.transform);
            canvasRect.gameObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var feed = Feed(canvasRect, "WorldFeed", Vector2.zero, Vector2.one);
            var hud = BuildHud(canvasRect, heartsRow, stripIcon);

            var roles = new List<ThemePainter.RoleMaterial>();
            foreach (ThemeRole role in System.Enum.GetValues(typeof(ThemeRole))) roles.Add(new ThemePainter.RoleMaterial { material = Role(role), role = role });
            void Map(string material, ThemeRole role) => roles.Add(new ThemePainter.RoleMaterial { material = Load<Material>($"{MaterialsRoot}/{material}.mat"), role = role });
            Map("Crouch Floor", ThemeRole.Path);
            Map("Crouch Wall Panel", ThemeRole.Wall);
            Map("Courtyard Roof", ThemeRole.Roof);
            Map("Crouch Grass", ThemeRole.Ground);
            Map("Crouch House Wall", ThemeRole.Wall);
            Map("Mecha Tree Leaves", ThemeRole.Foliage);
            Map("Mecha Hill", ThemeRole.Ground);
            Map("Crouch Gate Red", ThemeRole.Accent);
            Map("Road", ThemeRole.Path);
            Map("Grass", ThemeRole.Ground);
            Map("Tree Leaves", ThemeRole.Foliage);
            Map("Curb", ThemeRole.Accent);
            Map("Ground Far", ThemeRole.Ground);
            ChapterAssetsBuilder.SetField(painter, "roleMaterials", roles);

            Set(stage, "worldCamera", cameraObject);
            Set(stage, "brain", brain);
            Set(stage, "linkCamera", linkCamera);
            Set(stage, "sun", sun);
            Set(stage, "routeRoot", route);
            Set(stage, "stageCanvas", canvas);
            Set(stage, "feed", feed);
            Set(stage, "hud", hud);
            Set(stage, "painter", painter);
            Set(stage, "ninjaPrefab", LoadComponent<ChapterNinja>(ChapterActorsBuilder.NinjaPath));
            Set(stage, "groundPrefab", groundPrefab);
            SavePrefab(root, StagePath);
        }

        static ChapterHud BuildHud(RectTransform canvas, HeartsRow heartsRowPrefab, StripIcon stripIconPrefab)
        {
            var outline = OutlineTextMaterial();
            var rect = NewRect("Hud", canvas);
            Stretch(rect);
            var hud = rect.gameObject.AddComponent<ChapterHud>();
            var glow = NewImage(rect, "DangerGlow", GetSprite("DangerGlow"), new Color(0.95f, 0.1f, 0.05f, 0f));
            glow.raycastTarget = false;
            Stretch(glow.rectTransform);
            var rows = new List<Object>();
            for (var i = 0; i < SimulatedBody.MaxPlayers; i++)
            {
                var row = (HeartsRow)UnityEditor.PrefabUtility.InstantiatePrefab(heartsRowPrefab, rect);
                Place((RectTransform)row.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f - i * 84f), new Vector2(560f, 76f));
                rows.Add(row);
            }

            var strip = NewRect("Strip", rect);
            Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 110f));
            var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var captionPanel = NewImage(rect, "Caption", GetSprite("Panel"), new Color(0.06f, 0.06f, 0.1f, 0.82f));
            captionPanel.type = Image.Type.Sliced;
            Place(captionPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1180f, 110f));
            var caption = Label(captionPanel.rectTransform, "Label", null, "It's running! Chase it!", 62f, Color.white, outline);
            Stretch((RectTransform)caption.transform);
            var pip = NinjagoViewsBuilder.CameraPreview(rect, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(384f, 216f), out var pipFrame);

            SetArray(hud, "heartsRows", rows);
            Set(hud, "stripRoot", strip);
            Set(hud, "stripIconPrefab", stripIconPrefab);
            Set(hud, "captionPanel", captionPanel.gameObject);
            Set(hud, "caption", caption);
            Set(hud, "pipFrame", pipFrame);
            Set(hud, "pipIndicators", pip);
            Set(hud, "dangerGlow", glow);
            return hud;
        }

        static HeartsRow BuildHeartsRow()
        {
            var root = NewRect("ChapterHeartsRow", null);
            root.sizeDelta = new Vector2(560f, 76f);
            var row = root.gameObject.AddComponent<HeartsRow>();
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 64f);
            Place((RectTransform)tag.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 76f));
            var hearts = NewRect("Hearts", root);
            Place(hearts, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(-120f, 64f));
            var layout = hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            Set(row, "playerTag", tag);
            Set(row, "heartsRoot", hearts);
            Set(row, "heartPrefab", Load<GameObject>($"{NinjagoViewsBuilder.WidgetsRoot}/Heart.prefab").GetComponent<Image>());
            return SavePrefab(root.gameObject, HeartsRowPath).GetComponent<HeartsRow>();
        }

        static StripIcon BuildStripIcon()
        {
            var root = NewRect("ChapterStripIcon", null);
            root.sizeDelta = new Vector2(84f, 84f);
            var icon = root.gameObject.AddComponent<StripIcon>();
            var frame = NewImage(root, "Frame", GetSprite("Panel"), Color.white);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);
            var back = NewImage(root, "Back", GetSprite("Panel"), new Color(0.08f, 0.08f, 0.12f, 0.9f));
            back.type = Image.Type.Sliced;
            Place(back.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            var image = NewImage(root, "Icon", null, Color.white);
            Place(image.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-22f, -22f));
            Set(icon, "icon", image);
            Set(icon, "frame", frame);
            return SavePrefab(root.gameObject, StripIconPath).GetComponent<StripIcon>();
        }

        #endregion
    }
}
