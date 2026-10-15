#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>A weapon card in front of the ninja: it fills while one of its player's cursors rests on it.</summary>
    public class WeaponCard : MonoBehaviour
    {
        [Header("Rect")]
        [SerializeField] RectTransform rect = null!;
        [Header("Background")]
        [SerializeField] Image background = null!;
        [Header("Icon")]
        [SerializeField] Image icon = null!;
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Hold Fill")]
        [SerializeField] Image holdFill = null!;

        public WeaponType Weapon { get; private set; }
        public float Progress { get; private set; }

        #region Initialization

        public void Initialize(WeaponType weapon, ThreeWeaponsConfig.WeaponSettings settings, Sprite iconSprite, Color playerColor, Vector2 localPosition)
        {
            Weapon = weapon;
            rect.anchoredPosition = localPosition;
            background.color = settings.color;
            icon.sprite = iconSprite;
            label.StringReference = settings.label;
            holdFill.color = playerColor;
            holdFill.fillAmount = 0f;
        }

        #endregion

        #region Public API

        public bool Contains(Vector2 localPosition)
        {
            var offset = localPosition - rect.anchoredPosition;
            var half = rect.rect.size * 0.5f;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
        }

        /// <summary>Fills while hovered and drains twice as fast otherwise; true on the frame it fills.</summary>
        public bool Tick(bool hovered, float deltaTime, float holdSeconds)
        {
            var step = deltaTime / holdSeconds;
            Progress = Mathf.Clamp01(Progress + (hovered ? step : -2f * step));
            holdFill.fillAmount = Progress;
            return Progress >= 1f;
        }

        #endregion
    }
}
