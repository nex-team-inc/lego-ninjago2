#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    // The Boss Battle config assets (rebuilt by Nex > Ninjago > Boss Battle > Build Config) and the registries their fx
    // entries point at (TDD §9). Keys are read from the serialized data, so a key the accessors would fill from the
    // defaults at runtime still fails here.
    public class BossBattleConfigTests
    {
        const string ConfigPath = "Assets/Configs/Ninjago/BossBattle/BossBattleConfig.asset";
        const string VisualConfigPath = "Assets/Configs/Ninjago/BossBattle/BossBattleVisualConfig.asset";
        const string SfxPrefab = "Assets/Prefabs/Singletons/SfxManager.prefab";
        const string VfxPrefab = "Assets/Prefabs/Singletons/VfxManager.prefab";
        const string BgmPrefab = "Assets/Prefabs/Singletons/BgmManager.prefab";
        const string GameModeConfigPath = "Assets/Configs/GameModeConfig.asset";

        static BossBattleConfig Config
        {
            get
            {
                var config = AssetDatabase.LoadAssetAtPath<BossBattleConfig>(ConfigPath);
                Assert.IsNotNull(config, $"{ConfigPath} is missing (run Nex > Ninjago > Boss Battle > Build Config)");
                return config;
            }
        }

        static BossBattleVisualConfig VisualConfig
        {
            get
            {
                var config = AssetDatabase.LoadAssetAtPath<BossBattleVisualConfig>(VisualConfigPath);
                Assert.IsNotNull(config, $"{VisualConfigPath} is missing (run Nex > Ninjago > Boss Battle > Build Config)");
                return config;
            }
        }

        #region Keys

        [Test]
        public void EveryDictionaryKeyIsInTheAsset()
        {
            var serialized = new SerializedObject(Config);
            AssertAllKeys<BossPhase>(serialized, "vehicles");
            AssertAllKeys<BossDifficulty>(serialized, "difficulties");
            AssertAllKeys<BossHazard>(serialized, "hazardDamage");
            AssertAllKeys<BossFxCue>(serialized, "fx");
            AssertAllKeys<BossPromptId>(serialized, "prompts");
            AssertAllKeys<BossAttackIcon>(serialized, "tellColors");

            var visual = new SerializedObject(VisualConfig);
            AssertAllKeys<BossLook>(visual, "looks");
            AssertAllKeys<BossPhase>(visual, "cameraPoses");
            AssertAllKeys<BossCutscene>(visual, "cutsceneShots");
        }

        [Test]
        public void EveryEntryHasUsableValues()
        {
            var config = Config;
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase)))
            {
                var vehicle = config.Vehicle(phase);
                Assert.Greater(vehicle.speed, 0f, $"{phase} speed");
                Assert.Greater(vehicle.halfWidth, 0f, $"{phase} half width");
                Assert.Greater(vehicle.steerSmoothingSeconds, 0f, $"{phase} smoothing");
                Assert.Greater(vehicle.maxSteerSpeed, 0f, $"{phase} max steer speed");
                Assert.AreEqual(phase == BossPhase.Sky, vehicle.usesVerticalLean, $"{phase} vertical lean");
            }

            foreach (BossDifficulty difficulty in Enum.GetValues(typeof(BossDifficulty)))
            {
                var settings = config.Difficulty(difficulty);
                Assert.Greater(settings.hearts, 0, $"{difficulty} hearts");
                Assert.GreaterOrEqual(settings.timingScale, 1f, $"{difficulty} timing scale");
                Assert.GreaterOrEqual(settings.laserRadiusScale, 1f, $"{difficulty} laser radius scale");
            }

            Assert.Greater(config.Difficulty(BossDifficulty.Easy).hearts, config.Difficulty(BossDifficulty.Normal).hearts);
            foreach (BossHazard hazard in Enum.GetValues(typeof(BossHazard))) Assert.Greater(config.HazardDamage(hazard), 0, $"{hazard}");

            foreach (BossFxCue cue in Enum.GetValues(typeof(BossFxCue))) Assert.IsTrue(config.TryGetFx(cue, out _), $"fx {cue}");

            foreach (BossPromptId id in Enum.GetValues(typeof(BossPromptId)))
            {
                var prompt = config.Prompt(id);
                StringAssert.StartsWith("ninjago.boss.", prompt.key, $"prompt {id}");
                Assert.Greater(prompt.color.a, 0f, $"prompt {id} colour");
            }

            foreach (BossAttackIcon icon in Enum.GetValues(typeof(BossAttackIcon)))
            {
                if (icon == BossAttackIcon.None) continue;
                var color = config.TellColor(icon);
                Assert.GreaterOrEqual(Mathf.Max(color.r, Mathf.Max(color.g, color.b)), 2.5f, $"tell {icon} must bloom (HDR >= 2.5)");
            }

            var visual = VisualConfig;
            foreach (BossLook look in Enum.GetValues(typeof(BossLook))) Assert.IsNotNull(visual.Look(look), $"look {look}");
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase))) Assert.Greater(visual.Pose(phase).fov, 0f, $"pose {phase}");
            foreach (BossCutscene cutscene in Enum.GetValues(typeof(BossCutscene))) Assert.IsNotNull(visual.Shots(cutscene), $"shots {cutscene}");
        }

        #endregion

        #region Pacing

        [Test]
        public void VolleysSumToTheRoadHpAndTheLastPickupEmptiesTheBar()
        {
            var config = Config;
            var powerUp = config.PowerUp;
            Assert.Greater(powerUp.volleys.Count, 0);
            Assert.Greater(powerUp.pickupsToPhase2, 0);
            var sum = 0f;
            for (var i = 0; i < powerUp.pickupsToPhase2; i++)
            {
                var rockets = RocketVolleys.RocketsFor(powerUp.volleys, i);
                Assert.Greater(rockets, 0, $"pickup {i + 1} fires no rocket");
                sum += rockets * RocketVolleys.DamagePerRocket(powerUp.volleys, i, powerUp.baseRocketDamage);
            }

            Assert.AreEqual(sum, config.RoadMaxHp, 1e-3f);
            Assert.AreEqual(0f, RocketVolleys.HpAfter(powerUp.volleys, powerUp.pickupsToPhase2, powerUp.baseRocketDamage, powerUp.pickupsToPhase2), 1e-3f);
            Assert.Greater(RocketVolleys.HpAfter(powerUp.volleys, powerUp.pickupsToPhase2 - 1, powerUp.baseRocketDamage, powerUp.pickupsToPhase2), 0f,
                "only the last pickup empties the bar");
        }

        [Test]
        public void RoadScriptLoopsInRangeAndOffersEveryCrate()
        {
            var road = Config.Road;
            Assert.Greater(road.script.Count, 0);
            Assert.That(road.loopFrom >= 0 && road.loopFrom < road.script.Count, $"loopFrom {road.loopFrom} out of 0..{road.script.Count - 1}");
            Assert.AreEqual(BossBeat.Ready, road.script[0].beat, "every phase starts with the ready beat");

            var crates = 0;
            var cratesInLoop = 0;
            for (var i = 0; i < road.script.Count; i++)
            {
                var entry = road.script[i];
                Assert.Less((int)entry.beat, 10, $"road entry {i} is a sky beat ({entry.beat})");
                if (entry.beat != BossBeat.PowerUp) continue;
                crates++;
                if (i >= road.loopFrom) cratesInLoop++;
            }

            Assert.GreaterOrEqual(crates, Config.PowerUp.pickupsToPhase2, "the script offers every pickup the phase needs");
            Assert.Greater(cratesInLoop, 0, "the loop offers crates, so a looping phase can still end");
            Assert.IsFalse(road.script[road.script.Count - 1].overlapNext, "the last entry has nothing to overlap");
        }

        [Test]
        public void SkyScriptHasALaserWindowPerNotchAndTheFinish()
        {
            var config = Config;
            var sky = config.Sky;
            Assert.Greater(sky.script.Count, 0);
            Assert.AreEqual(BossBeat.Ready, sky.script[0].beat);
            var windows = 0;
            foreach (var entry in sky.script)
            {
                Assert.That((int)entry.beat >= 10 || entry.beat == BossBeat.Ready || entry.beat == BossBeat.Breather, $"sky entry {entry.beat}");
                if (entry.beat == BossBeat.LaserWindow) windows++;
            }

            Assert.AreEqual(sky.wakeThresholds.Count + 1, windows, "one laser window per wake notch, plus the finish");
            Assert.AreEqual(BossBeat.LaserWindow, sky.script[sky.script.Count - 1].beat, "the sky ends on the finishing laser");
            Assert.Greater(config.SkyMaxHp, 0f);
        }

        [Test]
        public void WakeThresholdsDescendInsideTheBar()
        {
            var thresholds = Config.Sky.wakeThresholds;
            Assert.Greater(thresholds.Count, 0);
            for (var i = 0; i < thresholds.Count; i++)
            {
                Assert.That(thresholds[i] > 0f && thresholds[i] < 1f, $"threshold {i} = {thresholds[i]}");
                if (i > 0) Assert.Less(thresholds[i], thresholds[i - 1], $"threshold {i} must be below threshold {i - 1}");
            }

            Assert.GreaterOrEqual(Config.Laser.boltDamageByWindow.Count, thresholds.Count + 1, "a bolt damage per window (notches + finish)");
        }

        [Test]
        public void ClapBandsLeaveASafeRow()
        {
            var config = Config;
            var sky = config.Vehicle(BossPhase.Sky);
            var gridBottom = sky.centerHeight - sky.halfHeight;
            var gridTop = sky.centerHeight + sky.halfHeight;
            Assert.Greater(config.Clap.sizes.Count, 0);
            foreach (var size in config.Clap.sizes)
            {
                var walls = new ClapWalls(size, gridBottom, gridTop);
                Assert.IsFalse(walls.IsHit(gridBottom, sky.vehicleHalfExtents.y), $"band {size.band}: the bottom row must be safe");
                Assert.IsTrue(walls.IsHit(gridTop, sky.vehicleHalfExtents.y), $"band {size.band}: the top row must be hit");
                Assert.Greater(size.warnSeconds, 0f);
            }
        }

        #endregion

        #region Registries

        [Test]
        public void FxEntriesPointAtRegisteredEnums()
        {
            var config = Config;
            var sfxKeys = RegistryKeys<SfxManager>(SfxPrefab, "soundEffectDict");
            var vfxKeys = RegistryKeys<VfxManager>(VfxPrefab, "effectSpecs");
            foreach (BossFxCue cue in Enum.GetValues(typeof(BossFxCue)))
            {
                Assert.IsTrue(config.TryGetFx(cue, out var fx), $"fx {cue}");
                if (fx.hasSound)
                {
                    Assert.IsTrue(Enum.IsDefined(typeof(SfxManager.SoundEffect), fx.sound), $"{cue}: undefined sound {(int)fx.sound}");
                    Assert.IsTrue(sfxKeys.Contains((int)fx.sound), $"{cue}: {fx.sound} has no SfxManager registry entry (run EnumDictionaryRepair)");
                }

                if (fx.hasVisual)
                {
                    Assert.IsTrue(Enum.IsDefined(typeof(VfxManager.VisualEffect), fx.visual), $"{cue}: undefined visual {(int)fx.visual}");
                    Assert.IsTrue(vfxKeys.Contains((int)fx.visual), $"{cue}: {fx.visual} has no VfxManager registry entry (run EnumDictionaryRepair)");
                }
            }
        }

        [Test]
        public void EveryBossKeyHasARegistryEntry()
        {
            // TDD §2: SoundEffect 800-834, VisualEffect 300-331, BgmType 51-52.
            AssertRegistered<SfxManager, SfxManager.SoundEffect>(SfxPrefab, "soundEffectDict", 800, 834);
            AssertRegistered<VfxManager, VfxManager.VisualEffect>(VfxPrefab, "effectSpecs", 300, 331);
            AssertRegistered<BgmManager, BgmManager.BgmType>(BgmPrefab, "bgmDict", 51, 52);
        }

        [Test]
        public void BossBattleModeEntryIsNeverAnotherModesCopy()
        {
            // EnumDictionary inserts copy their neighbour (Forge); key 15 must stay empty or hold its own game prefab.
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameModeConfigPath);
            Assert.IsNotNull(config, GameModeConfigPath);
            var pairs = new SerializedObject(config).FindProperty("modes.pairs");
            Assert.IsNotNull(pairs);
            var bossGuid = "";
            var otherGuids = new HashSet<string>();
            for (var i = 0; i < pairs.arraySize; i++)
            {
                var pair = pairs.GetArrayElementAtIndex(i);
                var guid = pair.FindPropertyRelative("value.gamePrefab.m_AssetGUID")?.stringValue ?? "";
                if (pair.FindPropertyRelative("key").intValue == (int)GameModeType.BossBattle) bossGuid = guid;
                else if (!string.IsNullOrEmpty(guid)) otherGuids.Add(guid);
            }

            Assert.IsFalse(otherGuids.Contains(bossGuid), $"GameModeConfig key 15 points at another mode's game prefab ({bossGuid})");
        }

        #endregion

        #region Helpers

        static HashSet<int> Keys(SerializedObject serialized, string dictionaryField)
        {
            var pairs = serialized.FindProperty(dictionaryField + ".pairs");
            Assert.IsNotNull(pairs, $"{serialized.targetObject.name}.{dictionaryField} has no pairs");
            var keys = new HashSet<int>();
            for (var i = 0; i < pairs.arraySize; i++) keys.Add(pairs.GetArrayElementAtIndex(i).FindPropertyRelative("key").intValue);
            return keys;
        }

        static void AssertAllKeys<TEnum>(SerializedObject serialized, string dictionaryField) where TEnum : Enum
        {
            var keys = Keys(serialized, dictionaryField);
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                Assert.IsTrue(keys.Contains(Convert.ToInt32(value)), $"{serialized.targetObject.name}.{dictionaryField} misses key {value}");
            }
        }

        static HashSet<int> RegistryKeys<TComponent>(string prefabPath, string dictionaryField) where TComponent : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            var component = prefab.GetComponent<TComponent>();
            Assert.IsNotNull(component, $"{prefabPath} has no {typeof(TComponent).Name}");
            return Keys(new SerializedObject(component), dictionaryField);
        }

        static void AssertRegistered<TComponent, TEnum>(string prefabPath, string dictionaryField, int firstKey, int lastKey)
            where TComponent : Component where TEnum : Enum
        {
            var keys = RegistryKeys<TComponent>(prefabPath, dictionaryField);
            for (var key = firstKey; key <= lastKey; key++)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(TEnum), key), $"{typeof(TEnum).Name} has no value {key}");
                Assert.IsTrue(keys.Contains(key), $"{prefabPath}: {(TEnum)Enum.ToObject(typeof(TEnum), key)} has no registry entry (run EnumDictionaryRepair)");
            }
        }

        #endregion
    }
}
