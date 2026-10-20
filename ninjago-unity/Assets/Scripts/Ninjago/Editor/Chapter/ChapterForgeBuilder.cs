#nullable enable

using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Rebuilds every Chapter Forge asset in dependency order. Idempotent: GUIDs are kept, and designer data in existing
    /// catalog, tuning and level assets is left alone.
    /// CLI: unity command eval 'return Nex.Ninjago.Editor.ChapterForgeBuilder.Run();' 300000 --timeout 320
    /// </summary>
    public static class ChapterForgeBuilder
    {
        #region Entry Point

        [MenuItem("Nex/Ninjago/Build Chapter Forge")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var localization = ChapterLocalizationSeeder.Run();
            ChapterActorsBuilder.Build();
            ChapterWorldBuilder.Build();
            ChapterAssetsBuilder.Build();
            ChapterModesBuilder.Build();
            ForgeExamplesBuilder.Build();
            ChapterVfxBuilder.Build();
            ChapterViewsBuilder.Build();
            ChapterGamesBuilder.Build();
            ForgeViewsBuilder.Build();
            AssetDatabase.SaveAssets();
            return $"[ChapterForgeBuilder] done ({localization})";
        }

        #endregion
    }
}
