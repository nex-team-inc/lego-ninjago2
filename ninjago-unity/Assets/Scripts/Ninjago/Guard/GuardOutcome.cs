namespace Nex.Ninjago
{
    /// <summary>How one throw at one player ended, and what the reading saw when it was not a block.</summary>
    public enum GuardOutcome
    {
        /// <summary>A fresh shield guard was up at the hit.</summary>
        Saved = 0,
        /// <summary>The shield went up just after the hit.</summary>
        Late = 1,
        /// <summary>The shield had been up too long to count; lower it and raise it again.</summary>
        Held = 2,
        /// <summary>Only one hand came up (or both, but wide): the child tried, the reading saw no shield.</summary>
        Half = 3,
        /// <summary>No try was seen.</summary>
        Missed = 4,
    }
}
