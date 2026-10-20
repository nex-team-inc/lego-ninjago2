#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A blocky brick monster (or, scaled up, the Boss): the Stone Kick thrower rig winds up and throws, the Slip and
    /// Spin staff rig cocks and swings, and on defeat it bursts into bricks. A ring at its feet in a player's color marks
    /// whose threat it is.
    /// </summary>
    public class ChapterEnemy : MonoBehaviour
    {
        [Header("Throw Rig")]
        [SerializeField] BossRig throwRig = null!;
        [Header("Staff Rig")]
        [SerializeField] BruteRig staffRig = null!;
        [Header("Staff")]
        [Tooltip("Shown only while it swings.")]
        [SerializeField] GameObject staff = null!;
        [Header("Owner Ring")]
        [SerializeField] Renderer ownerRing = null!;
        [Header("Body Renderers")]
        [SerializeField] Renderer[] bodyRenderers = null!;
        [Header("Center")]
        [SerializeField] Transform center = null!;
        [Header("Burst Scale")]
        [Tooltip("Size of the brick burst; the Boss bursts bigger.")]
        [SerializeField] float burstScale = 1f;
        [Header("Flee Speed")]
        [SerializeField] float fleeSpeed = 9f;

        public BossRig Thrower => throwRig;
        public BruteRig Staff => staffRig;
        public Vector3 Center => center.position;
        public Vector3 HandPosition => throwRig.HandPosition;
        public bool IsAlive { get; private set; } = true;

        #region Initialization

        public void Initialize(Color bodyColor)
        {
            foreach (var bodyRenderer in bodyRenderers) bodyRenderer.material.color = bodyColor;
            throwRig.Initialize(0, Color.white);
            staffRig.SetStaff(0f, 50f);
            staffRig.SetTrail(false);
            staff.SetActive(false);
            SetOwner(null, Color.white);
        }

        #endregion

        #region Public API

        /// <summary>Marks whose threat this is; null clears the ring.</summary>
        public void SetOwner(int? playerIndex, Color color)
        {
            ownerRing.gameObject.SetActive(playerIndex.HasValue);
            if (playerIndex.HasValue) ownerRing.material.color = color;
        }

        public void Spawn(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            IsAlive = true;
        }

        public void ShowStaff(bool visible)
        {
            staff.SetActive(visible);
            if (!visible) staffRig.SetTrail(false);
        }

        public void Tick(float deltaTime) => staffRig.Tick(deltaTime);

        public void PlayHit()
        {
            throwRig.PlayFlinch();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BossHit, Center, Quaternion.identity, burstScale);
        }

        /// <summary>The signature brick burst: the monster breaks into bricks and is gone.</summary>
        public void Burst()
        {
            IsAlive = false;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, Center, Quaternion.identity, burstScale);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.PieceHit);
            gameObject.SetActive(false);
        }

        public async UniTask FleeAsync(Vector3 direction, float seconds, CancellationToken cancellationToken)
        {
            if (!gameObject.activeSelf) return;
            // The model faces its local -Z, toward the ninjas.
            transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                transform.position += direction.normalized * (fleeSpeed * Time.deltaTime);
                transform.position += Vector3.up * (Mathf.Abs(Mathf.Sin(elapsed * 14f)) * 0.04f);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            gameObject.SetActive(false);
        }

        #endregion
    }
}
