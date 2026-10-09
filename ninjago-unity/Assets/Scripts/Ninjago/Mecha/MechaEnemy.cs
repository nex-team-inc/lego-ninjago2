#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One pooled brick enemy on the ring. A rusher runs at the mecha and reports when it arrives; an archer walks to its
    /// range (or stands on its pillar), draws its bow as a warning and reports each arrow it looses.
    /// </summary>
    public class MechaEnemy : MonoBehaviour, IPoolableObject
    {
        public enum Signal
        {
            None,
            Arrived,
            Fire,
        }

        enum State
        {
            Walking,
            Ready,
            Drawing,
            Reloading,
        }

        [Header("Type")]
        [SerializeField] MechaEnemyType type;
        [Header("Body")]
        [Tooltip("Waddles while walking.")]
        [SerializeField] Transform body = null!;
        [Header("Flash Renderers")]
        [Tooltip("Swap to the hurt material while the beam burns the enemy.")]
        [SerializeField] Renderer[] flashRenderers = null!;
        [Header("Hurt Material")]
        [SerializeField] Material hurtMaterial = null!;
        [Header("Bow")]
        [Tooltip("Archers: raised while drawing.")]
        [SerializeField] Transform? bow;
        [Header("Nocked Arrow")]
        [Tooltip("Archers: shown on the bow while drawing.")]
        [SerializeField] GameObject? nockedArrow;
        [Header("Waddle Degrees")]
        [SerializeField] float waddleDegrees = 12f;
        [Header("Waddle Hz")]
        [SerializeField] float waddleHz = 3f;
        [Header("Hurt Flash Seconds")]
        [SerializeField] float hurtFlashSeconds = 0.08f;

        Material[] baseMaterials = Array.Empty<Material>();
        MechaFightConfig.EnemySettings settings = null!;
        State state;
        float stateElapsed;
        float walkElapsed;
        float hurtLeft;

        public event Action<Component>? OnRelease;

        public MechaEnemyType Type => type;
        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        /// <summary>Changes every spawn, so an arrow can tell that its archer died even after the pool reused it.</summary>
        public int SpawnId { get; private set; }
        /// <summary>Archer drawing its bow: an arrow is about to leave.</summary>
        public bool IsDrawing => state == State.Drawing;
        public float HitRadius => settings.hitRadius;
        public float Height => settings.height;
        public Vector3 Feet => transform.position;
        public Vector3 Center => transform.position + Vector3.up * (settings.height * 0.5f);

        #region Life Cycle

        void Awake()
        {
            baseMaterials = new Material[flashRenderers.Length];
            for (var i = 0; i < flashRenderers.Length; i++) baseMaterials[i] = flashRenderers[i].sharedMaterial;
        }

        #endregion

        #region Public API

        /// <param name="localPosition">Lane-local feet position; the enemy turns to face the mecha at the lane center.</param>
        /// <param name="standing">Archers on a pillar: no walking, the draw cycle starts at once.</param>
        public void Spawn(int spawnId, MechaFightConfig.EnemySettings enemySettings, Vector3 localPosition, bool standing)
        {
            SpawnId = spawnId;
            settings = enemySettings;
            Health = settings.health;
            transform.localPosition = localPosition;
            var toCenter = new Vector3(-localPosition.x, 0f, -localPosition.z);
            transform.localRotation = toCenter.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
            state = standing ? State.Ready : State.Walking;
            stateElapsed = 0f;
            walkElapsed = 0f;
            hurtLeft = 0f;
            body.localRotation = Quaternion.identity;
            ShowDraw(0f);
            SetHurt(false);
        }

        /// <summary>Advances this enemy; meleeRadius is where a rusher lands its hit, measured from the lane center.</summary>
        public Signal Tick(float deltaTime, float meleeRadius)
        {
            UpdateHurt(deltaTime);
            stateElapsed += deltaTime;
            return state switch
            {
                State.Walking => Walk(deltaTime, meleeRadius),
                State.Ready => stateElapsed >= settings.firstDrawDelay ? Enter(State.Drawing) : Signal.None,
                State.Drawing => Draw(),
                _ => stateElapsed >= settings.reloadSeconds ? Enter(State.Drawing) : Signal.None,
            };
        }

        /// <summary>True on the hit that breaks the enemy.</summary>
        public bool Damage(float amount)
        {
            if (!IsAlive) return false;
            Health -= amount;
            hurtLeft = hurtFlashSeconds;
            SetHurt(true);
            return Health <= 0f;
        }

        public void Release()
        {
            Health = 0f;
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Behaviour

        Signal Walk(float deltaTime, float meleeRadius)
        {
            var position = transform.localPosition;
            var flat = new Vector2(position.x, position.z);
            var distance = flat.magnitude;
            var stopAt = type == MechaEnemyType.Rusher ? meleeRadius : settings.stopDistance;
            if (distance <= stopAt)
            {
                body.localRotation = Quaternion.identity;
                return type == MechaEnemyType.Rusher ? Signal.Arrived : Enter(State.Ready);
            }

            var step = Mathf.Min(settings.speed * deltaTime, distance - stopAt);
            flat -= flat / distance * step;
            transform.localPosition = new Vector3(flat.x, position.y, flat.y);
            walkElapsed += deltaTime;
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkElapsed * waddleHz * Mathf.PI * 2f) * waddleDegrees);
            return Signal.None;
        }

        Signal Draw()
        {
            ShowDraw(Mathf.Clamp01(stateElapsed / settings.drawSeconds));
            if (stateElapsed < settings.drawSeconds) return Signal.None;
            ShowDraw(0f);
            Enter(State.Reloading);
            return Signal.Fire;
        }

        Signal Enter(State next)
        {
            state = next;
            stateElapsed = 0f;
            return Signal.None;
        }

        void ShowDraw(float amount)
        {
            if (bow != null) bow.localRotation = Quaternion.Euler(Mathf.Lerp(50f, -10f, amount), 0f, 0f);
            if (nockedArrow != null) nockedArrow.SetActive(amount > 0f);
        }

        void UpdateHurt(float deltaTime)
        {
            if (hurtLeft <= 0f) return;
            hurtLeft -= deltaTime;
            if (hurtLeft <= 0f) SetHurt(false);
        }

        void SetHurt(bool hurt)
        {
            for (var i = 0; i < flashRenderers.Length; i++) flashRenderers[i].sharedMaterial = hurt ? hurtMaterial : baseMaterials[i];
        }

        #endregion
    }
}
