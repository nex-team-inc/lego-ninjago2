#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Crouch Run tuning: the crouch rule, speeds, the heard penalty, how each zone state reads, and the three levels'
    /// zone lists and timers. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "CrouchRunConfig", menuName = "Nex/Ninjago/Crouch Run Config")]
    public class CrouchRunConfig : ScriptableObject
    {
        [Serializable]
        public class ZoneLook
        {
            [Tooltip("The one-word banner while inside a zone of this state, e.g. CROUCH. Leave empty for no banner.")]
            public LocalizedString prompt = new();
            [Tooltip("Banner color for this state.")]
            public Color color = Color.white;
        }

        [Serializable]
        public struct Zone
        {
            public CrouchZoneState state;
            [Tooltip("Zone length in meters along the path.")]
            public float length;
        }

        [Serializable]
        public class Level
        {
            [Tooltip("Seconds on the clock at the start of the level.")]
            public float timerSeconds = 45f;
            [Tooltip("Zones in order from the start; the gate stands at the end of the last one.")]
            public List<Zone> zones = new();

            public float Length
            {
                get
                {
                    var length = 0f;
                    foreach (var zone in zones) length += zone.length;
                    return length;
                }
            }

            public float ZoneStart(int index)
            {
                var start = 0f;
                for (var i = 0; i < index; i++) start += zones[i].length;
                return start;
            }

            /// <summary>Index of the zone at distance x along the path (the last zone past the end).</summary>
            public int ZoneAt(float x)
            {
                var end = 0f;
                for (var i = 0; i < zones.Count; i++)
                {
                    end += zones[i].length;
                    if (x < end) return i;
                }

                return zones.Count - 1;
            }
        }

        [Header("Crouch")]
        [Tooltip("Body inches the chest must drop below its calibrated standing height to start crouching.")]
        [SerializeField, Range(1f, 20f)] float crouchDropInches = 5f;
        [Tooltip("Body inches of hysteresis: the chest must come back above (crouch drop - this) to stand again, so the ninja does not flicker.")]
        [SerializeField, Range(0f, 10f)] float hysteresisInches = 2f;
        [Tooltip("Seconds the chest must stay past the line before the stance changes; a small nod does not count.")]
        [SerializeField, Range(0f, 1f)] float stanceHoldSeconds = 0.15f;

        [Header("Speeds")]
        [Tooltip("Meters per second while standing (running).")]
        [SerializeField, Range(1f, 20f)] float runSpeed = 6f;
        [Tooltip("Meters per second while crouching (walking quietly).")]
        [SerializeField, Range(0.3f, 10f)] float crouchSpeed = 2f;
        [Tooltip("Seconds the ninja takes to change between walk and run speed.")]
        [SerializeField, Range(0f, 1f)] float speedChangeSeconds = 0.15f;

        [Header("Heard")]
        [Tooltip("Seconds of running past the windows before the monsters outside see it.")]
        [SerializeField, Range(0f, 2f)] float heardGraceSeconds = 0.35f;
        [Tooltip("Seconds taken off the clock when heard.")]
        [SerializeField, Range(0f, 15f)] float heardPenaltySeconds = 3f;
        [Tooltip("Seconds the ninja stays frozen at the zone start after being heard (the clock keeps running).")]
        [SerializeField, Range(0f, 3f)] float heardFreezeSeconds = 1f;

        [Header("Pacing")]
        [Tooltip("Seconds of LEVEL n before the clock starts.")]
        [SerializeField, Range(0f, 6f)] float levelIntroSeconds = 2.5f;
        [Tooltip("Seconds the CLEARED or TIME UP banner stays up after a level.")]
        [SerializeField, Range(0f, 6f)] float levelOutroSeconds = 2f;

        [Header("Zone Looks")]
        [SerializeField] EnumDictionary<CrouchZoneState, ZoneLook> looks = new();

        [Header("Levels")]
        [Tooltip("Level 1, 2 and 3 in order. Harder levels have shorter zones, faster changes and a tighter timer.")]
        [SerializeField] List<Level> levels = new();

        #region Public API

        public float CrouchDropInches { get => crouchDropInches; set => crouchDropInches = Mathf.Max(0.5f, value); }
        public float StandDropInches => Mathf.Max(0f, crouchDropInches - hysteresisInches);
        public float StanceHoldSeconds => stanceHoldSeconds;
        public float RunSpeed { get => runSpeed; set => runSpeed = Mathf.Max(0.1f, value); }
        public float CrouchSpeed { get => crouchSpeed; set => crouchSpeed = Mathf.Max(0.1f, value); }
        public float SpeedChangeSeconds => speedChangeSeconds;
        public float HeardGraceSeconds => heardGraceSeconds;
        public float HeardPenaltySeconds { get => heardPenaltySeconds; set => heardPenaltySeconds = Mathf.Max(0f, value); }
        public float HeardFreezeSeconds => heardFreezeSeconds;
        public float LevelIntroSeconds => levelIntroSeconds;
        public float LevelOutroSeconds => levelOutroSeconds;
        public int LevelCount => levels.Count;

        public Level GetLevel(int index) => levels[index];

        public ZoneLook GetLook(CrouchZoneState state) => looks[state];

        /// <summary>Seconds a perfect run of a level takes: running every clear zone, walking every talking zone.</summary>
        public float PerfectSeconds(int levelIndex)
        {
            var seconds = 0f;
            foreach (var zone in levels[levelIndex].zones) seconds += zone.length / (zone.state == CrouchZoneState.Clear ? runSpeed : crouchSpeed);
            return seconds;
        }

        #endregion
    }
}
