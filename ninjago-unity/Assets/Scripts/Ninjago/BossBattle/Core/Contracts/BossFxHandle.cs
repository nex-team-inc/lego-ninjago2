#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A running looped cue from IBossArena.StartFx: the pooled particle system that follows a transform (with an offset)
    /// and/or a looping sound. Pass it back to StopFx (by ref), which resets it to default.
    /// </summary>
    public struct BossFxHandle
    {
        public BossFxCue cue;
        public ParticleSystem? system;
        public Transform? follow;
        public Vector3 offset;
        public bool hasLoopSound;
        public SfxManager.SoundEffect loopSound;
        /// <summary>The AudioSource SfxManager.StartLoop returned (StopLoop needs it).</summary>
        public AudioSource? loopSource;

        public bool IsValid => system != null || loopSource != null || hasLoopSound;
    }
}
