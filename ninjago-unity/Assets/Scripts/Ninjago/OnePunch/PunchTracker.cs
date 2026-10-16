#nullable enable

using System;
using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Reads every listening player's two punch detectors. Arms come from that player's body nodes on each new body
    /// frame; the hand scale comes from the team.nex.mdk.hand detector (playerHands[playerIndex] only, so one player's
    /// hands never punch for another). The hand scale is HandPose.CalculatePpi over that player's body ppi: it grows as
    /// the hand nears the lens, but not when the whole player walks closer. The simulated body punches with its punch
    /// keys and ignores the hand detector.
    /// </summary>
    // Before gameplay Updates, so the street reads this frame's punches.
    [DefaultExecutionOrder(-100)]
    public class PunchTracker : MonoBehaviour
    {
        [Header("Hand Detector")]
        [Tooltip("Inactive HandPoseDetector instance; activated once its detection managers and tracked hands are assigned.")]
        [SerializeField] HandDetectionManager handDetector = null!;

        IReadOnlyList<PlayerBody> bodies = null!;
        OnePunchConfig config = null!;
        PunchDetector[] detectors = null!;
        bool[] listening = null!;
        double[] lastBodyFrames = null!;
        int[] seenSimulatedPunches = null!;

        /// <summary>A try closed: player index, hand (0 = left, 1 = right) and how it was read.</summary>
        public event Action<int, int, PunchDetector.Try>? Tried;

        /// <summary>Hand scale samples received since start; none means the toward-camera cue never existed.</summary>
        public int HandScaleSamples { get; private set; }

        /// <summary>Latest hand scale against the body per player and hand (index playerIndex * 2 + hand), for the debug readout.</summary>
        public float[] LastHandScales { get; private set; } = null!;

        #region Initialization

        public void Initialize(DetectionManager detection, OnePunchConfig aConfig, IReadOnlyList<PlayerBody> aBodies)
        {
            config = aConfig;
            bodies = aBodies;
            detectors = new PunchDetector[bodies.Count * 2];
            for (var i = 0; i < detectors.Length; i++) detectors[i] = new PunchDetector();
            listening = new bool[bodies.Count];
            lastBodyFrames = new double[bodies.Count];
            seenSimulatedPunches = new int[bodies.Count * 2];
            LastHandScales = new float[bodies.Count * 2];
            var trackHands = new List<ShouldTrackHands>(bodies.Count);
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++) trackHands.Add(new ShouldTrackHands(true, true));

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

        /// <summary>Starts or stops reading tries for a player; either way that player's detectors start fresh.</summary>
        public void SetListening(int playerIndex, bool isListening)
        {
            listening[playerIndex] = isListening;
            for (var hand = 0; hand < 2; hand++)
            {
                detectors[playerIndex * 2 + hand].Reset();
                seenSimulatedPunches[playerIndex * 2 + hand] = SimulatedBody.GetPunchPulses(playerIndex, hand);
            }
        }

        #endregion

        #region Sampling

        void HandleHandPoseDetection(HandPoseDetection detection)
        {
            if (detection.playerHands == null) return;
            var frameHeight = detection.frameSize.y;
            if (frameHeight <= 0f) return;
            var settings = config.PunchSettings;
            var now = Time.unscaledTime;
            for (var playerIndex = 0; playerIndex < bodies.Count && playerIndex < detection.playerHands.Count; playerIndex++)
            {
                var hands = detection.playerHands[playerIndex];
                if (hands == null) continue;
                AddScale(playerIndex, 0, hands.left, frameHeight, now, settings);
                AddScale(playerIndex, 1, hands.right, frameHeight, now, settings);
            }
        }

        void AddScale(int playerIndex, int hand, HandPose? pose, float frameHeight, float now, in PunchDetector.Settings settings)
        {
            var bodyPpi = bodies[playerIndex].RawPpi;
            if (pose == null || SimulatedBody.IsEnabled || bodyPpi <= 0f) return;
            var scale = pose.CalculatePpi() / frameHeight / bodyPpi;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;
            HandScaleSamples++;
            LastHandScales[playerIndex * 2 + hand] = scale;
            if (listening[playerIndex]) Emit(playerIndex, hand, detectors[playerIndex * 2 + hand].AddHandScale(now, scale, settings));
        }

        void Update()
        {
            var settings = config.PunchSettings;
            var now = Time.unscaledTime;
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++)
            {
                if (!listening[playerIndex]) continue;
                if (SimulatedBody.IsEnabled)
                {
                    ReadSimulated(playerIndex, now);
                    continue;
                }

                var body = bodies[playerIndex];
                if (body.BodyFrameTime != lastBodyFrames[playerIndex])
                {
                    lastBodyFrames[playerIndex] = body.BodyFrameTime;
                    for (var hand = 0; hand < 2; hand++)
                    {
                        if (!body.TryGetArmInches(hand == 0, out var shoulder, out var otherShoulder, out var wrist)) continue;
                        Emit(playerIndex, hand, detectors[playerIndex * 2 + hand].AddArm(now, shoulder, otherShoulder, wrist, settings));
                    }
                }

                for (var hand = 0; hand < 2; hand++) Emit(playerIndex, hand, detectors[playerIndex * 2 + hand].Tick(now, settings));
            }
        }

        // Each punch key press is one extension punch.
        void ReadSimulated(int playerIndex, float now)
        {
            for (var hand = 0; hand < 2; hand++)
            {
                var pulses = SimulatedBody.GetPunchPulses(playerIndex, hand);
                if (pulses == seenSimulatedPunches[playerIndex * 2 + hand]) continue;
                seenSimulatedPunches[playerIndex * 2 + hand] = pulses;
                Tried?.Invoke(playerIndex, hand, new PunchDetector.Try(PunchSignal.Extension, now, 0f, 0f, 0f, 0f));
            }
        }

        void Emit(int playerIndex, int hand, PunchDetector.Try? attempt)
        {
            if (attempt.HasValue) Tried?.Invoke(playerIndex, hand, attempt.Value);
        }

        #endregion
    }
}
