#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One tracked player: reads that player's DetectionManager engine nodes and holds the body origin stored at
    /// setup. Lean and hand values are body inches (the engine's ppi), so player height and distance cancel out.
    /// </summary>
    public sealed class PlayerBody
    {
        // Same hand point as BaseOnePlayerDetectionEngine: past the wrist along the forearm.
        const float elbowToWristHandRatio = 1.35f;

        readonly OnePlayerDetectionEngine engine;

        public int PlayerIndex { get; }
        public Color Color { get; }
        /// <summary>Calibrated chest position in aspect-normalized frame space (x right, y up).</summary>
        public Vector2 Origin { get; private set; }
        /// <summary>Hip-to-knee drop of each leg (x left, y right) in body inches while standing at setup; null when the knees were hidden.</summary>
        public Vector2? RestingKneeDrops { get; private set; }
        /// <summary>Shoulders, head and hands against the chest at setup (typical proportions where setup never saw them).</summary>
        public BodyShape Shape { get; private set; } = BodyShape.Typical;
        /// <summary>Shoulders, wrists and hips against the chest at setup, for games whose setup stores them (typical proportions otherwise).</summary>
        public RestJoints Rest { get; private set; } = RestJoints.Typical;
        public bool IsCalibrated { get; private set; }
        /// <summary>Frame heights per inch for this player, averaged by the engine; 0 before the first detection.</summary>
        public float RawPpi => engine.RawPpi;

        public PlayerBody(int playerIndex, Color color, OnePlayerDetectionEngine engine)
        {
            PlayerIndex = playerIndex;
            Color = color;
            this.engine = engine;
        }

        #region Calibration

        public void SetOrigin(Vector2 chest, Vector2? restingKneeDrops, BodyShape? shape)
        {
            Origin = chest;
            RestingKneeDrops = restingKneeDrops;
            Shape = shape ?? BodyShape.Typical;
            IsCalibrated = true;
        }

        public void SetRestJoints(RestJoints joints)
        {
            Rest = joints;
        }

        public void ClearOrigin()
        {
            IsCalibrated = false;
            Rest = RestJoints.Typical;
        }

        #endregion

        #region Nodes

        /// <summary>Smoothed chest in aspect-normalized space; false while this player's chest is not tracked.</summary>
        public bool TryGetChest(out Vector2 chest)
        {
            chest = default;
            if (engine.RawPpi <= 0f) return false;
            var pose = engine.LastBodyPoseDetectionResult.processed?.GetPlayerPose(PlayerIndex)?.bodyPose;
            if (pose == null) return false;
            var node = pose.Chest();
            if (!node.isDetected) return false;
            chest = node.ToVector2();
            return true;
        }

        /// <summary>Chest offset from the calibrated origin in body inches (x right = the player's right, y up).</summary>
        public bool TryGetLeanInches(out Vector2 lean)
        {
            if (SimulatedBody.IsEnabled)
            {
                lean = SimulatedBody.GetLeanInches(PlayerIndex);
                return true;
            }

            lean = default;
            if (!IsCalibrated || !TryGetChest(out var chest)) return false;
            lean = (chest - Origin) / engine.RawPpi;
            return true;
        }

        /// <summary>Body hand node relative to the chest, in body inches; false while either node is hidden.</summary>
        public bool TryGetHandInches(bool leftHand, out Vector2 inches)
        {
            inches = default;
            if (engine.RawPpi <= 0f) return false;
            var hand = engine.GetNodePosition(leftHand ? PoseNodeIndex.LeftHand : PoseNodeIndex.RightHand, true);
            var chest = engine.GetNodePosition(PoseNodeIndex.Chest, true);
            if (hand == null || chest == null) return false;
            inches = (Vector2)(hand.Value - chest.Value) / engine.DistancePerInch;
            return true;
        }

        /// <summary>Smoothed body hand node in aspect-normalized space; false while its elbow or wrist is hidden.</summary>
        public bool TryGetHand(bool leftHand, out Vector2 hand)
        {
            hand = default;
            var pose = Pose;
            if (pose == null) return false;
            var elbow = leftHand ? pose.LeftElbow() : pose.RightElbow();
            var wrist = leftHand ? pose.LeftWrist() : pose.RightWrist();
            if (!elbow.isDetected || !wrist.isDetected) return false;
            hand = Vector2.LerpUnclamped(elbow.ToVector2(), wrist.ToVector2(), elbowToWristHandRatio);
            return true;
        }

        /// <summary>
        /// Smoothed chest and shoulders in aspect-normalized space, and the hips' average height (null while both hips are
        /// hidden); false while the chest or a shoulder is hidden.
        /// </summary>
        public bool TryGetTorso(out Vector2 chest, out Vector2 leftShoulder, out Vector2 rightShoulder, out float? hipY)
        {
            chest = leftShoulder = rightShoulder = default;
            hipY = null;
            var pose = Pose;
            if (pose == null) return false;
            var chestNode = pose.Chest();
            var leftShoulderNode = pose.LeftShoulder();
            var rightShoulderNode = pose.RightShoulder();
            if (!chestNode.isDetected || !leftShoulderNode.isDetected || !rightShoulderNode.isDetected) return false;
            chest = chestNode.ToVector2();
            leftShoulder = leftShoulderNode.ToVector2();
            rightShoulder = rightShoulderNode.ToVector2();
            var leftHip = pose.LeftHip();
            var rightHip = pose.RightHip();
            if (leftHip.isDetected && rightHip.isDetected) hipY = (leftHip.ToVector2().y + rightHip.ToVector2().y) * 0.5f;
            else if (leftHip.isDetected) hipY = leftHip.ToVector2().y;
            else if (rightHip.isDetected) hipY = rightHip.ToVector2().y;
            return true;
        }

        /// <summary>Vertical hip-to-knee drop of each leg (x left, y right) in body inches; a lifted knee shrinks it.</summary>
        public bool TryGetKneeDrops(out Vector2 drops)
        {
            drops = default;
            var pose = Pose;
            if (pose == null) return false;
            var leftHip = pose.LeftHip();
            var leftKnee = pose.LeftKnee();
            var rightHip = pose.RightHip();
            var rightKnee = pose.RightKnee();
            if (!leftHip.isDetected || !leftKnee.isDetected || !rightHip.isDetected || !rightKnee.isDetected) return false;
            drops = new Vector2(leftHip.ToVector2().y - leftKnee.ToVector2().y, rightHip.ToVector2().y - rightKnee.ToVector2().y) / engine.RawPpi;
            return true;
        }

        /// <summary>Head (the nose, else the eyes or ears) relative to the chest in body inches; false while hidden.</summary>
        public bool TryGetHeadInches(out Vector2 inches)
        {
            inches = default;
            var pose = Pose;
            if (pose == null) return false;
            var chest = pose.Chest();
            if (!chest.isDetected || !TryGetHead(pose, out var head)) return false;
            inches = (head - chest.ToVector2()) / engine.RawPpi;
            return true;
        }

        /// <summary>One shoulder relative to the chest in body inches; false while hidden.</summary>
        public bool TryGetShoulderInches(bool leftShoulder, out Vector2 inches)
        {
            inches = default;
            var pose = Pose;
            if (pose == null) return false;
            var chest = pose.Chest();
            var shoulder = leftShoulder ? pose.LeftShoulder() : pose.RightShoulder();
            if (!chest.isDetected || !shoulder.isDetected) return false;
            inches = (shoulder.ToVector2() - chest.ToVector2()) / engine.RawPpi;
            return true;
        }

        /// <summary>This frame's shoulders, head and body hand nodes against the chest; false while any of them is hidden.</summary>
        public bool TrySampleShape(out BodyShape shape)
        {
            shape = default;
            if (!TryGetShoulderInches(true, out var leftShoulder) || !TryGetShoulderInches(false, out var rightShoulder) ||
                !TryGetHeadInches(out var head) || !TryGetChest(out var chest) || !TryGetHand(true, out var leftHand) || !TryGetHand(false, out var rightHand))
            {
                return false;
            }

            shape = new BodyShape(leftShoulder, rightShoulder, head, (leftHand - chest) / engine.RawPpi, (rightHand - chest) / engine.RawPpi);
            return true;
        }

        /// <summary>
        /// This frame's shoulders, wrists and head against the chest; false while the chest, a shoulder or a wrist is
        /// hidden. A hidden head is placed a typical 9 inches above the shoulders.
        /// </summary>
        public bool TrySampleUpperBody(out UpperBody upper)
        {
            upper = default;
            if (!TryGetArmInches(true, out var leftShoulder, out var rightShoulder, out var leftWrist) ||
                !TryGetArmInches(false, out _, out _, out var rightWrist))
            {
                return false;
            }

            var head = TryGetHeadInches(out var headInches) ? headInches : new Vector2(0f, (leftShoulder.y + rightShoulder.y) * 0.5f + 9f);
            upper = new UpperBody(leftShoulder, rightShoulder, leftWrist, rightWrist, head);
            return true;
        }

        /// <summary>This frame's shoulders, wrists and hip center against the chest; false while any of them or the chest is hidden.</summary>
        public bool TrySampleRestJoints(out RestJoints joints)
        {
            joints = default;
            if (!TryGetArmInches(true, out var leftShoulder, out var rightShoulder, out var leftWrist) ||
                !TryGetArmInches(false, out _, out _, out var rightWrist) || !TryGetHipCenter(out var hips) || !TryGetChest(out var chest))
            {
                return false;
            }

            joints = new RestJoints(leftShoulder, rightShoulder, leftWrist, rightWrist, (hips - chest) / engine.RawPpi);
            return true;
        }

        /// <summary>
        /// One arm against the chest in body inches: that side's shoulder, the other shoulder and that side's wrist; false
        /// while any of them or the chest is hidden.
        /// </summary>
        public bool TryGetArmInches(bool left, out Vector2 shoulder, out Vector2 otherShoulder, out Vector2 wrist)
        {
            shoulder = otherShoulder = wrist = default;
            var pose = Pose;
            if (pose == null) return false;
            var chest = pose.Chest();
            var shoulderNode = left ? pose.LeftShoulder() : pose.RightShoulder();
            var otherNode = left ? pose.RightShoulder() : pose.LeftShoulder();
            var wristNode = left ? pose.LeftWrist() : pose.RightWrist();
            if (!chest.isDetected || !shoulderNode.isDetected || !otherNode.isDetected || !wristNode.isDetected) return false;
            var origin = chest.ToVector2();
            shoulder = (shoulderNode.ToVector2() - origin) / engine.RawPpi;
            otherShoulder = (otherNode.ToVector2() - origin) / engine.RawPpi;
            wrist = (wristNode.ToVector2() - origin) / engine.RawPpi;
            return true;
        }

        /// <summary>
        /// Hip center against the calibrated chest origin in body inches; false while either hip is hidden. The simulated
        /// body sways with the lean keys and shakes its hips while its storm key is held.
        /// </summary>
        public bool TryGetHipInches(out Vector2 hips)
        {
            if (SimulatedBody.IsEnabled)
            {
                var shake = SimulatedBody.IsStorming(PlayerIndex) ? 3f * Mathf.Sin(2f * Mathf.PI * 2.5f * Time.time) : 0f;
                hips = SimulatedBody.GetLeanInches(PlayerIndex) + new Vector2(shake, Rest.hips.y);
                return true;
            }

            hips = default;
            if (!IsCalibrated || !TryGetHipCenter(out var center)) return false;
            hips = (center - Origin) / engine.RawPpi;
            return true;
        }

        /// <summary>Camera timestamp (seconds) of the body detection behind the current node values.</summary>
        public double BodyFrameTime => engine.LastBodyPoseDetectionResult.original?.frameTime ?? 0d;

        BodyPose? Pose => engine.RawPpi > 0f ? engine.LastBodyPoseDetectionResult.processed?.GetPlayerPose(PlayerIndex)?.bodyPose : null;

        // Midpoint of both hips in aspect-normalized space.
        bool TryGetHipCenter(out Vector2 center)
        {
            center = default;
            var pose = Pose;
            if (pose == null) return false;
            var leftHip = pose.LeftHip();
            var rightHip = pose.RightHip();
            if (!leftHip.isDetected || !rightHip.isDetected) return false;
            center = (leftHip.ToVector2() + rightHip.ToVector2()) * 0.5f;
            return true;
        }

        static bool TryGetHead(BodyPose pose, out Vector2 head)
        {
            head = default;
            var nose = pose.Nose();
            if (nose.isDetected)
            {
                head = nose.ToVector2();
                return true;
            }

            var left = pose.LeftEye().isDetected ? pose.LeftEye() : pose.LeftEar();
            var right = pose.RightEye().isDetected ? pose.RightEye() : pose.RightEar();
            if (!left.isDetected || !right.isDetected) return false;
            head = (left.ToVector2() + right.ToVector2()) * 0.5f;
            return true;
        }

        #endregion
    }
}
