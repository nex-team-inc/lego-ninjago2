#nullable enable

using System;
using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// One Forge control: a keyboard-navigable button whose label shows the value Enter changes. The background belongs
    /// to the keyboard focus highlight, so availability shows in the label color and selection in an accent bar.
    /// </summary>
    public class ForgeButton : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] Button button = null!;
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Label Text")]
        [SerializeField] TMP_Text text = null!;
        [Header("Accent Bar")]
        [SerializeField] Image accent = null!;
        [Header("Available Text Color")]
        [SerializeField] Color availableColor = new(0.1f, 0.1f, 0.12f);
        [Header("Unavailable Text Color")]
        [SerializeField] Color unavailableColor = new(0.62f, 0.62f, 0.66f);
        [Header("Selected Accent Color")]
        [SerializeField] Color selectedColor = new(1f, 0.55f, 0.1f);

        public event Action? Clicked;

        #region Initialization

        void Awake()
        {
            button.onClick.AddListener(HandleClick);
        }

        #endregion

        #region Public API

        /// <param name="available">False dims the label; pressing it still raises Clicked so the owner can say why.</param>
        public void Set(LocalizedString line, bool available = true, bool selected = false)
        {
            label.StringReference = line;
            text.color = available ? availableColor : unavailableColor;
            accent.color = selected ? selectedColor : Color.clear;
        }

        /// <summary>Shows a color under the label (a block type's color in the strip), unless it is the selected one.</summary>
        public void Tint(Color color)
        {
            accent.color = color;
        }

        #endregion

        #region Unity Events

        void HandleClick() => Clicked?.Invoke();

        #endregion
    }
}
