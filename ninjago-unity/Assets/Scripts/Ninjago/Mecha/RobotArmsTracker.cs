#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Reads each player's robot arms every frame: whether each arm holds the L (that player's body shoulder, elbow and
    /// wrist nodes) and where the two hands are (the team.nex.mdk.hand detector, playerHands[playerIndex] only, so one
    /// player's hands never aim another's mecha; stale hands fall back to that player's body hand nodes). Holds each
    /// player's hand origin and elbow reference from the setup hold.
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
            /// <summary>Both arms' shoulder, elbow and wrist nodes are visible.</summary>
            public bool armsTracked;
            public bool leftArmL;
            public bool rightArmL;
            public float leftElbowAngle;
            public float rightElbowAngle;
            public bool handsTracked;
            /// <summary>Average of the two hands in aspect-normalized frame space (x = the player's right, y up).</summary>
            public Vector2 handMidpoint;

            public readonly bool BothArmsL => leftArmL && rightArmL;
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
        Vector2[] handOrigins = null!;
        Vector2[] elbowReferences = null!;

        public float SetupHoldSeconds => config.SetupHoldSeconds;

        #region Initialization

        public void Initialize(DetectionManager detection, RobotArmsConfig aConfig, IReadOnlyList<PlayerBody> aBodies)
        {
            config = aConfig;
            bodies = aBodies;
            readings = new Reading[bodies.Count];
            samples = new HandSample[bodies.Count * 2];
            handOrigins = new Vector2[bodies.Count];
            elbowReferences = new Vector2[bodies.Count];
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

        /// <summary>Stores the setup hold: the averaged hands' origin and the two elbow angles (x left, y right) while holding the L.</summary>
        public void Calibrate(int playerIndex, Vector2 handOrigin, Vector2 elbowReference)
        {
            handOrigins[playerIndex] = handOrigin;
            elbowReferences[playerIndex] = elbowReference;
        }

        public Vector2 ElbowReference(int playerIndex) => elbowReferences[playerIndex];

        /// <summary>The averaged hands' offset from their calibrated origin in body inches (x = the player's right, y up).</summary>
        public bool TryGetAimInches(PlayerBody body, out Vector2 inches)
        {
            if (SimulatedBody.IsEnabled)
            {
                inches = SimulatedBody.GetLeanInches(body.PlayerIndex);
                return true;
            }

            var reading = readings[body.PlayerIndex];
            inches = default;
            if (!reading.handsTracked || body.RawPpi <= 0f) return false;
            inches = (reading.handMidpoint - handOrigins[body.PlayerIndex]) / body.RawPpi;
            return true;
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
            if (body.RawPpi <= 0f) return reading;
            var drop = config.WristBelowElbowInches * body.RawPpi;
            var leftTracked = body.TryGetArm(true, out var shoulder, out var elbow, out var wrist);
            if (leftTracked)
            {
                reading.leftElbowAngle = RobotArmsPose.ElbowAngle(shoulder, elbow, wrist);
                reading.leftArmL = RobotArmsPose.IsArmL(shoulder, elbow, wrist, config.MinElbowAngle, config.MaxElbowAngle, drop);
            }

            var rightTracked = body.TryGetArm(false, out shoulder, out elbow, out wrist);
            if (rightTracked)
            {
                reading.rightElbowAngle = RobotArmsPose.ElbowAngle(shoulder, elbow, wrist);
                reading.rightArmL = RobotArmsPose.IsArmL(shoulder, elbow, wrist, config.MinElbowAngle, config.MaxElbowAngle, drop);
            }

            reading.armsTracked = leftTracked && rightTracked;
            reading.handsTracked = TryGetHandMidpoint(body, out reading.handMidpoint);
            return reading;
        }

        // Both hands from the detector while it reports them, else both body hand nodes; never one of each, so the
        // midpoint does not jump when only one hand's source changes.
        bool TryGetHandMidpoint(PlayerBody body, out Vector2 midpoint)
        {
            var now = Time.unscaledTime;
            var left = samples[body.PlayerIndex * 2];
            var right = samples[body.PlayerIndex * 2 + 1];
            var fallbackAfter = config.BodyHandFallbackSeconds;
            if (now - left.time <= fallbackAfter && now - right.time <= fallbackAfter)
            {
                midpoint = (left.position + right.position) * 0.5f;
                return true;
            }

            midpoint = default;
            if (!body.TryGetHand(true, out var leftHand) || !body.TryGetHand(false, out var rightHand)) return false;
            midpoint = (leftHand + rightHand) * 0.5f;
            return true;
        }

        // Debug stand-in: the L holds unless that player's storm key is down; the lean keys move the hands (TryGetAimInches).
        static Reading Simulated(int playerIndex)
        {
            var holding = !SimulatedBody.IsStorming(playerIndex);
            return new Reading
            {
                armsTracked = true,
                leftArmL = holding,
                rightArmL = holding,
                leftElbowAngle = holding ? 90f : 170f,
                rightElbowAngle = holding ? 90f : 170f,
                handsTracked = true,
            };
        }

        #endregion
    }
}
