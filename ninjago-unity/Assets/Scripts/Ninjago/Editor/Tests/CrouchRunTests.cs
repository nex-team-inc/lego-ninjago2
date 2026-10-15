#nullable enable

using NUnit.Framework;
using UnityEditor;

namespace Nex.Ninjago.Editor.Tests
{
    public class CrouchDetectorTests
    {
        const float crouchLine = 5f;
        const float standLine = 3f;
        const float hold = 0.15f;

        static bool Step(CrouchDetector detector, float drop, float time) => detector.Update(drop, time, crouchLine, standLine, hold);

        [Test]
        public void AClearDropHeldBrieflyIsACrouch()
        {
            var detector = new CrouchDetector();
            Assert.IsFalse(Step(detector, 8f, 0f));
            Assert.IsFalse(Step(detector, 8f, 0.1f));
            Assert.IsTrue(Step(detector, 8f, 0.16f));
        }

        [Test]
        public void ANodDoesNotCount()
        {
            var detector = new CrouchDetector();
            Step(detector, 6f, 0f);
            Step(detector, 6f, 0.08f);
            Assert.IsFalse(Step(detector, 0.5f, 0.12f));
            Assert.IsFalse(Step(detector, 0.5f, 1f));
            // A small dip that never reaches the line.
            Assert.IsFalse(Step(detector, 4f, 2f));
            Assert.IsFalse(Step(detector, 4f, 3f));
        }

        [Test]
        public void HysteresisKeepsTheCrouchBetweenTheLines()
        {
            var detector = Crouched();
            Assert.IsTrue(Step(detector, 4f, 2f));
            Assert.IsTrue(Step(detector, 3.5f, 3f));
            Assert.IsTrue(Step(detector, 4.5f, 4f));
        }

        [Test]
        public void StandingUpPastTheStandLineRunsAgain()
        {
            var detector = Crouched();
            Step(detector, 1f, 2f);
            Assert.IsTrue(Step(detector, 1f, 2.1f));
            Assert.IsFalse(Step(detector, 1f, 2.2f));
        }

        static CrouchDetector Crouched()
        {
            var detector = new CrouchDetector();
            Step(detector, 9f, 0f);
            Step(detector, 9f, 1f);
            Assert.IsTrue(detector.IsCrouching);
            return detector;
        }
    }

    public class CrouchLevelTests
    {
        static CrouchRunConfig.Level Level(params (CrouchZoneState state, float length)[] zones)
        {
            var level = new CrouchRunConfig.Level();
            foreach (var (state, length) in zones) level.zones.Add(new CrouchRunConfig.Zone { state = state, length = length });
            return level;
        }

        [Test]
        public void ZonesAreLaidOutInOrder()
        {
            var level = Level((CrouchZoneState.Clear, 40f), (CrouchZoneState.Talking, 24f), (CrouchZoneState.Clear, 10f));
            Assert.AreEqual(74f, level.Length, 1e-4f);
            Assert.AreEqual(40f, level.ZoneStart(1), 1e-4f);
            Assert.AreEqual(0, level.ZoneAt(0f));
            Assert.AreEqual(0, level.ZoneAt(39.9f));
            Assert.AreEqual(1, level.ZoneAt(40f));
            Assert.AreEqual(2, level.ZoneAt(70f));
            Assert.AreEqual(2, level.ZoneAt(90f));
        }
    }

    // The tuned levels in the config asset (rebuilt by Nex > Ninjago > Build Crouch Run).
    public class CrouchRunContentTests
    {
        static CrouchRunConfig Config => AssetDatabase.LoadAssetAtPath<CrouchRunConfig>("Assets/Configs/Ninjago/CrouchRunConfig.asset");

        [Test]
        public void EveryLevelHasAPerfectRunWithTimeLeft()
        {
            var config = Config;
            Assert.AreEqual(3, config.LevelCount);
            for (var i = 0; i < config.LevelCount; i++)
            {
                Assert.Less(config.PerfectSeconds(i), config.GetLevel(i).timerSeconds, $"level {i + 1}");
            }
        }

        [Test]
        public void CrouchingTheWholeLevelRunsOutOfTime()
        {
            var config = Config;
            for (var i = 0; i < config.LevelCount; i++)
            {
                Assert.Greater(config.GetLevel(i).Length / config.CrouchSpeed, config.GetLevel(i).timerSeconds, $"level {i + 1}");
            }
        }

        [Test]
        public void LevelsMatchTheBrief()
        {
            var config = Config;
            Assert.AreEqual(4, config.GetLevel(0).zones.Count);
            Assert.AreEqual(6, config.GetLevel(1).zones.Count);
            Assert.AreEqual(8, config.GetLevel(2).zones.Count);
            Assert.AreEqual(CrouchZoneState.Clear, config.GetLevel(0).zones[0].state);
            var last = config.GetLevel(2).zones[7];
            Assert.AreEqual(CrouchZoneState.Talking, last.state);
            foreach (var zone in config.GetLevel(2).zones)
            {
                if (zone.state == CrouchZoneState.Talking) Assert.LessOrEqual(zone.length, last.length);
            }

            // Margins shrink: level 3 finishes with a small margin.
            var margin3 = config.GetLevel(2).timerSeconds - config.PerfectSeconds(2);
            Assert.Less(margin3, config.GetLevel(0).timerSeconds - config.PerfectSeconds(0));
            Assert.Less(margin3, 6f);
        }
    }
}
