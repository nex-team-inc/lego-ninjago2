#nullable enable

using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;
using static Nex.Ninjago.Editor.NinjagoVfxBuilder;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Guard: the tuning asset (with the outcome words), audio registry entries, VFX, the courtyard, the play view, the
    /// shared children's result view, the Addressable game prefab and its menu entry. Rebuilds keep GUIDs and tuning.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.GuardBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class GuardBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/GuardConfig.asset";
        const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/GuardView.prefab";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/GuardGame.prefab";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Guard")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[GuardBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildContent();
            BuildAudio();
        }

        public static void BuildAudio()
        {
            KidsAudio.Register(SfxManager.SoundEffect.GuardBlock, "GuardBlock_1", "GuardBlock_2");
            KidsAudio.Register(SfxManager.SoundEffect.Bonk, "Bonk_1", "Bonk_2");
            KidsAudio.Register(SfxManager.SoundEffect.Cheer, "Cheer_1");
            KidsAudio.RegisterMusic(BgmManager.BgmType.Guard, "Guard");
        }

        /// <summary>Everything but the audio registry, which needs the clips on disk.</summary>
        public static void BuildContent()
        {
            BuildConfig();
            BuildVfx();
            GuardWorldBuilder.Build();
            KidsViewsBuilder.BuildSceneView<GuardView>(ViewPath, view => Set(view, "scenePrefab", LoadComponent<GuardScene>(GuardWorldBuilder.ScenePath)));
            KidsViewsBuilder.BuildResultView();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var config = LoadOrCreate<GuardConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var looks = serialized.FindProperty("looks");
            Look(looks, GuardOutcome.Saved, "ninjago.guard.saved", new Color(0.35f, 1f, 0.4f));
            Look(looks, GuardOutcome.Late, "ninjago.guard.late", new Color(1f, 0.65f, 0.25f));
            Look(looks, GuardOutcome.Held, "ninjago.guard.held", new Color(1f, 0.9f, 0.3f));
            Look(looks, GuardOutcome.Half, "ninjago.guard.half", new Color(1f, 0.9f, 0.3f));
            Look(looks, GuardOutcome.Missed, "ninjago.guard.missed", new Color(1f, 0.55f, 0.8f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            KidsAudio.SetConfig("guardConfig", config);
        }

        static void Look(SerializedProperty looks, GuardOutcome outcome, string key, Color color)
        {
            var look = EnumDictionaryEditorUtils.GetValueProperty(looks, (int)outcome);
            WriteLocalized(look.FindPropertyRelative("label"), key);
            look.FindPropertyRelative("color").colorValue = color;
        }

        #endregion

        #region VFX

        static void BuildVfx()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var heart = NinjagoAssetsBuilder.Particle("Particle Heart", true);
            heart.SetTexture("_BaseMap", NinjagoAssetsBuilder.GetSprite("Heart").texture);
            EditorUtility.SetDirty(heart);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var shieldBurst = Save("Vfx_ShieldBurst", BuildShieldBurst(soft));
            var stars = Save("Vfx_DizzyStars", BuildStars(soft));
            var hearts = Save("Vfx_Hearts", BuildHearts(heart));
            var pop = Save("Vfx_BrickSnapPop", BuildBrickPop(brick, cube));
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.ShieldBurst, shieldBurst, 2, 4);
                Register(dict, VfxManager.VisualEffect.DizzyStars, stars, 2, 4);
                Register(dict, VfxManager.VisualEffect.Hearts, hearts, 2, 4);
                Register(dict, VfxManager.VisualEffect.BrickSnapPop, pop, 4, 8);
            });
        }

        // A big gold-and-white star flash off the shield, readable from across the room.
        static ParticleSystem BuildShieldBurst(Material material)
        {
            var system = NewSystem("Vfx_ShieldBurst", material, 0.6f, false, 60);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.82f, 0.15f));
            Burst(system, 48);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            FadeOut(system);
            GrowOverLife(system, 0.4f);
            return system;
        }

        // Yellow stars circling over a bonked head.
        static ParticleSystem BuildStars(Material material)
        {
            var system = NewSystem("Vfx_DizzyStars", material, 1f, false, 30);
            var main = system.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.3f), Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            emission.rateOverTime = 16f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.38f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Orbit(system, 7f, 0f, 0.1f);
            FadeOut(system);
            return system;
        }

        // Pink hearts floating up from the saved friend.
        static ParticleSystem BuildHearts(Material material)
        {
            var system = NewSystem("Vfx_Hearts", material, 0.8f, false, 16);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.55f), new Color(1f, 0.6f, 0.75f));
            main.gravityModifier = -0.1f;
            Burst(system, 8);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            return system;
        }

        // A few tiny bricks popping off: a brick hat coming off, a brick clicking into place.
        static ParticleSystem BuildBrickPop(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_BrickSnapPop", material, 0.8f, false, 16);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.7f, 1f));
            main.gravityModifier = 1f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 10);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            MeshRenderer(system, cube);
            return system;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("GuardGame");
            var game = root.AddComponent<GuardGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(KidsViewsBuilder.ResultViewPath));
            Set(game, "config", Load<GuardConfig>(ConfigPath));
            Set(game, "viewPrefab", LoadComponent<GuardView>(ViewPath));
            SetLocalized(game, "goodTitle", "ninjago.result.guard_good");
            SetLocalized(game, "tryAgainTitle", "ninjago.result.guard_again");
            SetLocalized(game, "outcomesLine", "ninjago.result.guard_outcomes");
            SetLocalized(game, "readingLine", "ninjago.result.guard_reading");
            SetLocalized(game, "bigLine", "ninjago.result.guard_big");
            SetLocalized(game, "bestLine", "ninjago.result.guard_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.Guard, GamePath, "ninjago.mode.guard");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
