#nullable enable

namespace Nex.Ninjago
{
    /// <summary>One prewarmed pool per BossPropKind (implemented by BossPools in the arena).</summary>
    public interface IBossPools
    {
        /// <summary>A spawned prop of this kind (NotifySpawned already called); it returns itself with Release().</summary>
        T Get<T>(BossPropKind kind) where T : PooledProp;
        void ReleaseAll();
    }
}
