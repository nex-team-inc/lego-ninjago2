#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A bridge brick: it hops from a pile into its slot in the builder's color, and lands with a squash.</summary>
    public class BuildBrick : MonoBehaviour
    {
        [Header("Tint Renderers")]
        [SerializeField] Renderer[] tintRenderers = null!;
        [Header("Arc Height")]
        [SerializeField] float arcHeight = 2f;

        Vector3 from;
        Vector3 to;
        float elapsed;
        float seconds;
        float landedElapsed = float.PositiveInfinity;
        bool flying;

        public event Action<BuildBrick>? Landed;

        /// <summary>Its place on the bridge (row * columns + column).</summary>
        public int Slot { get; private set; }

        #region Public API

        public void Fly(int slot, Vector3 fromWorld, Vector3 toWorld, float aSeconds, Color color)
        {
            Slot = slot;
            from = fromWorld;
            to = toWorld;
            seconds = aSeconds;
            elapsed = 0f;
            flying = true;
            transform.position = fromWorld;
            foreach (var tintRenderer in tintRenderers) tintRenderer.material.color = color;
        }

        #endregion

        #region Animation

        void Update()
        {
            if (flying)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));
                transform.localRotation = Quaternion.Euler(0f, (1f - t) * 180f, 0f);
                if (t < 1f) return;
                flying = false;
                landedElapsed = 0f;
                Landed?.Invoke(this);
                return;
            }

            if (landedElapsed > 0.3f) return;
            landedElapsed += Time.deltaTime;
            var squash = Mathf.Sin(Mathf.Clamp01(landedElapsed / 0.3f) * Mathf.PI) * 0.25f;
            transform.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
        }

        #endregion
    }
}
