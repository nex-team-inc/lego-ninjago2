#nullable enable

using System.Collections.Generic;

namespace Nex.Ninjago
{
    /// <summary>
    /// Which gestures were already hinted, for each block's hint policy: FirstTime hints a gesture the first time it
    /// appears in the level, NewOnly only when no earlier level of the chapter used it, None never.
    /// </summary>
    public sealed class ChapterHints
    {
        readonly HashSet<Gesture> seenInChapter = new();
        readonly HashSet<Gesture> seenInLevel = new();
        readonly HashSet<Gesture> chapterBeforeLevel = new();

        #region Public API

        public void BeginLevel()
        {
            chapterBeforeLevel.Clear();
            chapterBeforeLevel.UnionWith(seenInChapter);
            seenInLevel.Clear();
        }

        /// <summary>Marks earlier levels' gestures as seen when play starts mid-chapter (Continue or a debug jump).</summary>
        public void MarkSeen(IEnumerable<Gesture> gestures) => seenInChapter.UnionWith(gestures);

        /// <summary>True when this gesture's hint should show now; records the gesture as seen either way.</summary>
        public bool Consume(HintPolicy policy, Gesture gesture)
        {
            var show = policy switch
            {
                HintPolicy.FirstTime => !seenInLevel.Contains(gesture),
                HintPolicy.NewOnly => !chapterBeforeLevel.Contains(gesture) && !seenInLevel.Contains(gesture),
                _ => false,
            };
            seenInLevel.Add(gesture);
            seenInChapter.Add(gesture);
            return show;
        }

        #endregion
    }
}
