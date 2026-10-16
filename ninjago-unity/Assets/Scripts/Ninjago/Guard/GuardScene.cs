#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Guard's courtyard: a still camera behind the ninjas (one per active player, P1 on the left) and the little friend
    /// they protect, goblins on the wall lobbing big bricks. A fresh shield guard at the hit bursts the brick and the
    /// friend cheers; otherwise the brick lands on the ninja's head as a silly hat. Giant bricks come at everyone: with
    /// two players both must guard.
    /// </summary>
    public class GuardScene : MonoBehaviour
    {
        struct Pending
        {
            public GuardPlayer player;
            public float impact;
            public bool held;
            public int throwNumber;
            public bool big;
        }

        [Header("Scene Camera")]
        [SerializeField] Camera sceneCamera = null!;
        [Header("Ninjas")]
        [Tooltip("Slot 0 then slot 1; the second stays hidden for one player.")]
        [SerializeField] KidNinja[] ninjas = null!;
        [Header("Shields")]
        [Tooltip("One per ninja, in front of its face; grows while that player's guard is fresh.")]
        [SerializeField] Transform[] shields = null!;
        [Header("Duo Ninja Offset")]
        [SerializeField] float duoOffset = 1.6f;
        [Header("Friend")]
        [SerializeField] GuardFriend friend = null!;
        [Header("Solo Friend Position")]
        [Tooltip("Beside and behind the only ninja, so the camera sees both.")]
        [SerializeField] Vector3 soloFriendPosition = new(-1.3f, 0f, -0.9f);
        [Header("Duo Friend Position")]
        [SerializeField] Vector3 duoFriendPosition = new(0f, 0f, -0.9f);
        [Header("Throwers")]
        [Tooltip("Left then right goblin on the wall.")]
        [SerializeField] GuardThrower[] throwers = null!;
        [Header("Big Thrower")]
        [SerializeField] GuardThrower bigThrower = null!;
        [Header("Big Target")]
        [Tooltip("Where giant bricks aim; placed between the ninjas' faces.")]
        [SerializeField] Transform bigTarget = null!;
        [Header("Brick Pool")]
        [SerializeField] GuardBrickPool brickPool = null!;
        [Header("Brick Prefab")]
        [SerializeField] GuardBrick brickPrefab = null!;
        [Header("Brick Colors")]
        [SerializeField] Color[] brickColors = null!;
        [Header("Big Brick Scale")]
        [SerializeField] float bigBrickScale = 2.2f;
        [Header("Shield Pop Rate")]
        [SerializeField] float shieldRate = 14f;
        [Header("Status Text")]
        [Tooltip("Smart string with {current} {total}.")]
        [SerializeField] LocalizedString statusText = new();
        [Header("Intro Banner")]
        [SerializeField] LocalizedString introBanner = new();
        [Header("Hint")]
        [SerializeField] LocalizedString hintText = new();
        [Header("Block Prompt")]
        [SerializeField] LocalizedString blockPrompt = new();
        [Header("Team Block Prompt")]
        [SerializeField] LocalizedString teamPrompt = new();
        [Header("Team Block Banner")]
        [SerializeField] LocalizedString teamBanner = new();
        [Header("End Banner")]
        [SerializeField] LocalizedString endBanner = new();
        [Header("Panel Count Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString countText = new();

        readonly List<GuardPlayer> players = new();
        readonly List<Pending> pending = new();
        readonly bool[] live = new bool[2];
        GuardConfig config = null!;
        KidsHud hud = null!;
        float liveUntil = float.NegativeInfinity;
        int throwNumber;

        public IReadOnlyList<GuardPlayer> Players => players;
        public int ThrowCount { get; private set; }
        public int BigThrows { get; private set; }
        public int BigSaves { get; private set; }

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers, GuardConfig aConfig, KidsHud aHud, RenderTexture target)
        {
            config = aConfig;
            hud = aHud;
            sceneCamera.targetTexture = target;
            var duo = activePlayers.Count > 1;
            for (var slot = 0; slot < ninjas.Length; slot++)
            {
                var used = slot < activePlayers.Count;
                ninjas[slot].gameObject.SetActive(used);
                if (!used) continue;
                var body = activePlayers[slot];
                ninjas[slot].transform.localPosition = new Vector3(duo ? (slot == 0 ? -duoOffset : duoOffset) : 0f, 0f, 0f);
                ninjas[slot].Initialize(body.PlayerIndex, body.Color);
                shields[slot].localScale = Vector3.zero;
                players.Add(new GuardPlayer(body, ninjas[slot], shields[slot], ninjas[slot].FrontPoint, slot));
            }

            friend.transform.localPosition = duo ? duoFriendPosition : soloFriendPosition;
            var faces = Vector3.zero;
            foreach (var player in players) faces += player.Target.position;
            bigTarget.position = faces / players.Count;
            brickPool.Initialize(brickPrefab, 3);
            brickPool.Prewarm(3);
            VfxManager.Instance.Prewarm(VfxManager.VisualEffect.BrickBurst, 2);
            VfxManager.Instance.Prewarm(VfxManager.VisualEffect.ShieldBurst, 2);

            ThrowCount = Schedule(players.Count, config.ThrowsPerPlayer, config.BigThrows).Count;
            hud.Initialize(players.Count, LocalizedStrings.WithArguments(statusText, ("current", 0), ("total", ThrowCount)));
            foreach (var player in players) hud.Panel(player.Slot).Initialize(player.PlayerIndex, player.Color, countText, config.FlashSeconds);
        }

        #endregion

        #region Throws

        /// <summary>Plays every throw in order.</summary>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var schedule = Schedule(players.Count, config.ThrowsPerPlayer, config.BigThrows);
            hud.ShowBanner(introBanner, config.IntroSeconds);
            hud.ShowHint(hintText);
            await WaitAsync(config.IntroSeconds, cancellationToken);
            for (throwNumber = 1; throwNumber <= schedule.Count; throwNumber++)
            {
                var lane = schedule[throwNumber - 1];
                hud.SetStatus(LocalizedStrings.WithArguments(statusText, ("current", throwNumber), ("total", schedule.Count)));
                if (throwNumber == 3) hud.HideHint();
                await ThrowAsync(lane, cancellationToken);
                await WaitAsync(config.GapSeconds, cancellationToken);
            }

            await WaitAsync(config.LateWindowSeconds, cancellationToken);
            hud.ShowBanner(endBanner, float.PositiveInfinity);
        }

        // lane -1 is a giant brick at everyone.
        async UniTask ThrowAsync(int lane, CancellationToken cancellationToken)
        {
            var big = lane < 0;
            var thrower = big ? bigThrower : throwers[players.Count == 1 ? throwNumber % 2 : lane];
            for (var slot = 0; slot < live.Length; slot++) live[slot] = big || slot == lane;
            liveUntil = Time.time + config.WindUpSeconds + config.FlightSeconds + config.LateWindowSeconds;
            thrower.WindUp(config.WindUpSeconds);
            friend.SetWorried(true);
            await WaitAsync(config.WindUpSeconds, cancellationToken);

            thrower.Throw();
            var target = big ? bigTarget : players[lane].Target;
            var brick = brickPool.Get();
            brick.Fly(brickPool.transform, thrower.ThrowPosition, target, config.FlightSeconds, config.ArcHeight, big ? bigBrickScale : 1f,
                brickColors[throwNumber % brickColors.Length]);
            hud.ShowPrompt(big && players.Count > 1 ? teamPrompt : blockPrompt, big ? Color.white : players[lane].Color);
            await WaitAsync(config.FlightSeconds, cancellationToken);

            hud.HidePrompt();
            friend.SetWorried(false);
            if (big) JudgeBig(brick);
            else JudgeLane(brick, players[lane]);
        }

        void JudgeLane(GuardBrick brick, GuardPlayer player)
        {
            var time = Time.time;
            var settings = config.ReaderSettings;
            if (player.Reader.IsFresh(time, settings))
            {
                brick.Burst(1.4f);
                Block(player, time, false);
                CheerFriend();
                return;
            }

            brick.LandOn(player.Ninja.HeadPoint, config.HatSeconds);
            Bonk(player, time, false);
        }

        void JudgeBig(GuardBrick brick)
        {
            var time = Time.time;
            var settings = config.ReaderSettings;
            var saved = true;
            foreach (var player in players) saved &= player.Reader.IsFresh(time, settings);
            brick.Burst(2.6f);
            BigThrows++;
            if (saved) BigSaves++;
            foreach (var player in players)
            {
                if (player.Reader.IsFresh(time, settings)) Block(player, time, true);
                else Bonk(player, time, true);
            }

            if (saved) CheerFriend();
            if (saved && players.Count > 1) hud.ShowBanner(teamBanner, 1.5f);
            GuardAnalytics.BigThrow(throwNumber, saved, players.Count);
        }

        void Block(GuardPlayer player, float time, bool big)
        {
            player.Record(GuardOutcome.Saved);
            player.Ninja.Shove(config.ShoveMeters);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.ShieldBurst, player.Shield.position);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GuardBlock);
            Show(player, GuardOutcome.Saved);
            GuardAnalytics.Throw(player.PlayerIndex, throwNumber, big, GuardOutcome.Saved, time - player.Reader.RaisedAt);
        }

        // The silly miss: dizzy stars and a wobble; what the reading saw is judged once the late window has passed.
        void Bonk(GuardPlayer player, float time, bool big)
        {
            player.Ninja.Wobble(config.DizzySeconds);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.DizzyStars, player.Ninja.HeadPosition);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Bonk);
            hud.Panel(player.Slot).Flash(config.GetLook(GuardOutcome.Missed).label, config.GetLook(GuardOutcome.Missed).color);
            pending.Add(new Pending { player = player, impact = time, held = player.Reader.IsUp, throwNumber = throwNumber, big = big });
        }

        void CheerFriend()
        {
            friend.Cheer();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.Hearts, friend.CheerPosition);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Cheer);
        }

        /// <summary>The throw order: each player's throws alternating (all on the only player alone), giant bricks spread through.</summary>
        public static List<int> Schedule(int playerCount, int throwsPerPlayer, int bigThrows)
        {
            var lanes = new List<int>(throwsPerPlayer * playerCount + bigThrows);
            int[] pattern = { 0, 1, 1, 0, 1, 0, 0, 1 };
            for (var i = 0; i < throwsPerPlayer * playerCount; i++) lanes.Add(playerCount == 1 ? 0 : pattern[i % pattern.Length]);
            for (var big = bigThrows; big >= 1; big--) lanes.Insert(big * lanes.Count / (bigThrows + 1), -1);
            return lanes;
        }

        #endregion

        #region Reading

        void Update()
        {
            var time = Time.time;
            var settings = config.ReaderSettings;
            var follow = 1f - Mathf.Exp(-shieldRate * Time.deltaTime);
            foreach (var player in players)
            {
                var guardEvent = player.Read(time, settings);
                if (guardEvent == GuardReader.Event.Raised && !(live[player.Slot] && time <= liveUntil))
                {
                    player.CountAlone();
                    GuardAnalytics.RaiseAlone(player.PlayerIndex, throwNumber);
                }

                var shieldSize = player.Reader.IsFresh(time, settings) ? 1f : 0f;
                player.Shield.localScale = Vector3.Lerp(player.Shield.localScale, Vector3.one * shieldSize, follow);
            }

            ResolvePending(time);
        }

        void ResolvePending(float time)
        {
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var miss = pending[i];
                if (time < miss.impact + config.LateWindowSeconds) continue;
                pending.RemoveAt(i);
                var outcome = GuardPlayer.ClassifyMiss(miss.impact, miss.held, miss.player.Reader.RaisedAt, miss.player.LastHalfAt, config.LateWindowSeconds,
                    config.HalfWindowSeconds);
                miss.player.Record(outcome);
                if (outcome != GuardOutcome.Missed) Show(miss.player, outcome);
                GuardAnalytics.Throw(miss.player.PlayerIndex, miss.throwNumber, miss.big, outcome, -1f);
            }
        }

        void Show(GuardPlayer player, GuardOutcome outcome)
        {
            var look = config.GetLook(outcome);
            var panel = hud.Panel(player.Slot);
            panel.Flash(look.label, look.color);
            panel.SetCount(player.Count(GuardOutcome.Saved));
        }

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);

        #endregion
    }
}
