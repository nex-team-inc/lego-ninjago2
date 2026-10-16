#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The One Punch HUD over the street: a panel per player, the wave and monsters left, the WAVE / CLEARED / DOWN
    /// banner with the PUNCH hint on the first wave, and while someone is pinned the WIGGLE prompt and meter.
    /// </summary>
    public class OnePunchHud : MonoBehaviour
    {
        [Header("Player Panels")]
        [Tooltip("Left (slot 0) then right (slot 1).")]
        [SerializeField] OnePunchPlayerPanel[] panels = null!;
        [Header("Wave Label")]
        [SerializeField] NexLocalizedString waveLabel = null!;
        [Header("Wave Text")]
        [Tooltip("Smart string with {current} {total} {left}.")]
        [SerializeField] LocalizedString waveString = new();
        [Header("Center Banner")]
        [SerializeField] NexLocalizedString centerBanner = null!;
        [Header("Wave Banner Text")]
        [Tooltip("Smart string with {wave}.")]
        [SerializeField] LocalizedString waveBannerString = new();
        [Header("Wave Cleared Text")]
        [SerializeField] LocalizedString waveClearedString = new();
        [Header("Street Cleared Text")]
        [SerializeField] LocalizedString streetClearedString = new();
        [Header("Down Text")]
        [SerializeField] LocalizedString downString = new();
        [Header("Punch Hint")]
        [SerializeField] GameObject punchHint = null!;
        [Header("Pin Prompt")]
        [SerializeField] GameObject pinPrompt = null!;
        [Header("Wiggle Meter")]
        [SerializeField] GameObject wiggleMeter = null!;
        [Header("Wiggle Fill")]
        [SerializeField] Image wiggleFill = null!;
        [Header("Wiggle Idle Color")]
        [Tooltip("Fill color while the hips are not wiggling hard enough.")]
        [SerializeField] Color wiggleIdleColor = new(0.6f, 0.6f, 0.6f);

        OnePunchConfig config = null!;
        Color wiggleColor;
        float bannerHideAt = float.PositiveInfinity;
        float hintHideAt = float.PositiveInfinity;

        #region Initialization

        public void Initialize(OnePunchConfig aConfig, IReadOnlyList<StreetPlayer> players)
        {
            config = aConfig;
            for (var slot = 0; slot < panels.Length; slot++)
            {
                var used = slot < players.Count;
                panels[slot].gameObject.SetActive(used);
                if (used) panels[slot].Initialize(players[slot], config.SignalFlashSeconds);
            }

            centerBanner.gameObject.SetActive(false);
            punchHint.SetActive(false);
            HidePin();
        }

        #endregion

        #region Public API

        public void SetWave(int current, int total, int left)
        {
            waveLabel.StringReference = LocalizedStrings.WithArguments(waveString, ("current", current), ("total", total), ("left", left));
        }

        public void ShowWaveBanner(int wave, bool withHint)
        {
            ShowCenter(LocalizedStrings.WithArguments(waveBannerString, ("wave", wave)), config.WaveBannerSeconds);
            if (!withHint) return;
            punchHint.SetActive(true);
            hintHideAt = Time.time + config.WaveBannerSeconds * 2f;
        }

        public void ShowWaveCleared() => ShowCenter(waveClearedString, config.SecondsBetweenWaves);

        public void ShowStreetCleared() => ShowCenter(streetClearedString, float.PositiveInfinity);

        public void ShowDown() => ShowCenter(downString, float.PositiveInfinity);

        public void ShowTry(StreetPlayer player, PunchSignal signal)
        {
            var panel = panels[player.Slot];
            panel.SetCounts(player);
            panel.FlashSignal(config.GetLook(signal));
        }

        public void SetHealth(StreetPlayer player)
        {
            panels[player.Slot].SetHealth(player.Health / player.MaxHealth);
        }

        public void ShowPin(Color playerColor)
        {
            wiggleColor = playerColor;
            pinPrompt.SetActive(true);
            wiggleMeter.SetActive(true);
            SetWiggle(0f, false);
        }

        public void SetWiggle(float fill, bool wiggling)
        {
            wiggleFill.fillAmount = fill;
            wiggleFill.color = wiggling ? wiggleColor : wiggleIdleColor;
        }

        public void HidePin()
        {
            pinPrompt.SetActive(false);
            wiggleMeter.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (Time.time >= bannerHideAt)
            {
                centerBanner.gameObject.SetActive(false);
                bannerHideAt = float.PositiveInfinity;
            }

            if (Time.time < hintHideAt) return;
            punchHint.SetActive(false);
            hintHideAt = float.PositiveInfinity;
        }

        #endregion

        #region Helpers

        void ShowCenter(LocalizedString text, float seconds)
        {
            centerBanner.StringReference = text;
            centerBanner.gameObject.SetActive(true);
            bannerHideAt = Time.time + seconds;
        }

        #endregion
    }
}
