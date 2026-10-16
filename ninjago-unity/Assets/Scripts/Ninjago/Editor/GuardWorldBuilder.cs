#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Guard's courtyard, all from primitives: grass and a stone path, a low stone wall with goblins on it, the little
    /// friend, two ninjas with a big brick shield each, and the thrown brick.
    /// </summary>
    public static class GuardWorldBuilder
    {
        public const string ScenePath = KidsWorldBuilder.Root + "/GuardScene.prefab";
        const string BrickPath = KidsWorldBuilder.Root + "/GuardBrick.prefab";
        const float WallZ = 10f;
        const float WallHeight = 1.2f;

        #region Entry Point

        public static void Build()
        {
            var brick = BuildBrick();
            var root = new GameObject("GuardScene");
            var scene = root.AddComponent<GuardScene>();
            BuildCourtyard(root.transform);
            var camera = KidsWorldBuilder.Camera(root.transform, new Vector3(0f, 3f, -6.2f), 9f, 52f);
            var ninjas = new List<Object>();
            var shields = new List<Object>();
            foreach (var name in new[] { "NinjaLeft", "NinjaRight" })
            {
                var ninja = KidsWorldBuilder.BuildNinja(root.transform, name);
                ninjas.Add(ninja);
                shields.Add(BuildShield(ninja.transform.Find("Body/FrontPoint")));
            }

            var throwers = new List<Object>
            {
                BuildThrower(root.transform, "ThrowerLeft", new Vector3(-2.6f, WallHeight, WallZ + 0.4f), 1f, new Color(0.45f, 0.85f, 0.2f)),
                BuildThrower(root.transform, "ThrowerRight", new Vector3(2.6f, WallHeight, WallZ + 0.4f), 1f, new Color(0.6f, 0.3f, 0.95f)),
            };
            var big = BuildThrower(root.transform, "BigThrower", new Vector3(0f, WallHeight, WallZ + 1.6f), 1.5f, new Color(1f, 0.55f, 0.1f));
            var pool = Empty("Bricks", root.transform, Vector3.zero).gameObject.AddComponent<GuardBrickPool>();
            Set(scene, "sceneCamera", camera);
            SetArray(scene, "ninjas", ninjas);
            SetArray(scene, "shields", shields);
            Set(scene, "friend", BuildFriend(root.transform));
            SetArray(scene, "throwers", throwers);
            Set(scene, "bigThrower", big);
            Set(scene, "bigTarget", Empty("BigTarget", root.transform, new Vector3(0f, 1.7f, 0.5f)));
            Set(scene, "brickPool", pool);
            Set(scene, "brickPrefab", brick);
            Set(scene, "brickColors", colors =>
            {
                var palette = new[]
                {
                    new Color(0.95f, 0.2f, 0.2f), new Color(0.15f, 0.5f, 1f), new Color(0.2f, 0.75f, 0.3f), new Color(1f, 0.55f, 0.1f), new Color(0.75f, 0.3f, 0.9f),
                };
                colors.arraySize = palette.Length;
                for (var i = 0; i < palette.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = palette[i];
            });
            SetLocalized(scene, "statusText", "ninjago.guard.status");
            SetLocalized(scene, "introBanner", "ninjago.guard.intro");
            SetLocalized(scene, "hintText", "ninjago.guard.hint");
            SetLocalized(scene, "blockPrompt", "ninjago.guard.block");
            SetLocalized(scene, "teamPrompt", "ninjago.guard.team_block");
            SetLocalized(scene, "teamBanner", "ninjago.guard.team_banner");
            SetLocalized(scene, "endBanner", "ninjago.guard.end");
            SetLocalized(scene, "countText", "ninjago.guard.count");
            SavePrefab(root, ScenePath);
        }

        #endregion

        #region Courtyard

        static void BuildCourtyard(Transform root)
        {
            var grass = Lit("Crouch Grass", new Color(0.45f, 0.8f, 0.32f));
            var stone = Lit("Guard Stone", new Color(0.72f, 0.7f, 0.66f));
            var wallTop = Lit("Guard Wall Top", new Color(0.85f, 0.4f, 0.3f));
            Block(PrimitiveType.Cube, "Grass", root, new Vector3(0f, -0.25f, 14f), new Vector3(40f, 0.5f, 44f), grass);
            Block(PrimitiveType.Cube, "Path", root, new Vector3(0f, 0.01f, 4f), new Vector3(5.5f, 0.02f, 14f), Lit("Crouch Floor", new Color(0.86f, 0.6f, 0.34f), 0.2f));
            Block(PrimitiveType.Cube, "Wall", root, new Vector3(0f, WallHeight * 0.5f, WallZ + 1f), new Vector3(12f, WallHeight, 3.2f), stone);
            Block(PrimitiveType.Cube, "WallTop", root, new Vector3(0f, WallHeight + 0.05f, WallZ + 1f), new Vector3(12.4f, 0.1f, 3.4f), wallTop);
            KidsWorldBuilder.House(root, new Vector3(-7.5f, 0f, 3f), 90f, Lit("Crouch House Wall", new Color(0.98f, 0.9f, 0.72f)));
            KidsWorldBuilder.House(root, new Vector3(7.5f, 0f, 4f), -90f, Lit("Punch Wall Yellow", new Color(1f, 0.82f, 0.25f)));
            foreach (var x in new[] { -3.4f, 3.4f })
            {
                KidsWorldBuilder.Lantern(root, new Vector3(x, 0f, 2.5f));
            }

            KidsWorldBuilder.Tree(root, new Vector3(-6f, 0f, 15f), 1.3f);
            KidsWorldBuilder.Tree(root, new Vector3(6.5f, 0f, 16f), 1.5f);
            KidsWorldBuilder.Tree(root, new Vector3(-1.5f, 0f, 20f), 1.8f);
            KidsWorldBuilder.Tree(root, new Vector3(3f, 0f, 22f), 1.6f);
        }

        #endregion

        #region Pieces

        // A big yellow toy brick held up in front of the face, its top clearing the hood so the camera behind sees it;
        // GuardScene grows it while the guard is fresh.
        static Transform BuildShield(Transform frontPoint)
        {
            var shield = Empty("Shield", frontPoint, new Vector3(0f, 0.32f, 0f));
            var yellow = Lit("Guard Shield", new Color(1f, 0.82f, 0.12f), 0.45f, new Color(0.25f, 0.18f, 0f));
            var brick = KidsWorldBuilder.Brick(shield, "Brick", Vector3.zero, 1.7f, yellow);
            brick.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            return shield;
        }

        static GuardBrick BuildBrick()
        {
            var root = new GameObject("GuardBrick");
            var brick = root.AddComponent<GuardBrick>();
            var renderers = new List<Object>();
            KidsWorldBuilder.Brick(root.transform, "Brick", Vector3.zero, 1f, Lit("Guard Brick Tint", Color.white, 0.4f), renderers);
            SetArray(brick, "tintRenderers", renderers);
            return SavePrefab(root, BrickPath).GetComponent<GuardBrick>();
        }

        // A goblin standing on the wall, facing the ninjas; the held brick waits over its head.
        static GuardThrower BuildThrower(Transform parent, string name, Vector3 position, float scale, Color color)
        {
            var root = Empty(name, parent, position);
            root.localScale = Vector3.one * scale;
            root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var thrower = root.gameObject.AddComponent<GuardThrower>();
            var skin = Lit($"Guard Goblin {name}", color, 0.2f);
            var dark = Lit("Crouch Monster Dark", new Color(0.22f, 0.2f, 0.24f));
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var pupil = Lit("Mecha Eye Pupil", new Color(0.05f, 0.05f, 0.05f));
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                Block(PrimitiveType.Cube, "Leg", root, new Vector3(x, 0.25f, 0f), new Vector3(0.26f, 0.5f, 0.3f), dark);
            }

            var body = Empty("Body", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Belly", body, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.7f, 0.6f), skin);
            Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.5f, 0.55f), skin);
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Block(PrimitiveType.Cube, "Ear", body, new Vector3(x, 1.55f, 0f), new Vector3(0.28f, 0.12f, 0.12f), skin, new Vector3(0f, 0f, x * 50f));
                Block(PrimitiveType.Cube, "Eye", body, new Vector3(x * 0.36f, 1.5f, 0.28f), new Vector3(0.16f, 0.16f, 0.03f), white);
                Block(PrimitiveType.Cube, "Pupil", body, new Vector3(x * 0.36f, 1.48f, 0.3f), new Vector3(0.07f, 0.07f, 0.03f), pupil);
            }

            Block(PrimitiveType.Cube, "Grin", body, new Vector3(0f, 1.32f, 0.28f), new Vector3(0.3f, 0.06f, 0.03f), dark);
            var leftArm = KidsWorldBuilder.Limb(body, "LeftArm", new Vector3(-0.5f, 1.1f, 0f), new Vector3(0.18f, 0.5f, 0.2f), skin);
            var rightArm = KidsWorldBuilder.Limb(body, "RightArm", new Vector3(0.5f, 1.1f, 0f), new Vector3(0.18f, 0.5f, 0.2f), skin);
            var held = KidsWorldBuilder.Brick(body, "HeldBrick", new Vector3(0f, 2.05f, 0.1f), 1f, Lit("Guard Brick Held", new Color(0.95f, 0.2f, 0.2f), 0.4f));
            held.SetActive(false);
            Set(thrower, "body", body);
            Set(thrower, "leftArm", leftArm);
            Set(thrower, "rightArm", rightArm);
            Set(thrower, "heldBrick", held);
            Set(thrower, "throwPoint", Empty("ThrowPoint", body, new Vector3(0f, 2.05f, 0.1f)));
            return thrower;
        }

        // A small kid in a red cap with a blue shirt, facing the wall like the ninjas.
        static GuardFriend BuildFriend(Transform parent)
        {
            var root = Empty("Friend", parent, Vector3.zero);
            var friend = root.gameObject.AddComponent<GuardFriend>();
            var shirt = Lit("Guard Friend Shirt", new Color(0.2f, 0.55f, 1f));
            var skin = Lit("Ninja Skin", new Color(1f, 0.84f, 0.25f));
            var cap = Lit("Crouch Gate Red", new Color(0.92f, 0.18f, 0.15f), 0.3f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            foreach (var x in new[] { -0.09f, 0.09f })
            {
                Block(PrimitiveType.Cube, "Leg", root, new Vector3(x, 0.22f, 0f), new Vector3(0.14f, 0.44f, 0.16f), dark);
            }

            var body = Empty("Body", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Shirt", body, new Vector3(0f, 0.62f, 0f), new Vector3(0.36f, 0.38f, 0.22f), shirt);
            Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 0.98f, 0f), new Vector3(0.32f, 0.32f, 0.3f), skin);
            Block(PrimitiveType.Cube, "Cap", body, new Vector3(0f, 1.17f, 0.03f), new Vector3(0.34f, 0.08f, 0.36f), cap);
            var leftArm = KidsWorldBuilder.Limb(body, "LeftArm", new Vector3(-0.23f, 0.78f, 0f), new Vector3(0.1f, 0.34f, 0.12f), shirt);
            var rightArm = KidsWorldBuilder.Limb(body, "RightArm", new Vector3(0.23f, 0.78f, 0f), new Vector3(0.1f, 0.34f, 0.12f), shirt);
            Set(friend, "body", body);
            Set(friend, "leftArm", leftArm);
            Set(friend, "rightArm", rightArm);
            Set(friend, "cheerPoint", Empty("CheerPoint", body, new Vector3(0f, 1.5f, 0f)));
            return friend;
        }

        #endregion
    }
}
