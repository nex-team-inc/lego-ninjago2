#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    // Serializable sections of BossBattleConfig (TDD §3.2; numbers = GDD v2). Each section is a class with public fields
    // (read directly by the runners, written live by the Debug Settings rows) and a static Default() factory that the
    // config's field initializers and Reset() use. Durations are seconds, distances metres in arena local space.

    #region Entries

    /// <summary>One step of a phase script. A value type, so BeatScript hands out copies and never edits the asset.</summary>
    [Serializable]
    public struct BeatEntry
    {
        [Tooltip("What the boss does in this step.")]
        public BossBeat beat;
        [Tooltip("Rows of a slam, rocks of a drop, gust rows of a devil wind; 0 or less counts as 1.")]
        public int count;
        [Tooltip("Minion throws only: the roof corner(s) the minions land on.")]
        public MinionSides sides;
        [Tooltip("Claps only: index into the clap sizes (0 normal, 1 large; later indices reuse the last size).")]
        public int size;
        [Tooltip("The next entry starts while this one runs (a rock placed between slam rows, a wind during a clap).")]
        public bool overlapNext;
        [Tooltip("No breather after this step.")]
        public bool skipBreather;
        [Tooltip("Extra seconds before this step starts.")]
        public float extraDelay;

        /// <summary>The count with the "0 or less means 1" rule applied.</summary>
        public int Count => count <= 0 ? 1 : count;

        public static BeatEntry Of(BossBeat beat, int count = 1, MinionSides sides = default, int size = 0, bool overlapNext = false,
            bool skipBreather = false, float extraDelay = 0f)
        {
            return new BeatEntry
            {
                beat = beat, count = count, sides = sides, size = size, overlapNext = overlapNext, skipBreather = skipBreather,
                extraDelay = extraDelay,
            };
        }

        public override string ToString() => $"{beat} x{Count}{(sides != default ? " " + sides : "")}{(overlapNext ? " +overlap" : "")}";
    }

    /// <summary>The rockets one power-up pickup fires and how hard each one hits (multiplier of the base rocket damage).</summary>
    [Serializable]
    public struct RocketVolley
    {
        [Tooltip("Rockets fired by this pickup.")]
        public int rockets;
        [Tooltip("Damage of each rocket as a multiple of the base rocket damage (2 = the \"x2 DMG\" popup).")]
        public float damageMultiplier;

        public static RocketVolley Of(int rockets, float damageMultiplier) => new() { rockets = rockets, damageMultiplier = damageMultiplier };
    }

    /// <summary>How much of the sky grid (from the top) a clap's spiked walls cover, and how long the dive warning lasts.</summary>
    [Serializable]
    public struct ClapSize
    {
        [Tooltip("Fraction of the grid height covered by the walls, measured from the top (0.45 normal, 0.7 large).")]
        public float band;
        [Tooltip("Seconds of the DIVE warning before the walls slam together.")]
        public float warnSeconds;

        public static ClapSize Of(float band, float warnSeconds) => new() { band = band, warnSeconds = warnSeconds };
    }

    /// <summary>
    /// What a BossFxCue plays: a SfxManager sound, a VfxManager visual, a camera shake and a pooled point light. Any part
    /// can be off; a missing entry plays nothing.
    /// </summary>
    [Serializable]
    public class FxEntry
    {
        [Tooltip("Plays the sound below.")]
        public bool hasSound;
        public SfxManager.SoundEffect sound = SfxManager.SoundEffect.None;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("StartFx keeps the sound looping until StopFx (laser, devil wind).")]
        public bool loopSound;
        [Tooltip("Plays the visual below (VisualEffect has no None member, hence this switch).")]
        public bool hasVisual;
        public VfxManager.VisualEffect visual;
        public float visualScale = 1f;
        [Tooltip("The visual loops and follows a transform until StopFx (trails, glints, Zzz).")]
        public bool loopVisual;
        [Tooltip("Camera shake amount (metres) at the moment the cue plays; 0 = none.")]
        public float shake;
        public float shakeSeconds;
        [Tooltip("Pulses a pooled no-shadow point light at the cue (rocket hits, explosions, laser impacts).")]
        public bool pointLight;
        public Color lightColor = Color.white;
        public float lightIntensity = 2f;
        public float lightSeconds = 0.3f;

        #region Builders (defaults only)

        public FxEntry WithSound(SfxManager.SoundEffect effect, float soundVolume = 1f, bool loop = false)
        {
            hasSound = true;
            sound = effect;
            volume = soundVolume;
            loopSound = loop;
            return this;
        }

        public FxEntry WithVisual(VfxManager.VisualEffect effect, float scale = 1f, bool loop = false)
        {
            hasVisual = true;
            visual = effect;
            visualScale = scale;
            loopVisual = loop;
            return this;
        }

        public FxEntry WithShake(float amount, float seconds)
        {
            shake = amount;
            shakeSeconds = seconds;
            return this;
        }

        public FxEntry WithLight(Color color, float intensity, float seconds = 0.3f)
        {
            pointLight = true;
            lightColor = color;
            lightIntensity = intensity;
            lightSeconds = seconds;
            return this;
        }

        #endregion
    }

    /// <summary>A HUD prompt: localization key, text colour, default seconds (0 or less = until HidePrompt) and arrow.</summary>
    [Serializable]
    public class PromptEntry
    {
        [Tooltip("Localization key in LocalizationTable (ninjago.boss.prompt.*).")]
        public string key = "";
        public Color color = Color.white;
        [Tooltip("Seconds on screen when ShowPrompt is called with -1; 0 or less = until HidePrompt.")]
        public float seconds = 2.5f;
        public PromptArrow arrow;

        public static PromptEntry Of(string key, Color color, float seconds, PromptArrow arrow = PromptArrow.None) =>
            new() { key = key, color = color, seconds = seconds, arrow = arrow };
    }

    #endregion

    #region Sections

    /// <summary>Chest lean to steer (same rule as Chest Chase: ChestSteer + SharedSteer).</summary>
    [Serializable]
    public class SteerSettings
    {
        [Tooltip("Chest movement from the calibrated origin (body inches) ignored so breathing does not swerve.")]
        public float deadzoneInches = 1.5f;
        [Tooltip("Sideways chest lean (body inches) that steers to the edge of the lane.")]
        public float fullLeanInches = 7f;
        [Tooltip("Chest rise or drop (body inches) that climbs or dives to the edge of the sky grid.")]
        public float fullRiseInches = 4f;
        [Tooltip("Player 1's share of the blended steer when both chests are tracked; player 2 gets the rest. 0.5 = equal.")]
        [Range(0f, 1f)] public float playerOneShare = 0.5f;

        public static SteerSettings Default() => new();
    }

    /// <summary>Hearts and timing per difficulty (GDD §8).</summary>
    [Serializable]
    public class DifficultySettings
    {
        [Tooltip("Hearts at each phase start and on continue.")]
        public int hearts = 5;
        [Tooltip("Multiplies wind-ups, telegraphs, locks, warnings and spacing (higher = slower = easier).")]
        public float timingScale = 1f;
        [Tooltip("Multiplies the laser hit radius.")]
        public float laserRadiusScale = 1f;

        public static DifficultySettings Default(BossDifficulty difficulty)
        {
            return difficulty == BossDifficulty.Easy
                ? new DifficultySettings { hearts = 8, timingScale = 1.25f, laserRadiusScale = 1.3f }
                : new DifficultySettings();
        }
    }

    /// <summary>Phase 1 pacing and the road beat script (GDD §4, §4.5).</summary>
    [Serializable]
    public class RoadSettings
    {
        [Tooltip("Distance (m) of the boss ahead of the car; it walks backwards at this gap.")]
        public float bossDistance = 30f;
        [Tooltip("Sideways sway amplitude (m) of the walking boss.")]
        public float bossSwayX = 1.2f;
        public float bossSwayPeriod = 6f;
        [Tooltip("3-2-1 + roar at every phase start (including continue).")]
        public float readySeconds = 3f;
        [Tooltip("Steer check: the car must reach x <= -reach and x >= +reach once.")]
        public float steerCheckReach = 1.6f;
        public float steerCheckMaxSeconds = 8f;
        [Tooltip("Pause after every beat.")]
        public float breatherSeconds = 1.2f;
        [Tooltip("Boss bar fraction below which the boss is enraged.")]
        [Range(0f, 1f)] public float enrageFraction = 0.3f;
        [Tooltip("Wind-up multiplier while enraged (never telegraph, lock, warning or spacing times).")]
        public float enrageWindupScale = 0.85f;
        [Tooltip("The phase 1 beats in order (designer list).")]
        public List<BeatEntry> script = DefaultScript();
        [Tooltip("0-based index the script loops back to when it ends before the 5th pickup.")]
        public int loopFrom = 9;

        public static RoadSettings Default() => new();

        /// <summary>GDD §4.5. Beat #11 (slam x3 + an overlapped rock) is two entries, so the GDD's #12-#16 are entries 13-17.</summary>
        public static List<BeatEntry> DefaultScript()
        {
            return new List<BeatEntry>
            {
                BeatEntry.Of(BossBeat.Ready),                                   // 0
                BeatEntry.Of(BossBeat.SteerCheck),                              // 1
                BeatEntry.Of(BossBeat.GroundSlam, 2),                           // 2  spacing 1.9 (first slam, fixed masks)
                BeatEntry.Of(BossBeat.PowerUp),                                 // 3  crate #1 in the lane next to the car
                BeatEntry.Of(BossBeat.RockDrop, 1),                             // 4  first rock: slow lock
                BeatEntry.Of(BossBeat.MinionThrow, 1, MinionSides.Left),        // 5  first minion: slow
                BeatEntry.Of(BossBeat.PowerUp),                                 // 6
                BeatEntry.Of(BossBeat.GroundSlam, 3),                           // 7  spacing 1.7
                BeatEntry.Of(BossBeat.RockDrop, 2),                             // 8
                BeatEntry.Of(BossBeat.PowerUp),                                 // 9  loopFrom
                BeatEntry.Of(BossBeat.MinionThrow, 1, MinionSides.Right),       // 10
                BeatEntry.Of(BossBeat.GroundSlam, 3, overlapNext: true),        // 11 spacing 1.7 ...
                BeatEntry.Of(BossBeat.RockDrop, 1),                             // 12 ... + a rock between two rows
                BeatEntry.Of(BossBeat.PowerUp),                                 // 13
                BeatEntry.Of(BossBeat.RockDrop, 3),                             // 14
                BeatEntry.Of(BossBeat.MinionThrow, 1, MinionSides.Both),        // 15
                BeatEntry.Of(BossBeat.GroundSlam, 4),                           // 16 spacing 1.5
                BeatEntry.Of(BossBeat.PowerUp),                                 // 17 5th pickup -> phase 2
            };
        }
    }

    /// <summary>Rock Drop (GDD §4.1).</summary>
    [Serializable]
    public class RockSettings
    {
        [Tooltip("Arm raised with the orange glow before the throw (scaled by the wind-up scale).")]
        public float windupSeconds = 0.8f;
        [Tooltip("Rock up in the sky before the reticle appears.")]
        public float airSeconds = 1f;
        [Tooltip("Reticle and \"!\" follow the car (scaled by the timing scale).")]
        public float followSeconds = 1.6f;
        [Tooltip("SmoothDamp time of the reticle following the car.")]
        public float followSmoothSeconds = 0.25f;
        [Tooltip("Locked, blinking reticle before the fall (scaled by the timing scale).")]
        public float lockSeconds = 1.2f;
        [Tooltip("Lock of the first rock of the run.")]
        public float firstLockSeconds = 1.6f;
        public float fallSeconds = 0.35f;
        [Tooltip("Ring radius (m); hit when |carX - ringX| < radius + car half width.")]
        public float rockRadius = 0.55f;
        [Tooltip("The next rock of a pair/triple locks at least this long after the previous impact.")]
        public float nextLockAfterImpactSeconds = 1.5f;
        [Tooltip("An overlapped rock lands at least this long from any brick row arrival.")]
        public float overlapMinGapFromRow = 0.8f;

        public float LockSeconds(bool first) => first ? firstLockSeconds : lockSeconds;

        public static RockSettings Default() => new();
    }

    /// <summary>Ground Slam (GDD §4.3).</summary>
    [Serializable]
    public class SlamSettings
    {
        [Tooltip("Both fists raised with the red glow (scaled by the wind-up scale).")]
        public float windupSeconds = 1f;
        [Tooltip("A row's crack glows this long before its bricks grow.")]
        public float crackSeconds = 0.45f;
        public float growSeconds = 0.3f;
        [Tooltip("Bricks finish growing this long before reaching the car (at speed 14: 21 m).")]
        public float finishZSeconds = 1.5f;
        [Tooltip("Seconds between rows, per slam of the run (first, mid, mid, late); later slams reuse the last value.")]
        public List<float> rowSpacingBySlam = new() { 1.9f, 1.7f, 1.7f, 1.5f };
        [Tooltip("Under this row spacing, the open lane moves by at most one lane (no 011 <-> 110 back to back).")]
        public float maxLaneJumpBelow = 1.8f;
        [Tooltip("Fixed row masks of the first slam (bit = column, 1 = brick): 101 then 110 (or its mirror 011).")]
        public List<int> firstMasks = new() { 0b101, 0b110 };
        [Tooltip("Fraction of a lane a brick covers; the rest is a forgiving margin.")]
        [Range(0.5f, 1f)] public float cellFill = 0.9f;

        public float RowSpacing(int slamIndexInRun)
        {
            if (rowSpacingBySlam.Count == 0) return 1.7f;
            return rowSpacingBySlam[Mathf.Clamp(slamIndexInRun, 0, rowSpacingBySlam.Count - 1)];
        }

        /// <summary>Debug row: one spacing for every slam.</summary>
        public void SetAllRowSpacing(float seconds)
        {
            for (var i = 0; i < rowSpacingBySlam.Count; i++) rowSpacingBySlam[i] = seconds;
        }

        public static SlamSettings Default() => new();
    }

    /// <summary>Minion Throw and the side walls (GDD §4.2).</summary>
    [Serializable]
    public class MinionSettings
    {
        [Tooltip("Green hand glow before the throw (scaled by the wind-up scale).")]
        public float windupSeconds = 0.9f;
        [Tooltip("Arc from the boss hand to the roof corner.")]
        public float flightSeconds = 1f;
        [Tooltip("Seconds on the car before the damage clock starts (scaled by the timing scale).")]
        public float graceSeconds = 2.5f;
        [Tooltip("After the grace, one heart is lost every tick (scaled by the timing scale).")]
        public float tickSeconds = 2.5f;
        [Tooltip("Damage ticks before the minion jumps off by itself (result: failed).")]
        public int maxTicks = 2;
        [Header("First Minion (slower)")]
        public float firstWindupSeconds = 1.2f;
        public float firstFlightSeconds = 1.6f;
        public float firstGraceSeconds = 4f;
        public float firstTickSeconds = 3.5f;
        public int firstMaxTicks = 1;
        [Header("Wall And Smash")]
        [Tooltip("The brick wall grows along the road edge; smashing works once it has finished.")]
        public float wallGrowSeconds = 0.6f;
        [Tooltip("Car reach (m) on a walled side, so its body meets the wall.")]
        public float walledReach = 2.45f;
        [Tooltip("Smash when the car is at least this far out on the walled side ...")]
        public float smashX = 2f;
        [Tooltip("... moving outward faster than this (m/s).")]
        public float smashMinOutwardSpeed = 0.5f;
        [Tooltip("2P: a player's own lean toward the wall at least this strong drives the car into it.")]
        [Range(0f, 1f)] public float soloSmashSteer = 0.82f;
        [Tooltip("2P: seconds the car is driven into the wall before the smash.")]
        public float soloDriveSeconds = 0.25f;
        [Header("Both Sides")]
        [Tooltip("Grace of each minion in the Both variant.")]
        public float bothGraceSeconds = 3.5f;
        [Tooltip("The second side's grace starts when the first clears or this long after landing.")]
        public float bothSecondStartMaxSeconds = 3f;
        [Tooltip("Minion offset from the vehicle (x = outward from the centre, toward the side).")]
        public Vector3 roofOffset = new(0.55f, 1f, -0.2f);

        public float Windup(bool first) => first ? firstWindupSeconds : windupSeconds;
        public float Flight(bool first) => first ? firstFlightSeconds : flightSeconds;

        public static MinionSettings Default() => new();
    }

    /// <summary>Rocket crates and volleys (GDD §4.4). Phase 1 HP = the volley table's sum, so the last pickup empties the bar.</summary>
    [Serializable]
    public class PowerUpSettings
    {
        [Tooltip("Pickup 1, 2, 3 ...; pickups past the list reuse the last volley.")]
        public List<RocketVolley> volleys = new()
        {
            RocketVolley.Of(1, 1f), RocketVolley.Of(2, 2f), RocketVolley.Of(3, 2f), RocketVolley.Of(3, 2f), RocketVolley.Of(3, 2f),
        };
        [Tooltip("Damage of one rocket at multiplier 1 (with the default table: phase 1 HP 230).")]
        public float baseRocketDamage = 10f;
        [Tooltip("The pickup that empties the bar and starts phase 2.")]
        public int pickupsToPhase2 = 5;
        [Tooltip("Seconds after the previous beat before the crate spawns.")]
        public float leadSeconds = 0.3f;
        [Tooltip("Seconds from the crate spawning ahead to reaching the car.")]
        public float spawnLeadSeconds = 1.6f;
        public float rocketFlightSeconds = 1f;
        [Tooltip("Seconds between two rockets of a volley (keep above the 35 ms SFX throttle).")]
        public float rocketStaggerSeconds = 0.15f;
        [Tooltip("Crate half width (m) for the pickup overlap test.")]
        public float pickupHalfWidth = 0.9f;
        [Tooltip("Hit-stop on each rocket hit.")]
        public float rocketHitStopSeconds = 0.04f;

        public float RoadHp => RocketVolleys.TotalHp(volleys, pickupsToPhase2, baseRocketDamage);

        public static PowerUpSettings Default() => new();
    }

    /// <summary>Phase 2 pacing, the wake notches and the sky beat script (GDD §6, §6.4).</summary>
    [Serializable]
    public class SkySettings
    {
        public float bossDistance = 30f;
        public float readySeconds = 3f;
        [Tooltip("Rise and duck: the plane must reach y >= high and y <= low once.")]
        public float riseDuckHigh = 4.2f;
        public float riseDuckLow = 2.2f;
        public float riseDuckMaxSeconds = 8f;
        [Tooltip("Phase 2 boss HP.")]
        public float skyHp = 100f;
        [Tooltip("Bar fractions (descending) where the tired boss wakes up: laser stops, +1 heart.")]
        public List<float> wakeThresholds = new() { 0.7f, 0.3f, 0.05f };
        [Tooltip("Slump into the tired pose.")]
        public float tiredSeconds = 1.2f;
        [Tooltip("Wake-up roar.")]
        public float wakeSeconds = 1.2f;
        [Tooltip("The phase 2 beats in order (designer list).")]
        public List<BeatEntry> script = DefaultScript();
        [Tooltip("Gust rows of the extra barrage after a laser window that timed out.")]
        public int timeoutRepeatGusts = 3;

        /// <summary>The bar fraction where the next wake happens, or 0 once every notch is passed.</summary>
        public float NextWakeThreshold(int wakesPassed) => wakesPassed >= 0 && wakesPassed < wakeThresholds.Count ? wakeThresholds[wakesPassed] : 0f;

        public static SkySettings Default() => new();

        /// <summary>GDD §6.4. The final clap + wind frenzy is two entries (clap with overlapNext, then the wind).</summary>
        public static List<BeatEntry> DefaultScript()
        {
            return new List<BeatEntry>
            {
                BeatEntry.Of(BossBeat.Ready),                                   // 0
                BeatEntry.Of(BossBeat.RiseDuck),                                // 1
                BeatEntry.Of(BossBeat.DevilWind, 4),                            // 2
                BeatEntry.Of(BossBeat.LaserWindow),                             // 3  -> 70 %
                BeatEntry.Of(BossBeat.Clap, size: 0),                           // 4  normal
                BeatEntry.Of(BossBeat.DevilWind, 4),                            // 5
                BeatEntry.Of(BossBeat.LaserWindow),                             // 6  -> 30 %
                BeatEntry.Of(BossBeat.Clap, size: 1),                           // 7  large
                BeatEntry.Of(BossBeat.DevilWind, 5),                            // 8
                BeatEntry.Of(BossBeat.LaserWindow),                             // 9  -> 5 %
                BeatEntry.Of(BossBeat.Clap, size: 1, overlapNext: true),        // 10 large ...
                BeatEntry.Of(BossBeat.DevilWind, 3),                            // 11 ... + the final frenzy
                BeatEntry.Of(BossBeat.LaserWindow),                             // 12 -> 0 % "FINISH IT!"
            };
        }
    }

    /// <summary>Devil Wind gust barrages (GDD §6.1).</summary>
    [Serializable]
    public class WindSettings
    {
        [Tooltip("Cheeks puff with the violet glow (scaled by the wind-up scale).")]
        public float windupSeconds = 0.8f;
        [Tooltip("Row spacing of the first barrage; later barrages are faster by the step, down to the minimum.")]
        public float spacingStart = 1.9f;
        public float spacingMin = 1.6f;
        public float spacingStepPerBarrage = 0.1f;
        [Tooltip("Seconds a gust row is visible before it reaches the plane.")]
        public float leadSeconds = 1.5f;
        [Tooltip("Red/green cell frames appear this long before the row arrives.")]
        public float frameLeadSeconds = 1f;
        [Tooltip("Under this spacing, no climb <-> dive back to back.")]
        public float noClimbDiveBelow = 1.8f;
        [Tooltip("Fraction of a cell a gust covers; the rest is a forgiving margin.")]
        [Range(0.5f, 1f)] public float cellFill = 0.9f;

        public float SpacingFor(int barrageIndex) => Mathf.Max(spacingMin, spacingStart - spacingStepPerBarrage * Mathf.Max(0, barrageIndex));

        public static WindSettings Default() => new();
    }

    /// <summary>The tired boss and the auto-firing laser on the infinity path (GDD §6.2).</summary>
    [Serializable]
    public class LaserSettings
    {
        [Tooltip("Seconds between two bolts (8 per second).")]
        public float fireIntervalSeconds = 0.125f;
        [Tooltip("Damage of one bolt on target, per laser window; later windows reuse the last value.")]
        public List<float> boltDamageByWindow = new() { 0.7f, 0.9f, 0.6f, 0.25f };
        [Tooltip("Plane-to-ring distance (m) that counts as on target.")]
        public float hitRadius = 0.95f;
        [Tooltip("Assist: the radius grows from x1 at this window time ...")]
        public float assistStartSeconds = 6f;
        [Tooltip("... to the assist scale at this window time.")]
        public float assistFullSeconds = 12f;
        public float assistScale = 1.4f;
        [Tooltip("Half size of the infinity path of the target ring (x = 1.8 sin wt, y = 1.0 sin 2wt).")]
        public Vector2 pathHalfSize = new(1.8f, 1f);
        public float pathPeriodSeconds = 8f;
        [Tooltip("Centre of the infinity path at the plane's depth.")]
        public Vector2 pathCentre = new(0f, 3.2f);
        [Tooltip("A window this long without reaching the notch ends with a wake anyway.")]
        public float maxWindowSeconds = 18f;
        [Tooltip("Debug multiplier on every bolt's damage.")]
        public float damageScale = 1f;

        public float BoltDamage(int windowIndex)
        {
            if (boltDamageByWindow.Count == 0) return 0.5f * damageScale;
            return boltDamageByWindow[Mathf.Clamp(windowIndex, 0, boltDamageByWindow.Count - 1)] * damageScale;
        }

        public static LaserSettings Default() => new();
    }

    /// <summary>Boss Clap (GDD §6.3).</summary>
    [Serializable]
    public class ClapSettings
    {
        [Tooltip("Inhale with white palms (scaled by the wind-up scale).")]
        public float inhaleSeconds = 0.8f;
        [Tooltip("The plane is blown to the top (vertical steering overridden, sideways free).")]
        public float pushSeconds = 1.2f;
        public float wallRiseSeconds = 0.5f;
        [Tooltip("Clap 1, 2, ...; later claps reuse the last size.")]
        public List<ClapSize> sizes = new() { ClapSize.Of(0.45f, 1.8f), ClapSize.Of(0.7f, 2.2f) };
        [Tooltip("The walls smash together.")]
        public float slamSeconds = 0.22f;
        public float recoverSeconds = 1f;
        [Tooltip("|x| of the spiked walls before they close.")]
        public float wallX = 3.6f;

        public ClapSize SizeFor(int index)
        {
            if (sizes.Count == 0) return ClapSize.Of(0.45f, 1.8f);
            return sizes[Mathf.Clamp(index, 0, sizes.Count - 1)];
        }

        /// <summary>Debug rows: the dive warning of one clap size.</summary>
        public float WarnSeconds(int index) => SizeFor(index).warnSeconds;

        public void SetWarnSeconds(int index, float seconds)
        {
            if (index < 0 || index >= sizes.Count) return;
            var size = sizes[index];
            size.warnSeconds = Mathf.Max(0.1f, seconds);
            sizes[index] = size;
        }

        public static ClapSettings Default() => new();
    }

    /// <summary>Cutscene lengths and slow motion (GDD §5, §7).</summary>
    [Serializable]
    public class CutsceneSettings
    {
        public float introSeconds = 5f;
        public float transformSeconds = 6.5f;
        public float finaleSeconds = 6f;
        public float knockoutSeconds = 3f;
        public float skyRejoinSeconds = 2f;
        [Tooltip("Time scale of the transform/finale slow motion.")]
        public float slowMoScale = 0.35f;
        public float slowMoSeconds = 0.8f;
        public float knockoutSlowMoScale = 0.4f;
        [Tooltip("Height of each letterbox bar in UI pixels (1080p reference).")]
        public int letterboxPixels = 96;

        public float Seconds(BossCutscene cutscene)
        {
            return cutscene switch
            {
                BossCutscene.Intro => introSeconds,
                BossCutscene.Transform => transformSeconds,
                BossCutscene.Finale => finaleSeconds,
                BossCutscene.Knockout => knockoutSeconds,
                BossCutscene.SkyRejoin => skyRejoinSeconds,
                _ => 0f,
            };
        }

        public static CutsceneSettings Default() => new();
    }

    #endregion
}
