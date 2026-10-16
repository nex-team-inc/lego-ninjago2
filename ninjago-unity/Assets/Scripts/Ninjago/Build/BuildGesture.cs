namespace Nex.Ninjago
{
    /// <summary>How one clap try was read, shown on the builder's panel.</summary>
    public enum BuildGesture
    {
        /// <summary>The hands came together: one brick clicks onto the bridge.</summary>
        Counted = 0,
        /// <summary>The hands came close but not together: a try the reading did not count.</summary>
        Near = 1,
    }
}
