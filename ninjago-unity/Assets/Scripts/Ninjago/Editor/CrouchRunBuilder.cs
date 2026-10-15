#nullable enable

using System;
using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;
using static Nex.Ninjago.Editor.NinjagoVfxBuilder;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Crouch Run: the tuning asset (with the three starting levels), audio registry entries, VFX, world, views, the
    /// Addressable game prefab and its menu entry. Rebuilds keep GUIDs and a designer's tuning in an existing asset.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.CrouchRunBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class CrouchRunBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/CrouchRunConfig.asset";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/CrouchRunGame.prefab";
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Crouch Run")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[CrouchRunBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildConfig();
            BuildAudioRegistry();
            BuildVfx();
            CrouchRunWorldBuilder.Build();
            CrouchRunViewsBuilder.Build();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var isNew = AssetDatabase.LoadAssetAtPath<CrouchRunConfig>(ConfigPath) == null;
            var config = LoadOrCreate<CrouchRunConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var looks = serialized.FindProperty("looks");
            Look(looks, CrouchZoneState.Clear, "ninjago.crouch.clear", "ninjago.crouch.stand_to_run", new Color(0.25f, 0.8f, 0.3f));
            Look(looks, CrouchZoneState.Talking, "ninjago.crouch.talking", "ninjago.crouch.crouch", new Color(0.95f, 0.28f, 0.3f));
            // Rebuilds keep a designer's levels; only a new asset gets the starting ones.
            if (isNew) WriteStartingLevels(serialized.FindProperty("levels"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                Set(root.GetComponent<GameConfigsManager>(), "crouchRunConfig", config);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Look(SerializedProperty looks, CrouchZoneState state, string titleKey, string instructionKey, Color color)
        {
            var look = EnumDictionaryEditorUtils.GetValueProperty(looks, (int)state);
            WriteLocalized(look.FindPropertyRelative("title"), titleKey);
            WriteLocalized(look.FindPropertyRelative("instruction"), instructionKey);
            look.FindPropertyRelative("color").colorValue = color;
        }

        // At the starting speeds (run 6 m/s, crouch 2 m/s) a perfect run leaves about 7.7 s, 6.3 s and 5.7 s.
        static void WriteStartingLevels(SerializedProperty levels)
        {
            const CrouchZoneState c = CrouchZoneState.Clear;
            const CrouchZoneState t = CrouchZoneState.Talking;
            var starting = new[]
            {
                (45f, new[] { (c, 40f), (t, 24f), (c, 40f), (t, 24f) }),
                // The short clear zone before the second talking zone: a late crouch gets heard.
                (41f, new[] { (c, 30f), (t, 16f), (c, 12f), (t, 16f), (c, 28f), (t, 14f) }),
                // Faster changes, and the last talking zone is the longest: sprinting the ending fails.
                (41f, new[] { (c, 20f), (t, 10f), (c, 14f), (t, 10f), (c, 12f), (t, 8f), (c, 16f), (t, 22f) }),
            };
            levels.arraySize = starting.Length;
            for (var i = 0; i < starting.Length; i++)
            {
                var (timer, zones) = starting[i];
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("timerSeconds").floatValue = timer;
                var list = level.FindPropertyRelative("zones");
                list.arraySize = zones.Length;
                for (var z = 0; z < zones.Length; z++)
                {
                    var zone = list.GetArrayElementAtIndex(z);
                    zone.FindPropertyRelative("state").intValue = (int)zones[z].Item1;
                    zone.FindPropertyRelative("length").floatValue = zones[z].Item2;
                }
            }
        }

        #endregion

        #region Audio

        static void BuildAudioRegistry()
        {
            foreach (var path in new[] { $"{SfxRoot}/RunStep_1.wav", $"{SfxRoot}/RunStep_2.wav", $"{SfxRoot}/SneakStep_1.wav", $"{SfxRoot}/SneakStep_2.wav",
                         $"{BgmRoot}/CrouchRun.ogg" })
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
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.RunStep, SfxRoot, "RunStep_1", "RunStep_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.SneakStep, SfxRoot, "SneakStep_1", "SneakStep_2");
            });
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.CrouchRun).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/CrouchRun.ogg");
            });
        }

        #endregion

        #region VFX

        static void BuildVfx()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var heard = Save("Vfx_Heard", BuildHeard(soft));
            var gate = Save("Vfx_GateCleared", BuildConfetti(brick, Resources.GetBuiltinResource<Mesh>("Cube.fbx")));
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.Heard, heard, 2, 4);
                Register(dict, VfxManager.VisualEffect.GateCleared, gate, 2, 4);
            });
        }

        // A red-and-yellow shock ring around the ninja's head, facing the side camera, with sparks thrown up.
        static ParticleSystem BuildHeard(Material material)
        {
            var system = NewSystem("Vfx_Heard", material, 0.8f, false, 60);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.25f, 0.2f), new Color(1f, 0.9f, 0.2f));
            Burst(system, 40);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            FadeOut(system);
            GrowOverLife(system, 0.4f);
            return system;
        }

        // Colored bricks popping up out of the gate.
        static ParticleSystem BuildConfetti(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_GateCleared", material, 1.6f, false, 50);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.7f, 1f));
            main.gravityModifier = 1.2f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 40);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            MeshRenderer(system, cube);
            return system;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("CrouchRunGame");
            var game = root.AddComponent<CrouchRunGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "viewPrefab", LoadComponent<CrouchRunView>(CrouchRunViewsBuilder.ViewPath));
            Set(game, "config", Load<CrouchRunConfig>(ConfigPath));
            SetLocalized(game, "allClearTitle", "ninjago.result.crouch_all_clear");
            SetLocalized(game, "failedTitle", "ninjago.result.crouch_failed");
            SetLocalized(game, "clearedLine", "ninjago.result.crouch_cleared_line");
            SetLocalized(game, "failedLine", "ninjago.result.crouch_failed_line");
            SetLocalized(game, "bestLine", "ninjago.result.crouch_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.CrouchRun, GamePath, "ninjago.mode.crouch_run");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
