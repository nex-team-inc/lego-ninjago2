#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Nex.Ninjago
{
    /// <summary>
    /// Player-made modes and chapter progress, saved by PlayerDataManager (Easy Save). Public fields only; append new
    /// fields, never rename.
    /// </summary>
    [Preserve]
    public class ChapterSave
    {
        public List<ModeData> playerModes = new();
        public List<ModeProgress> progress = new();
        // Next number for a player-made mode id; ids are never reused.
        public int nextModeNumber = 1;

        public ModeProgress ProgressOf(string modeId)
        {
            foreach (var entry in progress)
            {
                if (entry.modeId == modeId) return entry;
            }

            var created = new ModeProgress { modeId = modeId };
            progress.Add(created);
            return created;
        }

        public ModeProgress? FindProgress(string modeId)
        {
            foreach (var entry in progress)
            {
                if (entry.modeId == modeId) return entry;
            }

            return null;
        }

        public ModeData? FindPlayerMode(string modeId)
        {
            foreach (var mode in playerModes)
            {
                if (mode.id == modeId) return mode;
            }

            return null;
        }
    }

    [Serializable, Preserve]
    public class ModeProgress
    {
        public string modeId = "";
        // The next unplayed level; Continue resumes at the rest view before it. 0 = not started.
        public int nextLevel;
        // Best stars per level index.
        public List<int> bestStars = new();
        // The last finished level's summary, shown again by Continue.
        public int lastLevel = -1;
        public int lastStars;
        public int lastHeartsLeft;
        public bool finished;

        public int BestStars(int levelIndex) => levelIndex < bestStars.Count ? bestStars[levelIndex] : 0;

        public void RecordLevel(int levelIndex, int stars, int heartsLeft, int levelCount)
        {
            while (bestStars.Count <= levelIndex) bestStars.Add(0);
            if (stars > bestStars[levelIndex]) bestStars[levelIndex] = stars;
            lastLevel = levelIndex;
            lastStars = stars;
            lastHeartsLeft = heartsLeft;
            nextLevel = levelIndex + 1;
            finished = nextLevel >= levelCount;
        }

        public void Restart()
        {
            nextLevel = 0;
            lastLevel = -1;
            finished = false;
        }
    }
}
