#nullable enable

using System;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One Forge control: a keyboard-navigable button whose label shows the value Enter changes.</summary>
    public class ForgeButton : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] Button button = null!;
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Background")]
        [SerializeField] Image background = null!;
        [Header("Normal Color")]
        [SerializeField] Color normalColor = Color.white;
        [Header("Selected Color")]
        [SerializeField] Color selectedColor = new(1f, 0.85f, 0.3f);
        [Header("Unavailable Color")]
        [SerializeField] Color unavailableColor = new(0.55f, 0.55f, 0.6f);

        public event Action? Clicked;

        #region Initialization

        void Awake()
        {
            button.onClick.AddListener(HandleClick);
        }

        #endregion

        #region Public API

        /// <param name="available">False dims it; pressing it still raises Clicked so the owner can say why.</param>
        public void Set(LocalizedString text, bool available = true, bool selected = false)
        {
            label.StringReference = text;
            background.color = !available ? unavailableColor : selected ? selectedColor : normalColor;
        }

        public void Tint(Color color)
        {
            background.color = color;
        }

        #endregion

        #region Unity Events

        void HandleClick() => Clicked?.Invoke();

        #endregion
    }
}
