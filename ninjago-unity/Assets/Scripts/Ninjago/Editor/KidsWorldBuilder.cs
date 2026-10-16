#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// 3D pieces shared by the children's games (Guard, Call, Build), all from primitives: the block ninja seen from
    /// behind, a goblin, toy bricks, the still camera and courtyard props.
    /// </summary>
    public static class KidsWorldBuilder
    {
        public const string Root = PrefabsRoot + "/Kids";
        public static readonly Color SkyColor = new(0.5f, 0.82f, 1f);

        #region Ninja

        // Facing +Z: the scarf tails and the sword on the back read the facing from the camera behind it.
        public static KidNinja BuildNinja(Transform parent, string name)
        {
            var ninjaRoot = Empty(name, parent, Vector3.zero);
            var rig = ninjaRoot.gameObject.AddComponent<KidNinja>();
            var tint = Lit("Punch Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var glove = Lit("Punch Glove", new Color(0.98f, 0.98f, 0.95f), 0.35f);
            var tinted = new List<Object>();
            var leftLeg = Limb(ninjaRoot, "LeftLeg", new Vector3(-0.14f, 0.84f, 0f), new Vector3(0.22f, 0.84f, 0.26f), dark);
            var rightLeg = Limb(ninjaRoot, "RightLeg", new Vector3(0.14f, 0.84f, 0f), new Vector3(0.22f, 0.84f, 0.26f), dark);
            var body = Empty("Body", ninjaRoot, new Vector3(0f, 0.84f, 0f));
            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.16f, 0.3f), dark);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.17f, 0f), new Vector3(0.54f, 0.07f, 0.34f), gold);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.46f, 0f), new Vector3(0.56f, 0.52f, 0.32f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Hood", body, new Vector3(0f, 0.95f, 0f), new Vector3(0.44f, 0.42f, 0.42f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Knot", body, new Vector3(0f, 0.96f, -0.24f), new Vector3(0.14f, 0.12f, 0.08f), tint).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.07f, 0.07f })
            {
                tinted.Add(Block(PrimitiveType.Cube, "ScarfTail", body, new Vector3(x, 0.72f, -0.26f), new Vector3(0.09f, 0.42f, 0.04f), tint,
                    new Vector3(-12f, 0f, x * 120f)).GetComponent<MeshRenderer>());
            }

            Block(PrimitiveType.Cube, "Blade", body, new Vector3(0.02f, 0.5f, -0.2f), new Vector3(0.05f, 0.85f, 0.04f), steel, new Vector3(0f, 0f, 38f));
            Block(PrimitiveType.Cube, "Hilt", body, new Vector3(-0.2f, 0.85f, -0.2f), new Vector3(0.07f, 0.22f, 0.06f), gold, new Vector3(0f, 0f, 38f));
            var leftArm = Arm(body, "LeftArm", -0.36f, tint, glove);
            var rightArm = Arm(body, "RightArm", 0.36f, tint, glove);
            tinted.Add(leftArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(rightArm.GetChild(0).GetComponent<MeshRenderer>());
            var head = Empty("HeadPoint", body, new Vector3(0f, 1.17f, 0f));
            var front = Empty("FrontPoint", body, new Vector3(0f, 0.9f, 0.5f));
            var marker = NinjagoWorldBuilder.BuildPlayerTagCanvas(ninjaRoot, "Marker", new Vector3(0f, 2.3f, 0f), 0.006f, 64f, true);
            Set(rig, "body", body);
            Set(rig, "leftArm", leftArm);
            Set(rig, "rightArm", rightArm);
            Set(rig, "leftLeg", leftLeg);
            Set(rig, "rightLeg", rightLeg);
            SetArray(rig, "colorRenderers", tinted);
            Set(rig, "marker", marker);
            Set(rig, "headPoint", head);
            Set(rig, "frontPoint", front);
            return rig;
        }

        // A shoulder pivot with the sleeve hanging along -Y and a big glove at its end.
        static Transform Arm(Transform body, string name, float x, Material sleeve, Material glove)
        {
            var arm = Empty(name, body, new Vector3(x, 0.66f, 0f));
            Block(PrimitiveType.Cube, "Sleeve", arm, new Vector3(0f, -0.26f, 0f), new Vector3(0.15f, 0.52f, 0.16f), sleeve);
            Block(PrimitiveType.Cube, "Glove", arm, new Vector3(0f, -0.6f, 0f), new Vector3(0.22f, 0.2f, 0.22f), glove);
            return arm;
        }

        /// <summary>A pivot with the block hanging along -Y.</summary>
        public static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material material)
        {
            var limb = Empty(name, parent, pivot);
            Block(PrimitiveType.Cube, "Block", limb, new Vector3(0f, -size.y * 0.5f, 0f), size, material);
            return limb;
        }

        #endregion

        #region Goblin

        // A round-bellied goblin on the street monster rig: big ears and eyes, a top point for the next one of a pile; face on +Z.
        public static StreetMonster BuildGoblin(string path)
        {
            var root = new GameObject("Goblin");
            var monster = root.AddComponent<StreetMonster>();
            var tint = Lit("Punch Monster Tint", Color.white, 0.2f);
            var dark = Lit("Crouch Monster Dark", new Color(0.22f, 0.2f, 0.24f));
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var pupil = Lit("Mecha Eye Pupil", new Color(0.05f, 0.05f, 0.05f));
            var tinted = new List<Object>();
            var leftLeg = Limb(root.transform, "LeftLeg", new Vector3(-0.2f, 0.5f, 0f), new Vector3(0.26f, 0.5f, 0.3f), dark);
            var rightLeg = Limb(root.transform, "RightLeg", new Vector3(0.2f, 0.5f, 0f), new Vector3(0.26f, 0.5f, 0.3f), dark);
            var body = Empty("Body", root.transform, Vector3.zero);
            tinted.Add(Block(PrimitiveType.Cube, "Belly", body, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.7f, 0.6f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.5f, 0.55f), tint).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                tinted.Add(Block(PrimitiveType.Cube, "Ear", body, new Vector3(x, 1.55f, 0f), new Vector3(0.28f, 0.12f, 0.12f), tint,
                    new Vector3(0f, 0f, x * 50f)).GetComponent<MeshRenderer>());
                Block(PrimitiveType.Cube, "Eye", body, new Vector3(x * 0.36f, 1.5f, 0.28f), new Vector3(0.16f, 0.16f, 0.03f), white);
                Block(PrimitiveType.Cube, "Pupil", body, new Vector3(x * 0.36f, 1.48f, 0.3f), new Vector3(0.07f, 0.07f, 0.03f), pupil);
            }

            Block(PrimitiveType.Cube, "Grin", body, new Vector3(0f, 1.32f, 0.28f), new Vector3(0.3f, 0.06f, 0.03f), dark);
            var leftArm = Limb(body, "LeftArm", new Vector3(-0.5f, 1.1f, 0f), new Vector3(0.18f, 0.5f, 0.2f), tint);
            var rightArm = Limb(body, "RightArm", new Vector3(0.5f, 1.1f, 0f), new Vector3(0.18f, 0.5f, 0.2f), tint);
            tinted.Add(leftArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(rightArm.GetChild(0).GetComponent<MeshRenderer>());
            Set(monster, "top", Empty("Top", body, new Vector3(0f, 1.7f, 0f)));
            Set(monster, "body", body);
            Set(monster, "leftLeg", leftLeg);
            Set(monster, "rightLeg", rightLeg);
            Set(monster, "leftArm", leftArm);
            Set(monster, "rightArm", rightArm);
            Set(monster, "centerOffset", property => property.vector3Value = new Vector3(0f, 0.9f, 0f));
            SetArray(monster, "tintRenderers", tinted);
            return SavePrefab(root, path).GetComponent<StreetMonster>();
        }

        #endregion

        #region Props

        /// <summary>A 2x4 toy brick around its center, studs on top, longer along X.</summary>
        public static GameObject Brick(Transform parent, string name, Vector3 position, float scale, Material material, List<Object>? renderers = null)
        {
            var brick = Empty(name, parent, position);
            brick.localScale = Vector3.one * scale;
            var block = Block(PrimitiveType.Cube, "Block", brick, Vector3.zero, new Vector3(0.8f, 0.4f, 0.4f), material);
            renderers?.Add(block.GetComponent<MeshRenderer>());
            for (var x = 0; x < 4; x++)
            {
                for (var z = 0; z < 2; z++)
                {
                    var stud = Block(PrimitiveType.Cylinder, "Stud", brick, new Vector3(-0.3f + x * 0.2f, 0.23f, -0.1f + z * 0.2f), new Vector3(0.12f, 0.03f, 0.12f), material);
                    renderers?.Add(stud.GetComponent<MeshRenderer>());
                }
            }

            return brick.gameObject;
        }

        public static Camera Camera(Transform root, Vector3 position, float pitch, float fieldOfView)
        {
            var camera = NinjagoWorldBuilder.NewCamera(root, position, new Vector3(pitch, 0f, 0f), fieldOfView);
            camera.backgroundColor = SkyColor;
            camera.farClipPlane = 120f;
            return camera;
        }

        public static void Tree(Transform parent, Vector3 position, float scale)
        {
            var tree = Empty("Tree", parent, position);
            tree.localScale = Vector3.one * scale;
            Block(PrimitiveType.Cube, "Trunk", tree, new Vector3(0f, 1.1f, 0f), new Vector3(0.5f, 2.2f, 0.5f), Lit("Mecha Tree Trunk", new Color(0.45f, 0.28f, 0.15f)));
            Block(PrimitiveType.Cube, "Leaves", tree, new Vector3(0f, 3f, 0f), new Vector3(2.4f, 2f, 2.4f), Lit("Mecha Tree Leaves", new Color(0.12f, 0.62f, 0.25f)),
                new Vector3(0f, 20f, 0f));
        }

        public static void Lantern(Transform parent, Vector3 position)
        {
            var lantern = Empty("Lantern", parent, position);
            Block(PrimitiveType.Cube, "Post", lantern, new Vector3(0f, 0.9f, 0f), new Vector3(0.14f, 1.8f, 0.14f), Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f)));
            Block(PrimitiveType.Cube, "Light", lantern, new Vector3(0f, 2f, 0f), new Vector3(0.5f, 0.55f, 0.5f),
                Lit("Kids Lantern", new Color(1f, 0.45f, 0.3f), 0.3f, new Color(0.5f, 0.15f, 0.05f)));
        }

        public static void House(Transform parent, Vector3 position, float yaw, Material wall)
        {
            var house = Empty("House", parent, position);
            house.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Block(PrimitiveType.Cube, "Walls", house, new Vector3(0f, 1.6f, 0f), new Vector3(4.5f, 3.2f, 4f), wall);
            Block(PrimitiveType.Cube, "Roof", house, new Vector3(0f, 3.5f, 0f), new Vector3(5.4f, 0.6f, 4.8f), Lit("Courtyard Roof", new Color(0.12f, 0.62f, 0.5f)));
            Block(PrimitiveType.Cube, "Door", house, new Vector3(0f, 0.9f, -2.01f), new Vector3(1f, 1.8f, 0.04f), Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f)));
        }

        #endregion
    }
}
