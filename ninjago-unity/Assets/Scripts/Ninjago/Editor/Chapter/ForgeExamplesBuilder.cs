#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The three example modes, made with the Forge's own edits (ForgeDocument, the same operations its view's buttons
    /// call) and Surprise me, then stored as pure data assets and listed in the menu: Quick Play (one level of four
    /// blocks), Boss Rush (the Boss level alone) and a generated chapter with its seed in the title. Existing assets are
    /// left as they are.
    /// </summary>
    public static class ForgeExamplesBuilder
    {
        public const int GeneratedSeed = 2026;
        const string Root = ChapterModesBuilder.ModesRoot;

        #region Entry Point

        public static void Build()
        {
            var catalog = Load<ForgeCatalog>(ChapterAssetsBuilder.CatalogPath);
            var config = Load<ForgeConfig>(ChapterAssetsBuilder.ForgeConfigPath);
            var linkSeconds = Load<ChapterTuning>(ChapterAssetsBuilder.TuningPath).LinkSeconds;
            var proof = Load<ModeDefinition>(ChapterModesBuilder.ProofModePath).ToModeData();
            var library = Load<ChapterModeLibrary>(ChapterModesBuilder.LibraryPath);
            var modes = ChapterAssetsBuilder.Field<List<ModeDefinition>>(library, "modes");

            Add(modes, Write(QuickPlay(catalog, linkSeconds), "QuickPlay", "quick_play", "chapter.mode.quick_play", ModeIcon.Lightning));
            Add(modes, Write(BossRush(proof, catalog, linkSeconds), "BossRush", "boss_rush", "chapter.mode.boss_rush", ModeIcon.Boss));
            var generated = ChapterGenerator.Generate(GeneratedSeed, catalog, config, $"surprise_{GeneratedSeed}");
            var titleKey = $"chapter.mode.surprise_{GeneratedSeed}";
            NinjagoLocalizationSeeder.Upsert(new[] { (titleKey, EnglishName(config, generated.name), false) });
            Add(modes, Write(generated, $"Surprise{GeneratedSeed}", generated.id, titleKey, ModeIcon.Dice));
            EditorUtility.SetDirty(library);
        }

        #endregion

        #region Examples

        // Start empty, snap in four blocks, then change their types so none repeats; the ride takes both vehicles.
        static ModeData QuickPlay(ForgeCatalog catalog, float linkSeconds)
        {
            var document = new ForgeDocument(new ModeData(), "", -1, catalog, linkSeconds);
            for (var i = 0; i < 4; i++) document.AddBlock();
            document.SelectBlock(1);
            document.CycleType();
            document.SelectBlock(2);
            document.CycleType();
            document.CycleRide();
            document.CycleRide();
            document.SelectBlock(3);
            document.CycleType();
            document.CycleType();
            document.CycleTheme();
            document.CycleTheme();
            return Checked(document, "Quick Play");
        }

        // Copy the shipped chapter and remove every level before the Boss; its chase takes both vehicles.
        static ModeData BossRush(ModeData proof, ForgeCatalog catalog, float linkSeconds)
        {
            var document = new ForgeDocument(proof.CopyAsPlayerMade("", new ModeName()), "", -1, catalog, linkSeconds);
            while (document.Mode.chapter.levels.Count > 1) document.RemoveLevel();
            document.SelectBlock(document.CurrentLevel.blocks.Count - 1);
            document.CycleRide();
            return Checked(document, "Boss Rush");
        }

        static ModeData Checked(ForgeDocument document, string name)
        {
            if (!document.IsValid) throw new System.InvalidOperationException($"{name} does not pass the validator: {document.Problems.Count} problem(s)");
            return document.Mode;
        }

        #endregion

        #region Assets

        static ModeDefinition Write(ModeData data, string folderName, string id, string titleKey, ModeIcon icon)
        {
            var folder = $"{Root}/{folderName}";
            var modePath = $"{folder}/{folderName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ModeDefinition>(modePath);
            if (existing != null) return existing;
            var levels = new List<LevelDefinition>();
            for (var i = 0; i < data.chapter.levels.Count; i++)
            {
                var level = LoadOrCreate<LevelDefinition>($"{folder}/{folderName}_Level{i + 1}.asset");
                ChapterAssetsBuilder.SetField(level, "level", data.chapter.levels[i].Copy());
                EditorUtility.SetDirty(level);
                levels.Add(level);
            }

            var chapter = LoadOrCreate<ChapterDefinition>($"{folder}/{folderName}_Chapter.asset");
            ChapterAssetsBuilder.SetField(chapter, "levels", levels);
            EditorUtility.SetDirty(chapter);
            var mode = LoadOrCreate<ModeDefinition>(modePath);
            ChapterAssetsBuilder.SetField(mode, "id", id);
            ChapterAssetsBuilder.SetField(mode, "title", ChapterAssetsBuilder.Loc(titleKey));
            ChapterAssetsBuilder.SetField(mode, "icon", icon);
            ChapterAssetsBuilder.SetField(mode, "chapter", chapter);
            EditorUtility.SetDirty(mode);
            return mode;
        }

        // Localized lookups do not resolve in edit mode, so the title is read from the English table.
        static string EnglishName(ForgeConfig config, ModeName name)
        {
            var english = (UnityEngine.Localization.Tables.StringTable)UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(TableName).GetTable("en");
            string Word(string field, int slot)
            {
                var keys = ChapterAssetsBuilder.Field<List<string>>(config, field);
                return english.GetEntry(keys[name.words[slot] % keys.Count])?.Value ?? "";
            }

            return $"{Word("placeWords", 0)} {Word("timeWords", 1)} {Word("actionWords", 2)} #{name.seed}";
        }

        static void Add(List<ModeDefinition> modes, ModeDefinition mode)
        {
            if (!modes.Contains(mode)) modes.Add(mode);
        }

        #endregion
    }
}
