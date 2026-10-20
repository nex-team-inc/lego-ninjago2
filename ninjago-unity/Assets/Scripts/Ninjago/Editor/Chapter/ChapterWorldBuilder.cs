#nullable enable

using System.Collections.Generic;
using Nex.Util;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The block stations placed along a level's route. Station scenery uses the theme role
    /// materials, so every theme repaints the same stations; the stealth path reuses the Crouch Run pieces and the ride
    /// reuses the Chest Chase road, sky and vehicles.
    /// </summary>
    public static class ChapterWorldBuilder
    {
        public const string ArenaPath = ChapterActorsBuilder.Root + "/FightArena.prefab";
        public const string StealthPath = ChapterActorsBuilder.Root + "/StealthPath.prefab";
        public const string RunnerPath = ChapterActorsBuilder.Root + "/RunnerTrack.prefab";
        const string CrouchRoot = PrefabsRoot + "/CrouchRun";

        #region Entry Point

        public static void Build()
        {
            BuildArena();
            BuildStealth();
            BuildRunner();
            ChapterStageBuilder.Build();
        }

        internal static Material Role(ThemeRole role) => Lit($"Theme {role}", role switch
        {
            ThemeRole.Ground => new Color(0.45f, 0.7f, 0.35f),
            ThemeRole.Path => new Color(0.86f, 0.78f, 0.62f),
            ThemeRole.Wall => new Color(0.86f, 0.32f, 0.22f),
            ThemeRole.Roof => new Color(0.12f, 0.62f, 0.5f),
            ThemeRole.Accent => new Color(1f, 0.8f, 0.3f),
            _ => new Color(0.15f, 0.6f, 0.25f),
        });

        internal static CinemachineCamera ViewCamera(Transform root, Vector3 position, Vector3 euler, float fieldOfView)
        {
            var go = new GameObject("ViewCamera");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            var camera = go.AddComponent<CinemachineCamera>();
            var lens = camera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 400f;
            camera.Lens = lens;
            return camera;
        }

        static void Texts(Object target, params (string field, string key)[] texts)
        {
            foreach (var (field, key) in texts) SetLocalized(target, field, key);
        }

        #endregion

        #region Fight Arena

        static void BuildArena()
        {
            var root = new GameObject("FightArena");
            var block = root.AddComponent<FightBlock>();
            var camera = ViewCamera(root.transform, new Vector3(0f, 2.8f, 2f), new Vector3(13f, 0f, 0f), 58f);
            Transform Spot(string name, Vector3 position) => Empty(name, root.transform, position);
            var solo = Spot("SoloSpot", new Vector3(0f, 0f, 6f));
            var duo = new Object[] { Spot("DuoSpot0", new Vector3(-1.3f, 0f, 6f)), Spot("DuoSpot1", new Vector3(1.3f, 0f, 6f)) };
            var soloHang = Spot("SoloHang", new Vector3(0f, 1.5f, 7.8f));
            var duoHangs = new Object[] { Spot("DuoHang0", new Vector3(-1.3f, 1.5f, 7.8f)), Spot("DuoHang1", new Vector3(1.3f, 1.5f, 7.8f)) };
            var soloPose = Spot("SoloCameraPose", new Vector3(0f, 2.8f, 2f));
            soloPose.localRotation = Quaternion.Euler(13f, 0f, 0f);
            var duoPose = Spot("DuoCameraPose", new Vector3(0f, 3.4f, 0.6f));
            duoPose.localRotation = Quaternion.Euler(15f, 0f, 0f);
            var bossPose = Spot("BossCameraPose", new Vector3(0f, 2.3f, 0.2f));
            bossPose.localRotation = Quaternion.Euler(-3f, 0f, 0f);
            var crowd = new List<Object>();
            foreach (var position in new[]
                     {
                         new Vector3(-3.2f, 0f, 12.5f), new Vector3(3.2f, 0f, 12.5f), new Vector3(-1.6f, 0f, 14.2f), new Vector3(1.6f, 0f, 14.2f),
                         new Vector3(-4.6f, 0f, 15.2f), new Vector3(4.6f, 0f, 15.2f), new Vector3(0f, 0f, 16.2f),
                     })
            {
                crowd.Add(Spot($"Crowd{crowd.Count}", position));
            }

            var boss = Spot("BossSpot", new Vector3(0f, 0f, 19.5f));
            BuildArenaScenery(root.transform);

            Set(block, "viewCamera", camera);
            Set(block, "routeLength", property => property.floatValue = 28f);
            Set(block, "soloSpot", solo);
            SetArray(block, "duoSpots", duo);
            Set(block, "soloCameraPose", soloPose);
            Set(block, "duoCameraPose", duoPose);
            Set(block, "bossCameraPose", bossPose);
            Set(block, "soloHang", soloHang);
            SetArray(block, "duoHangs", duoHangs);
            SetArray(block, "crowdSpots", crowd);
            Set(block, "bossSpot", boss);
            Set(block, "enemyPrefab", LoadComponent<ChapterEnemy>(ChapterActorsBuilder.EnemyPath));
            Set(block, "bossPrefab", LoadComponent<ChapterEnemy>(ChapterActorsBuilder.BossPath));
            Set(block, "rockPrefab", Load<GameObject>(ChapterActorsBuilder.RockPath).transform);
            Set(block, "piecesPrefab", LoadComponent<RockPieces>(ChapterActorsBuilder.StonesPath));
            Set(block, "markerPrefab", LoadComponent<WindowMarker>(ChapterActorsBuilder.MarkerPath));
            Set(block, "fightConfig", Load<FightConfig>(NinjagoAssetsBuilder.FightConfigPath));
            Set(block, "stoneKickConfig", Load<StoneKickConfig>(ConfigsRoot + "/StoneKickConfig.asset"));
            Set(block, "onePunchConfig", Load<OnePunchConfig>(OnePunchBuilder.ConfigPath));
            Texts(block, ("kickText", "chapter.fight.kick"), ("wiggleText", "chapter.fight.wiggle"));
            SavePrefab(root, ArenaPath);
        }

        // An open plaza along the route: walls and roofs on three sides, lanterns, trees outside.
        static void BuildArenaScenery(Transform root)
        {
            var scenery = Empty("Scenery", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Plaza", scenery, new Vector3(0f, -0.05f, 13f), new Vector3(16f, 0.1f, 26f), Role(ThemeRole.Path));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "SideWall", scenery, new Vector3(side * 8.5f, 1.2f, 14f), new Vector3(0.6f, 2.4f, 22f), Role(ThemeRole.Wall));
                Block(PrimitiveType.Cube, "SideRoof", scenery, new Vector3(side * 8.6f, 2.55f, 14f), new Vector3(1.6f, 0.3f, 23f), Role(ThemeRole.Roof));
                for (var z = 4f; z <= 24f; z += 5f)
                {
                    Block(PrimitiveType.Cube, "Post", scenery, new Vector3(side * 7.4f, 0.9f, z), new Vector3(0.25f, 1.8f, 0.25f), Role(ThemeRole.Wall));
                    Block(PrimitiveType.Cube, "Lantern", scenery, new Vector3(side * 7.4f, 2f, z), new Vector3(0.5f, 0.5f, 0.5f), Role(ThemeRole.Accent));
                }

                for (var z = 2f; z <= 26f; z += 6f)
                {
                    Block(PrimitiveType.Cube, "Trunk", scenery, new Vector3(side * 11f, 0.6f, z), new Vector3(0.4f, 1.2f, 0.4f), Role(ThemeRole.Wall));
                    Block(PrimitiveType.Cube, "Leaves", scenery, new Vector3(side * 11f, 2f, z), new Vector3(2f, 1.8f, 2f), Role(ThemeRole.Foliage));
                }
            }

            Block(PrimitiveType.Cube, "BackWall", scenery, new Vector3(0f, 2f, 25.5f), new Vector3(17f, 4f, 0.8f), Role(ThemeRole.Wall));
            Block(PrimitiveType.Cube, "BackRoof", scenery, new Vector3(0f, 4.3f, 25.3f), new Vector3(18.5f, 0.5f, 2.2f), Role(ThemeRole.Roof));
            Block(PrimitiveType.Cube, "GateL", scenery, new Vector3(-2.2f, 1.6f, 25f), new Vector3(0.6f, 3.2f, 1.2f), Role(ThemeRole.Accent));
            Block(PrimitiveType.Cube, "GateR", scenery, new Vector3(2.2f, 1.6f, 25f), new Vector3(0.6f, 3.2f, 1.2f), Role(ThemeRole.Accent));
        }

        #endregion

        #region Stealth Path

        static void BuildStealth()
        {
            var root = new GameObject("StealthPath");
            var block = root.AddComponent<StealthBlock>();
            var camera = ViewCamera(root.transform, new Vector3(11.5f, 2.2f, 4f), new Vector3(0f, -90f, 0f), 34f);
            // The path runs along this pivot's +X, which is the station's +Z (the route direction).
            var path = Empty("Path", root.transform, Vector3.zero);
            path.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var levelHost = Empty("Level", path, Vector3.zero);
            var builder = levelHost.gameObject.AddComponent<CrouchLevelBuilder>();
            Set(builder, "levelRoot", Empty("Pieces", levelHost, Vector3.zero));
            Set(builder, "floorPrefab", Load<GameObject>($"{CrouchRoot}/Floor.prefab").transform);
            Set(builder, "floorSeamPrefab", Load<GameObject>($"{CrouchRoot}/FloorSeam.prefab").transform);
            Set(builder, "wallPrefab", Load<GameObject>($"{CrouchRoot}/Wall.prefab").transform);
            Set(builder, "windowWallPrefab", Load<GameObject>($"{CrouchRoot}/WindowWall.prefab").transform);
            Set(builder, "windowPostPrefab", Load<GameObject>($"{CrouchRoot}/WindowPost.prefab").transform);
            Set(builder, "sunlightPrefab", Load<GameObject>($"{CrouchRoot}/Sunlight.prefab").transform);
            Set(builder, "pillarPrefab", Load<GameObject>($"{CrouchRoot}/Pillar.prefab").transform);
            Set(builder, "scrollPrefab", Load<GameObject>($"{CrouchRoot}/Scroll.prefab").transform);
            Set(builder, "monsterPrefab", LoadComponent<CrouchMonster>($"{CrouchRoot}/CrouchMonster.prefab"));
            Set(builder, "gatePrefab", Load<GameObject>($"{CrouchRoot}/Gate.prefab").transform);
            Set(builder, "groundPrefab", Load<GameObject>($"{CrouchRoot}/Ground.prefab").transform);
            Set(builder, "treePrefab", Load<GameObject>($"{CrouchRoot}/Tree.prefab").transform);
            Set(builder, "housePrefab", Load<GameObject>($"{CrouchRoot}/House.prefab").transform);
            Set(builder, "hillPrefab", Load<GameObject>($"{CrouchRoot}/Hill.prefab").transform);
            Set(builder, "sceneryMargin", property => property.floatValue = 12f);
            // The side camera is far back, so its signs are bigger than the prompts behind a ninja.
            var zoneSign = ChapterActorsBuilder.Prompt(root.transform, "ZoneSign", new Vector3(0f, 3.1f, 0f));
            zoneSign.transform.localScale = Vector3.one * 2.2f;
            var clock = ChapterActorsBuilder.Prompt(root.transform, "Clock", new Vector3(0f, 4.3f, 0f));
            clock.transform.localScale = Vector3.one * 1.6f;

            var looks = ChapterAssetsBuilder.Field<EnumDictionary<CrouchZoneState, StealthBlock.ZoneLook>>(block, "looks");
            looks[CrouchZoneState.Clear] = new StealthBlock.ZoneLook
            {
                title = ChapterAssetsBuilder.Loc("chapter.stealth.clear"), instruction = ChapterAssetsBuilder.Loc("chapter.stealth.stand_to_run"),
                color = new Color(0.25f, 0.8f, 0.3f),
            };
            looks[CrouchZoneState.Talking] = new StealthBlock.ZoneLook
            {
                title = ChapterAssetsBuilder.Loc("chapter.stealth.talking"), instruction = ChapterAssetsBuilder.Loc("chapter.stealth.crouch"),
                color = new Color(0.95f, 0.28f, 0.3f),
            };
            Set(block, "viewCamera", camera);
            Set(block, "routeLength", property => property.floatValue = 12f);
            Set(block, "pathRoot", path);
            Set(block, "levelBuilder", builder);
            Set(block, "crouchConfig", Load<CrouchRunConfig>(CrouchRunBuilder.ConfigPath));
            Set(block, "zoneSign", zoneSign);
            Set(block, "clock", clock);
            Texts(block, ("clockText", "chapter.stealth.clock"), ("heardText", "chapter.stealth.heard"), ("timeUpText", "chapter.stealth.time_up"));
            SavePrefab(root, StealthPath);
        }

        #endregion

        #region Runner Track

        static void BuildRunner()
        {
            var root = new GameObject("RunnerTrack");
            var block = root.AddComponent<RunnerBlock>();
            var worldRoot = Empty("World", root.transform, Vector3.zero);
            var world = worldRoot.gameObject.AddComponent<RunnerWorld>();
            var cameraObject = NinjagoWorldBuilder.NewCamera(worldRoot, new Vector3(0f, 3f, -6.5f), new Vector3(8.8f, 0f, 0f), 60f);
            cameraObject.enabled = false;
            var view = cameraObject.gameObject.AddComponent<CinemachineCamera>();
            var lens = view.Lens;
            lens.FieldOfView = 60f;
            lens.FarClipPlane = 400f;
            view.Lens = lens;
            var carPose = Empty("CarCameraPose", worldRoot, new Vector3(0f, 3f, -6.5f));
            carPose.localRotation = Quaternion.Euler(8.8f, 0f, 0f);
            var skyPose = Empty("SkyCameraPose", worldRoot, new Vector3(0f, 3.9f, -5f));
            skyPose.localRotation = Quaternion.Euler(1f, 0f, 0f);
            var road = NinjagoWorldBuilder.BuildRoad(worldRoot, out var roadStrip);
            var sky = NinjagoWorldBuilder.BuildSky(worldRoot, out var skyStrip);
            var car = NinjagoWorldBuilder.BuildCar(worldRoot);
            var skycraft = NinjagoWorldBuilder.BuildSkycraft(worldRoot);
            var barrierPool = Empty("BarrierPool", worldRoot, Vector3.zero).gameObject.AddComponent<ObstacleBlockPool>();
            var floatingPool = Empty("FloatingBlockPool", worldRoot, Vector3.zero).gameObject.AddComponent<ObstacleBlockPool>();
            Set(world, "worldCamera", cameraObject);
            Set(world, "carCameraPose", carPose);
            Set(world, "skyCameraPose", skyPose);
            Set(world, "car", car);
            Set(world, "skycraft", skycraft);
            Set(world, "roadScenery", road);
            Set(world, "skyScenery", sky);
            Set(world, "roadStrip", roadStrip);
            Set(world, "skyStrip", skyStrip);
            Set(world, "barrierPool", barrierPool);
            Set(world, "barrierPrefab", LoadComponent<ObstacleBlock>(NinjagoWorldBuilder.BarrierPath));
            Set(world, "floatingBlockPool", floatingPool);
            Set(world, "floatingBlockPrefab", LoadComponent<ObstacleBlock>(NinjagoWorldBuilder.FloatingBlockPath));

            var bossRunner = Empty("BossRunner", worldRoot, new Vector3(0f, 0f, 22f));
            var figure = Empty("Figure", bossRunner, Vector3.zero);
            figure.localScale = Vector3.one * 2.2f;
            var parts = ChapterActorsBuilder.BuildEnemyBody(figure, true);
            foreach (var part in parts.tinted) ((Renderer)part).sharedMaterial = Lit("Chapter Boss Body", new Color(0.35f, 0.3f, 0.4f));
            var flyer = Empty("BossFlyer", worldRoot, new Vector3(0f, 3f, 28f));
            var glider = Lit("Chapter Boss Flyer", new Color(0.25f, 0.2f, 0.3f), 0.4f, new Color(0.8f, 0.2f, 1f));
            Block(PrimitiveType.Cube, "Deck", flyer, Vector3.zero, new Vector3(3.4f, 0.2f, 2.4f), glider);
            Block(PrimitiveType.Cube, "WingL", flyer, new Vector3(-2.6f, 0.1f, -0.2f), new Vector3(2.4f, 0.1f, 1.2f), glider, new Vector3(0f, 0f, 12f));
            Block(PrimitiveType.Cube, "WingR", flyer, new Vector3(2.6f, 0.1f, -0.2f), new Vector3(2.4f, 0.1f, 1.2f), glider, new Vector3(0f, 0f, -12f));
            var prompt = ChapterActorsBuilder.Prompt(root.transform, "VehiclePrompt", new Vector3(0f, 2.4f, 0f));
            prompt.transform.localScale = Vector3.one * 1.5f;

            Set(block, "viewCamera", view);
            Set(block, "routeLength", property => property.floatValue = 135f);
            Set(block, "world", world);
            Set(block, "chaseConfig", Load<ChaseConfig>(NinjagoAssetsBuilder.ChaseConfigPath));
            Set(block, "vehiclePrompt", prompt);
            Set(block, "bossRunner", bossRunner);
            Set(block, "bossFlyer", flyer);
            Texts(block, ("escapeText", "chapter.runner.escape"));
            SavePrefab(root, RunnerPath);
        }

        #endregion
    }
}
