#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The chapter's levels in a row (Level 1, Level 2, ..., Boss): done ones marked with their best stars, the next one highlighted.</summary>
    public class ProgressPath : MonoBehaviour
    {
        [Header("Nodes Root")]
        [SerializeField] RectTransform nodesRoot = null!;
        [Header("Node Prefab")]
        [SerializeField] RestNode nodePrefab = null!;

        readonly List<RestNode> nodes = new();

        #region Public API

        /// <param name="nextLevel">The level played next; the count of levels for none (the chapter is over).</param>
        public void Show(ChapterData chapter, ForgeConfig names, int nextLevel, ModeProgress progress)
        {
            while (nodes.Count < chapter.levels.Count) nodes.Add(Instantiate(nodePrefab, nodesRoot));
            var normalNumber = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var used = i < chapter.levels.Count;
                nodes[i].gameObject.SetActive(used);
                if (!used) continue;
                var boss = chapter.levels[i].IsBoss;
                if (!boss) normalNumber++;
                var state = i < nextLevel ? -1 : i == nextLevel ? 0 : 1;
                nodes[i].Set(boss ? names.BossLevelName : names.LevelName(normalNumber), boss, state, progress.BestStars(i));
            }
        }

        #endregion
    }
}
