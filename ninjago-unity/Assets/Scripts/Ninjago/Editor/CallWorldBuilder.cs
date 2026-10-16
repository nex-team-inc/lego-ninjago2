#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Call's market street, all from primitives: stone paving between colorful stalls, the far gate the goblins come
    /// through, two ninjas, the big golden teammate waiting in the sky, and the goblin.
    /// </summary>
    public static class CallWorldBuilder
    {
        public const string ScenePath = KidsWorldBuilder.Root + "/CallScene.prefab";
        const string GoblinPath = KidsWorldBuilder.Root + "/Goblin.prefab";

        #region Entry Point

        public static void Build()
        {
            var goblin = KidsWorldBuilder.BuildGoblin(GoblinPath);
            var root = new GameObject("CallScene");
            var scene = root.AddComponent<CallScene>();
            BuildStreet(root.transform);
            var camera = KidsWorldBuilder.Camera(root.transform, new Vector3(0f, 3f, -6.2f), 9f, 52f);
            var ninjas = new List<Object> { KidsWorldBuilder.BuildNinja(root.transform, "NinjaLeft"), KidsWorldBuilder.BuildNinja(root.transform, "NinjaRight") };
            var pool = Empty("Goblins", root.transform, Vector3.zero).gameObject.AddComponent<StreetMonsterPool>();
            Set(scene, "sceneCamera", camera);
            SetArray(scene, "ninjas", ninjas);
            Set(scene, "teammate", BuildTeammate(root.transform));
            Set(scene, "goblinPool", pool);
            Set(scene, "goblinPrefab", goblin);
            Set(scene, "goblinColors", colors =>
            {
                var palette = new[]
                {
                    new Color(0.45f, 0.85f, 0.2f), new Color(0.6f, 0.3f, 0.95f), new Color(1f, 0.55f, 0.1f), new Color(0.1f, 0.8f, 0.75f), new Color(1f, 0.35f, 0.75f),
                };
                colors.arraySize = palette.Length;
                for (var i = 0; i < palette.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = palette[i];
            });
            SetLocalized(scene, "statusText", "ninjago.call.status");
            SetLocalized(scene, "introBanner", "ninjago.call.intro");
            SetLocalized(scene, "hintText", "ninjago.call.hint");
            SetLocalized(scene, "callPrompt", "ninjago.call.prompt");
            SetLocalized(scene, "teamBanner", "ninjago.call.team");
            SetLocalized(scene, "clearedBanner", "ninjago.call.cleared");
            SetLocalized(scene, "pileBanner", "ninjago.call.pile");
            SetLocalized(scene, "helpBanner", "ninjago.call.help");
            SetLocalized(scene, "endBanner", "ninjago.call.end");
            SetLocalized(scene, "countText", "ninjago.call.count");
            SavePrefab(root, ScenePath);
        }

        #endregion

        #region Street

        static void BuildStreet(Transform root)
        {
            Block(PrimitiveType.Cube, "Paving", root, new Vector3(0f, -0.25f, 14f), new Vector3(14f, 0.5f, 44f), Lit("Guard Stone", new Color(0.72f, 0.7f, 0.66f)));
            Block(PrimitiveType.Cube, "Grass", root, new Vector3(0f, -0.3f, 14f), new Vector3(40f, 0.5f, 46f), Lit("Crouch Grass", new Color(0.45f, 0.8f, 0.32f)));
            var awnings = new[]
            {
                Lit("Call Awning Red", new Color(0.95f, 0.3f, 0.3f)), Lit("Call Awning Blue", new Color(0.25f, 0.55f, 1f)),
                Lit("Call Awning Yellow", new Color(1f, 0.82f, 0.25f)), Lit("Call Awning Green", new Color(0.3f, 0.8f, 0.4f)),
            };
            var wood = Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f));
            var index = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                for (var z = -2f; z < 26f; z += 5f, index++)
                {
                    var stall = Empty("Stall", root, new Vector3(side * 5.2f, 0f, z));
                    Block(PrimitiveType.Cube, "Counter", stall, new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1f, 3.2f), wood);
                    foreach (var post in new[] { -1.4f, 1.4f })
                    {
                        Block(PrimitiveType.Cube, "Post", stall, new Vector3(side * 0.7f, 1.3f, post), new Vector3(0.12f, 2.6f, 0.12f), wood);
                    }

                    Block(PrimitiveType.Cube, "Awning", stall, new Vector3(0f, 2.65f, 0f), new Vector3(2.2f, 0.15f, 3.6f), awnings[index % awnings.Length],
                        new Vector3(0f, 0f, -side * 12f));
                }
            }

            var red = Lit("Crouch Gate Red", new Color(0.92f, 0.18f, 0.15f), 0.3f);
            var black = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            foreach (var x in new[] { -3.6f, 3.6f })
            {
                Block(PrimitiveType.Cylinder, "GatePost", root, new Vector3(x, 2.4f, 30f), new Vector3(0.6f, 2.4f, 0.6f), red);
            }

            Block(PrimitiveType.Cube, "GateTop", root, new Vector3(0f, 5.1f, 30f), new Vector3(9.6f, 0.5f, 0.8f), black);
            Block(PrimitiveType.Cube, "GateBeam", root, new Vector3(0f, 4.2f, 30f), new Vector3(8.4f, 0.4f, 0.5f), red);
            KidsWorldBuilder.Tree(root, new Vector3(-9f, 0f, 22f), 1.6f);
            KidsWorldBuilder.Tree(root, new Vector3(9f, 0f, 24f), 1.8f);
            KidsWorldBuilder.Lantern(root, new Vector3(-3.2f, 0f, 1.5f));
            KidsWorldBuilder.Lantern(root, new Vector3(3.2f, 0f, 1.5f));
        }

        #endregion

        #region Teammate

        // A big golden ninja: the same block ninja, bigger, with no player tag.
        static CallTeammate BuildTeammate(Transform parent)
        {
            var root = Empty("Teammate", parent, Vector3.zero);
            var teammate = root.gameObject.AddComponent<CallTeammate>();
            var ninja = KidsWorldBuilder.BuildNinja(root, "BigNinja");
            ninja.transform.localScale = Vector3.one * 1.6f;
            ninja.transform.Find("Marker").gameObject.SetActive(false);
            Set(teammate, "ninja", ninja);
            return teammate;
        }

        #endregion
    }
}
