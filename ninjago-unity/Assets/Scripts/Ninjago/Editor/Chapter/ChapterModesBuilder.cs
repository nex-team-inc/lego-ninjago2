#nullable enable

using UnityEditor;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The built-in modes as pure Forge data: Ninja Chapter Proof (four levels, the Boss last) and the library the menu
    /// lists. Level contents are written only into new assets, so a designer's edits survive rebuilds.
    /// </summary>
    public static class ChapterModesBuilder
    {
        public const string ModesRoot = ChapterAssetsBuilder.ChapterConfigsRoot + "/Modes";
        public const string LibraryPath = ChapterAssetsBuilder.ChapterConfigsRoot + "/ChapterModeLibrary.asset";
        public const string ProofModePath = ModesRoot + "/NinjaChapterProof/NinjaChapterProof.asset";
        const string ProofRoot = ModesRoot + "/NinjaChapterProof";

        #region Entry Point

        public static void Build()
        {
            var catalog = Load<ForgeCatalog>(ChapterAssetsBuilder.CatalogPath);
            var proof = BuildProof(catalog);
            var library = LoadOrCreate<ChapterModeLibrary>(LibraryPath);
            var modes = ChapterAssetsBuilder.Field<System.Collections.Generic.List<ModeDefinition>>(library, "modes");
            if (!modes.Contains(proof)) modes.Insert(0, proof);
            modes.RemoveAll(mode => mode == null);
            EditorUtility.SetDirty(library);
            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                Set(root.GetComponent<GameConfigsManager>(), "chapterModes", library);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Ninja Chapter Proof

        static ModeDefinition BuildProof(ForgeCatalog catalog)
        {
            var levels = new[]
            {
                Level(catalog, "Level1_TownDusk", ChapterTheme.TownDusk, BgmManager.BgmType.NinjaFight, level =>
                {
                    // Teaching: long windows, slow enemies, a one-word hint the first time each gesture appears.
                    Add(catalog, level, BlockType.Stealth, DifficultyPreset.Easy, HintPolicy.FirstTime, 60f);
                    Add(catalog, level, BlockType.SlashKick, DifficultyPreset.Easy, HintPolicy.FirstTime, 60f);
                    Add(catalog, level, BlockType.Runner, DifficultyPreset.Easy, HintPolicy.FirstTime, 55f).ride = RunnerRide.Car;
                    Add(catalog, level, BlockType.SlipSpin, DifficultyPreset.Easy, HintPolicy.FirstTime, 55f);
                }),
                Level(catalog, "Level2_HarborNight", ChapterTheme.HarborNight, BgmManager.BgmType.NinjaChase, level =>
                {
                    // Shorter windows, hints only for anything new, more obstacles, shorter stealth zones.
                    var ride = Add(catalog, level, BlockType.Runner, DifficultyPreset.Normal, HintPolicy.NewOnly, 55f);
                    ride.ride = RunnerRide.Skycraft;
                    ride.difficulty.count = 6;
                    Add(catalog, level, BlockType.Stealth, DifficultyPreset.Normal, HintPolicy.NewOnly, 55f);
                    Add(catalog, level, BlockType.SlipSpin, DifficultyPreset.Normal, HintPolicy.NewOnly, 60f);
                    Add(catalog, level, BlockType.SlashKick, DifficultyPreset.Normal, HintPolicy.NewOnly, 60f);
                }),
                Level(catalog, "Level3_MountainTemple", ChapterTheme.MountainTemple, BgmManager.BgmType.ThreeWeapons, level =>
                {
                    // Tighter windows, no hints, light overlaps late in the level, then a long escape.
                    Add(catalog, level, BlockType.SlashKick, DifficultyPreset.Hard, HintPolicy.None, 60f).difficulty.windowScale = 0.85f;
                    Add(catalog, level, BlockType.Stealth, DifficultyPreset.Hard, HintPolicy.None, 55f);
                    var guard = Add(catalog, level, BlockType.SlipSpin, DifficultyPreset.Hard, HintPolicy.None, 60f);
                    guard.difficulty.windowScale = 0.85f;
                    guard.difficulty.overlapEvery = 4;
                    var escape = Add(catalog, level, BlockType.Runner, DifficultyPreset.Hard, HintPolicy.None, 70f);
                    escape.ride = RunnerRide.CarThenSkycraft;
                }, (gap, link) =>
                {
                    if (gap == 2) link.storyKey = "chapter.story.escape";
                }),
                Level(catalog, "Level4_Boss", ChapterTheme.StormCitadel, BgmManager.BgmType.RobotMecha, level =>
                {
                    // One large enemy, existing gestures only, no stealth: throws, then swings, then both, then it flees.
                    var throws = Add(catalog, level, BlockType.SlashKick, DifficultyPreset.Hard, HintPolicy.None, 70f);
                    Boss(throws, 0.75f, 1.2f, 8);
                    var swings = Add(catalog, level, BlockType.SlipSpin, DifficultyPreset.Hard, HintPolicy.None, 65f);
                    Boss(swings, 0.75f, 1.2f, 10);
                    var mixed = Add(catalog, level, BlockType.SlashKick, DifficultyPreset.Hard, HintPolicy.None, 90f);
                    Boss(mixed, 0.7f, 1.25f, 12);
                    mixed.mix.Add(BlockType.SlipSpin);
                    // Alternating, then light overlaps (a swing tells while a rock hangs), building to two overlaps in a row.
                    mixed.pattern = "K S K S K+S S K K+S S+K K S";
                    var chase = Add(catalog, level, BlockType.Runner, DifficultyPreset.Hard, HintPolicy.None, 60f);
                    chase.ride = RunnerRide.Skycraft;
                    chase.boss = true;
                }),
            };

            var chapterPath = $"{ProofRoot}/NinjaChapterProof_Chapter.asset";
            var chapterIsNew = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(chapterPath) == null;
            var chapter = LoadOrCreate<ChapterDefinition>(chapterPath);
            if (chapterIsNew) ChapterAssetsBuilder.SetField(chapter, "levels", new System.Collections.Generic.List<LevelDefinition>(levels));
            EditorUtility.SetDirty(chapter);

            var mode = LoadOrCreate<ModeDefinition>(ProofModePath);
            ChapterAssetsBuilder.SetField(mode, "id", "ninja_chapter_proof");
            ChapterAssetsBuilder.SetField(mode, "title", ChapterAssetsBuilder.Loc("chapter.mode.proof"));
            ChapterAssetsBuilder.SetField(mode, "icon", ModeIcon.Ninja);
            ChapterAssetsBuilder.SetField(mode, "chapter", chapter);
            EditorUtility.SetDirty(mode);
            return mode;
        }

        #endregion

        #region Helpers

        static LevelDefinition Level(ForgeCatalog catalog, string name, ChapterTheme theme, BgmManager.BgmType music, System.Action<LevelData> fill,
            System.Action<int, LinkEntry>? editLink = null)
        {
            var path = $"{ProofRoot}/{name}.asset";
            var isNew = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path) == null;
            var definition = LoadOrCreate<LevelDefinition>(path);
            if (!isNew) return definition;
            var level = definition.Level;
            level.theme = theme;
            level.music = music;
            level.hearts = 5;
            level.blocks.Clear();
            fill(level);
            LinkResolver.Refresh(level, catalog);
            if (editLink != null)
            {
                for (var gap = 0; gap < level.links.Count; gap++)
                {
                    var before = level.links[gap].storyKey;
                    editLink(gap, level.links[gap]);
                    if (level.links[gap].storyKey != before) level.links[gap].auto = false;
                }
            }

            EditorUtility.SetDirty(definition);
            return definition;
        }

        static BlockEntry Add(ForgeCatalog catalog, LevelData level, BlockType type, DifficultyPreset preset, HintPolicy hints, float seconds)
        {
            var entry = catalog.NewEntry(type, preset);
            entry.hints = hints;
            entry.targetSeconds = seconds;
            level.blocks.Add(entry);
            return entry;
        }

        static void Boss(BlockEntry entry, float window, float speed, int count)
        {
            entry.boss = true;
            entry.difficulty.windowScale = window;
            entry.difficulty.speedScale = speed;
            entry.difficulty.count = count;
        }

        #endregion
    }
}
