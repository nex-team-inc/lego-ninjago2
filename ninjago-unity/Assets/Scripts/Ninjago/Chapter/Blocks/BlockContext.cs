#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>What every block of a level shares: the active players and their ninjas, hearts, hints, the log and the tuning.</summary>
    public sealed class BlockContext
    {
        public IReadOnlyList<PlayerBody> Players { get; }
        /// <summary>Same order as Players.</summary>
        public IReadOnlyList<ChapterNinja> Ninjas { get; }
        public PlayerHearts Hearts { get; }
        public ChapterHints Hints { get; }
        public ChapterLog Log { get; }
        public ChapterTuning Tuning { get; }
        public ForgeCatalog Catalog { get; }
        public Camera Camera { get; }
        public HandStormSampler Storms { get; }

        public BlockContext(IReadOnlyList<PlayerBody> players, IReadOnlyList<ChapterNinja> ninjas, PlayerHearts hearts, ChapterHints hints,
            ChapterLog log, ChapterTuning tuning, ForgeCatalog catalog, Camera camera, HandStormSampler storms)
        {
            Players = players;
            Ninjas = ninjas;
            Hearts = hearts;
            Hints = hints;
            Log = log;
            Tuning = tuning;
            Catalog = catalog;
            Camera = camera;
            Storms = storms;
        }
    }
}
