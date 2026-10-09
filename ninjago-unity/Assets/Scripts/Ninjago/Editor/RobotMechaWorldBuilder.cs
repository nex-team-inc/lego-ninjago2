#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The Robot Mecha 3D prefabs, all from primitives: the courtyard lane with the mecha, rusher, archer and arrow.</summary>
    public static class RobotMechaWorldBuilder
    {
        public const string LanePath = PrefabsRoot + "/Mecha/MechaLane.prefab";
        const string RusherPath = PrefabsRoot + "/Mecha/Rusher.prefab";
        const string ArcherPath = PrefabsRoot + "/Mecha/Archer.prefab";
        const string ArrowPath = PrefabsRoot + "/Mecha/Arrow.prefab";
        const float pillarRadius = 17f;
        const float pillarHeight = 4.5f;
        const float wallRadius = 25f;
        // Enemies are authored at brick size and scaled up so they read on the ring from the high camera.
        const float enemyScale = 1.35f;
        static readonly Color skyColor = new(0.45f, 0.76f, 1f);

        #region Entry Point

        public static void Build()
        {
            var hurt = Lit("Mecha Enemy Hurt", new Color(1f, 0.9f, 0.97f), 0.3f, new Color(2.2f, 1.1f, 1.9f));
            BuildLane(BuildRusher(hurt), BuildArcher(hurt), BuildArrow());
        }

        #endregion

        #region Lane

        static void BuildLane(MechaEnemy rusher, MechaEnemy archer, MechaArrow arrow)
        {
            var root = new GameObject("MechaLane");
            var lane = root.AddComponent<MechaLane>();
            var pillarTops = BuildArena(root.transform);
            var mecha = BuildMecha(root.transform);
            var fieldRoot = Empty("Field", root.transform, Vector3.zero);
            var field = fieldRoot.gameObject.AddComponent<MechaField>();
            Set(field, "rusherPool", Empty("RusherPool", fieldRoot, Vector3.zero).gameObject.AddComponent<MechaEnemyPool>());
            Set(field, "rusherPrefab", rusher);
            Set(field, "archerPool", Empty("ArcherPool", fieldRoot, Vector3.zero).gameObject.AddComponent<MechaEnemyPool>());
            Set(field, "archerPrefab", archer);
            Set(field, "arrowPool", Empty("ArrowPool", fieldRoot, Vector3.zero).gameObject.AddComponent<MechaArrowPool>());
            Set(field, "arrowPrefab", arrow);
            SetArray(field, "pillarTops", pillarTops);
            // Behind and high above the mecha so it never hides the rushers coming at it; turns with the mecha's yaw.
            var pivot = Empty("CameraPivot", root.transform, Vector3.zero);
            var camera = NinjagoWorldBuilder.NewCamera(pivot, new Vector3(0f, 7f, -7f), new Vector3(26f, 0f, 0f), 58f);
            camera.backgroundColor = skyColor;
            camera.farClipPlane = 80f;
            Set(lane, "mecha", mecha);
            Set(lane, "field", field);
            Set(lane, "cameraPivot", pivot);
            Set(lane, "laneCamera", camera);
            SavePrefab(root, LanePath);
        }

        // Round flat courtyard: center mark, a ring wall past the spawn ring, pillars on the ring for high archers.
        static Object[] BuildArena(Transform root)
        {
            var scenery = Empty("Scenery", root, Vector3.zero);
            Block(PrimitiveType.Cylinder, "Grass", scenery, new Vector3(0f, -0.6f, 0f), new Vector3(140f, 0.5f, 140f), Lit("Mecha Grass", new Color(0.35f, 0.8f, 0.3f)));
            Block(PrimitiveType.Cylinder, "Courtyard", scenery, new Vector3(0f, -0.06f, 0f), new Vector3(2f * wallRadius + 2f, 0.05f, 2f * wallRadius + 2f),
                Lit("Mecha Courtyard", new Color(0.95f, 0.82f, 0.58f)));
            Block(PrimitiveType.Cylinder, "InnerYard", scenery, new Vector3(0f, -0.03f, 0f), new Vector3(26f, 0.05f, 26f), Lit("Mecha Inner Yard", new Color(0.98f, 0.9f, 0.72f)));
            Block(PrimitiveType.Cylinder, "CenterMark", scenery, new Vector3(0f, -0.005f, 0f), new Vector3(4.4f, 0.03f, 4.4f), Lit("Mecha Center Mark", new Color(1f, 0.3f, 0.7f)));
            Block(PrimitiveType.Cylinder, "CenterMarkInner", scenery, new Vector3(0f, 0.005f, 0f), new Vector3(3.5f, 0.03f, 3.5f), Lit("Mecha Inner Yard", new Color(0.98f, 0.9f, 0.72f)));

            var brick = Lit("Mecha Wall", new Color(0.92f, 0.33f, 0.22f));
            var roof = Lit("Mecha Wall Roof", new Color(0.1f, 0.65f, 0.55f));
            const int segments = 20;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * 360f / segments;
                var position = OnRing(angle, wallRadius);
                Block(PrimitiveType.Cube, "Wall", scenery, position + Vector3.up * 0.7f, new Vector3(6.2f, 1.4f, 0.8f), brick, new Vector3(0f, angle, 0f));
                Block(PrimitiveType.Cube, "Roof", scenery, position + Vector3.up * 1.5f, new Vector3(6.6f, 0.25f, 1.3f), roof, new Vector3(0f, angle, 0f));
            }

            var column = Lit("Mecha Pillar", new Color(0.15f, 0.55f, 0.8f));
            var cap = Lit("Mecha Pillar Cap", new Color(1f, 0.8f, 0.2f), 0.4f);
            var tops = new List<Object>();
            for (var i = 0; i < 8; i++)
            {
                var angle = 22.5f + i * 45f;
                var pillar = Empty("Pillar", scenery, OnRing(angle, pillarRadius));
                pillar.localRotation = Quaternion.Euler(0f, angle, 0f);
                Block(PrimitiveType.Cube, "Column", pillar, new Vector3(0f, pillarHeight * 0.5f, 0f), new Vector3(1.1f, pillarHeight, 1.1f), column);
                Block(PrimitiveType.Cube, "Cap", pillar, new Vector3(0f, pillarHeight + 0.15f, 0f), new Vector3(1.9f, 0.3f, 1.9f), cap);
                tops.Add(Empty("Top", pillar, new Vector3(0f, pillarHeight + 0.3f, 0f)));
            }

            var trunk = Lit("Mecha Tree Trunk", new Color(0.45f, 0.28f, 0.15f));
            var leaves = Lit("Mecha Tree Leaves", new Color(0.12f, 0.62f, 0.25f));
            for (var i = 0; i < 10; i++)
            {
                var tree = Empty("Tree", scenery, OnRing(18f + i * 36f, 30f + i % 3 * 2f));
                Block(PrimitiveType.Cube, "Trunk", tree, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 1.6f, 0.6f), trunk);
                Block(PrimitiveType.Cube, "Leaves", tree, new Vector3(0f, 2.6f, 0f), new Vector3(2.6f, 2.2f, 2.6f), leaves, new Vector3(0f, i * 17f, 0f));
            }

            var hill = Lit("Mecha Hill", new Color(0.3f, 0.7f, 0.35f));
            for (var i = 0; i < 6; i++)
            {
                Block(PrimitiveType.Sphere, "Hill", scenery, OnRing(i * 60f + 10f, 62f) + Vector3.down * 6f, new Vector3(34f, 22f, 26f), hill, new Vector3(0f, i * 60f, 0f));
            }

            return tops.ToArray();
        }

        #endregion

        #region Mecha

        // Chunky brick robot facing +Z: feet, chest cannon, visor and lamps in front; a dark backpack with exhausts behind.
        static MechaRig BuildMecha(Transform root)
        {
            var mechaRoot = Empty("Mecha", root, Vector3.zero);
            var rig = mechaRoot.gameObject.AddComponent<MechaRig>();
            var yawRoot = Empty("YawRoot", mechaRoot, Vector3.zero);
            var body = Empty("Body", yawRoot, Vector3.zero);
            var white = Lit("Mecha White", new Color(0.96f, 0.95f, 0.99f), 0.35f);
            var lavender = Lit("Mecha Lavender", new Color(0.7f, 0.58f, 0.96f), 0.3f);
            var magenta = Lit("Mecha Magenta", new Color(0.96f, 0.22f, 0.64f), 0.4f);
            var dark = Lit("Mecha Joint", new Color(0.22f, 0.2f, 0.27f));
            var tint = Lit("Mecha Tint", Color.white, 0.35f);
            var lightOn = Lit("Mecha Light On", new Color(1f, 0.75f, 0.95f), 0.5f, new Color(2.6f, 0.8f, 2.1f));
            var lightDim = Lit("Mecha Light Dim", new Color(0.2f, 0.18f, 0.24f));
            var lights = new List<Object>();

            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Block(PrimitiveType.Cube, "Leg", body, new Vector3(x, 0.45f, 0f), new Vector3(0.55f, 0.8f, 0.6f), lavender);
                Block(PrimitiveType.Cube, "Foot", body, new Vector3(x, 0.12f, 0.14f), new Vector3(0.7f, 0.24f, 0.98f), dark);
            }

            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.88f, 0f), new Vector3(1.3f, 0.3f, 0.75f), dark);
            var pitch = Empty("PitchPivot", body, new Vector3(0f, 0.95f, 0f));
            Block(PrimitiveType.Cube, "Torso", pitch, new Vector3(0f, 0.6f, 0f), new Vector3(1.7f, 1.1f, 1f), white);
            Block(PrimitiveType.Cube, "ChestPlate", pitch, new Vector3(0f, 0.78f, 0.52f), new Vector3(1.25f, 0.55f, 0.08f), magenta);
            Block(PrimitiveType.Cylinder, "CannonBarrel", pitch, new Vector3(0f, 0.4f, 0.62f), new Vector3(0.56f, 0.12f, 0.56f), dark, new Vector3(90f, 0f, 0f));
            lights.Add(Renderer(Block(PrimitiveType.Cylinder, "CannonCore", pitch, new Vector3(0f, 0.4f, 0.72f), new Vector3(0.38f, 0.03f, 0.38f), lightOn,
                new Vector3(90f, 0f, 0f))));
            var muzzle = Empty("Muzzle", pitch, new Vector3(0f, 0.4f, 0.78f));
            Block(PrimitiveType.Cube, "Head", pitch, new Vector3(0f, 1.45f, 0f), new Vector3(0.85f, 0.6f, 0.75f), white);
            lights.Add(Renderer(Block(PrimitiveType.Cube, "Visor", pitch, new Vector3(0f, 1.5f, 0.38f), new Vector3(0.68f, 0.17f, 0.04f), lightOn)));
            Block(PrimitiveType.Cube, "Antenna", pitch, new Vector3(0f, 1.86f, -0.05f), new Vector3(0.08f, 0.26f, 0.08f), dark);
            lights.Add(Renderer(Block(PrimitiveType.Cube, "AntennaTip", pitch, new Vector3(0f, 2.02f, -0.05f), new Vector3(0.17f, 0.17f, 0.17f), lightOn)));
            Block(PrimitiveType.Cube, "Backpack", pitch, new Vector3(0f, 0.65f, -0.68f), new Vector3(1.15f, 0.9f, 0.4f), dark);
            // The camera sees the back: the backpack core and exhausts are the lights that show the pose dropping.
            lights.Add(Renderer(Block(PrimitiveType.Cube, "BackCore", pitch, new Vector3(0f, 0.78f, -0.89f), new Vector3(0.6f, 0.36f, 0.04f), lightOn)));
            var tinted = new List<Object>();
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Ear", pitch, new Vector3(side * 0.5f, 1.45f, 0f), new Vector3(0.15f, 0.35f, 0.35f), magenta);
                tinted.Add(Renderer(Block(PrimitiveType.Cube, "ShoulderPad", pitch, new Vector3(side * 1.05f, 1.06f, 0f), new Vector3(0.64f, 0.42f, 0.82f), tint)));
                lights.Add(Renderer(Block(PrimitiveType.Cube, "ShoulderLamp", pitch, new Vector3(side * 1.05f, 1.06f, 0.42f), new Vector3(0.32f, 0.15f, 0.03f), lightOn)));
                lights.Add(Renderer(Block(PrimitiveType.Cube, "Exhaust", pitch, new Vector3(side * 0.34f, 0.32f, -0.9f), new Vector3(0.3f, 0.3f, 0.1f), lightOn)));
            }

            var leftShoulder = BuildArm(pitch, "LeftShoulder", -1.05f, lavender, white, dark, out var leftElbow);
            var rightShoulder = BuildArm(pitch, "RightShoulder", 1.05f, lavender, white, dark, out var rightElbow);
            var beam = BuildBeam(muzzle);
            var marker = NinjagoWorldBuilder.BuildPlayerTagCanvas(yawRoot, "Marker", new Vector3(0f, 3.5f, 0f), 0.013f, 64f, true);
            marker.transform.parent.localRotation = Quaternion.Euler(22f, 0f, 0f);

            Set(rig, "yawRoot", yawRoot);
            Set(rig, "body", body);
            Set(rig, "pitchPivot", pitch);
            Set(rig, "muzzle", muzzle);
            Set(rig, "beam", beam);
            Set(rig, "leftShoulder", leftShoulder);
            Set(rig, "leftElbow", leftElbow);
            Set(rig, "rightShoulder", rightShoulder);
            Set(rig, "rightElbow", rightElbow);
            SetArray(rig, "lights", lights);
            Set(rig, "lightsOnMaterial", lightOn);
            Set(rig, "lightsDimMaterial", lightDim);
            SetArray(rig, "colorRenderers", tinted);
            Set(rig, "marker", marker);
            return rig;
        }

        // Shoulder pivot with the upper arm hanging along -Y; MechaRig swings it out and the forearm up into the L.
        static Transform BuildArm(Transform pitch, string name, float x, Material upper, Material lower, Material fist, out Transform elbow)
        {
            var shoulder = Empty(name, pitch, new Vector3(x, 0.95f, 0f));
            Block(PrimitiveType.Cube, "UpperArm", shoulder, new Vector3(0f, -0.38f, 0f), new Vector3(0.38f, 0.76f, 0.42f), upper);
            elbow = Empty("Elbow", shoulder, new Vector3(0f, -0.76f, 0f));
            Block(PrimitiveType.Cube, "Forearm", elbow, new Vector3(0f, -0.36f, 0f), new Vector3(0.44f, 0.72f, 0.48f), lower);
            Block(PrimitiveType.Cube, "Fist", elbow, new Vector3(0f, -0.8f, 0f), new Vector3(0.52f, 0.34f, 0.52f), fist);
            return shoulder;
        }

        // Unit beam along +Z for a hit width of 1: a bright core half as wide and a translucent pink glow half again as
        // wide, so the thick beam still reads from the high camera. No shadows.
        static Transform BuildBeam(Transform muzzle)
        {
            var beam = Empty("Beam", muzzle, Vector3.zero);
            var glow = Block(PrimitiveType.Cylinder, "Glow", beam, new Vector3(0f, 0f, 0.5f), new Vector3(1.5f, 0.5f, 1.5f),
                Unlit("Mecha Laser Glow", new Color(1f, 0.3f, 0.78f, 0.55f), true), new Vector3(90f, 0f, 0f));
            var core = Block(PrimitiveType.Cylinder, "Core", beam, new Vector3(0f, 0f, 0.5f), new Vector3(0.55f, 0.5f, 0.55f),
                Unlit("Mecha Laser Core", new Color(1f, 0.86f, 0.97f), false), new Vector3(90f, 0f, 0f));
            foreach (var part in new[] { glow, core })
            {
                var meshRenderer = part.GetComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            return beam;
        }

        #endregion

        #region Enemies

        // Low and wide: a brick boar that charges the mecha.
        static MechaEnemy BuildRusher(Material hurt)
        {
            var root = new GameObject("Rusher");
            var enemy = root.AddComponent<MechaEnemy>();
            var body = Empty("Body", root.transform, Vector3.zero);
            body.localScale = Vector3.one * enemyScale;
            var shellMaterial = Lit("Rusher Brick", new Color(1f, 0.42f, 0.12f));
            var dark = Lit("Rusher Dark", new Color(0.42f, 0.14f, 0.05f));
            var shell = Block(PrimitiveType.Cube, "Shell", body, new Vector3(0f, 0.62f, 0f), new Vector3(1.6f, 0.75f, 1.3f), shellMaterial);
            var top = Block(PrimitiveType.Cube, "ShellTop", body, new Vector3(0f, 1.1f, -0.05f), new Vector3(1.2f, 0.28f, 1f), shellMaterial);
            var snout = Block(PrimitiveType.Cube, "Snout", body, new Vector3(0f, 0.55f, 0.76f), new Vector3(0.8f, 0.45f, 0.35f), dark);
            AddEyes(body, 0.32f, 0.88f, 0.66f, 0.18f);
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Horn", body, new Vector3(side * 0.55f, 1.25f, 0.35f), new Vector3(0.14f, 0.38f, 0.14f), dark, new Vector3(25f, 0f, side * -20f));
                Block(PrimitiveType.Cube, "LegFront", body, new Vector3(side * 0.6f, 0.17f, 0.42f), new Vector3(0.32f, 0.36f, 0.32f), dark);
                Block(PrimitiveType.Cube, "LegBack", body, new Vector3(side * 0.6f, 0.17f, -0.42f), new Vector3(0.32f, 0.36f, 0.32f), dark);
            }

            Set(enemy, "type", property => property.intValue = (int)MechaEnemyType.Rusher);
            Set(enemy, "body", body);
            SetArray(enemy, "flashRenderers", new Object[] { Renderer(shell), Renderer(top), Renderer(snout) });
            Set(enemy, "hurtMaterial", hurt);
            return SavePrefab(root, RusherPath).GetComponent<MechaEnemy>();
        }

        // Taller, hooded, holding a bow that rises and shows a nocked arrow while it draws.
        static MechaEnemy BuildArcher(Material hurt)
        {
            var root = new GameObject("Archer");
            var enemy = root.AddComponent<MechaEnemy>();
            var body = Empty("Body", root.transform, Vector3.zero);
            body.localScale = Vector3.one * enemyScale;
            var cloth = Lit("Archer Cloth", new Color(0.58f, 0.25f, 0.9f));
            var dark = Lit("Archer Dark", new Color(0.2f, 0.12f, 0.3f));
            var face = Lit("Archer Face", new Color(1f, 0.84f, 0.3f));
            var wood = Lit("Archer Bow", new Color(0.6f, 0.32f, 0.12f));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Leg", body, new Vector3(side * 0.18f, 0.4f, 0f), new Vector3(0.28f, 0.8f, 0.32f), dark);
                Block(PrimitiveType.Cube, "Arm", body, new Vector3(side * 0.48f, 1.25f, 0.1f), new Vector3(0.2f, 0.6f, 0.22f), cloth, new Vector3(-30f, 0f, 0f));
            }

            var torso = Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.2f, 0f), new Vector3(0.75f, 0.8f, 0.45f), cloth);
            var head = Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 1.85f, 0f), new Vector3(0.5f, 0.48f, 0.48f), face);
            var hood = Block(PrimitiveType.Cube, "Hood", body, new Vector3(0f, 2.12f, -0.02f), new Vector3(0.6f, 0.25f, 0.58f), cloth);
            Block(PrimitiveType.Cube, "HoodPoint", body, new Vector3(0f, 2.32f, -0.12f), new Vector3(0.2f, 0.25f, 0.2f), cloth, new Vector3(-20f, 0f, 0f));
            AddEyes(body, 0.11f, 1.88f, 0.25f, 0.1f);
            var bow = Empty("Bow", body, new Vector3(0f, 1.35f, 0.45f));
            Block(PrimitiveType.Cube, "LimbTop", bow, new Vector3(0f, 0.42f, 0.08f), new Vector3(0.1f, 0.58f, 0.1f), wood, new Vector3(20f, 0f, 0f));
            Block(PrimitiveType.Cube, "LimbBottom", bow, new Vector3(0f, -0.42f, 0.08f), new Vector3(0.1f, 0.58f, 0.1f), wood, new Vector3(-20f, 0f, 0f));
            Block(PrimitiveType.Cube, "Grip", bow, new Vector3(0f, 0f, 0.14f), new Vector3(0.13f, 0.3f, 0.13f), dark);
            Block(PrimitiveType.Cube, "String", bow, new Vector3(0f, 0f, -0.04f), new Vector3(0.03f, 1.45f, 0.03f), Lit("Archer String", new Color(0.95f, 0.95f, 0.9f)));
            var nocked = Block(PrimitiveType.Cube, "NockedArrow", bow, new Vector3(0f, 0f, 0.35f), new Vector3(0.06f, 0.06f, 1f), ArrowMaterial());

            Set(enemy, "type", property => property.intValue = (int)MechaEnemyType.Archer);
            Set(enemy, "body", body);
            SetArray(enemy, "flashRenderers", new Object[] { Renderer(torso), Renderer(head), Renderer(hood) });
            Set(enemy, "hurtMaterial", hurt);
            Set(enemy, "bow", bow);
            Set(enemy, "nockedArrow", nocked);
            return SavePrefab(root, ArcherPath).GetComponent<MechaEnemy>();
        }

        // Big and bright so its flight reads from across the room; points along +Z.
        static MechaArrow BuildArrow()
        {
            var root = new GameObject("Arrow");
            var arrow = root.AddComponent<MechaArrow>();
            Block(PrimitiveType.Cube, "Shaft", root.transform, Vector3.zero, new Vector3(0.09f, 0.09f, 1.4f), ArrowMaterial());
            Block(PrimitiveType.Cube, "Head", root.transform, new Vector3(0f, 0f, 0.75f), new Vector3(0.22f, 0.22f, 0.3f), Lit("Arrow Head", new Color(0.8f, 0.82f, 0.88f), 0.6f),
                new Vector3(0f, 0f, 45f));
            var fletch = Lit("Arrow Fletch", new Color(1f, 0.25f, 0.6f));
            Block(PrimitiveType.Cube, "FletchH", root.transform, new Vector3(0f, 0f, -0.6f), new Vector3(0.36f, 0.03f, 0.28f), fletch);
            Block(PrimitiveType.Cube, "FletchV", root.transform, new Vector3(0f, 0f, -0.6f), new Vector3(0.03f, 0.36f, 0.28f), fletch);
            return SavePrefab(root, ArrowPath).GetComponent<MechaArrow>();
        }

        static void AddEyes(Transform body, float x, float y, float z, float size)
        {
            var white = Lit("Mecha Eye White", Color.white, 0.4f);
            var pupil = Lit("Mecha Eye Pupil", new Color(0.05f, 0.05f, 0.05f));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Eye", body, new Vector3(side * x, y, z), new Vector3(size, size, 0.04f), white);
                Block(PrimitiveType.Cube, "Pupil", body, new Vector3(side * x, y - size * 0.1f, z + 0.025f), new Vector3(size * 0.5f, size * 0.5f, 0.03f), pupil);
            }
        }

        #endregion

        #region Helpers

        static Material ArrowMaterial() => Lit("Arrow Shaft", new Color(1f, 0.88f, 0.15f), 0.4f, new Color(0.9f, 0.7f, 0.05f));

        static Material Unlit(string name, Color color, bool transparent)
        {
            var material = MaterialAsset(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            BaseShaderGUI.SetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Object Renderer(GameObject block) => block.GetComponent<MeshRenderer>();

        // + angles go to the player's right (clockwise seen from above), as in MechaFightConfig.Spawn.
        static Vector3 OnRing(float angle, float radius)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
        }

        #endregion
    }
}
