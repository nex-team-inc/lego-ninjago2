#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The shared street: a still camera behind the ninjas (one per active player, P1 on the left) and waves of
    /// monsters walking up from the far end. A counted punch throws the free monster nearest that ninja, or the one
    /// pinning the partner. A monster that reaches a ninja jumps on him: every other monster freezes, his health drains,
    /// and filling the wiggle meter (or the partner's punch) throws it off. Health at zero ends the run.
    /// </summary>
    public class OnePunchStreet : MonoBehaviour
    {
        [Header("Street Camera")]
        [SerializeField] Camera streetCamera = null!;
        [Header("Ninjas")]
        [Tooltip("Slot 0 then slot 1; the second stays hidden for one player.")]
        [SerializeField] StreetNinja[] ninjas = null!;
        [Header("Duo Ninja Offset")]
        [Tooltip("Meters from the street's middle to each ninja with two players.")]
        [SerializeField] float duoOffset = 1.7f;
        [Header("Monster Pool")]
        [SerializeField] StreetMonsterPool monsterPool = null!;
        [Header("Monster Prefab")]
        [SerializeField] StreetMonster monsterPrefab = null!;
        [Header("Monster Colors")]
        [Tooltip("One per place in a wave, so the monsters are easy to tell apart and count.")]
        [SerializeField] Color[] monsterColors = null!;
        [Header("Spawn Lanes")]
        [Tooltip("Share of the spawn half-width for each place in a wave; each wave starts two places further on.")]
        [SerializeField] float[] spawnLanes = { -0.8f, 0.45f, -0.15f, 0.85f, -0.5f };
        [Header("Impact Height")]
        [Tooltip("Meters above a thrown monster's feet where the punch impact plays.")]
        [SerializeField] float impactHeight = 1.1f;

        readonly List<StreetPlayer> players = new();
        readonly List<StreetMonster> walking = new();
        OnePunchConfig config = null!;
        PunchTracker tracker = null!;
        OnePunchHud hud = null!;
        StreetPlayer? pinned;
        StreetMonster? pinner;
        float pinElapsed;
        bool pinLanded;
        bool live;
        int remaining;
        float waveStart;
        int[] waveThrows = null!;

        public IReadOnlyList<StreetPlayer> Players => players;
        public int WavesCleared { get; private set; }
        public bool IsDown { get; private set; }
        public float FightSeconds { get; private set; }
        int Wave => WaveIndex + 1;
        int WaveIndex { get; set; }

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers, OnePunchConfig aConfig, PunchTracker aTracker, OnePunchHud aHud, RenderTexture target)
        {
            config = aConfig;
            tracker = aTracker;
            hud = aHud;
            streetCamera.targetTexture = target;
            var duo = activePlayers.Count > 1;
            for (var slot = 0; slot < ninjas.Length; slot++)
            {
                var used = slot < activePlayers.Count;
                ninjas[slot].gameObject.SetActive(used);
                if (!used) continue;
                var body = activePlayers[slot];
                ninjas[slot].transform.localPosition = new Vector3(duo ? (slot == 0 ? -duoOffset : duoOffset) : 0f, 0f, 0f);
                ninjas[slot].Initialize(body.PlayerIndex, body.Color);
                players.Add(new StreetPlayer(body, ninjas[slot], slot, config.StartingHealth));
            }

            waveThrows = new int[players.Count];
            monsterPool.Initialize(monsterPrefab, config.MonstersPerWave + 1);
            monsterPool.Prewarm(config.MonstersPerWave + 1);
            hud.Initialize(config, players);
            tracker.Tried += HandleTry;
        }

        void OnDestroy()
        {
            tracker.Tried -= HandleTry;
        }

        #endregion

        #region Waves

        /// <summary>Plays every wave until the street is clear or a pinned player's health runs out.</summary>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            live = true;
            foreach (var player in players) tracker.SetListening(player.PlayerIndex, true);
            for (WaveIndex = 0; WaveIndex < config.WaveCount; WaveIndex++)
            {
                SpawnWave();
                hud.ShowWaveBanner(Wave, WaveIndex == 0);
                while (remaining > 0 && !IsDown)
                {
                    Step(Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                EndWave();
                if (IsDown || WaveIndex == config.WaveCount - 1) break;
                hud.ShowWaveCleared();
                for (var waited = 0f; waited < config.SecondsBetweenWaves; waited += Time.deltaTime)
                {
                    FightSeconds += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            live = false;
            foreach (var player in players) tracker.SetListening(player.PlayerIndex, false);
            if (IsDown) hud.ShowDown();
            else hud.ShowStreetCleared();
        }

        void SpawnWave()
        {
            remaining = config.MonstersPerWave;
            waveStart = FightSeconds;
            Array.Clear(waveThrows, 0, waveThrows.Length);
            for (var i = 0; i < config.MonstersPerWave; i++)
            {
                var lane = spawnLanes[(i + WaveIndex * 2) % spawnLanes.Length];
                var position = new Vector3(lane * config.SpawnHalfWidth, 0f, config.FirstMonsterDistance + i * config.MonsterSpacing);
                var monster = monsterPool.Get();
                monster.Spawn(monsterPool.transform, i + 1, players.Count > 1 && position.x >= 0f ? 1 : 0, position, monsterColors[i % monsterColors.Length]);
                walking.Add(monster);
            }

            hud.SetWave(Wave, config.WaveCount, remaining);
        }

        void EndWave()
        {
            if (!IsDown) WavesCleared++;
            var details = new GameAnalyticsProperties { ["active_players"] = players.Count };
            foreach (var player in players)
            {
                details[$"p{player.PlayerIndex + 1}_thrown"] = waveThrows[player.Slot];
                details[$"p{player.PlayerIndex + 1}_health"] = Mathf.Round(player.Health * 100f) / 100f;
            }

            OnePunchAnalytics.WaveEnd(Wave, IsDown ? "down" : "cleared", FightSeconds - waveStart, details);
        }

        void Step(float deltaTime)
        {
            FightSeconds += deltaTime;
            if (pinned != null) UpdatePin(pinned, deltaTime);
            else MoveMonsters(deltaTime);
            PrintDebug();
        }

        void MoveMonsters(float deltaTime)
        {
            var step = config.MonsterSpeed * deltaTime;
            foreach (var monster in walking)
            {
                var target = players[monster.TargetSlot];
                if (monster.StepToward(target.Ninja.transform.localPosition, step) > config.GrabRange) continue;
                StartPin(target, monster);
                return;
            }
        }

        #endregion

        #region Punches

        void HandleTry(int playerIndex, int hand, PunchDetector.Try attempt)
        {
            if (!live) return;
            var player = FindPlayer(playerIndex);
            player.CountTry(attempt.signal);
            hud.ShowTry(player, attempt.signal);
            OnePunchAnalytics.PunchTry(playerIndex, Wave, hand, attempt);
            if (attempt.IsPunch) Punch(player, hand);
        }

        // Only free players listen, so a pinned player never punches here.
        void Punch(StreetPlayer player, int hand)
        {
            player.Ninja.PlayPunch(hand);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PunchImpact, player.Ninja.FistPosition(hand));
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Punch);
            if (pinned != null)
            {
                Rescue(player);
                return;
            }

            var target = Closest(player.Ninja.transform.localPosition);
            if (target == null) return;
            walking.Remove(target);
            player.CountThrow();
            waveThrows[player.Slot]++;
            OnePunchAnalytics.Thrown(player.PlayerIndex, Wave, target.Number, "punch", Distance(target, player));
            Throw(target);
        }

        StreetMonster? Closest(Vector3 from)
        {
            StreetMonster? closest = null;
            var closestDistance = float.MaxValue;
            foreach (var monster in walking)
            {
                var distance = (monster.LocalPosition - from).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = monster;
                closestDistance = distance;
            }

            return closest;
        }

        // Off the ground in a long arc, away from the street's middle, with a pop where it was hit.
        void Throw(StreetMonster monster)
        {
            remaining--;
            hud.SetWave(Wave, config.WaveCount, remaining);
            var side = monster.LocalPosition.x >= 0f ? 1f : -1f;
            var velocity = config.LaunchVelocity;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PunchImpact, monster.transform.position + Vector3.up * impactHeight);
            monster.Launch(new Vector3(velocity.x * side, velocity.y, velocity.z), config.FlightGravity, config.FlightSpinDegrees, config.FlightSeconds);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.FlyAway);
        }

        #endregion

        #region Pin

        void StartPin(StreetPlayer player, StreetMonster monster)
        {
            walking.Remove(monster);
            pinned = player;
            pinner = monster;
            pinElapsed = 0f;
            pinLanded = false;
            player.StartPin();
            tracker.SetListening(player.PlayerIndex, false);
            monster.JumpOn(player.Ninja.Seat, config.PinJumpSeconds);
            foreach (var other in walking) other.SetFrozen(true);
            hud.ShowPin(player.Color);
            OnePunchAnalytics.PinStart(player.PlayerIndex, Wave, monster.Number, player.Health);
        }

        void UpdatePin(StreetPlayer player, float deltaTime)
        {
            pinElapsed += deltaTime;
            if (pinElapsed < config.PinJumpSeconds) return;
            if (!pinLanded)
            {
                pinLanded = true;
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PinSlam, player.Ninja.Seat.position);
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Pin);
            }

            if (player.TrySampleHips(out var hips))
            {
                player.Meter.Add(hips, Time.time, config.WiggleWindowSeconds);
                player.Ninja.SetSway(hips - player.Body.Rest.hips);
            }

            var free = player.Meter.Tick(Time.time, deltaTime, config.WiggleWindowSeconds, config.WigglePathInches, config.WiggleRadiusInches,
                config.WiggleFillSeconds);
            hud.SetWiggle(player.Meter.Fill, player.Meter.IsWiggling);
            if (free)
            {
                WiggleFree(player);
                return;
            }

            if (player.Drain(config.DrainPerSecond * deltaTime)) OnePunchAnalytics.HealthLoss(player.PlayerIndex, Wave, player.Health);
            hud.SetHealth(player);
            if (player.Health > 0f) return;
            IsDown = true;
            OnePunchAnalytics.Down(player.PlayerIndex, Wave, pinElapsed);
        }

        void WiggleFree(StreetPlayer player)
        {
            player.CountWiggle();
            OnePunchAnalytics.Wiggle(player.PlayerIndex, Wave, pinElapsed, player.Health);
            OnePunchAnalytics.Thrown(player.PlayerIndex, Wave, pinner!.Number, "wiggle", 0f);
            OnePunchAnalytics.PinEscape(player.PlayerIndex, Wave, "wiggle", pinElapsed, player.Health);
            ReleasePin();
        }

        void Rescue(StreetPlayer rescuer)
        {
            rescuer.CountThrow();
            waveThrows[rescuer.Slot]++;
            OnePunchAnalytics.Thrown(rescuer.PlayerIndex, Wave, pinner!.Number, "rescue", Distance(pinner, rescuer));
            OnePunchAnalytics.PinEscape(pinned!.PlayerIndex, Wave, "partner_punch", pinElapsed, pinned.Health);
            ReleasePin();
        }

        void ReleasePin()
        {
            var freed = pinned!;
            var monster = pinner!;
            pinned = null;
            pinner = null;
            freed.EndPin();
            tracker.SetListening(freed.PlayerIndex, true);
            foreach (var other in walking) other.SetFrozen(false);
            hud.HidePin();
            Throw(monster);
        }

        #endregion

        #region Helpers

        StreetPlayer FindPlayer(int playerIndex)
        {
            foreach (var player in players)
            {
                if (player.PlayerIndex == playerIndex) return player;
            }

            throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, "Not an active player");
        }

        static float Distance(StreetMonster monster, StreetPlayer player) => Vector3.Distance(monster.transform.position, player.Ninja.transform.position);

        void PrintDebug()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!PlayerDataManager.Instance.DebugSettings.enableDebugPrinter) return;
            foreach (var player in players)
            {
                DebugPrinter.Instance.Print($"Punch P{player.PlayerIndex + 1}",
                    $"ext {player.Tries(PunchSignal.Extension)} cam {player.Tries(PunchSignal.TowardCamera)} slash {player.Tries(PunchSignal.SideSlash)} " +
                    $"none {player.Tries(PunchSignal.None)} health {player.Health:0.0} wiggle {player.Meter.Fill:0.00}{(player.Meter.IsWiggling ? " *" : "")}");
            }

            DebugPrinter.Instance.Print("Punch Street", $"wave {Wave} left {remaining} hand scale samples {tracker.HandScaleSamples}");
#endif
        }

        #endregion
    }
}
