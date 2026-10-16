#nullable enable

using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The HUD shared by the children's games: a panel per player, a status line, a big center banner, a call-to-action
    /// prompt, a small hint line and one meter. Each game decides the words.
    /// </summary>
    public class KidsHud : MonoBehaviour
    {
        [Header("Player Panels")]
        [Tooltip("Left (slot 0) then right (slot 1).")]
        [SerializeField] KidsPlayerPanel[] panels = null!;
        [Header("Status Label")]
        [SerializeField] NexLocalizedString statusLabel = null!;
        [Header("Banner")]
        [SerializeField] NexLocalizedString banner = null!;
        [Header("Prompt")]
        [Tooltip("The big word for what to do now, e.g. BLOCK!")]
        [SerializeField] NexLocalizedString prompt = null!;
        [Header("Prompt Text Component")]
        [SerializeField] TMP_Text promptText = null!;
        [Header("Hint")]
        [SerializeField] NexLocalizedString hint = null!;
        [Header("Meter")]
        [SerializeField] GameObject meter = null!;
        [Header("Meter Fill")]
        [SerializeField] Image meterFill = null!;

        float bannerHideAt = float.PositiveInfinity;

        #region Initialization

        /// <param name="playerCount">Panels past this many stay hidden.</param>
        /// <param name="initialStatus">The status line from the first frame: a smart-string label with no reference throws when it starts.</param>
        public void Initialize(int playerCount, LocalizedString initialStatus)
        {
            for (var slot = 0; slot < panels.Length; slot++) panels[slot].gameObject.SetActive(slot < playerCount);
            statusLabel.StringReference = initialStatus;
            banner.gameObject.SetActive(false);
            prompt.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
            meter.SetActive(false);
        }

        #endregion

        #region Public API

        public KidsPlayerPanel Panel(int slot) => panels[slot];

        public void SetStatus(LocalizedString text)
        {
            statusLabel.StringReference = text;
        }

        /// <param name="seconds">Positive infinity keeps it up.</param>
        public void ShowBanner(LocalizedString text, float seconds)
        {
            banner.StringReference = text;
            banner.gameObject.SetActive(true);
            bannerHideAt = Time.time + seconds;
        }

        public void ShowPrompt(LocalizedString text, Color color)
        {
            prompt.StringReference = text;
            promptText.color = color;
            prompt.gameObject.SetActive(true);
        }

        public void HidePrompt()
        {
            prompt.gameObject.SetActive(false);
        }

        public void ShowHint(LocalizedString text)
        {
            hint.StringReference = text;
            hint.gameObject.SetActive(true);
        }

        public void HideHint()
        {
            hint.gameObject.SetActive(false);
        }

        public void SetMeter(float fill, Color color)
        {
            meter.SetActive(true);
            meterFill.fillAmount = fill;
            meterFill.color = color;
        }

        public void HideMeter()
        {
            meter.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (Time.time < bannerHideAt) return;
            banner.gameObject.SetActive(false);
            bannerHideAt = float.PositiveInfinity;
        }

        #endregion
    }
}
