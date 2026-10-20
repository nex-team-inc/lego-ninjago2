#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Chapter runtime tuning. Detector thresholds stay in the configs that own them (CrouchRunConfig for the crouch,
    /// ChaseConfig for steering, StoneKickConfig for slash speed, the frenzy and kicks, FightConfig for the slip and the
    /// hand storm); the values here are the base timings a block entry's difficulty scales.
    /// Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterTuning", menuName = "Nex/Ninjago/Chapter/Chapter Tuning")]
    public class ChapterTuning : ScriptableObject
    {
        [Header("Links")]
        [Tooltip("Seconds a link takes, from the last result to the next block's camera settling.")]
        [SerializeField, Range(3f, 6f)] float linkSeconds = 4.5f;
        [Tooltip("Seconds before the link ends that the next block's first tell starts.")]
        [SerializeField, Range(0f, 2f)] float tellLeadSeconds = 0.8f;
        [Tooltip("Height of the ninjas' leap between two stations, in meters.")]
        [SerializeField, Range(1f, 30f)] float leapHeight = 9f;
        [Tooltip("Seconds the story line stays up.")]
        [SerializeField, Range(1f, 8f)] float captionSeconds = 3.5f;

        [Header("Failure")]
        [Tooltip("Seconds of the in-scene 'try again' beat before the block restarts from its own start.")]
        [SerializeField, Range(1f, 5f)] float tryAgainSeconds = 2.6f;
        [Tooltip("Seconds a hint word stays over the ninja.")]
        [SerializeField, Range(0.5f, 5f)] float hintSeconds = 2.2f;

        [Header("Slash and Kick")]
        [Tooltip("Seconds of the thrower's tell before the rock leaves its hand (divided by speed).")]
        [SerializeField, Range(0.3f, 3f)] float throwTellSeconds = 1.1f;
        [Tooltip("Seconds the rock or leaping monster takes to reach the hang point (divided by speed).")]
        [SerializeField, Range(0.3f, 3f)] float throwFlightSeconds = 1.1f;
        [Tooltip("Real seconds the rock hangs in slow motion waiting for a slash (times the window scale).")]
        [SerializeField, Range(0.5f, 6f)] float hangSeconds = 2.6f;
        [Tooltip("Time scale of the thrown thing while it hangs.")]
        [SerializeField, Range(0.05f, 1f)] float hangTimeScale = 0.2f;
        [Tooltip("Seconds before the same hand's next stroke counts, so one wobbly swipe is one slash.")]
        [SerializeField, Range(0.05f, 0.6f)] float slashStrokeGapSeconds = 0.14f;
        [Tooltip("Real seconds the KICK prompt stays open (times the window scale); one kick sends every stone.")]
        [SerializeField, Range(0.5f, 6f)] float kickWindowSeconds = 3f;
        [Tooltip("Share of throws that are a leaping brick monster instead of a rock.")]
        [SerializeField, Range(0f, 1f)] float leapShare = 0.35f;
        [Tooltip("Seconds per heart lost while pinned by a monster.")]
        [SerializeField, Range(0.5f, 6f)] float pinHeartSeconds = 2.5f;
        [Tooltip("Inches both hands must travel together to shake a pinned monster off.")]
        [SerializeField, Range(10f, 150f)] float pinTravelInches = 45f;
        [Tooltip("Seconds that travel must happen in; slower shaking starts the count over.")]
        [SerializeField, Range(0.4f, 4f)] float pinBurstSeconds = 1f;

        [Header("Slip and Spin")]
        [Tooltip("Seconds of the swing's tell (cocked staff and the safe-side arrow), times the window scale and divided by speed.")]
        [SerializeField, Range(0.3f, 3f)] float swingTellSeconds = 1.2f;

        [Header("Fight Pace")]
        [Tooltip("Seconds between one threat's result and the next tell (divided by speed).")]
        [SerializeField, Range(0.3f, 5f)] float threatGapSeconds = 1.4f;
        [Tooltip("Seconds into a threat that an overlapping next threat starts its tell (times the window scale).")]
        [SerializeField, Range(0.3f, 4f)] float overlapDelaySeconds = 1.6f;

        [Header("Stealth")]
        [Tooltip("Meters of each clear zone between talking zones.")]
        [SerializeField, Range(4f, 60f)] float clearZoneMeters = 14f;
        [Tooltip("Meters between the ninjas when two sneak in single file.")]
        [SerializeField, Range(0.5f, 4f)] float sneakSpacing = 1.4f;

        [Header("Runner")]
        [Tooltip("Seconds after the ride starts before the first obstacle reaches the vehicle.")]
        [SerializeField, Range(0f, 6f)] float rideOpeningSeconds = 2f;
        [Tooltip("Meters the Boss starts ahead of the vehicle in the chase.")]
        [SerializeField, Range(4f, 60f)] float bossStartGap = 22f;
        [Tooltip("Meters a clean dodge closes on the Boss.")]
        [SerializeField, Range(0f, 10f)] float bossGapPerDodge = 1.4f;
        [Tooltip("Meters a bump loses on the Boss.")]
        [SerializeField, Range(0f, 10f)] float bossGapPerHit = 2.5f;
        [Tooltip("The Boss never lets the vehicle closer than this.")]
        [SerializeField, Range(2f, 30f)] float bossMinGap = 7f;
        [Tooltip("Seconds of the Boss's escape at the end of the chase.")]
        [SerializeField, Range(1f, 8f)] float bossEscapeSeconds = 3.5f;
        [Tooltip("Seconds the vehicle takes to pull up at the end of the track, so the leap to the next station is short.")]
        [SerializeField, Range(0.5f, 4f)] float rideArrivalSeconds = 2.2f;

        #region Public API

        public float LinkSeconds { get => linkSeconds; set => linkSeconds = Mathf.Clamp(value, 3f, 6f); }
        public float TellLeadSeconds => tellLeadSeconds;
        public float LeapHeight => leapHeight;
        public float CaptionSeconds => captionSeconds;
        public float TryAgainSeconds => tryAgainSeconds;
        public float HintSeconds => hintSeconds;
        public float ThrowTellSeconds { get => throwTellSeconds; set => throwTellSeconds = Mathf.Max(0.1f, value); }
        public float ThrowFlightSeconds => throwFlightSeconds;
        public float HangSeconds { get => hangSeconds; set => hangSeconds = Mathf.Max(0.2f, value); }
        public float HangTimeScale { get => hangTimeScale; set => hangTimeScale = Mathf.Clamp(value, 0.02f, 1f); }
        public float SlashStrokeGapSeconds { get => slashStrokeGapSeconds; set => slashStrokeGapSeconds = Mathf.Max(0f, value); }
        public float KickWindowSeconds { get => kickWindowSeconds; set => kickWindowSeconds = Mathf.Max(0.3f, value); }
        public float LeapShare { get => leapShare; set => leapShare = Mathf.Clamp01(value); }
        public float PinHeartSeconds { get => pinHeartSeconds; set => pinHeartSeconds = Mathf.Max(0.2f, value); }
        public float PinTravelInches { get => pinTravelInches; set => pinTravelInches = Mathf.Max(1f, value); }
        public float PinBurstSeconds { get => pinBurstSeconds; set => pinBurstSeconds = Mathf.Max(0.1f, value); }
        public float SwingTellSeconds { get => swingTellSeconds; set => swingTellSeconds = Mathf.Max(0.1f, value); }
        public float ThreatGapSeconds { get => threatGapSeconds; set => threatGapSeconds = Mathf.Max(0f, value); }
        public float OverlapDelaySeconds => overlapDelaySeconds;
        public float ClearZoneMeters { get => clearZoneMeters; set => clearZoneMeters = Mathf.Max(1f, value); }
        public float SneakSpacing => sneakSpacing;
        public float RideOpeningSeconds => rideOpeningSeconds;
        public float BossStartGap => bossStartGap;
        public float BossGapPerDodge => bossGapPerDodge;
        public float BossGapPerHit => bossGapPerHit;
        public float BossMinGap => bossMinGap;
        public float BossEscapeSeconds => bossEscapeSeconds;
        public float RideArrivalSeconds => rideArrivalSeconds;

        #endregion
    }
}
