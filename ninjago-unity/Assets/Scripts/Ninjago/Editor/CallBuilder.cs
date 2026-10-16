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
    /// Call: the tuning asset (with the call result words), audio registry entries, VFX, the market street, the play
    /// view, the shared children's result view, the Addressable game prefab and its menu entry. Rebuilds keep GUIDs and tuning.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.CallBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class CallBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/CallConfig.asset";
        const string ViewPath = NinjagoViewsBuilder.ViewsRoot + "/CallView.prefab";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/CallGame.prefab";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Call")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[CallBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildContent();
            BuildAudio();
        }

        public static void BuildAudio()
        {
            KidsAudio.Register(SfxManager.SoundEffect.CallSignal, "CallSignal_1");
            KidsAudio.Register(SfxManager.SoundEffect.TeammateSlam, "TeammateSlam_1", "TeammateSlam_2");
            KidsAudio.Register(SfxManager.SoundEffect.GoblinPile, "GoblinPile_1");
            KidsAudio.RegisterMusic(BgmManager.BgmType.Call, "Call");
        }

        /// <summary>Everything but the audio registry, which needs the clips on disk.</summary>
        public static void BuildContent()
        {
            BuildConfig();
            BuildVfx();
            CallWorldBuilder.Build();
            KidsViewsBuilder.BuildSceneView<CallView>(ViewPath, view => Set(view, "scenePrefab", LoadComponent<CallScene>(CallWorldBuilder.ScenePath)));
            KidsViewsBuilder.BuildResultView();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var config = LoadOrCreate<CallConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var looks = serialized.FindProperty("looks");
            Look(looks, CallResult.Understood, "ninjago.call.understood", new Color(1f, 0.82f, 0.2f));
            Look(looks, CallResult.Queued, "ninjago.call.queued", new Color(1f, 0.9f, 0.55f));
            Look(looks, CallResult.Team, "ninjago.call.team", new Color(0.4f, 1f, 0.5f));
            Look(looks, CallResult.Missed, "ninjago.call.missed", new Color(1f, 0.55f, 0.8f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            KidsAudio.SetConfig("callConfig", config);
        }

        static void Look(SerializedProperty looks, CallResult result, string key, Color color)
        {
            var look = EnumDictionaryEditorUtils.GetValueProperty(looks, (int)result);
            WriteLocalized(look.FindPropertyRelative("label"), key);
            look.FindPropertyRelative("color").colorValue = color;
        }

        #endregion

        #region VFX

        static void BuildVfx()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var beam = Save("Vfx_CallBeam", BuildBeam(soft));
            var shockwave = Save("Vfx_SlamShockwave", BuildShockwave(soft));
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.CallBeam, beam, 2, 4);
                Register(dict, VfxManager.VisualEffect.SlamShockwave, shockwave, 1, 2);
            });
        }

        // A column of light shooting up from the caller, in that player's color (set when it plays).
        static ParticleSystem BuildBeam(Material material)
        {
            var system = NewSystem("Vfx_CallBeam", material, 0.6f, false, 80);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
            main.startColor = Color.white;
            var emission = system.emission;
            emission.rateOverTime = 80f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            return system;
        }

        // A ring of dust and sparkle racing out from the slam.
        static ParticleSystem BuildShockwave(Material material)
        {
            var system = NewSystem("Vfx_SlamShockwave", material, 0.8f, false, 90);
            var main = system.main;
            // Short enough that the ring stops well before the camera behind the ninjas.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.8f, 0.3f));
            Burst(system, 70);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            GrowOverLife(system, 1.8f);
            return system;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            var root = new GameObject("CallGame");
            var game = root.AddComponent<CallGame>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(KidsViewsBuilder.ResultViewPath));
            Set(game, "config", Load<CallConfig>(ConfigPath));
            Set(game, "viewPrefab", LoadComponent<CallView>(ViewPath));
            SetLocalized(game, "title", "ninjago.result.call_title");
            SetLocalized(game, "callsLine", "ninjago.result.call_calls");
            SetLocalized(game, "calmLine", "ninjago.result.call_calm");
            SetLocalized(game, "busyLine", "ninjago.result.call_busy");
            SetLocalized(game, "wavesLine", "ninjago.result.call_waves");
            SetLocalized(game, "calmFinishLine", "ninjago.result.call_calm_finish");
            SetLocalized(game, "notCalmFinishLine", "ninjago.result.call_not_calm_finish");
            SetLocalized(game, "bestLine", "ninjago.result.call_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, GamePath);
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.Call, GamePath, "ninjago.mode.call");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
