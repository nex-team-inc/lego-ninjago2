#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    public class SawDetectorTests
    {
        const float speed = 30f;
        const float cap = 0.08f;
        const float dt = 1f / 60f;

        [Test]
        public void AFastCrossingSlashesOnTheCrossingFrame()
        {
            var saw = new SawDetector();
            Assert.IsFalse(saw.Update(-2f, 0f, 0f, speed, cap));
            Assert.IsFalse(saw.Update(-0.5f, 0f, dt, speed, cap));
            Assert.IsTrue(saw.Update(1f, 0f, 2 * dt, speed, cap));
        }

        [Test]
        public void ASlowCrossingDoesNot()
        {
            var saw = new SawDetector();
            saw.Update(-0.2f, 0f, 0f, speed, cap);
            Assert.IsFalse(saw.Update(0.2f, 0f, dt, speed, cap));
        }

        [Test]
        public void SawingBothWaysCountsEveryCrossing()
        {
            var saw = new SawDetector();
            var slashes = 0;
            var time = 0f;
            // 6 inches either side of the shoulder at 3 Hz for one second.
            for (var i = 0; i <= 60; i++)
            {
                if (saw.Update(Mathf.Sin(time * 3f * Mathf.PI * 2f) * 6f, 0f, time, speed, cap)) slashes++;
                time += dt;
            }

            Assert.That(slashes, Is.InRange(5, 7));
        }

        [Test]
        public void TheCapOnlyStopsADoubleCount()
        {
            var saw = new SawDetector();
            saw.Update(-1f, 0f, 0f, speed, cap);
            Assert.IsTrue(saw.Update(1f, 0f, dt, speed, cap));
            Assert.IsFalse(saw.Update(-1f, 0f, 2 * dt, speed, cap));
            Assert.IsTrue(saw.Update(4f, 0f, 2 * dt + 0.09f, speed, cap));
        }
    }

    // Motions go through the real cursor path: hand detector samples at 15 Hz with jitter, the cursor's One Euro
    // smoothing, read every 60 Hz frame. Raw perfect circles hide how much the smoothing shrinks a fast twirl.
    public class SpinDetectorTests
    {
        static readonly Vector2 spot = new(-14f, 16f);

        static int Bursts(System.Func<float, Vector2> hand, float jitter = 0.8f)
        {
            var config = ScriptableObject.CreateInstance<ThreeWeaponsConfig>();
            var cursorConfig = ScriptableObject.CreateInstance<HandCursorConfig>();
            var box = ReachBox.For(new Rect(0f, 0f, 1f, 1f), new Vector2(1920f, 1080f), cursorConfig.ReachHeightInches, cursorConfig.ReachCenterAboveChestInches);
            var cursor = new HandCursor(0, 1);
            var spin = new SpinDetector();
            var random = new System.Random(3);
            var raw = hand(0f);
            var nextSample = 0f;
            var readyAt = 0f;
            var bursts = 0;
            for (var frame = 0; frame < 240; frame++)
            {
                var t = frame / 60f;
                if (t >= nextSample)
                {
                    nextSample += 1f / 15f;
                    raw = hand(t) + new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * jitter;
                }

                cursor.Track(raw, HandCursor.Source.HandDetector, t, box, cursorConfig.SmoothingMinCutoffHz, cursorConfig.SmoothingSpeedResponse);
                spin.Add(cursor.Inches, t, config.SpinWindowSeconds);
                if (t < readyAt || !spin.IsBurst(config.SpinPathInches, config.SpinRadiusInches, out _)) continue;
                bursts++;
                spin.Clear();
                readyAt = t + config.SpinCooldownSeconds;
            }

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(cursorConfig);
            return bursts;
        }

        static Vector2 Circle(float t, float radius, float hz) => spot + radius * new Vector2(Mathf.Cos(t * hz * Mathf.PI * 2f), Mathf.Sin(t * hz * Mathf.PI * 2f));

        [TestCase(3f, 1.5f, TestName = "SmallWristTwirlBursts")]
        [TestCase(4f, 2f, TestName = "QuickTwirlBursts")]
        [TestCase(6f, 1f, TestName = "MediumCircleBursts")]
        [TestCase(10f, 0.8f, TestName = "BigSlowArmCircleBursts")]
        public void SpinningBurstsAgainAndAgain(float radius, float hz)
        {
            Assert.GreaterOrEqual(Bursts(t => Circle(t, radius, hz)), 5);
        }

        [Test]
        public void AFastWaveBursts()
        {
            Assert.GreaterOrEqual(Bursts(t => spot + new Vector2(6f * Mathf.Sin(t * 2f * Mathf.PI * 2f), 0f)), 5);
        }

        [Test]
        public void AStillHandDoesNotBurstEvenWithHeavyJitter()
        {
            Assert.AreEqual(0, Bursts(_ => spot, 2f));
        }

        [Test]
        public void SlowMotionDoesNotBurst()
        {
            Assert.AreEqual(0, Bursts(t => spot + new Vector2(8f * t - 16f, 0f)));
            Assert.AreEqual(0, Bursts(t => Circle(t, 4f, 0.5f)));
        }

        [Test]
        public void MovingTheHandToTheNextSpotDoesNotBurst()
        {
            var next = new Vector2(29f, 3f);
            Assert.AreEqual(0, Bursts(t => Vector2.Lerp(spot, next, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1f) / 0.6f)))));
        }
    }

    public class SpinAreaTests
    {
        static readonly Vector2 leftShoulder = new(-7f, 2f);
        static readonly Vector2 rightShoulder = new(7f, 2f);
        static readonly Vector2 head = new(0f, 11f);

        static bool In(Vector2 point, SpinArea area)
        {
            var config = ScriptableObject.CreateInstance<ThreeWeaponsConfig>();
            var left = area is SpinArea.LeftHead or SpinArea.LeftBody;
            var inArea = GauntletGestures.IsInArea(point, area, left ? leftShoulder : rightShoulder, head, config.SpinSideInches, config.SpinHeadLineDropInches);
            Object.DestroyImmediate(config);
            return inArea;
        }

        [Test]
        public void TheNinjasHintPosesLandInTheirAreas()
        {
            // Arm raised up and out (head), arm held straight out to the side (body), as the ninja shows them.
            Assert.IsTrue(In(new Vector2(-19f, 20f), SpinArea.LeftHead));
            Assert.IsTrue(In(new Vector2(19f, 20f), SpinArea.RightHead));
            Assert.IsTrue(In(new Vector2(-29f, 3f), SpinArea.LeftBody));
            Assert.IsTrue(In(new Vector2(29f, 3f), SpinArea.RightBody));
        }

        [Test]
        public void SpinningCloseInAlsoCounts()
        {
            Assert.IsTrue(In(new Vector2(-8f, 13f), SpinArea.LeftHead));
            Assert.IsTrue(In(new Vector2(10f, -2f), SpinArea.RightBody));
        }

        [Test]
        public void TheWrongSideOrHeightOrTheMiddleDoesNotCount()
        {
            Assert.IsFalse(In(new Vector2(19f, 20f), SpinArea.LeftHead));
            Assert.IsFalse(In(new Vector2(-29f, 3f), SpinArea.LeftHead));
            Assert.IsFalse(In(new Vector2(-19f, 20f), SpinArea.LeftBody));
            Assert.IsFalse(In(new Vector2(0f, 15f), SpinArea.RightHead));
        }

        [Test]
        public void AMirroredFrameKeepsEachSideWithItsShoulder()
        {
            var config = ScriptableObject.CreateInstance<ThreeWeaponsConfig>();
            Assert.IsTrue(GauntletGestures.IsInArea(new Vector2(19f, 20f), SpinArea.LeftHead, new Vector2(7f, 2f), head, config.SpinSideInches,
                config.SpinHeadLineDropInches));
            Object.DestroyImmediate(config);
        }
    }

    public class FlickDetectorTests
    {
        static readonly FlickDetector.Settings settings = new(5f, 6f, 40f, 0.6f);
        static readonly Vector2 rightShoulder = new(7f, 2f);
        const float dt = 1f / 60f;

        [Test]
        public void CockedBackThenAcrossFastThrows()
        {
            var flick = new FlickDetector();
            Assert.IsFalse(flick.Update(new Vector2(14f, 6f), rightShoulder, 1f, 0f, settings));
            Assert.IsTrue(flick.IsCocked);
            Assert.IsFalse(flick.Update(new Vector2(9f, 4f), rightShoulder, 1f, dt, settings));
            Assert.IsTrue(flick.Update(new Vector2(4f, 2f), rightShoulder, 1f, 2 * dt, settings));
        }

        [Test]
        public void OneFlickIsOneThrowUntilCockedAgain()
        {
            var flick = new FlickDetector();
            flick.Update(new Vector2(14f, 6f), rightShoulder, 1f, 0f, settings);
            Assert.IsTrue(flick.Update(new Vector2(4f, 2f), rightShoulder, 1f, dt, settings));
            // Back out a little (not wound back) and across again: no throw.
            flick.Update(new Vector2(9f, -10f), rightShoulder, 1f, 0.3f, settings);
            Assert.IsFalse(flick.Update(new Vector2(3f, -10f), rightShoulder, 1f, 0.3f + dt, settings));
        }

        [Test]
        public void AHangingHandOrASlowMoveDoesNotThrow()
        {
            var hanging = new FlickDetector();
            hanging.Update(new Vector2(13f, -18f), rightShoulder, 1f, 0f, settings);
            Assert.IsFalse(hanging.Update(new Vector2(3f, -18f), rightShoulder, 1f, dt, settings));
            var slow = new FlickDetector();
            slow.Update(new Vector2(14f, 6f), rightShoulder, 1f, 0f, settings);
            Assert.IsFalse(slow.Update(new Vector2(6.8f, 2f), rightShoulder, 1f, 0.5f, settings));
        }

        [Test]
        public void TheLeftHandCocksOutToTheLeft()
        {
            var flick = new FlickDetector();
            var leftShoulder = new Vector2(-7f, 2f);
            flick.Update(new Vector2(-14f, 6f), leftShoulder, -1f, 0f, settings);
            Assert.IsTrue(flick.Update(new Vector2(-4f, 2f), leftShoulder, -1f, dt, settings));
        }
    }

    public class SpinAreaCallerTests
    {
        [Test]
        public void EveryFourCallsCoverAllAreasWithoutRepeats()
        {
            var caller = new SpinAreaCaller(7);
            var previous = caller.Current;
            for (var round = 0; round < 5; round++)
            {
                var seen = new HashSet<SpinArea> { caller.Current };
                for (var i = 0; i < 3; i++)
                {
                    var next = caller.Advance();
                    Assert.AreNotEqual(previous, next);
                    previous = next;
                    seen.Add(next);
                }

                Assert.AreEqual(4, seen.Count);
                var opener = caller.Advance();
                Assert.AreNotEqual(previous, opener);
                previous = opener;
            }
        }
    }
}
