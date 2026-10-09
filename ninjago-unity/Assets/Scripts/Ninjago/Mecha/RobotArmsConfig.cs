#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Robot Mecha input tuning: the robot arms slack line, the hand aim mapping and the turn speeds. Setters exist for the Debug
    /// Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotArmsConfig", menuName = "Nex/Ninjago/Robot Arms Config")]
    public class RobotArmsConfig : ScriptableObject
    {
        [Header("Pose Window")]
        [Tooltip("The robot L is elbows down by the sides, forearms bent up in front. A hand that sinks below this line between " +
            "the chest (0) and the hips (1) is slacking and breaks the L; 0.5 = closer to the hips than to the chest.")]
        [SerializeField, Range(0.1f, 1f)] float slackLine = 0.5f;
        [Tooltip("Body inches from the chest down to the hips, used while the hips are out of view (setup measures each player when it can).")]
        [SerializeField, Range(8f, 30f)] float torsoInches = 17f;
        [Tooltip("Seconds both arms must hold the L before steering turns on; filters one noisy frame.")]
        [SerializeField, Range(0f, 1f)] float poseGainSeconds = 0.1f;
        [Tooltip("Seconds either arm may break the L before steering cuts off; filters one dropped frame.")]
        [SerializeField, Range(0f, 1f)] float poseLossSeconds = 0.2f;
        [Tooltip("Setup: seconds a player holds the L with both arms before the hand origin and elbow reference are stored.")]
        [SerializeField, Range(0.3f, 4f)] float setupHoldSeconds = 1f;

        [Header("Hands")]
        [Tooltip("Seconds without a team.nex.mdk.hand detection before the hands fall back to the body hand nodes.")]
        [SerializeField, Range(0.05f, 2f)] float bodyHandFallbackSeconds = 0.3f;

        [Header("Aim")]
        [Tooltip("Body inches the averaged hands may drift from their calibrated spot (left/right from the shoulders, height from " +
            "the chest) without steering (breathing).")]
        [SerializeField, Range(0f, 6f)] float deadzoneInches = 1f;
        [Tooltip("Body inches the hands move sideways from the shoulders' middle, past the deadzone, to ask for the whole yaw range.")]
        [SerializeField, Range(2f, 30f)] float fullYawOffsetInches = 7f;
        [Tooltip("Yaw the whole sideways offset asks for; 180 = hands far to one side turn the mecha to face behind.")]
        [SerializeField, Range(30f, 180f)] float yawRangeDegrees = 180f;
        [Tooltip("Body inches the hands rise or sink against the chest, past the deadzone, to ask for the whole pitch.")]
        [SerializeField, Range(2f, 30f)] float fullPitchOffsetInches = 6f;
        [Tooltip("Pitch limit up and down in degrees.")]
        [SerializeField, Range(5f, 80f)] float pitchClampDegrees = 40f;
        [Tooltip("Swaps the axes: hand height turns the mecha and left/right pitches it.")]
        [SerializeField] bool swapAxes;

        [Header("Turn")]
        [Tooltip("Top yaw speed in degrees per second; 78 turns the mecha from forward to behind in about 2.5 s.")]
        [SerializeField, Range(10f, 360f)] float yawSpeed = 78f;
        [Tooltip("Top pitch speed in degrees per second.")]
        [SerializeField, Range(5f, 180f)] float pitchSpeed = 35f;
        [Tooltip("Turn acceleration and braking in degrees per second squared; lower feels heavier.")]
        [SerializeField, Range(30f, 2000f)] float turnAcceleration = 320f;

        #region Public API

        public float SlackLine { get => slackLine; set => slackLine = Mathf.Clamp(value, 0.05f, 1.5f); }
        public float TorsoInches => torsoInches;
        public float PoseGainSeconds => poseGainSeconds;
        public float PoseLossSeconds => poseLossSeconds;
        public float SetupHoldSeconds => setupHoldSeconds;
        public float BodyHandFallbackSeconds => bodyHandFallbackSeconds;
        public float DeadzoneInches { get => deadzoneInches; set => deadzoneInches = Mathf.Max(0f, value); }
        public float FullYawOffsetInches { get => fullYawOffsetInches; set => fullYawOffsetInches = Mathf.Max(0.5f, value); }
        public float FullPitchOffsetInches { get => fullPitchOffsetInches; set => fullPitchOffsetInches = Mathf.Max(0.5f, value); }
        public float PitchClampDegrees { get => pitchClampDegrees; set => pitchClampDegrees = Mathf.Clamp(value, 1f, 89f); }
        public bool SwapAxes { get => swapAxes; set => swapAxes = value; }
        public float YawSpeed { get => yawSpeed; set => yawSpeed = Mathf.Max(1f, value); }
        public float PitchSpeed { get => pitchSpeed; set => pitchSpeed = Mathf.Max(1f, value); }
        public float TurnAcceleration => turnAcceleration;

        public MechaAim.Settings AimSettings => new(deadzoneInches, fullYawOffsetInches, yawRangeDegrees, fullPitchOffsetInches, pitchClampDegrees, swapAxes);

        #endregion
    }
}
