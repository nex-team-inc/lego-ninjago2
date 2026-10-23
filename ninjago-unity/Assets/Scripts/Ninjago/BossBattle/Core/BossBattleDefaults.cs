#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using Sfx = Nex.SfxManager.SoundEffect;
using Vfx = Nex.VfxManager.VisualEffect;

namespace Nex.Ninjago
{
    /// <summary>
    /// Default values of BossBattleConfig's EnumDictionaries, one factory per key (TDD §0: class values are filled by a
    /// static default factory from the field initializers and Reset; readers fall back to these when a key is missing).
    /// </summary>
    public static class BossBattleDefaults
    {
        #region Vehicles

        /// <summary>GDD §2/§6: the car on three lanes, the jet on a 3x3 grid (centre 3.2, rows 1.6/3.2/4.8).</summary>
        public static ChaseConfig.VehicleSettings Vehicle(BossPhase phase)
        {
            if (phase == BossPhase.Sky)
            {
                return new ChaseConfig.VehicleSettings
                {
                    speed = 13f, usesVerticalLean = true, halfWidth = 2.2f, centerHeight = 3.2f, halfHeight = 1.6f,
                    steerSmoothingSeconds = 0.3f, maxSteerSpeed = 7f, cellWidth = 2f, cellHeight = 1.6f,
                    vehicleHalfExtents = new Vector2(0.6f, 0.25f),
                };
            }

            return new ChaseConfig.VehicleSettings
            {
                speed = 14f, usesVerticalLean = false, halfWidth = 2.2f, centerHeight = 0f, halfHeight = 0f,
                steerSmoothingSeconds = 0.35f, maxSteerSpeed = 7f, cellWidth = 2f, cellHeight = 1.6f,
                vehicleHalfExtents = new Vector2(0.7f, 0.3f),
            };
        }

        public static EnumDictionary<BossPhase, ChaseConfig.VehicleSettings> Vehicles()
        {
            var vehicles = new EnumDictionary<BossPhase, ChaseConfig.VehicleSettings>();
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase))) vehicles[phase] = Vehicle(phase);
            return vehicles;
        }

        #endregion

        #region Difficulty And Damage

        public static EnumDictionary<BossDifficulty, DifficultySettings> Difficulties()
        {
            var difficulties = new EnumDictionary<BossDifficulty, DifficultySettings>();
            foreach (BossDifficulty difficulty in Enum.GetValues(typeof(BossDifficulty))) difficulties[difficulty] = DifficultySettings.Default(difficulty);
            return difficulties;
        }

        /// <summary>GDD §8: brick 1, rock 1, minion tick 1, wind 1, spike wall 2.</summary>
        public static int HazardDamage(BossHazard hazard) => hazard == BossHazard.SpikeWall ? 2 : 1;

        public static EnumDictionary<BossHazard, int> HazardDamages()
        {
            var damage = new EnumDictionary<BossHazard, int>();
            foreach (BossHazard hazard in Enum.GetValues(typeof(BossHazard))) damage[hazard] = HazardDamage(hazard);
            return damage;
        }

        #endregion

        #region Fx

        static readonly Color Orange = new(1f, 0.6f, 0.25f);
        static readonly Color Gold = new(1f, 0.85f, 0.4f);
        static readonly Color Cyan = new(0.45f, 0.9f, 1f);
        static readonly Color Violet = new(0.75f, 0.45f, 1f);

        /// <summary>Each cue to its TDD §2 SoundEffect / VisualEffect (loops are started with StartFx and follow a transform).</summary>
        public static FxEntry Fx(BossFxCue cue)
        {
            var fx = new FxEntry();
            switch (cue)
            {
                case BossFxCue.RockThrow: return fx.WithSound(Sfx.BossRockThrow);
                case BossFxCue.RockWhistle: return fx.WithSound(Sfx.BossRockWhistle).WithVisual(Vfx.BossRockTrail, 1f, true);
                case BossFxCue.RockWarning: return fx;   // the reticle and the "!" are the warning; no sound of its own
                case BossFxCue.RockLock: return fx.WithSound(Sfx.BossRockLock);
                case BossFxCue.RockImpact: return fx.WithSound(Sfx.BossRockImpact).WithVisual(Vfx.BossRockImpact, 1.5f).WithShake(0.25f, 0.3f);
                case BossFxCue.SlamWindup: return fx;    // pose and red tell on the actor
                case BossFxCue.GroundSlam: return fx.WithSound(Sfx.BossGroundSlam).WithVisual(Vfx.BossSlamShockwave, 3f).WithShake(0.35f, 0.4f);
                case BossFxCue.CrackGlow: return fx.WithSound(Sfx.BossCrackGlow, 0.6f).WithVisual(Vfx.BossCrackGlow);
                case BossFxCue.BrickGrow: return fx.WithSound(Sfx.BossBrickGrow, 0.8f).WithVisual(Vfx.BossBrickGrow);
                case BossFxCue.MinionThrow: return fx.WithSound(Sfx.BossMinionThrow);
                case BossFxCue.MinionLand: return fx.WithSound(Sfx.BossMinionLand).WithVisual(Vfx.BossMinionLand).WithShake(0.08f, 0.15f);
                case BossFxCue.MinionHit: return fx.WithSound(Sfx.BossMinionHit).WithVisual(Vfx.BossMinionHit);
                case BossFxCue.WallGrow: return fx.WithSound(Sfx.BossWallGrow).WithVisual(Vfx.BossBrickGrow, 1.5f);
                case BossFxCue.WallSmash: return fx.WithSound(Sfx.BossWallSmash).WithVisual(Vfx.BossWallSmash, 1.5f).WithShake(0.2f, 0.25f);
                case BossFxCue.PowerUpSpawn: return fx.WithSound(Sfx.BossPowerUpSpawn).WithVisual(Vfx.BossPowerUpGlint, 1f, true);
                case BossFxCue.PowerUpPickup: return fx.WithSound(Sfx.BossPowerUpPickup).WithVisual(Vfx.BossPowerUpPickup).WithLight(Gold, 2.5f, 0.35f);
                case BossFxCue.RocketLaunch: return fx.WithSound(Sfx.BossRocketLaunch, 0.8f).WithVisual(Vfx.BossRocketTrail, 1f, true);
                case BossFxCue.RocketHit: return fx.WithSound(Sfx.BossRocketHit).WithVisual(Vfx.BossRocketHit, 2f).WithShake(0.15f, 0.2f).WithLight(Orange, 3f, 0.3f);
                case BossFxCue.BossHurt: return fx.WithSound(Sfx.BossHurt).WithVisual(Vfx.BossHurt, 2f);
                case BossFxCue.BossRoar: return fx.WithSound(Sfx.BossRoar).WithShake(0.2f, 0.8f);
                case BossFxCue.TakeOff: return fx.WithSound(Sfx.BossTakeOff).WithVisual(Vfx.BossTakeOff, 3f).WithShake(0.3f, 0.6f);
                case BossFxCue.Transform: return fx.WithSound(Sfx.BossTransform).WithVisual(Vfx.BossTransformBurst, 2f).WithLight(Color.white, 3f, 0.5f);
                case BossFxCue.DevilWind: return fx.WithSound(Sfx.BossDevilWind, 0.8f, true).WithVisual(Vfx.BossDevilWind, 2f, true);
                case BossFxCue.GustPass: return fx.WithVisual(Vfx.BossGustPass);
                case BossFxCue.Tired: return fx.WithSound(Sfx.BossTired).WithVisual(Vfx.BossTiredZzz, 2f, true);
                case BossFxCue.LaserLoop: return fx.WithSound(Sfx.BossLaserLoop, 0.7f, true);
                case BossFxCue.LaserHit: return fx.WithSound(Sfx.BossLaserHit, 0.7f).WithVisual(Vfx.BossLaserHit).WithLight(Cyan, 1.5f, 0.15f);
                case BossFxCue.LaserDeflect: return fx.WithSound(Sfx.BossLaserDeflect, 0.5f).WithVisual(Vfx.BossLaserDeflect);
                case BossFxCue.Wake: return fx.WithSound(Sfx.BossWake).WithVisual(Vfx.BossWake, 3f).WithShake(0.3f, 0.5f);
                case BossFxCue.ClapInhale: return fx.WithSound(Sfx.BossClapInhale);
                case BossFxCue.ClapPush: return fx.WithSound(Sfx.BossDevilWind, 0.8f).WithVisual(Vfx.BossClapPush, 2f);
                case BossFxCue.ClapWarning: return fx.WithSound(Sfx.BossClapWarning);
                case BossFxCue.SpikeSlam: return fx.WithSound(Sfx.BossSpikeSlam).WithVisual(Vfx.BossSpikeSlam, 2f).WithShake(0.4f, 0.4f);
                case BossFxCue.ExplodeSmall: return fx.WithSound(Sfx.BossExplode, 0.7f).WithVisual(Vfx.BossExplodeSmall, 2f).WithShake(0.15f, 0.2f).WithLight(Orange, 3f, 0.25f);
                case BossFxCue.ExplodeBig: return fx.WithSound(Sfx.BossExplode).WithVisual(Vfx.BossExplodeBig, 4f).WithShake(0.5f, 0.8f).WithLight(Color.white, 4f, 0.6f);
                case BossFxCue.Victory: return fx.WithSound(Sfx.Victory).WithVisual(Vfx.BossVictoryConfetti, 2f);
                case BossFxCue.VehicleHit: return fx.WithSound(Sfx.BossVehicleHit).WithVisual(Vfx.BossVehicleHit).WithShake(0.25f, 0.3f);
                case BossFxCue.Knockout: return fx.WithSound(Sfx.BossKnockout).WithVisual(Vfx.BossExplodeSmall, 2f).WithShake(0.3f, 0.4f);
                case BossFxCue.BannerSlam: return fx.WithSound(Sfx.BossBannerSlam);
                case BossFxCue.Enrage: return fx.WithSound(Sfx.BossRoar).WithShake(0.2f, 0.5f);
                case BossFxCue.Countdown: return fx.WithSound(Sfx.BossCountdown);
                case BossFxCue.Laugh: return fx.WithSound(Sfx.BossLaugh);
                case BossFxCue.Lightning: return fx.WithVisual(Vfx.BossLightning, 4f).WithLight(Violet, 2f, 0.2f);
                case BossFxCue.RebuildSparkle: return fx.WithSound(Sfx.BrickSnap).WithVisual(Vfx.BossRebuildSparkle, 2f);
                case BossFxCue.LaserMuzzle: return fx.WithVisual(Vfx.BossLaserMuzzle);
                case BossFxCue.Embers: return fx.WithVisual(Vfx.BossEmbers, 1f, true);
                case BossFxCue.StormDust: return fx.WithVisual(Vfx.BossStormDust, 1f, true);
                default: return fx;
            }
        }

        public static EnumDictionary<BossFxCue, FxEntry> FxTable()
        {
            var table = new EnumDictionary<BossFxCue, FxEntry>();
            foreach (BossFxCue cue in Enum.GetValues(typeof(BossFxCue))) table[cue] = Fx(cue);
            return table;
        }

        #endregion

        #region Prompts And Tells

        static readonly Color PromptWhite = new(1f, 1f, 1f);
        static readonly Color PromptGreen = new(0.45f, 1f, 0.45f);
        static readonly Color PromptYellow = new(1f, 0.9f, 0.3f);
        static readonly Color PromptRed = new(1f, 0.35f, 0.3f);
        static readonly Color PromptOrange = new(1f, 0.65f, 0.25f);
        static readonly Color PromptCyan = new(0.5f, 0.9f, 1f);

        /// <summary>Key ninjago.boss.prompt.&lt;snake_case id&gt;; seconds 0 = until HidePrompt.</summary>
        public static PromptEntry Prompt(BossPromptId id)
        {
            return id switch
            {
                BossPromptId.LeanToSteer => PromptEntry.Of("ninjago.boss.prompt.lean_to_steer", PromptWhite, 0f, PromptArrow.LeftRight),
                BossPromptId.GrabRockets => PromptEntry.Of("ninjago.boss.prompt.grab_rockets", PromptYellow, 2.5f),
                BossPromptId.DodgeRock => PromptEntry.Of("ninjago.boss.prompt.dodge_rock", PromptOrange, 2f, PromptArrow.LeftRight),
                BossPromptId.SmashLeft => PromptEntry.Of("ninjago.boss.prompt.smash_left", PromptGreen, 0f, PromptArrow.Left),
                BossPromptId.SmashRight => PromptEntry.Of("ninjago.boss.prompt.smash_right", PromptGreen, 0f, PromptArrow.Right),
                BossPromptId.SmashBoth => PromptEntry.Of("ninjago.boss.prompt.smash_both", PromptGreen, 0f, PromptArrow.LeftRight),
                BossPromptId.DodgeBricks => PromptEntry.Of("ninjago.boss.prompt.dodge_bricks", PromptRed, 2f, PromptArrow.LeftRight),
                BossPromptId.RiseAndDuck => PromptEntry.Of("ninjago.boss.prompt.rise_and_duck", PromptWhite, 0f, PromptArrow.UpDown),
                BossPromptId.DodgeWind => PromptEntry.Of("ninjago.boss.prompt.dodge_wind", PromptCyan, 2f),
                BossPromptId.TiredFire => PromptEntry.Of("ninjago.boss.prompt.tired_fire", PromptYellow, 2.5f),
                BossPromptId.FlyIntoRing => PromptEntry.Of("ninjago.boss.prompt.fly_into_ring", PromptGreen, 3f),
                BossPromptId.OnTarget => PromptEntry.Of("ninjago.boss.prompt.on_target", PromptGreen, 0f),
                BossPromptId.Dive => PromptEntry.Of("ninjago.boss.prompt.dive", PromptRed, 0f, PromptArrow.Down),
                BossPromptId.Brace => PromptEntry.Of("ninjago.boss.prompt.brace", PromptOrange, 1.5f),
                BossPromptId.FinishIt => PromptEntry.Of("ninjago.boss.prompt.finish_it", PromptYellow, 2.5f),
                BossPromptId.Enraged => PromptEntry.Of("ninjago.boss.prompt.enraged", PromptRed, 2f),
                BossPromptId.MinionHurry => PromptEntry.Of("ninjago.boss.prompt.minion_hurry", PromptRed, 0f),
                BossPromptId.GetReady => PromptEntry.Of("ninjago.boss.prompt.get_ready", PromptWhite, 3f),
                _ => PromptEntry.Of("", PromptWhite, 2f),
            };
        }

        public static EnumDictionary<BossPromptId, PromptEntry> Prompts()
        {
            var prompts = new EnumDictionary<BossPromptId, PromptEntry>();
            foreach (BossPromptId id in Enum.GetValues(typeof(BossPromptId))) prompts[id] = Prompt(id);
            return prompts;
        }

        /// <summary>HDR tell colours (max channel >= 2.5 so they bloom): rock orange, minion green, slam red, wind violet, clap white.</summary>
        public static Color TellColor(BossAttackIcon icon)
        {
            return icon switch
            {
                BossAttackIcon.Rock => new Color(3f, 1.4f, 0.25f),
                BossAttackIcon.Minion => new Color(0.5f, 3f, 0.6f),
                BossAttackIcon.Slam => new Color(3f, 0.35f, 0.3f),
                BossAttackIcon.Wind => new Color(1.8f, 0.6f, 3f),
                BossAttackIcon.Clap => new Color(2.8f, 2.8f, 2.8f),
                BossAttackIcon.Tired => new Color(0.8f, 1.3f, 2.6f),
                _ => Color.black,
            };
        }

        public static EnumDictionary<BossAttackIcon, Color> TellColors()
        {
            var colors = new EnumDictionary<BossAttackIcon, Color>();
            foreach (BossAttackIcon icon in Enum.GetValues(typeof(BossAttackIcon))) colors[icon] = TellColor(icon);
            return colors;
        }

        #endregion
    }
}
