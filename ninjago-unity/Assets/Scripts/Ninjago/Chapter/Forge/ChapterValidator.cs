#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    public readonly struct ForgeProblem
    {
        public readonly ForgeRule rule;
        /// <summary>A warning explains but does not block saving or playing.</summary>
        public readonly bool isWarning;
        public readonly int level;
        public readonly int block;
        /// <summary>Rule-specific: the other level for difficulty, the minutes for length.</summary>
        public readonly int other;

        public ForgeProblem(ForgeRule rule, int level = -1, int block = -1, int other = -1, bool isWarning = false)
        {
            this.rule = rule;
            this.level = level;
            this.block = block;
            this.other = other;
            this.isWarning = isWarning;
        }
    }

    /// <summary>
    /// The rules every mode is checked by, wherever it was made (assets, the Forge view, Surprise me): 2 to 6 blocks a
    /// level with no type twice in a row, links that resolve, a car and a skycraft somewhere, the Boss level last with no
    /// stealth, only existing gestures and a closing chase, difficulty that never drops between normal levels, and a
    /// warning for levels outside about 4 to 6 minutes. Each problem reads as one plain sentence.
    /// </summary>
    public static class ChapterValidator
    {
        public const int MinBlocks = 2;
        public const int MaxBlocks = 6;
        public const float MinLevelMinutes = 3.75f;
        public const float MaxLevelMinutes = 6.25f;

        #region Public API

        public static List<ForgeProblem> Validate(ChapterData chapter, ForgeCatalog catalog, float linkSeconds)
        {
            var problems = new List<ForgeProblem>();
            Validate(chapter, catalog, linkSeconds, problems);
            return problems;
        }

        public static void Validate(ChapterData chapter, ForgeCatalog catalog, float linkSeconds, List<ForgeProblem> problems)
        {
            problems.Clear();
            if (chapter.levels.Count == 0)
            {
                problems.Add(new ForgeProblem(ForgeRule.NoLevels));
                return;
            }

            var usesCar = false;
            var usesSkycraft = false;
            var previousNormal = -1;
            var previousIntensity = 0f;
            for (var levelIndex = 0; levelIndex < chapter.levels.Count; levelIndex++)
            {
                var level = chapter.levels[levelIndex];
                ValidateBlocks(level, levelIndex, catalog, problems, ref usesCar, ref usesSkycraft);
                if (level.IsBoss)
                {
                    ValidateBoss(level, levelIndex, chapter.levels.Count, catalog, problems);
                }
                else if (level.blocks.Count > 0)
                {
                    var intensity = Intensity(level);
                    if (previousNormal >= 0 && intensity < previousIntensity - 0.001f)
                    {
                        problems.Add(new ForgeProblem(ForgeRule.DifficultyDrops, levelIndex, -1, previousNormal));
                    }

                    previousNormal = levelIndex;
                    previousIntensity = intensity;
                }

                var minutes = level.TargetSeconds(linkSeconds) / 60f;
                if (level.blocks.Count > 0 && (minutes < MinLevelMinutes || minutes > MaxLevelMinutes))
                {
                    problems.Add(new ForgeProblem(ForgeRule.LevelLength, levelIndex, -1, Mathf.RoundToInt(minutes), true));
                }
            }

            if (!usesCar || !usesSkycraft) problems.Add(new ForgeProblem(ForgeRule.VehiclesMissing));
        }

        public static bool HasErrors(IReadOnlyList<ForgeProblem> problems)
        {
            foreach (var problem in problems)
            {
                if (!problem.isWarning) return true;
            }

            return false;
        }

        /// <summary>The problem as one plain sentence (levels and blocks counted from 1).</summary>
        public static LocalizedString Describe(ForgeProblem problem, ForgeConfig config) =>
            LocalizedStrings.WithArguments(config.RuleLine(problem.rule), ("level", problem.level + 1), ("block", problem.block + 1),
                ("other", problem.rule == ForgeRule.DifficultyDrops ? problem.other + 1 : problem.other));

        /// <summary>A level's difficulty: the mean intensity (speed over window) of its blocks.</summary>
        public static float Intensity(LevelData level)
        {
            if (level.blocks.Count == 0) return 0f;
            var sum = 0f;
            foreach (var block in level.blocks) sum += block.difficulty.Intensity;
            return sum / level.blocks.Count;
        }

        #endregion

        #region Rules

        static void ValidateBlocks(LevelData level, int levelIndex, ForgeCatalog catalog, List<ForgeProblem> problems, ref bool usesCar,
            ref bool usesSkycraft)
        {
            if (level.blocks.Count < MinBlocks || level.blocks.Count > MaxBlocks) problems.Add(new ForgeProblem(ForgeRule.BlockCount, levelIndex));
            for (var b = 0; b < level.blocks.Count; b++)
            {
                var block = level.blocks[b];
                if (b > 0 && block.type == level.blocks[b - 1].type) problems.Add(new ForgeProblem(ForgeRule.RepeatedType, levelIndex, b));
                if (block.mix.Count > 0 && !MixIsFight(block, catalog)) problems.Add(new ForgeProblem(ForgeRule.MixNotFight, levelIndex, b));
                if (catalog.UsesVehicle(block))
                {
                    usesCar |= block.ride.Uses(VehicleType.Car);
                    usesSkycraft |= block.ride.Uses(VehicleType.Skycraft);
                }

                if (b == 0) continue;
                var link = b - 1 < level.links.Count ? level.links[b - 1] : null;
                if (link != null && !LinkResolver.Resolves(link, level.blocks[b - 1], block, catalog))
                {
                    problems.Add(new ForgeProblem(ForgeRule.LinkUnresolved, levelIndex, b - 1));
                }
            }
        }

        static void ValidateBoss(LevelData level, int levelIndex, int levelCount, ForgeCatalog catalog, List<ForgeProblem> problems)
        {
            if (levelIndex != levelCount - 1) problems.Add(new ForgeProblem(ForgeRule.BossNotLast, levelIndex));
            var stealth = false;
            var unknown = false;
            var types = new List<BlockType>();
            foreach (var block in level.blocks)
            {
                catalog.TypesOf(block, types);
                foreach (var type in types)
                {
                    var spec = catalog.Block(type);
                    stealth |= !spec.allowedInBossLevel;
                    unknown |= spec.prefab == null || spec.gestures.Count == 0;
                }
            }

            if (stealth) problems.Add(new ForgeProblem(ForgeRule.BossHasStealth, levelIndex));
            if (unknown) problems.Add(new ForgeProblem(ForgeRule.BossUnknownGesture, levelIndex));
            var last = level.blocks.Count > 0 ? level.blocks[^1] : null;
            if (last == null || !last.boss || !catalog.UsesVehicle(last)) problems.Add(new ForgeProblem(ForgeRule.BossNoEscape, levelIndex));
        }

        static bool MixIsFight(BlockEntry block, ForgeCatalog catalog)
        {
            if (!catalog.Block(block.type).isFight) return false;
            foreach (var type in block.mix)
            {
                if (!catalog.Block(type).isFight) return false;
            }

            return true;
        }

        #endregion
    }
}
