#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Robot Mecha setup step after the body calibration: under a big L and ROBOT ARMS, each player holds the L with
    /// both arms (elbows down, forearms up in front) until their card fills; that hold stores the player's hand origin
    /// (the hands' neutral spot against the body) and torso length. The large camera preview stays up so the players see
    /// their own arms.
    /// </summary>
    public class RobotArmsCheck : MonoBehaviour
    {
        [Header("Check Root")]
        [Tooltip("Hidden until the body calibration is done.")]
        [SerializeField] GameObject root = null!;
        [Header("Cards Row")]
        [SerializeField] RectTransform cardsRow = null!;
        [Header("Card Prefab")]
        [SerializeField] RobotArmsCard cardPrefab = null!;
        [Header("Prompt Label")]
        [SerializeField] NexLocalizedString promptLabel = null!;
        [Header("Prompt: Hold The L")]
        [SerializeField] LocalizedString holdPrompt = new();
        [Header("Prompt: Ready")]
        [SerializeField] LocalizedString readyPrompt = new();
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        struct Hold
        {
            public float progress;
            public Vector2 offsetSum;
            public int count;
            public float torsoSum;
            public int torsoCount;
        }

        readonly List<RobotArmsCard> cards = new();
        IReadOnlyList<PlayerBody> players = null!;
        RobotArmsTracker tracker = null!;
        Hold[] holds = null!;

        #region Public API

        public void Show(IReadOnlyList<PlayerBody> aPlayers, RobotArmsTracker aTracker, DetectionManager detection, int requestedPlayers)
        {
            players = aPlayers;
            tracker = aTracker;
            holds = new Hold[players.Count];
            root.SetActive(true);
            promptLabel.StringReference = holdPrompt;
            var indicatorPlayers = new List<int>(players.Count);
            foreach (var player in players)
            {
                var card = Instantiate(cardPrefab, cardsRow);
                card.Initialize(player.PlayerIndex, player.Color);
                cards.Add(card);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        /// <summary>Advances one player's hold; true on the frame it completes and that player's arms are calibrated.</summary>
        public bool Tick(int slot, float deltaTime)
        {
            var playerIndex = players[slot].PlayerIndex;
            var reading = tracker.Get(playerIndex);
            ref var hold = ref holds[slot];
            var step = deltaTime / tracker.SetupHoldSeconds;
            if (reading.BothRaised)
            {
                hold.progress = Mathf.Min(1f, hold.progress + step);
                hold.offsetSum += reading.bodyOffsetInches;
                hold.count++;
                if (reading.torsoInches > 0f)
                {
                    hold.torsoSum += reading.torsoInches;
                    hold.torsoCount++;
                }
            }
            else
            {
                hold.progress = Mathf.Max(0f, hold.progress - 2f * step);
                // Only a continuous hold is averaged: a fully broken hold starts its samples again.
                if (hold.progress <= 0f) hold = default;
            }

            cards[slot].Show(reading.leftRaised, reading.rightRaised, hold.progress);
            if (hold.progress < 1f) return false;
            tracker.Calibrate(playerIndex, hold.offsetSum / hold.count, hold.torsoCount > 0 ? hold.torsoSum / hold.torsoCount : null);
            cards[slot].ShowDone();
            return true;
        }

        public void ShowReady()
        {
            promptLabel.StringReference = readyPrompt;
        }

        #endregion
    }
}
