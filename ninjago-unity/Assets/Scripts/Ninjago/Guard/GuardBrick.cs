#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A thrown toy brick. It flies an arc onto a moving target point, spinning; then it either bursts into bricks on a
    /// shield, or lands on the ninja's head as a silly hat and pops off a moment later. Pooled.
    /// </summary>
    public class GuardBrick : MonoBehaviour, IPoolableObject
    {
        [Header("Tint Renderers")]
        [SerializeField] Renderer[] tintRenderers = null!;
        [Header("Spin Degrees")]
        [Tooltip("Degrees per second the brick tumbles in flight.")]
        [SerializeField] float spinDegrees = 240f;

        Transform home = null!;
        Vector3 from;
        Transform? target;
        float seconds;
        float arcHeight;
        float elapsed;
        float hatLeft;
        bool flying;

        public event Action<Component>? OnRelease;

        public bool Arrived => !flying || elapsed >= seconds;
        public Vector3 Position => transform.position;

        #region Public API

        public void Fly(Transform aHome, Vector3 fromWorld, Transform aTarget, float aSeconds, float aArcHeight, float scale, Color color)
        {
            home = aHome;
            transform.SetParent(home, true);
            from = fromWorld;
            target = aTarget;
            seconds = aSeconds;
            arcHeight = aArcHeight;
            elapsed = 0f;
            hatLeft = 0f;
            flying = true;
            transform.SetPositionAndRotation(fromWorld, Quaternion.identity);
            transform.localScale = Vector3.one * scale;
            foreach (var tintRenderer in tintRenderers) tintRenderer.material.color = color;
        }

        /// <summary>Blocked: a shower of bricks and gone.</summary>
        public void Burst(float vfxScale)
        {
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, transform.position, Quaternion.identity, vfxScale);
            Release();
        }

        /// <summary>Missed: sits on top of the head, tipped, until it pops off.</summary>
        public void LandOn(Transform head, float aSeconds)
        {
            flying = false;
            target = null;
            hatLeft = aSeconds;
            transform.SetParent(head, true);
            transform.localPosition = new Vector3(0f, 0.12f, 0f);
            transform.localRotation = Quaternion.Euler(0f, 25f, 12f);
        }

        #endregion

        #region Animation

        void Update()
        {
            if (hatLeft > 0f)
            {
                hatLeft -= Time.deltaTime;
                if (hatLeft > 0f) return;
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickSnapPop, transform.position);
                Release();
                return;
            }

            if (!flying || target == null) return;
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / seconds);
            transform.position = Vector3.Lerp(from, target.position, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));
            transform.Rotate(new Vector3(0.6f, 1f, 0.3f), spinDegrees * Time.deltaTime, Space.Self);
        }

        void Release()
        {
            flying = false;
            target = null;
            hatLeft = 0f;
            transform.SetParent(home, true);
            OnRelease?.Invoke(this);
        }

        #endregion
    }
}
