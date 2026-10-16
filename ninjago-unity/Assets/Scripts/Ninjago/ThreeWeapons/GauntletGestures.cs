#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Reads one player's weapon gesture each frame. Hands are that player's team.nex.mdk.hand cursors (body inches from
    /// the chest); shoulders and head come from that player's body nodes, or from the setup snapshot while hidden (a
    /// whirling hand often covers the face). Sword and shuriken use each shoulder's x against that hand; the nunchuck
    /// areas use the shoulders' side and the head's height.
    /// </summary>
    public sealed class GauntletGestures
    {
        readonly SawDetector[] saws = { new(), new() };
        readonly SpinDetector[] spins = { new(), new() };
        readonly FlickDetector[] flicks = { new(), new() };
        readonly PlayerBody body;
        readonly HandCursorTracker cursors;
        readonly ThreeWeaponsConfig config;
        int simulatedPulses;
        float spinReadyTime;

        public GauntletGestures(PlayerBody body, HandCursorTracker cursors, ThreeWeaponsConfig config)
        {
            this.body = body;
            this.cursors = cursors;
            this.config = config;
        }

        #region Public API

        public void Reset()
        {
            for (var hand = 0; hand < 2; hand++) ResetHand(hand);
            simulatedPulses = SimulatedBody.GetKneePulses(body.PlayerIndex);
        }

        /// <summary>True on the frame this player lands the weapon's gesture; hand is 0 (left) or 1 (right).</summary>
        public bool TryDetect(WeaponType weapon, SpinArea area, float time, out int hand)
        {
            hand = 1;
            // Debug stand-in: each storm key press is one gesture.
            if (SimulatedBody.IsEnabled)
            {
                if (SimulatedBody.GetKneePulses(body.PlayerIndex) == simulatedPulses) return false;
                simulatedPulses++;
                return true;
            }

            for (hand = 0; hand < 2; hand++)
            {
                var cursor = cursors.GetCursor(body.PlayerIndex, hand);
                if (!cursor.IsVisible)
                {
                    ResetHand(hand);
                    continue;
                }

                if (Detect(weapon, area, hand, cursor.Inches, time)) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a burst's middle (body inches from the chest) is in the called nunchuck area: out from the body's middle
        /// toward that side's shoulder, and above (head) or below (body) a line just under the head. Only the side and
        /// the height band matter, so a hand spinning close in and one on an outstretched arm, as the ninja shows it,
        /// both count.
        /// </summary>
        public static bool IsInArea(Vector2 point, SpinArea area, Vector2 shoulder, Vector2 head, float sideInches, float headLineDropInches)
        {
            var outward = shoulder.x >= 0f ? point.x : -point.x;
            if (outward < sideInches) return false;
            var aboveLine = point.y >= head.y - headLineDropInches;
            return area is SpinArea.LeftHead or SpinArea.RightHead ? aboveLine : !aboveLine;
        }

        #endregion

        #region Helpers

        bool Detect(WeaponType weapon, SpinArea area, int hand, Vector2 inches, float time)
        {
            switch (weapon)
            {
                case WeaponType.Sword:
                    return saws[hand].Update(inches.x, Shoulder(hand).x, time, config.SlashSpeed, config.SlashCooldownSeconds);
                case WeaponType.Nunchucks:
                    spins[hand].Add(inches, time, config.SpinWindowSeconds);
                    if (time < spinReadyTime || !spins[hand].IsBurst(config.SpinPathInches, config.SpinRadiusInches, out var middle) ||
                        !IsInArea(middle, area, Shoulder(area is SpinArea.LeftHead or SpinArea.LeftBody ? 0 : 1), Head(), config.SpinSideInches,
                            config.SpinHeadLineDropInches))
                    {
                        return false;
                    }

                    spinReadyTime = time + config.SpinCooldownSeconds;
                    spins[0].Clear();
                    spins[1].Clear();
                    return true;
                default:
                    var settings = new FlickDetector.Settings(config.CockOutsideInches, config.CockBelowShoulderInches, config.ThrowSpeed, config.FlickWindowSeconds);
                    return flicks[hand].Update(inches, Shoulder(hand), hand == 0 ? -1f : 1f, time, settings);
            }
        }

        Vector2 Shoulder(int hand)
        {
            if (body.TryGetShoulderInches(hand == 0, out var shoulder)) return shoulder;
            return hand == 0 ? body.Shape.leftShoulder : body.Shape.rightShoulder;
        }

        Vector2 Head() => body.TryGetHeadInches(out var head) ? head : body.Shape.head;

        void ResetHand(int hand)
        {
            saws[hand].Reset();
            spins[hand].Clear();
            flicks[hand].Reset();
        }

        #endregion
    }
}
