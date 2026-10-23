#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Boss Battle tuning (TDD §3.2, numbers = GDD v2). Read at runtime only through
    /// GameConfigsManager.Instance.BossBattleConfig, so the Debug Settings rows edit the one live copy. Sections are public
    /// classes with public fields; EnumDictionary entries are read through the accessors below, which fall back to
    /// BossBattleDefaults when a key is missing or null. Asset: Assets/Configs/Ninjago/BossBattle/BossBattleConfig.asset
    /// (Nex > Ninjago > Boss Battle > Build Config).
    /// </summary>
    [CreateAssetMenu(fileName = "BossBattleConfig", menuName = "Nex/Ninjago/Boss Battle Config")]
    public class BossBattleConfig : ScriptableObject
    {
        [Header("Steering")]
        [SerializeField] SteerSettings steer = SteerSettings.Default();
        [Header("Vehicles")]
        [Tooltip("Road = the car (3 lanes), Sky = the jet (3x3 grid).")]
        [SerializeField] EnumDictionary<BossPhase, ChaseConfig.VehicleSettings> vehicles = BossBattleDefaults.Vehicles();
        [Header("Difficulties")]
        [SerializeField] EnumDictionary<BossDifficulty, DifficultySettings> difficulties = BossBattleDefaults.Difficulties();
        [Header("Hazard Damage")]
        [Tooltip("Hearts lost per hit; 0 or less falls back to the default.")]
        [SerializeField] EnumDictionary<BossHazard, int> hazardDamage = BossBattleDefaults.HazardDamages();
        [Header("Invulnerable Seconds")]
        [Tooltip("I-frames after a hit (the vehicle blinks).")]
        [SerializeField, Range(0f, 4f)] float invulnerableSeconds = 1f;
        [Header("Hit Stop Seconds")]
        [Tooltip("Freeze on a vehicle hit.")]
        [SerializeField, Range(0f, 0.3f)] float hitStopSeconds = 0.06f;
        [Header("Bump Speed Drop")]
        [Tooltip("Fraction of ground speed lost on a hit.")]
        [SerializeField, Range(0f, 0.9f)] float bumpSpeedDrop = 0.35f;
        [Header("Bump Recover Seconds")]
        [SerializeField, Range(0.1f, 4f)] float bumpRecoverSeconds = 1.2f;
        [Header("Heal Per Volley")]
        [Tooltip("Hearts given back by each rocket volley (up to the maximum).")]
        [SerializeField, Range(0, 3)] int healPerVolley = 1;
        [Header("Heal Per Wake")]
        [Tooltip("Hearts given back when the tired boss wakes up.")]
        [SerializeField, Range(0, 3)] int healPerWake = 1;
        [Header("Tracking Lost Seconds")]
        [Tooltip("A calibrated player unseen this long pauses the battle for re-calibration.")]
        [SerializeField, Range(0.5f, 10f)] float trackingLostSeconds = 2f;
        [Header("Road")]
        [SerializeField] RoadSettings road = RoadSettings.Default();
        [Header("Rock Drop")]
        [SerializeField] RockSettings rock = RockSettings.Default();
        [Header("Ground Slam")]
        [SerializeField] SlamSettings slam = SlamSettings.Default();
        [Header("Minion Throw")]
        [SerializeField] MinionSettings minion = MinionSettings.Default();
        [Header("Power-Up")]
        [SerializeField] PowerUpSettings powerUp = PowerUpSettings.Default();
        [Header("Sky")]
        [SerializeField] SkySettings sky = SkySettings.Default();
        [Header("Devil Wind")]
        [SerializeField] WindSettings wind = WindSettings.Default();
        [Header("Laser")]
        [SerializeField] LaserSettings laser = LaserSettings.Default();
        [Header("Clap")]
        [SerializeField] ClapSettings clap = ClapSettings.Default();
        [Header("Cutscenes")]
        [SerializeField] CutsceneSettings cutscenes = CutsceneSettings.Default();
        [Header("Fx")]
        [Tooltip("Sound, visual, shake and light per cue.")]
        [SerializeField] EnumDictionary<BossFxCue, FxEntry> fx = BossBattleDefaults.FxTable();
        [Header("Prompts")]
        [SerializeField] EnumDictionary<BossPromptId, PromptEntry> prompts = BossBattleDefaults.Prompts();
        [Header("Tell Colors")]
        [Tooltip("HDR hand/mouth tell colour per attack icon (>= 2.5 so it blooms).")]
        [SerializeField] EnumDictionary<BossAttackIcon, Color> tellColors = BossBattleDefaults.TellColors();

        #region Sections

        public SteerSettings Steer => steer;
        public RoadSettings Road => road;
        public RockSettings Rock => rock;
        public SlamSettings Slam => slam;
        public MinionSettings Minion => minion;
        public PowerUpSettings PowerUp => powerUp;
        public SkySettings Sky => sky;
        public WindSettings Wind => wind;
        public LaserSettings Laser => laser;
        public ClapSettings Clap => clap;
        public CutsceneSettings Cutscenes => cutscenes;

        #endregion

        #region Scalars

        public float InvulnerableSeconds { get => invulnerableSeconds; set => invulnerableSeconds = Mathf.Max(0f, value); }
        public float HitStopSeconds { get => hitStopSeconds; set => hitStopSeconds = Mathf.Clamp(value, 0f, 0.3f); }
        public float BumpSpeedDrop { get => bumpSpeedDrop; set => bumpSpeedDrop = Mathf.Clamp(value, 0f, 0.9f); }
        public float BumpRecoverSeconds { get => bumpRecoverSeconds; set => bumpRecoverSeconds = Mathf.Max(0.1f, value); }
        public int HealPerVolley { get => healPerVolley; set => healPerVolley = Mathf.Max(0, value); }
        public int HealPerWake { get => healPerWake; set => healPerWake = Mathf.Max(0, value); }
        public float TrackingLostSeconds { get => trackingLostSeconds; set => trackingLostSeconds = Mathf.Max(0.5f, value); }

        /// <summary>Phase 1 HP: the volley table's sum up to the phase-2 pickup (230 with the defaults).</summary>
        public float RoadMaxHp => powerUp.RoadHp;
        public float SkyMaxHp => sky.skyHp;

        #endregion

        #region Dictionaries

        public ChaseConfig.VehicleSettings Vehicle(BossPhase phase)
        {
            if (vehicles.TryGetValue(phase, out var settings) && settings != null) return settings;
            return vehicles[phase] = BossBattleDefaults.Vehicle(phase);
        }

        public DifficultySettings Difficulty(BossDifficulty difficulty)
        {
            if (difficulties.TryGetValue(difficulty, out var settings) && settings != null) return settings;
            return difficulties[difficulty] = DifficultySettings.Default(difficulty);
        }

        public int HazardDamage(BossHazard hazard)
        {
            return hazardDamage.TryGetValue(hazard, out var damage) && damage > 0 ? damage : BossBattleDefaults.HazardDamage(hazard);
        }

        /// <summary>False when the cue has no entry (play nothing).</summary>
        public bool TryGetFx(BossFxCue cue, out FxEntry entry)
        {
            if (fx.TryGetValue(cue, out var value) && value != null)
            {
                entry = value;
                return true;
            }

            entry = null!;
            return false;
        }

        public FxEntry? Fx(BossFxCue cue) => fx.TryGetValue(cue, out var value) ? value : null;

        public PromptEntry Prompt(BossPromptId id)
        {
            if (prompts.TryGetValue(id, out var entry) && entry != null) return entry;
            return prompts[id] = BossBattleDefaults.Prompt(id);
        }

        public Color TellColor(BossAttackIcon icon)
        {
            return tellColors.TryGetValue(icon, out var color) && (color.r > 0f || color.g > 0f || color.b > 0f)
                ? color
                : BossBattleDefaults.TellColor(icon);
        }

        #endregion

        #region Debug Rows

        /// <summary>Debug row: Normal hearts.</summary>
        public int NormalHearts
        {
            get => Difficulty(BossDifficulty.Normal).hearts;
            set => Difficulty(BossDifficulty.Normal).hearts = Mathf.Max(1, value);
        }

        /// <summary>Debug row: the shortest slam row spacing; setting it gives every slam that spacing.</summary>
        public float SlamRowSpacing
        {
            get
            {
                var min = float.MaxValue;
                foreach (var spacing in slam.rowSpacingBySlam) min = Mathf.Min(min, spacing);
                return slam.rowSpacingBySlam.Count == 0 ? slam.RowSpacing(0) : min;
            }
            set => slam.SetAllRowSpacing(Mathf.Max(0.5f, value));
        }

        #endregion

        #region Defaults

        /// <summary>Every value back to the GDD defaults (the builder's "reset" and the inspector's Reset).</summary>
        public void ResetToDefaults() => Reset();

        void Reset()
        {
            steer = SteerSettings.Default();
            vehicles = BossBattleDefaults.Vehicles();
            difficulties = BossBattleDefaults.Difficulties();
            hazardDamage = BossBattleDefaults.HazardDamages();
            invulnerableSeconds = 1f;
            hitStopSeconds = 0.06f;
            bumpSpeedDrop = 0.35f;
            bumpRecoverSeconds = 1.2f;
            healPerVolley = 1;
            healPerWake = 1;
            trackingLostSeconds = 2f;
            road = RoadSettings.Default();
            rock = RockSettings.Default();
            slam = SlamSettings.Default();
            minion = MinionSettings.Default();
            powerUp = PowerUpSettings.Default();
            sky = SkySettings.Default();
            wind = WindSettings.Default();
            laser = LaserSettings.Default();
            clap = ClapSettings.Default();
            cutscenes = CutsceneSettings.Default();
            fx = BossBattleDefaults.FxTable();
            prompts = BossBattleDefaults.Prompts();
            tellColors = BossBattleDefaults.TellColors();
        }

        /// <summary>
        /// Fills null sections, empty lists and missing/null dictionary entries from the defaults, keeping every value
        /// that is set (the builder's "update"). Returns how many values were filled.
        /// </summary>
        public int FillMissingDefaults()
        {
            var filled = 0;
            steer ??= Count(SteerSettings.Default(), ref filled);
            road ??= Count(RoadSettings.Default(), ref filled);
            rock ??= Count(RockSettings.Default(), ref filled);
            slam ??= Count(SlamSettings.Default(), ref filled);
            minion ??= Count(MinionSettings.Default(), ref filled);
            powerUp ??= Count(PowerUpSettings.Default(), ref filled);
            sky ??= Count(SkySettings.Default(), ref filled);
            wind ??= Count(WindSettings.Default(), ref filled);
            laser ??= Count(LaserSettings.Default(), ref filled);
            clap ??= Count(ClapSettings.Default(), ref filled);
            cutscenes ??= Count(CutsceneSettings.Default(), ref filled);
            filled += FillList(ref road.script, RoadSettings.DefaultScript());
            filled += FillList(ref sky.script, SkySettings.DefaultScript());
            filled += FillList(ref slam.rowSpacingBySlam, SlamSettings.Default().rowSpacingBySlam);
            filled += FillList(ref slam.firstMasks, SlamSettings.Default().firstMasks);
            filled += FillList(ref powerUp.volleys, PowerUpSettings.Default().volleys);
            filled += FillList(ref sky.wakeThresholds, SkySettings.Default().wakeThresholds);
            filled += FillList(ref laser.boltDamageByWindow, LaserSettings.Default().boltDamageByWindow);
            filled += FillList(ref clap.sizes, ClapSettings.Default().sizes);

            vehicles ??= new EnumDictionary<BossPhase, ChaseConfig.VehicleSettings>();
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase)))
            {
                if (vehicles.TryGetValue(phase, out var value) && value != null) continue;
                vehicles[phase] = BossBattleDefaults.Vehicle(phase);
                filled++;
            }

            difficulties ??= new EnumDictionary<BossDifficulty, DifficultySettings>();
            foreach (BossDifficulty difficulty in Enum.GetValues(typeof(BossDifficulty)))
            {
                if (difficulties.TryGetValue(difficulty, out var value) && value != null) continue;
                difficulties[difficulty] = DifficultySettings.Default(difficulty);
                filled++;
            }

            hazardDamage ??= new EnumDictionary<BossHazard, int>();
            foreach (BossHazard hazard in Enum.GetValues(typeof(BossHazard)))
            {
                if (hazardDamage.TryGetValue(hazard, out var value) && value > 0) continue;
                hazardDamage[hazard] = BossBattleDefaults.HazardDamage(hazard);
                filled++;
            }

            fx ??= new EnumDictionary<BossFxCue, FxEntry>();
            foreach (BossFxCue cue in Enum.GetValues(typeof(BossFxCue)))
            {
                if (fx.TryGetValue(cue, out var value) && value != null) continue;
                fx[cue] = BossBattleDefaults.Fx(cue);
                filled++;
            }

            prompts ??= new EnumDictionary<BossPromptId, PromptEntry>();
            foreach (BossPromptId id in Enum.GetValues(typeof(BossPromptId)))
            {
                if (prompts.TryGetValue(id, out var value) && value != null && !string.IsNullOrEmpty(value.key)) continue;
                prompts[id] = BossBattleDefaults.Prompt(id);
                filled++;
            }

            tellColors ??= new EnumDictionary<BossAttackIcon, Color>();
            foreach (BossAttackIcon icon in Enum.GetValues(typeof(BossAttackIcon)))
            {
                if (icon == BossAttackIcon.None) continue;
                if (tellColors.TryGetValue(icon, out var value) && (value.r > 0f || value.g > 0f || value.b > 0f)) continue;
                tellColors[icon] = BossBattleDefaults.TellColor(icon);
                filled++;
            }

            return filled;
        }

        static T Count<T>(T value, ref int filled)
        {
            filled++;
            return value;
        }

        static int FillList<T>(ref List<T> list, List<T> defaults)
        {
            if (list != null && list.Count > 0) return 0;
            list = defaults;
            return 1;
        }

        #endregion
    }
}
