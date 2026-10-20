#nullable enable

using System.Collections.Generic;
using System.Reflection;
using Nex.Util;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The Forge vocabulary assets: block icons, the ForgeCatalog (block types, vehicles, themes, links, hint words),
    /// ChapterTuning, ForgeConfig and the GameConfigsManager references. Data a designer tunes is written only into new
    /// assets; prefab and sprite references are refreshed on every run.
    /// </summary>
    public static class ChapterAssetsBuilder
    {
        public const string ChapterConfigsRoot = ConfigsRoot + "/Chapter";
        public const string CatalogPath = ChapterConfigsRoot + "/ForgeCatalog.asset";
        public const string TuningPath = ChapterConfigsRoot + "/ChapterTuning.asset";
        public const string ForgeConfigPath = ChapterConfigsRoot + "/ForgeConfig.asset";

        #region Entry Point

        public static void Build()
        {
            BuildSprites();
            var catalog = BuildCatalog();
            var tuning = LoadOrCreate<ChapterTuning>(TuningPath);
            var forge = BuildForgeConfig();
            EditorUtility.SetDirty(tuning);
            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                var manager = root.GetComponent<GameConfigsManager>();
                Set(manager, "forgeCatalog", catalog);
                Set(manager, "chapterTuning", tuning);
                Set(manager, "forgeConfig", forge);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A localized string reference to this LocalizationTable key, for writing into assets from C#.</summary>
        public static LocalizedString Loc(string key)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            return new LocalizedString(collection.SharedData.TableCollectionNameGuid, KeyId(key));
        }

        internal static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

        internal static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        #endregion

        #region Sprites

        static void BuildSprites()
        {
            NinjagoAssetsBuilder.BuildSprite("BlockStealth", 128, (u, v) =>
                new Vector2(u, v).magnitude <= 0.85f && new Vector2(u - 0.35f, v - 0.22f).magnitude > 0.66f, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("BlockRunner", 128, (u, v) => Chevron(u + 0.35f, v) || Chevron(u - 0.25f, v), Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("BlockSlashKick", 128, (u, v) =>
                (Mathf.Abs(u + v) < 0.2f && new Vector2(u, v).magnitude < 0.92f) || new Vector2(u - 0.5f, v - 0.5f).magnitude < 0.2f, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("BlockSlipSpin", 128, (u, v) =>
            {
                var r = new Vector2(u, v).magnitude;
                var angle = Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 0.5f;
                var inner = 0.35f + angle * 0.4f;
                return r >= inner && r <= inner + 0.2f && angle > 0.08f;
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("Check", 128, (u, v) =>
                (Mathf.Abs(v - (-0.35f - (u + 0.35f))) < 0.16f && u > -0.75f && u < -0.3f) || (Mathf.Abs(v - (-0.65f + (u + 0.3f) * 1.3f)) < 0.16f && u >= -0.4f && u < 0.75f),
                Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("Star", 128, (u, v) =>
            {
                var angle = Mathf.Atan2(v, u) + Mathf.PI / 2f;
                var spoke = Mathf.Abs(Mathf.Repeat(angle, Mathf.PI * 2f / 5f) - Mathf.PI / 5f) / (Mathf.PI / 5f);
                return new Vector2(u, v).magnitude <= Mathf.Lerp(0.95f, 0.42f, 1f - spoke);
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("IconBoss", 128, (u, v) =>
                (v < -0.1f && v > -0.7f && Mathf.Abs(u) < 0.75f) || (v >= -0.1f && v < 0.75f && Mathf.Abs(Mathf.Repeat(u + 0.75f, 0.5f) - 0.25f) < 0.25f - (v + 0.1f) * 0.29f && Mathf.Abs(u) < 0.75f),
                Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("IconLightning", 128, (u, v) =>
                (v > 0f && v < 0.9f && u > -0.35f + v * 0.2f && u < 0.05f + v * 0.35f) || (v <= 0.05f && v > -0.9f && u < 0.35f + v * 0.2f && u > -0.05f + v * 0.35f),
                Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("IconDice", 128, (u, v) =>
            {
                var box = Mathf.Abs(u) < 0.8f && Mathf.Abs(v) < 0.8f;
                var dot = new Vector2(u, v).magnitude < 0.15f || new Vector2(u - 0.42f, v - 0.42f).magnitude < 0.15f || new Vector2(u + 0.42f, v + 0.42f).magnitude < 0.15f;
                return box && !dot;
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("IconBrick", 128, (u, v) =>
                (Mathf.Abs(u) < 0.85f && v > -0.55f && v < 0.3f) || (v >= 0.3f && v < 0.6f && (Mathf.Abs(u - 0.42f) < 0.2f || Mathf.Abs(u + 0.42f) < 0.2f)), Vector4.zero);
        }

        static bool Chevron(float u, float v) => Mathf.Abs(v) < 0.75f && u < 0.3f - Mathf.Abs(v) * 0.6f && u > 0.3f - Mathf.Abs(v) * 0.6f - 0.32f;

        #endregion

        #region Catalog

        static ForgeCatalog BuildCatalog()
        {
            var catalog = LoadOrCreate<ForgeCatalog>(CatalogPath);
            var blocks = Field<EnumDictionary<BlockType, ForgeCatalog.BlockSpec>>(catalog, "blocks");
            var icons = Field<EnumDictionary<ModeIcon, Sprite?>>(catalog, "icons");
            // Only a catalog with no vocabulary yet is filled in, so a designer's tuning survives rebuilds.
            if (blocks[BlockType.Stealth] == null || blocks[BlockType.Stealth].startStates == LinkState.None) WriteVocabulary(catalog, blocks);
            blocks[BlockType.Stealth].prefab = LoadComponent<StealthBlock>(ChapterWorldBuilder.StealthPath);
            blocks[BlockType.Runner].prefab = LoadComponent<RunnerBlock>(ChapterWorldBuilder.RunnerPath);
            blocks[BlockType.SlashKick].prefab = LoadComponent<FightBlock>(ChapterWorldBuilder.ArenaPath);
            blocks[BlockType.SlipSpin].prefab = LoadComponent<FightBlock>(ChapterWorldBuilder.ArenaPath);
            blocks[BlockType.Stealth].icon = NinjagoAssetsBuilder.GetSprite("BlockStealth");
            blocks[BlockType.Runner].icon = NinjagoAssetsBuilder.GetSprite("BlockRunner");
            blocks[BlockType.SlashKick].icon = NinjagoAssetsBuilder.GetSprite("BlockSlashKick");
            blocks[BlockType.SlipSpin].icon = NinjagoAssetsBuilder.GetSprite("BlockSlipSpin");
            icons[ModeIcon.Ninja] = NinjagoAssetsBuilder.GetSprite("Circle");
            icons[ModeIcon.Boss] = NinjagoAssetsBuilder.GetSprite("IconBoss");
            icons[ModeIcon.Lightning] = NinjagoAssetsBuilder.GetSprite("IconLightning");
            icons[ModeIcon.Dice] = NinjagoAssetsBuilder.GetSprite("IconDice");
            icons[ModeIcon.Brick] = NinjagoAssetsBuilder.GetSprite("IconBrick");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void WriteVocabulary(ForgeCatalog catalog, EnumDictionary<BlockType, ForgeCatalog.BlockSpec> blocks)
        {
            blocks[BlockType.Stealth] = Spec("chapter.block.stealth", new Color(0.45f, 0.55f, 1f), LinkState.AtWall | LinkState.OnRoof | LinkState.Landed,
                LinkState.Ambush | LinkState.GuardAhead, LinkState.None, LinkState.None, TwoPlayerShare.TeamSneak, 60f, new[] { Gesture.Crouch },
                Preset(1f, 1f, 4, 16f), Preset(1f, 1f, 4, 14f), Preset(1f, 1f, 5, 11f));
            blocks[BlockType.Stealth].sideView = true;
            blocks[BlockType.Stealth].allowedInBossLevel = false;
            blocks[BlockType.Runner] = Spec("chapter.block.runner", new Color(1f, 0.68f, 0.2f), LinkState.EnemyFled | LinkState.Airborne,
                LinkState.AtWall | LinkState.Landed | LinkState.OnRoof, LinkState.BossBroken, LinkState.None, TwoPlayerShare.SharedVehicle, 55f,
                new[] { Gesture.Lean }, Preset(1f, 0.9f, 4, 0f), Preset(1f, 1f, 5, 0f), Preset(1f, 1.15f, 6, 0f));
            blocks[BlockType.Runner].usesVehicle = true;
            blocks[BlockType.SlashKick] = Spec("chapter.block.slash_kick", new Color(1f, 0.35f, 0.3f), LinkState.Ambush | LinkState.CrowdIncoming | LinkState.Landed,
                LinkState.EnemyFled | LinkState.CrowdIncoming | LinkState.AtWall, LinkState.BossPhase, LinkState.BossPhase | LinkState.BossBroken,
                TwoPlayerShare.OwnThreats, 60f, new[] { Gesture.Slash, Gesture.Kick }, Preset(1.4f, 0.85f, 6, 0f), Preset(1f, 1f, 7, 0f), Preset(0.8f, 1.15f, 8, 0f));
            blocks[BlockType.SlashKick].isFight = true;
            blocks[BlockType.SlashKick].patternLetter = "K";
            blocks[BlockType.SlipSpin] = Spec("chapter.block.slip_spin", new Color(0.3f, 0.85f, 0.5f), LinkState.GuardAhead | LinkState.AtWall | LinkState.Landed,
                LinkState.CrowdIncoming | LinkState.EnemyFled, LinkState.BossPhase, LinkState.BossPhase | LinkState.BossBroken, TwoPlayerShare.OwnThreats, 55f,
                new[] { Gesture.Slip, Gesture.Spin }, Preset(1.4f, 0.85f, 8, 0f), Preset(1f, 1f, 9, 0f), Preset(0.8f, 1.15f, 10, 0f));
            blocks[BlockType.SlipSpin].isFight = true;
            blocks[BlockType.SlipSpin].patternLetter = "S";

            var vehicles = Field<EnumDictionary<VehicleType, LinkState>>(catalog, "vehicleEndStates");
            vehicles[VehicleType.Car] = LinkState.AtWall;
            vehicles[VehicleType.Skycraft] = LinkState.Landed | LinkState.OnRoof;

            var hints = Field<EnumDictionary<Gesture, LocalizedString>>(catalog, "hintWords");
            hints[Gesture.Crouch] = Loc("chapter.hint.crouch");
            hints[Gesture.Lean] = Loc("chapter.hint.lean");
            hints[Gesture.Slash] = Loc("chapter.hint.slash");
            hints[Gesture.Kick] = Loc("chapter.hint.kick");
            hints[Gesture.Slip] = Loc("chapter.hint.slip");
            hints[Gesture.Spin] = Loc("chapter.hint.spin");
            hints[Gesture.Wiggle] = Loc("chapter.hint.wiggle");

            var themes = Field<EnumDictionary<ChapterTheme, ForgeCatalog.ThemeSpec>>(catalog, "themes");
            themes[ChapterTheme.TownDusk] = new ForgeCatalog.ThemeSpec
            {
                displayName = Loc("chapter.theme.town_dusk"), music = BgmManager.BgmType.NinjaFight, sky = new Color(1f, 0.62f, 0.45f),
                ground = new Color(0.55f, 0.62f, 0.32f), path = new Color(0.88f, 0.72f, 0.52f), wall = new Color(0.95f, 0.48f, 0.3f), roof = new Color(0.22f, 0.32f, 0.6f),
                accent = new Color(1f, 0.78f, 0.3f), foliage = new Color(0.3f, 0.52f, 0.22f), fog = new Color(1f, 0.7f, 0.55f), fogDensity = 0.006f,
                light = new Color(1f, 0.75f, 0.55f), lightIntensity = 1.25f, lightAngles = new Vector2(20f, -40f), ambient = new Color(0.62f, 0.5f, 0.55f), lanternsGlow = true,
            };
            themes[ChapterTheme.HarborNight] = new ForgeCatalog.ThemeSpec
            {
                displayName = Loc("chapter.theme.harbor_night"), music = BgmManager.BgmType.NinjaChase, sky = new Color(0.07f, 0.11f, 0.26f),
                ground = new Color(0.14f, 0.22f, 0.32f), path = new Color(0.36f, 0.34f, 0.4f), wall = new Color(0.22f, 0.34f, 0.58f), roof = new Color(0.62f, 0.18f, 0.24f),
                accent = new Color(1f, 0.85f, 0.4f), foliage = new Color(0.1f, 0.3f, 0.32f), fog = new Color(0.1f, 0.15f, 0.3f), fogDensity = 0.012f,
                light = new Color(0.55f, 0.65f, 1f), lightIntensity = 0.75f, lightAngles = new Vector2(40f, 30f), ambient = new Color(0.25f, 0.3f, 0.48f), lanternsGlow = true,
            };
            themes[ChapterTheme.MountainTemple] = new ForgeCatalog.ThemeSpec
            {
                displayName = Loc("chapter.theme.mountain_temple"), music = BgmManager.BgmType.ThreeWeapons, sky = new Color(0.5f, 0.82f, 1f),
                ground = new Color(0.38f, 0.72f, 0.32f), path = new Color(0.94f, 0.86f, 0.7f), wall = new Color(0.97f, 0.96f, 0.92f), roof = new Color(0.88f, 0.18f, 0.18f),
                accent = new Color(1f, 0.82f, 0.18f), foliage = new Color(0.18f, 0.66f, 0.3f), fog = new Color(0.76f, 0.88f, 1f), fogDensity = 0.005f,
                light = new Color(1f, 0.97f, 0.9f), lightIntensity = 1.35f, lightAngles = new Vector2(55f, -25f), ambient = new Color(0.62f, 0.66f, 0.76f),
            };
            themes[ChapterTheme.StormCitadel] = new ForgeCatalog.ThemeSpec
            {
                displayName = Loc("chapter.theme.storm_citadel"), music = BgmManager.BgmType.RobotMecha, sky = new Color(0.3f, 0.24f, 0.4f),
                ground = new Color(0.26f, 0.22f, 0.3f), path = new Color(0.46f, 0.42f, 0.5f), wall = new Color(0.36f, 0.3f, 0.45f), roof = new Color(0.58f, 0.14f, 0.62f),
                accent = new Color(0.6f, 1f, 0.4f), foliage = new Color(0.2f, 0.32f, 0.26f), fog = new Color(0.32f, 0.27f, 0.42f), fogDensity = 0.014f,
                light = new Color(0.78f, 0.72f, 1f), lightIntensity = 0.95f, lightAngles = new Vector2(35f, 60f), ambient = new Color(0.36f, 0.3f, 0.46f), lanternsGlow = true,
            };

            var links = Field<List<ForgeCatalog.NaturalLink>>(catalog, "naturalLinks");
            links.Clear();
            // Boss states first: between two Boss phases the Boss's own moment wins over the scenery.
            links.Add(Link(LinkState.BossPhase, LinkMoment.BossRoar, CameraMove.CraneUp, BgmManager.StingerType.BossAppear, "chapter.story.boss_phase",
                "chapter.story.boss_phase_2"));
            links.Add(Link(LinkState.BossBroken, LinkMoment.BrickBurst, CameraMove.FollowFlight, BgmManager.StingerType.BossAppear, "chapter.story.boss_broken"));
            links.Add(Link(LinkState.Ambush, LinkMoment.Dust, CameraMove.Glide, BgmManager.StingerType.BossAppear, "chapter.story.ambush", "chapter.story.ambush_2"));
            links.Add(Link(LinkState.EnemyFled, LinkMoment.EnemyFlees, CameraMove.FollowFlight, BgmManager.StingerType.StageClear, "chapter.story.fled", "chapter.story.fled_2"));
            links.Add(Link(LinkState.GuardAhead, LinkMoment.NinjaLanding, CameraMove.Glide, BgmManager.StingerType.BossAppear, "chapter.story.guard"));
            links.Add(Link(LinkState.CrowdIncoming, LinkMoment.Dust, CameraMove.CraneUp, BgmManager.StingerType.BossAppear, "chapter.story.crowd"));
            links.Add(Link(LinkState.OnRoof, LinkMoment.DropOffRoof, CameraMove.CraneUp, BgmManager.StingerType.StageClear, "chapter.story.on_roof"));
            links.Add(Link(LinkState.AtWall, LinkMoment.AroundCorner, CameraMove.Glide, BgmManager.StingerType.StageClear, "chapter.story.at_wall", "chapter.story.at_wall_2"));
            links.Add(Link(LinkState.Landed, LinkMoment.NinjaLanding, CameraMove.Glide, BgmManager.StingerType.StageClear, "chapter.story.landed"));
            links.Add(Link(LinkState.Airborne, LinkMoment.VehicleArrives, CameraMove.FollowFlight, BgmManager.StingerType.StageClear, "chapter.story.airborne"));
            SetField(catalog, "brickRebuild", Link(LinkState.None, LinkMoment.BrickRebuild, CameraMove.CraneUp, BgmManager.StingerType.StageClear,
                "chapter.story.rebuild", "chapter.story.rebuild_2").spec);
        }

        static ForgeCatalog.BlockSpec Spec(string nameKey, Color color, LinkState start, LinkState end, LinkState bossStart, LinkState bossEnd,
            TwoPlayerShare share, float seconds, Gesture[] gestures, BlockDifficulty easy, BlockDifficulty normal, BlockDifficulty hard)
        {
            var spec = new ForgeCatalog.BlockSpec
            {
                displayName = Loc(nameKey), color = color, startStates = start, endStates = end, bossStartStates = bossStart, bossEndStates = bossEnd,
                twoPlayers = share, defaultSeconds = seconds, gestures = new List<Gesture>(gestures),
            };
            spec.presets[DifficultyPreset.Easy] = easy;
            spec.presets[DifficultyPreset.Normal] = normal;
            spec.presets[DifficultyPreset.Hard] = hard;
            return spec;
        }

        static BlockDifficulty Preset(float window, float speed, int count, float zone) => new()
        {
            windowScale = window, speedScale = speed, count = count, zoneLength = zone,
        };

        static ForgeCatalog.NaturalLink Link(LinkState state, LinkMoment moment, CameraMove move, BgmManager.StingerType stinger, params string[] stories) => new()
        {
            state = state,
            spec = new ForgeCatalog.LinkSpec { moment = moment, cameraMove = move, stinger = stinger, storyKeys = new List<string>(stories) },
        };

        #endregion

        #region Forge Config

        static ForgeConfig BuildForgeConfig()
        {
            var config = LoadOrCreate<ForgeConfig>(ForgeConfigPath);
            SetField(config, "placeWords", Keys("forge.word.place.", 10));
            SetField(config, "timeWords", Keys("forge.word.time.", 8));
            SetField(config, "actionWords", Keys("forge.word.action.", 8));
            SetField(config, "nameFormat", Loc("forge.name.format"));
            SetField(config, "seededNameFormat", Loc("forge.name.seeded"));
            SetField(config, "levelName", Loc("chapter.level.name"));
            SetField(config, "bossLevelName", Loc("chapter.level.boss"));
            var rides = Field<EnumDictionary<RunnerRide, LocalizedString>>(config, "rideNames");
            rides[RunnerRide.Car] = Loc("forge.ride.car");
            rides[RunnerRide.Skycraft] = Loc("forge.ride.skycraft");
            rides[RunnerRide.CarThenSkycraft] = Loc("forge.ride.car_skycraft");
            var presets = Field<EnumDictionary<DifficultyPreset, LocalizedString>>(config, "presetNames");
            presets[DifficultyPreset.Easy] = Loc("forge.preset.easy");
            presets[DifficultyPreset.Normal] = Loc("forge.preset.normal");
            presets[DifficultyPreset.Hard] = Loc("forge.preset.hard");
            var hints = Field<EnumDictionary<HintPolicy, LocalizedString>>(config, "hintNames");
            hints[HintPolicy.FirstTime] = Loc("forge.hints.first_time");
            hints[HintPolicy.NewOnly] = Loc("forge.hints.new_only");
            hints[HintPolicy.None] = Loc("forge.hints.none");
            var moments = Field<EnumDictionary<LinkMoment, LocalizedString>>(config, "momentNames");
            moments[LinkMoment.BrickRebuild] = Loc("forge.moment.brick_rebuild");
            moments[LinkMoment.BossRoar] = Loc("forge.moment.boss_roar");
            moments[LinkMoment.Dust] = Loc("forge.moment.dust");
            moments[LinkMoment.BrickBurst] = Loc("forge.moment.brick_burst");
            moments[LinkMoment.NinjaLanding] = Loc("forge.moment.landing");
            moments[LinkMoment.VehicleArrives] = Loc("forge.moment.vehicle");
            moments[LinkMoment.DropOffRoof] = Loc("forge.moment.roof");
            moments[LinkMoment.AroundCorner] = Loc("forge.moment.corner");
            moments[LinkMoment.EnemyFlees] = Loc("forge.moment.flees");
            var rules = Field<EnumDictionary<ForgeRule, LocalizedString>>(config, "ruleLines");
            rules[ForgeRule.BlockCount] = Loc("forge.rule.block_count");
            rules[ForgeRule.RepeatedType] = Loc("forge.rule.repeated");
            rules[ForgeRule.LinkUnresolved] = Loc("forge.rule.link");
            rules[ForgeRule.VehiclesMissing] = Loc("forge.rule.vehicles");
            rules[ForgeRule.BossNotLast] = Loc("forge.rule.boss_last");
            rules[ForgeRule.BossHasStealth] = Loc("forge.rule.boss_stealth");
            rules[ForgeRule.BossUnknownGesture] = Loc("forge.rule.boss_gesture");
            rules[ForgeRule.BossNoEscape] = Loc("forge.rule.boss_escape");
            rules[ForgeRule.DifficultyDrops] = Loc("forge.rule.difficulty");
            rules[ForgeRule.LevelLength] = Loc("forge.rule.length");
            rules[ForgeRule.NoLevels] = Loc("forge.rule.no_levels");
            rules[ForgeRule.MixNotFight] = Loc("forge.rule.mix");
            SetField(config, "levelMusic", new List<BgmManager.BgmType>
            {
                BgmManager.BgmType.NinjaFight, BgmManager.BgmType.NinjaChase, BgmManager.BgmType.ThreeWeapons, BgmManager.BgmType.OnePunch,
            });
            SetField(config, "bossMusic", BgmManager.BgmType.RobotMecha);
            EditorUtility.SetDirty(config);
            return config;
        }

        static List<string> Keys(string prefix, int count)
        {
            var keys = new List<string>(count);
            for (var i = 0; i < count; i++) keys.Add(prefix + i);
            return keys;
        }

        #endregion
    }
}
