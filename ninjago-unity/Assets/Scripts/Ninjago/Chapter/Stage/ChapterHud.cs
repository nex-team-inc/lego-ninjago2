#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The HUD frame of a level, the same from the first block to the last: each player's hearts, a strip with one icon
    /// per block (the current one lit), the one-line story caption, the corner camera preview with player indicators,
    /// and a red edge glow that flashes when danger comes in. Prompts live in the world, not here.
    /// </summary>
    public class ChapterHud : MonoBehaviour
    {
        [Header("Hearts Rows")]
        [Tooltip("One per player index.")]
        [SerializeField] HeartsRow[] heartsRows = null!;
        [Header("Strip Root")]
        [SerializeField] RectTransform stripRoot = null!;
        [Header("Strip Icon Prefab")]
        [SerializeField] StripIcon stripIconPrefab = null!;
        [Header("Caption Panel")]
        [SerializeField] GameObject captionPanel = null!;
        [Header("Caption")]
        [SerializeField] NexLocalizedString caption = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;
        [Header("Danger Glow")]
        [Tooltip("Full-screen edge vignette, transparent in the middle.")]
        [SerializeField] Image dangerGlow = null!;
        [Header("Danger Fade Seconds")]
        [SerializeField] float dangerFadeSeconds = 0.7f;

        readonly List<StripIcon> icons = new();
        PlayerHearts hearts = null!;
        float captionUntil;
        float danger;

        #region Initialization

        public void Initialize(DetectionManager detection, int requestedPlayers, PlayerHearts aHearts)
        {
            hearts = aHearts;
            hearts.Changed += HandleHeartsChanged;
            pipFrame.Initialize(detection.PlayAreaController);
            captionPanel.SetActive(false);
            dangerGlow.enabled = false;
            pipIndicators.Initialize(requestedPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            hearts.Changed -= HandleHeartsChanged;
        }

        #endregion

        #region Public API

        public void SetPlayers(IReadOnlyList<PlayerBody> players)
        {
            foreach (var row in heartsRows) row.gameObject.SetActive(false);
            foreach (var player in players)
            {
                var row = heartsRows[player.PlayerIndex];
                row.gameObject.SetActive(true);
                row.Initialize(player.PlayerIndex, player.Color);
                row.Set(hearts.Get(player.PlayerIndex), hearts.Max);
            }
        }

        public void ShowLevel(LevelData level, ForgeCatalog catalog)
        {
            while (icons.Count < level.blocks.Count) icons.Add(Instantiate(stripIconPrefab, stripRoot));
            for (var i = 0; i < icons.Count; i++)
            {
                var used = i < level.blocks.Count;
                icons[i].gameObject.SetActive(used);
                if (!used) continue;
                var spec = catalog.Block(level.blocks[i].type);
                icons[i].Initialize(spec.icon, spec.color);
                icons[i].SetState(1);
            }

            captionPanel.SetActive(false);
        }

        public void SetCurrent(int blockIndex)
        {
            for (var i = 0; i < icons.Count; i++) icons[i].SetState(i < blockIndex ? -1 : i == blockIndex ? 0 : 1);
        }

        public void ShowCaption(LocalizedString line, float seconds)
        {
            caption.StringReference = line;
            captionPanel.SetActive(true);
            captionUntil = Time.unscaledTime + seconds;
        }

        /// <param name="strength">0..1; a hit flashes harder than a warning.</param>
        public void FlashDanger(float strength)
        {
            danger = Mathf.Max(danger, strength);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (captionPanel.activeSelf && Time.unscaledTime >= captionUntil) captionPanel.SetActive(false);
            danger = Mathf.MoveTowards(danger, 0f, Time.unscaledDeltaTime / dangerFadeSeconds);
            dangerGlow.enabled = danger > 0f;
            var color = dangerGlow.color;
            color.a = danger;
            dangerGlow.color = color;
        }

        void HandleHeartsChanged(int playerIndex, int left)
        {
            if (playerIndex < heartsRows.Length) heartsRows[playerIndex].Set(left, hearts.Max);
        }

        #endregion
    }
}
