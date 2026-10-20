#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's hearts in the HUD frame: that player's tag and a heart per heart left, empty hearts dimmed.</summary>
    public class HeartsRow : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Hearts Root")]
        [SerializeField] RectTransform heartsRoot = null!;
        [Header("Heart Prefab")]
        [SerializeField] Image heartPrefab = null!;
        [Header("Full Heart Color")]
        [SerializeField] Color fullColor = new(1f, 0.3f, 0.35f);
        [Header("Empty Heart Color")]
        [SerializeField] Color emptyColor = new(0.2f, 0.2f, 0.25f, 0.6f);

        readonly List<Image> hearts = new();

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            playerTag.Initialize(playerIndex, color);
        }

        #endregion

        #region Public API

        public void Set(int left, int max)
        {
            while (hearts.Count < max) hearts.Add(Instantiate(heartPrefab, heartsRoot));
            for (var i = 0; i < hearts.Count; i++)
            {
                hearts[i].gameObject.SetActive(i < max);
                hearts[i].color = i < left ? fullColor : emptyColor;
            }
        }

        #endregion
    }
}
