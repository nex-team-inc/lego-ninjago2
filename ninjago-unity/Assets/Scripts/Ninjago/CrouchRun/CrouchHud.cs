#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's half of the Crouch Run HUD: the big two-line zone banner, the clock, the level, times heard, the
    /// HEARD flash and the LEVEL / CLEARED / TIME UP center banner.
    /// </summary>
    public class CrouchHud : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Banner Panel")]
        [SerializeField] Image bannerPanel = null!;
        [Header("Banner Title")]
        [SerializeField] NexLocalizedString bannerTitle = null!;
        [Header("Banner Instruction")]
        [SerializeField] NexLocalizedString bannerInstruction = null!;
        [Header("Clock Label")]
        [SerializeField] NexLocalizedString clockLabel = null!;
        [Header("Clock Text Component")]
        [SerializeField] TMPro.TMP_Text clockText = null!;
        [Header("Clock Text")]
        [Tooltip("Smart string with {seconds}.")]
        [SerializeField] LocalizedString clockString = new();
        [Header("Clock Low Seconds")]
        [Tooltip("The clock turns red at or below this many seconds.")]
        [SerializeField] float lowSeconds = 10f;
        [Header("Clock Low Color")]
        [SerializeField] Color lowColor = new(1f, 0.3f, 0.25f);
        [Header("Level Label")]
        [SerializeField] NexLocalizedString levelLabel = null!;
        [Header("Level Text")]
        [Tooltip("Smart string with {level}.")]
        [SerializeField] LocalizedString levelString = new();
        [Header("Heard Count Label")]
        [SerializeField] NexLocalizedString heardCountLabel = null!;
        [Header("Heard Count Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString heardCountString = new();
        [Header("Heard Flash")]
        [Tooltip("The big HEARD word.")]
        [SerializeField] RectTransform heardFlash = null!;
        [Header("Heard Flash Seconds")]
        [SerializeField] float heardFlashSeconds = 1.1f;
        [Header("Center Banner")]
        [SerializeField] NexLocalizedString centerBanner = null!;
        [Header("Cleared Text")]
        [SerializeField] LocalizedString clearedString = new();
        [Header("Time Up Text")]
        [SerializeField] LocalizedString timeUpString = new();
        [Header("Compact Elements")]
        [Tooltip("Scaled down (about their top pivots) when two players share the screen as half-height strips.")]
        [SerializeField] RectTransform[] compactElements = null!;
        [Header("Compact Scale")]
        [SerializeField, Range(0.3f, 1f)] float compactScale = 0.62f;

        float heardStart = float.NegativeInfinity;
        float heardRestScale = 1f;
        int shownSeconds = -1;

        #region Initialization

        public void Initialize(int playerIndex, Color color, bool compact)
        {
            playerTag.Initialize(playerIndex, color);
            if (compact)
            {
                foreach (var element in compactElements) element.localScale = Vector3.one * compactScale;
                heardRestScale = compactScale;
            }

            heardFlash.gameObject.SetActive(false);
            centerBanner.gameObject.SetActive(false);
            SetHeardCount(0);
        }

        #endregion

        #region Public API

        public void SetLevel(int levelNumber)
        {
            levelLabel.StringReference = LocalizedStrings.WithArguments(levelString, ("level", levelNumber));
        }

        public void SetClock(float secondsLeft)
        {
            var seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            clockLabel.StringReference = LocalizedStrings.WithArguments(clockString, ("seconds", seconds));
            clockText.color = seconds <= lowSeconds ? lowColor : Color.white;
        }

        public void ShowZone(CrouchRunConfig.ZoneLook look)
        {
            bannerPanel.gameObject.SetActive(true);
            bannerTitle.StringReference = look.title;
            bannerInstruction.StringReference = look.instruction;
            bannerPanel.color = look.color;
        }

        public void HideZone()
        {
            bannerPanel.gameObject.SetActive(false);
        }

        public void SetHeardCount(int count)
        {
            heardCountLabel.StringReference = LocalizedStrings.WithArguments(heardCountString, ("count", count));
        }

        public void FlashHeard()
        {
            heardStart = Time.unscaledTime;
            heardFlash.gameObject.SetActive(true);
        }

        /// <summary>LEVEL n before a level starts.</summary>
        public void ShowLevelBanner(int levelNumber)
        {
            ShowCenter(LocalizedStrings.WithArguments(levelString, ("level", levelNumber)));
        }

        public void ShowCleared() => ShowCenter(clearedString);

        public void ShowTimeUp() => ShowCenter(timeUpString);

        public void HideCenter()
        {
            centerBanner.gameObject.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (!heardFlash.gameObject.activeSelf) return;
            var t = (Time.unscaledTime - heardStart) / heardFlashSeconds;
            if (t >= 1f)
            {
                heardFlash.gameObject.SetActive(false);
                return;
            }

            heardFlash.localScale = Vector3.one * (heardRestScale * (1f + 0.5f * (1f - Mathf.Clamp01(t * 4f))));
        }

        #endregion

        #region Helpers

        void ShowCenter(LocalizedString text)
        {
            centerBanner.StringReference = text;
            centerBanner.gameObject.SetActive(true);
        }

        #endregion
    }
}
