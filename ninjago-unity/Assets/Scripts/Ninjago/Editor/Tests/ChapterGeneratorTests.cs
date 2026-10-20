#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    /// <summary>Surprise me: every seed makes a valid chapter, and the same seed always makes the same chapter.</summary>
    public class ChapterGeneratorTests
    {
        ForgeCatalog catalog = null!;
        ForgeConfig config = null!;
        float linkSeconds;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ForgeCatalog>(ChapterAssetsBuilder.CatalogPath);
            config = AssetDatabase.LoadAssetAtPath<ForgeConfig>(ChapterAssetsBuilder.ForgeConfigPath);
            linkSeconds = AssetDatabase.LoadAssetAtPath<ChapterTuning>(ChapterAssetsBuilder.TuningPath).LinkSeconds;
        }

        [Test]
        public void FiftySeedsAllPassTheValidator()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var mode = ChapterGenerator.Generate(seed * 7919, catalog, config, $"seed_{seed}");
                var problems = ChapterValidator.Validate(mode.chapter, catalog, linkSeconds);
                Assert.IsFalse(ChapterValidator.HasErrors(problems), $"seed {seed * 7919}: {string.Join("; ", problems.ConvertAll(p => $"{p.rule} L{p.level + 1} B{p.block + 1}"))}");
            }
        }

        [Test]
        public void TheSameSeedMakesTheSameChapter()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var first = ChapterGenerator.Generate(seed * 31, catalog, config, "same");
                var second = ChapterGenerator.Generate(seed * 31, catalog, config, "same");
                Assert.AreEqual(JsonUtility.ToJson(first.chapter), JsonUtility.ToJson(second.chapter), $"seed {seed * 31}");
                CollectionAssert.AreEqual(first.name.words, second.name.words);
                Assert.AreEqual(seed * 31, first.name.seed, "the seed shows in the name");
            }
        }

        [Test]
        public void GeneratedChaptersKeepTheirPromises()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var chapter = ChapterGenerator.Generate(seed, catalog, config, "promise").chapter;
                var first = chapter.levels[0];
                var types = new HashSet<BlockType>();
                foreach (var block in first.blocks)
                {
                    types.Add(block.type);
                    Assert.AreEqual(HintPolicy.FirstTime, block.hints, $"seed {seed}: Level 1 hints");
                }

                Assert.AreEqual(4, types.Count, $"seed {seed}: every type in Level 1");
                Assert.IsTrue(chapter.levels[^1].IsBoss, $"seed {seed}: the Boss comes last");
                for (var i = 0; i < chapter.levels.Count - 1; i++) Assert.IsFalse(chapter.levels[i].IsBoss, $"seed {seed}: one Boss level");
                for (var i = 1; i < chapter.levels.Count - 1; i++)
                {
                    Assert.GreaterOrEqual(ChapterValidator.Intensity(chapter.levels[i]), ChapterValidator.Intensity(chapter.levels[i - 1]), $"seed {seed}: ramp");
                }
            }
        }
    }
}
