#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nex.Ninjago.Editor.Tests
{
    // Motions run at camera rates with tracking jitter: body frames at 15 Hz (joints +-0.3 in), hand detector frames at
    // 20 Hz (hand scale +-3%). Points are body inches against the chest, x = the player's right, y up.
    public class PunchDetectorTests
    {
        const float bodyHz = 15f;
        const float handHz = 20f;
        const float jointJitter = 0.3f;
        const float scaleJitter = 0.03f;
        static readonly Vector2 rightShoulder = new(7f, 2f);
        static readonly Vector2 leftShoulder = new(-7f, 2f);
        static readonly Vector2 guard = new(5f, 5f);

        PunchDetector.Settings settings;

        [SetUp]
        public void SetUp()
        {
            var config = ScriptableObject.CreateInstance<OnePunchConfig>();
            settings = config.PunchSettings;
            Object.DestroyImmediate(config);
        }

        [Test]
        public void AShortCrossFromTheGuardIsAnExtension()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var tries = RunRightHand(Move(guard, new Vector2(-1f, 4f), 0.5f, 0.15f), 2f, seed);
                AssertSingle(tries, PunchSignal.Extension, seed);
                // Read within two body frames of the fist arriving: the ninja strikes on that frame.
                Assert.LessOrEqual(tries[0].time, 0.65f + 2f / bodyHz, $"seed {seed}");
            }
        }

        [Test]
        public void ACrossAimedDownAtAShortMonsterStillCounts()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                AssertSingle(RunRightHand(Move(guard, new Vector2(0f, 0f), 0.5f, 0.15f), 2f, seed), PunchSignal.Extension, seed);
            }
        }

        [Test]
        public void DroppingTheGuardIsNotAPunch()
        {
            var drops = new[] { (guard, 0.2f), (guard, 0.4f), (new Vector2(2f, 3f), 0.25f) };
            foreach (var (from, seconds) in drops)
            {
                for (var seed = 0; seed < 25; seed++)
                {
                    var tries = RunRightHand(Move(from, new Vector2(8f, -20f), 0.5f, seconds), 2f, seed);
                    AssertSingle(tries, PunchSignal.None, seed, $"{from} in {seconds} s");
                }
            }
        }

        [Test]
        public void AJabStraightAtTheLensIsReadByTheHandScale()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var tries = RunRightHand(Move(guard, guard + new Vector2(-1f, -0.5f), 0.5f, 0.15f), 2f, seed, Grow(1f, 1.3f, 0.5f, 0.15f));
                AssertSingle(tries, PunchSignal.TowardCamera, seed);
            }
        }

        [Test]
        public void AOneFrameHandScaleSpikeIsNotAPunch()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                CollectionAssert.IsEmpty(RunRightHand(_ => guard, 2f, seed, t => t > 0.95f && t < 1.0f ? 1.5f : 1f), $"seed {seed}");
            }
        }

        [Test]
        public void AHandFoundAgainAfterAGapIsNotAPunch()
        {
            var detector = new PunchDetector();
            for (var t = 0f; t < 0.3f; t += 1f / bodyHz) detector.AddArm(t, rightShoulder, leftShoulder, guard, settings);
            var time = 0f;
            for (; time < 1f; time += 1f / handHz) Assert.IsNull(detector.AddHandScale(time, 1f, guard, settings));
            // Missed for half a second, then seen again much bigger.
            for (time += 0.5f; time < 2f; time += 1f / handHz) Assert.IsNull(detector.AddHandScale(time, 1.5f, guard, settings), $"t {time:0.00}");
        }

        // A still player, arms hanging: the hand detector's scale for a hand by the hip swings wildly (seen up to 4x in a real run).
        [Test]
        public void HandScaleNoiseFromHangingHandsIsNotAPunch()
        {
            var hanging = new Vector2(8f, -20f);
            for (var seed = 0; seed < 25; seed++)
            {
                var noise = new System.Random(seed + 100);
                var tries = RunRightHand(_ => hanging, 8f, seed, _ => 1f + (float)noise.NextDouble() * 3f);
                CollectionAssert.IsEmpty(tries, $"seed {seed}: {Describe(tries)}");
            }
        }

        [Test]
        public void HandScaleNoiseOnAStillGuardIsNotAPunch()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var noise = new System.Random(seed + 200);
                var tries = RunRightHand(_ => guard, 8f, seed, _ => 1f + ((float)noise.NextDouble() * 2f - 1f) * 0.08f);
                CollectionAssert.IsEmpty(tries, $"seed {seed}: {Describe(tries)}");
            }
        }

        // A real run logged a 2.8 in arm length jump between two body frames 8 ms apart, read as a 324 in/s punch.
        [Test]
        public void OneJitteryFrameIsNotAPunch()
        {
            var detector = new PunchDetector();
            var tries = new List<PunchDetector.Try>();
            var random = new System.Random(5);
            for (var frame = 0; frame < 90; frame++)
            {
                var t = frame / bodyHz;
                Add(t, guard + Jitter(random, jointJitter));
                if (frame % 10 != 5) continue;
                // A second frame right behind it, with the wrist thrown 3 in further from the shoulder.
                Add(t + 0.008f, guard + new Vector2(-2.2f, 2.2f));
            }

            CollectionAssert.IsEmpty(tries, Describe(tries));

            void Add(float t, Vector2 wrist)
            {
                var result = detector.AddArm(t, rightShoulder, leftShoulder, wrist, settings);
                if (result.HasValue) tries.Add(result.Value);
            }
        }

        // Standing still, a wrist can swap sides or be guessed off the bottom of the frame for a few frames.
        [Test]
        public void AWristGlitchIsNotATry()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var swap = RunRightHand(t => t > 1f && t < 1.25f ? new Vector2(-8f, 4f) : guard, 3f, seed);
                CollectionAssert.IsEmpty(swap, $"swap, seed {seed}: {Describe(swap)}");
                var offFrame = RunRightHand(t => t > 1f && t < 1.2f ? new Vector2(20f, -15f) : guard, 3f, seed);
                CollectionAssert.IsEmpty(offFrame, $"off frame, seed {seed}: {Describe(offFrame)}");
                var hangingJitter = new System.Random(seed + 300);
                var hanging = RunRightHand(_ => new Vector2(8f, -20f) + Jitter(hangingJitter, 4f), 6f, seed);
                CollectionAssert.IsEmpty(hanging, $"hanging, seed {seed}: {Describe(hanging)}");
            }
        }

        [Test]
        public void ASlashAcrossTheShoulderIsRejected()
        {
            var sweeps = new[]
            {
                (new Vector2(17f, 2f), new Vector2(-3f, 2f)),
                (new Vector2(-3f, 2f), new Vector2(17f, 2f)),
                (new Vector2(17f, 8f), new Vector2(-3f, 8f)),
                (new Vector2(-3f, 8f), new Vector2(17f, 8f)),
                (new Vector2(16f, -2f), new Vector2(-2f, -2f)),
            };
            foreach (var (from, to) in sweeps)
            {
                for (var seed = 0; seed < 25; seed++)
                {
                    var tries = RunRightHand(Move(from, to, 0.5f, 0.25f), 2f, seed);
                    AssertSingle(tries, PunchSignal.SideSlash, seed, $"{from} -> {to}");
                }
            }
        }

        [Test]
        public void AFastSlashStaysRejectedEvenWithHandScaleNoise()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var tries = RunRightHand(Move(new Vector2(17f, 2f), new Vector2(-3f, 2f), 0.5f, 0.2f), 2f, seed, _ => 1f);
                AssertSingle(tries, PunchSignal.SideSlash, seed);
            }
        }

        [Test]
        public void TheLeftHandMirrorsTheRightHand()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var cross = Run(Mirror(Move(guard, new Vector2(-1f, 4f), 0.5f, 0.15f)), 2f, seed, null, leftShoulder, rightShoulder);
                AssertSingle(cross, PunchSignal.Extension, seed);
                var slash = Run(Mirror(Move(new Vector2(17f, 2f), new Vector2(-3f, 2f), 0.5f, 0.25f)), 2f, seed, null, leftShoulder, rightShoulder);
                AssertSingle(slash, PunchSignal.SideSlash, seed);
            }
        }

        [Test]
        public void AStillHandNeverOpensATry()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                CollectionAssert.IsEmpty(RunRightHand(_ => guard, 6f, seed, _ => 1f), $"seed {seed}");
                CollectionAssert.IsEmpty(RunRightHand(_ => new Vector2(8f, -20f), 6f, seed, _ => 1f), $"hanging, seed {seed}");
            }
        }

        [Test]
        public void ASlowReachIsNotATry()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                CollectionAssert.IsEmpty(RunRightHand(Move(guard, new Vector2(-1f, 4f), 0.5f, 1.2f), 3f, seed), $"seed {seed}");
            }
        }

        [Test]
        public void AFastMoveWithNeitherSignalIsLoggedAsNothing()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                // A forward punch from a hanging arm: in 2D the arm only gets shorter, and there is no hand scale here.
                var hanging = RunRightHand(Move(new Vector2(8f, -20f), new Vector2(1f, 3f), 0.5f, 0.2f), 2f, seed);
                AssertSingle(hanging, PunchSignal.None, seed);
                // Scale growth while the hand is still rising from the hip (not yet in front) does not count either.
                var risingScale = RunRightHand(Move(new Vector2(8f, -20f), new Vector2(1f, 3f), 0.5f, 0.2f), 2f, seed, Grow(1f, 1.3f, 0.5f, 0.1f));
                AssertSingle(risingScale, PunchSignal.None, seed);
            }
        }

        [Test]
        public void TwoPunchesAFullSecondApartBothCountAndThePullBackIsNotATry()
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var first = Move(guard, new Vector2(-1f, 4f), 0.5f, 0.15f);
                var pullBack = Move(new Vector2(-1f, 4f), guard, 0.75f, 0.2f);
                var second = Move(guard, new Vector2(-1f, 4f), 1.5f, 0.15f);
                var tries = RunRightHand(t => t < 0.75f ? first(t) : t < 1.5f ? pullBack(t) : second(t), 3f, seed);
                Assert.AreEqual(2, tries.Count, $"seed {seed}: {Describe(tries)}");
                Assert.AreEqual(PunchSignal.Extension, tries[0].signal);
                Assert.AreEqual(PunchSignal.Extension, tries[1].signal);
            }
        }

        [Test]
        public void TheCooldownSwallowsAQuickSecondPunch()
        {
            var detector = new PunchDetector();
            var firstTries = Feed(detector, Move(guard, new Vector2(-1f, 4f), 0.5f, 0.15f), 0f, 0.75f, 3, null, rightShoulder, leftShoulder);
            AssertSingle(firstTries, PunchSignal.Extension, 3);
            var restEnds = firstTries[0].time + settings.cooldownSeconds;
            // A snap back to the guard and a second cross, all inside the rest.
            var snap = Move(new Vector2(-1f, 4f), guard, 0.75f, 0.05f);
            var again = Move(guard, new Vector2(-1f, 4f), 0.8f, 0.05f);
            var tries = Feed(detector, t => t < 0.8f ? snap(t) : again(t), 0.75f, restEnds - 0.01f, 3, null, rightShoulder, leftShoulder);
            CollectionAssert.IsEmpty(tries);
        }

        #region Harness

        List<PunchDetector.Try> RunRightHand(Func<float, Vector2> wrist, float seconds, int seed, Func<float, float>? scale = null) =>
            Run(wrist, seconds, seed, scale, rightShoulder, leftShoulder);

        List<PunchDetector.Try> Run(Func<float, Vector2> wrist, float seconds, int seed, Func<float, float>? scale, Vector2 shoulder, Vector2 otherShoulder)
        {
            var detector = new PunchDetector();
            var tries = Feed(detector, wrist, 0f, seconds, seed, scale, shoulder, otherShoulder);
            var last = detector.Tick(seconds + 1f, settings);
            if (last.HasValue) tries.Add(last.Value);
            return tries;
        }

        // Body and hand frames in time order between from and to.
        List<PunchDetector.Try> Feed(PunchDetector detector, Func<float, Vector2> wrist, float from, float to, int seed, Func<float, float>? scale,
            Vector2 shoulder, Vector2 otherShoulder)
        {
            var random = new System.Random(seed);
            var tries = new List<PunchDetector.Try>();
            var nextBody = Mathf.Ceil(from * bodyHz) / bodyHz;
            var nextHand = Mathf.Ceil(from * handHz) / handHz + 0.013f;
            while (Mathf.Min(nextBody, nextHand) <= to)
            {
                PunchDetector.Try? result = null;
                if (nextBody <= nextHand)
                {
                    var t = nextBody;
                    result = detector.AddArm(t, shoulder + Jitter(random, jointJitter), otherShoulder + Jitter(random, jointJitter),
                        wrist(t) + Jitter(random, jointJitter), settings);
                    nextBody += 1f / bodyHz;
                }
                else
                {
                    var t = nextHand;
                    if (scale != null)
                    {
                        result = detector.AddHandScale(t, scale(t) * (1f + (float)(random.NextDouble() * 2.0 - 1.0) * scaleJitter),
                            wrist(t) + Jitter(random, jointJitter), settings);
                    }
                    nextHand += 1f / handHz;
                }

                if (result.HasValue) tries.Add(result.Value);
            }

            return tries;
        }

        static Func<float, Vector2> Move(Vector2 from, Vector2 to, float start, float duration) =>
            t => Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (t - start) / duration));

        static Func<float, float> Grow(float from, float to, float start, float duration) =>
            t => Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (t - start) / duration));

        static Func<float, Vector2> Mirror(Func<float, Vector2> path) => t =>
        {
            var p = path(t);
            return new Vector2(-p.x, p.y);
        };

        static Vector2 Jitter(System.Random random, float amount) =>
            new((float)(random.NextDouble() * 2.0 - 1.0) * amount, (float)(random.NextDouble() * 2.0 - 1.0) * amount);

        static void AssertSingle(List<PunchDetector.Try> tries, PunchSignal expected, int seed, string label = "")
        {
            Assert.AreEqual(1, tries.Count, $"{label} seed {seed}: {Describe(tries)}");
            Assert.AreEqual(expected, tries[0].signal, $"{label} seed {seed}: {Describe(tries)}");
        }

        static string Describe(List<PunchDetector.Try> tries)
        {
            var parts = new List<string>();
            foreach (var t in tries) parts.Add($"{t.signal}@{t.time:0.000} (grow {t.growth:0.0} at {t.growthSpeed:0}, scale {t.scaleGrowth:0.00}, side {t.sideTravel:0.0})");
            return parts.Count == 0 ? "no tries" : string.Join(", ", parts);
        }

        #endregion
    }

    // The tuned asset (rebuilt by Nex > Ninjago > Build One Punch) against the brief's numbers.
    public class OnePunchContentTests
    {
        static OnePunchConfig Config => UnityEditor.AssetDatabase.LoadAssetAtPath<OnePunchConfig>("Assets/Configs/Ninjago/OnePunchConfig.asset");

        [Test]
        public void TheRunMatchesTheBrief()
        {
            var config = Config;
            Assert.AreEqual(5, config.WaveCount);
            Assert.AreEqual(5, config.MonstersPerWave);
            Assert.AreEqual(2f, config.SecondsBetweenWaves, 0.5f);
            Assert.AreEqual(8f, config.StartingHealth);
            Assert.AreEqual(1f, config.DrainPerSecond);
            Assert.AreEqual(0.4f, config.WiggleWindowSeconds, 0.05f);
        }

        [Test]
        public void MonstersNeverReachTheNinjasOnTheSameFrame()
        {
            var config = Config;
            Assert.Greater(config.MonsterSpacing / config.MonsterSpeed, 0.5f, "seconds between arrivals");
            Assert.Greater(config.FirstMonsterDistance, config.GrabRange + 3f);
        }

        [Test]
        public void EverySignalHasAWord()
        {
            foreach (PunchSignal signal in Enum.GetValues(typeof(PunchSignal))) Assert.IsFalse(Config.GetLook(signal).label.IsEmpty, signal.ToString());
        }
    }

    // Hip center at 15 Hz with +-0.3 in jitter; the meter ticks at 60 Hz.
    public class WiggleMeterTests
    {
        const float bodyHz = 15f;
        const float tickHz = 60f;
        const float jitter = 0.3f;

        OnePunchConfig config = null!;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<OnePunchConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void AFastWiggleBreaksFreeForAboutOneHealth()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var seconds = SecondsToFill(t => new Vector2(3f * Mathf.Sin(2f * Mathf.PI * 2.5f * t), 0f), 6f, seed);
                var cost = seconds * config.DrainPerSecond;
                Assert.That(cost, Is.InRange(0.6f, 1.4f), $"seed {seed}: {seconds:0.00} s");
            }
        }

        [Test]
        public void AHipShakeOfOtherShapesAlsoCounts()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                // Smaller and quicker, and a figure-of-eight swing.
                Assert.Less(SecondsToFill(t => new Vector2(2.5f * Mathf.Sin(2f * Mathf.PI * 3f * t), 0f), 6f, seed), 1.6f, $"seed {seed}");
                Assert.Less(SecondsToFill(t => new Vector2(3f * Mathf.Sin(2f * Mathf.PI * 2f * t), 1.5f * Mathf.Sin(4f * Mathf.PI * 2f * t)), 6f, seed), 1.6f,
                    $"seed {seed}");
            }
        }

        [Test]
        public void StandingStillNeverBreaksFree()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                Assert.IsTrue(float.IsPositiveInfinity(SecondsToFill(_ => Vector2.zero, 12f, seed, 0.45f)), $"seed {seed}");
            }

            // Standing still the whole pin drains every point of health.
            Assert.Less(config.StartingHealth / config.DrainPerSecond, 12f);
        }

        [Test]
        public void ASlowSwayOrOneSidestepIsNotAWiggle()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                Assert.IsTrue(float.IsPositiveInfinity(SecondsToFill(t => new Vector2(3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t), 0f), 10f, seed)), $"sway {seed}");
                Assert.IsTrue(float.IsPositiveInfinity(SecondsToFill(t => new Vector2(Mathf.Lerp(0f, 14f, Mathf.SmoothStep(0f, 1f, (t - 1f) / 0.4f)), 0f), 6f, seed)),
                    $"step {seed}");
            }
        }

        [Test]
        public void LosingTheBodyStopsTheFill()
        {
            var meter = new WiggleMeter();
            var random = new System.Random(1);
            var t = 0f;
            var nextBody = 0f;
            while (t < 0.7f)
            {
                if (t >= nextBody)
                {
                    meter.Add(new Vector2(3f * Mathf.Sin(2f * Mathf.PI * 2.5f * t), 0f) + Jitter(random), t, config.WiggleWindowSeconds);
                    nextBody += 1f / bodyHz;
                }

                Tick(meter, t);
                t += 1f / tickHz;
            }

            var fillAtLoss = meter.Fill;
            Assert.Greater(fillAtLoss, 0f);
            for (; t < 3f; t += 1f / tickHz) Tick(meter, t);
            Assert.Less(meter.Fill, fillAtLoss + 0.35f);
            Assert.IsFalse(meter.IsWiggling);
        }

        float SecondsToFill(Func<float, Vector2> hip, float seconds, int seed, float noise = jitter)
        {
            var meter = new WiggleMeter();
            var random = new System.Random(seed);
            var nextBody = 0f;
            for (var t = 0f; t <= seconds; t += 1f / tickHz)
            {
                if (t >= nextBody)
                {
                    meter.Add(hip(t) + Jitter(random, noise), t, config.WiggleWindowSeconds);
                    nextBody += 1f / bodyHz;
                }

                if (Tick(meter, t)) return t;
            }

            return float.PositiveInfinity;
        }

        bool Tick(WiggleMeter meter, float t) => meter.Tick(t, 1f / tickHz, config.WiggleWindowSeconds, config.WigglePathInches, config.WiggleRadiusInches,
            config.WiggleFillSeconds);

        static Vector2 Jitter(System.Random random, float amount = jitter) =>
            new((float)(random.NextDouble() * 2.0 - 1.0) * amount, (float)(random.NextDouble() * 2.0 - 1.0) * amount);
    }
}
