#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Nex.Ninjago
{
    /// <summary>
    /// Plays one phase's beat script from State.beatIndex to its end (RoadPhaseRunner, SkyPhaseRunner). The game owns the views
    /// around it; the runner owns the attacks, prompts and checkpoints inside the phase.
    /// </summary>
    public interface IBossPhaseRunner
    {
        BossPhase Phase { get; }
        /// <summary>Cleared when the phase's boss bar is done, KnockedOut at 0 hearts; cancellation means Quit.</summary>
        UniTask<BossPhaseOutcome> RunAsync(IBossArena arena, CancellationToken cancellationToken);
    }
}
