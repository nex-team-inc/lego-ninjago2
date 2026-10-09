#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's half of the Robot Mecha HUD: tag, hearts, which arm holds the L, time left, kills, the first ROBOT ARMS
    /// prompt, the hit flash, the end banners and the edge arrows.
    /// </summary>
    public class MechaHud : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Hearts Row")]
        [SerializeField] RectTransform heartsRow = null!;
        [Header("Heart Prefab")]
        [SerializeField] Image heartPrefab = null!;
        [Header("Full Heart Color")]
        [SerializeField] Color fullHeartColor = new(1f, 0.25f, 0.3f);
        [Header("Empty Heart Color")]
        [SerializeField] Color emptyHeartColor = new(0.15f, 0.15f, 0.15f, 0.5f);
        [Header("Left Arm Icon")]
        [SerializeField] Image leftArmIcon = null!;
        [Header("Right Arm Icon")]
        [SerializeField] Image rightArmIcon = null!;
        [Header("Arm Held Color")]
        [SerializeField] Color armHeldColor = new(0.3f, 1f, 0.35f);
        [Header("Arm Broken Color")]
        [SerializeField] Color armBrokenColor = new(0.55f, 0.55f, 0.6f, 0.9f);
        [Header("Time Label")]
        [SerializeField] NexLocalizedString timeLabel = null!;
        [Header("Time Text")]
        [Tooltip("Smart string with {seconds}.")]
        [SerializeField] LocalizedString timeText = new();
        [Header("Kills Label")]
        [SerializeField] NexLocalizedString killsLabel = null!;
        [Header("Kills Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString killsText = new();
        [Header("Robot Arms Prompt")]
        [Tooltip("The big L and ROBOT ARMS at the start of the fight.")]
        [SerializeField] GameObject robotArmsPrompt = null!;
        [Header("Hit Flash")]
        [SerializeField] Image hitFlash = null!;
        [Header("Hit Flash Seconds")]
        [SerializeField] float hitFlashSeconds = 0.45f;
        [Header("Down Banner")]
        [SerializeField] GameObject downBanner = null!;
        [Header("Survived Banner")]
        [SerializeField] GameObject survivedBanner = null!;
        [Header("Edge Arrows")]
        [SerializeField] EdgeArrows edgeArrows = null!;

        readonly List<Image> hearts = new();
        float hitFlashStart = float.NegativeInfinity;
        int shownSeconds = -1;

        public EdgeArrows EdgeArrows => edgeArrows;

        #region Initialization

        public void Initialize(int playerIndex, Color color, int maxHearts)
        {
            playerTag.Initialize(playerIndex, color);
            for (var i = 0; i < maxHearts; i++)
            {
                hearts.Add(Instantiate(heartPrefab, heartsRow));
            }

            SetHearts(maxHearts);
            SetArms(false, false);
            SetKills(0);
            ShowPrompt(false);
            hitFlash.enabled = false;
            downBanner.SetActive(false);
            survivedBanner.SetActive(false);
        }

        #endregion

        #region Public API

        public void SetHearts(int remaining)
        {
            for (var i = 0; i < hearts.Count; i++)
            {
                hearts[i].color = i < remaining ? fullHeartColor : emptyHeartColor;
            }
        }

        public void SetArms(bool leftRaised, bool rightRaised)
        {
            leftArmIcon.color = leftRaised ? armHeldColor : armBrokenColor;
            rightArmIcon.color = rightRaised ? armHeldColor : armBrokenColor;
        }

        public void SetTimeLeft(int seconds)
        {
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            timeLabel.StringReference = LocalizedStrings.WithArguments(timeText, ("seconds", seconds));
        }

        public void SetKills(int count)
        {
            killsLabel.StringReference = LocalizedStrings.WithArguments(killsText, ("count", count));
        }

        public void ShowPrompt(bool visible)
        {
            if (robotArmsPrompt.activeSelf != visible) robotArmsPrompt.SetActive(visible);
        }

        public void FlashHit()
        {
            hitFlashStart = Time.unscaledTime;
            hitFlash.enabled = true;
        }

        public void ShowDown()
        {
            ShowPrompt(false);
            downBanner.SetActive(true);
        }

        public void ShowSurvived()
        {
            ShowPrompt(false);
            survivedBanner.SetActive(true);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (!hitFlash.enabled) return;
            var t = (Time.unscaledTime - hitFlashStart) / hitFlashSeconds;
            if (t >= 1f)
            {
                hitFlash.enabled = false;
                return;
            }

            var color = hitFlash.color;
            color.a = 0.5f * (1f - t);
            hitFlash.color = color;
        }

        #endregion
    }
}
