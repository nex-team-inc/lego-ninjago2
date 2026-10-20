#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Surprise me: a valid chapter from a seed, the same chapter for the same seed. Normal levels ramp from easy to
    /// hard, Level 1 has every block type with first-time hints, no type repeats in a row, the car and the skycraft both
    /// appear, and the Boss level comes last (fight phases, a mixed phase, then the chase it escapes from).
    /// </summary>
    public static class ChapterGenerator
    {
        #region Public API

        public static ModeData Generate(int seed, ForgeCatalog catalog, ForgeConfig config, string modeId)
        {
            var random = new System.Random(seed);
            var chapter = new ChapterData();
            var normalCount = random.Next(config.NormalLevels.x, config.NormalLevels.y + 1);
            var themes = new List<ChapterTheme> { ChapterTheme.TownDusk, ChapterTheme.HarborNight, ChapterTheme.MountainTemple };
            Shuffle(themes, random);
            var types = new List<BlockType>();
            foreach (var type in catalog.BlockTypes)
            {
                if (catalog.Block(type).prefab != null) types.Add(type);
            }

            for (var levelIndex = 0; levelIndex < normalCount; levelIndex++)
            {
                var ramp = normalCount == 1 ? 0f : levelIndex / (float)(normalCount - 1);
                var level = NewLevel(themes[levelIndex % themes.Count], config.LevelMusic[levelIndex % config.LevelMusic.Count], config);
                var blockCount = random.Next(config.BlocksPerLevel.x, config.BlocksPerLevel.y + 1);
                var order = levelIndex == 0 ? EveryTypeFirst(types, blockCount, random) : NoRepeats(types, blockCount, random);
                foreach (var type in order) level.blocks.Add(Block(type, ramp, levelIndex, normalCount, catalog, random));
                chapter.levels.Add(level);
            }

            chapter.levels.Add(BossLevel(catalog, config, random));
            EnsureBothVehicles(chapter, catalog);
            foreach (var level in chapter.levels) LinkResolver.Refresh(level, catalog);
            return new ModeData
            {
                id = modeId,
                name = config.RandomName(random, seed),
                icon = ModeIcon.Dice,
                builtIn = false,
                chapter = chapter,
            };
        }

        #endregion

        #region Levels

        static LevelData NewLevel(ChapterTheme theme, BgmManager.BgmType music, ForgeConfig config) => new()
        {
            theme = theme,
            music = music,
            hearts = config.GeneratedHearts,
        };

        // Difficulty ramps by the level's place: Easy first, Hard last, fine values strictly between, so it never drops.
        static BlockEntry Block(BlockType type, float ramp, int levelIndex, int normalCount, ForgeCatalog catalog, System.Random random)
        {
            var preset = levelIndex == 0 ? DifficultyPreset.Easy : levelIndex == normalCount - 1 ? DifficultyPreset.Hard : DifficultyPreset.Normal;
            var entry = catalog.NewEntry(type, preset);
            entry.difficulty.windowScale = Mathf.Lerp(1.4f, 0.85f, ramp);
            entry.difficulty.speedScale = Mathf.Lerp(0.85f, 1.15f, ramp);
            entry.hints = levelIndex == 0 ? HintPolicy.FirstTime : levelIndex == normalCount - 1 ? HintPolicy.None : HintPolicy.NewOnly;
            if (catalog.UsesVehicle(entry)) entry.ride = levelIndex == 0 ? RunnerRide.Car : (RunnerRide)random.Next(3);
            if (catalog.Block(type).isFight && levelIndex == normalCount - 1 && normalCount > 1) entry.difficulty.overlapEvery = 4;
            return entry;
        }

        static LevelData BossLevel(ForgeCatalog catalog, ForgeConfig config, System.Random random)
        {
            var level = NewLevel(ChapterTheme.StormCitadel, config.BossMusic, config);
            var fights = new List<BlockType>();
            foreach (var type in catalog.BlockTypes)
            {
                var spec = catalog.Block(type);
                if (spec.isFight && spec.allowedInBossLevel && spec.prefab != null) fights.Add(type);
            }

            Shuffle(fights, random);
            var phases = random.Next(config.BossFightPhases.x, config.BossFightPhases.y + 1);
            for (var phase = 0; phase < phases; phase++)
            {
                var last = phase == phases - 1 && fights.Count > 1;
                var type = fights[phase % fights.Count];
                if (level.blocks.Count > 0 && level.blocks[^1].type == type) type = fights[(phase + 1) % fights.Count];
                var entry = BossEntry(catalog, type, last ? 90f : 70f, last ? 12 : 7);
                if (last)
                {
                    // The mixed phase: every fight type in one readable pattern, the hardest part of the chapter.
                    foreach (var other in fights)
                    {
                        if (other != type) entry.mix.Add(other);
                    }

                    entry.pattern = config.BossMixPattern;
                    entry.difficulty.windowScale = 0.7f;
                }

                level.blocks.Add(entry);
            }

            var chase = BossEntry(catalog, BlockType.Runner, 60f, 6);
            chase.ride = random.Next(2) == 0 ? RunnerRide.Skycraft : RunnerRide.CarThenSkycraft;
            level.blocks.Add(chase);
            return level;
        }

        static BlockEntry BossEntry(ForgeCatalog catalog, BlockType type, float seconds, int count)
        {
            var entry = catalog.NewEntry(type, DifficultyPreset.Hard);
            entry.boss = true;
            entry.hints = HintPolicy.None;
            entry.targetSeconds = seconds;
            entry.difficulty.windowScale = 0.75f;
            entry.difficulty.speedScale = 1.2f;
            entry.difficulty.count = count;
            return entry;
        }

        // Level 1 meets every type once, in a random order, then fills up without repeats.
        static List<BlockType> EveryTypeFirst(List<BlockType> types, int count, System.Random random)
        {
            var order = new List<BlockType>(types);
            Shuffle(order, random);
            while (order.Count < count) order.Add(Pick(types, order[^1], random));
            return order;
        }

        static List<BlockType> NoRepeats(List<BlockType> types, int count, System.Random random)
        {
            var order = new List<BlockType>(count) { types[random.Next(types.Count)] };
            while (order.Count < count) order.Add(Pick(types, order[^1], random));
            return order;
        }

        static BlockType Pick(List<BlockType> types, BlockType previous, System.Random random)
        {
            var type = types[random.Next(types.Count - 1)];
            return type == previous ? types[^1] : type;
        }

        // The first ride is a car; when no ride used the skycraft, the last ride before the Boss (or the chase) gets it.
        static void EnsureBothVehicles(ChapterData chapter, ForgeCatalog catalog)
        {
            var usesCar = false;
            var usesSky = false;
            BlockEntry? firstRide = null;
            BlockEntry? lastRide = null;
            foreach (var level in chapter.levels)
            {
                foreach (var block in level.blocks)
                {
                    if (!catalog.UsesVehicle(block)) continue;
                    firstRide ??= block;
                    lastRide = block;
                    usesCar |= block.ride.Uses(VehicleType.Car);
                    usesSky |= block.ride.Uses(VehicleType.Skycraft);
                }
            }

            if (lastRide == null) return;
            if (!usesCar) firstRide!.ride = firstRide == lastRide ? RunnerRide.CarThenSkycraft : RunnerRide.Car;
            if (!usesSky) lastRide.ride = firstRide == lastRide ? RunnerRide.CarThenSkycraft : RunnerRide.Skycraft;
        }

        static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}
