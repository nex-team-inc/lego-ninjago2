#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Robot Mecha fight tuning: hearts, the laser, each enemy type and the wave. Setters exist for the Debug Settings rows,
    /// which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "MechaFightConfig", menuName = "Nex/Ninjago/Mecha Fight Config")]
    public class MechaFightConfig : ScriptableObject
    {
        [Serializable]
        public class EnemySettings
        {
            [Tooltip("Laser damage the enemy takes before it breaks; the beam deals Beam Damage Per Second.")]
            public float health = 1f;
            [Tooltip("Meters per second toward the mecha.")]
            public float speed = 2.4f;
            [Tooltip("Body radius in meters the beam test uses.")]
            public float hitRadius = 0.8f;
            [Tooltip("Body height in meters the beam test uses.")]
            public float height = 1.3f;
            [Tooltip("Archers: distance from the mecha where a ground archer stops walking.")]
            public float stopDistance = 15f;
            [Tooltip("Archers: seconds after reaching its spot before the first draw.")]
            public float firstDrawDelay = 1f;
            [Tooltip("Archers: seconds of bow draw (the warning) before the arrow leaves.")]
            public float drawSeconds = 1.2f;
            [Tooltip("Archers: arrow flight seconds. Killing the archer before the arrow lands cancels it.")]
            public float arrowFlightSeconds = 2.4f;
            [Tooltip("Archers: seconds from an arrow leaving to the next draw.")]
            public float reloadSeconds = 3f;
        }

        [Serializable]
        public struct Spawn
        {
            [Tooltip("Seconds into the wave (before the spawn timing scale).")]
            public float time;
            public MechaEnemyType type;
            [Tooltip("Degrees around the ring from the mecha's starting facing: + is the player's right, 180 is behind.")]
            public float angle;
            [Tooltip("Archers only: stands on the pillar nearest to the angle, above the horizon.")]
            public bool high;
        }

        [Header("Rounds")]
        [Tooltip("Hearts each player starts with. A rusher hit or an arrow costs one; at 0 that player is down.")]
        [SerializeField, Range(1, 12)] int hearts = 5;
        [Tooltip("Seconds a player must survive to win.")]
        [SerializeField, Range(10f, 300f)] float waveSeconds = 70f;
        [Tooltip("Seconds the big L and ROBOT ARMS prompt stays up when the fight starts.")]
        [SerializeField, Range(0f, 15f)] float promptSeconds = 5f;
        [Tooltip("Seconds the SURVIVED or DOWN banner stays up before the result.")]
        [SerializeField, Range(0f, 6f)] float endHoldSeconds = 2.5f;

        [Header("Beam")]
        [Tooltip("Laser thickness in meters; the hit test and the visible beam both use it.")]
        [SerializeField, Range(0.1f, 4f)] float beamWidth = 0.8f;
        [Tooltip("Laser reach in meters.")]
        [SerializeField, Range(5f, 80f)] float beamRange = 32f;
        [Tooltip("Damage per second to the nearest enemy inside the beam.")]
        [SerializeField, Range(0.1f, 20f)] float beamDamagePerSecond = 2.2f;

        [Header("Arena")]
        [Tooltip("Radius in meters of the ring where ground enemies appear.")]
        [SerializeField, Range(8f, 40f)] float spawnRadius = 22f;
        [Tooltip("Distance in meters from the mecha at which a rusher lands its hit.")]
        [SerializeField, Range(0.5f, 6f)] float meleeRadius = 2.2f;
        [Tooltip("Height in meters an arrow travels above the straight line at the middle of its flight.")]
        [SerializeField, Range(0f, 10f)] float arrowArcHeight = 3f;

        [Header("Enemies")]
        [SerializeField] EnumDictionary<MechaEnemyType, EnemySettings> enemies = new();

        [Header("Wave")]
        [Tooltip("Multiplies every spawn time; below 1 the wave comes sooner and denser.")]
        [SerializeField, Range(0.3f, 2f)] float spawnTimeScale = 1f;
        [Tooltip("Spawns in time order: 0-15 s front, 15-40 s sides and a high archer, 40-70 s behind and two archers.")]
        [SerializeField] List<Spawn> spawns = new();

        #region Public API

        public int Hearts { get => hearts; set => hearts = Mathf.Max(1, value); }
        public float WaveSeconds { get => waveSeconds; set => waveSeconds = Mathf.Max(1f, value); }
        public float PromptSeconds => promptSeconds;
        public float EndHoldSeconds => endHoldSeconds;
        public float BeamWidth { get => beamWidth; set => beamWidth = Mathf.Max(0.05f, value); }
        public float BeamRange => beamRange;
        public float BeamDamagePerSecond { get => beamDamagePerSecond; set => beamDamagePerSecond = Mathf.Max(0.01f, value); }
        public float SpawnRadius => spawnRadius;
        public float MeleeRadius => meleeRadius;
        public float ArrowArcHeight => arrowArcHeight;
        public float SpawnTimeScale { get => spawnTimeScale; set => spawnTimeScale = Mathf.Max(0.05f, value); }
        public IReadOnlyList<Spawn> Spawns => spawns;

        public EnemySettings GetEnemy(MechaEnemyType type) => enemies[type];

        public float SpawnTime(int index) => spawns[index].time * spawnTimeScale;

        #endregion
    }
}
