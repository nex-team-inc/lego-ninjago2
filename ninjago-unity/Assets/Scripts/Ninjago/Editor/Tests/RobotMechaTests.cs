#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    public class RobotArmsPoseTests
    {
        // Frame space, y up: chest at 1.0, hips at 0.6 (a 0.4 torso).
        const float chestY = 1f;
        const float hipY = 0.6f;
        const float slack = 0.5f;
        static readonly Vector2 chest = new(0.5f, chestY);
        static readonly Vector2 leftShoulder = new(0.42f, 1.02f);
        static readonly Vector2 rightShoulder = new(0.58f, 1.02f);

        [Test]
        public void ForearmsUpInFrontHoldTheL()
        {
            // Elbows down by the sides, hands in front a little below the chest.
            Assert.IsTrue(RobotArmsPose.IsRaised(0.92f, chestY, hipY, slack));
            Assert.AreEqual(0.2f, RobotArmsPose.HandDrop(0.92f, chestY, hipY), 1e-4f);
        }

        [Test]
        public void HandsCloserToTheHipsThanTheChestAreSlacking()
        {
            Assert.IsFalse(RobotArmsPose.IsRaised(0.75f, chestY, hipY, slack));
            Assert.IsFalse(RobotArmsPose.IsRaised(0.55f, chestY, hipY, slack));
            Assert.IsTrue(RobotArmsPose.IsRaised(0.85f, chestY, hipY, slack));
        }

        [Test]
        public void HandsAboveTheChestStillHoldTheL()
        {
            Assert.IsTrue(RobotArmsPose.IsRaised(1.15f, chestY, hipY, slack));
            Assert.Less(RobotArmsPose.HandDrop(1.15f, chestY, hipY), 0f);
        }

        [Test]
        public void TheSlackLineMovesTheCutoff()
        {
            Assert.IsFalse(RobotArmsPose.IsRaised(0.85f, chestY, hipY, 0.3f));
            Assert.IsTrue(RobotArmsPose.IsRaised(0.75f, chestY, hipY, 0.8f));
        }

        [Test]
        public void SidewaysIsMeasuredFromTheShoulders()
        {
            var centered = RobotArmsPose.BodyOffset(new Vector2(0.44f, 0.92f), new Vector2(0.56f, 0.92f), leftShoulder, rightShoulder, chest);
            Assert.AreEqual(0f, centered.x, 1e-4f);
            Assert.AreEqual(-0.08f, centered.y, 1e-4f);
            // Both forearms swing toward the player's right.
            var right = RobotArmsPose.BodyOffset(new Vector2(0.50f, 0.92f), new Vector2(0.62f, 0.92f), leftShoulder, rightShoulder, chest);
            Assert.AreEqual(0.06f, right.x, 1e-4f);
        }

        [Test]
        public void SwayingTheWholeBodyDoesNotSteer()
        {
            var shift = new Vector2(0.1f, -0.03f);
            var before = RobotArmsPose.BodyOffset(new Vector2(0.47f, 0.95f), new Vector2(0.6f, 0.93f), leftShoulder, rightShoulder, chest);
            var after = RobotArmsPose.BodyOffset(new Vector2(0.47f, 0.95f) + shift, new Vector2(0.6f, 0.93f) + shift, leftShoulder + shift, rightShoulder + shift,
                chest + shift);
            Assert.AreEqual(before.x, after.x, 1e-4f);
            Assert.AreEqual(before.y, after.y, 1e-4f);
        }
    }

    public class PoseLatchTests
    {
        const float gain = 0.1f;
        const float loss = 0.2f;

        [Test]
        public void GainsAfterTheGainTime()
        {
            var latch = new PoseLatch();
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(true, 0f, gain, loss));
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(true, 0.05f, gain, loss));
            Assert.AreEqual(PoseLatch.Change.Gained, latch.Update(true, 0.1f, gain, loss));
            Assert.IsTrue(latch.Held);
        }

        [Test]
        public void OneDroppedFrameDoesNotLoseThePose()
        {
            var latch = Held();
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(false, 1f, gain, loss));
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(true, 1.03f, gain, loss));
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(true, 2f, gain, loss));
            Assert.IsTrue(latch.Held);
        }

        [Test]
        public void ABrokenArmLosesThePoseAfterTheLossTime()
        {
            var latch = Held();
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(false, 1f, gain, loss));
            Assert.AreEqual(PoseLatch.Change.Lost, latch.Update(false, 1.2f, gain, loss));
            Assert.IsFalse(latch.Held);
        }

        [Test]
        public void ReleaseDropsAtOnce()
        {
            var latch = Held();
            latch.Release();
            Assert.IsFalse(latch.Held);
            Assert.AreEqual(PoseLatch.Change.None, latch.Update(false, 2f, gain, loss));
        }

        static PoseLatch Held()
        {
            var latch = new PoseLatch();
            latch.Update(true, 0f, gain, loss);
            latch.Update(true, 0.5f, gain, loss);
            return latch;
        }
    }

    public class MechaAimTests
    {
        static readonly MechaAim.Settings settings = new(1f, 10f, 180f, 7f, 40f, false);

        [Test]
        public void BreathingInsideTheDeadzoneDoesNotSteer()
        {
            Assert.AreEqual(Vector2.zero, MechaAim.Desired(new Vector2(0.8f, -0.9f), settings));
        }

        [Test]
        public void HandsToThePlayersRightTurnRightAndLeftTurnLeft()
        {
            Assert.AreEqual(90f, MechaAim.Desired(new Vector2(6f, 0f), settings).x, 1e-3f);
            Assert.AreEqual(-90f, MechaAim.Desired(new Vector2(-6f, 0f), settings).x, 1e-3f);
        }

        [Test]
        public void FarToOneSideFacesBehind()
        {
            Assert.AreEqual(180f, MechaAim.Desired(new Vector2(30f, 0f), settings).x, 1e-3f);
            Assert.AreEqual(-180f, MechaAim.Desired(new Vector2(-30f, 0f), settings).x, 1e-3f);
        }

        [Test]
        public void HigherHandsAimUpAndPitchIsClamped()
        {
            Assert.Greater(MechaAim.Desired(new Vector2(0f, 3f), settings).y, 0f);
            Assert.Less(MechaAim.Desired(new Vector2(0f, -3f), settings).y, 0f);
            Assert.AreEqual(40f, MechaAim.Desired(new Vector2(0f, 50f), settings).y, 1e-3f);
        }

        [Test]
        public void SwappedAxesTurnWithHandHeight()
        {
            var swapped = new MechaAim.Settings(1f, 10f, 180f, 7f, 40f, true);
            var desired = MechaAim.Desired(new Vector2(0f, 6f), swapped);
            Assert.AreEqual(90f, desired.x, 1e-3f);
            Assert.AreEqual(0f, desired.y, 1e-3f);
        }
    }

    public class TurnFollowerTests
    {
        const float yawSpeed = 78f;
        const float acceleration = 320f;
        const float dt = 1f / 60f;

        [Test]
        public void ForwardToBehindTakesAboutTwoAndAHalfSeconds()
        {
            var follower = new TurnFollower();
            var seconds = 0f;
            while (follower.Value < 179.9f && seconds < 10f)
            {
                follower.Step(180f, yawSpeed, acceleration, dt);
                seconds += dt;
            }

            Assert.That(seconds, Is.InRange(2.3f, 2.8f));
        }

        [Test]
        public void NeverPassesTheTargetOrTheTopSpeed()
        {
            var follower = new TurnFollower();
            for (var i = 0; i < 600; i++)
            {
                follower.Step(90f, yawSpeed, acceleration, dt);
                Assert.LessOrEqual(follower.Value, 90f + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(follower.Velocity), yawSpeed + 1e-3f);
            }

            Assert.AreEqual(90f, follower.Value, 1e-3f);
        }

        [Test]
        public void DoesNotSnapToANewTarget()
        {
            var follower = new TurnFollower();
            follower.Step(180f, yawSpeed, acceleration, dt);
            Assert.Less(follower.Value, 1f);
        }

        [Test]
        public void StopHoldsTheFacing()
        {
            var follower = new TurnFollower();
            for (var i = 0; i < 30; i++) follower.Step(180f, yawSpeed, acceleration, dt);
            var facing = follower.Value;
            follower.Stop();
            Assert.AreEqual(0f, follower.Velocity);
            Assert.AreEqual(facing, follower.Value);
        }
    }

    public class MechaBeamTests
    {
        static readonly Vector3 muzzle = new(0f, 1.3f, 0.8f);
        const float range = 32f;
        const float beamRadius = 0.4f;

        [Test]
        public void AnEnemyStraightAheadIsHit()
        {
            Assert.AreEqual(12f, MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, new Vector3(0f, 0f, 12.8f), 0.8f, 1.3f), 0.05f);
        }

        [Test]
        public void AnEnemyBesideTheBeamIsMissed()
        {
            Assert.Less(MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, new Vector3(1.5f, 0f, 12f), 0.8f, 1.3f), 0f);
            Assert.GreaterOrEqual(MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, new Vector3(1.1f, 0f, 12f), 0.8f, 1.3f), 0f);
        }

        [Test]
        public void AnEnemyBehindOrOutOfRangeIsMissed()
        {
            Assert.Less(MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, new Vector3(0f, 0f, -5f), 0.8f, 1.3f), 0f);
            Assert.Less(MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, new Vector3(0f, 0f, 40f), 0.8f, 1.3f), 0f);
        }

        [Test]
        public void AHighArcherNeedsTheBeamRaised()
        {
            var feet = new Vector3(0f, 6f, 17f);
            Assert.Less(MechaBeam.HitDistance(muzzle, Vector3.forward, range, beamRadius, feet, 0.5f, 2.2f), 0f);
            var raised = Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward;
            Assert.Greater(MechaBeam.HitDistance(muzzle, raised, range, beamRadius, feet, 0.5f, 2.2f), 0f);
        }

        [Test]
        public void ABeamAimedDownStopsAtTheGround()
        {
            var down = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward;
            Assert.AreEqual(1.3f / Mathf.Sin(30f * Mathf.Deg2Rad), MechaBeam.GroundDistance(muzzle, down, range), 1e-3f);
            Assert.AreEqual(range, MechaBeam.GroundDistance(muzzle, Vector3.forward, range));
        }
    }
}
