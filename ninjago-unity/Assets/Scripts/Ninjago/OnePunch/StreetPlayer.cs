#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>One active player in the street: that player's ninja, health, wiggle meter and run counts.</summary>
    public sealed class StreetPlayer
    {
        readonly int[] tries = new int[4];
        double lastHipFrame = double.NaN;

        public PlayerBody Body { get; }
        public StreetNinja Ninja { get; }
        /// <summary>0 for the left ninja (or the only one), 1 for the right.</summary>
        public int Slot { get; }
        public float MaxHealth { get; }
        public float Health { get; private set; }
        public int Thrown { get; private set; }
        public int TimesPinned { get; private set; }
        /// <summary>Pins this player wiggled out of.</summary>
        public int Wiggles { get; private set; }
        public WiggleMeter Meter { get; } = new();
        public int PlayerIndex => Body.PlayerIndex;
        public Color Color => Body.Color;

        public StreetPlayer(PlayerBody body, StreetNinja ninja, int slot, float maxHealth)
        {
            Body = body;
            Ninja = ninja;
            Slot = slot;
            MaxHealth = maxHealth;
            Health = maxHealth;
        }

        #region Public API

        public int Tries(PunchSignal signal) => tries[(int)signal];

        public void CountTry(PunchSignal signal) => tries[(int)signal]++;

        public void CountThrow() => Thrown++;

        public void CountWiggle() => Wiggles++;

        public void StartPin()
        {
            TimesPinned++;
            Meter.Reset();
            Ninja.SetPinned(true);
        }

        public void EndPin()
        {
            Ninja.SetPinned(false);
        }

        /// <returns>True when the drain crossed a whole point of health.</returns>
        public bool Drain(float amount)
        {
            var before = Mathf.CeilToInt(Health);
            Health = Mathf.Max(0f, Health - amount);
            return Mathf.CeilToInt(Health) < before;
        }

        /// <summary>The hip center (body inches from the setup chest) on a new body frame; every frame for the simulated body.</summary>
        public bool TrySampleHips(out Vector2 hips)
        {
            hips = default;
            if (!SimulatedBody.IsEnabled)
            {
                if (Body.BodyFrameTime == lastHipFrame) return false;
                lastHipFrame = Body.BodyFrameTime;
            }

            return Body.TryGetHipInches(out hips);
        }

        #endregion
    }
}
