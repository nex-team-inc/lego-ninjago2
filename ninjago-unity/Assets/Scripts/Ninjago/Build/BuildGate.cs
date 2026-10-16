#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The gate on the far side of the river: its two doors swing open when the bridge is done.</summary>
    public class BuildGate : MonoBehaviour
    {
        [Header("Left Door")]
        [Tooltip("Hinged on its outer edge.")]
        [SerializeField] Transform leftDoor = null!;
        [Header("Right Door")]
        [SerializeField] Transform rightDoor = null!;
        [Header("Open Seconds")]
        [SerializeField] float openSeconds = 1f;
        [Header("Open Degrees")]
        [SerializeField] float openDegrees = 105f;

        float elapsed = float.PositiveInfinity;

        #region Public API

        public void Open()
        {
            elapsed = 0f;
        }

        #endregion

        #region Animation

        void Update()
        {
            if (elapsed > openSeconds) return;
            elapsed += Time.deltaTime;
            // Overshoots a little and settles, like a toy door flung open.
            var t = Mathf.Clamp01(elapsed / openSeconds);
            var angle = openDegrees * (1f + 0.15f * Mathf.Sin(t * Mathf.PI)) * Mathf.SmoothStep(0f, 1f, t);
            leftDoor.localRotation = Quaternion.Euler(0f, -angle, 0f);
            rightDoor.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        #endregion
    }
}
