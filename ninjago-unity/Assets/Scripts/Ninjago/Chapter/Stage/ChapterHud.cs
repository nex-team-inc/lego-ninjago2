#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The HUD frame of a level, the same from the first block to the last: each player's hearts, a strip with one icon
    /// per block (the current one lit), the one-line story caption, and the corner camera preview with player
    /// indicators. Prompts live in the world, not here.
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

        readonly List<StripIcon> icons = new();
        PlayerHearts hearts = null!;
        float captionUntil;

        #region Initialization

        public void Initialize(DetectionManager detection, int requestedPlayers, PlayerHearts aHearts)
        {
            hearts = aHearts;
            hearts.Changed += HandleHeartsChanged;
            pipFrame.Initialize(detection.PlayAreaController);
            captionPanel.SetActive(false);
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

        #endregion

        #region Life Cycle

        void Update()
        {
            if (captionPanel.activeSelf && Time.unscaledTime >= captionUntil) captionPanel.SetActive(false);
        }

        void HandleHeartsChanged(int playerIndex, int left)
        {
            if (playerIndex < heartsRows.Length) heartsRows[playerIndex].Set(left, hearts.Max);
        }

        #endregion
    }
}
