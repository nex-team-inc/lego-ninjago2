#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One block of the level in the HUD strip: its type's icon, lit while current, dimmed once done.</summary>
    public class StripIcon : MonoBehaviour
    {
        [Header("Icon")]
        [SerializeField] Image icon = null!;
        [Header("Frame")]
        [SerializeField] Image frame = null!;
        [Header("Current Scale")]
        [SerializeField] float currentScale = 1.3f;

        Color color;

        #region Public API

        public void Initialize(Sprite? sprite, Color typeColor)
        {
            icon.sprite = sprite;
            color = typeColor;
        }

        /// <param name="state">-1 done, 0 current, 1 still ahead.</param>
        public void SetState(int state)
        {
            var current = state == 0;
            icon.color = state < 0 ? new Color(color.r, color.g, color.b, 0.35f) : current ? color : new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 0.8f);
            frame.color = current ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            transform.localScale = Vector3.one * (current ? currentScale : 1f);
        }

        #endregion
    }
}
