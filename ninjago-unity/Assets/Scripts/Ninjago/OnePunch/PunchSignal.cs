namespace Nex.Ninjago
{
    /// <summary>How one punch try was read. Extension and TowardCamera throw a monster; SideSlash and None do not.</summary>
    public enum PunchSignal
    {
        Extension = 0,
        TowardCamera = 1,
        SideSlash = 2,
        None = 3,
    }
}
