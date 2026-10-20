#nullable enable

using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// What a fight shouts at each player, next to the threat in the world: INCOMING! as a rock or monster flies in,
    /// then the move to make (SLASH IT!, KICK!, LEAN LEFT!, SPIN!, SHAKE IT OFF!) with a red edge glow on danger. While
    /// the entry's hint policy still teaches a gesture, a second line says how to do it with the body.
    /// </summary>
    public class FightCalls : MonoBehaviour
    {
        [Header("Incoming Text")]
        [SerializeField] LocalizedString incomingText = new();
        [Header("Monster Text")]
        [SerializeField] LocalizedString monsterText = new();
        [Header("Slash Text")]
        [SerializeField] LocalizedString slashText = new();
        [Header("Slash Count Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString slashCountText = new();
        [Header("Slash How")]
        [SerializeField] LocalizedString slashHow = new();
        [Header("Frenzy How")]
        [SerializeField] LocalizedString frenzyHow = new();
        [Header("Kick Text")]
        [SerializeField] LocalizedString kickText = new();
        [Header("Kick How")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString kickHow = new();
        [Header("Smash Text")]
        [SerializeField] LocalizedString smashText = new();
        [Header("Lean Left Text")]
        [SerializeField] LocalizedString leanLeftText = new();
        [Header("Lean Right Text")]
        [SerializeField] LocalizedString leanRightText = new();
        [Header("Lean How")]
        [SerializeField] LocalizedString leanHow = new();
        [Header("Spin Text")]
        [SerializeField] LocalizedString spinText = new();
        [Header("Shake How")]
        [SerializeField] LocalizedString shakeHow = new();
        [Header("Pinned Text")]
        [SerializeField] LocalizedString pinnedText = new();
        [Header("Danger Color")]
        [SerializeField] Color dangerColor = new(0.95f, 0.2f, 0.15f);
        [Header("Warning Glow")]
        [Tooltip("HUD danger glow for a warning; a lost heart flashes at full strength.")]
        [SerializeField, Range(0f, 1f)] float warningGlow = 0.45f;
        [Header("Smash Seconds")]
        [SerializeField] float smashSeconds = 0.9f;

        ChapterHud hud = null!;
        ChapterHints hints = null!;
        HintPolicy policy;

        #region Initialization

        public void Initialize(ChapterHud aHud, ChapterHints aHints)
        {
            hud = aHud;
            hints = aHints;
        }

        #endregion

        #region Public API

        /// <summary>Called when a fight entry is staged, with that entry's hint policy.</summary>
        public void Begin(HintPolicy hintPolicy)
        {
            policy = hintPolicy;
        }

        public void Incoming(ChapterFightLane lane, bool monster, Vector3 at)
        {
            Warn(lane, monster ? monsterText : incomingText, null, at);
        }

        /// <param name="slashes">Slashes so far; 0 asks for the first one.</param>
        public void Slash(ChapterFightLane lane, int slashes, Vector3 at)
        {
            var line = slashes == 0 ? slashText : LocalizedStrings.WithArguments(slashCountText, ("count", slashes));
            Call(lane, line, Teach(Gesture.Slash, slashes == 0 ? slashHow : frenzyHow), lane.Color, at);
        }

        public void Kick(ChapterFightLane lane, int stones, Vector3 at)
        {
            Call(lane, kickText, Teach(Gesture.Kick, LocalizedStrings.WithArguments(kickHow, ("count", stones))), lane.Color, at);
        }

        public void Smash(ChapterFightLane lane, Vector3 at)
        {
            Call(lane, smashText, null, lane.Color, at, smashSeconds);
        }

        public void Lean(ChapterFightLane lane, SweepSide safeSide, Vector3 at)
        {
            Warn(lane, safeSide.Sign() < 0 ? leanLeftText : leanRightText, Teach(Gesture.Slip, leanHow), at);
        }

        public void Spin(ChapterFightLane lane, Vector3 at)
        {
            Call(lane, spinText, Teach(Gesture.Spin, shakeHow), lane.Color, at);
        }

        /// <summary>Always says how: a pin is rare and the player is losing hearts while reading it.</summary>
        public void Pinned(ChapterFightLane lane, Vector3 at)
        {
            Warn(lane, pinnedText, shakeHow, at);
            hud.FlashDanger(1f);
        }

        public void Hit()
        {
            hud.FlashDanger(1f);
        }

        public static void Clear(ChapterFightLane lane)
        {
            lane.Prompt.Hide();
        }

        #endregion

        #region Helpers

        LocalizedString? Teach(Gesture gesture, LocalizedString how) => hints.Teaches(policy, gesture) ? how : null;

        void Warn(ChapterFightLane lane, LocalizedString line, LocalizedString? how, Vector3 at)
        {
            Call(lane, line, how, dangerColor, at);
            hud.FlashDanger(warningGlow);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Spotted);
        }

        static void Call(ChapterFightLane lane, LocalizedString line, LocalizedString? how, Color color, Vector3 at, float seconds = 0f)
        {
            lane.Prompt.Show(line, color, seconds, how, at);
            lane.Prompt.Pop();
        }

        #endregion
    }
}
