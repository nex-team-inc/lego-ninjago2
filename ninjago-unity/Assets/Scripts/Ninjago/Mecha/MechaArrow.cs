#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>One pooled arrow in flight from an archer to the mecha on an arc; it lands after its flight time.</summary>
    public class MechaArrow : MonoBehaviour, IPoolableObject
    {
        MechaEnemy archer = null!;
        int archerSpawnId;
        Vector3 from;
        Vector3 to;
        float flightSeconds;
        float arcHeight;
        float elapsed;

        public event Action<Component>? OnRelease;

        /// <summary>False once the archer that loosed it has died: the arrow is cancelled.</summary>
        public bool ArcherAlive => archer.IsAlive && archer.SpawnId == archerSpawnId;

        public bool IsFrom(MechaEnemy enemy) => archer == enemy && archerSpawnId == enemy.SpawnId;

        #region Public API

        /// <param name="localFrom">Lane-local start point.</param>
        /// <param name="localTo">Lane-local landing point.</param>
        public void Launch(MechaEnemy shooter, Vector3 localFrom, Vector3 localTo, float flight, float arc)
        {
            archer = shooter;
            archerSpawnId = shooter.SpawnId;
            from = localFrom;
            to = localTo;
            flightSeconds = flight;
            arcHeight = arc;
            elapsed = 0f;
            Place(0f);
        }

        /// <summary>True on the frame the arrow lands.</summary>
        public bool Tick(float deltaTime)
        {
            elapsed += deltaTime;
            var t = Mathf.Clamp01(elapsed / flightSeconds);
            Place(t);
            return t >= 1f;
        }

        public void Release()
        {
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Helpers

        void Place(float t)
        {
            var position = PointAt(t);
            var ahead = PointAt(Mathf.Min(1f, t + 0.02f)) - PointAt(Mathf.Max(0f, t - 0.02f));
            transform.localPosition = position;
            if (ahead.sqrMagnitude > 1e-6f) transform.localRotation = Quaternion.LookRotation(ahead);
        }

        Vector3 PointAt(float t) => Vector3.Lerp(from, to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));

        #endregion
    }
}
