#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One block type's station on the level route: its camera view, where the ninjas land, and its play. The level
    /// runner stages a block when the link toward it starts, runs it when the link's camera settles, and stages and
    /// runs it again after a "try again" beat. A fifth block type is one new subclass registered in the ForgeCatalog.
    /// </summary>
    public abstract class ChapterBlock : MonoBehaviour
    {
        [Header("View Camera")]
        [Tooltip("This block's camera view; links blend the brain camera into it.")]
        [SerializeField] CinemachineCamera viewCamera = null!;
        [Header("Route Length")]
        [Tooltip("Meters of route this station takes before the next one starts.")]
        [SerializeField] float routeLength = 40f;

        public BlockContext Context { get; private set; } = null!;
        protected BlockEntry Entry { get; private set; } = null!;

        public CinemachineCamera ViewCamera => viewCamera;
        public virtual float RouteLength => routeLength;
        /// <summary>Where a link's moment plays when it arrives here.</summary>
        public abstract Vector3 FocusPoint { get; }

        #region Initialization

        /// <param name="seed">Keeps the station's scenery the same on every retry.</param>
        public virtual void Initialize(BlockContext context, BlockEntry entry, int seed)
        {
            Context = context;
            Entry = entry;
        }

        #endregion

        #region Public API

        /// <summary>Resets the station to its start (enemies, zones, vehicle); called before a link arrives and before a restart.</summary>
        public abstract void Stage();

        /// <summary>Where the ninja in this slot lands when the link arrives.</summary>
        public abstract Pose ArrivalPose(int slot);

        /// <summary>Plays the block from its first tell; false when it failed (out of hearts, out of time) and must restart.</summary>
        public abstract UniTask<bool> RunAsync(CancellationToken cancellationToken);

        /// <summary>The link out of this block starts; survivors may flee, a vehicle may stop.</summary>
        public virtual UniTask PlayExitAsync(LinkEntry link, CancellationToken cancellationToken) => UniTask.CompletedTask;

        /// <summary>The block is left behind for good; release anything the next block might see.</summary>
        public virtual void Finish()
        {
        }

        #endregion

        #region Helpers

        /// <summary>Shows the gesture's one-word hint over every ninja when the entry's hint policy allows it.</summary>
        public bool TryHint(Gesture gesture)
        {
            if (!Context.Hints.Consume(Entry.hints, gesture)) return false;
            foreach (var ninja in Context.Ninjas) ninja.Hint.Show(Context.Catalog.HintWord(gesture), ninja.Color, Context.Tuning.HintSeconds);
            return true;
        }

        /// <summary>Shows the gesture's hint on one prompt (e.g. over the shared vehicle) when the policy allows it.</summary>
        protected bool TryHint(Gesture gesture, WorldPrompt prompt, Color color)
        {
            if (!Context.Hints.Consume(Entry.hints, gesture)) return false;
            prompt.Show(Context.Catalog.HintWord(gesture), color, Context.Tuning.HintSeconds);
            return true;
        }

        protected static UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion
    }
}
