#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>One active player in Build: that player's ninja and brick pile, the clap reading and the clap counts.</summary>
    public sealed class BuildPlayer
    {
        // Seconds without a body frame before the reading starts over and the ninja's arms hang.
        const float lostSeconds = 0.6f;

        readonly int[] gestures = new int[2];
        readonly UpperBodyStream stream;
        bool hasPose;
        float lastPoseTime = float.NegativeInfinity;

        public PlayerBody Body { get; }
        public KidNinja Ninja { get; }
        /// <summary>Where this player's bricks fly from.</summary>
        public Transform Pile { get; }
        /// <summary>0 for the left ninja (or the only one), 1 for the right; also the bridge column it builds first.</summary>
        public int Slot { get; }
        public ClapReader Reader { get; } = new();
        public float LastClapAt { get; set; } = float.NegativeInfinity;
        public int PlayerIndex => Body.PlayerIndex;
        public Color Color => Body.Color;

        public BuildPlayer(PlayerBody body, KidNinja ninja, Transform pile, int slot)
        {
            Body = body;
            Ninja = ninja;
            Pile = pile;
            Slot = slot;
            stream = new UpperBodyStream(body);
        }

        #region Public API

        public int Count(BuildGesture gesture) => gestures[(int)gesture];

        public void Record(BuildGesture gesture) => gestures[(int)gesture]++;

        /// <summary>Reads this frame's body (when there is a new one) into the ninja's arms and the clap.</summary>
        public ClapReader.Event Read(float time, in ClapReader.Settings settings)
        {
            if (stream.TryRead(time, out var pose))
            {
                hasPose = true;
                lastPoseTime = time;
                Ninja.CopyArms(pose);
                return Reader.Update(pose, time, settings);
            }

            if (!hasPose || time - lastPoseTime <= lostSeconds) return ClapReader.Event.None;
            hasPose = false;
            Reader.Reset();
            Ninja.ReleaseArms();
            return ClapReader.Event.None;
        }

        #endregion
    }
}
