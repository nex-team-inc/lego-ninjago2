#nullable enable

// Boss Battle enums (TDD §3.1). Every value is explicit: these are saved (BossBattleSave, PlayerPreference) or used as
// EnumDictionary keys in BossBattleConfig / BossBattleVisualConfig, so append only, keep ascending order, never renumber.

namespace Nex.Ninjago
{
    /// <summary>The two halves of the fight: the car on the road, then the jet in the storm.</summary>
    public enum BossPhase
    {
        Road = 0,
        Sky = 1,
    }

    /// <summary>Hearts, timing scale and laser radius per difficulty (BossBattleConfig.Difficulty).</summary>
    public enum BossDifficulty
    {
        Easy = 0,
        Normal = 1,
    }

    /// <summary>One step of a phase's beat script. Road beats are below 10, sky beats from 10.</summary>
    public enum BossBeat
    {
        Breather = 0,
        GroundSlam = 1,
        RockDrop = 2,
        MinionThrow = 3,
        PowerUp = 4,
        Ready = 5,
        SteerCheck = 6,

        DevilWind = 10,
        LaserWindow = 11,
        Clap = 12,
        RiseDuck = 13,
    }

    /// <summary>What hit the vehicle (damage per hazard in BossBattleConfig.HazardDamage).</summary>
    public enum BossHazard
    {
        Brick = 0,
        Rock = 1,
        Minion = 2,
        Wind = 3,
        SpikeWall = 4,
    }

    /// <summary>Which roof corner(s) a minion lands on; Both throws one per side.</summary>
    public enum MinionSides
    {
        Left = 1,
        Right = 2,
        Both = 3,
    }

    public enum BossPhaseOutcome
    {
        Cleared = 0,
        KnockedOut = 1,
        Quit = 2,
    }

    public enum BossRunEnd
    {
        Won = 0,
        KnockedOut = 1,
        Quit = 2,
    }

    /// <summary>Lighting and post look of the arena: dusk road, night storm, dawn finale.</summary>
    public enum BossLook
    {
        Dusk = 0,
        Night = 1,
        Dawn = 2,
    }

    public enum BossCutscene
    {
        Intro = 0,
        Transform = 1,
        Finale = 2,
        Knockout = 3,
        SkyRejoin = 4,
    }

    /// <summary>The icon over the boss head (and the colour of its hand/mouth tell) while an attack winds up.</summary>
    public enum BossAttackIcon
    {
        None = 0,
        Rock = 1,
        Minion = 2,
        Slam = 3,
        Wind = 4,
        Clap = 5,
        Tired = 6,
    }

    public enum PromptArrow
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4,
        LeftRight = 5,
        UpDown = 6,
    }

    /// <summary>One HUD prompt line (localization key, colour, seconds and arrow in BossBattleConfig.Prompt).</summary>
    public enum BossPromptId
    {
        LeanToSteer = 0,
        GrabRockets = 1,
        DodgeRock = 2,
        SmashLeft = 3,
        SmashRight = 4,
        SmashBoth = 5,
        DodgeBricks = 6,
        RiseAndDuck = 7,
        DodgeWind = 8,
        TiredFire = 9,
        FlyIntoRing = 10,
        OnTarget = 11,
        Dive = 12,
        Brace = 13,
        FinishIt = 14,
        Enraged = 15,
        MinionHurry = 16,
        GetReady = 17,
    }

    public enum BossBannerId
    {
        BossName = 0,
        Phase1 = 1,
        Phase2 = 2,
        Victory = 3,
        KnockedOut = 4,
        Wake = 5,
    }

    /// <summary>One pool per kind in the arena (IBossPools.Get). Road props below 10, sky props from 10.</summary>
    public enum BossPropKind
    {
        Brick = 0,
        Rock = 1,
        Reticle = 2,
        CrackGlow = 3,
        SideWall = 4,
        Minion = 5,
        PowerCrate = 6,
        Rocket = 7,

        WindGust = 10,
        LaserBolt = 11,
        SpikeWall = 12,
        TargetRing = 13,
        // The glowing safe band under the clap walls.
        SafeBand = 14,
    }

    /// <summary>A sound + visual + shake + light moment; BossBattleConfig.Fx maps each cue to its manager keys.</summary>
    public enum BossFxCue
    {
        RockThrow = 0,
        RockWhistle = 1,
        RockWarning = 2,
        RockLock = 3,
        RockImpact = 4,
        SlamWindup = 5,
        GroundSlam = 6,
        CrackGlow = 7,
        BrickGrow = 8,
        MinionThrow = 9,
        MinionLand = 10,
        MinionHit = 11,
        WallGrow = 12,
        WallSmash = 13,
        PowerUpSpawn = 14,
        PowerUpPickup = 15,
        RocketLaunch = 16,
        RocketHit = 17,
        BossHurt = 18,
        BossRoar = 19,
        TakeOff = 20,
        Transform = 21,
        DevilWind = 22,
        GustPass = 23,
        Tired = 24,
        LaserLoop = 25,
        LaserHit = 26,
        LaserDeflect = 27,
        Wake = 28,
        ClapInhale = 29,
        ClapPush = 30,
        ClapWarning = 31,
        SpikeSlam = 32,
        ExplodeSmall = 33,
        ExplodeBig = 34,
        Victory = 35,
        VehicleHit = 36,
        Knockout = 37,
        BannerSlam = 38,
        Enrage = 39,
        Countdown = 40,
        Laugh = 41,
        Lightning = 42,
        RebuildSparkle = 43,
        LaserMuzzle = 44,
        Embers = 45,
        StormDust = 46,
    }

    /// <summary>RockReticle states: rock in the air, reticle following the car, locked (blinking), rock falling, landed.</summary>
    public enum ReticleState
    {
        Hidden = 0,
        Following = 1,
        Locked = 2,
        Falling = 3,
        Done = 4,
    }
}
