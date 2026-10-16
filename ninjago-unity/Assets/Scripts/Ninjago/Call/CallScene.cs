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
    /// Call's market courtyard: a still camera behind the ninjas (one per active player, P1 on the left), goblins
    /// walking up in waves, and one big golden teammate in the sky. A call brings the teammate spinning down in front
    /// of the caller; the slam throws every goblin near it away, piled ones too. A second caller joining while it is on
    /// the way makes one big team slam. Goblins that reach a ninja climb on; a full pile is a silly fail and the wave
    /// starts again, and after enough piles the teammate comes by itself.
    /// </summary>
    public class CallScene : MonoBehaviour
    {
        [Header("Scene Camera")]
        [SerializeField] Camera sceneCamera = null!;
        [Header("Ninjas")]
        [Tooltip("Slot 0 then slot 1; the second stays hidden for one player.")]
        [SerializeField] KidNinja[] ninjas = null!;
        [Header("Duo Ninja Offset")]
        [SerializeField] float duoOffset = 1.7f;
        [Header("Teammate")]
        [SerializeField] CallTeammate teammate = null!;
        [Header("Goblin Pool")]
        [SerializeField] StreetMonsterPool goblinPool = null!;
        [Header("Goblin Prefab")]
        [SerializeField] StreetMonster goblinPrefab = null!;
        [Header("Goblin Colors")]
        [SerializeField] Color[] goblinColors = null!;
        [Header("Spawn Lanes")]
        [Tooltip("Share of the spawn half-width for each goblin of a wave, in order.")]
        [SerializeField] float[] spawnLanes = { -0.7f, 0.6f, -0.2f, 0.9f, -0.9f, 0.3f };
        [Header("Status Text")]
        [Tooltip("Smart string with {current} {total}.")]
        [SerializeField] LocalizedString statusText = new();
        [Header("Intro Banner")]
        [SerializeField] LocalizedString introBanner = new();
        [Header("Hint")]
        [SerializeField] LocalizedString hintText = new();
        [Header("Call Prompt")]
        [SerializeField] LocalizedString callPrompt = new();
        [Header("Team Banner")]
        [SerializeField] LocalizedString teamBanner = new();
        [Header("Wave Cleared Banner")]
        [SerializeField] LocalizedString clearedBanner = new();
        [Header("Pile Banner")]
        [SerializeField] LocalizedString pileBanner = new();
        [Header("Help Banner")]
        [Tooltip("When the teammate comes by itself.")]
        [SerializeField] LocalizedString helpBanner = new();
        [Header("End Banner")]
        [SerializeField] LocalizedString endBanner = new();
        [Header("Panel Count Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString countText = new();
        [Header("Ready Meter Color")]
        [SerializeField] Color readyColor = new(1f, 0.8f, 0.15f);
        [Header("Busy Meter Color")]
        [SerializeField] Color busyColor = new(0.6f, 0.6f, 0.6f);

        readonly List<CallPlayer> players = new();
        readonly List<StreetMonster> walking = new();
        CallConfig config = null!;
        KidsHud hud = null!;
        CallPlayer? caller;
        CallPlayer? queued;
        CallPlayer? piled;
        bool teamSlam;
        bool autoSlam;
        float readyAt;
        int remaining;
        int waveIndex;
        Color? shownPrompt;

        public IReadOnlyList<CallPlayer> Players => players;
        public int WaveCount => config.GoblinsPerWave.Count;
        public int WavesCleared { get; private set; }
        public int Piles { get; private set; }
        public int AutoHelps { get; private set; }
        public int TeamSlams { get; private set; }
        bool TeammateReady => teammate.IsAway && Time.time >= readyAt;

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers, CallConfig aConfig, KidsHud aHud, RenderTexture target)
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
                players.Add(new CallPlayer(body, ninjas[slot], slot));
            }

            teammate.Initialize();
            teammate.Landed += HandleLanded;
            teammate.Left += HandleLeft;
            goblinPool.Initialize(goblinPrefab, 6);
            goblinPool.Prewarm(6);
            VfxManager.Instance.Prewarm(VfxManager.VisualEffect.SlamShockwave, 1);
            VfxManager.Instance.Prewarm(VfxManager.VisualEffect.BrickBurst, 4);
            hud.Initialize(players.Count, LocalizedStrings.WithArguments(statusText, ("current", 1), ("total", WaveCount)));
            foreach (var player in players) hud.Panel(player.Slot).Initialize(player.PlayerIndex, player.Color, countText, config.FlashSeconds);
        }

        void OnDestroy()
        {
            teammate.Landed -= HandleLanded;
            teammate.Left -= HandleLeft;
        }

        #endregion

        #region Waves

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            hud.ShowBanner(introBanner, config.IntroSeconds);
            hud.ShowHint(hintText);
            await WaitAsync(config.IntroSeconds, cancellationToken);
            for (waveIndex = 0; waveIndex < WaveCount; waveIndex++)
            {
                hud.SetStatus(LocalizedStrings.WithArguments(statusText, ("current", waveIndex + 1), ("total", WaveCount)));
                if (waveIndex == 1) hud.HideHint();
                var pilesThisWave = 0;
                while (true)
                {
                    SpawnWave();
                    await UniTask.WaitUntil(() => remaining == 0 || piled != null, cancellationToken: cancellationToken);
                    if (piled == null) break;
                    pilesThisWave++;
                    Piles++;
                    CallAnalytics.Pile(piled.PlayerIndex, waveIndex + 1, pilesThisWave);
                    piled = null;
                    TumbleAll();
                    hud.ShowBanner(pileBanner, config.PileSeconds);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GoblinPile);
                    await WaitAsync(config.PileSeconds, cancellationToken);
                    if (pilesThisWave <= config.MaxPilesPerWave) continue;
                    // Enough tries: the teammate comes by itself and the scene moves on.
                    AutoHelps++;
                    CallAnalytics.AutoHelp(waveIndex + 1);
                    SpawnWave();
                    hud.ShowBanner(helpBanner, 1.5f);
                    await UniTask.WaitUntil(() => TeammateReady, cancellationToken: cancellationToken);
                    Send(null, center: true);
                    await UniTask.WaitUntil(() => remaining == 0, cancellationToken: cancellationToken);
                    break;
                }

                WavesCleared++;
                if (waveIndex < WaveCount - 1) hud.ShowBanner(clearedBanner, config.WaveGapSeconds);
                await WaitAsync(config.WaveGapSeconds, cancellationToken);
            }

            hud.HidePrompt();
            hud.ShowBanner(endBanner, float.PositiveInfinity);
        }

        void SpawnWave()
        {
            var count = config.GoblinsPerWave[waveIndex];
            remaining = count;
            for (var i = 0; i < count; i++)
            {
                var lane = spawnLanes[(i + waveIndex) % spawnLanes.Length];
                var position = new Vector3(lane * config.SpawnHalfWidth, 0f, config.SpawnDistance + i * config.SpawnSpacing);
                var goblin = goblinPool.Get();
                goblin.Spawn(goblinPool.transform, i + 1, players.Count > 1 && position.x >= 0f ? 1 : 0, position, goblinColors[i % goblinColors.Length],
                    config.GoblinScale);
                walking.Add(goblin);
            }
        }

        // The soft fail: every goblin, piled or walking, tumbles away.
        void TumbleAll()
        {
            foreach (var goblin in walking) Throw(goblin, goblin.transform.position + Vector3.forward);
            walking.Clear();
            foreach (var player in players)
            {
                foreach (var goblin in player.Pile) Throw(goblin, player.Ninja.transform.position);
                player.ClearPile();
                player.Ninja.Wobble(1.2f);
            }
        }

        #endregion

        #region Reading

        void Update()
        {
            var time = Time.time;
            var settings = config.ReaderSettings;
            foreach (var player in players)
            {
                var callEvent = player.Read(time, settings);
                if (callEvent == CallReader.Event.Call) OnCall(player);
                else if (callEvent == CallReader.Event.Missed) Report(player, CallResult.Missed);
            }

            if (queued != null && TeammateReady)
            {
                var next = queued;
                queued = null;
                Send(next, center: false);
            }

            MoveGoblins(Time.deltaTime);
            UpdatePrompt(time);
        }

        void OnCall(CallPlayer player)
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.CallSignal);
            var beam = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.CallBeam, player.Ninja.transform.position, Quaternion.identity);
            if (beam != null)
            {
                var main = beam.main;
                main.startColor = player.Color;
            }

            if (TeammateReady)
            {
                Send(player, center: false);
                Report(player, CallResult.Understood);
            }
            else if (teammate.IsArriving && caller != null && caller != player && !teamSlam)
            {
                teamSlam = true;
                teammate.Retarget(new Vector3(0f, 0f, config.LandAhead));
                hud.ShowBanner(teamBanner, 1.2f);
                Report(player, CallResult.Team);
            }
            else
            {
                queued ??= player;
                Report(player, CallResult.Queued);
            }
        }

        void Report(CallPlayer player, CallResult result)
        {
            player.Record(result);
            var look = config.GetLook(result);
            var panel = hud.Panel(player.Slot);
            panel.Flash(look.label, look.color);
            panel.SetCount(player.Understood);
            CallAnalytics.Call(player.PlayerIndex, waveIndex + 1, result, player.Reader.Hand, GoblinsNear(player), player.Pile.Count);
        }

        void UpdatePrompt(float time)
        {
            Color? prompt = null;
            var nearest = config.PromptDistance;
            if (TeammateReady)
            {
                foreach (var goblin in walking)
                {
                    foreach (var player in players)
                    {
                        var distance = Vector3.Distance(goblin.LocalPosition, player.Ninja.transform.localPosition);
                        if (distance >= nearest) continue;
                        nearest = distance;
                        prompt = player.Color;
                    }
                }
            }

            if (prompt != shownPrompt)
            {
                shownPrompt = prompt;
                if (prompt.HasValue) hud.ShowPrompt(callPrompt, prompt.Value);
                else hud.HidePrompt();
            }

            var ready = TeammateReady;
            var fill = ready ? 1f : teammate.IsAway ? 1f - Mathf.Clamp01((readyAt - time) / Mathf.Max(0.01f, config.CooldownSeconds)) : 0f;
            hud.SetMeter(fill, ready ? readyColor : busyColor);
        }

        #endregion

        #region Goblins

        void MoveGoblins(float deltaTime)
        {
            var step = config.GoblinSpeed * deltaTime;
            for (var i = walking.Count - 1; i >= 0; i--)
            {
                var goblin = walking[i];
                var target = players[goblin.TargetSlot];
                if (goblin.StepToward(target.Ninja.transform.localPosition, step) > config.GrabRange) continue;
                walking.RemoveAt(i);
                goblin.JumpOn(target.PileTop, 0.35f);
                target.AddToPile(goblin);
                target.Ninja.Wobble(0.5f);
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Pin);
                if (target.Pile.Count >= config.PileSize) piled ??= target;
            }
        }

        int GoblinsNear(CallPlayer player)
        {
            var count = 0;
            foreach (var goblin in walking)
            {
                if (Vector3.Distance(goblin.LocalPosition, player.Ninja.transform.localPosition) < config.PromptDistance) count++;
            }

            return count;
        }

        // Off in a big arc away from the slam, bursting into bricks.
        void Throw(StreetMonster goblin, Vector3 awayFrom)
        {
            remaining--;
            var side = goblin.transform.position.x >= awayFrom.x ? 1f : -1f;
            var velocity = config.LaunchVelocity;
            goblin.Launch(new Vector3(velocity.x * side, velocity.y, velocity.z), config.FlightGravity, config.FlightSpinDegrees, config.FlightSeconds);
        }

        #endregion

        #region Teammate

        // caller null with center: the teammate came by itself.
        void Send(CallPlayer? who, bool center)
        {
            caller = who;
            teamSlam = false;
            autoSlam = who == null;
            var spot = center || who == null ? new Vector3(0f, 0f, config.LandAhead) : who.Ninja.transform.localPosition + new Vector3(0f, 0f, config.LandAhead);
            teammate.Arrive(spot, config.ArriveSeconds);
        }

        void HandleLanded()
        {
            var spot = transform.TransformPoint(teammate.Spot);
            var radius = config.SlamRadius * (teamSlam ? 2f : 1f);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.SlamShockwave, spot, Quaternion.identity, radius / 4f);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.SpinjitzuBurst, spot + Vector3.up);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.TeammateSlam);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.FlyAway);
            var cleared = 0;
            for (var i = walking.Count - 1; i >= 0; i--)
            {
                var goblin = walking[i];
                var offset = goblin.transform.position - spot;
                offset.y = 0f;
                if (!autoSlam && offset.magnitude > radius) continue;
                walking.RemoveAt(i);
                Throw(goblin, spot);
                cleared++;
            }

            foreach (var player in players)
            {
                if (!autoSlam && !teamSlam && player != caller) continue;
                foreach (var goblin in player.Pile)
                {
                    Throw(goblin, spot);
                    cleared++;
                }

                player.ClearPile();
            }

            if (teamSlam) TeamSlams++;
            CallAnalytics.Slam(waveIndex + 1, caller?.PlayerIndex ?? -1, teamSlam, cleared);
            LeaveAfterStayAsync(destroyCancellationToken).Forget();
        }

        async UniTaskVoid LeaveAfterStayAsync(CancellationToken cancellationToken)
        {
            await WaitAsync(config.StayAfterSlamSeconds, cancellationToken);
            teammate.Leave(config.LeaveSeconds);
        }

        void HandleLeft()
        {
            readyAt = Time.time + config.CooldownSeconds;
        }

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);

        #endregion
    }
}
