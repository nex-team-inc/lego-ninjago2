#nullable enable

using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.Ninjago
{
    /// <summary>The Debug Settings rows' chapter actions: play any mode at once (with the level and block jump) and validate every mode.</summary>
    public static class ChapterDebugHooks
    {
        /// <summary>The seed Surprise me used last, shown in Debug Settings.</summary>
        public static int LastSeed { get; set; } = -1;

        #region Public API

        /// <summary>Loads the chapter game with the mode at this menu index (built-in first, then player-made).</summary>
        public static string PlayMode(int modeIndex)
        {
            var configs = GameConfigsManager.Instance;
            var modes = ChapterModes.All(configs.ChapterModes, PlayerDataManager.Instance.ChapterSave);
            if (modeIndex < 0 || modeIndex >= modes.Count) return $"No mode at index {modeIndex} ({modes.Count} modes).";
            configs.SelectedMode = GameModeType.Chapter;
            configs.SelectedChapterModeId = modes[modeIndex].id;
            LoadAsync().Forget();
            return $"Playing {modes[modeIndex].id}.";
        }

        /// <summary>Every mode through the same rules; one line per mode, then each problem.</summary>
        public static string ValidateAll()
        {
            var configs = GameConfigsManager.Instance;
            var report = new StringBuilder();
            foreach (var mode in ChapterModes.All(configs.ChapterModes, PlayerDataManager.Instance.ChapterSave))
            {
                var problems = ChapterValidator.Validate(mode.chapter, configs.ForgeCatalog, configs.ChapterTuning.LinkSeconds);
                report.AppendLine($"{mode.id}: {(problems.Count == 0 ? "valid" : $"{problems.Count} problem(s)")}");
                foreach (var problem in problems) report.AppendLine("  " + ChapterValidator.Describe(problem, configs.ForgeConfig).GetLocalizedString());
            }

            return report.ToString();
        }

        #endregion

        #region Helpers

        static async UniTaskVoid LoadAsync()
        {
            await ScreenBlockerManager.Instance.Show();
            SceneManager.LoadScene(GameConfigsManager.Instance.GameScene);
        }

        #endregion
    }
}
