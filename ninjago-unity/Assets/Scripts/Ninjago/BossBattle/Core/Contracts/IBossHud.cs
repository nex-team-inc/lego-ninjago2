#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The battle HUD (TDD §3.4, implemented by BossHud): bars, hearts, prompts, banners, world labels and the PiP.</summary>
    public interface IBossHud
    {
        void SetVisible(bool visible);
        /// <summary>Letterbox on; bars, hearts and prompts hidden; the PiP kept.</summary>
        void SetCutsceneMode(bool cutscene, float seconds);
        void SetBossBar(BossPhase phase, float fraction, bool animateRefill);
        void ShowDamagePopup(Vector3 worldPoint, int multiplier);
        void SetHearts(int hearts, int maxHearts);
        void SetPickups(int collected, int total);
        /// <summary>seconds -1 = the config default (BossBattleConfig.Prompt).</summary>
        void ShowPrompt(BossPromptId id, float seconds = -1f);
        void HidePrompt();
        void ShowBanner(BossBannerId id, float seconds);
        /// <summary>3, 2, 1; 0 hides.</summary>
        void SetCountdown(int value);
        /// <summary>The P1/P2 tags follow it.</summary>
        void SetVehicleAnchor(Transform? vehicle);
        /// <summary>Filtered controller (-1 neutral); updates the PiP and the tags.</summary>
        void SetController(int playerIndex);
        /// <summary>The "!" over the vehicle.</summary>
        void SetRockWarning(int slot, bool visible, bool locked);
        /// <summary>Over the boss head.</summary>
        void SetAttackIcon(BossAttackIcon icon);
        void SetTargetRing(bool visible, Vector3 worldPoint, float worldRadius, bool onTarget);
        void SetCellFrames(bool visible, int blockedMask, float localZ);
        void SetPipDimmed(bool dimmed);
    }
}
