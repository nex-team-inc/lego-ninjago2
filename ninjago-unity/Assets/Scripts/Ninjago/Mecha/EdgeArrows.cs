#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The big arrows on the edge of one player's half, one per enemy that player's camera does not see, pointing the way
    /// to turn. An archer that is drawing or has an arrow in the air pulses. Fill once per frame: Begin, Point, End.
    /// </summary>
    public class EdgeArrows : MonoBehaviour
    {
        [Header("Area")]
        [Tooltip("The rect the lane camera's picture fills.")]
        [SerializeField] RectTransform area = null!;
        [Header("Arrow Prefab")]
        [Tooltip("Points along +X; anchored and pivoted at the area center.")]
        [SerializeField] Image arrowPrefab = null!;
        [Header("Edge Inset")]
        [Tooltip("Canvas units from the area edge to the arrow center.")]
        [SerializeField] float inset = 150f;
        [Header("Rusher Color")]
        [SerializeField] Color rusherColor = new(1f, 0.45f, 0.1f);
        [Header("Archer Color")]
        [SerializeField] Color archerColor = new(0.75f, 0.3f, 1f);
        [Header("Danger Pulse Hz")]
        [SerializeField] float pulseHz = 4f;
        [Header("Danger Pulse Scale")]
        [SerializeField] float pulseScale = 0.3f;

        readonly List<Image> arrows = new();
        int used;

        #region Public API

        public void Begin()
        {
            used = 0;
        }

        /// <summary>Shows an arrow toward target unless the camera already sees it.</summary>
        public void Point(Camera laneCamera, Vector3 target, bool archer, bool danger)
        {
            var viewport = laneCamera.WorldToViewportPoint(target);
            if (viewport.z > 0f && viewport.x is >= 0f and <= 1f && viewport.y is >= 0f and <= 1f) return;
            var direction = Direction(laneCamera, target, viewport);
            var half = area.rect.size * 0.5f - new Vector2(inset, inset);
            var scale = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(direction.x), 1e-4f), half.y / Mathf.Max(Mathf.Abs(direction.y), 1e-4f));
            var arrow = Take();
            arrow.rectTransform.anchoredPosition = direction * scale;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var pulse = danger ? 1f + pulseScale * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f)) : 1f;
            arrow.rectTransform.localScale = Vector3.one * pulse;
            arrow.color = archer ? archerColor : rusherColor;
        }

        public void End()
        {
            for (var i = used; i < arrows.Count; i++)
            {
                if (arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
            }
        }

        #endregion

        #region Helpers

        // In front but off screen: the way from the picture center to the target. Behind: down the screen, leaning to
        // the side the target is on.
        Vector2 Direction(Camera laneCamera, Vector3 target, Vector3 viewport)
        {
            var local = laneCamera.transform.InverseTransformPoint(target);
            var direction = local.z > 0f
                ? new Vector2((viewport.x - 0.5f) * laneCamera.aspect, viewport.y - 0.5f)
                : new Vector2(local.x, -Mathf.Abs(local.z));
            return direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.down;
        }

        Image Take()
        {
            if (used == arrows.Count) arrows.Add(Instantiate(arrowPrefab, area));
            var arrow = arrows[used++];
            if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
            return arrow;
        }

        #endregion
    }
}
