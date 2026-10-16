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
    /// Build's river crossing: a still camera behind the ninjas (one per active player, P1 on the left) at the edge of a
    /// river, a gate shut on the far side. Every click (hands together) sends one brick hopping from that player's pile
    /// onto the bridge, row by row, two bricks wide; the ninjas step onto each finished row. A full bridge opens the gate
    /// and the ninjas bounce across. If the time runs out first, the way stays shut.
    /// </summary>
    public class BuildScene : MonoBehaviour
    {
        const int Columns = 2;

        [Header("Scene Camera")]
        [SerializeField] Camera sceneCamera = null!;
        [Header("Ninjas")]
        [Tooltip("Slot 0 then slot 1; the second stays hidden for one player.")]
        [SerializeField] KidNinja[] ninjas = null!;
        [Header("Brick Piles")]
        [Tooltip("Left (slot 0) then right (slot 1); a player's bricks fly from that player's pile.")]
        [SerializeField] Transform[] piles = null!;
        [Header("Bridge Root")]
        [Tooltip("At this side's edge of the river, on the ground; rows go along +Z.")]
        [SerializeField] Transform bridgeRoot = null!;
        [Header("Brick Prefab")]
        [SerializeField] BuildBrick brickPrefab = null!;
        [Header("Row Length")]
        [Tooltip("Meters each row of bricks covers along the bridge.")]
        [SerializeField] float rowLength = 0.64f;
        [Header("Column Width")]
        [SerializeField] float columnWidth = 1.28f;
        [Header("Brick Height")]
        [SerializeField] float brickHeight = 0.64f;
        [Header("Gate")]
        [SerializeField] BuildGate gate = null!;
        [Header("Beyond The Gate")]
        [Tooltip("Meters past the far end of the bridge where the ninjas stop after crossing.")]
        [SerializeField] float beyondGate = 3.5f;
        [Header("Status Text")]
        [Tooltip("Smart string with {placed} {total} {seconds}.")]
        [SerializeField] LocalizedString statusText = new();
        [Header("Intro Banner")]
        [SerializeField] LocalizedString introBanner = new();
        [Header("Hint")]
        [SerializeField] LocalizedString hintText = new();
        [Header("Open Banner")]
        [SerializeField] LocalizedString openBanner = new();
        [Header("Time Up Banner")]
        [SerializeField] LocalizedString timeUpBanner = new();
        [Header("Panel Count Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString countText = new();
        [Header("Progress Color")]
        [SerializeField] Color progressColor = new(0.35f, 0.9f, 0.4f);

        readonly List<BuildPlayer> players = new();
        BuildConfig config = null!;
        KidsHud hud = null!;
        bool[] taken = null!;
        bool[] landed = null!;
        int doneRows;
        float lastClickTime = float.NegativeInfinity;
        bool running;
        bool hintShown;
        int shownSeconds = -1;
        float nextHopTime;

        public IReadOnlyList<BuildPlayer> Players => players;
        public int Placed { get; private set; }
        public int Total => taken.Length;
        public float Elapsed { get; private set; }
        public bool IsOpen { get; private set; }
        float GapEnd => bridgeRoot.localPosition.z + config.Rows * rowLength;

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers, BuildConfig aConfig, KidsHud aHud, RenderTexture target)
        {
            config = aConfig;
            hud = aHud;
            sceneCamera.targetTexture = target;
            taken = new bool[config.Rows * Columns];
            landed = new bool[taken.Length];
            var duo = activePlayers.Count > 1;
            for (var slot = 0; slot < ninjas.Length; slot++)
            {
                var used = slot < activePlayers.Count;
                ninjas[slot].gameObject.SetActive(used);
                piles[slot].gameObject.SetActive(used || slot == 0);
                if (!used) continue;
                var body = activePlayers[slot];
                ninjas[slot].transform.localPosition = new Vector3(duo ? (slot - 0.5f) * columnWidth : 0f, 0f, 0f);
                ninjas[slot].Initialize(body.PlayerIndex, body.Color);
                players.Add(new BuildPlayer(body, ninjas[slot], piles[slot], slot));
            }

            gate.transform.localPosition = new Vector3(0f, 0f, GapEnd + 0.8f);
            VfxManager.Instance.Prewarm(VfxManager.VisualEffect.BrickSnapPop, 4);
            hud.Initialize(players.Count, Status());
            hud.SetMeter(0f, progressColor);
            foreach (var player in players) hud.Panel(player.Slot).Initialize(player.PlayerIndex, player.Color, countText, config.FlashSeconds);
        }

        #endregion

        #region Building

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            hud.ShowBanner(introBanner, config.IntroSeconds);
            ShowHint(true);
            await WaitAsync(config.IntroSeconds, cancellationToken);
            running = true;
            lastClickTime = Time.time;
            await UniTask.WaitUntil(() => IsOpen || Elapsed >= config.TimeLimitSeconds, cancellationToken: cancellationToken);
            running = false;
            ShowHint(false);
            BuildAnalytics.End(IsOpen, Placed, Total, Elapsed);
            if (!IsOpen)
            {
                hud.ShowBanner(timeUpBanner, float.PositiveInfinity);
                return;
            }

            gate.Open();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.GateCleared, gate.transform.position + Vector3.up * 2.5f);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.WayOpen);
            hud.ShowBanner(openBanner, float.PositiveInfinity);
            await WaitAsync(config.CelebrateSeconds, cancellationToken);
        }

        void OnClick(BuildPlayer player, float time)
        {
            var slot = NextSlot(taken, Columns, players.Count > 1 ? player.Slot : CountTaken() % Columns);
            if (slot < 0) return;
            taken[slot] = true;
            player.Record(BuildGesture.Counted);
            Show(player, BuildGesture.Counted);
            BuildAnalytics.Clap(player.PlayerIndex, CountTaken(), float.IsNegativeInfinity(player.LastClapAt) ? -1f : time - player.LastClapAt, Elapsed);
            player.LastClapAt = time;
            lastClickTime = time;
            var brick = Instantiate(brickPrefab, bridgeRoot);
            brick.Landed += HandleLanded;
            brick.Fly(slot, player.Pile.position, bridgeRoot.TransformPoint(SlotPosition(slot)), config.BrickFlightSeconds, player.Color);
        }

        void HandleLanded(BuildBrick brick)
        {
            brick.Landed -= HandleLanded;
            landed[brick.Slot] = true;
            Placed++;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickSnapPop, brick.transform.position + Vector3.up * brickHeight * 0.5f);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BrickSnap);
            hud.SetMeter((float)Placed / Total, progressColor);
            hud.SetStatus(Status());
            while (doneRows < config.Rows && landed[doneRows * Columns] && landed[doneRows * Columns + 1])
            {
                doneRows++;
                BuildAnalytics.RowDone(doneRows, Elapsed);
            }

            if (doneRows == config.Rows) IsOpen = true;
        }

        /// <summary>The frontmost free slot, the preferred column first: rows fill in order, and a faster builder helps the slower one.</summary>
        public static int NextSlot(IReadOnlyList<bool> taken, int columns, int preferredColumn)
        {
            for (var row = 0; row * columns < taken.Count; row++)
            {
                if (!taken[row * columns + preferredColumn]) return row * columns + preferredColumn;
                for (var column = 0; column < columns; column++)
                {
                    if (!taken[row * columns + column]) return row * columns + column;
                }
            }

            return -1;
        }

        Vector3 SlotPosition(int slot) => new(((slot % Columns) - 0.5f) * columnWidth, -brickHeight * 0.5f, (slot / Columns + 0.5f) * rowLength);

        int CountTaken()
        {
            var count = 0;
            foreach (var isTaken in taken) count += isTaken ? 1 : 0;
            return count;
        }

        #endregion

        #region Reading

        void Update()
        {
            var time = Time.time;
            var settings = config.ReaderSettings;
            foreach (var player in players)
            {
                var clap = player.Read(time, settings);
                if (!running) continue;
                if (clap == ClapReader.Event.Clap) OnClick(player, time);
                else if (clap == ClapReader.Event.NearClap)
                {
                    player.Record(BuildGesture.Near);
                    Show(player, BuildGesture.Near);
                    BuildAnalytics.NearClap(player.PlayerIndex, Elapsed);
                }
            }

            if (running)
            {
                Elapsed += Time.deltaTime;
                ShowHint(time - lastClickTime > config.IdleHintSeconds);
                var seconds = Mathf.CeilToInt(Mathf.Max(0f, config.TimeLimitSeconds - Elapsed));
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    hud.SetStatus(Status());
                }
            }

            Walk(Time.deltaTime);
        }

        // On the last finished row while building; through the gate once it is open, bouncing.
        void Walk(float deltaTime)
        {
            var targetZ = IsOpen ? GapEnd + beyondGate : doneRows == 0 ? 0f : bridgeRoot.localPosition.z + (doneRows - 0.5f) * rowLength;
            var hop = IsOpen && Time.time >= nextHopTime;
            if (hop) nextHopTime = Time.time + 0.42f;
            foreach (var player in players)
            {
                var ninja = player.Ninja.transform;
                var position = ninja.localPosition;
                var moving = position.z < targetZ - 0.01f;
                player.Ninja.SetStepping(moving);
                if (!moving) continue;
                position.z = Mathf.MoveTowards(position.z, targetZ, config.WalkSpeed * deltaTime);
                ninja.localPosition = position;
                if (hop) player.Ninja.Hop(0.45f, 0.38f);
            }
        }

        void Show(BuildPlayer player, BuildGesture gesture)
        {
            var look = config.GetLook(gesture);
            var panel = hud.Panel(player.Slot);
            panel.Flash(look.label, look.color);
            panel.SetCount(player.Count(BuildGesture.Counted));
        }

        void ShowHint(bool show)
        {
            if (show == hintShown) return;
            hintShown = show;
            if (show) hud.ShowHint(hintText);
            else hud.HideHint();
        }

        LocalizedString Status() =>
            LocalizedStrings.WithArguments(statusText, ("placed", Placed), ("total", Total),
                ("seconds", Mathf.CeilToInt(Mathf.Max(0f, config.TimeLimitSeconds - Elapsed))));

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);

        #endregion
    }
}
