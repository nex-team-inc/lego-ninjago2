namespace Nex.Ninjago
{
    /// <summary>What a call gesture led to, shown on the caller's panel.</summary>
    public enum CallResult
    {
        /// <summary>Read, and the teammate is on the way.</summary>
        Understood = 0,
        /// <summary>Read while the teammate was busy: it comes right after.</summary>
        Queued = 1,
        /// <summary>Read while the teammate was already coming for the partner: one big slam for both.</summary>
        Team = 2,
        /// <summary>The hand came up, but not above the head: not read as a call.</summary>
        Missed = 3,
    }
}
