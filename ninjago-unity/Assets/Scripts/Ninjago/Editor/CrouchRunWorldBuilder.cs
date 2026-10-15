#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The Crouch Run 3D prefabs, all from primitives: the lane with its profile ninja and side camera, and the level pieces.</summary>
    public static class CrouchRunWorldBuilder
    {
        public const string LanePath = PrefabsRoot + "/CrouchRun/CrouchLane.prefab";
        const string Root = PrefabsRoot + "/CrouchRun";
        static readonly Color skyColor = new(0.5f, 0.8f, 1f);

        #region Entry Point

        public static void Build()
        {
            var path = BuildPathPiece();
            var monster = BuildMonster();
            var gate = BuildGate();
            var ground = BuildGround();
            var hedge = BuildHedge();
            var tree = BuildTree();
            var house = BuildHouse();
            var hill = BuildHill();
            BuildLane(path, monster, gate, ground, hedge, tree, house, hill);
        }

        #endregion

        #region Lane

        static void BuildLane(Renderer path, CrouchMonster monster, Transform gate, Transform ground, Transform hedge, Transform tree, Transform house,
            Transform hill)
        {
            var root = new GameObject("CrouchLane");
            var lane = root.AddComponent<CrouchLane>();
            var ninja = BuildNinja(root.transform);
            var levelHost = Empty("Level", root.transform, Vector3.zero);
            var builder = levelHost.gameObject.AddComponent<CrouchLevelBuilder>();
            var pieces = Empty("Pieces", levelHost, Vector3.zero);
            Set(builder, "levelRoot", pieces);
            Set(builder, "pathPiecePrefab", path);
            Set(builder, "monsterPrefab", monster);
            Set(builder, "gatePrefab", gate);
            Set(builder, "groundPrefab", ground);
            Set(builder, "hedgePrefab", hedge);
            Set(builder, "treePrefab", tree);
            Set(builder, "housePrefab", house);
            Set(builder, "hillPrefab", hill);
            // Locked to the side: looks along +Z at the path, level, so the path sits low and the monsters have room above; CrouchLane moves it along X with the ninja.
            var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(4f, 2.2f, -11.5f), Vector3.zero, 34f);
            camera.backgroundColor = skyColor;
            camera.farClipPlane = 120f;
            Set(lane, "ninja", ninja);
            Set(lane, "levelBuilder", builder);
            Set(lane, "laneCamera", camera);
            SavePrefab(root, LanePath);
        }

        // In profile facing +X: the face slit and the sword hilt read the facing; the scarf tail trails behind.
        static CrouchNinjaRig BuildNinja(Transform root)
        {
            var ninjaRoot = Empty("Ninja", root, Vector3.zero);
            var rig = ninjaRoot.gameObject.AddComponent<CrouchNinjaRig>();
            var tint = Lit("Crouch Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var skin = Lit("Ninja Skin", new Color(1f, 0.84f, 0.25f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var tinted = new List<Object>();
            var frontLeg = Limb(ninjaRoot, "FrontLeg", new Vector3(0f, 0.72f, -0.11f), new Vector3(0.2f, 0.72f, 0.2f), dark);
            var backLeg = Limb(ninjaRoot, "BackLeg", new Vector3(0f, 0.72f, 0.11f), new Vector3(0.2f, 0.72f, 0.2f), dark);
            var body = Empty("Body", ninjaRoot, new Vector3(0f, 0.72f, 0f));
            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.16f, 0.42f), dark);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.07f, 0.46f), gold);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.45f, 0f), new Vector3(0.32f, 0.5f, 0.44f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Hood", body, new Vector3(0.02f, 0.92f, 0f), new Vector3(0.42f, 0.4f, 0.4f), tint).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "FaceSlit", body, new Vector3(0.22f, 0.94f, 0f), new Vector3(0.03f, 0.1f, 0.3f), skin);
            tinted.Add(Block(PrimitiveType.Cube, "ScarfTail", body, new Vector3(-0.32f, 0.86f, 0f), new Vector3(0.32f, 0.08f, 0.12f), tint,
                new Vector3(0f, 0f, -20f)).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "Blade", body, new Vector3(-0.2f, 0.5f, 0f), new Vector3(0.04f, 0.8f, 0.05f), steel, new Vector3(0f, 0f, 35f));
            Block(PrimitiveType.Cube, "Hilt", body, new Vector3(0.02f, 0.86f, 0f), new Vector3(0.06f, 0.22f, 0.07f), gold, new Vector3(0f, 0f, 35f));
            var frontArm = Limb(body, "FrontArm", new Vector3(0f, 0.64f, -0.26f), new Vector3(0.14f, 0.48f, 0.14f), tint, skin);
            var backArm = Limb(body, "BackArm", new Vector3(0f, 0.64f, 0.26f), new Vector3(0.14f, 0.48f, 0.14f), tint, skin);
            tinted.Add(frontArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(backArm.GetChild(0).GetComponent<MeshRenderer>());
            var head = Empty("HeadPoint", body, new Vector3(0f, 1.05f, 0f));
            var marker = NinjagoWorldBuilder.BuildPlayerTagCanvas(ninjaRoot, "Marker", new Vector3(0f, 2.25f, 0f), 0.011f, 64f, true);
            Set(rig, "body", body);
            Set(rig, "frontLeg", frontLeg);
            Set(rig, "backLeg", backLeg);
            Set(rig, "frontArm", frontArm);
            Set(rig, "backArm", backArm);
            SetArray(rig, "colorRenderers", tinted);
            Set(rig, "marker", marker);
            Set(rig, "headPoint", head);
            return rig;
        }

        // A pivot with the block hanging along -Y (and an optional hand at its end).
        static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material material, Material? hand = null)
        {
            var limb = Empty(name, parent, pivot);
            Block(PrimitiveType.Cube, "Block", limb, new Vector3(0f, -size.y * 0.5f, 0f), size, material);
            if (hand != null) Block(PrimitiveType.Cube, "Hand", limb, new Vector3(0f, -size.y - 0.05f, 0f), Vector3.one * 0.13f, hand);
            return limb;
        }

        #endregion

        #region Level Pieces

        static Renderer BuildPathPiece()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PathPiece";
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.GetComponent<MeshRenderer>().sharedMaterial = Lit("Crouch Path", Color.white, 0.1f);
            return SavePrefab(root, $"{Root}/PathPiece.prefab").GetComponent<Renderer>();
        }

        // A blocky person, bigger than the ninja, nodding as it talks; the bubble above repeats the banner.
        static CrouchMonster BuildMonster()
        {
            var root = new GameObject("CrouchMonster");
            var monster = root.AddComponent<CrouchMonster>();
            var bodyRoot = Empty("Body", root.transform, Vector3.zero);
            var tint = Lit("Crouch Monster Tint", Color.white, 0.2f);
            var dark = Lit("Crouch Monster Dark", new Color(0.22f, 0.2f, 0.24f));
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var pupil = Lit("Mecha Eye Pupil", new Color(0.05f, 0.05f, 0.05f));
            var tinted = new List<Object>();
            foreach (var x in new[] { -0.25f, 0.25f })
            {
                Block(PrimitiveType.Cube, "Leg", bodyRoot, new Vector3(x, 0.45f, 0f), new Vector3(0.36f, 0.9f, 0.4f), dark);
                tinted.Add(Block(PrimitiveType.Cube, "Arm", bodyRoot, new Vector3(x * 2.9f, 1.4f, 0.05f), new Vector3(0.26f, 0.8f, 0.3f), tint,
                    new Vector3(-15f, 0f, 0f)).GetComponent<MeshRenderer>());
            }

            tinted.Add(Block(PrimitiveType.Cube, "Torso", bodyRoot, new Vector3(0f, 1.4f, 0f), new Vector3(1f, 1f, 0.6f), tint).GetComponent<MeshRenderer>());
            var head = Empty("Head", bodyRoot, new Vector3(0f, 2.1f, 0f));
            tinted.Add(Block(PrimitiveType.Cube, "Skull", head, new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.55f, 0.6f), tint).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.15f, 0.15f })
            {
                Block(PrimitiveType.Cube, "Eye", head, new Vector3(x, 0.32f, 0.31f), new Vector3(0.14f, 0.14f, 0.03f), white);
                Block(PrimitiveType.Cube, "Pupil", head, new Vector3(x, 0.3f, 0.33f), new Vector3(0.07f, 0.07f, 0.03f), pupil);
            }

            Block(PrimitiveType.Cube, "Mouth", head, new Vector3(0f, 0.12f, 0.31f), new Vector3(0.3f, 0.08f, 0.03f), dark);
            var canvas = NinjagoWorldBuilder.WorldCanvas(root.transform, "Bubble", new Vector3(0f, 3.25f, 0f), 0.009f, new Vector2(320f, 150f));
            var bubble = NewImage(canvas.transform, "Background", GetSprite("Panel"), Color.white);
            bubble.type = Image.Type.Sliced;
            Stretch(bubble.rectTransform);
            var outline = OutlineTextMaterial();
            var title = Label(canvas.transform, "Title", "ninjago.crouch.talking", "TALKING", 64f, Color.white, outline);
            Place((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(320f, 74f));
            var instruction = Label(canvas.transform, "Instruction", "ninjago.crouch.crouch", "CROUCH", 44f, Color.white, outline);
            Place((RectTransform)instruction.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f),
                new Vector2(320f, 56f));
            Set(monster, "body", bodyRoot);
            Set(monster, "head", head);
            SetArray(monster, "tintRenderers", tinted);
            Set(monster, "bubble", bubble);
            Set(monster, "bubbleTitle", title);
            Set(monster, "bubbleInstruction", instruction);
            return SavePrefab(root, $"{Root}/CrouchMonster.prefab").GetComponent<CrouchMonster>();
        }

        // The checkpoint: a red torii over the path.
        static Transform BuildGate()
        {
            var root = new GameObject("Gate");
            var red = Lit("Crouch Gate Red", new Color(0.92f, 0.18f, 0.15f), 0.3f);
            var black = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            foreach (var z in new[] { -1.7f, 1.7f })
            {
                Block(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 1.6f, z), new Vector3(0.32f, 1.6f, 0.32f), red);
            }

            Block(PrimitiveType.Cube, "Top", root.transform, new Vector3(0f, 3.3f, 0f), new Vector3(0.4f, 0.28f, 4.6f), black);
            Block(PrimitiveType.Cube, "Beam", root.transform, new Vector3(0f, 2.8f, 0f), new Vector3(0.3f, 0.22f, 4f), red);
            return SavePrefab(root, $"{Root}/Gate.prefab").transform;
        }

        // Grass from in front of the path back to the hills, just below the raised path.
        static Transform BuildGround()
        {
            var root = new GameObject("Ground");
            Block(PrimitiveType.Cube, "Grass", root.transform, new Vector3(0f, -0.6f, 30f), new Vector3(1f, 1f, 100f), Lit("Crouch Grass", new Color(0.45f, 0.8f, 0.32f)));
            return SavePrefab(root, $"{Root}/Ground.prefab").transform;
        }

        static Transform BuildHedge()
        {
            var root = new GameObject("Hedge");
            Block(PrimitiveType.Cube, "Bush", root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0.8f), Lit("Crouch Hedge", new Color(0.18f, 0.6f, 0.25f)));
            return SavePrefab(root, $"{Root}/Hedge.prefab").transform;
        }

        static Transform BuildTree()
        {
            var root = new GameObject("Tree");
            Block(PrimitiveType.Cube, "Trunk", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.5f, 2.2f, 0.5f), Lit("Mecha Tree Trunk", new Color(0.45f, 0.28f, 0.15f)));
            Block(PrimitiveType.Cube, "Leaves", root.transform, new Vector3(0f, 3f, 0f), new Vector3(2.4f, 2f, 2.4f), Lit("Mecha Tree Leaves", new Color(0.12f, 0.62f, 0.25f)),
                new Vector3(0f, 20f, 0f));
            return SavePrefab(root, $"{Root}/Tree.prefab").transform;
        }

        static Transform BuildHouse()
        {
            var root = new GameObject("House");
            Block(PrimitiveType.Cube, "Walls", root.transform, new Vector3(0f, 1.4f, 0f), new Vector3(4f, 2.8f, 3f), Lit("Crouch House Wall", new Color(0.98f, 0.9f, 0.72f)));
            Block(PrimitiveType.Cube, "Roof", root.transform, new Vector3(0f, 3.1f, 0f), new Vector3(4.8f, 0.5f, 3.6f), Lit("Courtyard Roof", new Color(0.12f, 0.62f, 0.5f)));
            Block(PrimitiveType.Cube, "Door", root.transform, new Vector3(0f, 0.8f, -1.51f), new Vector3(0.9f, 1.6f, 0.04f), Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f)));
            return SavePrefab(root, $"{Root}/House.prefab").transform;
        }

        static Transform BuildHill()
        {
            var root = new GameObject("Hill");
            Block(PrimitiveType.Sphere, "Mound", root.transform, new Vector3(0f, -2f, 0f), new Vector3(18f, 12f, 10f), Lit("Mecha Hill", new Color(0.3f, 0.7f, 0.35f)));
            return SavePrefab(root, $"{Root}/Hill.prefab").transform;
        }

        #endregion
    }
}
