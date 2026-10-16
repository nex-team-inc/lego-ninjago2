#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The One Punch 3D prefabs, all from primitives: the street (road along +Z, blocky buildings, the far gate), its
    /// still camera behind and above the ninjas, two block ninjas seen from behind, and the blocky monster.
    /// </summary>
    public static class OnePunchWorldBuilder
    {
        public const string StreetPath = PrefabsRoot + "/OnePunch/OnePunchStreet.prefab";
        const string MonsterPath = PrefabsRoot + "/OnePunch/StreetMonster.prefab";
        const float RoadHalfWidth = 4.5f;
        const float StreetLength = 80f;
        static readonly Color skyColor = new(0.45f, 0.78f, 1f);

        #region Entry Point

        public static void Build()
        {
            var monster = BuildMonster();
            var root = new GameObject("OnePunchStreet");
            var street = root.AddComponent<OnePunchStreet>();
            BuildRoad(root.transform);
            BuildBuildings(root.transform);
            // Tilted only a little down, so a thrown monster's arc stays under the top of the screen.
            var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 3.4f, -6.8f), new Vector3(8f, 0f, 0f), 50f);
            camera.backgroundColor = skyColor;
            camera.farClipPlane = 140f;
            var ninjas = new List<Object> { BuildNinja(root.transform, "NinjaLeft"), BuildNinja(root.transform, "NinjaRight") };
            var pool = Empty("Monsters", root.transform, Vector3.zero).gameObject.AddComponent<StreetMonsterPool>();
            Set(street, "streetCamera", camera);
            SetArray(street, "ninjas", ninjas);
            Set(street, "monsterPool", pool);
            Set(street, "monsterPrefab", monster);
            Set(street, "monsterColors", colors =>
            {
                var palette = new[]
                {
                    new Color(0.6f, 0.3f, 0.95f), new Color(0.45f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(0.1f, 0.8f, 0.75f),
                    new Color(1f, 0.35f, 0.75f),
                };
                colors.arraySize = palette.Length;
                for (var i = 0; i < palette.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = palette[i];
            });
            SavePrefab(root, StreetPath);
        }

        #endregion

        #region Street

        static void BuildRoad(Transform root)
        {
            var asphalt = Lit("Punch Asphalt", new Color(0.32f, 0.33f, 0.37f), 0.1f);
            var sidewalk = Lit("Punch Sidewalk", new Color(0.82f, 0.8f, 0.74f));
            var curb = Lit("Punch Curb", new Color(0.95f, 0.93f, 0.88f));
            var paint = Lit("Punch Road Paint", new Color(1f, 0.85f, 0.15f), 0.2f);
            var center = StreetLength * 0.5f - 8f;
            Block(PrimitiveType.Cube, "Road", root, new Vector3(0f, -0.25f, center), new Vector3(RoadHalfWidth * 2f, 0.5f, StreetLength), asphalt);
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Sidewalk", root, new Vector3(side * (RoadHalfWidth + 1.75f), -0.1f, center), new Vector3(3.5f, 0.5f, StreetLength), sidewalk);
                Block(PrimitiveType.Cube, "Curb", root, new Vector3(side * (RoadHalfWidth + 0.08f), 0.02f, center), new Vector3(0.16f, 0.2f, StreetLength), curb);
            }

            // From in front of the ninjas, so no dash shows between a ninja's legs.
            for (var z = 3f; z < StreetLength - 8f; z += 5f)
            {
                Block(PrimitiveType.Cube, "Dash", root, new Vector3(0f, 0.005f, z), new Vector3(0.18f, 0.01f, 2.2f), paint);
            }

            // The far end where monsters come from: a red gate over the road.
            var red = Lit("Crouch Gate Red", new Color(0.92f, 0.18f, 0.15f), 0.3f);
            var black = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cylinder, "GatePost", root, new Vector3(side * (RoadHalfWidth + 0.6f), 3f, 44f), new Vector3(0.7f, 3f, 0.7f), red);
            }

            Block(PrimitiveType.Cube, "GateTop", root, new Vector3(0f, 6.4f, 44f), new Vector3(RoadHalfWidth * 2f + 3.4f, 0.6f, 0.9f), black);
            Block(PrimitiveType.Cube, "GateBeam", root, new Vector3(0f, 5.3f, 44f), new Vector3(RoadHalfWidth * 2f + 2f, 0.45f, 0.6f), red);
        }

        // Saturated blocks on both sides, each with a roof trim and a dark window band; heights vary so the skyline reads.
        static void BuildBuildings(Transform root)
        {
            var walls = new[]
            {
                Lit("Punch Wall Yellow", new Color(1f, 0.82f, 0.25f)), Lit("Punch Wall Teal", new Color(0.15f, 0.7f, 0.68f)),
                Lit("Punch Wall Coral", new Color(1f, 0.45f, 0.38f)), Lit("Punch Wall Lime", new Color(0.55f, 0.85f, 0.3f)),
                Lit("Punch Wall Violet", new Color(0.55f, 0.42f, 0.9f)), Lit("Punch Wall Orange", new Color(1f, 0.6f, 0.2f)),
            };
            var trim = Lit("Punch Trim", new Color(0.95f, 0.95f, 0.95f));
            var glass = Lit("Punch Window", new Color(0.2f, 0.32f, 0.5f), 0.7f);
            var heights = new[] { 7f, 11f, 8.5f, 13f, 9f, 6.5f, 12f, 10f };
            var index = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                for (var z = -6f; z < 56f; z += 7.5f, index++)
                {
                    var height = heights[index % heights.Length];
                    var x = side * (RoadHalfWidth + 3.5f + 3f);
                    var building = Empty("Building", root, new Vector3(x, 0f, z + 3.6f));
                    Block(PrimitiveType.Cube, "Walls", building, new Vector3(0f, height * 0.5f, 0f), new Vector3(6f, height, 7f), walls[index % walls.Length]);
                    Block(PrimitiveType.Cube, "Roof", building, new Vector3(0f, height + 0.15f, 0f), new Vector3(6.4f, 0.3f, 7.4f), trim);
                    for (var floor = 2.2f; floor < height - 1.2f; floor += 2.6f)
                    {
                        Block(PrimitiveType.Cube, "Windows", building, new Vector3(-side * 3.01f, floor, 0f), new Vector3(0.05f, 1.1f, 5.6f), glass);
                    }
                }
            }
        }

        #endregion

        #region Ninja

        // Seen from behind: the scarf tails and the sword on the back read the facing; the gloves are big so the punching hand reads.
        static StreetNinja BuildNinja(Transform parent, string name)
        {
            var ninjaRoot = Empty(name, parent, Vector3.zero);
            var rig = ninjaRoot.gameObject.AddComponent<StreetNinja>();
            var tint = Lit("Punch Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var glove = Lit("Punch Glove", new Color(0.98f, 0.98f, 0.95f), 0.35f);
            var rope = Lit("Punch Rope", new Color(0.62f, 0.42f, 0.2f));
            var tinted = new List<Object>();
            foreach (var x in new[] { -0.14f, 0.14f })
            {
                Block(PrimitiveType.Cube, "Leg", ninjaRoot, new Vector3(x, 0.42f, 0f), new Vector3(0.22f, 0.84f, 0.26f), dark);
            }

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
            var leftArm = Arm(body, "LeftArm", -0.36f, tint, glove, out var leftFist);
            var rightArm = Arm(body, "RightArm", 0.36f, tint, glove, out var rightFist);
            tinted.Add(leftArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(rightArm.GetChild(0).GetComponent<MeshRenderer>());

            var ropes = Empty("Ropes", body, Vector3.zero);
            foreach (var y in new[] { 0.28f, 0.48f, 0.66f })
            {
                Block(PrimitiveType.Cylinder, "Rope", ropes, new Vector3(0f, y, 0f), new Vector3(0.82f, 0.035f, 0.52f), rope);
            }

            var seat = Empty("Seat", body, new Vector3(0f, 0.72f, 0.05f));
            var marker = NinjagoWorldBuilder.BuildPlayerTagCanvas(ninjaRoot, "Marker", new Vector3(0f, 2.25f, 0f), 0.006f, 64f, true);
            Set(rig, "body", body);
            Set(rig, "leftArm", leftArm);
            Set(rig, "rightArm", rightArm);
            Set(rig, "leftFist", leftFist);
            Set(rig, "rightFist", rightFist);
            SetArray(rig, "colorRenderers", tinted);
            Set(rig, "ropes", ropes.gameObject);
            Set(rig, "seat", seat);
            Set(rig, "marker", marker);
            return rig;
        }

        // A shoulder pivot with the sleeve hanging along -Y and a big glove at its end.
        static Transform Arm(Transform body, string name, float x, Material sleeve, Material glove, out Transform fist)
        {
            var arm = Empty(name, body, new Vector3(x, 0.66f, 0f));
            Block(PrimitiveType.Cube, "Sleeve", arm, new Vector3(0f, -0.26f, 0f), new Vector3(0.15f, 0.52f, 0.16f), sleeve);
            fist = Block(PrimitiveType.Cube, "Glove", arm, new Vector3(0f, -0.6f, 0f), new Vector3(0.22f, 0.2f, 0.22f), glove).transform;
            return arm;
        }

        #endregion

        #region Monster

        // Bigger than a ninja, with a stud on the head like a toy brick; its face is on +Z.
        static StreetMonster BuildMonster()
        {
            var root = new GameObject("StreetMonster");
            var monster = root.AddComponent<StreetMonster>();
            var tint = Lit("Punch Monster Tint", Color.white, 0.2f);
            var dark = Lit("Crouch Monster Dark", new Color(0.22f, 0.2f, 0.24f));
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var pupil = Lit("Mecha Eye Pupil", new Color(0.05f, 0.05f, 0.05f));
            var tinted = new List<Object>();
            var leftLeg = Limb(root.transform, "LeftLeg", new Vector3(-0.26f, 0.8f, 0f), new Vector3(0.34f, 0.8f, 0.4f), dark);
            var rightLeg = Limb(root.transform, "RightLeg", new Vector3(0.26f, 0.8f, 0f), new Vector3(0.34f, 0.8f, 0.4f), dark);
            var body = Empty("Body", root.transform, Vector3.zero);
            tinted.Add(Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.22f, 0f), new Vector3(1.05f, 0.9f, 0.62f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 1.98f, 0f), new Vector3(0.78f, 0.62f, 0.66f), tint).GetComponent<MeshRenderer>());
            tinted.Add(Block(PrimitiveType.Cylinder, "Stud", body, new Vector3(0f, 2.34f, 0f), new Vector3(0.36f, 0.06f, 0.36f), tint).GetComponent<MeshRenderer>());
            foreach (var x in new[] { -0.17f, 0.17f })
            {
                Block(PrimitiveType.Cube, "Eye", body, new Vector3(x, 2.06f, 0.34f), new Vector3(0.18f, 0.18f, 0.03f), white);
                Block(PrimitiveType.Cube, "Pupil", body, new Vector3(x, 2.03f, 0.36f), new Vector3(0.08f, 0.08f, 0.03f), pupil);
            }

            Block(PrimitiveType.Cube, "Mouth", body, new Vector3(0f, 1.82f, 0.34f), new Vector3(0.36f, 0.08f, 0.03f), dark);
            var leftArm = Limb(body, "LeftArm", new Vector3(-0.66f, 1.6f, 0f), new Vector3(0.26f, 0.72f, 0.3f), tint);
            var rightArm = Limb(body, "RightArm", new Vector3(0.66f, 1.6f, 0f), new Vector3(0.26f, 0.72f, 0.3f), tint);
            tinted.Add(leftArm.GetChild(0).GetComponent<MeshRenderer>());
            tinted.Add(rightArm.GetChild(0).GetComponent<MeshRenderer>());
            Set(monster, "top", Empty("Top", body, new Vector3(0f, 2.38f, 0f)));
            Set(monster, "body", body);
            Set(monster, "leftLeg", leftLeg);
            Set(monster, "rightLeg", rightLeg);
            Set(monster, "leftArm", leftArm);
            Set(monster, "rightArm", rightArm);
            SetArray(monster, "tintRenderers", tinted);
            return SavePrefab(root, MonsterPath).GetComponent<StreetMonster>();
        }

        // A pivot with the block hanging along -Y.
        static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Material material)
        {
            var limb = Empty(name, parent, pivot);
            Block(PrimitiveType.Cube, "Block", limb, new Vector3(0f, -size.y * 0.5f, 0f), size, material);
            return limb;
        }

        #endregion
    }
}
