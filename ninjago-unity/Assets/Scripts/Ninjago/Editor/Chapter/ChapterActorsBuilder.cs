#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The chapter's actors, all from primitives: the in-world prompt and window marker, the one ninja per player (the
    /// fight rig and the gait rig on one block body), the blocky brick monster, the much larger Boss with a strong
    /// silhouette, and the thrown rock with its stones.
    /// </summary>
    public static class ChapterActorsBuilder
    {
        public const string Root = PrefabsRoot + "/Chapter";
        public const string PromptPath = Root + "/WorldPrompt.prefab";
        public const string MarkerPath = Root + "/WindowMarker.prefab";
        public const string NinjaPath = Root + "/ChapterNinja.prefab";
        public const string EnemyPath = Root + "/BrickMonster.prefab";
        public const string BossPath = Root + "/Boss.prefab";
        public const string RockPath = Root + "/ChapterRock.prefab";
        public const string StonesPath = Root + "/ChapterStones.prefab";

        #region Entry Point

        public static void Build()
        {
            BuildPrompt();
            BuildMarker();
            BuildNinja();
            BuildEnemy(EnemyPath, false);
            BuildEnemy(BossPath, true);
            BuildRock();
            BuildStones();
        }

        #endregion

        #region Prompts

        static void BuildPrompt()
        {
            var outline = OutlineTextMaterial();
            var root = new GameObject("WorldPrompt");
            var prompt = root.AddComponent<WorldPrompt>();
            var canvas = NinjagoWorldBuilder.WorldCanvas(root.transform, "Canvas", Vector3.zero, 0.0032f, new Vector2(420f, 170f));
            var panel = NewImage(canvas.transform, "Panel", GetSprite("Panel"), new Color(0.1f, 0.1f, 0.15f, 0.9f));
            panel.type = Image.Type.Sliced;
            Stretch(panel.rectTransform);
            var label = Label(canvas.transform, "Label", null, "SLASH", 96f, Color.white, outline);
            Place((RectTransform)label.transform, new Vector2(0f, 0.35f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var detail = Label(canvas.transform, "Detail", null, "STAND TO RUN", 48f, Color.white, outline);
            Place((RectTransform)detail.transform, Vector2.zero, new Vector2(1f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Set(prompt, "root", canvas.gameObject);
            Set(prompt, "label", label);
            Set(prompt, "text", label.GetComponent<TextMeshProUGUI>());
            Set(prompt, "panel", panel);
            Set(prompt, "detail", detail);
            Set(prompt, "detailText", detail.GetComponent<TextMeshProUGUI>());
            SavePrefab(root, PromptPath);
        }

        static void BuildMarker()
        {
            var root = new GameObject("WindowMarker");
            var marker = root.AddComponent<WindowMarker>();
            var canvas = NinjagoWorldBuilder.WorldCanvas(root.transform, "Canvas", Vector3.zero, 0.0055f, new Vector2(220f, 220f));
            var ring = NewImage(canvas.transform, "Ring", GetSprite("Ring"), Color.white);
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            var arrow = NewRect("Arrow", canvas.transform);
            Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 140f));
            var arrowImage = NewImage(arrow, "Image", GetSprite("Arrow"), Color.white);
            Stretch(arrowImage.rectTransform);
            Set(marker, "ring", ring);
            Set(marker, "arrow", arrow);
            Set(marker, "arrowImage", arrowImage);
            SavePrefab(root, MarkerPath);
        }

        #endregion

        #region Ninja

        // Built in profile facing +X (the gait rig's frame) under a Profile pivot turned so the ninja faces the root's +Z.
        static void BuildNinja()
        {
            var root = new GameObject("ChapterNinja");
            var ninja = root.AddComponent<ChapterNinja>();
            var fightRig = root.AddComponent<NinjaRig>();
            var gaitRig = root.AddComponent<CrouchNinjaRig>();
            var bodyRoot = Empty("BodyRoot", root.transform, Vector3.zero);
            var profile = Empty("Profile", bodyRoot, Vector3.zero);
            profile.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var tint = Lit("Chapter Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var skin = Lit("Ninja Skin", new Color(1f, 0.84f, 0.25f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var tinted = new List<Object>();
            var frontLeg = Limb(profile, "FrontLeg", new Vector3(0f, 0.72f, -0.12f), new Vector3(0.22f, 0.72f, 0.22f), dark);
            var backLeg = Limb(profile, "BackLeg", new Vector3(0f, 0.72f, 0.12f), new Vector3(0.22f, 0.72f, 0.22f), dark);
            var body = Empty("Body", profile, new Vector3(0f, 0.72f, 0f));
            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.06f, 0f), new Vector3(0.32f, 0.16f, 0.5f), dark);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.17f, 0f), new Vector3(0.36f, 0.07f, 0.54f), gold);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.5f, 0.52f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Hood", body, new Vector3(0.02f, 0.93f, 0f), new Vector3(0.44f, 0.42f, 0.44f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "HoodKnot", body, new Vector3(-0.26f, 0.98f, 0f), new Vector3(0.12f, 0.14f, 0.14f), tint).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "FaceSlit", body, new Vector3(0.23f, 0.95f, 0f), new Vector3(0.03f, 0.1f, 0.34f), skin);
            tinted.Add(Block(PrimitiveType.Cube, "ScarfTail", body, new Vector3(-0.34f, 0.86f, 0.05f), new Vector3(0.34f, 0.08f, 0.12f), tint,
                new Vector3(0f, 0f, -20f)).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "Blade", body, new Vector3(-0.22f, 0.5f, 0.08f), new Vector3(0.04f, 0.85f, 0.05f), steel, new Vector3(0f, 0f, 35f));
            Block(PrimitiveType.Cube, "Hilt", body, new Vector3(0.02f, 0.88f, 0.08f), new Vector3(0.06f, 0.22f, 0.07f), gold, new Vector3(0f, 0f, 35f));
            var frontArm = Limb(body, "FrontArm", new Vector3(0f, 0.66f, -0.32f), new Vector3(0.15f, 0.48f, 0.15f), tint, skin);
            var backArm = Limb(body, "BackArm", new Vector3(0f, 0.66f, 0.32f), new Vector3(0.15f, 0.48f, 0.15f), tint, skin);
            tinted.Add(frontArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(backArm.GetChild(0).GetComponent<MeshRenderer>());
            var head = Empty("HeadPoint", body, new Vector3(0f, 1.05f, 0f));
            var center = Empty("Center", root.transform, new Vector3(0f, 1f, 0f));

            var tag = NinjagoWorldBuilder.BuildPlayerTagCanvas(root.transform, "Marker", new Vector3(0f, 2.2f, 0f), 0.009f, 64f, true);
            var ringCanvas = NinjagoWorldBuilder.WorldCanvas(root.transform, "SwirlRing", new Vector3(0f, 0.03f, 0f), 0.01f, new Vector2(300f, 300f));
            ringCanvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = NewImage(ringCanvas.transform, "Fill", GetSprite("Ring"), Color.white);
            Stretch(ring.rectTransform);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            var status = Prompt(root.transform, "StatusPrompt", new Vector3(0f, 2.75f, 0f));
            var hint = Prompt(root.transform, "HintPrompt", new Vector3(0f, 3.35f, 0f));

            Set(fightRig, "bodyRoot", bodyRoot);
            SetArray(fightRig, "colorRenderers", tinted);
            Set(fightRig, "marker", tag);
            Set(fightRig, "swirlRingRoot", ringCanvas.gameObject);
            Set(fightRig, "swirlRing", ring);
            Set(gaitRig, "body", body);
            Set(gaitRig, "frontLeg", frontLeg);
            Set(gaitRig, "backLeg", backLeg);
            Set(gaitRig, "frontArm", frontArm);
            Set(gaitRig, "backArm", backArm);
            SetArray(gaitRig, "colorRenderers", tinted);
            Set(gaitRig, "marker", tag);
            Set(gaitRig, "headPoint", head);
            Set(ninja, "fightRig", fightRig);
            Set(ninja, "gaitRig", gaitRig);
            Set(ninja, "status", status);
            Set(ninja, "hint", hint);
            Set(ninja, "center", center);
            Set(ninja, "marker", tag.transform.parent);
            SavePrefab(root, NinjaPath);
        }

        internal static WorldPrompt Prompt(Transform parent, string name, Vector3 position)
        {
            var prompt = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PromptPath), parent);
            prompt.name = name;
            prompt.transform.localPosition = position;
            return prompt.GetComponent<WorldPrompt>();
        }

        static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material material, Material? hand = null)
        {
            var limb = Empty(name, parent, pivot);
            Block(PrimitiveType.Cube, "Block", limb, new Vector3(0f, -size.y * 0.5f, 0f), size, material);
            if (hand != null) Block(PrimitiveType.Cube, "Hand", limb, new Vector3(0f, -size.y - 0.05f, 0f), Vector3.one * 0.14f, hand);
            return limb;
        }

        #endregion

        #region Enemies

        static void BuildEnemy(string path, bool boss)
        {
            var root = new GameObject(boss ? "Boss" : "BrickMonster");
            var enemy = root.AddComponent<ChapterEnemy>();
            var throwRig = root.AddComponent<BossRig>();
            var staffRig = root.AddComponent<BruteRig>();
            var throwBody = Empty("ThrowBody", root.transform, Vector3.zero);
            var staffBody = Empty("StaffBody", throwBody, Vector3.zero);
            var parts = BuildEnemyBody(staffBody, boss);

            var wood = Lit("Staff Wood", new Color(0.45f, 0.27f, 0.13f));
            var edge = Lit("Staff Edge", new Color(1f, 0.9f, 0.2f), 0.5f, new Color(2.2f, 1.6f, 0.2f));
            // The staff lies along the pivot's local -Z, reaching the ninja at yaw 0 from the attacker spot.
            var pivot = Empty("StaffPivot", staffBody, new Vector3(0f, 1.25f, -0.6f));
            pivot.localRotation = Quaternion.Euler(50f, 0f, 0f);
            var staff = Empty("Staff", pivot, Vector3.zero);
            Block(PrimitiveType.Cylinder, "Shaft", staff, new Vector3(0f, 0f, -2.2f), new Vector3(0.13f, 2.2f, 0.13f), wood, new Vector3(90f, 0f, 0f));
            Block(PrimitiveType.Cube, "Edge", staff, new Vector3(0f, 0.07f, -2.25f), new Vector3(0.17f, 0.05f, 4.2f), edge);
            Block(PrimitiveType.Cube, "CapFar", staff, new Vector3(0f, 0f, -4.4f), new Vector3(0.26f, 0.26f, 0.26f), edge);
            var tip = Empty("Tip", pivot, new Vector3(0f, 0f, -4.4f));
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = 0.35f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = Particle("Staff Trail", true);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;

            var ringMaterial = Lit("Chapter Owner Ring", Color.white, 0.2f, new Color(0.6f, 0.6f, 0.6f));
            var ring = Block(PrimitiveType.Cylinder, "OwnerRing", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(1.6f, 0.02f, 1.6f), ringMaterial);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var tag = NinjagoWorldBuilder.BuildPlayerTagCanvas(root.transform, "Marker", new Vector3(0f, 3f, 0f), 0.01f, 64f, false);
            tag.transform.parent.gameObject.SetActive(false);
            var center = Empty("Center", root.transform, new Vector3(0f, 1.3f, 0f));

            Set(throwRig, "bodyRoot", throwBody);
            Set(throwRig, "armPivot", parts.armPivot);
            Set(throwRig, "handPoint", parts.hand);
            Set(throwRig, "chestPoint", parts.chest);
            Set(throwRig, "marker", tag);
            Set(staffRig, "bodyRoot", staffBody);
            Set(staffRig, "staffPivot", pivot);
            Set(staffRig, "staffTrail", trail);
            Set(staffRig, "staffTip", tip);
            Set(enemy, "throwRig", throwRig);
            Set(enemy, "staffRig", staffRig);
            Set(enemy, "staff", staff.gameObject);
            Set(enemy, "ownerRing", ring.GetComponent<MeshRenderer>());
            SetArray(enemy, "bodyRenderers", parts.tinted);
            Set(enemy, "center", center);
            Set(enemy, "burstScale", property => property.floatValue = boss ? 3f : 1f);
            if (boss) root.transform.localScale = Vector3.one * 3f;
            SavePrefab(root, path);
        }

        public readonly struct EnemyParts
        {
            public readonly Transform armPivot;
            public readonly Transform hand;
            public readonly Transform chest;
            public readonly List<Object> tinted;

            public EnemyParts(Transform armPivot, Transform hand, Transform chest, List<Object> tinted)
            {
                this.armPivot = armPivot;
                this.hand = hand;
                this.chest = chest;
                this.tinted = tinted;
            }
        }

        /// <summary>
        /// A blocky brick body facing -Z: studs on top so the bricks are easy to count. The Boss adds horns, spiked
        /// shoulders and a cape so it reads from across the room.
        /// </summary>
        public static EnemyParts BuildEnemyBody(Transform parent, bool boss)
        {
            var tint = Lit("Chapter Monster Tint", Color.white);
            var dark = Lit("Chapter Monster Dark", new Color(0.2f, 0.2f, 0.24f));
            var eye = Lit("Chapter Monster Eye", new Color(1f, 0.85f, 0.2f), 0.4f, new Color(2.4f, 1.8f, 0.3f));
            var tinted = new List<Object>();
            Block(PrimitiveType.Cube, "LegL", parent, new Vector3(-0.3f, 0.35f, 0f), new Vector3(0.38f, 0.7f, 0.44f), dark);
            Block(PrimitiveType.Cube, "LegR", parent, new Vector3(0.3f, 0.35f, 0f), new Vector3(0.38f, 0.7f, 0.44f), dark);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", parent, new Vector3(0f, 1.2f, 0f), new Vector3(1.2f, 1f, 0.75f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Head", parent, new Vector3(0f, 2.0f, 0f), new Vector3(0.8f, 0.6f, 0.7f), tint).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                foreach (var z in new[] { -0.17f, 0.17f })
                {
                    tinted.Add(Block(PrimitiveType.Cylinder, "Stud", parent, new Vector3(x, 2.35f, z), new Vector3(0.22f, 0.06f, 0.22f), tint).GetComponent<MeshRenderer>());
                }
            }

            Block(PrimitiveType.Cube, "EyeL", parent, new Vector3(-0.18f, 2.05f, -0.36f), new Vector3(0.16f, 0.12f, 0.04f), eye);
            Block(PrimitiveType.Cube, "EyeR", parent, new Vector3(0.18f, 2.05f, -0.36f), new Vector3(0.16f, 0.12f, 0.04f), eye);
            Block(PrimitiveType.Cube, "Mouth", parent, new Vector3(0f, 1.85f, -0.36f), new Vector3(0.4f, 0.06f, 0.04f), dark);
            Block(PrimitiveType.Cube, "ArmL", parent, new Vector3(-0.78f, 1.25f, -0.1f), new Vector3(0.32f, 0.8f, 0.36f), tint);
            var armPivot = Empty("ArmPivot", parent, new Vector3(0.78f, 1.62f, 0f));
            tinted.Add(Block(PrimitiveType.Cube, "Arm", armPivot, new Vector3(0f, -0.4f, 0f), new Vector3(0.32f, 0.8f, 0.36f), tint).GetComponent<MeshRenderer>());
            var hand = Empty("HandPoint", armPivot, new Vector3(0f, -0.9f, 0f));
            Block(PrimitiveType.Cube, "Fist", armPivot, new Vector3(0f, -0.88f, 0f), new Vector3(0.36f, 0.3f, 0.36f), dark);
            var chest = Empty("ChestPoint", parent, new Vector3(0f, 1.3f, -0.5f));
            if (boss)
            {
                var horn = Lit("Chapter Boss Horn", new Color(0.95f, 0.92f, 0.85f), 0.3f);
                var cape = Lit("Chapter Boss Cape", new Color(0.55f, 0.12f, 0.6f));
                Block(PrimitiveType.Cube, "HornL", parent, new Vector3(-0.42f, 2.55f, 0f), new Vector3(0.14f, 0.6f, 0.14f), horn, new Vector3(0f, 0f, 25f));
                Block(PrimitiveType.Cube, "HornR", parent, new Vector3(0.42f, 2.55f, 0f), new Vector3(0.14f, 0.6f, 0.14f), horn, new Vector3(0f, 0f, -25f));
                Block(PrimitiveType.Cube, "SpikeL", parent, new Vector3(-0.8f, 1.85f, 0f), new Vector3(0.3f, 0.5f, 0.3f), horn, new Vector3(0f, 0f, 35f));
                Block(PrimitiveType.Cube, "SpikeR", parent, new Vector3(0.8f, 1.85f, 0f), new Vector3(0.3f, 0.5f, 0.3f), horn, new Vector3(0f, 0f, -35f));
                Block(PrimitiveType.Cube, "Cape", parent, new Vector3(0f, 1.1f, 0.45f), new Vector3(1.4f, 1.6f, 0.08f), cape, new Vector3(-8f, 0f, 0f));
            }

            return new EnemyParts(armPivot, hand, chest, tinted);
        }

        #endregion

        #region Rock

        static void BuildRock()
        {
            var stone = Lit("Chapter Rock", new Color(0.62f, 0.55f, 0.48f));
            var root = new GameObject("ChapterRock");
            Block(PrimitiveType.Cube, "Core", root.transform, Vector3.zero, new Vector3(0.9f, 0.8f, 0.85f), stone, new Vector3(12f, 25f, 8f));
            Block(PrimitiveType.Cube, "Chunk", root.transform, new Vector3(0.2f, 0.2f, 0.1f), new Vector3(0.6f, 0.55f, 0.6f), stone, new Vector3(40f, 10f, 30f));
            SavePrefab(root, RockPath);
        }

        static void BuildStones()
        {
            var root = new GameObject("ChapterStones");
            var pieces = root.AddComponent<RockPieces>();
            Set(pieces, "piecePrefab", Load<GameObject>(PrefabsRoot + "/StoneKick/RockPiece.prefab").transform);
            SavePrefab(root, StonesPath);
        }

        #endregion
    }
}
