#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One active player in Call: that player's ninja, the call reading, the goblins piled on the ninja, and how much the
    /// hands moved (to tell whether a child could finish without wild movement).
    /// </summary>
    public sealed class CallPlayer
    {
        // Seconds without a body frame before the reading starts over and the ninja's arms hang.
        const float lostSeconds = 0.6f;

        readonly int[] results = new int[4];
        readonly UpperBodyStream stream;
        readonly List<StreetMonster> pile = new();
        UpperBody lastPose;
        bool hasPose;
        float lastPoseTime = float.NegativeInfinity;

        public PlayerBody Body { get; }
        public KidNinja Ninja { get; }
        /// <summary>0 for the left ninja (or the only one), 1 for the right.</summary>
        public int Slot { get; }
        public CallReader Reader { get; } = new();
        /// <summary>Body inches both wrists travelled while tracked.</summary>
        public float HandTravel { get; private set; }
        public float TrackedSeconds { get; private set; }
        public float AverageHandSpeed => TrackedSeconds > 0f ? HandTravel / TrackedSeconds : 0f;
        public IReadOnlyList<StreetMonster> Pile => pile;
        /// <summary>Where the next goblin to climb on sits: the hood, or the top goblin's head.</summary>
        public Transform PileTop => pile.Count == 0 ? Ninja.HeadPoint : pile[^1].Top;
        public int Understood => results[(int)CallResult.Understood] + results[(int)CallResult.Queued] + results[(int)CallResult.Team];
        public int PlayerIndex => Body.PlayerIndex;
        public Color Color => Body.Color;

        public CallPlayer(PlayerBody body, KidNinja ninja, int slot)
        {
            Body = body;
            Ninja = ninja;
            Slot = slot;
            stream = new UpperBodyStream(body);
        }

        #region Public API

        public int Count(CallResult result) => results[(int)result];

        public void Record(CallResult result) => results[(int)result]++;

        public void AddToPile(StreetMonster goblin) => pile.Add(goblin);

        public void ClearPile() => pile.Clear();

        /// <summary>Reads this frame's body (when there is a new one) into the ninja's arms, the call and the hand travel.</summary>
        public CallReader.Event Read(float time, in CallReader.Settings settings)
        {
            if (stream.TryRead(time, out var pose))
            {
                if (hasPose && time - lastPoseTime <= lostSeconds)
                {
                    HandTravel += (pose.leftWrist - lastPose.leftWrist).magnitude + (pose.rightWrist - lastPose.rightWrist).magnitude;
                    TrackedSeconds += time - lastPoseTime;
                }

                lastPose = pose;
                hasPose = true;
                lastPoseTime = time;
                Ninja.CopyArms(pose);
                return Reader.Update(pose, settings);
            }

            if (!hasPose || time - lastPoseTime <= lostSeconds) return CallReader.Event.None;
            hasPose = false;
            Reader.Reset();
            Ninja.ReleaseArms();
            return CallReader.Event.None;
        }

        #endregion
    }
}
