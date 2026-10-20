#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Nex.Ninjago.Editor.Tests
{
    /// <summary>The Forge rules on the shipped chapter and on hand-made bad data.</summary>
    public class ChapterRulesTests
    {
        ForgeCatalog catalog = null!;
        float linkSeconds;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ForgeCatalog>(ChapterAssetsBuilder.CatalogPath);
            linkSeconds = AssetDatabase.LoadAssetAtPath<ChapterTuning>(ChapterAssetsBuilder.TuningPath).LinkSeconds;
        }

        [Test]
        public void NinjaChapterProofPassesEveryRule()
        {
            var problems = ChapterValidator.Validate(Proof().chapter, catalog, linkSeconds);
            Assert.IsEmpty(problems, Describe(problems));
        }

        [Test]
        public void EveryBuiltInModeIsDataThatPassesTheRules()
        {
            var library = AssetDatabase.LoadAssetAtPath<ChapterModeLibrary>(ChapterModesBuilder.LibraryPath);
            Assert.GreaterOrEqual(library.Modes.Count, 4, "Ninja Chapter Proof and the three examples");
            foreach (var definition in library.Modes)
            {
                var problems = ChapterValidator.Validate(definition.ToModeData().chapter, catalog, linkSeconds);
                Assert.IsFalse(ChapterValidator.HasErrors(problems), $"{definition.Id}: {Describe(problems)}");
            }
        }

        [Test]
        public void ProofLinksAreNaturalWhereTheStoryFits()
        {
            var levels = Proof().chapter.levels;
            AssertLinks(levels[0], LinkState.Ambush, LinkState.EnemyFled, LinkState.AtWall);
            AssertLinks(levels[1], LinkState.OnRoof, LinkState.GuardAhead, LinkState.CrowdIncoming);
            AssertLinks(levels[2], LinkState.AtWall, LinkState.GuardAhead, LinkState.EnemyFled);
            AssertLinks(levels[3], LinkState.BossPhase, LinkState.BossPhase, LinkState.BossBroken);
        }

        [Test]
        public void IntoAndOutOfStealthTheCameraSwings()
        {
            var level = Proof().chapter.levels[0];
            Assert.AreEqual(CameraMove.SwingBehind, level.links[0].cameraMove);
            Assert.AreEqual(CameraMove.SwingToSide, Proof().chapter.levels[1].links[0].cameraMove);
        }

        [Test]
        public void BrickRebuildLinksAnyTwoBlocks()
        {
            foreach (var a in catalog.BlockTypes)
            {
                foreach (var b in catalog.BlockTypes)
                {
                    var link = LinkResolver.ForState(LinkState.None, Entry(a), Entry(b), catalog, 0);
                    Assert.IsTrue(LinkResolver.Resolves(link, Entry(a), Entry(b), catalog), $"{a} -> {b}");
                    Assert.AreEqual(LinkMoment.BrickRebuild, link.moment);
                }
            }
        }

        [Test]
        public void EachRuleExplainsItsProblem()
        {
            AssertRule(ForgeRule.BlockCount, Chapter(Level(BlockType.SlashKick)));
            AssertRule(ForgeRule.RepeatedType, Chapter(Level(BlockType.SlashKick, BlockType.SlashKick, Ride(RunnerRide.CarThenSkycraft))));
            AssertRule(ForgeRule.VehiclesMissing, Chapter(Level(BlockType.Stealth, BlockType.SlashKick, Ride(RunnerRide.Car))));
            var bad = Level(BlockType.SlashKick, Ride(RunnerRide.CarThenSkycraft));
            bad.links[0].auto = false;
            bad.links[0].state = LinkState.GuardAhead;
            AssertRule(ForgeRule.LinkUnresolved, Chapter(bad));
            var boss = Level(BlockType.SlashKick, BossChase());
            boss.blocks[0].boss = true;
            AssertRule(ForgeRule.BossNotLast, Chapter(boss, Level(BlockType.Stealth, Ride(RunnerRide.CarThenSkycraft))));
            var stealthBoss = Level(BlockType.Stealth, BossChase());
            AssertRule(ForgeRule.BossHasStealth, Chapter(stealthBoss));
            var noEscape = Level(BlockType.SlipSpin, Ride(RunnerRide.CarThenSkycraft), BlockType.SlashKick);
            noEscape.blocks[0].boss = true;
            AssertRule(ForgeRule.BossNoEscape, Chapter(noEscape));
            var hard = Level(BlockType.SlashKick, Ride(RunnerRide.CarThenSkycraft));
            hard.blocks[0].difficulty.windowScale = 0.7f;
            AssertRule(ForgeRule.DifficultyDrops, Chapter(hard, Level(BlockType.SlashKick, Ride(RunnerRide.CarThenSkycraft))));
            var mixed = Level(BlockType.Stealth, Ride(RunnerRide.CarThenSkycraft));
            mixed.blocks[0].mix.Add(BlockType.SlashKick);
            AssertRule(ForgeRule.MixNotFight, Chapter(mixed));
        }

        [Test]
        public void ShortLevelsWarnWithoutBlocking()
        {
            var level = Level(BlockType.SlashKick, Ride(RunnerRide.CarThenSkycraft));
            var problems = ChapterValidator.Validate(Chapter(level), catalog, linkSeconds);
            Assert.IsTrue(problems.Exists(p => p.rule == ForgeRule.LevelLength && p.isWarning), Describe(problems));
            Assert.IsFalse(ChapterValidator.HasErrors(problems), Describe(problems));
        }

        [Test]
        public void BossMixedPhasePlaysTheDesignedPattern()
        {
            var mixed = Proof().chapter.levels[3].blocks[2];
            var steps = new List<FightPattern.Step>();
            FightPattern.Build(mixed, catalog, steps);
            Assert.AreEqual(14, steps.Count);
            Assert.AreEqual(BlockType.SlashKick, steps[0].type);
            Assert.AreEqual(BlockType.SlipSpin, steps[1].type);
            Assert.IsTrue(steps[4].overlapsNext, "K+S overlaps");
            Assert.AreEqual(BlockType.SlipSpin, steps[5].type);
            Assert.IsFalse(steps[3].overlapsNext);
        }

        [Test]
        public void HintsTeachUntilDoneAndOnlyWhenNew()
        {
            var hints = new ChapterHints();
            hints.BeginLevel();
            Assert.IsTrue(hints.Teaches(HintPolicy.FirstTime, Gesture.Slash));
            Assert.IsTrue(hints.Teaches(HintPolicy.FirstTime, Gesture.Slash), "still teaching: not done yet");
            hints.Learned(Gesture.Slash);
            Assert.IsFalse(hints.Teaches(HintPolicy.FirstTime, Gesture.Slash));
            hints.BeginLevel();
            Assert.IsTrue(hints.Teaches(HintPolicy.FirstTime, Gesture.Slash), "first time in a new level");
            hints.BeginLevel();
            Assert.IsFalse(hints.Teaches(HintPolicy.NewOnly, Gesture.Slash), "seen in an earlier level");
            Assert.IsTrue(hints.Teaches(HintPolicy.NewOnly, Gesture.Spin));
            Assert.IsFalse(hints.Teaches(HintPolicy.None, Gesture.Kick));
        }

        [Test]
        public void StarsRewardFinishingHeartsAndNoRetry()
        {
            var rules = new StarRules();
            Assert.AreEqual(3, LevelRunner.Stars(rules, 5, new LevelResult { heartsLeft = 4, restarts = 0 }));
            Assert.AreEqual(2, LevelRunner.Stars(rules, 5, new LevelResult { heartsLeft = 1, restarts = 0 }));
            Assert.AreEqual(1, LevelRunner.Stars(rules, 5, new LevelResult { heartsLeft = 1, restarts = 2 }));
        }

        #region Helpers

        static ModeData Proof() => AssetDatabase.LoadAssetAtPath<ModeDefinition>(ChapterModesBuilder.ProofModePath).ToModeData();

        BlockEntry Entry(BlockType type) => catalog.NewEntry(type, DifficultyPreset.Normal);

        object Ride(RunnerRide ride)
        {
            var entry = Entry(BlockType.Runner);
            entry.ride = ride;
            return entry;
        }

        object BossChase()
        {
            var entry = (BlockEntry)Ride(RunnerRide.CarThenSkycraft);
            entry.boss = true;
            return entry;
        }

        // Blocks as types or ready entries.
        LevelData Level(params object[] blocks)
        {
            var level = new LevelData();
            foreach (var block in blocks) level.blocks.Add(block as BlockEntry ?? Entry((BlockType)block));
            LinkResolver.Refresh(level, catalog);
            return level;
        }

        static ChapterData Chapter(params LevelData[] levels)
        {
            var chapter = new ChapterData();
            chapter.levels.AddRange(levels);
            return chapter;
        }

        void AssertRule(ForgeRule rule, ChapterData chapter)
        {
            var problems = ChapterValidator.Validate(chapter, catalog, linkSeconds);
            Assert.IsTrue(problems.Exists(p => p.rule == rule), $"expected {rule}: {Describe(problems)}");
        }

        static void AssertLinks(LevelData level, params LinkState[] states)
        {
            Assert.AreEqual(states.Length, level.links.Count);
            for (var i = 0; i < states.Length; i++) Assert.AreEqual(states[i], level.links[i].state, $"link {i + 1}");
        }

        static string Describe(List<ForgeProblem> problems) => string.Join("; ", problems.ConvertAll(p => $"{p.rule} L{p.level + 1} B{p.block + 1}"));

        #endregion
    }
}
