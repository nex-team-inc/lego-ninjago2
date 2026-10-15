#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Three Weapons: a gauntlet of three monsters fought one at a time, each with a different weapon the player picks
    /// in turn: a sawing sword slash, a wild nunchuck spin in the called spot, a shuriken flick.
    /// </summary>
    public class ThreeWeaponsGame : HandCursorGame
    {
        [Header("Gauntlet View")]
        [SerializeField] ThreeWeaponsView viewPrefab = null!;
        [Header("Three Weapons Config")]
        [SerializeField] ThreeWeaponsConfig config = null!;
        [Header("Result: Title")]
        [SerializeField] LocalizedString title = new();
        [Header("Result: Weapons Line")]
        [Tooltip("Smart string with {player} {first} {second} {third} (weapon names) {time}.")]
        [SerializeField] LocalizedString weaponsLine = new();
        [Header("Result: Counts Line")]
        [Tooltip("Smart string with {player} {slashes} {spins} {throws}.")]
        [SerializeField] LocalizedString countsLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {time}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.ThreeWeapons;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Cursors, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.ThreeWeapons, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Lanes);
        }

        NinjagoOutcome BuildOutcome(IReadOnlyList<GauntletLane> lanes)
        {
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var best = float.MaxValue;
            foreach (var lane in lanes)
            {
                var player = lane.PlayerIndex + 1;
                best = Mathf.Min(best, lane.ClearSeconds);
                lines.Add(new ResultLine.Data(weaponsLine, lane.PlayerColor, ("player", player), ("first", WeaponName(lane, 0)), ("second", WeaponName(lane, 1)),
                    ("third", WeaponName(lane, 2)), ("time", lane.ClearSeconds)));
                lines.Add(new ResultLine.Data(countsLine, lane.PlayerColor, ("player", player), ("slashes", lane.Slashes), ("spins", lane.SpinBursts),
                    ("throws", lane.Throws)));
                var prefix = $"p{player}_";
                for (var fight = 0; fight < lane.Weapons.Count; fight++) details[$"{prefix}fight{fight + 1}"] = lane.Weapons[fight].ToString();
                details[prefix + "slashes"] = lane.Slashes;
                details[prefix + "spin_bursts"] = lane.SpinBursts;
                details[prefix + "throws"] = lane.Throws;
                details[prefix + "clear_s"] = Mathf.Round(lane.ClearSeconds * 10f) / 10f;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var improved = progress.threeWeaponsBestSeconds < 0f || best < progress.threeWeaponsBestSeconds;
            var isNewBest = improved && progress.threeWeaponsRuns > 0;
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.threeWeaponsRuns++;
                if (improved) saved.threeWeaponsBestSeconds = best;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("time", progress.threeWeaponsBestSeconds)));
            return new NinjagoOutcome(title, lines, "win", details);
        }

        // The localized card label of the weapon used in that fight.
        string WeaponName(GauntletLane lane, int fight) => config.GetWeapon(lane.Weapons[fight]).label.GetLocalizedString();

        #endregion
    }
}
