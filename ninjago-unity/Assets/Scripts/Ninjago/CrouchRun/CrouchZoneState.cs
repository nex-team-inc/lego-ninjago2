namespace Nex.Ninjago
{
    // Values are persisted in the CrouchRunConfig EnumDictionary and zone lists: explicit ints, append only.
    public enum CrouchZoneState
    {
        Clear = 0,
        // A stretch of windows: the monsters outside see a standing ninja.
        Talking = 1,
    }
}
