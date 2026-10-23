#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// Who is shown as "controlling" the shared vehicle (PiP triangle, P1/P2 tags; GDD §2), filtered so it does not
    /// flicker: with two players, a player is promoted after leading the other's contribution by at least LeadShare for
    /// PromoteSeconds; nobody steering (both below NeutralBelow) for NeutralSeconds turns it neutral (-1). With one player,
    /// that player is shown all the time.
    /// </summary>
    public sealed class ControllerFilter
    {
        public float LeadShare { get; set; } = 0.2f;
        public float PromoteSeconds { get; set; } = 0.6f;
        public float NeutralBelow { get; set; } = 0.15f;
        public float NeutralSeconds { get; set; } = 0.6f;
        /// <summary>The index shown when only one player is active (the lone calibrated player).</summary>
        public int SoloPlayer { get; set; }

        int candidate = -1;
        float leadSeconds;
        float idleSeconds;

        /// <summary>The filtered controller: a player index, or -1 neutral.</summary>
        public int Current { get; private set; } = -1;
        /// <summary>True when the last Update changed Current.</summary>
        public bool Changed { get; private set; }

        #region Public API

        /// <param name="c0">VehicleSteerer.Contribution(0) this frame.</param>
        /// <param name="c1">VehicleSteerer.Contribution(1) this frame.</param>
        public int Update(float c0, float c1, int activeCount, float deltaTime)
        {
            var next = Current;
            if (activeCount <= 1)
            {
                next = SoloPlayer;
                candidate = -1;
                leadSeconds = 0f;
                idleSeconds = 0f;
            }
            else if (c0 < NeutralBelow && c1 < NeutralBelow)
            {
                candidate = -1;
                leadSeconds = 0f;
                idleSeconds += deltaTime;
                if (idleSeconds >= NeutralSeconds) next = -1;
            }
            else
            {
                idleSeconds = 0f;
                var leader = c0 - c1 >= LeadShare ? 0 : c1 - c0 >= LeadShare ? 1 : -1;
                if (leader < 0 || leader == Current)
                {
                    candidate = -1;
                    leadSeconds = 0f;
                }
                else
                {
                    if (leader != candidate)
                    {
                        candidate = leader;
                        leadSeconds = 0f;
                    }

                    leadSeconds += deltaTime;
                    if (leadSeconds >= PromoteSeconds) next = leader;
                }
            }

            Changed = next != Current;
            Current = next;
            return Current;
        }

        public void Reset()
        {
            Current = -1;
            Changed = false;
            candidate = -1;
            leadSeconds = 0f;
            idleSeconds = 0f;
        }

        #endregion
    }
}
