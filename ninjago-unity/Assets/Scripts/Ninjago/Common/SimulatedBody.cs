#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Debug stand-in for a tracked body (Debug Settings "Ninja: Simulated Body"), so every mini-game runs without a
    /// camera. SimulatedBodyKeyboard drives it from keys and the mouse; the Unity CLI can call the setters directly.
    /// </summary>
    public static class SimulatedBody
    {
        public const int MaxPlayers = 2;

        static readonly Vector2[] leanInches = new Vector2[MaxPlayers];
        static readonly bool[] storming = new bool[MaxPlayers];
        static readonly Vector2?[] handScreenPositions = new Vector2?[MaxPlayers * 2];
        static readonly int[] kneePulses = new int[MaxPlayers];
        static readonly int[] punchPulses = new int[MaxPlayers * 2];

        public static bool IsEnabled
        {
            get
            {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
                return PlayerDataManager.Instance.DebugSettings.ninjaSimulatedBody;
#else
                return false;
#endif
            }
        }

        #region Public API

        public static Vector2 GetLeanInches(int playerIndex) => leanInches[playerIndex];

        public static bool IsStorming(int playerIndex) => storming[playerIndex];

        /// <summary>Lean in body inches from the calibrated origin (x right, y up) and whether both hands storm.</summary>
        public static void Set(int playerIndex, float leanX, float leanY, bool handsStorming)
        {
            leanInches[playerIndex] = new Vector2(leanX, leanY);
            storming[playerIndex] = handsStorming;
        }

        /// <summary>Hand cursor in screen-normalized space (0..1, y up); hand 0 = left, 1 = right. False while lowered.</summary>
        public static bool TryGetHand(int playerIndex, int hand, out Vector2 screenPosition)
        {
            var value = handScreenPositions[playerIndex * 2 + hand];
            screenPosition = value ?? default;
            return value.HasValue;
        }

        public static void SetHand(int playerIndex, int hand, float screenX, float screenY)
        {
            handScreenPositions[playerIndex * 2 + hand] = new Vector2(screenX, screenY);
        }

        public static void LowerHand(int playerIndex, int hand)
        {
            handScreenPositions[playerIndex * 2 + hand] = null;
        }

        /// <summary>One knee pulse (a kick) for that player.</summary>
        public static void PulseKnee(int playerIndex) => kneePulses[playerIndex]++;

        /// <summary>Knee pulses since start; readers compare against the count they last saw.</summary>
        public static int GetKneePulses(int playerIndex) => kneePulses[playerIndex];

        /// <summary>One punch with that player's hand (0 = left, 1 = right).</summary>
        public static void PulsePunch(int playerIndex, int hand) => punchPulses[playerIndex * 2 + hand]++;

        /// <summary>Punches since start; readers compare against the count they last saw.</summary>
        public static int GetPunchPulses(int playerIndex, int hand) => punchPulses[playerIndex * 2 + hand];

        #endregion
    }
}
