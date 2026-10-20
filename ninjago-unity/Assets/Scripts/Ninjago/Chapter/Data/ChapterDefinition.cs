#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    [CreateAssetMenu(fileName = "Chapter", menuName = "Nex/Ninjago/Chapter/Chapter Definition")]
    public class ChapterDefinition : ScriptableObject
    {
        [Header("Levels")]
        [Tooltip("In play order; the Boss level is last.")]
        [SerializeField] List<LevelDefinition> levels = new();
        [Header("Star Rules")]
        [SerializeField] StarRules stars = new();
        [Header("Rest View Rules")]
        [SerializeField] RestRules rest = new();

        public IReadOnlyList<LevelDefinition> Levels => levels;

        /// <summary>A copy, so play and the Forge never edit the asset.</summary>
        public ChapterData ToData()
        {
            var data = new ChapterData { stars = stars.Copy(), rest = rest.Copy() };
            foreach (var level in levels) data.levels.Add(level.Level.Copy());
            return data;
        }
    }
}
