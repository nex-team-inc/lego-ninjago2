#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor
{
    /// <summary>Validate and "Play from here" for chapter assets: shared lookups and drawing.</summary>
    static class ChapterInspectorGUI
    {
        static ForgeCatalog Catalog => AssetDatabase.LoadAssetAtPath<ForgeCatalog>(ChapterAssetsBuilder.CatalogPath);
        static ForgeConfig Config => AssetDatabase.LoadAssetAtPath<ForgeConfig>(ChapterAssetsBuilder.ForgeConfigPath);
        static ChapterTuning Tuning => AssetDatabase.LoadAssetAtPath<ChapterTuning>(ChapterAssetsBuilder.TuningPath);
        static ChapterModeLibrary Library => AssetDatabase.LoadAssetAtPath<ChapterModeLibrary>(ChapterModesBuilder.LibraryPath);

        public static List<ForgeProblem> Validate(ChapterDefinition chapter) => ChapterValidator.Validate(chapter.ToData(), Catalog, Tuning.LinkSeconds);

        /// <summary>The problems for this level and block (-1 = any); chapter-wide problems always show.</summary>
        public static void Problems(List<ForgeProblem>? problems, int level, int block)
        {
            if (problems == null) return;
            var shown = 0;
            foreach (var problem in problems)
            {
                if (level >= 0 && problem.level >= 0 && problem.level != level) continue;
                if (block >= 0 && problem.block >= 0 && problem.block != block) continue;
                var line = ChapterValidator.Describe(problem, Config).GetLocalizedString();
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(line) ? problem.rule.ToString() : line, problem.isWarning ? MessageType.Warning : MessageType.Error);
                shown++;
            }

            if (shown == 0) EditorGUILayout.HelpBox("Valid: every rule passes.", MessageType.Info);
        }

        public static ModeDefinition? OwnerMode(ChapterDefinition chapter)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ModeDefinition"))
            {
                var mode = AssetDatabase.LoadAssetAtPath<ModeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (mode != null && mode.Chapter == chapter) return mode;
            }

            return null;
        }

        public static ChapterDefinition? OwnerChapter(LevelDefinition level, out int index)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ChapterDefinition"))
            {
                var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (chapter == null) continue;
                for (var i = 0; i < chapter.Levels.Count; i++)
                {
                    if (chapter.Levels[i] != level) continue;
                    index = i;
                    return chapter;
                }
            }

            index = -1;
            return null;
        }

        public static void Play(ChapterDefinition? chapter, int level, int block)
        {
            var mode = chapter == null ? null : OwnerMode(chapter);
            if (mode == null)
            {
                EditorUtility.DisplayDialog("Play from here", "No mode uses this chapter yet. Create a Mode Definition for it first.", "OK");
                return;
            }

            ChapterEditorPlay.Request(mode.Id, level, block);
        }

        /// <summary>Shows whether the main menu lists this mode, with a button to add it.</summary>
        public static void MenuRow(ModeDefinition mode)
        {
            var library = Library;
            var listed = false;
            foreach (var entry in library.Modes) listed |= entry == mode;
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(listed ? "Listed in the main menu" : "Not in the main menu yet", EditorStyles.boldLabel);
            if (listed || !GUILayout.Button("Add to menu", GUILayout.Width(120f))) return;
            var serialized = new SerializedObject(library);
            var modes = serialized.FindProperty("modes");
            modes.arraySize++;
            modes.GetArrayElementAtIndex(modes.arraySize - 1).objectReferenceValue = mode;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        public static void LinkFill(LevelDefinition level)
        {
            Undo.RecordObject(level, "Fill links");
            LinkResolver.Refresh(level.Level, Catalog);
            EditorUtility.SetDirty(level);
        }
    }

    [CustomEditor(typeof(ModeDefinition))]
    public class ModeDefinitionEditor : UnityEditor.Editor
    {
        List<ForgeProblem>? problems;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var mode = (ModeDefinition)target;
            EditorGUILayout.Space();
            ChapterInspectorGUI.MenuRow(mode);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate") && mode.Chapter != null) problems = ChapterInspectorGUI.Validate(mode.Chapter);
                if (GUILayout.Button("Play from here")) ChapterEditorPlay.Request(mode.Id, 0, 0);
            }

            ChapterInspectorGUI.Problems(problems, -1, -1);
        }
    }

    [CustomEditor(typeof(ChapterDefinition))]
    public class ChapterDefinitionEditor : UnityEditor.Editor
    {
        List<ForgeProblem>? problems;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var chapter = (ChapterDefinition)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate")) problems = ChapterInspectorGUI.Validate(chapter);
                if (GUILayout.Button("Play from here")) ChapterInspectorGUI.Play(chapter, 0, 0);
            }

            for (var i = 0; i < chapter.Levels.Count; i++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField($"Level {i + 1}: {(chapter.Levels[i] != null ? chapter.Levels[i].name : "(none)")}");
                if (GUILayout.Button("Play from here", GUILayout.Width(120f))) ChapterInspectorGUI.Play(chapter, i, 0);
            }

            ChapterInspectorGUI.Problems(problems, -1, -1);
        }
    }

    [CustomEditor(typeof(LevelDefinition))]
    public class LevelDefinitionEditor : UnityEditor.Editor
    {
        List<ForgeProblem>? problems;
        int problemBlock = -1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var level = (LevelDefinition)target;
            var chapter = ChapterInspectorGUI.OwnerChapter(level, out var levelIndex);
            EditorGUILayout.Space();
            if (chapter == null) EditorGUILayout.HelpBox("Add this level to a Chapter Definition to validate and play it.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill links")) ChapterInspectorGUI.LinkFill(level);
                using (new EditorGUI.DisabledScope(chapter == null))
                {
                    if (GUILayout.Button("Validate")) Validate(chapter!, -1);
                    if (GUILayout.Button("Play from here")) ChapterInspectorGUI.Play(chapter, levelIndex, 0);
                }
            }

            var blocks = level.Level.blocks;
            for (var b = 0; b < blocks.Count; b++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField($"Block {b + 1}: {blocks[b].type}{(blocks[b].boss ? " (Boss)" : "")}");
                using (new EditorGUI.DisabledScope(chapter == null))
                {
                    if (GUILayout.Button("Validate", GUILayout.Width(80f))) Validate(chapter!, b);
                    if (GUILayout.Button("Play from here", GUILayout.Width(120f))) ChapterInspectorGUI.Play(chapter, levelIndex, b);
                }
            }

            if (chapter != null) ChapterInspectorGUI.Problems(problems, levelIndex, problemBlock);
        }

        void Validate(ChapterDefinition chapter, int block)
        {
            problems = ChapterInspectorGUI.Validate(chapter);
            problemBlock = block;
        }
    }
}
