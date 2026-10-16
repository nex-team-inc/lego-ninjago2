#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Call tuning: the call reading, the teammate, the goblin waves and piles, the calm-movement line, and the words for
    /// each call result. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "CallConfig", menuName = "Nex/Ninjago/Call Config")]
    public class CallConfig : ScriptableObject
    {
        [Serializable]
        public class ResultLook
        {
            [Tooltip("The word flashed on the caller's panel.")]
            public LocalizedString label = new();
            public Color color = Color.white;
        }

        [Header("Call")]
        [Tooltip("Body inches above the head the raised wrist must reach.")]
        [SerializeField, Range(-6f, 12f)] float aboveHead = 1f;
        [Tooltip("Body inches above the shoulder line where a raised hand counts as a try (and must drop below to call again).")]
        [SerializeField, Range(0f, 12f)] float tryAboveShoulder = 6f;

        [Header("Teammate")]
        [Tooltip("Seconds the teammate takes to spin down from the sky.")]
        [SerializeField, Range(0.2f, 2f)] float arriveSeconds = 0.6f;
        [Tooltip("Seconds the teammate stands after the slam before jumping away.")]
        [SerializeField, Range(0f, 2f)] float stayAfterSlamSeconds = 0.5f;
        [Tooltip("Seconds the teammate takes to jump back up.")]
        [SerializeField, Range(0.2f, 2f)] float leaveSeconds = 0.5f;
        [Tooltip("Seconds after leaving before the teammate can come again.")]
        [SerializeField, Range(0f, 8f)] float cooldownSeconds = 1.5f;
        [Tooltip("Meters in front of the caller where the teammate lands.")]
        [SerializeField, Range(0f, 8f)] float landAhead = 2.5f;
        [Tooltip("Meters around the landing spot the slam clears (doubled for a team call).")]
        [SerializeField, Range(1f, 12f)] float slamRadius = 4f;

        [Header("Goblins")]
        [Tooltip("Goblins in each wave, in order.")]
        [SerializeField] List<int> goblinsPerWave = new() { 3, 4, 5, 6 };
        [Tooltip("Meters per second a goblin walks.")]
        [SerializeField, Range(0.3f, 6f)] float goblinSpeed = 1.5f;
        [Tooltip("Goblin size against the prefab.")]
        [SerializeField, Range(0.3f, 2f)] float goblinScale = 0.8f;
        [Tooltip("Meters down the courtyard of the nearest goblin when a wave starts.")]
        [SerializeField, Range(4f, 40f)] float spawnDistance = 13f;
        [Tooltip("Meters between goblins in a wave.")]
        [SerializeField, Range(0.5f, 10f)] float spawnSpacing = 2.2f;
        [Tooltip("Meters either side of the middle where goblins start.")]
        [SerializeField, Range(0.5f, 6f)] float spawnHalfWidth = 2.6f;
        [Tooltip("Meters from a ninja where a goblin climbs on.")]
        [SerializeField, Range(0.3f, 4f)] float grabRange = 1f;
        [Tooltip("Goblins on one ninja that make a goblin pile: the wave starts again.")]
        [SerializeField, Range(1, 8)] int pileSize = 3;
        [Tooltip("Goblin piles in one wave before the teammate comes by itself.")]
        [SerializeField, Range(0, 6)] int maxPilesPerWave = 2;
        [Tooltip("Meters from a ninja where a goblin brings up the CALL prompt.")]
        [SerializeField, Range(1f, 15f)] float promptDistance = 6f;

        [Header("Fly-Away")]
        [Tooltip("Meters per second a slammed goblin is thrown: x away from the slam, y up, z down the courtyard.")]
        [SerializeField] Vector3 launchVelocity = new(6f, 12f, 5f);
        [SerializeField, Range(0.3f, 3f)] float flightSeconds = 1f;
        [SerializeField, Range(0f, 40f)] float flightGravity = 12f;
        [SerializeField, Range(0f, 2000f)] float flightSpinDegrees = 720f;

        [Header("Calm Movement")]
        [Tooltip("Average hand speed (body inches per second, both hands) at or under which a run counts as calm.")]
        [SerializeField, Range(2f, 80f)] float calmHandSpeed = 20f;

        [Header("Pacing")]
        [SerializeField, Range(0f, 8f)] float introSeconds = 3f;
        [Tooltip("Seconds between a cleared wave and the next.")]
        [SerializeField, Range(0f, 8f)] float waveGapSeconds = 2f;
        [Tooltip("Seconds the GOBLIN PILE banner stays before the wave starts again.")]
        [SerializeField, Range(0f, 8f)] float pileSeconds = 2.2f;
        [SerializeField, Range(0f, 8f)] float endHoldSeconds = 3f;
        [SerializeField, Range(0.2f, 4f)] float flashSeconds = 1.4f;

        [Header("Result Looks")]
        [SerializeField] EnumDictionary<CallResult, ResultLook> looks = new();

        #region Public API

        public CallReader.Settings ReaderSettings => new(aboveHead, tryAboveShoulder);

        public float AboveHead { get => aboveHead; set => aboveHead = value; }
        public float TryAboveShoulder { get => tryAboveShoulder; set => tryAboveShoulder = Mathf.Max(0f, value); }
        public float ArriveSeconds => arriveSeconds;
        public float StayAfterSlamSeconds => stayAfterSlamSeconds;
        public float LeaveSeconds => leaveSeconds;
        public float CooldownSeconds { get => cooldownSeconds; set => cooldownSeconds = Mathf.Max(0f, value); }
        public float LandAhead => landAhead;
        public float SlamRadius { get => slamRadius; set => slamRadius = Mathf.Max(0.5f, value); }
        public IReadOnlyList<int> GoblinsPerWave => goblinsPerWave;
        public float GoblinSpeed { get => goblinSpeed; set => goblinSpeed = Mathf.Max(0.1f, value); }
        public float GoblinScale => goblinScale;
        public float SpawnDistance => spawnDistance;
        public float SpawnSpacing => spawnSpacing;
        public float SpawnHalfWidth => spawnHalfWidth;
        public float GrabRange => grabRange;
        public int PileSize { get => pileSize; set => pileSize = Mathf.Max(1, value); }
        public int MaxPilesPerWave => maxPilesPerWave;
        public float PromptDistance => promptDistance;
        public Vector3 LaunchVelocity => launchVelocity;
        public float FlightSeconds => flightSeconds;
        public float FlightGravity => flightGravity;
        public float FlightSpinDegrees => flightSpinDegrees;
        public float CalmHandSpeed { get => calmHandSpeed; set => calmHandSpeed = Mathf.Max(1f, value); }
        public float IntroSeconds => introSeconds;
        public float WaveGapSeconds => waveGapSeconds;
        public float PileSeconds => pileSeconds;
        public float EndHoldSeconds => endHoldSeconds;
        public float FlashSeconds => flashSeconds;

        public ResultLook GetLook(CallResult result) => looks[result];

        #endregion
    }
}
