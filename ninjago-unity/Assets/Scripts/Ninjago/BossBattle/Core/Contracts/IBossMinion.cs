#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A thrown minion (MinionActor, a PooledProp of kind Minion). The minion attack owns the timing (grace, ticks, smash) through
    /// SideMinionState; the minion only animates: the arc onto the car's roof corner, clinging and stabbing, and leaving.
    /// </summary>
    public interface IBossMinion
    {
        /// <summary>Arcs from a world point onto the vehicle's roof corner (vehicle-local offset) over flightSeconds, then clings.</summary>
        void Launch(Vector3 fromWorld, Transform vehicle, Vector3 localOffset, float flightSeconds);
        /// <summary>True once it has landed and clings to the vehicle.</summary>
        bool Landed { get; }
        /// <summary>Stabbing animation on each damage tick.</summary>
        void Strike();
        /// <summary>Scraped off by the wall smash: flung outward on that side (-1 left, +1 right), then released.</summary>
        void ScrapeOff(int side);
        /// <summary>Gives up after its max ticks: hops off backwards, then released.</summary>
        void JumpOff();
    }
}
