#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Base of every pooled arena prop (bricks, rocks, reticles, walls, minions, crates, rockets, gusts, bolts...). The pool
    /// calls NotifySpawned after Get; the prop moves itself (ground props scroll by IBossArena.GroundSpeed) and calls
    /// Release() when done. Release is idempotent: only the first call after a spawn returns it to the pool.
    /// </summary>
    public abstract class PooledProp : MonoBehaviour, IPoolableObject
    {
        [Header("Kind")]
        [Tooltip("The pool this prop belongs to.")]
        [SerializeField] BossPropKind kind;

        bool released = true;

        public BossPropKind Kind => kind;
        /// <summary>Between NotifySpawned and Release.</summary>
        public bool IsSpawned => !released;

        public event Action<Component>? OnRelease;

        #region Public API

        public void Release()
        {
            if (released) return;
            released = true;
            OnReleased();
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Pool

        /// <summary>Called by the pool right after Get.</summary>
        internal void NotifySpawned()
        {
            released = false;
            OnSpawned();
        }

        protected virtual void OnSpawned()
        {
        }

        protected virtual void OnReleased()
        {
        }

        #endregion
    }
}
