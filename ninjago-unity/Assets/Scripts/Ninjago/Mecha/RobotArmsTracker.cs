#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Reads each player's robot arms every frame from where the hands are against that player's torso: a hand held up
    /// in front (closer to the chest than to the hips) holds its half of the L; the hands' midpoint against the shoulders
    /// (left/right) and the chest (height) is the aim. Hands come from the team.nex.mdk.hand detector
    /// (playerHands[playerIndex] only, so one player's hands never aim another's mecha); a stale hand falls back to that
    /// player's body hand node. Holds each player's neutral hand spot and torso length from the setup hold.
    /// </summary>
    // Before gameplay Updates, so the lanes read this frame's arms.
    [DefaultExecutionOrder(-100)]
    public class RobotArmsTracker : MonoBehaviour
    {
        [Header("Hand Detector")]
        [Tooltip("Inactive HandPoseDetector instance; activated once its detection managers and tracked hands are assigned.")]
        [SerializeField] HandDetectionManager handDetector = null!;

        public struct Reading
        {
            /// <summary>The chest and both shoulders are visible.</summary>
            public bool bodyTracked;
            public bool handsTracked;
            public bool leftRaised;
            public bool rightRaised;
            /// <summary>How far each hand has sunk down the torso: 0 at the chest, 1 at the hips.</summary>
            public float leftDrop;
            public float rightDrop;
            /// <summary>Hands' midpoint from the shoulders' middle (x = the player's right) and from the chest (y up), in body inches.</summary>
            public Vector2 bodyOffsetInches;
            /// <summary>Chest to hips in body inches; 0 while the hips are hidden.</summary>
            public float torsoInches;

            public readonly bool BothRaised => leftRaised && rightRaised;
        }

        struct HandSample
        {
            public Vector2 position;
            public float time;
        }

        IReadOnlyList<PlayerBody> bodies = null!;
        RobotArmsConfig config = null!;
        Reading[] readings = null!;
        HandSample[] samples = null!;
        Vector2[] neutralOffsets = null!;
        float[] torsoReferences = null!;

        public float SetupHoldSeconds => config.SetupHoldSeconds;

        #region Initialization

        public void Initialize(DetectionManager detection, RobotArmsConfig aConfig, IReadOnlyList<PlayerBody> aBodies)
        {
            config = aConfig;
            bodies = aBodies;
            readings = new Reading[bodies.Count];
            samples = new HandSample[bodies.Count * 2];
            neutralOffsets = new Vector2[bodies.Count];
            torsoReferences = new float[bodies.Count];
            var trackHands = new List<ShouldTrackHands>(bodies.Count);
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++)
            {
                samples[playerIndex * 2].time = samples[playerIndex * 2 + 1].time = float.NegativeInfinity;
                trackHands.Add(new ShouldTrackHands(true, true));
            }

            // The prefab tracks no hand for player 1; the engine reads this list when it starts.
            handDetector.handDetectionConfig.trackHandsList = trackHands;
            handDetector.coreDetectionManager = detection.CvDetectionManager;
            handDetector.optionalBodyPoseDetector = detection.BodyPoseDetectionManager;
            handDetector.shouldDetect = false;
            handDetector.gameObject.SetActive(true);
            handDetector.captureHandPoseDetection += HandleHandPoseDetection;
        }

        void OnDestroy()
        {
            handDetector.captureHandPoseDetection -= HandleHandPoseDetection;
        }

        #endregion

        #region Public API

        public void SetDetecting(bool detecting)
        {
            handDetector.shouldDetect = detecting;
        }

        public Reading Get(int playerIndex) => readings[playerIndex];

        /// <summary>Stores the setup hold: the hands' neutral spot against the body and, when the hips were seen, the torso length (body inches).</summary>
        public void Calibrate(int playerIndex, Vector2 neutralOffsetInches, float? torsoInches)
        {
            neutralOffsets[playerIndex] = neutralOffsetInches;
            torsoReferences[playerIndex] = torsoInches ?? config.TorsoInches;
        }

        public Vector2 NeutralOffset(int playerIndex) => neutralOffsets[playerIndex];

        public float TorsoReference(int playerIndex) => torsoReferences[playerIndex] > 0f ? torsoReferences[playerIndex] : config.TorsoInches;

        /// <summary>The hands' offset from their calibrated spot against the body, in body inches (x = the player's right, y up).</summary>
        public bool TryGetAimInches(PlayerBody body, out Vector2 inches)
        {
            if (SimulatedBody.IsEnabled)
            {
                inches = SimulatedBody.GetLeanInches(body.PlayerIndex);
                return true;
            }

            var reading = readings[body.PlayerIndex];
            inches = reading.bodyOffsetInches - neutralOffsets[body.PlayerIndex];
            return reading.handsTracked;
        }

        #endregion

        #region Sampling

        void HandleHandPoseDetection(HandPoseDetection detection)
        {
            if (detection.playerHands == null) return;
            // Hand landmarks are raw frame pixels (y down); aspect-normalized space is frame heights with y up.
            var frameHeight = detection.frameSize.y;
            if (frameHeight <= 0f) return;
            for (var playerIndex = 0; playerIndex < bodies.Count && playerIndex < detection.playerHands.Count; playerIndex++)
            {
                var hands = detection.playerHands[playerIndex];
                if (hands == null) continue;
                Store(playerIndex * 2, hands.left, frameHeight);
                Store(playerIndex * 2 + 1, hands.right, frameHeight);
            }
        }

        void Store(int slot, HandPose? pose, float frameHeight)
        {
            if (pose == null) return;
            var pixels = pose.Center2D();
            samples[slot] = new HandSample { position = new Vector2(pixels.x / frameHeight, 1f - pixels.y / frameHeight), time = Time.unscaledTime };
        }

        void Update()
        {
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++)
            {
                readings[playerIndex] = SimulatedBody.IsEnabled ? Simulated(playerIndex) : Read(bodies[playerIndex]);
            }
        }

        Reading Read(PlayerBody body)
        {
            var reading = new Reading();
            var ppi = body.RawPpi;
            if (ppi <= 0f || !body.TryGetTorso(out var chest, out var leftShoulder, out var rightShoulder, out var hipY)) return reading;
            reading.bodyTracked = true;
            if (!TryGetHand(body, 0, out var leftHand) || !TryGetHand(body, 1, out var rightHand)) return reading;
            reading.handsTracked = true;
            var hip = hipY ?? chest.y - TorsoReference(body.PlayerIndex) * ppi;
            if (hipY.HasValue) reading.torsoInches = (chest.y - hipY.Value) / ppi;
            reading.leftDrop = RobotArmsPose.HandDrop(leftHand.y, chest.y, hip);
            reading.rightDrop = RobotArmsPose.HandDrop(rightHand.y, chest.y, hip);
            reading.leftRaised = reading.leftDrop < config.SlackLine;
            reading.rightRaised = reading.rightDrop < config.SlackLine;
            reading.bodyOffsetInches = RobotArmsPose.BodyOffset(leftHand, rightHand, leftShoulder, rightShoulder, chest) / ppi;
            return reading;
        }

        // hand 0 = left, 1 = right; the detector's hand while it reports it, else the body hand node.
        bool TryGetHand(PlayerBody body, int hand, out Vector2 position)
        {
            var sample = samples[body.PlayerIndex * 2 + hand];
            if (Time.unscaledTime - sample.time <= config.BodyHandFallbackSeconds)
            {
                position = sample.position;
                return true;
            }

            return body.TryGetHand(hand == 0, out position);
        }

        // Debug stand-in: the hands stay up unless that player's storm key is down; the lean keys move them (TryGetAimInches).
        static Reading Simulated(int playerIndex)
        {
            var holding = !SimulatedBody.IsStorming(playerIndex);
            return new Reading
            {
                bodyTracked = true,
                handsTracked = true,
                leftRaised = holding,
                rightRaised = holding,
                leftDrop = holding ? 0.2f : 0.9f,
                rightDrop = holding ? 0.2f : 0.9f,
            };
        }

        #endregion
    }
}
