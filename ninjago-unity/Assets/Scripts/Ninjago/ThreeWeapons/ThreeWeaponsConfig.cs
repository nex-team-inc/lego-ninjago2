#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Three Weapons tuning: each weapon's hits to kill and words, the card hold, the three gesture windows, walking and
    /// pacing. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "ThreeWeaponsConfig", menuName = "Nex/Ninjago/Three Weapons Config")]
    public class ThreeWeaponsConfig : ScriptableObject
    {
        [Serializable]
        public class WeaponSettings
        {
            [Tooltip("Gestures that kill the monster: slashes, spin bursts (one per called area) or throws.")]
            public int hitsToKill = 6;
            [Tooltip("Card label: SWORD, NUNCHUCKS or SHURIKEN.")]
            public LocalizedString label = new();
            [Tooltip("The word over the ninja during the fight: SLASH, SPIN or THROW.")]
            public LocalizedString prompt = new();
            [Tooltip("Card and effect color.")]
            public Color color = Color.white;
            [Tooltip("Card icon.")]
            public Sprite? icon;
        }

        [Header("Weapons")]
        [SerializeField] EnumDictionary<WeaponType, WeaponSettings> weapons = new();

        [Header("Choice")]
        [Tooltip("Seconds a cursor must rest on a weapon card to lock it.")]
        [SerializeField, Range(0.1f, 3f)] float holdSeconds = 0.6f;

        [Header("Sword")]
        [Tooltip("Sideways hand speed (body inches per second) needed as the hand crosses its shoulder.")]
        [SerializeField, Range(5f, 200f)] float slashSpeed = 30f;
        [Tooltip("Seconds after a slash in which that hand's next crossing is ignored; only stops a double count.")]
        [SerializeField, Range(0f, 0.5f)] float slashCooldownSeconds = 0.08f;

        [Header("Nunchucks")]
        [Tooltip("Seconds of hand motion one burst looks back over.")]
        [SerializeField, Range(0.1f, 1.5f)] float spinWindowSeconds = 0.35f;
        [Tooltip("Body inches the hand must travel inside that window.")]
        [SerializeField, Range(4f, 80f)] float spinPathInches = 18f;
        [Tooltip("Body inches every point of that path must stay within, around the path's middle.")]
        [SerializeField, Range(1f, 20f)] float spinRadiusInches = 7f;
        [Tooltip("Body inches the path's middle may sit from the called spot (beside the head or the chest, at that shoulder's side).")]
        [SerializeField, Range(2f, 20f)] float spinAreaRadiusInches = 8f;
        [Tooltip("Seconds after a burst before the next one can count.")]
        [SerializeField, Range(0f, 2f)] float spinCooldownSeconds = 0.5f;

        [Header("Shuriken")]
        [Tooltip("Inward hand speed (body inches per second) needed as the hand crosses in front of its shoulder.")]
        [SerializeField, Range(5f, 200f)] float throwSpeed = 40f;
        [Tooltip("Body inches outside its shoulder the hand must be to cock the throw (wound back), so sawing does not throw.")]
        [SerializeField, Range(0f, 12f)] float cockOutsideInches = 5f;
        [Tooltip("Body inches below its shoulder the hand may be and still be cocked (wound back to the side, not hanging).")]
        [SerializeField, Range(-10f, 20f)] float cockBelowShoulderInches = 6f;
        [Tooltip("Seconds from last being cocked to crossing; a slower move is not a flick.")]
        [SerializeField, Range(0.1f, 2f)] float flickWindowSeconds = 0.6f;

        [Header("Walk")]
        [Tooltip("Meters per second the ninja walks to the next monster.")]
        [SerializeField, Range(0.5f, 10f)] float walkSpeed = 3f;
        [Tooltip("Meters in front of a monster where the ninja stops to fight.")]
        [SerializeField, Range(1f, 6f)] float stopDistance = 2.4f;

        [Header("Pacing")]
        [Tooltip("Seconds the monster takes to break after the killing hit, before the ninja walks on.")]
        [SerializeField, Range(0f, 4f)] float killPauseSeconds = 1.2f;
        [Tooltip("Seconds the cleared banner stays before the result.")]
        [SerializeField, Range(0f, 6f)] float endHoldSeconds = 2f;

        #region Public API

        public WeaponSettings GetWeapon(WeaponType weapon) => weapons[weapon];
        public float HoldSeconds { get => holdSeconds; set => holdSeconds = Mathf.Max(0.05f, value); }
        public float SlashSpeed { get => slashSpeed; set => slashSpeed = Mathf.Max(1f, value); }
        public float SlashCooldownSeconds { get => slashCooldownSeconds; set => slashCooldownSeconds = Mathf.Max(0f, value); }
        public float SpinWindowSeconds { get => spinWindowSeconds; set => spinWindowSeconds = Mathf.Max(0.05f, value); }
        public float SpinPathInches { get => spinPathInches; set => spinPathInches = Mathf.Max(1f, value); }
        public float SpinRadiusInches { get => spinRadiusInches; set => spinRadiusInches = Mathf.Max(0.5f, value); }
        public float SpinAreaRadiusInches => spinAreaRadiusInches;
        public float SpinCooldownSeconds => spinCooldownSeconds;
        public float ThrowSpeed => throwSpeed;
        public float CockOutsideInches => cockOutsideInches;
        public float CockBelowShoulderInches => cockBelowShoulderInches;
        public float FlickWindowSeconds => flickWindowSeconds;
        public float WalkSpeed => walkSpeed;
        public float StopDistance => stopDistance;
        public float KillPauseSeconds => killPauseSeconds;
        public float EndHoldSeconds => endHoldSeconds;

        public int SlashesToKill { get => weapons[WeaponType.Sword].hitsToKill; set => weapons[WeaponType.Sword].hitsToKill = Mathf.Max(1, value); }
        public int ThrowsToKill { get => weapons[WeaponType.Shuriken].hitsToKill; set => weapons[WeaponType.Shuriken].hitsToKill = Mathf.Max(1, value); }

        #endregion
    }
}
