#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Brick Titan (TDD §3.4, implemented by BossActor on the boss prefab). Faces -z toward the vehicle; procedural
    /// animation on scaled time. The *Async actions return at the moment the gameplay needs (release, impact, blow start).
    /// </summary>
    public interface IBossActor
    {
        Transform Transform { get; }
        /// <summary>World; rockets aim here.</summary>
        Vector3 ChestPoint { get; }
        /// <summary>World; the laser weak point.</summary>
        Vector3 CorePoint { get; }
        Vector3 MouthPoint { get; }
        Vector3 FeetPoint { get; }
        /// <summary>World; the attack icon anchor.</summary>
        Vector3 HeadTopPoint { get; }
        Vector3 HandPoint(bool left);

        void Initialize(BossBattleConfig config);
        void ResetVisual(BossPhase phase);
        /// <summary>Arena base placement each frame; the actor adds sway and action offsets.</summary>
        void SetAnchor(Vector3 localPosition);
        /// <summary>Phase 2 laser: move so the Core sits here (null = free hover).</summary>
        void SetCoreTarget(Vector3? localCorePosition);
        /// <summary>Hand/mouth colour tell (None clears).</summary>
        void SetTell(BossAttackIcon icon);

        UniTask RoarAsync(float seconds, CancellationToken cancellationToken);
        /// <summary>Returns at the release.</summary>
        UniTask ThrowAsync(bool left, float windupSeconds, CancellationToken cancellationToken);
        /// <summary>Returns at the impact.</summary>
        UniTask SlamAsync(float windupSeconds, CancellationToken cancellationToken);
        /// <summary>Returns when the blowing starts.</summary>
        UniTask BlowAsync(float windupSeconds, CancellationToken cancellationToken);
        void StopBlowing();
        UniTask InhaleAsync(float seconds, CancellationToken cancellationToken);
        UniTask ClapAsync(float seconds, CancellationToken cancellationToken);
        UniTask TakeOffAsync(Vector3 toLocal, float seconds, CancellationToken cancellationToken);
        UniTask LandAsync(Vector3 fromLocal, Vector3 toLocal, float seconds, CancellationToken cancellationToken);
        UniTask LaughAsync(float seconds, CancellationToken cancellationToken);
        UniTask ExplodeAsync(CancellationToken cancellationToken);
        void SetTired(bool tired);
        void SetEnraged(bool enraged);
        void Flinch(float strength);
        void SetVisible(bool visible);
    }
}
