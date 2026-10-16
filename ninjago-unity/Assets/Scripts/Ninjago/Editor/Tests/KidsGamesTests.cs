#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nex.Ninjago.Editor.Tests
{
    // Poses are body inches against the chest (x = the player's right, y up), sampled at 15 Hz with +-0.4 in jitter
    // and read through the same 3-frame median the games use.
    public static class KidsPoses
    {
        public const float BodyHz = 15f;
        public static readonly Vector2 LeftShoulder = new(-7f, 2f);
        public static readonly Vector2 RightShoulder = new(7f, 2f);
        public static readonly Vector2 Head = new(0f, 11f);
        public static readonly Vector2 LeftHanging = new(-8f, -20f);
        public static readonly Vector2 RightHanging = new(8f, -20f);

        public static UpperBody Pose(Vector2 leftWrist, Vector2 rightWrist) => new(LeftShoulder, RightShoulder, leftWrist, rightWrist, Head);

        public static Func<float, Vector2> Move(Vector2 from, Vector2 to, float start, float duration) =>
            t => Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (t - start) / duration));

        /// <summary>Feeds the wrists' paths frame by frame through a 3-frame median; calls read with each filtered pose and its time.</summary>
        public static void Run(Func<float, Vector2> leftWrist, Func<float, Vector2> rightWrist, float seconds, int seed, Action<UpperBody, float> read,
            float jitter = 0.4f)
        {
            var random = new System.Random(seed);
            var recent = new UpperBody[3];
            var count = 0;
            for (var frame = 0; frame / BodyHz <= seconds; frame++)
            {
                var t = frame / BodyHz;
                recent[count % 3] = new UpperBody(LeftShoulder + Jitter(random, jitter), RightShoulder + Jitter(random, jitter),
                    leftWrist(t) + Jitter(random, jitter), rightWrist(t) + Jitter(random, jitter), Head + Jitter(random, jitter));
                count++;
                if (count >= 3) read(UpperBody.Median(recent[0], recent[1], recent[2]), t);
            }
        }

        static Vector2 Jitter(System.Random random, float amount) =>
            new((float)(random.NextDouble() * 2.0 - 1.0) * amount, (float)(random.NextDouble() * 2.0 - 1.0) * amount);
    }

    public class CallReaderTests
    {
        CallReader.Settings settings;

        [SetUp]
        public void SetUp()
        {
            var config = ScriptableObject.CreateInstance<CallConfig>();
            settings = config.ReaderSettings;
            Object.DestroyImmediate(config);
        }

        [Test]
        public void AHandRaisedHighIsOneCallWhicheverHand()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var right = Read(_ => KidsPoses.LeftHanging, KidsPoses.Move(KidsPoses.RightHanging, new Vector2(6f, 16f), 0.5f, 0.4f), 3f, seed);
                Assert.AreEqual(1, right.FindAll(e => e == CallReader.Event.Call).Count, $"right, seed {seed}");
                var left = Read(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-6f, 16f), 0.5f, 0.4f), _ => KidsPoses.RightHanging, 3f, seed);
                Assert.AreEqual(1, left.FindAll(e => e == CallReader.Event.Call).Count, $"left, seed {seed}");
                var both = Read(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-6f, 16f), 0.5f, 0.4f),
                    KidsPoses.Move(KidsPoses.RightHanging, new Vector2(6f, 16f), 0.55f, 0.4f), 3f, seed);
                Assert.AreEqual(1, both.FindAll(e => e == CallReader.Event.Call).Count, $"both hands, seed {seed}");
            }
        }

        [Test]
        public void HoldingTheHandUpIsStillOneCallAndLoweringItReArms()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var up = KidsPoses.Move(KidsPoses.RightHanging, new Vector2(6f, 16f), 0.5f, 0.4f);
                var down = KidsPoses.Move(new Vector2(6f, 16f), KidsPoses.RightHanging, 3f, 0.4f);
                var again = KidsPoses.Move(KidsPoses.RightHanging, new Vector2(6f, 16f), 4.5f, 0.4f);
                var events = Read(_ => KidsPoses.LeftHanging, t => t < 3f ? up(t) : t < 4.5f ? down(t) : again(t), 6f, seed, 1f);
                Assert.AreEqual(2, events.FindAll(e => e == CallReader.Event.Call).Count, $"seed {seed}");
            }
        }

        [Test]
        public void AHandThatStopsAtTheChinIsAMissedCall()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var up = KidsPoses.Move(KidsPoses.RightHanging, new Vector2(5f, 9f), 0.5f, 0.4f);
                var down = KidsPoses.Move(new Vector2(5f, 9f), KidsPoses.RightHanging, 1.5f, 0.4f);
                var events = Read(_ => KidsPoses.LeftHanging, t => t < 1.5f ? up(t) : down(t), 3f, seed);
                CollectionAssert.AreEqual(new[] { CallReader.Event.Missed }, events, $"seed {seed}");
            }
        }

        [Test]
        public void StandingStillOrWavingAtTheChestIsNoCall()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                CollectionAssert.IsEmpty(Read(_ => KidsPoses.LeftHanging, _ => KidsPoses.RightHanging, 6f, seed, 1f), $"still, seed {seed}");
                var wave = Read(_ => KidsPoses.LeftHanging, t => new Vector2(6f + 5f * Mathf.Sin(t * 6f), 0f), 6f, seed);
                CollectionAssert.IsEmpty(wave, $"chest wave, seed {seed}");
            }
        }

        List<CallReader.Event> Read(Func<float, Vector2> left, Func<float, Vector2> right, float seconds, int seed, float jitter = 0.4f)
        {
            var reader = new CallReader();
            var events = new List<CallReader.Event>();
            KidsPoses.Run(left, right, seconds, seed, (pose, _) =>
            {
                var e = reader.Update(pose, settings);
                if (e != CallReader.Event.None) events.Add(e);
            }, jitter);
            return events;
        }
    }

    public class GuardReaderTests
    {
        GuardReader.Settings settings;

        [SetUp]
        public void SetUp()
        {
            var config = ScriptableObject.CreateInstance<GuardConfig>();
            settings = config.ReaderSettings;
            Object.DestroyImmediate(config);
        }

        [Test]
        public void HandsUpInFrontOfTheFaceIsAGuard()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var events = Read(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-3f, 9f), 0.5f, 0.4f),
                    KidsPoses.Move(KidsPoses.RightHanging, new Vector2(3f, 9f), 0.5f, 0.4f), 2f, seed);
                Assert.AreEqual(1, events.FindAll(e => e.e == GuardReader.Event.Raised).Count, $"seed {seed}");
            }
        }

        [Test]
        public void AShortGuardThatIsOnlyHalfwayUpStillCounts()
        {
            // Forearms up to just over the shoulders, the least a tired child might do.
            for (var seed = 0; seed < 20; seed++)
            {
                var events = Read(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-4f, 4f), 0.5f, 0.3f),
                    KidsPoses.Move(KidsPoses.RightHanging, new Vector2(4f, 4f), 0.5f, 0.3f), 2f, seed);
                Assert.IsTrue(events.Exists(e => e.e == GuardReader.Event.Raised), $"seed {seed}");
            }
        }

        [Test]
        public void OneHandUpOrAWideCheerIsAHalfGuard()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var oneHand = Read(_ => KidsPoses.LeftHanging, KidsPoses.Move(KidsPoses.RightHanging, new Vector2(3f, 9f), 0.5f, 0.3f), 2f, seed);
                Assert.IsFalse(oneHand.Exists(e => e.e == GuardReader.Event.Raised), $"one hand, seed {seed}");
                Assert.IsTrue(oneHand.Exists(e => e.e == GuardReader.Event.HalfRaised), $"one hand, seed {seed}");
                var cheer = Read(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-18f, 14f), 0.5f, 0.3f),
                    KidsPoses.Move(KidsPoses.RightHanging, new Vector2(18f, 14f), 0.5f, 0.3f), 2f, seed);
                Assert.IsFalse(cheer.Exists(e => e.e == GuardReader.Event.Raised), $"cheer, seed {seed}");
            }
        }

        [Test]
        public void StandingStillOrHandsOnTheBellyIsNoGuard()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                CollectionAssert.IsEmpty(Read(_ => KidsPoses.LeftHanging, _ => KidsPoses.RightHanging, 6f, seed, 1f), $"seed {seed}");
                var belly = Read(_ => new Vector2(-3f, -6f), _ => new Vector2(3f, -6f), 6f, seed);
                CollectionAssert.IsEmpty(belly, $"belly, seed {seed}");
            }
        }

        [Test]
        public void AHeldGuardStopsBeingFreshAndJitterNearTheLineDoesNotFlicker()
        {
            var reader = new GuardReader();
            var raisedAt = -1f;
            var lowered = 0;
            KidsPoses.Run(KidsPoses.Move(KidsPoses.LeftHanging, new Vector2(-3f, 3f), 0.3f, 0.3f), KidsPoses.Move(KidsPoses.RightHanging, new Vector2(3f, 3f), 0.3f, 0.3f),
                6f, 3, (pose, t) =>
                {
                    var e = reader.Update(pose, t, settings);
                    if (e == GuardReader.Event.Raised) raisedAt = t;
                    if (e == GuardReader.Event.Lowered) lowered++;
                }, 1f);
            Assert.Greater(raisedAt, 0f);
            Assert.AreEqual(0, lowered, "wrists held 1 in above the shoulders with 1 in jitter");
            Assert.IsTrue(reader.IsFresh(raisedAt + settings.freshSeconds * 0.5f, settings));
            Assert.IsFalse(reader.IsFresh(raisedAt + settings.freshSeconds + 0.1f, settings));
        }

        [Test]
        public void AMissIsSortedByWhatTheReadingSaw()
        {
            const float late = 0.6f;
            const float half = 1.2f;
            Assert.AreEqual(GuardOutcome.Late, GuardPlayer.ClassifyMiss(10f, false, 10.3f, float.NegativeInfinity, late, half));
            Assert.AreEqual(GuardOutcome.Held, GuardPlayer.ClassifyMiss(10f, true, 7f, float.NegativeInfinity, late, half));
            Assert.AreEqual(GuardOutcome.Half, GuardPlayer.ClassifyMiss(10f, false, 5f, 9.5f, late, half));
            Assert.AreEqual(GuardOutcome.Missed, GuardPlayer.ClassifyMiss(10f, false, 5f, 6f, late, half));
            Assert.AreEqual(GuardOutcome.Missed, GuardPlayer.ClassifyMiss(10f, false, 11f, float.NegativeInfinity, late, half), "raised long after");
        }

        [Test]
        public void TheScheduleSharesThrowsAndSpreadsTheGiantBricks()
        {
            var duo = GuardScene.Schedule(2, 10, 3);
            Assert.AreEqual(23, duo.Count);
            Assert.AreEqual(10, duo.FindAll(l => l == 0).Count);
            Assert.AreEqual(10, duo.FindAll(l => l == 1).Count);
            Assert.AreEqual(3, duo.FindAll(l => l == -1).Count);
            Assert.AreNotEqual(-1, duo[0], "the first throw is a plain one");
            for (var i = 1; i < duo.Count; i++) Assert.IsFalse(duo[i] == -1 && duo[i - 1] == -1, "no two giants in a row");
            var solo = GuardScene.Schedule(1, 10, 3);
            Assert.AreEqual(13, solo.Count);
            Assert.AreEqual(10, solo.FindAll(l => l == 0).Count);
        }

        List<(GuardReader.Event e, float t)> Read(Func<float, Vector2> left, Func<float, Vector2> right, float seconds, int seed, float jitter = 0.4f)
        {
            var reader = new GuardReader();
            var events = new List<(GuardReader.Event, float)>();
            KidsPoses.Run(left, right, seconds, seed, (pose, t) =>
            {
                var e = reader.Update(pose, t, settings);
                if (e != GuardReader.Event.None) events.Add((e, t));
            }, jitter);
            return events;
        }
    }
}
