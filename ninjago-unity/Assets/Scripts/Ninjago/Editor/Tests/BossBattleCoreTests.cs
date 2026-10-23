#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    // Pure Boss Battle logic (TDD §3.3, §9). Configs are fresh ScriptableObject instances with the GDD defaults.
    // Every fixture name starts with "BossBattle" so `utests BossBattle` runs them all.

    public class BossBattleHealthTests
    {
        [Test]
        public void DamageClampsAtTheFloorAndReturnsWhatWasDealt()
        {
            var health = new BossHealth(100f);
            var changes = 0;
            health.Changed += () => changes++;
            Assert.AreEqual(20f, health.Damage(20f), 1e-4f);
            Assert.AreEqual(0.8f, health.Fraction, 1e-4f);
            // Floor at the 70 % notch: only 10 of the 50 lands.
            Assert.AreEqual(10f, health.Damage(50f, 0.7f), 1e-4f);
            Assert.AreEqual(70f, health.Current, 1e-4f);
            Assert.AreEqual(0f, health.Damage(5f, 0.7f));
            Assert.AreEqual(2, changes);
            Assert.IsFalse(health.IsDepleted);
            health.Damage(1000f);
            Assert.IsTrue(health.IsDepleted);
            health.SetFraction(0.3f);
            Assert.AreEqual(30f, health.Current, 1e-4f);
        }
    }

    public class BossBattleRocketVolleysTests
    {
        static readonly List<RocketVolley> Table = PowerUpSettings.Default().volleys;

        [Test]
        public void RoadHpIsTheTableSum()
        {
            Assert.AreEqual(230f, RocketVolleys.TotalHp(Table, 5, 10f), 1e-4f);
            Assert.AreEqual(230f, PowerUpSettings.Default().RoadHp, 1e-4f);
        }

        [Test]
        public void BarAfterEachPickupMatchesTheGdd()
        {
            var health = new BossHealth(RocketVolleys.TotalHp(Table, 5, 10f));
            var expected = new[] { 96, 78, 52, 26, 0 };
            Assert.AreEqual(100, Mathf.RoundToInt(health.Fraction * 100f));
            for (var pickup = 0; pickup < 5; pickup++)
            {
                var rockets = RocketVolleys.RocketsFor(Table, pickup);
                for (var i = 0; i < rockets; i++) health.Damage(RocketVolleys.DamagePerRocket(Table, pickup, 10f));
                Assert.AreEqual(expected[pickup], Mathf.RoundToInt(health.Fraction * 100f), $"after pickup {pickup + 1}");
                Assert.AreEqual(RocketVolleys.HpAfter(Table, pickup + 1, 10f), health.Current, 1e-3f);
            }

            Assert.IsTrue(health.IsDepleted);
        }

        [Test]
        public void VolleyTableAndIndexPastTheEnd()
        {
            Assert.AreEqual(1, RocketVolleys.RocketsFor(Table, 0));
            Assert.AreEqual(10f, RocketVolleys.DamagePerRocket(Table, 0, 10f), 1e-4f);
            Assert.AreEqual(2, RocketVolleys.RocketsFor(Table, 1));
            Assert.AreEqual(20f, RocketVolleys.DamagePerRocket(Table, 1, 10f), 1e-4f);
            Assert.AreEqual(3, RocketVolleys.RocketsFor(Table, 9));
            Assert.AreEqual(20f, RocketVolleys.DamagePerRocket(Table, 9, 10f), 1e-4f);
            Assert.AreEqual(0f, RocketVolleys.HpAfter(Table, 7, 10f, 5));
        }
    }

    public class BossBattleBeatScriptTests
    {
        static readonly List<BeatEntry> Road = RoadSettings.DefaultScript();

        [Test]
        public void WalksTheScriptAndLoopsFromLoopFrom()
        {
            var script = new BeatScript(Road, 9, 0, true);
            for (var i = 0; i < Road.Count; i++)
            {
                Assert.AreEqual(i, script.Index);
                Assert.AreEqual(Road[i].beat, script.Next().beat, $"entry {i}");
            }

            Assert.AreEqual(9, script.Index);
            Assert.AreEqual(BossBeat.PowerUp, script.Next().beat);
            Assert.IsFalse(script.Finished);
        }

        [Test]
        public void NonLoopingScriptFinishes()
        {
            var sky = SkySettings.DefaultScript();
            var script = new BeatScript(sky, 0, sky.Count - 1, false);
            Assert.AreEqual(BossBeat.LaserWindow, script.Next().beat);
            Assert.IsTrue(script.Finished);
            Assert.AreEqual(BossBeat.Breather, script.Next().beat);
        }

        [Test]
        public void MissedCrateReturnsRightAfterTheNextAttack()
        {
            // Index 3 is the first crate; it is missed, then rock (4) is an attack: the crate comes back before the minion.
            var script = new BeatScript(Road, 9, 3, true);
            Assert.AreEqual(BossBeat.PowerUp, script.Next().beat);
            script.RequeuePowerUpAfterNextAttack();
            Assert.AreEqual(BossBeat.RockDrop, script.Next().beat);
            Assert.AreEqual(5, script.Index);
            Assert.AreEqual(BossBeat.PowerUp, script.Next().beat);
            Assert.AreEqual(5, script.Index, "the requeued crate does not move the saved index");
            Assert.AreEqual(BossBeat.MinionThrow, script.Next().beat);
        }

        [Test]
        public void RequeueIsSkippedWhenTheScriptAlreadyHasTheCrateNext()
        {
            // Index 5 minion, 6 crate: the missed crate is the scripted one.
            var script = new BeatScript(Road, 9, 5, true);
            script.RequeuePowerUpAfterNextAttack();
            Assert.AreEqual(BossBeat.MinionThrow, script.Next().beat);
            Assert.AreEqual(BossBeat.PowerUp, script.Next().beat);
            Assert.AreEqual(BossBeat.GroundSlam, script.Next().beat);
        }

        [Test]
        public void RequeuedCrateWaitsForTheOverlappedEntry()
        {
            // Slam overlapping a rock: the crate comes back after the rock, never between the two.
            var script = new BeatScript(new List<BeatEntry>
            {
                BeatEntry.Of(BossBeat.GroundSlam, 3, overlapNext: true),
                BeatEntry.Of(BossBeat.RockDrop),
                BeatEntry.Of(BossBeat.MinionThrow, 1, MinionSides.Left),
            }, 0, 0, false);
            script.RequeuePowerUpAfterNextAttack();
            Assert.AreEqual(BossBeat.GroundSlam, script.Next().beat);
            Assert.IsTrue(script.PowerUpRequeued);
            Assert.AreEqual(BossBeat.RockDrop, script.Next().beat);
            Assert.AreEqual(BossBeat.PowerUp, script.Next().beat);
            Assert.AreEqual(BossBeat.MinionThrow, script.Next().beat);
            Assert.IsTrue(script.Finished);
        }

        [Test]
        public void ResumeAtBeatTenWithTheFourthVolley()
        {
            var config = ScriptableObject.CreateInstance<BossBattleConfig>();
            try
            {
                var save = new BossBattleSave { phase = BossPhase.Road, pickupsCollected = 3, beatIndex = 10, difficulty = BossDifficulty.Normal, numPlayers = 1 };
                var state = BossRunState.FromSave(save, config);
                var script = new BeatScript(config.Road.script, config.Road.loopFrom, state.beatIndex, true);
                Assert.AreEqual(10, script.Index);
                var next = script.Next();
                Assert.AreEqual(BossBeat.MinionThrow, next.beat);
                Assert.AreEqual(MinionSides.Right, next.sides);
                // The 4th volley: 3 rockets at x2.
                var volleys = config.PowerUp.volleys;
                Assert.AreEqual(3, RocketVolleys.RocketsFor(volleys, state.pickupsCollected));
                Assert.AreEqual(20f, RocketVolleys.DamagePerRocket(volleys, state.pickupsCollected, config.PowerUp.baseRocketDamage), 1e-4f);
                Assert.AreEqual(120f, state.RoadHealth.Current, 1e-3f);
                Assert.AreEqual(52, Mathf.RoundToInt(state.RoadHealth.Fraction * 100f));
                Assert.AreEqual(5, state.hearts);
                Assert.IsTrue(state.isContinue);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }

    public class BossBattleVehicleSteererTests
    {
        static readonly SteerSettings Steer = SteerSettings.Default();

        // RunnerWorld.Step's math, written out: SharedSteer blend → target → per-axis SmoothDamp.
        static Vector2 RunnerWorldReference(ChaseConfig.VehicleSettings settings, Vector2?[] steers, int frames, float deltaTime, Vector2 start)
        {
            var shared = new SharedSteer();
            var position = start;
            var velocity = Vector2.zero;
            for (var frame = 0; frame < frames; frame++)
            {
                var blended = shared.Blend(steers, Steer.playerOneShare, deltaTime);
                var target = new Vector2(blended.x * settings.halfWidth, settings.centerHeight + blended.y * settings.halfHeight);
                position.x = Mathf.SmoothDamp(position.x, target.x, ref velocity.x, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
                position.y = Mathf.SmoothDamp(position.y, target.y, ref velocity.y, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
            }

            return position;
        }

        static VehicleSteerer NewSteerer(ChaseConfig.VehicleSettings settings)
        {
            var steerer = new VehicleSteerer();
            steerer.Configure(settings, Steer);
            steerer.Reset(new Vector2(0f, settings.centerHeight));
            return steerer;
        }

        [Test]
        public void MatchesRunnerWorldForAConstantLean()
        {
            foreach (var phase in new[] { BossPhase.Road, BossPhase.Sky })
            {
                var settings = BossBattleDefaults.Vehicle(phase);
                var lean = ChestSteer.Steer(new Vector2(5f, 3f), settings.usesVerticalLean, Steer.deadzoneInches, Steer.fullLeanInches, Steer.fullRiseInches);
                var steers = new Vector2?[] { lean, new Vector2(-0.2f, 0f) };
                var steerer = NewSteerer(settings);
                for (var frame = 0; frame < 90; frame++)
                {
                    steerer.Step(steers, 1f / 60f);
                    var reference = RunnerWorldReference(settings, steers, frame + 1, 1f / 60f, new Vector2(0f, settings.centerHeight));
                    Assert.AreEqual(reference.x, steerer.Position.x, 1e-5f, $"{phase} frame {frame} x");
                    Assert.AreEqual(reference.y, steerer.Position.y, 1e-5f, $"{phase} frame {frame} y");
                }
            }
        }

        [Test]
        public void ForcedYOverridesTheLeanAndSidewaysStaysFree()
        {
            var settings = BossBattleDefaults.Vehicle(BossPhase.Sky);
            var steerer = NewSteerer(settings);
            steerer.ForcedY = 4.8f;
            var steers = new Vector2?[] { new Vector2(1f, -1f), null };
            for (var frame = 0; frame < 180; frame++) steerer.Step(steers, 1f / 60f);
            Assert.AreEqual(4.8f, steerer.Target.y, 1e-5f);
            Assert.AreEqual(4.8f, steerer.Position.y, 0.02f);
            Assert.AreEqual(2.2f, steerer.Position.x, 0.02f);
            steerer.ForcedY = null;
            steerer.Step(steers, 1f / 60f);
            Assert.AreEqual(1.6f, steerer.Target.y, 1e-5f);
        }

        [Test]
        public void PerSideReachAndVerticalSwitch()
        {
            var settings = BossBattleDefaults.Vehicle(BossPhase.Road);
            var steerer = NewSteerer(settings);
            steerer.ReachRight = 2.45f;
            steerer.Step(new Vector2?[] { new Vector2(1f, 0f), null }, 1f / 60f);
            Assert.AreEqual(2.45f, steerer.Target.x, 1e-5f);
            steerer.Step(new Vector2?[] { new Vector2(-1f, 0f), null }, 1f / 60f);
            Assert.AreEqual(-2.2f, steerer.Target.x, 1e-5f);

            var sky = NewSteerer(BossBattleDefaults.Vehicle(BossPhase.Sky));
            sky.VerticalEnabled = false;
            sky.Step(new Vector2?[] { new Vector2(0f, 1f), null }, 1f / 60f);
            Assert.AreEqual(3.2f, sky.Target.y, 1e-5f);
            sky.ForcedX = -1f;
            sky.Step(new Vector2?[] { new Vector2(1f, 0f), null }, 1f / 60f);
            Assert.AreEqual(-1f, sky.Target.x, 1e-5f);
        }

        [Test]
        public void PlayerSteerAndContribution()
        {
            var steerer = NewSteerer(BossBattleDefaults.Vehicle(BossPhase.Road));
            steerer.Step(new Vector2?[] { new Vector2(1f, 0f), new Vector2(0.4f, 0f) }, 0.1f);
            Assert.AreEqual(new Vector2(0.4f, 0f), steerer.PlayerSteer(1));
            Assert.AreEqual(0.5f, steerer.Contribution(0), 1e-4f);
            Assert.AreEqual(0.2f, steerer.Contribution(1), 1e-4f);
            Assert.AreEqual(0, steerer.DominantPlayer);
            // A lone tracked player carries the whole blend.
            steerer.Step(new Vector2?[] { null, new Vector2(-0.6f, 0f) }, 0.1f);
            Assert.IsNull(steerer.PlayerSteer(0));
            Assert.AreEqual(0.6f, steerer.Contribution(1), 1e-4f);
            Assert.AreEqual(-0.6f * 2.2f, steerer.Target.x, 1e-4f);
        }
    }

    public class BossBattleControllerFilterTests
    {
        const float Frame = 1f / 60f;

        static void Run(ControllerFilter filter, float c0, float c1, int players, float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame) filter.Update(c0, c1, players, Frame);
        }

        [Test]
        public void SoloPlayerIsAlwaysShown()
        {
            var filter = new ControllerFilter();
            Assert.AreEqual(0, filter.Update(0f, 0f, 1, Frame));
            Assert.IsTrue(filter.Changed);
            Assert.AreEqual(0, filter.Update(0f, 0f, 1, Frame));
            Assert.IsFalse(filter.Changed);
        }

        [Test]
        public void PromotedAfterLeadingForPointSixSeconds()
        {
            var filter = new ControllerFilter();
            Run(filter, 0.45f, 0.1f, 2, 0.5f);
            Assert.AreEqual(-1, filter.Current, "not before 0.6 s");
            Run(filter, 0.45f, 0.1f, 2, 0.15f);
            Assert.AreEqual(0, filter.Current);
            // A small lead (< 0.2) never switches.
            Run(filter, 0.3f, 0.45f, 2, 2f);
            Assert.AreEqual(0, filter.Current);
            // A clear lead by player 2 does.
            Run(filter, 0.1f, 0.45f, 2, 0.7f);
            Assert.AreEqual(1, filter.Current);
        }

        [Test]
        public void NeutralWhenNobodySteers()
        {
            var filter = new ControllerFilter();
            Run(filter, 0.5f, 0f, 2, 0.7f);
            Assert.AreEqual(0, filter.Current);
            Run(filter, 0.1f, 0.05f, 2, 0.3f);
            Assert.AreEqual(0, filter.Current, "a short idle keeps the highlight");
            var changedToNeutral = false;
            for (var t = 0f; t < 0.5f; t += Frame)
            {
                filter.Update(0.1f, 0.05f, 2, Frame);
                if (filter.Changed && filter.Current == -1) changedToNeutral = true;
            }

            Assert.AreEqual(-1, filter.Current);
            Assert.IsTrue(changedToNeutral);
        }
    }

    public class BossBattleLaneGridTests
    {
        [Test]
        public void CellCentersMatchTheRunner()
        {
            var road = BossBattleDefaults.Vehicle(BossPhase.Road);
            var sky = BossBattleDefaults.Vehicle(BossPhase.Sky);
            Assert.AreEqual(new Vector2(-2f, 0f), LaneGrid.CellCenter(road, 0, 0, false));
            Assert.AreEqual(new Vector2(2f, 0f), LaneGrid.CellCenter(road, 2, 0, false));
            Assert.AreEqual(1.6f, LaneGrid.CellCenter(sky, 1, 0, true).y, 1e-5f);
            Assert.AreEqual(4.8f, LaneGrid.CellCenter(sky, 1, 2, true).y, 1e-5f);
        }

        [Test]
        public void OverlapsOnTheRoadIgnoreHeightAndInTheSkyUseIt()
        {
            var road = BossBattleDefaults.Vehicle(BossPhase.Road);
            Assert.IsFalse(LaneGrid.Overlaps(road, new Vector2(0f, 0f), SlamPatternPicker.OpenCenter, false, 0.9f));
            Assert.IsTrue(LaneGrid.Overlaps(road, new Vector2(0f, 5f), SlamPatternPicker.OpenLeft, false, 0.9f));
            Assert.IsFalse(LaneGrid.Overlaps(road, new Vector2(-2.2f, 0f), SlamPatternPicker.OpenLeft, false, 0.9f));
            var sky = BossBattleDefaults.Vehicle(BossPhase.Sky);
            Assert.IsFalse(LaneGrid.Overlaps(sky, new Vector2(0f, 4.8f), WindPatternPicker.Climb, true, 0.9f));
            Assert.IsTrue(LaneGrid.Overlaps(sky, new Vector2(0f, 3.2f), WindPatternPicker.Climb, true, 0.9f));
            Assert.AreEqual(1, LaneGrid.FreeCells(WindPatternPicker.Ring, true));
            Assert.AreEqual(2, LaneGrid.ColumnOf(road, 2.3f));
            Assert.AreEqual(0, LaneGrid.RowOf(sky, 1.7f));
        }
    }

    public class BossBattleSlamPatternPickerTests
    {
        [Test]
        public void FirstSlamUsesTheFixedMasks()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var picker = new SlamPatternPicker(seed);
                Assert.AreEqual(SlamPatternPicker.OpenCenter, picker.Next(1.9f, 0, 0));
                var second = picker.Next(1.9f, 0, 1);
                Assert.That(second == SlamPatternPicker.OpenLeft || second == SlamPatternPicker.OpenRight, $"seed {seed}: {second}");
            }
        }

        [Test]
        public void UnderTheJumpLimitTheOpenLaneMovesOneLaneAtMost()
        {
            var picker = new SlamPatternPicker(7);
            for (var slam = 1; slam < 200; slam++)
            {
                var previous = picker.Next(1.5f, slam, 0);
                for (var row = 1; row < 4; row++)
                {
                    var mask = picker.Next(1.5f, slam, row);
                    Assert.AreNotEqual(previous, mask);
                    Assert.LessOrEqual(SlamPatternPicker.LaneJump(previous, mask), 1, $"slam {slam} row {row}");
                    Assert.AreEqual(2, 3 - LaneGrid.FreeCells(mask, false), "two-lane barrier");
                    previous = mask;
                }
            }
        }

        [Test]
        public void SameSeedSamePatterns()
        {
            var a = new SlamPatternPicker(42);
            var b = new SlamPatternPicker(42);
            for (var i = 0; i < 50; i++) Assert.AreEqual(a.Next(1.7f, 1 + i / 3, i % 3), b.Next(1.7f, 1 + i / 3, i % 3));
        }
    }

    public class BossBattleWindPatternPickerTests
    {
        [Test]
        public void FirstBarrageIsSingleAxisAndEveryMaskLeavesACell()
        {
            var picker = new WindPatternPicker(3);
            for (var row = 0; row < 40; row++)
            {
                var mask = picker.Next(0, 1.9f);
                Assert.IsTrue(WindPatternPicker.IsSingleAxis(mask), $"row {row}");
                Assert.GreaterOrEqual(WindPatternPicker.FreeCells(mask), 1);
            }

            for (var barrage = 1; barrage < 30; barrage++)
            {
                for (var row = 0; row < 5; row++) Assert.GreaterOrEqual(WindPatternPicker.FreeCells(picker.Next(barrage, 1.6f)), 1);
            }
        }

        [Test]
        public void NoClimbDiveBackToBackUnderTheLimit()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var picker = new WindPatternPicker(seed);
                for (var barrage = 0; barrage < 10; barrage++)
                {
                    var previous = picker.Next(barrage, 1.7f);
                    for (var row = 1; row < 5; row++)
                    {
                        var mask = picker.Next(barrage, 1.7f);
                        Assert.IsFalse(WindPatternPicker.IsClimbDiveSwap(previous, mask), $"seed {seed} barrage {barrage} row {row}");
                        Assert.AreNotEqual(previous, mask);
                        previous = mask;
                    }
                }
            }
        }
    }

    public class BossBattleRockScheduleTests
    {
        [Test]
        public void ImpactsStayAtLeastPointEightFromRowArrivals()
        {
            var rows = new List<float> { 2f, 3.7f, 5.4f };
            for (var desired = 0f; desired < 8f; desired += 0.05f)
            {
                var impact = RockSchedule.PlaceImpact(desired, rows, 0.8f);
                Assert.GreaterOrEqual(impact, desired - 1e-5f, "never earlier than possible");
                Assert.IsTrue(RockSchedule.IsClear(impact, rows, 0.8f - 1e-4f), $"desired {desired:0.00} -> {impact:0.00}");
            }
        }

        [Test]
        public void PlacedMidwayBetweenTwoRows()
        {
            var rows = new List<float> { 2f, 3.7f, 5.4f };
            Assert.AreEqual(2.85f, RockSchedule.PlaceImpact(2.5f, rows, 0.8f), 1e-4f);
            Assert.AreEqual(1f, RockSchedule.PlaceImpact(1f, rows, 0.8f), 1e-4f, "before the first row: unchanged");
            Assert.AreEqual(6.2f, RockSchedule.PlaceImpact(5f, rows, 0.8f), 1e-4f, "after the last row");
        }
    }

    public class BossBattleRockReticleTests
    {
        const float Frame = 1f / 60f;

        [Test]
        public void TimelineFollowsLocksFallsAndLandsOnce()
        {
            var settings = RockSettings.Default();
            var reticle = new RockReticle();
            reticle.Start(0f, settings, false, 1f);
            Assert.AreEqual(1f + 1.6f + 1.2f + 0.35f, reticle.TotalSeconds, 1e-4f);
            var impacts = 0;
            var lockedX = float.NaN;
            var t = 0f;
            var seenFollowing = false;
            while (reticle.IsActive && t < 10f)
            {
                t += Frame;
                reticle.Tick(Frame, 2f);
                if (reticle.State == ReticleState.Following) seenFollowing = true;
                if (reticle.State == ReticleState.Locked && float.IsNaN(lockedX)) lockedX = reticle.X;
                if (reticle.State is ReticleState.Locked or ReticleState.Falling) Assert.AreEqual(lockedX, reticle.X, 1e-5f, "locked: no follow");
                if (reticle.ImpactNow) impacts++;
            }

            Assert.IsTrue(seenFollowing);
            Assert.AreEqual(1, impacts);
            Assert.AreEqual(4.15f, t, 2f * Frame);
            Assert.AreEqual(2f, reticle.X, 0.05f, "followed the car");
            reticle.Tick(Frame, 2f);
            Assert.IsFalse(reticle.ImpactNow);
        }

        [Test]
        public void FirstRockLocksLongerAndEasyScalesFollowAndLock()
        {
            var settings = RockSettings.Default();
            var reticle = new RockReticle();
            reticle.Start(0f, settings, true, 1f);
            Assert.AreEqual(1f + 1.6f + 1.6f + 0.35f, reticle.TotalSeconds, 1e-4f);
            reticle.Start(0f, settings, false, 1.25f);
            Assert.AreEqual(1f + 2f + 1.5f + 0.35f, reticle.TotalSeconds, 1e-4f);
            reticle.Start(0f, settings, false, 1f, 0.5f);
            Assert.AreEqual(1f + 2.1f + 1.2f + 0.35f, reticle.TotalSeconds, 1e-4f);
        }

        [Test]
        public void RingTouchingTheCarHits()
        {
            var reticle = new RockReticle();
            reticle.Start(0f, RockSettings.Default(), false, 1f);
            Assert.IsTrue(reticle.IsHit(1.2f, 0.7f, 0.55f));
            Assert.IsFalse(reticle.IsHit(1.3f, 0.7f, 0.55f));
            Assert.IsFalse(reticle.IsHit(-2.2f, 0.7f, 0.55f));
        }
    }

    public class BossBattleSideMinionStateTests
    {
        const float Frame = 1f / 60f;

        static (int damage, float gaveUpAt, List<float> tickTimes) RunUntilGiveUp(SideMinionState minion)
        {
            var damage = 0;
            var t = 0f;
            var tickTimes = new List<float>();
            while (minion.Active && t < 30f)
            {
                t += Frame;
                var ticks = minion.Tick(Frame);
                for (var i = 0; i < ticks; i++) tickTimes.Add(t);
                damage += ticks;
            }

            return (damage, t, tickTimes);
        }

        [Test]
        public void FirstMinionGraceTickAndOneTickThenGivesUp()
        {
            var minion = new SideMinionState();
            minion.Start(MinionSides.Left, true, MinionSettings.Default(), 1f);
            Assert.AreEqual(4f, minion.GraceSeconds, 1e-4f);
            Assert.AreEqual(3.5f, minion.TickSeconds, 1e-4f);
            Assert.AreEqual(1, minion.MaxTicks);
            var (damage, gaveUpAt, ticks) = RunUntilGiveUp(minion);
            Assert.AreEqual(1, damage);
            Assert.AreEqual(7.5f, ticks[0], 2f * Frame);
            Assert.AreEqual(7.5f, gaveUpAt, 2f * Frame);
            Assert.IsTrue(minion.GaveUp);
            Assert.IsFalse(minion.Cleared);
        }

        [Test]
        public void LaterMinionTicksTwice()
        {
            var minion = new SideMinionState();
            minion.Start(MinionSides.Right, false, MinionSettings.Default(), 1f);
            var (damage, _, ticks) = RunUntilGiveUp(minion);
            Assert.AreEqual(2, damage);
            Assert.AreEqual(5f, ticks[0], 2f * Frame);
            Assert.AreEqual(7.5f, ticks[1], 2f * Frame);
            Assert.IsTrue(minion.GaveUp);
        }

        [Test]
        public void SmashNeedsTheWallTheSideAndOutwardSpeed()
        {
            var settings = MinionSettings.Default();
            var minion = new SideMinionState();
            minion.Start(MinionSides.Right, false, settings, 1f);
            minion.Tick(0.3f);
            Assert.IsFalse(minion.WallReady);
            Assert.IsFalse(minion.TryClear(2.3f, 2f, false), "wall still growing");
            minion.Tick(0.4f);
            Assert.IsTrue(minion.WallReady);
            Assert.IsFalse(minion.TryClear(1.9f, 2f, false), "not far enough out");
            Assert.IsFalse(minion.TryClear(2.3f, 0.4f, false), "too slow outward");
            Assert.IsFalse(minion.TryClear(2.3f, -1f, false), "moving inward");
            Assert.IsFalse(minion.TryClear(-2.3f, -2f, false), "wrong side");
            Assert.IsTrue(minion.TryClear(2.05f, 0.6f, false));
            Assert.IsTrue(minion.Cleared);
            Assert.AreEqual(0, minion.Tick(10f), "a cleared minion deals no damage");

            var left = new SideMinionState();
            left.Start(MinionSides.Left, false, settings, 1f);
            left.Tick(1f);
            Assert.IsTrue(left.TryClear(-2.4f, -0.8f, false));
        }

        [Test]
        public void TwoPlayerSoloSmash()
        {
            var settings = MinionSettings.Default();
            Assert.IsTrue(SideMinionState.IsSoloLean(new Vector2(-0.9f, 0f), MinionSides.Left, settings.soloSmashSteer));
            Assert.IsFalse(SideMinionState.IsSoloLean(new Vector2(-0.7f, 0f), MinionSides.Left, settings.soloSmashSteer));
            Assert.IsFalse(SideMinionState.IsSoloLean(new Vector2(0.9f, 0f), MinionSides.Left, settings.soloSmashSteer));
            Assert.IsFalse(SideMinionState.IsSoloLean(null, MinionSides.Left, settings.soloSmashSteer));
            var minion = new SideMinionState();
            minion.Start(MinionSides.Left, false, settings, 1f);
            Assert.IsFalse(minion.TryClear(0f, 0f, true), "not before the wall is up");
            minion.Tick(0.7f);
            Assert.IsTrue(minion.TryClear(0f, 0f, true), "the blended car is in the middle; the solo lean still smashes");
        }

        [Test]
        public void HeldGraceWaitsForTheOtherSide()
        {
            var settings = MinionSettings.Default();
            var minion = new SideMinionState();
            minion.Start(MinionSides.Right, false, settings, 1f);
            minion.SetGraceSeconds(settings.bothGraceSeconds);
            minion.GraceHeld = true;
            for (var i = 0; i < 600; i++) Assert.AreEqual(0, minion.Tick(Frame));
            Assert.IsTrue(minion.WallReady);
            Assert.IsTrue(minion.InGrace);
            minion.GraceHeld = false;
            var (damage, _, ticks) = RunUntilGiveUp(minion);
            Assert.AreEqual(2, damage);
            Assert.AreEqual(3.5f + 2.5f, ticks[0], 2f * Frame);
        }
    }

    public class BossBattleLemniscatePathTests
    {
        [Test]
        public void BoundsAreExactlyTheHalfSizeAroundTheCentre()
        {
            var settings = LaserSettings.Default();
            var path = LemniscatePath.From(settings);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var t = 0f; t <= 8f; t += 0.001f)
            {
                var p = path.Evaluate(t);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Assert.AreEqual(-1.8f, min.x, 1e-3f);
            Assert.AreEqual(1.8f, max.x, 1e-3f);
            Assert.AreEqual(2.2f, min.y, 1e-3f);
            Assert.AreEqual(4.2f, max.y, 1e-3f);
            Assert.GreaterOrEqual(min.x, -1.8f - 1e-5f);
            Assert.LessOrEqual(max.y, 4.2f + 1e-5f);
            Assert.AreEqual(new Vector2(0f, 3.2f), path.Evaluate(0f));
            Assert.AreEqual(1.8f, path.Evaluate(2f).x, 1e-4f, "a quarter period: the right loop");
        }
    }

    public class BossBattleLaserAimTests
    {
        [Test]
        public void OnTargetAssistAndCoreRay()
        {
            var settings = LaserSettings.Default();
            Assert.IsTrue(LaserAim.OnTarget(new Vector2(0.9f, 3.2f), new Vector2(0f, 3.2f), 0.95f));
            Assert.IsFalse(LaserAim.OnTarget(new Vector2(1f, 3.2f), new Vector2(0f, 3.2f), 0.95f));
            Assert.AreEqual(0.95f, LaserAim.AssistRadius(0.95f, 3f, settings), 1e-5f);
            Assert.AreEqual(0.95f * 1.2f, LaserAim.AssistRadius(0.95f, 9f, settings), 1e-5f);
            Assert.AreEqual(0.95f * 1.4f, LaserAim.AssistRadius(0.95f, 20f, settings), 1e-5f);
            var core = LaserAim.CoreOnCameraRay(new Vector3(0f, 3.4f, -8f), new Vector3(1f, 3.4f, 0f), 30f);
            Assert.AreEqual(30f, core.z, 1e-4f);
            Assert.AreEqual(1f * 38f / 8f, core.x, 1e-4f);
            Assert.AreEqual(3.4f, core.y, 1e-4f);
        }
    }

    public class BossBattleClapWallsTests
    {
        [Test]
        public void NormalAndLargeBands()
        {
            var clap = ClapSettings.Default();
            var normal = new ClapWalls(clap.SizeFor(0));
            Assert.AreEqual(3.36f, normal.BandBottom, 1e-4f);
            Assert.IsTrue(normal.IsHit(3.2f, 0.25f));
            Assert.IsFalse(normal.IsHit(2.8f, 0.25f));
            var large = new ClapWalls(clap.SizeFor(1));
            Assert.AreEqual(2.56f, large.BandBottom, 1e-4f);
            Assert.IsTrue(large.IsHit(2.8f, 0.25f));
            Assert.IsFalse(large.IsHit(1.6f, 0.25f));
            Assert.AreEqual(2.2f, clap.SizeFor(5).warnSeconds, 1e-5f, "later claps reuse the large size");
        }
    }

    public class BossBattleRunStateTests
    {
        [Test]
        public void NewSaveAndContinue()
        {
            var config = ScriptableObject.CreateInstance<BossBattleConfig>();
            try
            {
                var easy = BossRunState.New(config, BossDifficulty.Easy, 2);
                Assert.AreEqual(8, easy.hearts);
                Assert.AreEqual(230f, easy.RoadHealth.Max, 1e-4f);
                Assert.AreEqual(100f, easy.SkyHealth.Max, 1e-4f);
                Assert.IsFalse(string.IsNullOrEmpty(easy.runId));

                var state = BossRunState.New(config, BossDifficulty.Normal, 1);
                state.phase = BossPhase.Sky;
                state.wakesPassed = 2;
                state.beatIndex = 7;
                state.hearts = 2;
                state.Stats.AddHit(BossHazard.Rock);
                state.Stats.AddHit(BossHazard.SpikeWall);
                state.Stats.boltsFired = 40;
                state.Stats.boltsHit = 10;
                state.Stats.elapsedSeconds = 123f;
                var save = state.ToSave();
                Assert.AreEqual(1, save.version);
                Assert.AreEqual(2, save.hitsTaken);

                var resumed = BossRunState.FromSave(save, config);
                Assert.AreEqual(state.runId, resumed.runId);
                Assert.AreEqual(BossPhase.Sky, resumed.phase);
                Assert.AreEqual(7, resumed.beatIndex);
                Assert.AreEqual(5, resumed.hearts, "hearts full on continue");
                Assert.AreEqual(0.3f, resumed.SkyHealth.Fraction, 1e-4f, "at the last wake notch passed");
                Assert.AreEqual(2, resumed.Stats.HitsTaken);
                Assert.AreEqual(0.25f, resumed.Stats.Accuracy, 1e-4f);
                Assert.AreEqual(123f, resumed.Stats.elapsedSeconds, 1e-4f);
                Assert.AreSame(resumed.SkyHealth, resumed.ActiveHealth);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void StatsCountHitsPerHazard()
        {
            var stats = new BossBattleStats();
            stats.AddHit(BossHazard.Brick);
            stats.AddHit(BossHazard.Brick);
            stats.AddHit(BossHazard.Wind);
            stats.restoredHits = 4;
            Assert.AreEqual(2, stats.HitsBy(BossHazard.Brick));
            Assert.AreEqual(7, stats.HitsTaken);
            Assert.AreEqual(0f, stats.Accuracy);
            stats.Reset();
            Assert.AreEqual(0, stats.HitsTaken);
        }
    }

    public class BossBattleTimeScaleTests
    {
        [TearDown]
        public void TearDown() => BossTimeScale.Reset();

        [Test]
        public void PauseWinsOverSlowMoAndResetRestoresOne()
        {
            BossTimeScale.SetSlowMo(0.35f);
            Assert.AreEqual(0.35f, Time.timeScale, 1e-5f);
            BossTimeScale.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            BossTimeScale.SetPaused(false);
            Assert.AreEqual(0.35f, Time.timeScale, 1e-5f);
            BossTimeScale.Reset();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(BossTimeScale.IsPaused);
        }
    }
}
