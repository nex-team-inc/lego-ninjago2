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

    public class SpinDetectorTests
    {
        const float window = 0.35f;
        static readonly Vector2 area = new(-7f, 11f);

        static bool Circle(SpinDetector spin, Vector2 center, float radius, float hz, float seconds, float areaRadius = 8f)
        {
            for (var t = 0f; t <= seconds; t += 1f / 60f)
            {
                spin.Add(center + radius * new Vector2(Mathf.Cos(t * hz * Mathf.PI * 2f), Mathf.Sin(t * hz * Mathf.PI * 2f)), t, window);
                if (spin.TryBurst(area, areaRadius, 18f, 7f)) return true;
            }

            return false;
        }

        [Test]
        public void WildMotionInTheCalledSpotBursts()
        {
            Assert.IsTrue(Circle(new SpinDetector(), area + new Vector2(1f, -1f), 4f, 3f, 1f));
        }

        [Test]
        public void TheSameMotionElsewhereDoesNotCount()
        {
            Assert.IsFalse(Circle(new SpinDetector(), new Vector2(7f, 11f), 4f, 3f, 1f));
            Assert.IsFalse(Circle(new SpinDetector(), new Vector2(-7f, 0f), 4f, 3f, 1f));
        }

        [Test]
        public void SlowOrWideMotionDoesNotCount()
        {
            Assert.IsFalse(Circle(new SpinDetector(), area, 4f, 0.5f, 1f));
            Assert.IsFalse(Circle(new SpinDetector(), area, 12f, 3f, 1f, 20f));
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
