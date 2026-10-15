#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's gauntlet: three monsters in a line that never move, a ninja that walks up to each in turn, and a
    /// camera behind the ninja rendering into that player's screen region. Before each fight that player's cursors
    /// pick one of the weapons left; the weapon's gesture is the whole fight, from that player's hands only.
    /// </summary>
    public class GauntletLane : MonoBehaviour
    {
        [Header("Ninja")]
        [SerializeField] WeaponNinjaRig ninja = null!;
        [Header("Monsters")]
        [Tooltip("The three fights in order, standing in a line ahead along +Z.")]
        [SerializeField] GauntletMonster[] monsters = null!;
        [Header("Crowd")]
        [Tooltip("Monsters watching from the sides; always dim.")]
        [SerializeField] GauntletMonster[] crowd = null!;
        [Header("Shuriken Projectiles")]
        [Tooltip("Reused in turn, so quick flicks can have several in the air.")]
        [SerializeField] Transform[] projectiles = null!;
        [Header("Lane Camera")]
        [SerializeField] Camera laneCamera = null!;
        [Header("Camera Offset")]
        [Tooltip("Behind and above the ninja.")]
        [SerializeField] Vector3 cameraOffset = new(0f, 2.6f, -4.4f);
        [Header("Solo Field Of View")]
        [SerializeField] float soloFieldOfView = 52f;
        [Header("Split Field Of View")]
        [SerializeField] float splitFieldOfView = 66f;
        [Header("Card Height")]
        [Tooltip("Screen height (0 bottom, 1 top) of the weapon cards: between the ninja and the monster.")]
        [SerializeField, Range(0f, 1f)] float cardHeight = 0.42f;
        [Header("Projectile Seconds")]
        [SerializeField] float projectileSeconds = 0.22f;

        readonly List<WeaponType> weapons = new();
        readonly List<WeaponCard> cards = new();
        PlayerBody body = null!;
        ThreeWeaponsConfig config = null!;
        HandCursorTracker cursors = null!;
        HandCursorLayer cursorLayer = null!;
        RectTransform cardsRoot = null!;
        WeaponCard cardPrefab = null!;
        GauntletHud hud = null!;
        GauntletGestures gestures = null!;
        SpinAreaCaller areaCaller = null!;
        Rect region;
        int landed;
        int nextProjectile;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        /// <summary>The weapon used in each fight, in order.</summary>
        public IReadOnlyList<WeaponType> Weapons => weapons;
        public int Slashes { get; private set; }
        public int SpinBursts { get; private set; }
        public int Throws { get; private set; }
        public float ClearSeconds { get; private set; }

        #region Initialization

        public void Initialize(PlayerBody aBody, ThreeWeaponsConfig aConfig, HandCursorTracker aCursors, HandCursorLayer aCursorLayer, RectTransform aCardsRoot,
            WeaponCard aCardPrefab, GauntletHud aHud, RenderTexture target, Rect screenRegion)
        {
            body = aBody;
            config = aConfig;
            cursors = aCursors;
            cursorLayer = aCursorLayer;
            cardsRoot = aCardsRoot;
            cardPrefab = aCardPrefab;
            hud = aHud;
            region = screenRegion;
            gestures = new GauntletGestures(body, cursors, config);
            areaCaller = new SpinAreaCaller(body.PlayerIndex * 31 + Environment.TickCount);
            laneCamera.targetTexture = target;
            laneCamera.fieldOfView = region.width < 1f ? splitFieldOfView : soloFieldOfView;
            ninja.Initialize(body.PlayerIndex, body.Color);
            hud.Initialize(body.PlayerIndex, body.Color);
            hud.SetFight(1, monsters.Length);
            foreach (var monster in monsters) monster.SetActive(false);
            foreach (var watcher in crowd) watcher.SetActive(false);
            foreach (var projectile in projectiles) projectile.gameObject.SetActive(false);
            FollowWith(monsters[0]);
        }

        #endregion

        #region Gauntlet

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var start = Time.time;
            var remaining = new List<WeaponType> { WeaponType.Sword, WeaponType.Nunchucks, WeaponType.Shuriken };
            for (var fight = 0; fight < monsters.Length; fight++)
            {
                var monster = monsters[fight];
                hud.SetFight(fight + 1, monsters.Length);
                monster.SetActive(true);
                await WalkToAsync(monster, cancellationToken);
                var weapon = await ChooseAsync(remaining, fight, monster, cancellationToken);
                remaining.Remove(weapon);
                weapons.Add(weapon);
                await FightAsync(weapon, fight, monster, cancellationToken);
            }

            ClearSeconds = Time.time - start;
            hud.ShowCleared();
        }

        async UniTask WalkToAsync(GauntletMonster monster, CancellationToken cancellationToken)
        {
            var stopZ = monster.transform.localPosition.z - config.StopDistance;
            ninja.SetWalking(true);
            while (ninja.transform.localPosition.z < stopZ)
            {
                var step = Mathf.Min(config.WalkSpeed * Time.deltaTime, stopZ - ninja.transform.localPosition.z);
                ninja.transform.localPosition += new Vector3(0f, 0f, step);
                ninja.Tick(Time.deltaTime, step);
                FollowWith(monster);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            ninja.SetWalking(false);
        }

        // The cards left sit between the ninja and the monster; resting one of this player's cursors on a card locks it.
        async UniTask<WeaponType> ChooseAsync(IReadOnlyList<WeaponType> remaining, int fight, GauntletMonster monster, CancellationToken cancellationToken)
        {
            var start = Time.time;
            for (var i = 0; i < remaining.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardsRoot);
                var settings = config.GetWeapon(remaining[i]);
                var x = region.xMin + region.width * (i + 1f) / (remaining.Count + 1f);
                card.Initialize(remaining[i], settings, settings.icon!, body.Color, cursorLayer.ToLocal(new Vector2(x, cardHeight)));
                cards.Add(card);
            }

            hud.ShowPick(true);
            cursorLayer.SetPlayerVisible(PlayerIndex, true);
            WeaponType? chosen = null;
            while (chosen == null)
            {
                foreach (var card in cards)
                {
                    if (!card.Tick(Hovered(card), Time.deltaTime, config.HoldSeconds)) continue;
                    chosen = card.Weapon;
                    break;
                }

                ninja.Tick(Time.deltaTime, 0f);
                FollowWith(monster);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            foreach (var card in cards) Destroy(card.gameObject);
            cards.Clear();
            hud.ShowPick(false);
            cursorLayer.SetPlayerVisible(PlayerIndex, false);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            ThreeWeaponsAnalytics.WeaponChosen(PlayerIndex, fight + 1, chosen.Value, Time.time - start);
            return chosen.Value;
        }

        async UniTask FightAsync(WeaponType weapon, int fight, GauntletMonster monster, CancellationToken cancellationToken)
        {
            var settings = config.GetWeapon(weapon);
            var needed = Mathf.Max(1, settings.hitsToKill);
            var released = 0;
            landed = 0;
            var start = Time.time;
            ninja.ShowWeapon(weapon, settings.prompt);
            if (weapon == WeaponType.Nunchucks) ninja.SetSpinArea(areaCaller.Current);
            gestures.Reset();
            while (landed < needed)
            {
                if (released < needed && gestures.TryDetect(weapon, areaCaller.Current, Time.time, out var hand))
                {
                    released++;
                    Strike(weapon, hand, fight, monster, needed);
                }

                ninja.Tick(Time.deltaTime, 0f);
                FollowWith(monster);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            ninja.ShowIdle();
            var system = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.MonsterBreak, monster.ChestPosition, Quaternion.identity);
            if (system != null)
            {
                var main = system.main;
                main.startColor = settings.color;
            }

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.EnemyExplosion);
            monster.Break();
            ThreeWeaponsAnalytics.MonsterKilled(PlayerIndex, fight + 1, weapon, Time.time - start, released);
            await UniTask.Delay(TimeSpan.FromSeconds(config.KillPauseSeconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        void Strike(WeaponType weapon, int hand, int fight, GauntletMonster monster, int needed)
        {
            switch (weapon)
            {
                case WeaponType.Sword:
                    Slashes++;
                    landed++;
                    ninja.PlaySlash(hand);
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.WeaponSlash, monster.ChestPosition,
                        laneCamera.transform.rotation * Quaternion.Euler(0f, 0f, hand == 0 ? 20f : -20f));
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.WeaponSlash);
                    monster.Hit(1f - (float)landed / needed);
                    ThreeWeaponsAnalytics.Slash(PlayerIndex, fight + 1, hand, Slashes);
                    break;
                case WeaponType.Nunchucks:
                    SpinBursts++;
                    landed++;
                    var area = areaCaller.Current;
                    ninja.PlaySpin();
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NunchuckSpin, ninja.SpinSpot(area));
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.WeaponSpin);
                    monster.Hit(1f - (float)landed / needed);
                    ThreeWeaponsAnalytics.SpinBurst(PlayerIndex, fight + 1, hand, area, SpinBursts);
                    ninja.SetSpinArea(areaCaller.Advance());
                    break;
                default:
                    Throws++;
                    ninja.PlayThrow(hand);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.WeaponThrow);
                    ThreeWeaponsAnalytics.Throw(PlayerIndex, fight + 1, hand, Throws);
                    FlyAsync(projectiles[nextProjectile++ % projectiles.Length], ninja.HandPosition(hand), monster, needed).Forget();
                    break;
            }
        }

        // The shuriken spins from the hand into the monster in front of the ninja; no aiming.
        async UniTaskVoid FlyAsync(Transform projectile, Vector3 from, GauntletMonster monster, int needed)
        {
            projectile.gameObject.SetActive(true);
            var elapsed = 0f;
            while (elapsed < projectileSeconds)
            {
                elapsed += Time.deltaTime;
                projectile.position = Vector3.Lerp(from, monster.ChestPosition, Mathf.Clamp01(elapsed / projectileSeconds));
                projectile.Rotate(0f, 1440f * Time.deltaTime, 0f, Space.Self);
                await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
            }

            projectile.gameObject.SetActive(false);
            landed++;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.ShurikenHit, monster.ChestPosition);
            monster.Hit(1f - (float)landed / needed);
        }

        #endregion

        #region Helpers

        bool Hovered(WeaponCard card)
        {
            for (var hand = 0; hand < 2; hand++)
            {
                var cursor = cursors.GetCursor(PlayerIndex, hand);
                if (cursor.IsVisible && card.Contains(cursorLayer.ToLocal(cursor.ScreenPosition))) return true;
            }

            return false;
        }

        // Behind the ninja, facing the monster being fought.
        void FollowWith(GauntletMonster monster)
        {
            var cameraTransform = laneCamera.transform;
            cameraTransform.position = ninja.transform.position + cameraOffset;
            cameraTransform.LookAt(monster.ChestPosition);
        }

        #endregion
    }
}
