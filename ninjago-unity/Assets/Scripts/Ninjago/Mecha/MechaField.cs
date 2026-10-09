#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The ring around one player's mecha: spawns that player's copy of the wave, moves the rushers and archers, flies the
    /// arrows and reports every hit that reaches the mecha. An archer's arrows in the air are cancelled when it dies.
    /// </summary>
    public class MechaField : MonoBehaviour
    {
        [Header("Rusher Pool")]
        [SerializeField] MechaEnemyPool rusherPool = null!;
        [Header("Rusher Prefab")]
        [SerializeField] MechaEnemy rusherPrefab = null!;
        [Header("Archer Pool")]
        [SerializeField] MechaEnemyPool archerPool = null!;
        [Header("Archer Prefab")]
        [SerializeField] MechaEnemy archerPrefab = null!;
        [Header("Arrow Pool")]
        [SerializeField] MechaArrowPool arrowPool = null!;
        [Header("Arrow Prefab")]
        [SerializeField] MechaArrow arrowPrefab = null!;
        [Header("Pillar Tops")]
        [Tooltip("High spawn points on the ring; a high archer stands on the one nearest its angle.")]
        [SerializeField] Transform[] pillarTops = null!;
        [Header("Bow Height")]
        [Tooltip("Meters above an archer's feet where its arrows leave.")]
        [SerializeField] float bowHeight = 1.6f;
        [Header("Arrow Target Height")]
        [Tooltip("Meters above the center mark where arrows land on the mecha.")]
        [SerializeField] float arrowTargetHeight = 1.8f;
        [Header("Rusher Color")]
        [SerializeField] Color rusherColor = new(1f, 0.45f, 0.1f);
        [Header("Archer Color")]
        [SerializeField] Color archerColor = new(0.65f, 0.3f, 0.95f);

        readonly List<MechaEnemy> enemies = new();
        readonly List<MechaArrow> arrows = new();
        MechaFightConfig config = null!;
        int nextSpawn;
        int spawnSerial;

        /// <summary>A rusher arrived or an arrow landed; the argument is "rusher" or "arrow".</summary>
        public event Action<string>? MechaHit;

        public IReadOnlyList<MechaEnemy> Enemies => enemies;

        #region Initialization

        public void Initialize(MechaFightConfig aConfig)
        {
            config = aConfig;
            rusherPool.Initialize(rusherPrefab);
            archerPool.Initialize(archerPrefab);
            arrowPool.Initialize(arrowPrefab);
            rusherPool.Prewarm(6);
            archerPool.Prewarm(3);
            arrowPool.Prewarm(4);
        }

        #endregion

        #region Public API

        public void Tick(float waveTime, float deltaTime)
        {
            SpawnDue(waveTime);
            TickEnemies(deltaTime);
            TickArrows(deltaTime);
        }

        /// <summary>The nearest enemy inside the beam, or null; distance is along the beam.</summary>
        public MechaEnemy? FindBeamTarget(Vector3 origin, Vector3 direction, float range, float beamRadius, out float distance)
        {
            MechaEnemy? target = null;
            distance = range;
            foreach (var enemy in enemies)
            {
                var hit = MechaBeam.HitDistance(origin, direction, range, beamRadius, enemy.Feet, enemy.HitRadius, enemy.Height);
                if (hit < 0f || hit >= distance) continue;
                distance = hit;
                target = enemy;
            }

            return target;
        }

        /// <summary>Breaks a dead enemy; true when it was an archer with an arrow in the air (that arrow is now cancelled).</summary>
        public bool Break(MechaEnemy enemy)
        {
            var cancelledArrow = HasArrowInFlight(enemy);
            PlayBreak(enemy);
            enemies.Remove(enemy);
            enemy.Release();
            return cancelledArrow;
        }

        /// <summary>Wave over: every enemy breaks apart and every arrow fizzles, none of it counting as a kill.</summary>
        public void Clear()
        {
            foreach (var enemy in enemies)
            {
                PlayBreak(enemy);
                enemy.Release();
            }

            enemies.Clear();
            foreach (var arrow in arrows)
            {
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.ArrowFizzle, arrow.transform.position);
                arrow.Release();
            }

            arrows.Clear();
        }

        public void PointEdgeArrows(EdgeArrows edgeArrows, Camera laneCamera)
        {
            edgeArrows.Begin();
            foreach (var enemy in enemies)
            {
                var archer = enemy.Type == MechaEnemyType.Archer;
                edgeArrows.Point(laneCamera, enemy.Center, archer, archer && (enemy.IsDrawing || HasArrowInFlight(enemy)));
            }

            edgeArrows.End();
        }

        #endregion

        #region Wave

        void SpawnDue(float waveTime)
        {
            var spawns = config.Spawns;
            while (nextSpawn < spawns.Count && config.SpawnTime(nextSpawn) <= waveTime)
            {
                Spawn(spawns[nextSpawn]);
                nextSpawn++;
            }
        }

        void Spawn(MechaFightConfig.Spawn spawn)
        {
            var archer = spawn.type == MechaEnemyType.Archer;
            var enemy = (archer ? archerPool : rusherPool).Get();
            var high = archer && spawn.high;
            var position = high ? transform.InverseTransformPoint(NearestPillar(spawn.angle).position) : OnRing(spawn.angle, config.SpawnRadius);
            enemy.Spawn(++spawnSerial, config.GetEnemy(spawn.type), position, high);
            enemies.Add(enemy);
        }

        void TickEnemies(float deltaTime)
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                var signal = enemy.Tick(deltaTime, config.MeleeRadius);
                if (signal == MechaEnemy.Signal.Fire)
                {
                    Loose(enemy);
                }
                else if (signal == MechaEnemy.Signal.Arrived)
                {
                    PlayBreak(enemy);
                    enemies.RemoveAt(i);
                    enemy.Release();
                    MechaHit?.Invoke("rusher");
                }
            }
        }

        void Loose(MechaEnemy archer)
        {
            var arrow = arrowPool.Get();
            var from = transform.InverseTransformPoint(archer.Feet + Vector3.up * bowHeight + archer.transform.forward * 0.6f);
            var settings = config.GetEnemy(MechaEnemyType.Archer);
            arrow.Launch(archer, from, new Vector3(0f, arrowTargetHeight, 0f), settings.arrowFlightSeconds, config.ArrowArcHeight);
            arrows.Add(arrow);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
        }

        void TickArrows(float deltaTime)
        {
            for (var i = arrows.Count - 1; i >= 0; i--)
            {
                var arrow = arrows[i];
                if (!arrow.ArcherAlive)
                {
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.ArrowFizzle, arrow.transform.position);
                    arrows.RemoveAt(i);
                    arrow.Release();
                    continue;
                }

                if (!arrow.Tick(deltaTime)) continue;
                arrows.RemoveAt(i);
                arrow.Release();
                MechaHit?.Invoke("arrow");
            }
        }

        #endregion

        #region Helpers

        bool HasArrowInFlight(MechaEnemy enemy)
        {
            foreach (var arrow in arrows)
            {
                if (arrow.IsFrom(enemy)) return true;
            }

            return false;
        }

        void PlayBreak(MechaEnemy enemy)
        {
            var system = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.EnemyExplosion, enemy.Center, Quaternion.identity);
            if (system == null) return;
            var main = system.main;
            main.startColor = enemy.Type == MechaEnemyType.Archer ? archerColor : rusherColor;
        }

        Transform NearestPillar(float angle)
        {
            var best = pillarTops[0];
            var bestDelta = float.MaxValue;
            foreach (var top in pillarTops)
            {
                var local = transform.InverseTransformPoint(top.position);
                var delta = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg));
                if (delta >= bestDelta) continue;
                bestDelta = delta;
                best = top;
            }

            return best;
        }

        // + angles go to the player's right: clockwise seen from above, the way a + yaw turns the mecha.
        static Vector3 OnRing(float angle, float radius)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
        }

        #endregion
    }
}
