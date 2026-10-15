#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Draws every active player's two cursors over the world feeds. Stretch it over the whole screen and keep it last
    /// in its canvas so cursors stay on top of rocks, tiles and HUD.
    /// </summary>
    public class HandCursorLayer : MonoBehaviour
    {
        [Header("Layer Rect")]
        [Tooltip("Full-screen rect the cursors are positioned in.")]
        [SerializeField] RectTransform layer = null!;
        [Header("Cursor Prefab")]
        [SerializeField] HandCursorMark markPrefab = null!;

        readonly List<HandCursor> cursors = new();
        readonly List<HandCursorMark> marks = new();
        readonly HashSet<int> hiddenPlayers = new();

        #region Initialization

        public void Initialize(HandCursorTracker tracker, IReadOnlyList<PlayerBody> players)
        {
            foreach (var player in players)
            {
                for (var hand = 0; hand < 2; hand++)
                {
                    var mark = Instantiate(markPrefab, layer);
                    mark.Initialize(player.Color);
                    mark.gameObject.SetActive(false);
                    cursors.Add(tracker.GetCursor(player.PlayerIndex, hand));
                    marks.Add(mark);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>Layer-local position (pivot at the center) of a screen-normalized point.</summary>
        public Vector2 ToLocal(Vector2 screenPosition) => Vector2.Scale(screenPosition - new Vector2(0.5f, 0.5f), layer.rect.size);

        /// <summary>Shows or hides one player's cursors (the cursors keep tracking either way).</summary>
        public void SetPlayerVisible(int playerIndex, bool visible)
        {
            if (visible) hiddenPlayers.Remove(playerIndex);
            else hiddenPlayers.Add(playerIndex);
        }

        #endregion

        #region Life Cycle

        void LateUpdate()
        {
            for (var i = 0; i < cursors.Count; i++)
            {
                var cursor = cursors[i];
                var mark = marks[i];
                var shown = cursor.IsVisible && !hiddenPlayers.Contains(cursor.PlayerIndex);
                if (mark.gameObject.activeSelf != shown) mark.gameObject.SetActive(shown);
                if (shown) mark.Rect.anchoredPosition = ToLocal(cursor.ScreenPosition);
            }
        }

        #endregion
    }
}
