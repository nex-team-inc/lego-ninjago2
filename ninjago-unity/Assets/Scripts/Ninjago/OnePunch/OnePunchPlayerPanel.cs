#nullable enable

using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's corner of the One Punch HUD: the tag, the health bar, the four try counts and the last try's word.</summary>
    public class OnePunchPlayerPanel : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Health Fill")]
        [SerializeField] Image healthFill = null!;
        [Header("Health Full Color")]
        [SerializeField] Color healthFullColor = new(0.3f, 0.9f, 0.35f);
        [Header("Health Low Color")]
        [SerializeField] Color healthLowColor = new(1f, 0.25f, 0.2f);
        [Header("Counts Label")]
        [SerializeField] NexLocalizedString countsLabel = null!;
        [Header("Counts Text")]
        [Tooltip("Smart string with {ext} {cam} {slash} {none}.")]
        [SerializeField] LocalizedString countsString = new();
        [Header("Signal Label")]
        [SerializeField] NexLocalizedString signalLabel = null!;
        [Header("Signal Text Component")]
        [SerializeField] TMP_Text signalText = null!;

        float signalShownAt = float.NegativeInfinity;
        float signalSeconds;

        #region Initialization

        public void Initialize(StreetPlayer player, float aSignalSeconds)
        {
            signalSeconds = aSignalSeconds;
            playerTag.Initialize(player.PlayerIndex, player.Color);
            signalLabel.gameObject.SetActive(false);
            SetHealth(1f);
            SetCounts(player);
        }

        #endregion

        #region Public API

        public void SetHealth(float fraction)
        {
            healthFill.fillAmount = fraction;
            healthFill.color = Color.Lerp(healthLowColor, healthFullColor, fraction);
        }

        public void SetCounts(StreetPlayer player)
        {
            countsLabel.StringReference = LocalizedStrings.WithArguments(countsString, ("ext", player.Tries(PunchSignal.Extension)),
                ("cam", player.Tries(PunchSignal.TowardCamera)), ("slash", player.Tries(PunchSignal.SideSlash)), ("none", player.Tries(PunchSignal.None)));
        }

        public void FlashSignal(OnePunchConfig.SignalLook look)
        {
            signalLabel.StringReference = look.label;
            signalText.color = look.color;
            signalLabel.gameObject.SetActive(true);
            signalShownAt = Time.unscaledTime;
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (signalLabel.gameObject.activeSelf && Time.unscaledTime - signalShownAt > signalSeconds) signalLabel.gameObject.SetActive(false);
        }

        #endregion
    }
}
