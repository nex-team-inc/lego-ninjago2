#nullable enable

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The one Chapter game prefab every chapter mode runs through (and its GameModeConfig entry), plus the Forge's.
    /// The menu lists chapter modes from the mode library, so no game prefab or GameModeType exists per mode.
    /// </summary>
    public static class ChapterGamesBuilder
    {
        public const string ChapterGamePath = NinjagoGamesBuilder.GamesRoot + "/ChapterGame.prefab";

        #region Entry Point

        public static void Build()
        {
            var root = new GameObject("ChapterGame");
            var game = root.AddComponent<ChapterGame>();
            WireGame(game, root.transform);
            SavePrefab(root, ChapterGamePath);
            Register(GameModeType.Chapter, ChapterGamePath, "chapter.mode.proof");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ForgeGamePath) == null) Unregister(GameModeType.Forge);
        }

        public const string ForgeGamePath = NinjagoGamesBuilder.GamesRoot + "/ForgeGame.prefab";

        // A key the EnumDictionary filled in by copying its neighbour must not point at another mode's game.
        static void Unregister(GameModeType mode)
        {
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            var value = Nex.Util.EnumDictionaryEditorUtils.GetValueProperty(serialized.FindProperty("modes"), (int)mode);
            value.FindPropertyRelative("gamePrefab.m_AssetGUID").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        /// <summary>Everything a chapter runtime needs: the shared Ninjago wiring, the hand storm, the stage and the views.</summary>
        internal static void WireGame(ChapterGame game, Transform root)
        {
            NinjagoGamesBuilder.WireShared(game, root);
            // The stage brings its own sun, lit per theme.
            Object.DestroyImmediate(root.Find("Sun").gameObject);
            var stormsRoot = Empty("HandStorm", root, Vector3.zero);
            var storms = stormsRoot.gameObject.AddComponent<HandStormSampler>();
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Packages/team.nex.mdk.hand/Prefabs/HandPoseDetector.prefab"), stormsRoot);
            hand.SetActive(false);
            Set(storms, "handDetector", hand.GetComponent<Jazz.HandDetectionManager>());
            Set(game, "stagePrefab", LoadComponent<ChapterStage>(ChapterStageBuilder.StagePath));
            Set(game, "storms", storms);
            Set(game, "fightConfig", Load<FightConfig>(NinjagoAssetsBuilder.FightConfigPath));
            Set(game, "startViewPrefab", LoadComponent<ChapterStartView>(ChapterViewsBuilder.StartViewPath));
            Set(game, "blockViewPrefab", LoadComponent<ChapterBlockView>(ChapterViewsBuilder.BlockViewPath));
            Set(game, "restViewPrefab", LoadComponent<ChapterRestView>(ChapterViewsBuilder.RestViewPath));
            Set(game, "endingViewPrefab", LoadComponent<ChapterEndingView>(ChapterViewsBuilder.EndingViewPath));
            SetLocalized(game, "nextLevelText", "chapter.start.next");
            SetLocalized(game, "freshStartText", "chapter.start.fresh");
            SetLocalized(game, "tryAgainText", "chapter.try_again");
        }

        internal static void Register(GameModeType mode, string prefabPath, string nameKey)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), mode, prefabPath, nameKey);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
