using System;

namespace Nex.Ninjago
{
    // Keys of ForgeCatalog EnumDictionaries and saved in player-made modes: explicit values, append only.
    // A fifth block type is one entry here plus one ChapterBlock subclass registered in the ForgeCatalog asset.
    public enum BlockType
    {
        Stealth = 0,
        Runner = 1,
        SlashKick = 2,
        SlipSpin = 3,
    }

    /// <summary>Where a block can start or end in the world; a link is natural when one block's end meets the next one's start.</summary>
    [Flags]
    public enum LinkState
    {
        None = 0,
        Ambush = 1 << 0,
        EnemyFled = 1 << 1,
        AtWall = 1 << 2,
        OnRoof = 1 << 3,
        Airborne = 1 << 4,
        Landed = 1 << 5,
        CrowdIncoming = 1 << 6,
        GuardAhead = 1 << 7,
        BossPhase = 1 << 8,
        BossBroken = 1 << 9,
    }

    public enum RunnerRide
    {
        Car = 0,
        Skycraft = 1,
        CarThenSkycraft = 2,
    }

    public enum DifficultyPreset
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public enum HintPolicy
    {
        /// <summary>A one-word hint the first time each gesture appears in the level.</summary>
        FirstTime = 0,
        /// <summary>Hints only for gestures no earlier level of the chapter used.</summary>
        NewOnly = 1,
        None = 2,
    }

    public enum ChapterTheme
    {
        TownDusk = 0,
        HarborNight = 1,
        MountainTemple = 2,
        StormCitadel = 3,
    }

    public enum Gesture
    {
        Crouch = 0,
        Lean = 1,
        Slash = 2,
        Kick = 3,
        Slip = 4,
        Spin = 5,
        Wiggle = 6,
    }

    /// <summary>The moment in the world that covers a block swap.</summary>
    public enum LinkMoment
    {
        BrickRebuild = 0,
        BossRoar = 1,
        Dust = 2,
        BrickBurst = 3,
        NinjaLanding = 4,
        VehicleArrives = 5,
        DropOffRoof = 6,
        AroundCorner = 7,
        EnemyFlees = 8,
    }

    public enum CameraMove
    {
        Glide = 0,
        SwingToSide = 1,
        SwingBehind = 2,
        CraneUp = 3,
        FollowFlight = 4,
    }

    /// <summary>How two players share a block type.</summary>
    public enum TwoPlayerShare
    {
        /// <summary>Each player's threats are marked in that player's color; every result hits the same enemies.</summary>
        OwnThreats = 0,
        /// <summary>Both chests steer one vehicle with the data-set blend.</summary>
        SharedVehicle = 1,
        /// <summary>Each player has a ninja in single file; the team is heard when anyone stands in a talking zone.</summary>
        TeamSneak = 2,
    }

    public enum ModeIcon
    {
        Ninja = 0,
        Boss = 1,
        Lightning = 2,
        Dice = 3,
        Brick = 4,
    }

    public enum ForgeRule
    {
        BlockCount = 0,
        RepeatedType = 1,
        LinkUnresolved = 2,
        VehiclesMissing = 3,
        BossNotLast = 4,
        BossHasStealth = 5,
        BossUnknownGesture = 6,
        BossNoEscape = 7,
        DifficultyDrops = 8,
        LevelLength = 9,
        NoLevels = 10,
        MixNotFight = 11,
    }
}
