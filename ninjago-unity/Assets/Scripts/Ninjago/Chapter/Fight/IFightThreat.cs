#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Nex.Ninjago
{
    /// <summary>One fight type's threat for one lane: tell, window, gesture, result. A fifth fight type is one more of these, registered in FightBlock.</summary>
    public interface IFightThreat
    {
        UniTask RunAsync(ChapterFightLane lane, CancellationToken cancellationToken);
    }
}
