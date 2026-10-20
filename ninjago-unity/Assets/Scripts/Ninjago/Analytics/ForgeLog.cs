#nullable enable

using System.Collections.Generic;
using Nex.Platform;

namespace Nex.Ninjago
{
    /// <summary>Forge analytics through AnalyticsManager: opening, every strip edit, validator problems shown, tests, saves and seeds.</summary>
    public sealed class ForgeLog
    {
        readonly HashSet<ForgeRule> shownRules = new();

        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        #region Public API

        public void Opened() => Analytics.TrackEvent("forge_opened");

        public void BlockAdded(ForgeDocument document) => Track("forge_block_added", document);

        public void BlockRemoved(ForgeDocument document) => Track("forge_block_removed", document);

        public void BlockMoved(ForgeDocument document) => Track("forge_block_moved", document);

        public void TestStarted(ForgeDocument document) => Track("forge_test_started", document);

        /// <summary>Logs each rule the first time it shows in the live list (again after it was cleared).</summary>
        public void ProblemsShown(IReadOnlyList<ForgeProblem> problems)
        {
            var current = new HashSet<ForgeRule>();
            foreach (var problem in problems)
            {
                current.Add(problem.rule);
                if (shownRules.Contains(problem.rule)) continue;
                Analytics.TrackEvent("forge_validator_problem_shown", new GameAnalyticsProperties
                {
                    ["rule"] = problem.rule.ToString(), ["level"] = problem.level + 1, ["block"] = problem.block + 1, ["warning"] = problem.isWarning,
                });
            }

            shownRules.Clear();
            shownRules.UnionWith(current);
        }

        public void ModeSaved(ModeData mode, int seed) => Mode("forge_mode_saved", mode, seed);

        public void ModeRenamed(ModeData mode) => Mode("forge_mode_renamed", mode, -1);

        public void ModeCopied(ModeData mode, string sourceId) => Analytics.TrackEvent("forge_mode_copied", new GameAnalyticsProperties
        {
            ["mode_id"] = mode.id, ["source_id"] = sourceId,
        });

        public void ModeDeleted(string modeId) => Analytics.TrackEvent("forge_mode_deleted", new GameAnalyticsProperties { ["mode_id"] = modeId });

        public void SeedGenerated(int seed) => Analytics.TrackEvent("forge_seed_generated", new GameAnalyticsProperties { ["seed"] = seed });

        public void SeedKept(int seed, string modeId) => Analytics.TrackEvent("forge_seed_kept", new GameAnalyticsProperties
        {
            ["seed"] = seed, ["mode_id"] = modeId,
        });

        #endregion

        #region Helpers

        static void Track(string name, ForgeDocument document)
        {
            var block = document.CurrentBlock;
            Analytics.TrackEvent(name, new GameAnalyticsProperties
            {
                ["mode_id"] = document.EditingId, ["level"] = document.Level + 1, ["block"] = document.Block + 1,
                ["block_type"] = block?.type.ToString() ?? "", ["blocks"] = document.CurrentLevel.blocks.Count,
            });
        }

        static void Mode(string name, ModeData mode, int seed) => Analytics.TrackEvent(name, new GameAnalyticsProperties
        {
            ["mode_id"] = mode.id, ["levels"] = mode.chapter.levels.Count, ["seed"] = seed,
        });

        #endregion
    }
}
