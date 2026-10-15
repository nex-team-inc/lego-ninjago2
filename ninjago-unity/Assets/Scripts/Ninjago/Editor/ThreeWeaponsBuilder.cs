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
    /// Three Weapons: card icon sprites, the tuning asset, audio registry entries, VFX, world, views, the Addressable
    /// game prefab (a hand cursor game, so setup confirms the cursors) and its menu entry. Rebuilds keep GUIDs and tuning.
    /// CLI (only this mini-game): unity command eval 'return Nex.Ninjago.Editor.ThreeWeaponsBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class ThreeWeaponsBuilder
    {
        public const string ConfigPath = ConfigsRoot + "/ThreeWeaponsConfig.asset";
        const string GamePath = NinjagoGamesBuilder.GamesRoot + "/ThreeWeaponsGame.prefab";
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Three Weapons")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = NinjagoLocalizationSeeder.Run();
            Build();
            AssetDatabase.SaveAssets();
            return $"[ThreeWeaponsBuilder] done ({localization})";
        }

        public static void Build()
        {
            BuildSprites();
            BuildConfig();
            BuildAudioRegistry();
            BuildVfx();
            ThreeWeaponsWorldBuilder.Build();
            ThreeWeaponsViewsBuilder.Build();
            BuildGame();
            RegisterMode();
        }

        #endregion

        #region Sprites

        static void BuildSprites()
        {
            NinjagoAssetsBuilder.BuildSprite("SwordIcon", 128, (u, v) =>
            {
                var blade = Mathf.Abs(u) <= 0.1f && v >= -0.3f && v <= 0.92f - Mathf.Abs(u) * 2f;
                var guard = Mathf.Abs(u) <= 0.42f && v >= -0.42f && v <= -0.3f;
                var hilt = Mathf.Abs(u) <= 0.08f && v >= -0.82f && v <= -0.42f;
                return blade || guard || hilt || new Vector2(u, v + 0.86f).magnitude <= 0.12f;
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("NunchuckIcon", 128, (u, v) =>
            {
                var point = new Vector2(u, v);
                var left = SegmentDistance(point, new Vector2(-0.7f, -0.75f), new Vector2(-0.18f, 0.45f)) <= 0.13f;
                var right = SegmentDistance(point, new Vector2(0.7f, -0.75f), new Vector2(0.18f, 0.45f)) <= 0.13f;
                var chain = SegmentDistance(point, new Vector2(-0.18f, 0.45f), new Vector2(0.18f, 0.45f)) <= 0.05f ||
                    (new Vector2(u, v - 0.62f).magnitude <= 0.2f && new Vector2(u, v - 0.62f).magnitude >= 0.12f && v > 0.5f);
                return left || right || chain;
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("StarIcon", 128, (u, v) =>
            {
                var r = new Vector2(u, v).magnitude;
                var star = Mathf.Abs(u) / 0.95f + Mathf.Abs(v) / 0.26f <= 1f || Mathf.Abs(u) / 0.26f + Mathf.Abs(v) / 0.95f <= 1f || r <= 0.3f;
                return star && r >= 0.09f;
            }, Vector4.zero);
            NinjagoAssetsBuilder.BuildSprite("SpinIcon", 128, (u, v) =>
            {
                var r = new Vector2(u, v).magnitude;
                var angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                var ring = r >= 0.56f && r <= 0.82f && (angle < -25f || angle > 25f);
                // Arrowhead at the ring's open end, pointing round the circle.
                var head = u >= 0.42f && u <= 0.98f && v >= 0.02f && v <= 0.36f - Mathf.Abs(u - 0.69f) * 1.1f;
                return ring || head;
            }, Vector4.zero);
        }

        static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return (point - (a + ab * t)).magnitude;
        }

        #endregion

        #region Config

        static void BuildConfig()
        {
            var isNew = AssetDatabase.LoadAssetAtPath<ThreeWeaponsConfig>(ConfigPath) == null;
            var config = LoadOrCreate<ThreeWeaponsConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            var weapons = serialized.FindProperty("weapons");
            Weapon(weapons, WeaponType.Sword, isNew, 12, "ninjago.weapons.sword", "ninjago.weapons.slash", new Color(0.35f, 0.6f, 1f), "SwordIcon");
            Weapon(weapons, WeaponType.Nunchucks, isNew, 4, "ninjago.weapons.nunchucks", "ninjago.weapons.spin", new Color(1f, 0.55f, 0.15f), "NunchuckIcon");
            Weapon(weapons, WeaponType.Shuriken, isNew, 6, "ninjago.weapons.shuriken", "ninjago.weapons.throw", new Color(0.65f, 0.35f, 0.95f), "StarIcon");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                Set(root.GetComponent<GameConfigsManager>(), "threeWeaponsConfig", config);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Rebuilds keep a designer's hits to kill; words, colors and icons are always rewritten.
        static void Weapon(SerializedProperty weapons, WeaponType weapon, bool isNew, int hitsToKill, string labelKey, string promptKey, Color color, string icon)
        {
            var value = EnumDictionaryEditorUtils.GetValueProperty(weapons, (int)weapon);
            if (isNew) value.FindPropertyRelative("hitsToKill").intValue = hitsToKill;
            WriteLocalized(value.FindPropertyRelative("label"), labelKey);
            WriteLocalized(value.FindPropertyRelative("prompt"), promptKey);
            value.FindPropertyRelative("color").colorValue = color;
            value.FindPropertyRelative("icon").objectReferenceValue = NinjagoAssetsBuilder.GetSprite(icon);
        }

        #endregion

        #region Audio

        static void BuildAudioRegistry()
        {
            foreach (var path in new[] { $"{SfxRoot}/WeaponSlash_1.wav", $"{SfxRoot}/WeaponSlash_2.wav", $"{SfxRoot}/WeaponSpin_1.wav", $"{SfxRoot}/WeaponSpin_2.wav",
                         $"{SfxRoot}/WeaponThrow_1.wav", $"{SfxRoot}/WeaponThrow_2.wav", $"{BgmRoot}/ThreeWeapons.ogg" })
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
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.WeaponSlash, SfxRoot, "WeaponSlash_1", "WeaponSlash_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.WeaponSpin, SfxRoot, "WeaponSpin_1", "WeaponSpin_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.WeaponThrow, SfxRoot, "WeaponThrow_1", "WeaponThrow_2");
            });
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.ThreeWeapons).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/ThreeWeapons.ogg");
            });
        }

        #endregion

        #region VFX

        static void BuildVfx()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var slash = Save("Vfx_WeaponSlash", BuildSlash(soft));
            var spin = Save("Vfx_NunchuckSpin", BuildSpin(soft));
            var hit = Save("Vfx_ShurikenHit", BuildSparks(soft));
            var explosion = Load<GameObject>(PrefabsRoot + "/Vfx/Vfx_EnemyExplosion.prefab").GetComponent<ParticleSystem>();
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.WeaponSlash, slash, 4, 8);
                Register(dict, VfxManager.VisualEffect.NunchuckSpin, spin, 2, 4);
                Register(dict, VfxManager.VisualEffect.ShurikenHit, hit, 4, 8);
                Register(dict, VfxManager.VisualEffect.MonsterBreak, explosion, 2, 4);
            });
        }

        // A bright streak along the system's local X, turned to the camera by the lane.
        static ParticleSystem BuildSlash(Material material)
        {
            var system = NewSystem("Vfx_WeaponSlash", material, 0.4f, false, 50);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.6f, 0.85f, 1f));
            Burst(system, 40);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 1.3f;
            FadeOut(system);
            GrowOverLife(system, 0.2f);
            return system;
        }

        // An orange whirl around the called spot.
        static ParticleSystem BuildSpin(Material material)
        {
            var system = NewSystem("Vfx_NunchuckSpin", material, 0.6f, false, 60);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.3f));
            Burst(system, 45);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            Orbit(system, 0f, 1.5f, 0f);
            var velocity = system.velocityOverLifetime;
            velocity.orbitalZ = 16f;
            FadeOut(system);
            return system;
        }

        static ParticleSystem BuildSparks(Material material)
        {
            var system = NewSystem("Vfx_ShurikenHit", material, 0.4f, false, 30);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.8f, 0.6f, 1f));
            main.gravityModifier = 0.8f;
            Burst(system, 22);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            FadeOut(system);
            return system;
        }

        #endregion

        #region Game Prefab

        static void BuildGame()
        {
            CursorGamesBuilder.BuildGame<ThreeWeaponsGame>("ThreeWeaponsGame", GamePath, game =>
            {
                Set(game, "viewPrefab", LoadComponent<ThreeWeaponsView>(ThreeWeaponsViewsBuilder.ViewPath));
                Set(game, "config", Load<ThreeWeaponsConfig>(ConfigPath));
                SetLocalized(game, "title", "ninjago.result.weapons_title");
                SetLocalized(game, "weaponsLine", "ninjago.result.weapons_order");
                SetLocalized(game, "countsLine", "ninjago.result.weapons_counts");
                SetLocalized(game, "bestLine", "ninjago.result.weapons_best");
                SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            });
        }

        static void RegisterMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.ThreeWeapons, GamePath, "ninjago.mode.three_weapons");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            NinjagoGamesBuilder.WriteMenuOrder();
        }

        #endregion
    }
}
