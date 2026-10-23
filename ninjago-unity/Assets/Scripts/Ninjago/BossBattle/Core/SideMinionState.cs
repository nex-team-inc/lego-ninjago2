#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One minion clinging to a roof corner (GDD §4.2), from its landing. The wall on that road edge is ready after
    /// wallGrowSeconds. Damage: no damage during the grace; after it, one heart per tick (the first tick lands at
    /// grace + tick), and after maxTicks the minion gives up and jumps off (result "failed"). The first minion of the run
    /// is slower (grace 4, tick 3.5, one tick: its only damage lands at 7.5 s). Grace and tick are scaled by the
    /// difficulty timing scale. Smash: once the wall is ready, the car at |x| ≥ smashX on that side moving outward faster
    /// than smashMinOutwardSpeed, or (2P) a player's solo lean that drove the car into the wall.
    /// </summary>
    public sealed class SideMinionState
    {
        float wallGrow;
        float grace;
        float tick;
        int maxTicks;
        float smashX;
        float minOutwardSpeed;
        float graceClock;
        float damageClock;

        public MinionSides Side { get; private set; } = MinionSides.Left;
        /// <summary>-1 left, +1 right.</summary>
        public int Sign => Side == MinionSides.Right ? 1 : -1;
        public bool Active { get; private set; }
        public bool Cleared { get; private set; }
        public bool GaveUp { get; private set; }
        public int TicksDealt { get; private set; }
        /// <summary>Seconds since the landing.</summary>
        public float TimeOnCar { get; private set; }
        public bool WallReady => Active && TimeOnCar >= wallGrow;
        /// <summary>Still in the no-damage grace.</summary>
        public bool InGrace => graceClock < grace;
        /// <summary>Past half the grace: the prompt escalates (pictogram, PiP pulse).</summary>
        public bool Escalated => graceClock >= grace * 0.5f;
        /// <summary>"Both" variant: while held, the grace and damage clocks wait (the wall still grows).</summary>
        public bool GraceHeld { get; set; }
        public float GraceSeconds => grace;
        public float TickSeconds => tick;
        public int MaxTicks => maxTicks;

        #region Public API

        /// <param name="side">Left or Right (Both is two instances).</param>
        public void Start(MinionSides side, bool isFirst, MinionSettings settings, float timingScale)
        {
            Side = side == MinionSides.Right ? MinionSides.Right : MinionSides.Left;
            wallGrow = Mathf.Max(0f, settings.wallGrowSeconds);
            grace = Mathf.Max(0f, (isFirst ? settings.firstGraceSeconds : settings.graceSeconds) * timingScale);
            tick = Mathf.Max(0.05f, (isFirst ? settings.firstTickSeconds : settings.tickSeconds) * timingScale);
            maxTicks = Mathf.Max(0, isFirst ? settings.firstMaxTicks : settings.maxTicks);
            smashX = settings.smashX;
            minOutwardSpeed = settings.smashMinOutwardSpeed;
            graceClock = 0f;
            damageClock = 0f;
            TimeOnCar = 0f;
            TicksDealt = 0;
            GraceHeld = false;
            Cleared = false;
            GaveUp = false;
            Active = true;
        }

        /// <summary>"Both" variant: a different grace (bothGraceSeconds × timing scale) for this side.</summary>
        public void SetGraceSeconds(float seconds) => grace = Mathf.Max(0f, seconds);

        /// <returns>Damage ticks that landed this frame (a heart each).</returns>
        public int Tick(float deltaTime)
        {
            if (!Active) return 0;
            TimeOnCar += deltaTime;
            if (GraceHeld) return 0;
            var left = deltaTime;
            if (graceClock < grace)
            {
                graceClock += left;
                if (graceClock < grace) return 0;
                left = graceClock - grace;
                graceClock = grace;
            }

            damageClock += left;
            var ticks = 0;
            while (TicksDealt < maxTicks && damageClock >= tick)
            {
                damageClock -= tick;
                TicksDealt++;
                ticks++;
            }

            if (TicksDealt >= maxTicks)
            {
                GaveUp = true;
                Active = false;
            }

            return ticks;
        }

        /// <param name="vehicleX">VehicleSteerer.Position.x.</param>
        /// <param name="vehicleVelX">VehicleSteerer.Velocity.x.</param>
        /// <param name="playerSoloSmash">2P: the arena drove the car into this wall for a player's solo lean.</param>
        /// <returns>True when this call smashed the wall (Cleared).</returns>
        public bool TryClear(float vehicleX, float vehicleVelX, bool playerSoloSmash)
        {
            if (!WallReady) return false;
            var smashed = playerSoloSmash || (Sign * vehicleX >= smashX && Sign * vehicleVelX > minOutwardSpeed);
            if (!smashed) return false;
            Cleared = true;
            Active = false;
            return true;
        }

        /// <summary>2P rule: this player's own steer toward the side is at least the threshold (MinionSettings.soloSmashSteer).</summary>
        public static bool IsSoloLean(Vector2? playerSteer, MinionSides side, float threshold)
        {
            if (!playerSteer.HasValue) return false;
            var sign = side == MinionSides.Right ? 1f : -1f;
            return sign * playerSteer.Value.x >= threshold;
        }

        /// <summary>Ends without a result (cancel/cleanup).</summary>
        public void Stop() => Active = false;

        #endregion
    }
}
