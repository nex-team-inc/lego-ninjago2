#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Guard tuning: the shield guard reading, the throws, how a missed throw is judged, and the words for each outcome.
    /// Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "GuardConfig", menuName = "Nex/Ninjago/Guard Config")]
    public class GuardConfig : ScriptableObject
    {
        [Serializable]
        public class OutcomeLook
        {
            [Tooltip("The word flashed on the player's panel.")]
            public LocalizedString label = new();
            public Color color = Color.white;
        }

        [Header("Shield Guard")]
        [Tooltip("Body inches above the shoulder line both wrists must reach (0 = shoulder height).")]
        [SerializeField, Range(-6f, 12f)] float minAboveShoulder = 0f;
        [Tooltip("Body inches past each shoulder a raised wrist may be and still be in front of the face.")]
        [SerializeField, Range(0f, 12f)] float maxOutsideShoulder = 4f;
        [Tooltip("Body inches below the shoulder line a wrist must drop to lower the guard (the gap stops flicker).")]
        [SerializeField, Range(0f, 12f)] float lowerBelowShoulder = 4f;
        [Tooltip("Seconds a raised guard keeps blocking; then it must come down and go up again.")]
        [SerializeField, Range(0.3f, 5f)] float freshSeconds = 1.5f;

        [Header("Throws")]
        [Tooltip("Throws aimed at each player alone.")]
        [SerializeField, Range(1, 30)] int throwsPerPlayer = 10;
        [Tooltip("Giant bricks thrown at everyone at once: with two players, both must guard.")]
        [SerializeField, Range(0, 10)] int bigThrows = 3;
        [Tooltip("Seconds a goblin holds the brick up before throwing (the warning).")]
        [SerializeField, Range(0.3f, 4f)] float windUpSeconds = 1.1f;
        [Tooltip("Seconds the brick flies.")]
        [SerializeField, Range(0.4f, 4f)] float flightSeconds = 1.3f;
        [Tooltip("Seconds of rest after a hit before the next goblin winds up.")]
        [SerializeField, Range(0f, 6f)] float gapSeconds = 1.4f;
        [Tooltip("Meters a thrown brick rises above the straight line at mid-flight.")]
        [SerializeField, Range(0f, 10f)] float arcHeight = 3f;

        [Header("Judging A Miss")]
        [Tooltip("Seconds after the hit in which a guard still counts as too late, not missed.")]
        [SerializeField, Range(0f, 2f)] float lateWindowSeconds = 0.6f;
        [Tooltip("Seconds before the hit in which a half guard counts as a try.")]
        [SerializeField, Range(0f, 3f)] float halfWindowSeconds = 1.2f;

        [Header("Feel")]
        [SerializeField, Range(0f, 4f)] float dizzySeconds = 1.2f;
        [Tooltip("Seconds a missed brick sits on the ninja's head.")]
        [SerializeField, Range(0f, 4f)] float hatSeconds = 1.6f;
        [Tooltip("Meters the ninja slides back when a brick hits the shield.")]
        [SerializeField, Range(0f, 2f)] float shoveMeters = 0.35f;

        [Header("Pacing")]
        [SerializeField, Range(0f, 8f)] float introSeconds = 3f;
        [SerializeField, Range(0f, 8f)] float endHoldSeconds = 3f;
        [Tooltip("Seconds an outcome word stays on the player's panel.")]
        [SerializeField, Range(0.2f, 4f)] float flashSeconds = 1.4f;

        [Header("Outcome Looks")]
        [SerializeField] EnumDictionary<GuardOutcome, OutcomeLook> looks = new();

        #region Public API

        public GuardReader.Settings ReaderSettings => new(minAboveShoulder, maxOutsideShoulder, lowerBelowShoulder, freshSeconds);

        public float MinAboveShoulder { get => minAboveShoulder; set => minAboveShoulder = value; }
        public float MaxOutsideShoulder { get => maxOutsideShoulder; set => maxOutsideShoulder = Mathf.Max(0f, value); }
        public float FreshSeconds { get => freshSeconds; set => freshSeconds = Mathf.Max(0.1f, value); }
        public int ThrowsPerPlayer { get => throwsPerPlayer; set => throwsPerPlayer = Mathf.Max(1, value); }
        public int BigThrows => bigThrows;
        public float WindUpSeconds { get => windUpSeconds; set => windUpSeconds = Mathf.Max(0.1f, value); }
        public float FlightSeconds { get => flightSeconds; set => flightSeconds = Mathf.Max(0.1f, value); }
        public float GapSeconds => gapSeconds;
        public float ArcHeight => arcHeight;
        public float LateWindowSeconds => lateWindowSeconds;
        public float HalfWindowSeconds => halfWindowSeconds;
        public float DizzySeconds => dizzySeconds;
        public float HatSeconds => hatSeconds;
        public float ShoveMeters => shoveMeters;
        public float IntroSeconds => introSeconds;
        public float EndHoldSeconds => endHoldSeconds;
        public float FlashSeconds => flashSeconds;

        public OutcomeLook GetLook(GuardOutcome outcome) => looks[outcome];

        #endregion
    }
}
