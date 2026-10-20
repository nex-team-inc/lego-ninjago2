#nullable enable

using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// A short line in the world (a hint word, KICK x3, HEARD, a zone sign) on a small world-space canvas that faces
    /// the camera, in the color of the player it is for. Prompts live in the world, never on a separate screen.
    /// </summary>
    public class WorldPrompt : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] GameObject root = null!;
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Label Text")]
        [SerializeField] TMP_Text text = null!;
        [Header("Panel")]
        [SerializeField] Image panel = null!;
        [Header("Second Line")]
        [Tooltip("Optional smaller line under the label, e.g. STAND TO RUN under CLEAR.")]
        [SerializeField] NexLocalizedString detail = null!;
        [Header("Pop Seconds")]
        [SerializeField] float popSeconds = 0.25f;

        Camera facing = null!;
        Vector3 restScale;
        Vector3 restLocalPosition;
        float shownAt;
        float hideAt = float.PositiveInfinity;

        public bool IsShown => root.activeSelf;

        #region Initialization

        public void Initialize(Camera camera)
        {
            facing = camera;
            restScale = root.transform.localScale;
            restLocalPosition = transform.localPosition;
            root.SetActive(false);
        }

        #endregion

        #region Public API

        /// <param name="seconds">0 keeps it up until Hide.</param>
        /// <param name="at">A world position to show it at instead of its own place (it goes back on Hide).</param>
        public void Show(LocalizedString line, Color color, float seconds = 0f, LocalizedString? second = null, Vector3? at = null)
        {
            if (at.HasValue) transform.position = at.Value;
            label.StringReference = line;
            text.color = Color.white;
            panel.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.92f);
            // A label must have its text before it first enables.
            if (second != null) detail.StringReference = second;
            detail.gameObject.SetActive(second != null);
            if (!root.activeSelf) shownAt = Time.unscaledTime;
            root.SetActive(true);
            hideAt = seconds > 0f ? Time.unscaledTime + seconds : float.PositiveInfinity;
        }

        public void Hide()
        {
            root.SetActive(false);
            transform.localPosition = restLocalPosition;
        }

        #endregion

        #region Life Cycle

        void LateUpdate()
        {
            if (!root.activeSelf) return;
            if (Time.unscaledTime >= hideAt)
            {
                Hide();
                return;
            }

            var toCamera = transform.position - facing.transform.position;
            if (toCamera.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            var pop = 1f - Mathf.Clamp01((Time.unscaledTime - shownAt) / popSeconds);
            root.transform.localScale = restScale * (1f + 0.35f * pop);
        }

        #endregion
    }
}
