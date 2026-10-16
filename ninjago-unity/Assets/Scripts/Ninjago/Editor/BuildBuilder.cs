#nullable enable

using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Build: the tuning asset (with the clap words), audio registry entries, the river crossing, the play view, the
    /// shared children's result view, the Addressable game prefab and its menu entry. Rebuilds keep GUIDs and tuning.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.BuildBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class BuildBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/BuildConfig.asset";
        const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/BuildView.prefab";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/BuildGame.prefab";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Build")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[BuildBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildContent();
            BuildAudio();
        }

        public static void BuildAudio()
        {
            KidsAudio.Register(SfxManager.SoundEffect.BrickSnap, "BrickSnap_1", "BrickSnap_2", "BrickSnap_3");
            KidsAudio.Register(SfxManager.SoundEffect.WayOpen, "WayOpen_1");
            KidsAudio.RegisterMusic(BgmManager.BgmType.Build, "Build");
        }

        /// <summary>Everything but the audio registry, which needs the clips on disk.</summary>
        public static void BuildContent()
        {
            BuildConfig();
            BuildWorldBuilder.Build();
            KidsViewsBuilder.BuildSceneView<BuildView>(ViewPath, view => Set(view, "scenePrefab", LoadComponent<BuildScene>(BuildWorldBuilder.ScenePath)));
            KidsViewsBuilder.BuildResultView();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var config = LoadOrCreate<BuildConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var looks = serialized.FindProperty("looks");
            Look(looks, BuildGesture.Counted, "ninjago.build.counted", new Color(0.4f, 1f, 0.45f));
            Look(looks, BuildGesture.Near, "ninjago.build.near", new Color(1f, 0.9f, 0.35f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            KidsAudio.SetConfig("buildConfig", config);
        }

        static void Look(SerializedProperty looks, BuildGesture gesture, string key, Color color)
        {
            var look = EnumDictionaryEditorUtils.GetValueProperty(looks, (int)gesture);
            WriteLocalized(look.FindPropertyRelative("label"), key);
            look.FindPropertyRelative("color").colorValue = color;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("BuildGame");
            var game = root.AddComponent<BuildGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(KidsViewsBuilder.ResultViewPath));
            Set(game, "config", Load<BuildConfig>(ConfigPath));
            Set(game, "viewPrefab", LoadComponent<BuildView>(ViewPath));
            SetLocalized(game, "openTitle", "ninjago.result.build_open");
            SetLocalized(game, "shutTitle", "ninjago.result.build_shut");
            SetLocalized(game, "clapsLine", "ninjago.result.build_claps");
            SetLocalized(game, "openLine", "ninjago.result.build_open_line");
            SetLocalized(game, "shutLine", "ninjago.result.build_shut_line");
            SetLocalized(game, "readingLine", "ninjago.result.build_reading");
            SetLocalized(game, "bestLine", "ninjago.result.build_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.Build, GamePath, "ninjago.mode.build");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
