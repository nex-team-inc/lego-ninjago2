#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One level on the progress path: its name, done (with its best stars), next (highlighted) or still ahead.</summary>
    public class RestNode : MonoBehaviour
    {
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Disc")]
        [SerializeField] Image disc = null!;
        [Header("Done Mark")]
        [SerializeField] GameObject doneMark = null!;
        [Header("Next Highlight")]
        [SerializeField] GameObject nextHighlight = null!;
        [Header("Stars")]
        [SerializeField] Image[] stars = null!;
        [Header("Done Color")]
        [SerializeField] Color doneColor = new(0.3f, 0.8f, 0.4f);
        [Header("Next Color")]
        [SerializeField] Color nextColor = new(1f, 0.8f, 0.2f);
        [Header("Ahead Color")]
        [SerializeField] Color aheadColor = new(0.35f, 0.35f, 0.42f);
        [Header("Boss Color")]
        [SerializeField] Color bossColor = new(0.6f, 0.25f, 0.8f);
        [Header("Star Off Color")]
        [SerializeField] Color starOffColor = new(1f, 1f, 1f, 0.15f);

        #region Public API

        /// <param name="state">-1 done, 0 next, 1 ahead.</param>
        public void Set(LocalizedString name, bool boss, int state, int bestStars)
        {
            label.StringReference = name;
            disc.color = state < 0 ? doneColor : state == 0 ? nextColor : boss ? bossColor : aheadColor;
            doneMark.SetActive(state < 0);
            nextHighlight.SetActive(state == 0);
            transform.localScale = Vector3.one * (state == 0 ? 1.2f : boss ? 1.1f : 1f);
            for (var i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(state < 0);
                stars[i].color = i < bestStars ? new Color(1f, 0.85f, 0.25f) : starOffColor;
            }
        }

        #endregion
    }
}
