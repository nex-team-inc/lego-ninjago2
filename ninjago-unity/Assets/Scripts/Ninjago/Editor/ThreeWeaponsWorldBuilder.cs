#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The Three Weapons 3D prefabs, all from primitives: the gauntlet lane with its ninja, three monsters, a watching crowd and shuriken.</summary>
    public static class ThreeWeaponsWorldBuilder
    {
        public const string LanePath = PrefabsRoot + "/ThreeWeapons/GauntletLane.prefab";
        const string Root = PrefabsRoot + "/ThreeWeapons";
        static readonly Color skyColor = new(0.5f, 0.8f, 1f);
        static readonly Color[] monsterColors = { new(0.92f, 0.3f, 0.25f), new(0.3f, 0.75f, 0.35f), new(0.6f, 0.35f, 0.9f) };

        #region Entry Point

        public static void Build()
        {
            var dim = Lit("Gauntlet Monster Dim", new Color(0.5f, 0.5f, 0.55f));
            var monsterPrefabs = new GauntletMonster[monsterColors.Length];
            for (var i = 0; i < monsterColors.Length; i++) monsterPrefabs[i] = BuildMonster(i, dim);
            BuildLane(monsterPrefabs, BuildShuriken());
        }

        #endregion

        #region Lane

        static void BuildLane(GauntletMonster[] monsterPrefabs, Transform shurikenPrefab)
        {
            var root = new GameObject("GauntletLane");
            var lane = root.AddComponent<GauntletLane>();
            BuildFloor(root.transform);
            var ninja = BuildNinja(root.transform);
            var monsters = new List<Object>();
            for (var i = 0; i < 3; i++) monsters.Add(PlaceMonster(monsterPrefabs[i], root.transform, new Vector3(0f, 0f, 9f + i * 9f), 0f, 1f));
            var crowd = new List<Object>();
            // The crowd turns in toward the line of fights from both sides.
            var spots = new[] { (-5.6f, 7f, -60f), (5.8f, 8.5f, 60f), (-5.8f, 14.5f, -70f), (5.6f, 16f, 70f), (-4.8f, 23.5f, -55f), (5f, 25f, 55f), (-1.8f, 31f, -10f), (2f, 31.5f, 10f) };
            for (var i = 0; i < spots.Length; i++)
            {
                var (x, z, yaw) = spots[i];
                crowd.Add(PlaceMonster(monsterPrefabs[i % monsterPrefabs.Length], root.transform, new Vector3(x, 0f, z), yaw, 0.85f));
            }

            var projectiles = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                var projectile = (Transform)PrefabUtility.InstantiatePrefab(shurikenPrefab, root.transform);
                projectile.name = $"Shuriken{i}";
                projectiles.Add(projectile);
            }

            var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 2.6f, -4.4f), new Vector3(10f, 0f, 0f), 52f);
            camera.backgroundColor = skyColor;
            camera.farClipPlane = 80f;
            Set(lane, "ninja", ninja);
            SetArray(lane, "monsters", monsters);
            SetArray(lane, "crowd", crowd);
            SetArray(lane, "projectiles", projectiles);
            Set(lane, "laneCamera", camera);
            SavePrefab(root, LanePath);
        }

        static GauntletMonster PlaceMonster(GauntletMonster prefab, Transform parent, Vector3 position, float yaw, float scale)
        {
            var monster = (GauntletMonster)PrefabUtility.InstantiatePrefab(prefab, parent);
            monster.transform.localPosition = position;
            monster.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            monster.transform.localScale = Vector3.one * scale;
            return monster;
        }

        // An open courtyard floor with a bright path of tiles toward the three monsters.
        static void BuildFloor(Transform root)
        {
            var floor = Empty("Floor", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Ground", floor, new Vector3(0f, -0.1f, 15f), new Vector3(40f, 0.2f, 60f), Lit("Gauntlet Ground", new Color(0.95f, 0.85f, 0.6f)));
            var tile = Lit("Gauntlet Tile", new Color(0.98f, 0.93f, 0.78f));
            for (var z = 0; z < 34; z += 2)
            {
                Block(PrimitiveType.Cube, "Tile", floor, new Vector3(0f, 0.01f, z), new Vector3(2.6f, 0.02f, 1.7f), tile);
            }

            var wall = Lit("Courtyard Wall", new Color(0.86f, 0.32f, 0.22f));
            var roof = Lit("Courtyard Roof", new Color(0.12f, 0.62f, 0.5f));
            Block(PrimitiveType.Cube, "BackWall", floor, new Vector3(0f, 1.6f, 38f), new Vector3(30f, 3.2f, 0.8f), wall);
            Block(PrimitiveType.Cube, "BackRoof", floor, new Vector3(0f, 3.35f, 37.8f), new Vector3(31f, 0.4f, 2f), roof);
            var lantern = Lit("Courtyard Lantern", new Color(1f, 0.8f, 0.3f), 0.3f, new Color(1f, 0.6f, 0.1f));
            var post = Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f));
            foreach (var x in new[] { -8f, 8f })
            {
                for (var z = 4f; z < 34f; z += 10f)
                {
                    Block(PrimitiveType.Cube, "Post", floor, new Vector3(x, 1f, z), new Vector3(0.3f, 2f, 0.3f), post);
                    Block(PrimitiveType.Cube, "Lantern", floor, new Vector3(x, 2.15f, z), new Vector3(0.55f, 0.55f, 0.55f), lantern);
                }
            }
        }

        #endregion

        #region Ninja

        // Facing +Z with the camera behind: every hint is on the model, beside the body so the back never hides it.
        static WeaponNinjaRig BuildNinja(Transform root)
        {
            var ninjaRoot = Empty("Ninja", root, Vector3.zero);
            var rig = ninjaRoot.gameObject.AddComponent<WeaponNinjaRig>();
            var tint = Lit("Crouch Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var skin = Lit("Ninja Skin", new Color(1f, 0.84f, 0.25f));
            var tinted = new List<Object>();
            var leftLeg = Leg(ninjaRoot, -0.15f, dark);
            var rightLeg = Leg(ninjaRoot, 0.15f, dark);
            var body = Empty("Body", ninjaRoot, Vector3.zero);
            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.88f, 0f), new Vector3(0.58f, 0.18f, 0.34f), dark);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.99f, 0f), new Vector3(0.62f, 0.08f, 0.38f), gold);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.28f, 0f), new Vector3(0.62f, 0.56f, 0.36f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Hood", body, new Vector3(0f, 1.8f, 0f), new Vector3(0.46f, 0.44f, 0.44f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "HoodKnot", body, new Vector3(0f, 1.86f, -0.27f), new Vector3(0.16f, 0.16f, 0.12f), tint).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "FaceSlit", body, new Vector3(0f, 1.82f, 0.23f), new Vector3(0.36f, 0.1f, 0.02f), skin);
            var leftShoulder = Arm(body, "LeftShoulder", -0.4f, tint, skin, tinted, out var leftHand);
            var rightShoulder = Arm(body, "RightShoulder", 0.4f, tint, skin, tinted, out var rightHand);

            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var sword = Empty("Sword", rightHand, Vector3.zero);
            Block(PrimitiveType.Cube, "Blade", sword, new Vector3(0f, -0.55f, 0f), new Vector3(0.07f, 1f, 0.03f), steel);
            Block(PrimitiveType.Cube, "Guard", sword, new Vector3(0f, -0.04f, 0f), new Vector3(0.26f, 0.05f, 0.08f), gold);
            var wood = Lit("Staff Wood", new Color(0.45f, 0.27f, 0.13f));
            var leftNunchuck = Nunchuck(leftHand, wood);
            var rightNunchuck = Nunchuck(rightHand, wood);
            var leftStar = Star(leftHand, "Shuriken", steel, 0.28f);
            var rightStar = Star(rightHand, "Shuriken", steel, 0.28f);

            var arrows = Empty("SwordArrows", ninjaRoot, Vector3.zero);
            Icon(arrows, "Left", new Vector3(-0.85f, 1.05f, 0.15f), GetSprite("Arrow"), new Color(0.35f, 0.6f, 1f), 180f);
            Icon(arrows, "Right", new Vector3(0.85f, 1.05f, 0.15f), GetSprite("Arrow"), new Color(0.35f, 0.6f, 1f), 0f);
            var spots = new Object[]
            {
                Empty("SpotLeftHead", ninjaRoot, new Vector3(-0.62f, 2f, -0.15f)), Empty("SpotRightHead", ninjaRoot, new Vector3(0.62f, 2f, -0.15f)),
                Empty("SpotLeftBody", ninjaRoot, new Vector3(-0.85f, 1.3f, -0.15f)), Empty("SpotRightBody", ninjaRoot, new Vector3(0.85f, 1.3f, -0.15f)),
            };
            var spinIcon = Icon(ninjaRoot, "SpinIcon", new Vector3(-0.62f, 2f, -0.15f), GetSprite("SpinIcon"), new Color(1f, 0.6f, 0.15f), 0f);
            var leftDart = Icon(ninjaRoot, "LeftDartIcon", new Vector3(-0.75f, 2.15f, -0.2f), GetSprite("StarIcon"), new Color(0.75f, 0.45f, 1f), 0f);
            var rightDart = Icon(ninjaRoot, "RightDartIcon", new Vector3(0.75f, 2.15f, -0.2f), GetSprite("StarIcon"), new Color(0.75f, 0.45f, 1f), 0f);

            Set(rig, "body", body);
            Set(rig, "leftLeg", leftLeg);
            Set(rig, "rightLeg", rightLeg);
            Set(rig, "leftShoulder", leftShoulder);
            Set(rig, "rightShoulder", rightShoulder);
            Set(rig, "leftHand", leftHand);
            Set(rig, "rightHand", rightHand);
            Set(rig, "sword", sword.gameObject);
            Set(rig, "leftNunchuck", leftNunchuck);
            Set(rig, "rightNunchuck", rightNunchuck);
            Set(rig, "leftShuriken", leftStar.gameObject);
            Set(rig, "rightShuriken", rightStar.gameObject);
            Set(rig, "swordArrows", arrows.gameObject);
            Set(rig, "spinIcon", spinIcon);
            SetArray(rig, "spinSpots", spots);
            Set(rig, "leftDartIcon", leftDart.gameObject);
            Set(rig, "rightDartIcon", rightDart.gameObject);
            SetArray(rig, "colorRenderers", tinted);
            return rig;
        }

        static Transform Leg(Transform root, float x, Material material)
        {
            var leg = Empty(x < 0f ? "LeftLeg" : "RightLeg", root, new Vector3(x, 0.86f, 0f));
            Block(PrimitiveType.Cube, "Block", leg, new Vector3(0f, -0.43f, 0f), new Vector3(0.24f, 0.86f, 0.28f), material);
            return leg;
        }

        // A straight arm hanging along -Y from the shoulder pivot, the hand point at its end.
        static Transform Arm(Transform body, string name, float x, Material sleeve, Material skin, List<Object> tinted, out Transform hand)
        {
            var shoulder = Empty(name, body, new Vector3(x, 1.5f, 0f));
            tinted.Add(Block(PrimitiveType.Cube, "Sleeve", shoulder, new Vector3(0f, -0.3f, 0f), new Vector3(0.17f, 0.6f, 0.2f), sleeve).GetComponent<MeshRenderer>());
            Block(PrimitiveType.Cube, "Fist", shoulder, new Vector3(0f, -0.66f, 0f), new Vector3(0.16f, 0.14f, 0.16f), skin);
            hand = Empty("Hand", shoulder, new Vector3(0f, -0.68f, 0f));
            return shoulder;
        }

        // Two sticks hanging from the hand on a pivot the rig whirls.
        static Transform Nunchuck(Transform hand, Material wood)
        {
            var pivot = Empty("Nunchuck", hand, Vector3.zero);
            Block(PrimitiveType.Cube, "StickA", pivot, new Vector3(0f, -0.2f, 0f), new Vector3(0.06f, 0.36f, 0.06f), wood);
            Block(PrimitiveType.Cube, "StickB", pivot, new Vector3(0.22f, -0.5f, 0f), new Vector3(0.06f, 0.36f, 0.06f), wood, new Vector3(0f, 0f, 50f));
            return pivot;
        }

        static Transform Star(Transform parent, string name, Material material, float size)
        {
            var star = Empty(name, parent, Vector3.zero);
            Block(PrimitiveType.Cube, "A", star, Vector3.zero, new Vector3(size, 0.02f, size * 0.3f), material, new Vector3(0f, 45f, 0f));
            Block(PrimitiveType.Cube, "B", star, Vector3.zero, new Vector3(size, 0.02f, size * 0.3f), material, new Vector3(0f, -45f, 0f));
            return star;
        }

        // A small world-space icon facing the camera behind the ninja.
        static Transform Icon(Transform parent, string name, Vector3 position, Sprite sprite, Color color, float roll)
        {
            var canvas = NinjagoWorldBuilder.WorldCanvas(parent, name, position, 0.005f, new Vector2(80f, 80f));
            var image = NewImage(canvas.transform, "Image", sprite, color);
            Stretch(image.rectTransform);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, roll);
            var shadow = image.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return canvas.transform;
        }

        #endregion

        #region Pieces

        // Big and blocky, facing -Z toward the ninja; the health bar shows only while it is the one being fought.
        static GauntletMonster BuildMonster(int index, Material dim)
        {
            var root = new GameObject($"GauntletMonster{index + 1}");
            var monster = root.AddComponent<GauntletMonster>();
            var body = Empty("Body", root.transform, Vector3.zero);
            var skin = Lit($"Gauntlet Monster {index + 1}", monsterColors[index], 0.2f);
            var dark = Lit("Crouch Monster Dark", new Color(0.22f, 0.2f, 0.24f));
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var lit = new List<Object>();
            foreach (var x in new[] { -0.35f, 0.35f })
            {
                Block(PrimitiveType.Cube, "Leg", body, new Vector3(x, 0.5f, 0f), new Vector3(0.45f, 1f, 0.5f), dark);
                lit.Add(Block(PrimitiveType.Cube, "Arm", body, new Vector3(x * 3.1f, 1.6f, -0.1f), new Vector3(0.36f, 1f, 0.4f), skin, new Vector3(20f, 0f, 0f))
                    .GetComponent<MeshRenderer>());
                lit.Add(Block(PrimitiveType.Cube, "Horn", body, new Vector3(x, 2.95f, 0f), new Vector3(0.14f, 0.4f, 0.14f), white, new Vector3(0f, 0f, -x * 40f))
                    .GetComponent<MeshRenderer>());
            }

            lit.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.65f, 0f), new Vector3(1.4f, 1.3f, 0.8f), skin).GetComponent<MeshRenderer>());
            lit.Add(Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 2.6f, 0f), new Vector3(0.9f, 0.7f, 0.75f), skin).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                lit.Add(Block(PrimitiveType.Cube, "Eye", body, new Vector3(x, 2.68f, -0.39f), new Vector3(0.18f, 0.14f, 0.04f), white).GetComponent<MeshRenderer>());
            }

            Block(PrimitiveType.Cube, "Mouth", body, new Vector3(0f, 2.42f, -0.39f), new Vector3(0.42f, 0.1f, 0.04f), dark);
            var chest = Empty("Chest", root.transform, new Vector3(0f, 1.7f, -0.45f));
            var canvas = NinjagoWorldBuilder.WorldCanvas(root.transform, "HealthBar", new Vector3(0f, 3.5f, 0f), 0.01f, new Vector2(160f, 22f));
            var back = NewImage(canvas.transform, "Back", GetSprite("Panel"), new Color(0.08f, 0.08f, 0.1f, 0.9f));
            back.type = Image.Type.Sliced;
            Stretch(back.rectTransform);
            var fill = NewImage(canvas.transform, "Fill", GetSprite("Panel"), new Color(1f, 0.3f, 0.25f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));
            Set(monster, "body", body);
            SetArray(monster, "litRenderers", lit);
            Set(monster, "dimMaterial", dim);
            Set(monster, "healthBarRoot", canvas.gameObject);
            Set(monster, "healthBarFill", fill);
            Set(monster, "chestPoint", chest);
            return SavePrefab(root, $"{Root}/GauntletMonster{index + 1}.prefab").GetComponent<GauntletMonster>();
        }

        static Transform BuildShuriken()
        {
            var root = new GameObject("ShurikenProjectile");
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            Star(root.transform, "Star", steel, 0.45f);
            return SavePrefab(root, $"{Root}/ShurikenProjectile.prefab").transform;
        }

        #endregion
    }
}
