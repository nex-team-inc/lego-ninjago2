#nullable enable

using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>One player's corner of a children's game HUD: the tag, one count, and a word flashed for the last gesture.</summary>
    public class KidsPlayerPanel : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Count Label")]
        [SerializeField] NexLocalizedString countLabel = null!;
        [Header("Flash Label")]
        [SerializeField] NexLocalizedString flashLabel = null!;
        [Header("Flash Text Component")]
        [SerializeField] TMP_Text flashText = null!;

        LocalizedString countTemplate = null!;
        float flashShownAt = float.NegativeInfinity;
        float flashSeconds;

        #region Initialization

        /// <param name="aCountTemplate">Smart string with {count}.</param>
        public void Initialize(int playerIndex, Color color, LocalizedString aCountTemplate, float aFlashSeconds)
        {
            countTemplate = aCountTemplate;
            flashSeconds = aFlashSeconds;
            playerTag.Initialize(playerIndex, color);
            flashLabel.gameObject.SetActive(false);
            SetCount(0);
        }

        #endregion

        #region Public API

        public void SetCount(int count)
        {
            countLabel.StringReference = LocalizedStrings.WithArguments(countTemplate, ("count", count));
        }

        public void Flash(LocalizedString word, Color color)
        {
            flashLabel.StringReference = word;
            flashText.color = color;
            flashLabel.gameObject.SetActive(true);
            flashShownAt = Time.unscaledTime;
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (flashLabel.gameObject.activeSelf && Time.unscaledTime - flashShownAt > flashSeconds) flashLabel.gameObject.SetActive(false);
        }

        #endregion
    }
}
