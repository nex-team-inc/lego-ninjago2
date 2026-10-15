#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Reads one player's weapon gesture each frame. Hands are that player's team.nex.mdk.hand cursors (body inches from
    /// the chest); shoulders and head come from that player's body nodes, or from the setup snapshot while hidden (a
    /// whirling hand often covers the face). Sword and shuriken use each shoulder's x against that hand; the nunchuck
    /// areas use the head, the chest and the hands.
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

        /// <summary>The called nunchuck spot in body inches from the chest: that side's shoulder x, at head or chest height.</summary>
        public Vector2 AreaCenter(SpinArea area)
        {
            var left = area is SpinArea.LeftHead or SpinArea.LeftBody;
            var head = area is SpinArea.LeftHead or SpinArea.RightHead;
            return new Vector2(Shoulder(left ? 0 : 1).x, head ? Head().y : 0f);
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
                    if (time < spinReadyTime || !spins[hand].TryBurst(AreaCenter(area), config.SpinAreaRadiusInches, config.SpinPathInches, config.SpinRadiusInches))
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
