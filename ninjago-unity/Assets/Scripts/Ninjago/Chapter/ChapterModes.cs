#nullable enable

using System.Collections.Generic;

namespace Nex.Ninjago
{
    /// <summary>Every playable mode in menu order: the built-in assets, then the player-made modes from the save.</summary>
    public static class ChapterModes
    {
        public static List<ModeData> All(ChapterModeLibrary library, ChapterSave save)
        {
            var modes = new List<ModeData>(library.Modes.Count + save.playerModes.Count);
            foreach (var definition in library.Modes) modes.Add(definition.ToModeData());
            modes.AddRange(save.playerModes);
            return modes;
        }

        /// <summary>A playable copy of the mode with this id, or null when it no longer exists.</summary>
        public static ModeData? Find(string id, ChapterModeLibrary library, ChapterSave save)
        {
            var definition = library.Find(id);
            if (definition != null) return definition.ToModeData();
            var playerMode = save.FindPlayerMode(id);
            if (playerMode == null) return null;
            return new ModeData { id = playerMode.id, name = playerMode.name.Copy(), icon = playerMode.icon, builtIn = false, chapter = playerMode.chapter.Copy() };
        }
    }
}
