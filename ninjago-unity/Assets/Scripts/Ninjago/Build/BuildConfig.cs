#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Build tuning: the clap reading, the bridge, the time limit, the walk across, and the words for each clap. Setters
    /// exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfig", menuName = "Nex/Ninjago/Build Config")]
    public class BuildConfig : ScriptableObject
    {
        [Serializable]
        public class GestureLook
        {
            [Tooltip("The word flashed on the builder's panel.")]
            public LocalizedString label = new();
            public Color color = Color.white;
        }

        [Header("Clap")]
        [Tooltip("Wrists closer than this many shoulder widths are together: a click.")]
        [SerializeField, Range(0.1f, 1f)] float togetherRatio = 0.5f;
        [Tooltip("Wrists further than this many shoulder widths are apart: the next click is ready.")]
        [SerializeField, Range(0.3f, 2f)] float apartRatio = 0.9f;
        [Tooltip("Wrists that got closer than this but not together, then parted, were a near clap (not counted).")]
        [SerializeField, Range(0.2f, 1.5f)] float nearRatio = 0.75f;
        [Tooltip("Shortest seconds between two clicks, so one clap never counts twice.")]
        [SerializeField, Range(0f, 1f)] float minClapSeconds = 0.25f;
        [Tooltip("Body inches below the chest a wrist may be and still click (hands hanging low do not).")]
        [SerializeField, Range(0f, 30f)] float lowestWristBelowChest = 14f;

        [Header("Bridge")]
        [Tooltip("Rows of bricks from this side of the river to the far side.")]
        [SerializeField, Range(2, 30)] int rows = 10;
        [Tooltip("Seconds a brick flies from its pile into place.")]
        [SerializeField, Range(0.1f, 2f)] float brickFlightSeconds = 0.45f;

        [Header("Pacing")]
        [Tooltip("Seconds to finish the bridge; when they run out the way stays shut.")]
        [SerializeField, Range(20f, 300f)] float timeLimitSeconds = 100f;
        [Tooltip("Seconds without a click before the hint comes back.")]
        [SerializeField, Range(1f, 20f)] float idleHintSeconds = 5f;
        [Tooltip("Meters per second the ninjas walk onto the bridge and across.")]
        [SerializeField, Range(0.5f, 8f)] float walkSpeed = 2.4f;
        [SerializeField, Range(0f, 8f)] float introSeconds = 3f;
        [Tooltip("Seconds of the walk across and the open gate before the result.")]
        [SerializeField, Range(0f, 10f)] float celebrateSeconds = 4f;
        [SerializeField, Range(0f, 8f)] float endHoldSeconds = 2f;
        [SerializeField, Range(0.2f, 4f)] float flashSeconds = 0.8f;

        [Header("Gesture Looks")]
        [SerializeField] EnumDictionary<BuildGesture, GestureLook> looks = new();

        #region Public API

        public ClapReader.Settings ReaderSettings => new(togetherRatio, apartRatio, nearRatio, minClapSeconds, -lowestWristBelowChest);

        public float TogetherRatio { get => togetherRatio; set => togetherRatio = Mathf.Clamp(value, 0.05f, apartRatio - 0.05f); }
        public float ApartRatio { get => apartRatio; set => apartRatio = Mathf.Max(togetherRatio + 0.05f, value); }
        public float NearRatio { get => nearRatio; set => nearRatio = value; }
        public float MinClapSeconds { get => minClapSeconds; set => minClapSeconds = Mathf.Max(0f, value); }
        public int Rows { get => rows; set => rows = Mathf.Max(1, value); }
        public float BrickFlightSeconds => brickFlightSeconds;
        public float TimeLimitSeconds { get => timeLimitSeconds; set => timeLimitSeconds = Mathf.Max(5f, value); }
        public float IdleHintSeconds => idleHintSeconds;
        public float WalkSpeed => walkSpeed;
        public float IntroSeconds => introSeconds;
        public float CelebrateSeconds => celebrateSeconds;
        public float EndHoldSeconds => endHoldSeconds;
        public float FlashSeconds => flashSeconds;

        public GestureLook GetLook(BuildGesture gesture) => looks[gesture];

        #endregion
    }
}
