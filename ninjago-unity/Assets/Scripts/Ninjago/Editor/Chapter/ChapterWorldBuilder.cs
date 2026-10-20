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
    /// The block stations placed along a level's route and the stage they play on. Station scenery uses the theme role
    /// materials, so every theme repaints the same stations; the stealth path reuses the Crouch Run pieces and the ride
    /// reuses the Chest Chase road, sky and vehicles.
    /// </summary>
    public static class ChapterWorldBuilder
    {
        public const string ArenaPath = ChapterActorsBuilder.Root + "/FightArena.prefab";
        public const string StealthPath = ChapterActorsBuilder.Root + "/StealthPath.prefab";
        public const string RunnerPath = ChapterActorsBuilder.Root + "/RunnerTrack.prefab";
        public const string StagePath = ChapterActorsBuilder.Root + "/ChapterStage.prefab";
        const string GroundPath = ChapterActorsBuilder.Root + "/ChapterGround.prefab";
        const string HeartsRowPath = NinjagoViewsBuilder.WidgetsRoot + "/ChapterHeartsRow.prefab";
        const string StripIconPath = NinjagoViewsBuilder.WidgetsRoot + "/ChapterStripIcon.prefab";
        const string CrouchRoot = PrefabsRoot + "/CrouchRun";

        #region Entry Point

        public static void Build()
        {
            BuildArena();
            BuildStealth();
            BuildRunner();
            BuildStage();
        }

        static Material Role(ThemeRole role) => Lit($"Theme {role}", role switch
        {
            ThemeRole.Ground => new Color(0.45f, 0.7f, 0.35f),
            ThemeRole.Path => new Color(0.86f, 0.78f, 0.62f),
            ThemeRole.Wall => new Color(0.86f, 0.32f, 0.22f),
            ThemeRole.Roof => new Color(0.12f, 0.62f, 0.5f),
            ThemeRole.Accent => new Color(1f, 0.8f, 0.3f),
            _ => new Color(0.15f, 0.6f, 0.25f),
        });

        static CinemachineCamera ViewCamera(Transform root, Vector3 position, Vector3 euler, float fieldOfView)
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

        #region Stage

        static void BuildStage()
        {
            var ground = new GameObject("ChapterGround");
            Block(PrimitiveType.Cube, "Plane", ground.transform, Vector3.zero, new Vector3(1f, 0.04f, 1f), Role(ThemeRole.Ground));
            var groundPrefab = SavePrefab(ground, GroundPath).transform;
            var heartsRow = BuildHeartsRow();
            var stripIcon = BuildStripIcon();

            var root = new GameObject("ChapterStage");
            var stage = root.AddComponent<ChapterStage>();
            var painter = root.AddComponent<ThemePainter>();
            var cameraObject = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 3f, -5f), Vector3.zero, 60f);
            cameraObject.name = "WorldCamera";
            cameraObject.farClipPlane = 400f;
            var brain = cameraObject.gameObject.AddComponent<CinemachineBrain>();
            var linkCamera = ViewCamera(root.transform, Vector3.zero, Vector3.zero, 60f);
            linkCamera.name = "LinkCamera";
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.8f;
            var route = Empty("Route", root.transform, Vector3.zero);

            var canvasRect = NewRect("StageCanvas", root.transform);
            canvasRect.gameObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var feed = Feed(canvasRect, "WorldFeed", Vector2.zero, Vector2.one);
            var hud = BuildHud(canvasRect, heartsRow, stripIcon);

            var roles = new List<ThemePainter.RoleMaterial>();
            foreach (ThemeRole role in System.Enum.GetValues(typeof(ThemeRole))) roles.Add(new ThemePainter.RoleMaterial { material = Role(role), role = role });
            void Map(string material, ThemeRole role) => roles.Add(new ThemePainter.RoleMaterial { material = Load<Material>($"{MaterialsRoot}/{material}.mat"), role = role });
            Map("Crouch Floor", ThemeRole.Path);
            Map("Crouch Wall Panel", ThemeRole.Wall);
            Map("Courtyard Roof", ThemeRole.Roof);
            Map("Crouch Grass", ThemeRole.Ground);
            Map("Crouch House Wall", ThemeRole.Wall);
            Map("Mecha Tree Leaves", ThemeRole.Foliage);
            Map("Mecha Hill", ThemeRole.Ground);
            Map("Crouch Gate Red", ThemeRole.Accent);
            Map("Road", ThemeRole.Path);
            Map("Grass", ThemeRole.Ground);
            Map("Tree Leaves", ThemeRole.Foliage);
            Map("Curb", ThemeRole.Accent);
            Map("Ground Far", ThemeRole.Ground);
            ChapterAssetsBuilder.SetField(painter, "roleMaterials", roles);

            Set(stage, "worldCamera", cameraObject);
            Set(stage, "brain", brain);
            Set(stage, "linkCamera", linkCamera);
            Set(stage, "sun", sun);
            Set(stage, "routeRoot", route);
            Set(stage, "stageCanvas", canvas);
            Set(stage, "feed", feed);
            Set(stage, "hud", hud);
            Set(stage, "painter", painter);
            Set(stage, "ninjaPrefab", LoadComponent<ChapterNinja>(ChapterActorsBuilder.NinjaPath));
            Set(stage, "groundPrefab", groundPrefab);
            SavePrefab(root, StagePath);
        }

        static ChapterHud BuildHud(RectTransform canvas, HeartsRow heartsRowPrefab, StripIcon stripIconPrefab)
        {
            var outline = OutlineTextMaterial();
            var rect = NewRect("Hud", canvas);
            Stretch(rect);
            var hud = rect.gameObject.AddComponent<ChapterHud>();
            var rows = new List<Object>();
            for (var i = 0; i < SimulatedBody.MaxPlayers; i++)
            {
                var row = (HeartsRow)UnityEditor.PrefabUtility.InstantiatePrefab(heartsRowPrefab, rect);
                Place((RectTransform)row.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f - i * 84f), new Vector2(560f, 76f));
                rows.Add(row);
            }

            var strip = NewRect("Strip", rect);
            Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 110f));
            var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var captionPanel = NewImage(rect, "Caption", GetSprite("Panel"), new Color(0.06f, 0.06f, 0.1f, 0.82f));
            captionPanel.type = Image.Type.Sliced;
            Place(captionPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1180f, 110f));
            var caption = Label(captionPanel.rectTransform, "Label", null, "It's running! Chase it!", 62f, Color.white, outline);
            Stretch((RectTransform)caption.transform);
            var pip = NinjagoViewsBuilder.CameraPreview(rect, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(384f, 216f), out var pipFrame);

            SetArray(hud, "heartsRows", rows);
            Set(hud, "stripRoot", strip);
            Set(hud, "stripIconPrefab", stripIconPrefab);
            Set(hud, "captionPanel", captionPanel.gameObject);
            Set(hud, "caption", caption);
            Set(hud, "pipFrame", pipFrame);
            Set(hud, "pipIndicators", pip);
            return hud;
        }

        static HeartsRow BuildHeartsRow()
        {
            var root = NewRect("ChapterHeartsRow", null);
            root.sizeDelta = new Vector2(560f, 76f);
            var row = root.gameObject.AddComponent<HeartsRow>();
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 64f);
            Place((RectTransform)tag.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 76f));
            var hearts = NewRect("Hearts", root);
            Place(hearts, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(-120f, 64f));
            var layout = hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            Set(row, "playerTag", tag);
            Set(row, "heartsRoot", hearts);
            Set(row, "heartPrefab", Load<GameObject>($"{NinjagoViewsBuilder.WidgetsRoot}/Heart.prefab").GetComponent<Image>());
            return SavePrefab(root.gameObject, HeartsRowPath).GetComponent<HeartsRow>();
        }

        static StripIcon BuildStripIcon()
        {
            var root = NewRect("ChapterStripIcon", null);
            root.sizeDelta = new Vector2(84f, 84f);
            var icon = root.gameObject.AddComponent<StripIcon>();
            var frame = NewImage(root, "Frame", GetSprite("Panel"), Color.white);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);
            var back = NewImage(root, "Back", GetSprite("Panel"), new Color(0.08f, 0.08f, 0.12f, 0.9f));
            back.type = Image.Type.Sliced;
            Place(back.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            var image = NewImage(root, "Icon", null, Color.white);
            Place(image.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-22f, -22f));
            Set(icon, "icon", image);
            Set(icon, "frame", frame);
            return SavePrefab(root.gameObject, StripIconPath).GetComponent<StripIcon>();
        }

        #endregion
    }
}
