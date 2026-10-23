#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Nex.Ninjago
{
    /// <summary>The fight's cutscenes (implemented by BossCutscenes on the arena prefab).</summary>
    public interface IBossCutscenes
    {
        bool IsPlaying { get; }
        /// <summary>Plays the shots; leaves the world in the cutscene's end state with steering enabled.</summary>
        UniTask PlayAsync(BossCutscene cutscene, IBossArena arena, CancellationToken cancellationToken);
        /// <summary>Jumps the playing cutscene to its end state (Back on a cutscene already seen).</summary>
        void SkipToEnd();
    }
}
