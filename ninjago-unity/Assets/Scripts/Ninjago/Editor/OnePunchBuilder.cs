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
    /// One Punch: the tuning asset (with the signal words and colors), audio registry entries, VFX, the street, views,
    /// the Addressable game prefab and its menu entry. Rebuilds keep GUIDs and a designer's tuning in an existing asset.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.OnePunchBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class OnePunchBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/OnePunchConfig.asset";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/OnePunchGame.prefab";
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build One Punch")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[OnePunchBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildConfig();
            BuildAudioRegistry();
            BuildVfx();
            OnePunchWorldBuilder.Build();
            OnePunchViewsBuilder.Build();
            WireSetupPrompt();
            BuildGame();
            RegisterMode();
        }

        // One Punch shares the plain setup view, which shows this prompt while a player's hands or hips are out of view.
        static void WireSetupPrompt()
        {
            var root = PrefabUtility.LoadPrefabContents(NinjagoViewsBuilder.SetupViewPath);
            try
            {
                SetLocalized(root.GetComponent<NinjagoSetupView>(), "jointsPrompt", "ninjago.setup.joints");
                PrefabUtility.SaveAsPrefabAsset(root, NinjagoViewsBuilder.SetupViewPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var config = LoadOrCreate<OnePunchConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var looks = serialized.FindProperty("signalLooks");
            Look(looks, PunchSignal.Extension, "ninjago.punch.signal_extension", new Color(0.35f, 1f, 0.4f));
            Look(looks, PunchSignal.TowardCamera, "ninjago.punch.signal_toward_camera", new Color(0.35f, 0.8f, 1f));
            Look(looks, PunchSignal.SideSlash, "ninjago.punch.signal_side_slash", new Color(1f, 0.35f, 0.3f));
            Look(looks, PunchSignal.None, "ninjago.punch.signal_none", new Color(0.75f, 0.75f, 0.75f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                Set(root.GetComponent<GameConfigsManager>(), "onePunchConfig", config);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Look(SerializedProperty looks, PunchSignal signal, string labelKey, Color color)
        {
            var look = EnumDictionaryEditorUtils.GetValueProperty(looks, (int)signal);
            WriteLocalized(look.FindPropertyRelative("label"), labelKey);
            look.FindPropertyRelative("color").colorValue = color;
        }

        #endregion

        #region Audio

        static void BuildAudioRegistry()
        {
            foreach (var path in new[] { $"{SfxRoot}/Punch_1.wav", $"{SfxRoot}/Punch_2.wav", $"{SfxRoot}/FlyAway_1.wav", $"{SfxRoot}/FlyAway_2.wav",
                         $"{SfxRoot}/Pin_1.wav", $"{BgmRoot}/OnePunch.ogg" })
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
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.Punch, SfxRoot, "Punch_1", "Punch_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.FlyAway, SfxRoot, "FlyAway_1", "FlyAway_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.Pin, SfxRoot, "Pin_1");
            });
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.OnePunch).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/OnePunch.ogg");
            });
        }

        #endregion

        #region VFX

        static void BuildVfx()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var impact = Save("Vfx_PunchImpact", BuildImpact(soft));
            var trail = Save("Vfx_BrickTrail", BuildTrail(brick, cube));
            var burst = Save("Vfx_BrickBurst", BuildBurst(brick, cube));
            var slam = Save("Vfx_PinSlam", BuildSlam(soft));
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.PunchImpact, impact, 4, 8);
                Register(dict, VfxManager.VisualEffect.BrickTrail, trail, 4, 8);
                Register(dict, VfxManager.VisualEffect.BrickBurst, burst, 4, 8);
                Register(dict, VfxManager.VisualEffect.PinSlam, slam, 2, 4);
            });
        }

        // A quick white-and-yellow star pop where the fist lands.
        static ParticleSystem BuildImpact(Material material)
        {
            var system = NewSystem("Vfx_PunchImpact", material, 0.35f, false, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.9f, 0.25f));
            Burst(system, 26);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            FadeOut(system);
            GrowOverLife(system, 0.3f);
            return system;
        }

        // Bright bricks shed behind a flying monster; stopped by the monster when it bursts.
        static ParticleSystem BuildTrail(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_BrickTrail", material, 1f, true, 80);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.4f, 0.3f));
            main.gravityModifier = 0.8f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = system.emission;
            emission.rateOverTime = 45f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            MeshRenderer(system, cube);
            return system;
        }

        // The monster bursts into a big shower of colored bricks that rain down.
        static ParticleSystem BuildBurst(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_BrickBurst", material, 2f, false, 60);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.3f, 0.95f), new Color(1f, 0.6f, 0.15f));
            main.gravityModifier = 1.3f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 48);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotation.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            MeshRenderer(system, cube);
            return system;
        }

        // A dust ring and stars around the ninja's shoulders as the monster lands on him.
        static ParticleSystem BuildSlam(Material material)
        {
            var system = NewSystem("Vfx_PinSlam", material, 0.8f, false, 50);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.9f, 0.75f), new Color(1f, 0.85f, 0.3f));
            Burst(system, 36);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            GrowOverLife(system, 1.6f);
            return system;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("OnePunchGame");
            var game = root.AddComponent<OnePunchGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            var trackerRoot = Empty("PunchTracker", root.transform, Vector3.zero);
            var tracker = trackerRoot.gameObject.AddComponent<PunchTracker>();
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Packages/team.nex.mdk.hand/Prefabs/HandPoseDetector.prefab"), trackerRoot);
            hand.SetActive(false);
            Set(tracker, "handDetector", hand.GetComponent<Jazz.HandDetectionManager>());
            Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(OnePunchViewsBuilder.ResultViewPath));
            Set(game, "tracker", tracker);
            Set(game, "config", Load<OnePunchConfig>(ConfigPath));
            Set(game, "viewPrefab", LoadComponent<OnePunchView>(OnePunchViewsBuilder.ViewPath));
            SetLocalized(game, "clearedTitle", "ninjago.result.punch_cleared");
            SetLocalized(game, "downTitle", "ninjago.result.punch_down");
            SetLocalized(game, "triesLine", "ninjago.result.punch_tries");
            SetLocalized(game, "playerLine", "ninjago.result.punch_player");
            SetLocalized(game, "wavesCueLine", "ninjago.result.punch_waves_cue");
            SetLocalized(game, "wavesNoCueLine", "ninjago.result.punch_waves_no_cue");
            SetLocalized(game, "bestLine", "ninjago.result.punch_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.OnePunch, GamePath, "ninjago.mode.one_punch");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
