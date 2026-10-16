#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Build's river crossing, all from primitives: this bank with two brick piles, the river below, the far bank with a
    /// shut gate, the two ninjas at the edge, and the bridge brick.
    /// </summary>
    public static class BuildWorldBuilder
    {
        public const string ScenePath = KidsWorldBuilder.Root + "/BuildScene.prefab";
        const string BrickPath = KidsWorldBuilder.Root + "/BuildBrick.prefab";
        const float Edge = 0.8f;
        // Ten rows of 0.64 m; the far bank starts here (BuildScene places the gate from its own row count).
        const float FarBank = Edge + 10f * 0.64f;

        #region Entry Point

        public static void Build()
        {
            var brick = BuildBrickPrefab();
            var root = new GameObject("BuildScene");
            var scene = root.AddComponent<BuildScene>();
            BuildBanks(root.transform);
            var camera = KidsWorldBuilder.Camera(root.transform, new Vector3(0f, 3.4f, -6f), 15f, 50f);
            var ninjas = new List<Object> { KidsWorldBuilder.BuildNinja(root.transform, "NinjaLeft"), KidsWorldBuilder.BuildNinja(root.transform, "NinjaRight") };
            var piles = new List<Object> { BuildPile(root.transform, "PileLeft", -2.7f), BuildPile(root.transform, "PileRight", 2.7f) };
            Set(scene, "sceneCamera", camera);
            SetArray(scene, "ninjas", ninjas);
            SetArray(scene, "piles", piles);
            Set(scene, "bridgeRoot", Empty("Bridge", root.transform, new Vector3(0f, 0f, Edge)));
            Set(scene, "brickPrefab", brick);
            Set(scene, "gate", BuildGate(root.transform));
            SetLocalized(scene, "statusText", "ninjago.build.status");
            SetLocalized(scene, "introBanner", "ninjago.build.intro");
            SetLocalized(scene, "hintText", "ninjago.build.hint");
            SetLocalized(scene, "openBanner", "ninjago.build.open");
            SetLocalized(scene, "timeUpBanner", "ninjago.build.time_up");
            SetLocalized(scene, "countText", "ninjago.build.count");
            SavePrefab(root, ScenePath);
        }

        #endregion

        #region Banks

        static void BuildBanks(Transform root)
        {
            var grass = Lit("Crouch Grass", new Color(0.45f, 0.8f, 0.32f));
            var cliff = Lit("Build Cliff", new Color(0.62f, 0.45f, 0.3f));
            var water = Lit("Build Water", new Color(0.2f, 0.6f, 1f), 0.8f, new Color(0.02f, 0.08f, 0.15f));
            Block(PrimitiveType.Cube, "NearBank", root, new Vector3(0f, -1f, Edge - 6f), new Vector3(40f, 2f, 12f), grass);
            Block(PrimitiveType.Cube, "FarBank", root, new Vector3(0f, -1f, FarBank + 15f), new Vector3(40f, 2f, 30f), grass);
            Block(PrimitiveType.Cube, "NearCliff", root, new Vector3(0f, -1.1f, Edge + 0.05f), new Vector3(40f, 2f, 0.1f), cliff);
            Block(PrimitiveType.Cube, "FarCliff", root, new Vector3(0f, -1.1f, FarBank - 0.05f), new Vector3(40f, 2f, 0.1f), cliff);
            Block(PrimitiveType.Cube, "River", root, new Vector3(0f, -1.8f, (Edge + FarBank) * 0.5f), new Vector3(40f, 0.2f, FarBank - Edge), water);
            var wood = Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f));
            foreach (var z in new[] { Edge - 0.15f, FarBank + 0.15f })
            {
                foreach (var x in new[] { -1.55f, 1.55f })
                {
                    Block(PrimitiveType.Cube, "BridgePost", root, new Vector3(x, 0.35f, z), new Vector3(0.22f, 1f, 0.22f), wood);
                }
            }

            KidsWorldBuilder.House(root, new Vector3(-6.5f, 0f, FarBank + 7f), 30f, Lit("Crouch House Wall", new Color(0.98f, 0.9f, 0.72f)));
            KidsWorldBuilder.House(root, new Vector3(7f, 0f, FarBank + 9f), -25f, Lit("Punch Wall Coral", new Color(1f, 0.45f, 0.38f)));
            KidsWorldBuilder.Tree(root, new Vector3(-3.5f, 0f, FarBank + 12f), 1.6f);
            KidsWorldBuilder.Tree(root, new Vector3(4f, 0f, FarBank + 14f), 1.8f);
            KidsWorldBuilder.Tree(root, new Vector3(-8f, 0f, -2f), 1.3f);
            KidsWorldBuilder.Tree(root, new Vector3(8.5f, 0f, -1f), 1.4f);
        }

        // A pyramid of spare bricks beside the edge; the pile's transform is its top, where flying bricks start.
        static Transform BuildPile(Transform parent, string name, float x)
        {
            var root = Empty(name, parent, new Vector3(x, 0f, -0.4f));
            var materials = new[]
            {
                Lit("Build Pile Yellow", new Color(1f, 0.82f, 0.2f), 0.4f), Lit("Build Pile Green", new Color(0.3f, 0.8f, 0.35f), 0.4f),
                Lit("Build Pile Orange", new Color(1f, 0.55f, 0.15f), 0.4f),
            };
            var index = 0;
            for (var layer = 0; layer < 3; layer++)
            {
                for (var i = 0; i < 3 - layer; i++, index++)
                {
                    KidsWorldBuilder.Brick(root, "Brick", new Vector3((i - (2 - layer) * 0.5f) * 0.85f, 0.2f + layer * 0.45f, 0f), 1f, materials[index % materials.Length]);
                }
            }

            var top = Empty("Top", root, new Vector3(0f, 1.6f, 0f));
            return top;
        }

        // A red frame with two wooden doors hinged on their outer edges; BuildScene places it on the far bank.
        static BuildGate BuildGate(Transform parent)
        {
            var root = Empty("Gate", parent, new Vector3(0f, 0f, FarBank + 0.8f));
            var gate = root.gameObject.AddComponent<BuildGate>();
            var red = Lit("Crouch Gate Red", new Color(0.92f, 0.18f, 0.15f), 0.3f);
            var black = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var door = Lit("Build Door", new Color(0.55f, 0.32f, 0.16f));
            foreach (var x in new[] { -1.9f, 1.9f })
            {
                Block(PrimitiveType.Cylinder, "Post", root, new Vector3(x, 1.7f, 0f), new Vector3(0.42f, 1.7f, 0.42f), red);
            }

            Block(PrimitiveType.Cube, "Top", root, new Vector3(0f, 3.6f, 0f), new Vector3(5f, 0.36f, 0.6f), black);
            Block(PrimitiveType.Cube, "Beam", root, new Vector3(0f, 3.1f, 0f), new Vector3(4.4f, 0.3f, 0.4f), red);
            var leftDoor = Empty("LeftDoor", root, new Vector3(-1.7f, 0f, 0f));
            Block(PrimitiveType.Cube, "Panel", leftDoor, new Vector3(0.85f, 1.4f, 0f), new Vector3(1.7f, 2.8f, 0.14f), door);
            var rightDoor = Empty("RightDoor", root, new Vector3(1.7f, 0f, 0f));
            Block(PrimitiveType.Cube, "Panel", rightDoor, new Vector3(-0.85f, 1.4f, 0f), new Vector3(1.7f, 2.8f, 0.14f), door);
            Set(gate, "leftDoor", leftDoor);
            Set(gate, "rightDoor", rightDoor);
            return gate;
        }

        #endregion

        #region Brick

        // A big 2x4 brick, long across the bridge, tinted in the builder's color.
        static BuildBrick BuildBrickPrefab()
        {
            var root = new GameObject("BuildBrick");
            var brick = root.AddComponent<BuildBrick>();
            var renderers = new List<Object>();
            KidsWorldBuilder.Brick(root.transform, "Brick", Vector3.zero, 1.6f, Lit("Build Brick Tint", Color.white, 0.4f), renderers);
            SetArray(brick, "tintRenderers", renderers);
            return SavePrefab(root, BrickPath).GetComponent<BuildBrick>();
        }

        #endregion
    }
}
