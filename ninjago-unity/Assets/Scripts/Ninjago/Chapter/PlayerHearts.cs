#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Each player's hearts in the current level. Reaching 0 fails the current block, which restarts with full hearts.</summary>
    public sealed class PlayerHearts
    {
        readonly int[] hearts = new int[SimulatedBody.MaxPlayers];

        public int Max { get; private set; }

        /// <summary>(player index, hearts left) after any change.</summary>
        public event Action<int, int>? Changed;

        #region Public API

        public void Reset(int max)
        {
            Max = max;
            for (var i = 0; i < hearts.Length; i++) Set(i, max);
        }

        public int Get(int playerIndex) => hearts[playerIndex];

        /// <returns>True when this player has no hearts left.</returns>
        public bool Lose(int playerIndex, int amount = 1)
        {
            Set(playerIndex, Mathf.Max(0, hearts[playerIndex] - amount));
            return hearts[playerIndex] <= 0;
        }

        public bool IsOut(int playerIndex) => hearts[playerIndex] <= 0;

        #endregion

        #region Helpers

        void Set(int playerIndex, int value)
        {
            hearts[playerIndex] = value;
            Changed?.Invoke(playerIndex, value);
        }

        #endregion
    }
}
