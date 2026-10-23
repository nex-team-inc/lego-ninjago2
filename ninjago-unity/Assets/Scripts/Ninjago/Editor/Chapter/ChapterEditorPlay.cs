#nullable enable

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// "Play from here" for chapter assets: enters Play Mode from the main scene, picks the mode in the real menu and
    /// sets the Debug Settings jump to the level and block, so the start view's Start lands right there.
    /// </summary>
    [InitializeOnLoad]
    public static class ChapterEditorPlay
    {
        const string ModeKey = "ChapterEditorPlay.Mode";
        const string LevelKey = "ChapterEditorPlay.Level";
        const string BlockKey = "ChapterEditorPlay.Block";

        static ChapterEditorPlay()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        #region Public API

        public static void Request(string modeId, int levelIndex, int blockIndex)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(ModeKey, modeId);
            SessionState.SetInt(LevelKey, levelIndex);
            SessionState.SetInt(BlockKey, blockIndex);
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            EditorApplication.EnterPlaymode();
        }

        #endregion

        #region Helpers

        static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(ModeKey, ""))) EditorApplication.update += Poll;
            if (change == PlayModeStateChange.ExitingPlayMode) Clear();
        }

        // Waits for the menu to settle, then sets the jump and presses the mode's entry like a player would.
        static void Poll()
        {
            if (!EditorApplication.isPlaying)
            {
                Clear();
                return;
            }

            var viewManager = Object.FindFirstObjectByType<ViewManager>();
            if (viewManager == null || viewManager.IsInTransition || viewManager.TopView is not GameModeSelectionView) return;
            var modeId = SessionState.GetString(ModeKey, "");
            foreach (var entry in Object.FindObjectsByType<GameModeSelectionButton>(FindObjectsSortMode.None))
            {
                if (entry.Mode != GameModeType.Chapter || entry.ChapterModeId != modeId) continue;
                var debug = PlayerDataManager.Instance.DebugSettings;
                debug.chapterJumpLevel = SessionState.GetInt(LevelKey, -1);
                debug.chapterJumpBlock = SessionState.GetInt(BlockKey, 0);
                entry.GetComponent<Button>().onClick.Invoke();
                Clear();
                return;
            }

            Debug.LogWarning($"[ChapterEditorPlay] Mode '{modeId}' is not in the menu; add it to the ChapterModeLibrary, or to the GameModeConfig playtest list when Playtest Only is on.");
            Clear();
        }

        static void Clear()
        {
            EditorApplication.update -= Poll;
            SessionState.EraseString(ModeKey);
        }

        #endregion
    }
}
