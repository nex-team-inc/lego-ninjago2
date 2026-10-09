#nullable enable

using System;
using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Robot Mecha: the L sprite, tuning assets (with the starting wave), audio registry entries, VFX, world, views, the
    /// Addressable game prefab and its menu entry. Rebuilds keep the GUIDs and a designer's tuning in existing assets.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.RobotMechaBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class RobotMechaBuilder
    {
        public const string RobotArmsConfigPath = ConfigsRoot + "/RobotArmsConfig.asset";
        public const string MechaFightConfigPath = ConfigsRoot + "/MechaFightConfig.asset";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/RobotMechaGame.prefab";
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Robot Mecha")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[RobotMechaBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildSprites();
            BuildConfigs();
            BuildAudioRegistry();
            RobotMechaVfxBuilder.Build();
            RobotMechaWorldBuilder.Build();
            RobotMechaViewsBuilder.Build();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Sprites

        static void BuildSprites()
        {
            NinjagoAssetsBuilder.BuildSprite("LShape", 128, (u, v) =>
            {
                var upright = u >= -0.72f && u <= -0.22f && v >= -0.86f && v <= 0.86f;
                var foot = u >= -0.72f && u <= 0.72f && v >= -0.86f && v <= -0.38f;
                return upright || foot;
            }, Vector4.zero);
        }

        #endregion

        #region Configs

        static void BuildConfigs()
        {
            var arms = LoadOrCreate<RobotArmsConfig>(RobotArmsConfigPath);
            var fightIsNew = AssetDatabase.LoadAssetAtPath<MechaFightConfig>(MechaFightConfigPath) == null;
            var fight = LoadOrCreate<MechaFightConfig>(MechaFightConfigPath);
            // Rebuilds keep a designer's enemy and wave tuning; only a new asset gets the starting values.
            if (fightIsNew) WriteStartingFight(fight);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                var manager = root.GetComponent<GameConfigsManager>();
                Set(manager, "robotArmsConfig", arms);
                Set(manager, "mechaFightConfig", fight);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WriteStartingFight(MechaFightConfig fight)
        {
            var serialized = new SerializedObject(fight);
            var enemies = serialized.FindProperty("enemies");
            Enemy(enemies, MechaEnemyType.Rusher, health: 1f, speed: 2.4f, hitRadius: 1.05f, height: 1.6f);
            var archer = Enemy(enemies, MechaEnemyType.Archer, health: 1.5f, speed: 1.8f, hitRadius: 0.7f, height: 3f);
            archer.FindPropertyRelative("stopDistance").floatValue = 15f;
            archer.FindPropertyRelative("firstDrawDelay").floatValue = 1f;
            archer.FindPropertyRelative("drawSeconds").floatValue = 1.4f;
            archer.FindPropertyRelative("arrowFlightSeconds").floatValue = 2.4f;
            archer.FindPropertyRelative("reloadSeconds").floatValue = 3.5f;

            var r = MechaEnemyType.Rusher;
            var a = MechaEnemyType.Archer;
            var wave = new (float time, MechaEnemyType type, float angle, bool high)[]
            {
                // 0-15 s: in front and slightly to the side, while the ROBOT ARMS prompt is up.
                (2.5f, r, 0f, false), (5.5f, r, -18f, false), (8f, r, 20f, false), (10f, a, 12f, false), (12.5f, r, -8f, false),
                // 15-40 s: rushers from the sides, and one archer on a pillar above the horizon so the hands must rise.
                (16f, r, 75f, false), (19f, r, -80f, false), (22f, a, -22.5f, true), (24f, r, 95f, false), (27f, r, -60f, false),
                (30f, r, 110f, false), (32f, r, -100f, false), (35f, r, 70f, false), (37.5f, r, -90f, false),
                // 40-70 s: rushers from behind, plus two archers.
                (41f, r, 180f, false), (43f, a, 135f, false), (45f, r, 160f, false), (48f, r, -170f, false), (50f, a, -112.5f, true),
                (53f, r, -160f, false), (56f, r, 150f, false), (59f, r, -150f, false), (62f, r, 175f, false), (65f, r, -175f, false),
            };
            var spawns = serialized.FindProperty("spawns");
            spawns.arraySize = wave.Length;
            for (var i = 0; i < wave.Length; i++)
            {
                var spawn = spawns.GetArrayElementAtIndex(i);
                spawn.FindPropertyRelative("time").floatValue = wave[i].time;
                spawn.FindPropertyRelative("type").intValue = (int)wave[i].type;
                spawn.FindPropertyRelative("angle").floatValue = wave[i].angle;
                spawn.FindPropertyRelative("high").boolValue = wave[i].high;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fight);
        }

        static SerializedProperty Enemy(SerializedProperty enemies, MechaEnemyType type, float health, float speed, float hitRadius, float height)
        {
            var value = EnumDictionaryEditorUtils.GetValueProperty(enemies, (int)type);
            value.FindPropertyRelative("health").floatValue = health;
            value.FindPropertyRelative("speed").floatValue = speed;
            value.FindPropertyRelative("hitRadius").floatValue = hitRadius;
            value.FindPropertyRelative("height").floatValue = height;
            return value;
        }

        #endregion

        #region Audio

        static void BuildAudioRegistry()
        {
            foreach (var path in new[] { $"{SfxRoot}/LaserHum_1.wav", $"{SfxRoot}/MechaHit_1.wav", $"{SfxRoot}/MechaHit_2.wav", $"{SfxRoot}/EnemyExplosion_1.wav",
                         $"{SfxRoot}/EnemyExplosion_2.wav", $"{BgmRoot}/RobotMecha.ogg" })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var isMusic = path.StartsWith(BgmRoot, StringComparison.Ordinal);
                var settings = importer.defaultSampleSettings;
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !isMusic;
                importer.SaveAndReimport();
            }

            NinjagoAssetsBuilder.EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict", dict =>
            {
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.LaserHum, SfxRoot, "LaserHum_1");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.MechaHit, SfxRoot, "MechaHit_1", "MechaHit_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.EnemyExplosion, SfxRoot, "EnemyExplosion_1", "EnemyExplosion_2");
            });
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.RobotMecha).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/RobotMecha.ogg");
            });
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("RobotMechaGame");
            var game = root.AddComponent<RobotMechaGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "setupViewPrefab", LoadComponent<NinjagoSetupView>(RobotMechaViewsBuilder.SetupViewPath));
            var armsRoot = Empty("RobotArms", root.transform, Vector3.zero);
            var tracker = armsRoot.gameObject.AddComponent<RobotArmsTracker>();
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Packages/team.nex.mdk.hand/Prefabs/HandPoseDetector.prefab"), armsRoot);
            hand.SetActive(false);
            Set(tracker, "handDetector", hand.GetComponent<Jazz.HandDetectionManager>());
            Set(game, "arms", tracker);
            Set(game, "armsConfig", Load<RobotArmsConfig>(RobotArmsConfigPath));
            Set(game, "viewPrefab", LoadComponent<MechaFightView>(RobotMechaViewsBuilder.FightViewPath));
            Set(game, "config", Load<MechaFightConfig>(MechaFightConfigPath));
            SetLocalized(game, "survivedTitle", "ninjago.result.mecha_survived");
            SetLocalized(game, "downTitle", "ninjago.result.mecha_down");
            SetLocalized(game, "playerLine", "ninjago.result.mecha_player");
            SetLocalized(game, "bestLine", "ninjago.result.mecha_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.RobotMecha, GamePath, "ninjago.mode.robot_mecha");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
