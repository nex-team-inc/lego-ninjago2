#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The built-in modes listed in the main menu, before the player-made ones.</summary>
    [CreateAssetMenu(fileName = "ChapterModeLibrary", menuName = "Nex/Ninjago/Chapter/Mode Library")]
    public class ChapterModeLibrary : ScriptableObject
    {
        [Header("Built-in Modes")]
        [SerializeField] List<ModeDefinition> modes = new();

        public IReadOnlyList<ModeDefinition> Modes => modes;

        public ModeDefinition? Find(string id)
        {
            foreach (var mode in modes)
            {
                if (mode.Id == id) return mode;
            }

            return null;
        }
    }
}
