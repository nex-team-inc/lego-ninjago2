#nullable enable

using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Creates or updates the Boss Battle config assets (same asset, same GUID on every run):
    /// Assets/Configs/Ninjago/BossBattle/BossBattleConfig.asset (GDD v2 defaults) and BossBattleVisualConfig.asset (class
    /// defaults; package R fills the values). An existing asset keeps its values and only gets missing entries filled;
    /// Run(true) rewrites the gameplay config to the defaults. GameConfigsManager.prefab is wired by the Boss Battle builder.
    /// CLI: unity command eval 'return Nex.Ninjago.Editor.BossBattleConfigBuilder.Run();'
    /// </summary>
    public static class BossBattleConfigBuilder
    {
        public const string Root = NinjagoEditorUtils.ConfigsRoot + "/BossBattle";
        public const string ConfigPath = Root + "/BossBattleConfig.asset";
        public const string VisualConfigPath = Root + "/BossBattleVisualConfig.asset";

        [MenuItem("Nex/Ninjago/Boss Battle/Build Config")]
        static void RunFromMenu() => Debug.Log(Run());

        [MenuItem("Nex/Ninjago/Boss Battle/Reset Config To Defaults")]
        static void ResetFromMenu() => Debug.Log(Run(true));

        #region Public API

        /// <param name="resetDefaults">Rewrite BossBattleConfig to the defaults (the visual config is never reset here).</param>
        public static string Run(bool resetDefaults = false)
        {
            var config = BuildConfig(resetDefaults, out var configReport);
            var visual = BuildVisualConfig(out var visualReport);
            AssetDatabase.SaveAssets();
            return $"[BossBattleConfigBuilder] {ConfigPath}: {configReport} (guid {Guid(config)}); {VisualConfigPath}: {visualReport} (guid {Guid(visual)})";
        }

        public static BossBattleConfig BuildConfig(bool resetDefaults, out string report)
        {
            var created = AssetDatabase.LoadAssetAtPath<BossBattleConfig>(ConfigPath) == null;
            var config = NinjagoEditorUtils.LoadOrCreate<BossBattleConfig>(ConfigPath);
            if (resetDefaults && !created)
            {
                config.ResetToDefaults();
                report = "reset to defaults";
            }
            else
            {
                var filled = config.FillMissingDefaults();
                report = created ? "created" : $"kept, {filled} missing value(s) filled";
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        public static BossBattleVisualConfig BuildVisualConfig(out string report)
        {
            var created = AssetDatabase.LoadAssetAtPath<BossBattleVisualConfig>(VisualConfigPath) == null;
            var visual = NinjagoEditorUtils.LoadOrCreate<BossBattleVisualConfig>(VisualConfigPath);
            var filled = visual.FillMissingDefaults();
            report = created ? "created" : $"kept, {filled} missing value(s) filled";
            EditorUtility.SetDirty(visual);
            return visual;
        }

        #endregion

        #region Helpers

        static string Guid(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        #endregion
    }
}
