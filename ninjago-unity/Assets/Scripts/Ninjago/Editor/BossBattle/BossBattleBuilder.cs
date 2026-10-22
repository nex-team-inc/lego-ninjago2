#nullable enable

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Boss Battle's menu entry: the localized mode name, the Addressable game prefab, its GameModeConfig entry and its place in
    /// the menu (right after the Forge). Until the Boss Battle runtime is integrated, the game prefab is an early-access copy of
    /// the Chest Chase game (the car and plane ride the boss fight is built on); integration rebuilds the same prefab in place,
    /// so the GUID and the menu entry stay.
    /// CLI: unity command eval 'return Nex.Ninjago.Editor.BossBattleBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class BossBattleBuilder
    {
        public const string GamePath = NinjagoGamesBuilder.GamesRoot + "/BossBattleGame.prefab";
        const string ChestChaseGamePath = NinjagoGamesBuilder.GamesRoot + "/ChestChaseGame.prefab";
        const string NameKey = "ninjago.mode.boss_battle";

        static readonly (string key, string english, bool smart)[] menuRows =
        {
            (NameKey, "Boss Battle", false),
        };

        #region Entry Point

        [MenuItem("Nex/Ninjago/Boss Battle/Build All")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Upsert(menuRows);
            BuildGame();
            RegisterMode();
            AssetDatabase.SaveAssets();
            return $"[BossBattleBuilder] done ({localization})";
        }

        #endregion

        #region Game Prefab

        // Early access: the Chest Chase game under the Boss Battle entry. Never overwrites a prefab integration already built.
        static void BuildGame()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GamePath) != null) return;
            if (!AssetDatabase.CopyAsset(ChestChaseGamePath, GamePath)) throw new System.InvalidOperationException($"Could not copy {ChestChaseGamePath}");
        }

        #endregion

        #region Registration

        static void RegisterMode()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GamePath) == null)
            {
                Unregister();
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.BossBattle, GamePath, NameKey);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        // A key the EnumDictionary filled in by copying its neighbour must not point at another mode's game.
        static void Unregister()
        {
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            var value = Nex.Util.EnumDictionaryEditorUtils.GetValueProperty(serialized.FindProperty("modes"), (int)GameModeType.BossBattle);
            value.FindPropertyRelative("gamePrefab.m_AssetGUID").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
