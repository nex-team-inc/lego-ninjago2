#nullable enable

using System.Collections.Generic;

namespace Nex.Ninjago
{
    /// <summary>
    /// Which gestures still need teaching, for each block's hint policy: FirstTime teaches a gesture in every level until
    /// the player has done it once there, NewOnly does the same but only for gestures no earlier level of the chapter
    /// used, None never teaches.
    /// </summary>
    public sealed class ChapterHints
    {
        readonly HashSet<Gesture> seenInChapter = new();
        readonly HashSet<Gesture> learnedInLevel = new();
        readonly HashSet<Gesture> chapterBeforeLevel = new();

        #region Public API

        public void BeginLevel()
        {
            chapterBeforeLevel.Clear();
            chapterBeforeLevel.UnionWith(seenInChapter);
            learnedInLevel.Clear();
        }

        /// <summary>Marks earlier levels' gestures as seen when play starts mid-chapter (Continue or a debug jump).</summary>
        public void MarkSeen(IEnumerable<Gesture> gestures) => seenInChapter.UnionWith(gestures);

        /// <summary>True when this gesture's how-to should show now; records the gesture as seen in the chapter.</summary>
        public bool Teaches(HintPolicy policy, Gesture gesture)
        {
            seenInChapter.Add(gesture);
            return policy switch
            {
                HintPolicy.FirstTime => !learnedInLevel.Contains(gesture),
                HintPolicy.NewOnly => !chapterBeforeLevel.Contains(gesture) && !learnedInLevel.Contains(gesture),
                _ => false,
            };
        }

        /// <summary>The player did the gesture right once in this level; its how-to stops showing.</summary>
        public void Learned(Gesture gesture) => learnedInLevel.Add(gesture);

        #endregion
    }
}
