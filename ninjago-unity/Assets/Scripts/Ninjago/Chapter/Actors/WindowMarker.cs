#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The small in-world icon of a QTE window, in the owning player's color: a ring on the thrown rock or monster that
    /// drains as the window closes, or an arrow beside the ninja pointing to the safe side.
    /// </summary>
    public class WindowMarker : MonoBehaviour
    {
        [Header("Ring")]
        [SerializeField] Image ring = null!;
        [Header("Arrow")]
        [SerializeField] RectTransform arrow = null!;
        [Header("Arrow Image")]
        [SerializeField] Image arrowImage = null!;
        [Header("Pulse Hz")]
        [SerializeField] float pulseHz = 3f;

        Camera facing = null!;
        Transform? follow;
        Vector3 offset;

        #region Initialization

        public void Initialize(Camera camera)
        {
            facing = camera;
            Hide();
        }

        #endregion

        #region Public API

        public void ShowRing(Transform target, Color color)
        {
            follow = target;
            offset = Vector3.zero;
            ring.gameObject.SetActive(true);
            arrow.gameObject.SetActive(false);
            ring.color = color;
            ring.fillAmount = 1f;
            gameObject.SetActive(true);
        }

        /// <param name="side">-1 points left on screen, +1 right.</param>
        public void ShowArrow(Transform target, Vector3 targetOffset, float side, Color color)
        {
            follow = target;
            offset = targetOffset;
            ring.gameObject.SetActive(false);
            arrow.gameObject.SetActive(true);
            arrowImage.color = color;
            arrow.localRotation = Quaternion.Euler(0f, 0f, side < 0f ? 180f : 0f);
            gameObject.SetActive(true);
        }

        public void SetFill(float fill01)
        {
            ring.fillAmount = Mathf.Clamp01(fill01);
        }

        public void Hide()
        {
            follow = null;
            gameObject.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void LateUpdate()
        {
            if (follow == null) return;
            transform.position = follow.position + offset;
            var toCamera = transform.position - facing.transform.position;
            if (toCamera.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f));
        }

        #endregion
    }
}
