#nullable enable

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

using Nex.BilliardRogue;
using Nex.Dev.Attributes;
using Nex.Ninjago;

namespace Nex
{
    // Rows of the DebugSettingsPanel (public fields, properties and void methods with primitive parameters).
    // Keep it a flat class: no const/static fields, no nested collections. Release builds never load it.
    public class DebugSettings
    {
        [DebugOrder(0)] public bool enableDebugPrinter = false;

        // Billiard Rogue cheats, flow and render toggles.
        [DebugOrder(10), Description("Cheat: God Mode")] public bool godMode;
        [DebugOrder(11), Description("Cheat: Infinite Balls")] public bool infiniteBalls;
        [DebugOrder(12), Description("Cheat: Unlock All Balls")] public bool unlockAllBalls;
        // Control lab: god mode, so the run never ends; turns still end after BalanceRules.shotsPerTurn balls and the
        // enemies still advance. Off in every build (the control demo too), so the player loses HP as in the game.
        [DebugOrder(13), Description("Cheat: Practice Mode (god mode, the run never ends)")] public bool practiceMode;

        [DebugOrder(20), Description("Flow: Auto Aim Bot")] public bool autoAimBot;
        [DebugOrder(21), Description("Flow: Skip Calibration (Editor only)")] public bool skipCalibration;
        [DebugOrder(22), Description("Flow: Fast Enemy Phase")] public bool fastEnemyPhase;
        [DebugOrder(23), Description("Input: Force Mouse/Keyboard")] public bool forceDebugInput;

        // Control lab live tuning: ControlConfig value x scale (the asset stays untouched), read every frame.
        [DebugOrder(24), Description("Input: Strike Speed x (higher = harder thrust)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float strikeSpeedScale = 1f;
        [DebugOrder(25), Description("Input: Contact Distance x (higher = paws may stay apart)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float contactDistanceScale = 1f;
        [DebugOrder(26), Description("Input: Aim Smoothing x (higher = steadier, more lag)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float aimSmoothingScale = 1f;
        [DebugOrder(27), Description("Input: Launch Range x (lower = less left-paw travel)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float launchRangeScale = 1f;

        [DebugOrder(30), Description("Run: Force Start Stage (-1 = off, 0..11)"), NumericSteps(IntSteps = 1, IntMin = -1, IntMax = 11)]
        public int forceStartStage = -1;
        [DebugOrder(31), Description("Run: Force Reward Ball (-1 = off, 0..11 = BallType)"), NumericSteps(IntSteps = 1, IntMin = -1, IntMax = 11)]
        public int forceRewardBall = -1;
        [DebugOrder(32), Description("Run: Fixed Seed (0 = random)")] public int fixedSeed;
        [DebugOrder(33), Description("Run: Force POWER while balls fly (-1 = off, 0..1)"), NumericSteps(Steps = 0.05, Min = -1, Max = 1)]
        public float forceHype = -1f;

        [DebugOrder(40), Description("Debug: Show Sim Debug")] public bool showSimDebug;
        // Hidden by default in every build, the control demo included (GDD v2 §6). Renamed from showControlDebug so a
        // value saved while the demo defaulted it on is dropped instead of keeping the readout up.
        [DebugOrder(41), Description("Debug: Show Control Readout")] public bool showControlReadout;

        [DebugOrder(50), Description("Render: Disable Pixelation")] public bool disablePixelation;
        [DebugOrder(51), Description("Render: Disable Tilt-Shift")] public bool disableTiltShift;
        [DebugOrder(52), Description("Render: Disable Bloom")] public bool disableBloom;
        [DebugOrder(53), Description("Render: Quality Tier (0 = auto by GPU, 1 = full, 2 = low)"), NumericSteps(IntSteps = 1, IntMin = 0, IntMax = 2)]
        public int renderTier;
        // [Perf] line every few seconds (FrameTimingLogger): fps, CPU main / render thread and GPU ms, GC, draw counts.
#if BR_CONTROL_DEMO
        [DebugOrder(54), Description("Debug: Log Frame Timing ([Perf] in logcat)")] public bool logFrameTiming = true;
#else
        [DebugOrder(54), Description("Debug: Log Frame Timing ([Perf] in logcat)")] public bool logFrameTiming;
#endif
        // GPU A/B on the device with the [Perf] gpu ms: the world camera's cost is what Freeze World removes (the last
        // world frame stays on screen), the shadow pass what Disable Shadows removes.
        [DebugOrder(55), Description("Render: Disable Shadows")] public bool disableShadows;
        [DebugOrder(56), Description("Render: Freeze World (skip the world camera, GPU A/B)")] public bool freezeWorld;

        // Method rows close the panel first, then run; gameplay registers the handlers in DebugHooks.
        [DebugOrder(60), Description("Run: Kill All Enemies")]
        public void KillAllEnemies()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.KillAll()}");
#endif
        }

        [DebugOrder(61), Description("Run: Clear Stage")]
        public void ClearStage()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.ClearStage()}");
#endif
        }

        [DebugOrder(62), Description("Run: Add Every Ball")]
        public void AddEveryBall()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.AddEveryBall()}");
#endif
        }

        [DebugOrder(63), Description("Save: Reset Meta Progress"), SaveBeforeInvoking]
        public void ResetMetaProgress() => PlayerDataManager.Instance.ResetMetaProgress();

        [DebugOrder(64), Description("Save: Clear Saved Run"), SaveBeforeInvoking]
        public void ClearSavedRun() => PlayerDataManager.Instance.ClearRun();

        // Ninjago playtest rows. The properties edit the config assets directly (session only, never saved with these
        // settings); keep good values by copying them into the assets under Assets/Configs/Ninjago.
        [DebugOrder(-100), Description("Ninja: Simulated Body (keys A/D W/S Space, J/L I/K RightShift, mouse, punch Q/E U/O)")]
        public bool ninjaSimulatedBody;

        // Hand cursor games (Stone Kick, Earth Seal): same rule as the rows above, the config assets are edited live.
        [DebugOrder(-200), Description("Cursor: Smoothing Min Cutoff (Hz)"), NumericSteps(Steps = 0.1, Min = 0.1, Max = 10), ES3NonSerializable]
        public float CursorMinCutoff { get => Cursor.SmoothingMinCutoffHz; set => Cursor.SmoothingMinCutoffHz = value; }
        [DebugOrder(-199), Description("Cursor: Smoothing Speed Response"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float CursorSpeedResponse { get => Cursor.SmoothingSpeedResponse; set => Cursor.SmoothingSpeedResponse = value; }
        [DebugOrder(-198), Description("Cursor: Reach Height (in)"), NumericSteps(Steps = 1, Min = 10, Max = 60), ES3NonSerializable]
        public float CursorReachHeight { get => Cursor.ReachHeightInches; set => Cursor.ReachHeightInches = value; }
        [DebugOrder(-197), Description("Cursor: Reach Center Above Chest (in)"), NumericSteps(Steps = 1, Min = -10, Max = 20), ES3NonSerializable]
        public float CursorReachCenter { get => Cursor.ReachCenterAboveChestInches; set => Cursor.ReachCenterAboveChestInches = value; }
        [DebugOrder(-196), Description("Cursor: Body Hand Fallback"), ES3NonSerializable]
        public bool CursorBodyFallback { get => Cursor.UseBodyHandFallback; set => Cursor.UseBodyHandFallback = value; }
        [DebugOrder(-195), Description("Setup: Hand Check Hold (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 3), ES3NonSerializable]
        public float SetupHandCheck { get => Players.HandCheckSeconds; set => Players.HandCheckSeconds = value; }

        [DebugOrder(-190), Description("Kick: Slash Speed (in/s)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float KickSlashSpeed { get => StoneKick.SlashSpeedInchesPerSecond; set => StoneKick.SlashSpeedInchesPerSecond = value; }
        [DebugOrder(-191), Description("Kick: Slash Max Cross Time (s)"), NumericSteps(Steps = 0.05, Min = 0.05, Max = 2), ES3NonSerializable]
        public float KickSlashCross { get => StoneKick.SlashMaxCrossSeconds; set => StoneKick.SlashMaxCrossSeconds = value; }
        [DebugOrder(-189), Description("Kick: Long Hang (s)"), NumericSteps(Steps = 0.25, Min = 0.5, Max = 10), ES3NonSerializable]
        public float KickLongHang { get => StoneKick.LongHangSeconds; set => StoneKick.LongHangSeconds = value; }
        [DebugOrder(-188), Description("Kick: Long Hang Throws"), NumericSteps(IntSteps = 1, IntMin = 0, IntMax = 12), ES3NonSerializable]
        public int KickLongHangCount { get => StoneKick.LongHangCount; set => StoneKick.LongHangCount = value; }
        [DebugOrder(-187), Description("Kick: Hang (s)"), NumericSteps(Steps = 0.25, Min = 0.3, Max = 8), ES3NonSerializable]
        public float KickHang { get => StoneKick.HangSeconds; set => StoneKick.HangSeconds = value; }
        [DebugOrder(-186), Description("Kick: Throw Flight (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 4), ES3NonSerializable]
        public float KickThrowFlight { get => StoneKick.ThrowSeconds; set => StoneKick.ThrowSeconds = value; }
        [DebugOrder(-185), Description("Kick: Slash Frenzy (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 6), ES3NonSerializable]
        public float KickSlashFrenzy { get => StoneKick.SlashFrenzySeconds; set => StoneKick.SlashFrenzySeconds = value; }
        [DebugOrder(-176), Description("Kick: Max Stones Per Rock"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 12), ES3NonSerializable]
        public int KickMaxStones { get => StoneKick.MaxStones; set => StoneKick.MaxStones = value; }
        [DebugOrder(-184), Description("Kick: Kick Window (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 6), ES3NonSerializable]
        public float KickWindow { get => StoneKick.KickWindowSeconds; set => StoneKick.KickWindowSeconds = value; }
        [DebugOrder(-183), Description("Kick: Knee Lift (fraction of thigh)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 0.95), ES3NonSerializable]
        public float KickKneeLift { get => StoneKick.KneeLiftRatio; set => StoneKick.KneeLiftRatio = value; }
        [DebugOrder(-182), Description("Kick: Knee Release (fraction of lift)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 0.9), ES3NonSerializable]
        public float KickKneeRelease { get => StoneKick.KneeReleaseRatio; set => StoneKick.KneeReleaseRatio = value; }
        [DebugOrder(-181), Description("Kick: Knee Rise Max (s)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 2), ES3NonSerializable]
        public float KickRiseMax { get => StoneKick.KickRiseMaxSeconds; set => StoneKick.KickRiseMaxSeconds = value; }
        [DebugOrder(-180), Description("Kick: Kick Cooldown (s)"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float KickCooldown { get => StoneKick.KickCooldownSeconds; set => StoneKick.KickCooldownSeconds = value; }
        [DebugOrder(-179), Description("Kick: Hearts"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 9), ES3NonSerializable]
        public int KickHearts { get => StoneKick.Hearts; set => StoneKick.Hearts = value; }
        [DebugOrder(-178), Description("Kick: Throws Per Player"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 12), ES3NonSerializable]
        public int KickThrows { get => StoneKick.ThrowsPerPlayer; set => StoneKick.ThrowsPerPlayer = value; }
        [DebugOrder(-177), Description("Kick: Rest Between Throws (s)"), NumericSteps(Steps = 0.25, Min = 0.3, Max = 8), ES3NonSerializable]
        public float KickRest { get => StoneKick.RestBetweenThrowsSeconds; set => StoneKick.RestBetweenThrowsSeconds = value; }

        [DebugOrder(-170), Description("Seal: Hearts"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 12), ES3NonSerializable]
        public int SealHearts { get => EarthSeal.Hearts; set => EarthSeal.Hearts = value; }
        [DebugOrder(-169), Description("Seal: Seal Drain (s, full to empty)"), NumericSteps(Steps = 0.25, Min = 0.5, Max = 20), ES3NonSerializable]
        public float SealDrain { get => EarthSeal.SealDrainSeconds; set => EarthSeal.SealDrainSeconds = value; }
        [DebugOrder(-160), Description("Seal W1: Grid Size"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 6), ES3NonSerializable]
        public int SealW1Grid { get => W1.gridSize; set => W1.gridSize = value; }
        [DebugOrder(-159), Description("Seal W1: Crack Chance"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float SealW1Chance { get => W1.crackChance; set => W1.crackChance = value; }
        [DebugOrder(-158), Description("Seal W1: Max Cracks"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 6), ES3NonSerializable]
        public int SealW1MaxCracks { get => W1.maxCracks; set => W1.maxCracks = value; }
        [DebugOrder(-157), Description("Seal W1: Warning (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 8), ES3NonSerializable]
        public float SealW1Warning { get => W1.warningSeconds; set => W1.warningSeconds = value; }
        [DebugOrder(-156), Description("Seal W1: Seal Time (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 8), ES3NonSerializable]
        public float SealW1Seal { get => W1.sealSeconds; set => W1.sealSeconds = value; }
        [DebugOrder(-155), Description("Seal W1: Breakthrough (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 10), ES3NonSerializable]
        public float SealW1Breakthrough { get => W1.breakthroughSeconds; set => W1.breakthroughSeconds = value; }
        [DebugOrder(-150), Description("Seal W2: Grid Size"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 6), ES3NonSerializable]
        public int SealW2Grid { get => W2.gridSize; set => W2.gridSize = value; }
        [DebugOrder(-149), Description("Seal W2: Crack Chance"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float SealW2Chance { get => W2.crackChance; set => W2.crackChance = value; }
        [DebugOrder(-148), Description("Seal W2: Max Cracks"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 6), ES3NonSerializable]
        public int SealW2MaxCracks { get => W2.maxCracks; set => W2.maxCracks = value; }
        [DebugOrder(-147), Description("Seal W2: Warning (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 8), ES3NonSerializable]
        public float SealW2Warning { get => W2.warningSeconds; set => W2.warningSeconds = value; }
        [DebugOrder(-146), Description("Seal W2: Seal Time (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 8), ES3NonSerializable]
        public float SealW2Seal { get => W2.sealSeconds; set => W2.sealSeconds = value; }
        [DebugOrder(-145), Description("Seal W2: Breakthrough (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 10), ES3NonSerializable]
        public float SealW2Breakthrough { get => W2.breakthroughSeconds; set => W2.breakthroughSeconds = value; }
        [DebugOrder(-140), Description("Seal W3: Grid Size"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 6), ES3NonSerializable]
        public int SealW3Grid { get => W3.gridSize; set => W3.gridSize = value; }
        [DebugOrder(-139), Description("Seal W3: Crack Chance"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float SealW3Chance { get => W3.crackChance; set => W3.crackChance = value; }
        [DebugOrder(-138), Description("Seal W3: Max Cracks"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 6), ES3NonSerializable]
        public int SealW3MaxCracks { get => W3.maxCracks; set => W3.maxCracks = value; }
        [DebugOrder(-137), Description("Seal W3: Warning (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 8), ES3NonSerializable]
        public float SealW3Warning { get => W3.warningSeconds; set => W3.warningSeconds = value; }
        [DebugOrder(-136), Description("Seal W3: Seal Time (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 8), ES3NonSerializable]
        public float SealW3Seal { get => W3.sealSeconds; set => W3.sealSeconds = value; }
        [DebugOrder(-135), Description("Seal W3: Breakthrough (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 10), ES3NonSerializable]
        public float SealW3Breakthrough { get => W3.breakthroughSeconds; set => W3.breakthroughSeconds = value; }
        [DebugOrder(-130), Description("Seal W4: Grid Size"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 6), ES3NonSerializable]
        public int SealW4Grid { get => W4.gridSize; set => W4.gridSize = value; }
        [DebugOrder(-129), Description("Seal W4: Crack Chance"), NumericSteps(Steps = 0.01, Min = 0, Max = 1), ES3NonSerializable]
        public float SealW4Chance { get => W4.crackChance; set => W4.crackChance = value; }
        [DebugOrder(-128), Description("Seal W4: Max Cracks"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 6), ES3NonSerializable]
        public int SealW4MaxCracks { get => W4.maxCracks; set => W4.maxCracks = value; }
        [DebugOrder(-127), Description("Seal W4: Warning (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 8), ES3NonSerializable]
        public float SealW4Warning { get => W4.warningSeconds; set => W4.warningSeconds = value; }
        [DebugOrder(-126), Description("Seal W4: Seal Time (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 8), ES3NonSerializable]
        public float SealW4Seal { get => W4.sealSeconds; set => W4.sealSeconds = value; }
        [DebugOrder(-125), Description("Seal W4: Breakthrough (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 10), ES3NonSerializable]
        public float SealW4Breakthrough { get => W4.breakthroughSeconds; set => W4.breakthroughSeconds = value; }

        // Three Weapons: the config asset is edited live, like the rows below.
        [DebugOrder(-500), Description("Weapons: Slash Speed (in/s)"), NumericSteps(Steps = 5, Min = 5, Max = 200), ES3NonSerializable]
        public float WeaponsSlashSpeed { get => ThreeWeapons.SlashSpeed; set => ThreeWeapons.SlashSpeed = value; }
        [DebugOrder(-499), Description("Weapons: Slash Cooldown (s)"), NumericSteps(Steps = 0.01, Min = 0, Max = 0.5), ES3NonSerializable]
        public float WeaponsSlashCooldown { get => ThreeWeapons.SlashCooldownSeconds; set => ThreeWeapons.SlashCooldownSeconds = value; }
        [DebugOrder(-498), Description("Weapons: Slashes To Kill"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 40), ES3NonSerializable]
        public int WeaponsSlashesToKill { get => ThreeWeapons.SlashesToKill; set => ThreeWeapons.SlashesToKill = value; }
        [DebugOrder(-497), Description("Weapons: Spin Window (s)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1.5), ES3NonSerializable]
        public float WeaponsSpinWindow { get => ThreeWeapons.SpinWindowSeconds; set => ThreeWeapons.SpinWindowSeconds = value; }
        [DebugOrder(-496), Description("Weapons: Spin Path Length (in)"), NumericSteps(Steps = 1, Min = 4, Max = 80), ES3NonSerializable]
        public float WeaponsSpinPath { get => ThreeWeapons.SpinPathInches; set => ThreeWeapons.SpinPathInches = value; }
        [DebugOrder(-495), Description("Weapons: Spin Radius (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 20), ES3NonSerializable]
        public float WeaponsSpinRadius { get => ThreeWeapons.SpinRadiusInches; set => ThreeWeapons.SpinRadiusInches = value; }
        [DebugOrder(-494), Description("Weapons: Throws To Kill"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 30), ES3NonSerializable]
        public int WeaponsThrowsToKill { get => ThreeWeapons.ThrowsToKill; set => ThreeWeapons.ThrowsToKill = value; }
        [DebugOrder(-493), Description("Weapons: Card Hold (s)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 3), ES3NonSerializable]
        public float WeaponsHold { get => ThreeWeapons.HoldSeconds; set => ThreeWeapons.HoldSeconds = value; }

        // One Punch: the config asset is edited live, like the rows below.
        [DebugOrder(-600), Description("Punch: Extension Speed (in/s)"), NumericSteps(Steps = 1, Min = 5, Max = 200), ES3NonSerializable]
        public float PunchExtensionSpeed { get => OnePunch.ExtensionSpeed; set => OnePunch.ExtensionSpeed = value; }
        [DebugOrder(-599), Description("Punch: Chest-Front Window (in)"), NumericSteps(Steps = 0.5, Min = 2, Max = 20), ES3NonSerializable]
        public float PunchChestFrontWindow { get => OnePunch.ChestFrontWindow; set => OnePunch.ChestFrontWindow = value; }
        [DebugOrder(-598), Description("Punch: Hand-Scale Speed (per s)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 5), ES3NonSerializable]
        public float PunchHandScaleSpeed { get => OnePunch.HandScaleSpeed; set => OnePunch.HandScaleSpeed = value; }
        [DebugOrder(-597), Description("Punch: Side-Slash Rejection (ratio)"), NumericSteps(Steps = 0.05, Min = 0, Max = 1), ES3NonSerializable]
        public float PunchSideSlashRatio { get => OnePunch.SideSlashLengthRatio; set => OnePunch.SideSlashLengthRatio = value; }
        [DebugOrder(-596), Description("Punch: Punch Cooldown (s)"), NumericSteps(Steps = 0.05, Min = 0, Max = 1.5), ES3NonSerializable]
        public float PunchCooldown { get => OnePunch.PunchCooldownSeconds; set => OnePunch.PunchCooldownSeconds = value; }
        [DebugOrder(-595), Description("Punch: Monster Speed (m/s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 10), ES3NonSerializable]
        public float PunchMonsterSpeed { get => OnePunch.MonsterSpeed; set => OnePunch.MonsterSpeed = value; }
        [DebugOrder(-594), Description("Punch: Grab Range (m)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 5), ES3NonSerializable]
        public float PunchGrabRange { get => OnePunch.GrabRange; set => OnePunch.GrabRange = value; }
        [DebugOrder(-593), Description("Punch: Wiggle Path Length (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 40), ES3NonSerializable]
        public float PunchWigglePath { get => OnePunch.WigglePathInches; set => OnePunch.WigglePathInches = value; }
        [DebugOrder(-592), Description("Punch: Wiggle Radius (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 20), ES3NonSerializable]
        public float PunchWiggleRadius { get => OnePunch.WiggleRadiusInches; set => OnePunch.WiggleRadiusInches = value; }
        [DebugOrder(-591), Description("Punch: Wiggle Window (s)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1.5), ES3NonSerializable]
        public float PunchWiggleWindow { get => OnePunch.WiggleWindowSeconds; set => OnePunch.WiggleWindowSeconds = value; }
        [DebugOrder(-590), Description("Punch: Health Drain (per s)"), NumericSteps(Steps = 0.1, Min = 0.1, Max = 5), ES3NonSerializable]
        public float PunchDrain { get => OnePunch.DrainPerSecond; set => OnePunch.DrainPerSecond = value; }
        [DebugOrder(-589), Description("Punch: Starting Health"), NumericSteps(Steps = 0.5, Min = 1, Max = 30), ES3NonSerializable]
        public float PunchStartingHealth { get => OnePunch.StartingHealth; set => OnePunch.StartingHealth = value; }

        // Guard: the config asset is edited live, like the rows below.
        [DebugOrder(-700), Description("Guard: Hands Above Shoulders (in)"), NumericSteps(Steps = 0.5, Min = -6, Max = 12), ES3NonSerializable]
        public float GuardAboveShoulder { get => Guard.MinAboveShoulder; set => Guard.MinAboveShoulder = value; }
        [DebugOrder(-699), Description("Guard: Hands Past Shoulders (in)"), NumericSteps(Steps = 0.5, Min = 0, Max = 12), ES3NonSerializable]
        public float GuardOutsideShoulder { get => Guard.MaxOutsideShoulder; set => Guard.MaxOutsideShoulder = value; }
        [DebugOrder(-698), Description("Guard: Guard Lasts (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 5), ES3NonSerializable]
        public float GuardFresh { get => Guard.FreshSeconds; set => Guard.FreshSeconds = value; }
        [DebugOrder(-697), Description("Guard: Wind-Up (s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 4), ES3NonSerializable]
        public float GuardWindUp { get => Guard.WindUpSeconds; set => Guard.WindUpSeconds = value; }
        [DebugOrder(-696), Description("Guard: Brick Flight (s)"), NumericSteps(Steps = 0.1, Min = 0.4, Max = 4), ES3NonSerializable]
        public float GuardFlight { get => Guard.FlightSeconds; set => Guard.FlightSeconds = value; }
        [DebugOrder(-695), Description("Guard: Throws Per Player"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 30), ES3NonSerializable]
        public int GuardThrows { get => Guard.ThrowsPerPlayer; set => Guard.ThrowsPerPlayer = value; }

        // Call: the config asset is edited live, like the rows below.
        [DebugOrder(-800), Description("Call: Hand Above Head (in)"), NumericSteps(Steps = 0.5, Min = -6, Max = 12), ES3NonSerializable]
        public float CallAboveHead { get => Call.AboveHead; set => Call.AboveHead = value; }
        [DebugOrder(-799), Description("Call: Try Line Above Shoulders (in)"), NumericSteps(Steps = 0.5, Min = 0, Max = 12), ES3NonSerializable]
        public float CallTryLine { get => Call.TryAboveShoulder; set => Call.TryAboveShoulder = value; }
        [DebugOrder(-798), Description("Call: Teammate Cooldown (s)"), NumericSteps(Steps = 0.25, Min = 0, Max = 8), ES3NonSerializable]
        public float CallCooldown { get => Call.CooldownSeconds; set => Call.CooldownSeconds = value; }
        [DebugOrder(-797), Description("Call: Slam Radius (m)"), NumericSteps(Steps = 0.25, Min = 0.5, Max = 12), ES3NonSerializable]
        public float CallSlamRadius { get => Call.SlamRadius; set => Call.SlamRadius = value; }
        [DebugOrder(-796), Description("Call: Goblin Speed (m/s)"), NumericSteps(Steps = 0.1, Min = 0.3, Max = 6), ES3NonSerializable]
        public float CallGoblinSpeed { get => Call.GoblinSpeed; set => Call.GoblinSpeed = value; }
        [DebugOrder(-795), Description("Call: Goblins In A Pile"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 8), ES3NonSerializable]
        public int CallPileSize { get => Call.PileSize; set => Call.PileSize = value; }
        [DebugOrder(-794), Description("Call: Calm Hand Speed (in/s)"), NumericSteps(Steps = 1, Min = 2, Max = 80), ES3NonSerializable]
        public float CallCalmSpeed { get => Call.CalmHandSpeed; set => Call.CalmHandSpeed = value; }

        // Build: the config asset is edited live, like the rows below.
        [DebugOrder(-900), Description("Build: Hands Together (shoulder widths)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1), ES3NonSerializable]
        public float BuildTogether { get => Build.TogetherRatio; set => Build.TogetherRatio = value; }
        [DebugOrder(-899), Description("Build: Hands Apart (shoulder widths)"), NumericSteps(Steps = 0.05, Min = 0.3, Max = 2), ES3NonSerializable]
        public float BuildApart { get => Build.ApartRatio; set => Build.ApartRatio = value; }
        [DebugOrder(-898), Description("Build: Near Clap (shoulder widths)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 1.5), ES3NonSerializable]
        public float BuildNear { get => Build.NearRatio; set => Build.NearRatio = value; }
        [DebugOrder(-897), Description("Build: Shortest Gap Between Claps (s)"), NumericSteps(Steps = 0.05, Min = 0, Max = 1), ES3NonSerializable]
        public float BuildMinClap { get => Build.MinClapSeconds; set => Build.MinClapSeconds = value; }
        [DebugOrder(-896), Description("Build: Bridge Rows (next run)"), NumericSteps(IntSteps = 1, IntMin = 2, IntMax = 30), ES3NonSerializable]
        public int BuildRows { get => Build.Rows; set => Build.Rows = value; }
        [DebugOrder(-895), Description("Build: Time Limit (s)"), NumericSteps(Steps = 5, Min = 20, Max = 300), ES3NonSerializable]
        public float BuildTimeLimit { get => Build.TimeLimitSeconds; set => Build.TimeLimitSeconds = value; }

        // Crouch Run: the config asset is edited live, like the rows below.
        [DebugOrder(-400), Description("Crouch: Crouch Threshold (in below standing)"), NumericSteps(Steps = 0.5, Min = 1, Max = 20), ES3NonSerializable]
        public float CrouchThreshold { get => CrouchRun.CrouchDropInches; set => CrouchRun.CrouchDropInches = value; }
        [DebugOrder(-399), Description("Crouch: Run Speed (m/s)"), NumericSteps(Steps = 0.25, Min = 1, Max = 20), ES3NonSerializable]
        public float CrouchRunSpeed { get => CrouchRun.RunSpeed; set => CrouchRun.RunSpeed = value; }
        [DebugOrder(-398), Description("Crouch: Crouch Speed (m/s)"), NumericSteps(Steps = 0.25, Min = 0.25, Max = 10), ES3NonSerializable]
        public float CrouchWalkSpeed { get => CrouchRun.CrouchSpeed; set => CrouchRun.CrouchSpeed = value; }
        [DebugOrder(-397), Description("Crouch: Heard Penalty (s)"), NumericSteps(Steps = 0.5, Min = 0, Max = 15), ES3NonSerializable]
        public float CrouchHeardPenalty { get => CrouchRun.HeardPenaltySeconds; set => CrouchRun.HeardPenaltySeconds = value; }
        [DebugOrder(-396), Description("Crouch: Level 1 Timer (s)"), NumericSteps(Steps = 1, Min = 5, Max = 180), ES3NonSerializable]
        public float CrouchTimer1 { get => CrouchRun.GetLevel(0).timerSeconds; set => CrouchRun.GetLevel(0).timerSeconds = value; }
        [DebugOrder(-395), Description("Crouch: Level 2 Timer (s)"), NumericSteps(Steps = 1, Min = 5, Max = 180), ES3NonSerializable]
        public float CrouchTimer2 { get => CrouchRun.GetLevel(1).timerSeconds; set => CrouchRun.GetLevel(1).timerSeconds = value; }
        [DebugOrder(-394), Description("Crouch: Level 3 Timer (s)"), NumericSteps(Steps = 1, Min = 5, Max = 180), ES3NonSerializable]
        public float CrouchTimer3 { get => CrouchRun.GetLevel(2).timerSeconds; set => CrouchRun.GetLevel(2).timerSeconds = value; }

        // Robot Mecha: same rule as the rows above, the config assets are edited live.
        [DebugOrder(-300), Description("Mecha: Slack Line (0 chest, 1 hips)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1), ES3NonSerializable]
        public float MechaSlackLine { get => RobotArms.SlackLine; set => RobotArms.SlackLine = value; }
        [DebugOrder(-299), Description("Mecha: Hand Deadzone (in)"), NumericSteps(Steps = 0.25, Min = 0, Max = 6), ES3NonSerializable]
        public float MechaDeadzone { get => RobotArms.DeadzoneInches; set => RobotArms.DeadzoneInches = value; }
        [DebugOrder(-289), Description("Mecha: Hand Offset For Full Turn (in)"), NumericSteps(Steps = 0.5, Min = 2, Max = 30), ES3NonSerializable]
        public float MechaFullTurnOffset { get => RobotArms.FullYawOffsetInches; set => RobotArms.FullYawOffsetInches = value; }
        [DebugOrder(-288), Description("Mecha: Hand Offset For Full Pitch (in)"), NumericSteps(Steps = 0.5, Min = 2, Max = 30), ES3NonSerializable]
        public float MechaFullPitchOffset { get => RobotArms.FullPitchOffsetInches; set => RobotArms.FullPitchOffsetInches = value; }
        [DebugOrder(-298), Description("Mecha: Yaw Speed (deg/s)"), NumericSteps(Steps = 2, Min = 10, Max = 360), ES3NonSerializable]
        public float MechaYawSpeed { get => RobotArms.YawSpeed; set => RobotArms.YawSpeed = value; }
        [DebugOrder(-297), Description("Mecha: Pitch Speed (deg/s)"), NumericSteps(Steps = 1, Min = 5, Max = 180), ES3NonSerializable]
        public float MechaPitchSpeed { get => RobotArms.PitchSpeed; set => RobotArms.PitchSpeed = value; }
        [DebugOrder(-296), Description("Mecha: Pitch Clamp (deg)"), NumericSteps(Steps = 1, Min = 5, Max = 80), ES3NonSerializable]
        public float MechaPitchClamp { get => RobotArms.PitchClampDegrees; set => RobotArms.PitchClampDegrees = value; }
        [DebugOrder(-295), Description("Mecha: Swap Axes (hand height turns)"), ES3NonSerializable]
        public bool MechaSwapAxes { get => RobotArms.SwapAxes; set => RobotArms.SwapAxes = value; }
        [DebugOrder(-294), Description("Mecha: Beam Width (m)"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 4), ES3NonSerializable]
        public float MechaBeamWidth { get => MechaFight.BeamWidth; set => MechaFight.BeamWidth = value; }
        [DebugOrder(-293), Description("Mecha: Beam Damage (per s)"), NumericSteps(Steps = 0.1, Min = 0.1, Max = 20), ES3NonSerializable]
        public float MechaBeamDamage { get => MechaFight.BeamDamagePerSecond; set => MechaFight.BeamDamagePerSecond = value; }
        [DebugOrder(-292), Description("Mecha: Spawn Timing x (lower = sooner)"), NumericSteps(Steps = 0.05, Min = 0.3, Max = 2), ES3NonSerializable]
        public float MechaSpawnTiming { get => MechaFight.SpawnTimeScale; set => MechaFight.SpawnTimeScale = value; }
        [DebugOrder(-291), Description("Mecha: Hearts"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 12), ES3NonSerializable]
        public int MechaHearts { get => MechaFight.Hearts; set => MechaFight.Hearts = value; }
        [DebugOrder(-290), Description("Mecha: Wave Length (s)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float MechaWaveLength { get => MechaFight.WaveSeconds; set => MechaFight.WaveSeconds = value; }

        [DebugOrder(-99), Description("Fight: Slip Window (s)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 2), ES3NonSerializable]
        public float FightSlipWindow { get => Fight.SlipWindowSeconds; set => Fight.SlipWindowSeconds = value; }
        [DebugOrder(-98), Description("Fight: Lean Threshold (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 12), ES3NonSerializable]
        public float FightLeanThreshold { get => Fight.LeanThresholdInches; set => Fight.LeanThresholdInches = value; }
        [DebugOrder(-97), Description("Fight: Telegraph (s)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 2), ES3NonSerializable]
        public float FightTelegraph { get => Fight.TelegraphSeconds; set => Fight.TelegraphSeconds = value; }
        [DebugOrder(-87), Description("Fight: First Telegraph (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 4), ES3NonSerializable]
        public float FightFirstTelegraph { get => Fight.FirstTelegraphSeconds; set => Fight.FirstTelegraphSeconds = value; }
        [DebugOrder(-96), Description("Fight: Counter Window (s)"), NumericSteps(Steps = 0.05, Min = 0.3, Max = 3), ES3NonSerializable]
        public float FightCounterWindow { get => Fight.CounterWindowSeconds; set => Fight.CounterWindowSeconds = value; }
        [DebugOrder(-95), Description("Fight: Slow-mo Scale"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1), ES3NonSerializable]
        public float FightSlowMotion { get => Fight.SlowMotionScale; set => Fight.SlowMotionScale = value; }
        [DebugOrder(-94), Description("Fight: Hand Travel To Fill (in)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float FightHandTravel { get => Fight.HandTravelInches; set => Fight.HandTravelInches = value; }
        [DebugOrder(-93), Description("Fight: Hand Speed Peak (in/s)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float FightSpeedPeak { get => Fight.HandSpeedPeakInchesPerSecond; set => Fight.HandSpeedPeakInchesPerSecond = value; }
        [DebugOrder(-92), Description("Fight: Hand Jitter Floor (in)"), NumericSteps(Steps = 0.05, Min = 0, Max = 2), ES3NonSerializable]
        public float FightHandNoiseFloor { get => Fight.HandNoiseFloorInches; set => Fight.HandNoiseFloorInches = value; }
        [DebugOrder(-91), Description("Fight: Body Hand Fallback"), ES3NonSerializable]
        public bool FightBodyHandFallback { get => Fight.UseBodyHandFallback; set => Fight.UseBodyHandFallback = value; }
        [DebugOrder(-90), Description("Fight: Hearts"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 9), ES3NonSerializable]
        public int FightHearts { get => Fight.Hearts; set => Fight.Hearts = value; }
        [DebugOrder(-89), Description("Fight: Sweeps Per Player"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 12), ES3NonSerializable]
        public int FightSweeps { get => Fight.SweepsPerPlayer; set => Fight.SweepsPerPlayer = value; }
        [DebugOrder(-88), Description("Fight: Rest Between Sweeps (s)"), NumericSteps(Steps = 0.25, Min = 0.5, Max = 8), ES3NonSerializable]
        public float FightRest { get => Fight.RestBetweenSweepsSeconds; set => Fight.RestBetweenSweepsSeconds = value; }

        [DebugOrder(-80), Description("Chase: Deadzone (in)"), NumericSteps(Steps = 0.25, Min = 0, Max = 6), ES3NonSerializable]
        public float ChaseDeadzone { get => Chase.DeadzoneInches; set => Chase.DeadzoneInches = value; }
        [DebugOrder(-79), Description("Chase: Full Lean X (in)"), NumericSteps(Steps = 0.5, Min = 2, Max = 20), ES3NonSerializable]
        public float ChaseFullLean { get => Chase.FullLeanInches; set => Chase.FullLeanInches = value; }
        [DebugOrder(-78), Description("Chase: Full Rise Y (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 20), ES3NonSerializable]
        public float ChaseFullRise { get => Chase.FullRiseInches; set => Chase.FullRiseInches = value; }
        [DebugOrder(-77), Description("Chase: 2P Steer Blend (P1 share)"), NumericSteps(Steps = 0.05, Min = 0, Max = 1), ES3NonSerializable]
        public float ChaseSteerBlend { get => Chase.PlayerOneSteerShare; set => Chase.PlayerOneSteerShare = value; }
        [DebugOrder(-76), Description("Chase: Car Steer Smoothing (s)"), NumericSteps(Steps = 0.05, Min = 0.05, Max = 2), ES3NonSerializable]
        public float ChaseCarSmoothing { get => Car.steerSmoothingSeconds; set => Car.steerSmoothingSeconds = value; }
        [DebugOrder(-75), Description("Chase: Sky Steer Smoothing (s)"), NumericSteps(Steps = 0.05, Min = 0.05, Max = 2), ES3NonSerializable]
        public float ChaseSkySmoothing { get => Sky.steerSmoothingSeconds; set => Sky.steerSmoothingSeconds = value; }
        [DebugOrder(-74), Description("Chase: Car Obstacle Spacing (s)"), NumericSteps(Steps = 0.1, Min = 0.6, Max = 5), ES3NonSerializable]
        public float ChaseCarSpacing { get => Car.obstacleSpacingSeconds; set => Car.obstacleSpacingSeconds = value; }
        [DebugOrder(-73), Description("Chase: Sky Obstacle Spacing (s)"), NumericSteps(Steps = 0.1, Min = 0.6, Max = 5), ES3NonSerializable]
        public float ChaseSkySpacing { get => Sky.obstacleSpacingSeconds; set => Sky.obstacleSpacingSeconds = value; }
        [DebugOrder(-72), Description("Chase: Obstacle Lead (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 4), ES3NonSerializable]
        public float ChaseObstacleLead { get => Car.obstacleLeadSeconds; set => Car.obstacleLeadSeconds = Sky.obstacleLeadSeconds = value; }
        [DebugOrder(-71), Description("Chase: Bump Speed Drop"), NumericSteps(Steps = 0.05, Min = 0, Max = 0.9), ES3NonSerializable]
        public float ChaseBumpDrop { get => Chase.BumpSpeedDrop; set => Chase.BumpSpeedDrop = value; }

        [DebugOrder(-70), Description("Setup: Solo Fallback Wait (s)"), NumericSteps(Steps = 1, Min = 1, Max = 30), ES3NonSerializable]
        public float SetupSoloFallback { get => Players.SoloFallbackSeconds; set => Players.SoloFallbackSeconds = value; }

        [DebugOrder(-69), Description("Save: Reset Ninjago Progress"), SaveBeforeInvoking]
        public void ResetNinjagoProgress() => PlayerDataManager.Instance.ResetNinjagoProgress();

        static FightConfig Fight => GameConfigsManager.Instance.FightConfig;
        static ChaseConfig Chase => GameConfigsManager.Instance.ChaseConfig;
        static ChaseConfig.VehicleSettings Car => Chase.GetVehicle(VehicleType.Car);
        static ChaseConfig.VehicleSettings Sky => Chase.GetVehicle(VehicleType.Skycraft);
        static NinjagoPlayersConfig Players => GameConfigsManager.Instance.PlayersConfig;
        static HandCursorConfig Cursor => GameConfigsManager.Instance.HandCursorConfig;
        static StoneKickConfig StoneKick => GameConfigsManager.Instance.StoneKickConfig;
        static EarthSealConfig EarthSeal => GameConfigsManager.Instance.EarthSealConfig;
        static RobotArmsConfig RobotArms => GameConfigsManager.Instance.RobotArmsConfig;
        static MechaFightConfig MechaFight => GameConfigsManager.Instance.MechaFightConfig;
        static CrouchRunConfig CrouchRun => GameConfigsManager.Instance.CrouchRunConfig;
        static ThreeWeaponsConfig ThreeWeapons => GameConfigsManager.Instance.ThreeWeaponsConfig;
        static OnePunchConfig OnePunch => GameConfigsManager.Instance.OnePunchConfig;
        static GuardConfig Guard => GameConfigsManager.Instance.GuardConfig;
        static CallConfig Call => GameConfigsManager.Instance.CallConfig;
        static BuildConfig Build => GameConfigsManager.Instance.BuildConfig;
        static EarthSealConfig.WaveSettings W1 => EarthSeal.GetWave(EarthSealWave.Wave1);
        static EarthSealConfig.WaveSettings W2 => EarthSeal.GetWave(EarthSealWave.Wave2);
        static EarthSealConfig.WaveSettings W3 => EarthSeal.GetWave(EarthSealWave.Wave3);
        static EarthSealConfig.WaveSettings W4 => EarthSeal.GetWave(EarthSealWave.Wave4);

        // Volumes go through the PlayerDataManager properties so VolumeManager applies and persists them.
        public void MuteMusic() => SetMusicVolume(0);

        public void MuteSfx() => SetSfxVolume(0);

        public void SetMusicVolume(float volume) => PlayerDataManager.Instance.BgmVolumeProperty.Value = volume;
        public void SetSfxVolume(float volume) => PlayerDataManager.Instance.SfxVolumeProperty.Value = volume;

        public void ClearAllDataCache()
        {
            var playerDataManager = PlayerDataManager.Instance;
            playerDataManager.ResetPlayerPreference();
            playerDataManager.ResetMetaProgress();
            playerDataManager.ClearRun();
        }
    }
}
