#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A fight entry's threat steps. A mixed entry's pattern of letters (each fight type's letter, '+' joining two that
    /// overlap) plays as written; otherwise count threats take the entry's types in turn and every overlapEvery-th
    /// threat overlaps the next.
    /// </summary>
    public static class FightPattern
    {
        public readonly struct Step
        {
            public readonly BlockType type;
            public readonly bool overlapsNext;

            public Step(BlockType type, bool overlapsNext)
            {
                this.type = type;
                this.overlapsNext = overlapsNext;
            }
        }

        #region Public API

        public static void Build(BlockEntry entry, ForgeCatalog catalog, List<Step> result)
        {
            result.Clear();
            var types = new List<BlockType>();
            catalog.TypesOf(entry, types);
            if (entry.mix.Count > 0 && !string.IsNullOrWhiteSpace(entry.pattern))
            {
                foreach (var token in entry.pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries);
                    for (var p = 0; p < parts.Length; p++)
                    {
                        if (TryTypeForLetter(parts[p], types, catalog, out var type)) result.Add(new Step(type, p < parts.Length - 1));
                    }
                }

                if (result.Count > 0) return;
            }

            var count = Mathf.Max(1, entry.difficulty.count);
            for (var i = 0; i < count; i++)
            {
                var overlaps = entry.difficulty.overlapEvery > 0 && (i + 1) % entry.difficulty.overlapEvery == 0 && i < count - 1;
                result.Add(new Step(types[i % types.Count], overlaps));
            }
        }

        #endregion

        #region Helpers

        static bool TryTypeForLetter(string letter, List<BlockType> types, ForgeCatalog catalog, out BlockType type)
        {
            foreach (var candidate in types)
            {
                if (!string.Equals(catalog.Block(candidate).patternLetter, letter, StringComparison.OrdinalIgnoreCase)) continue;
                type = candidate;
                return true;
            }

            type = default;
            return false;
        }

        #endregion
    }
}
