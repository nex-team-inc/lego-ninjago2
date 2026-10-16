#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// One Punch tuning: the punch try and its two signals, side slash rejection, the waves, the fly-away and the pin.
    /// Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "OnePunchConfig", menuName = "Nex/Ninjago/One Punch Config")]
    public class OnePunchConfig : ScriptableObject
    {
        [Serializable]
        public class SignalLook
        {
            [Tooltip("Short word flashed over the player's HUD when a try is read this way.")]
            public LocalizedString label = new();
            public Color color = Color.white;
        }

        [Header("Punch Try")]
        [Tooltip("Wrist speed against the chest (body inches per second) that opens a try.")]
        [SerializeField, Range(5f, 150f)] float trySpeed = 25f;
        [Tooltip("Seconds after a try opens before it closes as a side slash or nothing, if no signal fired.")]
        [SerializeField, Range(0.1f, 1f)] float tryWindowSeconds = 0.35f;
        [Tooltip("Seconds a hand rests after each try (covers pulling the fist back).")]
        [SerializeField, Range(0f, 1.5f)] float punchCooldownSeconds = 0.35f;
        [Tooltip("How far back (seconds) the growth behind either signal may start.")]
        [SerializeField, Range(0.05f, 1f)] float signalWindowSeconds = 0.3f;

        [Header("Extension Signal")]
        [Tooltip("Shoulder-to-wrist growth speed (body inches per second) that counts as a punch.")]
        [SerializeField, Range(5f, 200f)] float extensionSpeed = 20f;
        [Tooltip("Shoulder-to-wrist growth (body inches) the punch needs at least.")]
        [SerializeField, Range(0.5f, 15f)] float extensionMinGrowth = 2.5f;
        [Tooltip("Body inches above and below the chest where the wrist must end.")]
        [SerializeField, Range(2f, 20f)] float chestFrontWindow = 9f;
        [Tooltip("Body inches past each shoulder the wrist may end and still be in front of the chest.")]
        [SerializeField, Range(0f, 8f)] float chestFrontMargin = 2f;

        [Header("Toward Camera Signal")]
        [Tooltip("Hand scale growth per second that counts as a punch (1 = the hand doubles in size in a second).")]
        [SerializeField, Range(0.1f, 5f)] float handScaleSpeed = 0.8f;
        [Tooltip("Hand scale growth the punch needs at least (0.15 = 15% bigger).")]
        [SerializeField, Range(0.03f, 1f)] float handScaleMinGrowth = 0.15f;

        [Header("Side Slash Rejection")]
        [Tooltip("A try is a side slash when the arm length changes by at most this share of the sideways wrist travel.")]
        [SerializeField, Range(0f, 1f)] float sideSlashLengthRatio = 0.35f;
        [Tooltip("Sideways wrist travel (body inches) before a try can be a side slash.")]
        [SerializeField, Range(2f, 30f)] float sideSlashMinTravel = 8f;

        [Header("Signal Looks")]
        [SerializeField] EnumDictionary<PunchSignal, SignalLook> signalLooks = new();

        [Header("Waves")]
        [SerializeField, Range(1, 10)] int waveCount = 5;
        [SerializeField, Range(1, 10)] int monstersPerWave = 5;
        [Tooltip("Seconds between the last monster of a wave going and the next wave walking in.")]
        [SerializeField, Range(0f, 8f)] float secondsBetweenWaves = 2f;
        [Tooltip("Meters down the street of the nearest monster when a wave starts.")]
        [SerializeField, Range(4f, 60f)] float firstMonsterDistance = 16f;
        [Tooltip("Meters between monsters in a wave, so they never reach the ninjas on the same frame.")]
        [SerializeField, Range(1f, 20f)] float monsterSpacing = 4f;
        [Tooltip("Meters either side of the street's middle where monsters start.")]
        [SerializeField, Range(0.5f, 6f)] float spawnHalfWidth = 2.6f;
        [Tooltip("Meters per second a monster walks.")]
        [SerializeField, Range(0.3f, 10f)] float monsterSpeed = 2.4f;
        [Tooltip("Meters from a ninja where a monster jumps on him.")]
        [SerializeField, Range(0.3f, 5f)] float grabRange = 1.3f;

        [Header("Fly-Away")]
        [Tooltip("Seconds from the punch to the monster bursting into bricks.")]
        [SerializeField, Range(0.3f, 4f)] float flightSeconds = 1.15f;
        [Tooltip("Launch velocity in meters per second: x away from the street's middle, y up, z down the street.")]
        [SerializeField] Vector3 launchVelocity = new(7f, 15f, 9f);
        [Tooltip("Meters per second squared pulling the flying monster down.")]
        [SerializeField, Range(0f, 40f)] float flightGravity = 12f;
        [Tooltip("Degrees per second the flying monster spins.")]
        [SerializeField, Range(0f, 2000f)] float flightSpinDegrees = 900f;

        [Header("Pin")]
        [SerializeField, Range(1f, 30f)] float startingHealth = 8f;
        [Tooltip("Health lost per second while pinned.")]
        [SerializeField, Range(0.1f, 5f)] float drainPerSecond = 1f;
        [Tooltip("Seconds of the jump onto the ninja before the drain starts.")]
        [SerializeField, Range(0f, 1.5f)] float pinJumpSeconds = 0.35f;
        [Tooltip("Seconds of hip movement the wiggle rule looks at.")]
        [SerializeField, Range(0.1f, 1.5f)] float wiggleWindowSeconds = 0.4f;
        [Tooltip("Body inches the hip center must travel within the window.")]
        [SerializeField, Range(1f, 40f)] float wigglePathInches = 8f;
        [Tooltip("Body inches every hip point in the window must stay within, around their middle.")]
        [SerializeField, Range(1f, 20f)] float wiggleRadiusInches = 5f;
        [Tooltip("Seconds of wiggling that fill the meter once the window rule holds; with the window this is about 1 health.")]
        [SerializeField, Range(0.1f, 5f)] float wiggleFillSeconds = 0.6f;

        [Header("Pacing")]
        [Tooltip("Seconds the WAVE banner and the PUNCH hint stay up when a wave starts.")]
        [SerializeField, Range(0f, 6f)] float waveBannerSeconds = 1.6f;
        [Tooltip("Seconds the CLEARED or DOWN banner stays up before the result.")]
        [SerializeField, Range(0f, 6f)] float endHoldSeconds = 2.5f;
        [Tooltip("Seconds a try's word stays over the player's HUD.")]
        [SerializeField, Range(0.2f, 3f)] float signalFlashSeconds = 1.2f;

        #region Public API

        public PunchDetector.Settings PunchSettings => new(trySpeed, tryWindowSeconds, punchCooldownSeconds, signalWindowSeconds, extensionSpeed,
            extensionMinGrowth, chestFrontWindow, chestFrontMargin, handScaleSpeed, handScaleMinGrowth, sideSlashLengthRatio, sideSlashMinTravel);

        public float ExtensionSpeed { get => extensionSpeed; set => extensionSpeed = Mathf.Max(0.5f, value); }
        public float ChestFrontWindow { get => chestFrontWindow; set => chestFrontWindow = Mathf.Max(0.5f, value); }
        public float HandScaleSpeed { get => handScaleSpeed; set => handScaleSpeed = Mathf.Max(0.01f, value); }
        public float SideSlashLengthRatio { get => sideSlashLengthRatio; set => sideSlashLengthRatio = Mathf.Clamp01(value); }
        public float PunchCooldownSeconds { get => punchCooldownSeconds; set => punchCooldownSeconds = Mathf.Max(0f, value); }

        public int WaveCount => waveCount;
        public int MonstersPerWave => monstersPerWave;
        public float SecondsBetweenWaves => secondsBetweenWaves;
        public float FirstMonsterDistance => firstMonsterDistance;
        public float MonsterSpacing => monsterSpacing;
        public float SpawnHalfWidth => spawnHalfWidth;
        public float MonsterSpeed { get => monsterSpeed; set => monsterSpeed = Mathf.Max(0.05f, value); }
        public float GrabRange { get => grabRange; set => grabRange = Mathf.Max(0.1f, value); }

        public float FlightSeconds => flightSeconds;
        public Vector3 LaunchVelocity => launchVelocity;
        public float FlightGravity => flightGravity;
        public float FlightSpinDegrees => flightSpinDegrees;

        public float StartingHealth { get => startingHealth; set => startingHealth = Mathf.Max(0.5f, value); }
        public float DrainPerSecond { get => drainPerSecond; set => drainPerSecond = Mathf.Max(0.01f, value); }
        public float PinJumpSeconds => pinJumpSeconds;
        public float WiggleWindowSeconds { get => wiggleWindowSeconds; set => wiggleWindowSeconds = Mathf.Max(0.05f, value); }
        public float WigglePathInches { get => wigglePathInches; set => wigglePathInches = Mathf.Max(0.5f, value); }
        public float WiggleRadiusInches { get => wiggleRadiusInches; set => wiggleRadiusInches = Mathf.Max(0.5f, value); }
        public float WiggleFillSeconds => wiggleFillSeconds;

        public float WaveBannerSeconds => waveBannerSeconds;
        public float EndHoldSeconds => endHoldSeconds;
        public float SignalFlashSeconds => signalFlashSeconds;

        public SignalLook GetLook(PunchSignal signal) => signalLooks[signal];

        #endregion
    }
}
