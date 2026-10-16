#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A blocky monster standing outside a window, looking in; its head slowly scans along the corridor.</summary>
    public class CrouchMonster : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Breathes up and down.")]
        [SerializeField] Transform body = null!;
        [Header("Head")]
        [SerializeField] Transform head = null!;
        [Header("Tint Renderers")]
        [SerializeField] Renderer[] tintRenderers = null!;
        [Header("Scan Hz")]
        [SerializeField] float scanHz = 0.35f;
        [Header("Scan Degrees")]
        [SerializeField] float scanDegrees = 30f;

        float phase;

        #region Initialization

        public void Initialize(Color bodyColor, float scanPhase)
        {
            foreach (var tintRenderer in tintRenderers) tintRenderer.material.color = bodyColor;
            phase = scanPhase;
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            var t = Time.time * scanHz * Mathf.PI * 2f + phase;
            body.localPosition = new Vector3(0f, Mathf.Sin(t * 2f) * 0.03f, 0f);
            head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t) * scanDegrees, 0f);
        }

        #endregion
    }
}
