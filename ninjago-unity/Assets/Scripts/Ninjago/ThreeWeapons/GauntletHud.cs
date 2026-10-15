#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's half of the Three Weapons HUD: tag, fight counter, the PICK A WEAPON prompt, the weapon's gesture
    /// word (SLASH / SPIN / THROW) in the same top slot so it never covers the monster, and the cleared banner. The
    /// other gesture hints live on the ninja.
    /// </summary>
    public class GauntletHud : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Fight Label")]
        [SerializeField] NexLocalizedString fightLabel = null!;
        [Header("Fight Text")]
        [Tooltip("Smart string with {current} {total}.")]
        [SerializeField] LocalizedString fightString = new();
        [Header("Pick Prompt")]
        [SerializeField] GameObject pickPrompt = null!;
        [Header("Gesture Prompt")]
        [SerializeField] NexLocalizedString gesturePrompt = null!;
        [Header("Cleared Banner")]
        [SerializeField] GameObject clearedBanner = null!;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            playerTag.Initialize(playerIndex, color);
            SetFight(1, 3);
            pickPrompt.SetActive(false);
            gesturePrompt.gameObject.SetActive(false);
            clearedBanner.SetActive(false);
        }

        #endregion

        #region Public API

        public void SetFight(int current, int total)
        {
            fightLabel.StringReference = LocalizedStrings.WithArguments(fightString, ("current", current), ("total", total));
        }

        public void ShowPick(bool visible)
        {
            pickPrompt.SetActive(visible);
        }

        public void ShowGesture(LocalizedString prompt)
        {
            gesturePrompt.StringReference = prompt;
            gesturePrompt.gameObject.SetActive(true);
        }

        public void HideGesture()
        {
            gesturePrompt.gameObject.SetActive(false);
        }

        public void ShowCleared()
        {
            clearedBanner.SetActive(true);
        }

        #endregion
    }
}
