#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One hand's punch reader, from flat 2D joints. A fast wrist move (against the chest) with the wrist up at chest
    /// height opens a try. The try is a punch the moment either signal fires, unless it is a side slash; otherwise it
    /// closes after the try window as a side slash or nothing. Extension: the shoulder-to-wrist distance grows fast and
    /// the wrist ends in front of the chest, between the shoulders. Toward camera: the hand's scale against the body's
    /// (median of three hand frames) grows fast while the hand is held in front of the chest. Side slash: the wrist races
    /// sideways with almost no change in that distance. Each try rests the hand for the cooldown.
    /// </summary>
    public sealed class PunchDetector
    {
        // Every speed and growth spans at least this long, so jitter between frames that arrive together is not a fast move.
        const float speedSpanSeconds = 0.1f;
        // A wrist faster than this between two body frames is a tracking glitch (a left/right swap, a guess off the frame), not a hand.
        const float maxWristSpeed = 180f;
        // The shortest frame gap the glitch speed is measured over.
        const float minFrameSeconds = 1f / 30f;
        // Lowering the hand also lengthens the arm: during an extension the wrist may drop at most this much per inch sideways.
        const float maxDropPerSideInch = 1.5f;
        // Growth is only read across consecutive frames: a joint or hand found again after a gap may simply have been missed while it moved.
        const float maxGapSeconds = 0.25f;

        public readonly struct Settings
        {
            public readonly float trySpeed;
            public readonly float tryWindowSeconds;
            public readonly float cooldownSeconds;
            public readonly float signalWindowSeconds;
            public readonly float extensionSpeed;
            public readonly float extensionMinGrowth;
            public readonly float chestFrontWindow;
            public readonly float chestFrontMargin;
            public readonly float handScaleSpeed;
            public readonly float handScaleMinGrowth;
            public readonly float slashLengthRatio;
            public readonly float slashMinTravel;

            public Settings(float trySpeed, float tryWindowSeconds, float cooldownSeconds, float signalWindowSeconds, float extensionSpeed,
                float extensionMinGrowth, float chestFrontWindow, float chestFrontMargin, float handScaleSpeed, float handScaleMinGrowth,
                float slashLengthRatio, float slashMinTravel)
            {
                this.trySpeed = trySpeed;
                this.tryWindowSeconds = tryWindowSeconds;
                this.cooldownSeconds = cooldownSeconds;
                this.signalWindowSeconds = signalWindowSeconds;
                this.extensionSpeed = extensionSpeed;
                this.extensionMinGrowth = extensionMinGrowth;
                this.chestFrontWindow = chestFrontWindow;
                this.chestFrontMargin = chestFrontMargin;
                this.handScaleSpeed = handScaleSpeed;
                this.handScaleMinGrowth = handScaleMinGrowth;
                this.slashLengthRatio = slashLengthRatio;
                this.slashMinTravel = slashMinTravel;
            }
        }

        public readonly struct Try
        {
            public readonly PunchSignal signal;
            public readonly float time;
            /// <summary>Shoulder-to-wrist growth (inches) behind an extension, and its speed (in/s).</summary>
            public readonly float growth;
            public readonly float growthSpeed;
            /// <summary>Hand scale growth behind a toward-camera punch (0.2 = 20% bigger).</summary>
            public readonly float scaleGrowth;
            /// <summary>Sideways wrist travel (inches) since the try opened.</summary>
            public readonly float sideTravel;

            public Try(PunchSignal signal, float time, float growth, float growthSpeed, float scaleGrowth, float sideTravel)
            {
                this.signal = signal;
                this.time = time;
                this.growth = growth;
                this.growthSpeed = growthSpeed;
                this.scaleGrowth = scaleGrowth;
                this.sideTravel = sideTravel;
            }

            public bool IsPunch => signal is PunchSignal.Extension or PunchSignal.TowardCamera;
        }

        struct ArmSample
        {
            public float time;
            public Vector2 wrist;
            public float length;
        }

        struct ScaleSample
        {
            public float time;
            public float scale;
            /// <summary>The hand was in front of the chest; hands elsewhere (hanging, turned edge-on) give no usable scale.</summary>
            public bool inFront;
        }

        readonly List<ArmSample> arms = new();
        readonly List<ScaleSample> scales = new();
        // The last three raw wrists and shoulders; the signals read their medians, so one jittery or glitched frame does not count.
        readonly Vector2[] recentWrists = new Vector2[3];
        readonly Vector2[] recentShoulders = new Vector2[3];
        int recentArmCount;
        float lastArmTime = float.NegativeInfinity;
        // The last three raw hand scales; their median is what the signal reads, so a one-frame spike does not count.
        readonly float[] recentScales = new float[3];
        int recentScaleCount;
        float lastScaleTime = float.NegativeInfinity;
        Vector2 lastShoulder;
        Vector2 lastOtherShoulder;
        bool hasShoulders;
        bool tryOpen;
        float tryStart;
        Vector2 tryStartWrist;
        float tryStartLength;
        bool slashSeen;
        bool racingSideways;
        float sideTravel;
        float restUntil = float.NegativeInfinity;

        #region Public API

        /// <summary>One body frame of this arm, every point in body inches against the chest (x = the player's right, y up).</summary>
        /// <returns>The try this frame closed, if any.</returns>
        public Try? AddArm(float time, Vector2 rawShoulder, Vector2 otherShoulder, Vector2 rawWrist, in Settings settings)
        {
            if (time - lastArmTime > maxGapSeconds)
            {
                arms.Clear();
                recentArmCount = 0;
            }

            lastArmTime = time;
            recentWrists[recentArmCount % 3] = rawWrist;
            recentShoulders[recentArmCount % 3] = rawShoulder;
            recentArmCount++;
            if (recentArmCount < 3) return Tick(time, settings);
            var wrist = Median(recentWrists[0], recentWrists[1], recentWrists[2]);
            var shoulder = Median(recentShoulders[0], recentShoulders[1], recentShoulders[2]);
            var length = (wrist - shoulder).magnitude;
            if (arms.Count > 0 && (wrist - arms[^1].wrist).magnitude / Mathf.Max(time - arms[^1].time, minFrameSeconds) > maxWristSpeed)
            {
                // Dropped unread: neither the glitch nor the jump back is a move.
                arms.Clear();
                tryOpen = false;
            }

            arms.Add(new ArmSample { time = time, wrist = wrist, length = length });
            lastShoulder = shoulder;
            lastOtherShoulder = otherShoulder;
            hasShoulders = true;
            TrimArms(time - Mathf.Max(settings.signalWindowSeconds, speedSpanSeconds * 2f));
            if (time < restUntil) return null;
            if (!tryOpen && !OpenOnFastWrist(time, wrist, settings)) return null;

            sideTravel = Mathf.Abs(wrist.x - tryStartWrist.x);
            if (sideTravel >= settings.slashMinTravel && Mathf.Abs(length - tryStartLength) <= settings.slashLengthRatio * sideTravel) slashSeen = true;
            // A sweep through the shoulder grows the arm fast on its far side: no signal until a long sideways move slows down.
            racingSideways = sideTravel >= settings.slashMinTravel && SidewaysSpeed(time, wrist) >= settings.trySpeed;
            if (!slashSeen && !racingSideways && IsExtension(time, length, wrist, shoulder, otherShoulder, settings, out var growth, out var speed))
            {
                return Close(PunchSignal.Extension, time, growth, speed, 0f, settings);
            }

            return Expire(time, settings);
        }

        /// <summary>
        /// One hand detector frame of this hand: its scale against the body's (bigger = closer to the lens than the body)
        /// and where the hand is, in body inches against the chest.
        /// </summary>
        /// <returns>The try this sample closed, if any.</returns>
        public Try? AddHandScale(float time, float rawScale, Vector2 hand, in Settings settings)
        {
            if (time - lastScaleTime > maxGapSeconds)
            {
                scales.Clear();
                recentScaleCount = 0;
            }

            lastScaleTime = time;
            recentScales[recentScaleCount % 3] = rawScale;
            recentScaleCount++;
            if (recentScaleCount < 3) return null;
            var scale = Median(recentScales[0], recentScales[1], recentScales[2]);
            var inFront = hasShoulders && InChestFront(hand, lastShoulder, lastOtherShoulder, settings);
            scales.Add(new ScaleSample { time = time, scale = scale, inFront = inFront });
            var cutoff = time - settings.signalWindowSeconds;
            var drop = 0;
            while (drop < scales.Count - 1 && scales[drop].time < cutoff) drop++;
            if (drop > 0) scales.RemoveRange(0, drop);
            if (!inFront || time < restUntil || (tryOpen && (slashSeen || racingSideways)) || !IsTowardCamera(time, scale, settings, out var growth)) return null;
            if (!tryOpen)
            {
                var last = arms.Count > 0 ? arms[^1] : default;
                Open(time, last.wrist, last.length);
            }

            return Close(PunchSignal.TowardCamera, time, 0f, 0f, growth, settings);
        }

        /// <summary>Closes a try whose window ran out while no samples arrived (tracking lost mid-move).</summary>
        public Try? Tick(float time, in Settings settings) => tryOpen ? Expire(time, settings) : null;

        public void Reset()
        {
            arms.Clear();
            scales.Clear();
            recentScaleCount = 0;
            lastScaleTime = float.NegativeInfinity;
            recentArmCount = 0;
            lastArmTime = float.NegativeInfinity;
            hasShoulders = false;
            tryOpen = false;
            slashSeen = false;
            racingSideways = false;
            restUntil = float.NegativeInfinity;
        }

        #endregion

        #region Signals

        // Hands hanging by the hips only jitter: a try needs the wrist up at chest height or above.
        bool OpenOnFastWrist(float time, Vector2 wrist, in Settings settings)
        {
            if (wrist.y < -settings.chestFrontWindow) return false;
            for (var i = arms.Count - 2; i >= 0; i--)
            {
                var reference = arms[i];
                var span = time - reference.time;
                if (span < speedSpanSeconds) continue;
                if ((wrist - reference.wrist).magnitude / span < settings.trySpeed) return false;
                Open(reference.time, reference.wrist, reference.length);
                return true;
            }

            return false;
        }

        // Any earlier frame in the signal window from which the arm grew far enough, fast enough; reports the largest growth.
        bool IsExtension(float time, float length, Vector2 wrist, Vector2 shoulder, Vector2 otherShoulder, in Settings settings, out float growth,
            out float speed)
        {
            growth = speed = 0f;
            if (!InChestFront(wrist, shoulder, otherShoulder, settings)) return false;
            var cutoff = time - settings.signalWindowSeconds;
            for (var i = arms.Count - 2; i >= 0 && arms[i].time >= cutoff; i--)
            {
                var span = time - arms[i].time;
                var grown = length - arms[i].length;
                if (span < speedSpanSeconds || grown < settings.extensionMinGrowth || grown / span < settings.extensionSpeed || grown <= growth) continue;
                var moved = wrist - arms[i].wrist;
                if (-moved.y > maxDropPerSideInch * Mathf.Abs(moved.x)) continue;
                growth = grown;
                speed = grown / span;
            }

            return growth > 0f;
        }

        static bool InChestFront(Vector2 point, Vector2 shoulder, Vector2 otherShoulder, in Settings settings)
        {
            var left = Mathf.Min(shoulder.x, otherShoulder.x) - settings.chestFrontMargin;
            var right = Mathf.Max(shoulder.x, otherShoulder.x) + settings.chestFrontMargin;
            return point.x >= left && point.x <= right && Mathf.Abs(point.y) <= settings.chestFrontWindow;
        }

        float SidewaysSpeed(float time, Vector2 wrist)
        {
            if (arms.Count < 2) return 0f;
            var previous = arms[^2];
            var span = time - previous.time;
            return span > 0f ? Mathf.Abs(wrist.x - previous.wrist.x) / span : 0f;
        }

        // Only from an earlier frame with the hand also in front: a hand raised into view from the hip is not coming closer.
        bool IsTowardCamera(float time, float scale, in Settings settings, out float growth)
        {
            growth = 0f;
            if (scale <= 0f) return false;
            for (var i = scales.Count - 2; i >= 0; i--)
            {
                var span = time - scales[i].time;
                if (span < speedSpanSeconds || !scales[i].inFront || scales[i].scale <= 0f) continue;
                var grown = scale / scales[i].scale - 1f;
                if (grown < settings.handScaleMinGrowth || grown / span < settings.handScaleSpeed || grown <= growth) continue;
                growth = grown;
            }

            return growth > 0f;
        }

        #endregion

        #region Try

        void Open(float time, Vector2 wrist, float length)
        {
            tryOpen = true;
            tryStart = time;
            tryStartWrist = wrist;
            tryStartLength = length;
            slashSeen = false;
            racingSideways = false;
            sideTravel = 0f;
        }

        Try? Expire(float time, in Settings settings)
        {
            if (time - tryStart < settings.tryWindowSeconds) return null;
            return Close(slashSeen ? PunchSignal.SideSlash : PunchSignal.None, time, 0f, 0f, 0f, settings);
        }

        Try Close(PunchSignal signal, float time, float growth, float speed, float scaleGrowth, in Settings settings)
        {
            tryOpen = false;
            slashSeen = false;
            racingSideways = false;
            restUntil = time + settings.cooldownSeconds;
            return new Try(signal, time, growth, speed, scaleGrowth, sideTravel);
        }

        static float Median(float a, float b, float c) => Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));

        static Vector2 Median(Vector2 a, Vector2 b, Vector2 c) => new(Median(a.x, b.x, c.x), Median(a.y, b.y, c.y));

        void TrimArms(float cutoff)
        {
            var drop = 0;
            while (drop < arms.Count - 1 && arms[drop].time < cutoff) drop++;
            if (drop > 0) arms.RemoveRange(0, drop);
        }

        #endregion
    }
}
