#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Fills in the link between two blocks from the end and start states: the first shared state in the catalog's
    /// preferred order is a natural link, and no shared state is the brick rebuild, which links any two blocks.
    /// </summary>
    public static class LinkResolver
    {
        #region Public API

        public static LinkState Shared(BlockEntry previous, BlockEntry next, ForgeCatalog catalog) =>
            catalog.EndStates(previous) & catalog.StartStates(next);

        /// <summary>The preferred shared state, or None when only the brick rebuild fits.</summary>
        public static LinkState NaturalState(BlockEntry previous, BlockEntry next, ForgeCatalog catalog)
        {
            var shared = Shared(previous, next, catalog);
            foreach (var link in catalog.NaturalLinks)
            {
                if ((shared & link.state) != 0) return link.state;
            }

            return LinkState.None;
        }

        public static LinkEntry Auto(BlockEntry previous, BlockEntry next, ForgeCatalog catalog, int variant)
        {
            return ForState(NaturalState(previous, next, catalog), previous, next, catalog, variant);
        }

        /// <summary>A link that uses the given state (None = brick rebuild), with the library's moment and story.</summary>
        public static LinkEntry ForState(LinkState state, BlockEntry previous, BlockEntry next, ForgeCatalog catalog, int variant)
        {
            var spec = (state == LinkState.None ? null : catalog.NaturalLinkFor(state)) ?? catalog.BrickRebuild;
            return new LinkEntry
            {
                auto = true,
                state = state,
                moment = spec.moment,
                cameraMove = CameraMoveFor(spec.cameraMove, previous, next, catalog),
                storyKey = spec.storyKeys.Count > 0 ? spec.storyKeys[Mathf.Abs(variant) % spec.storyKeys.Count] : "",
                stinger = spec.stinger,
            };
        }

        /// <summary>Moving into or out of a side view always swings the camera, so the move has a reason in the world.</summary>
        public static CameraMove CameraMoveFor(CameraMove preferred, BlockEntry previous, BlockEntry next, ForgeCatalog catalog)
        {
            var fromSide = catalog.IsSideView(previous);
            var toSide = catalog.IsSideView(next);
            if (toSide && !fromSide) return CameraMove.SwingToSide;
            if (fromSide && !toSide) return CameraMove.SwingBehind;
            return preferred;
        }

        public static bool Resolves(LinkEntry link, BlockEntry previous, BlockEntry next, ForgeCatalog catalog)
        {
            if (link.state == LinkState.None) return true;
            return IsSingle(link.state) && (Shared(previous, next, catalog) & link.state) == link.state;
        }

        /// <summary>One link per gap; auto links are filled in again after blocks were added, removed or moved.</summary>
        public static void Refresh(LevelData level, ForgeCatalog catalog)
        {
            var gaps = Mathf.Max(0, level.blocks.Count - 1);
            while (level.links.Count > gaps) level.links.RemoveAt(level.links.Count - 1);
            while (level.links.Count < gaps) level.links.Add(new LinkEntry());
            for (var i = 0; i < gaps; i++)
            {
                if (!level.links[i].auto) continue;
                level.links[i] = Auto(level.blocks[i], level.blocks[i + 1], catalog, i);
            }
        }

        /// <summary>The natural states this gap can use in preferred order, then None for the brick rebuild.</summary>
        public static void Options(BlockEntry previous, BlockEntry next, ForgeCatalog catalog, List<LinkState> result)
        {
            result.Clear();
            var shared = Shared(previous, next, catalog);
            foreach (var link in catalog.NaturalLinks)
            {
                if ((shared & link.state) != 0 && !result.Contains(link.state)) result.Add(link.state);
            }

            result.Add(LinkState.None);
        }

        #endregion

        #region Helpers

        static bool IsSingle(LinkState state)
        {
            var bits = (int)state;
            return bits != 0 && (bits & (bits - 1)) == 0;
        }

        #endregion
    }
}
