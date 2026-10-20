#nullable enable

using System;
using Nex.KeyboardNavigation;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex
{
    public class GameModeSelectionButton : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] Button button = null!;
        [SerializeField] ButtonKeyResponder keyResponder = null!;

        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;

        /// <summary>The mode, and the chapter mode id for Chapter entries (empty otherwise).</summary>
        public event Action<GameModeType, string>? Clicked;

        public KeyResponder KeyResponder => keyResponder;
        public GameModeType Mode => mode;
        public string ChapterModeId => chapterModeId;

        GameModeType mode;
        string chapterModeId = "";

        #region Initialization

        public void Initialize(GameModeType aMode, LocalizedString displayName, string aChapterModeId = "")
        {
            mode = aMode;
            chapterModeId = aChapterModeId;
            label.StringReference = displayName;
            button.onClick.AddListener(HandleClick);
        }

        #endregion

        #region Unity Events

        void HandleClick()
        {
            Clicked?.Invoke(mode, chapterModeId);
        }

        #endregion
    }
}
