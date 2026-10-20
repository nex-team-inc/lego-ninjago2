#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// A fight in an open space along the route, seen from behind the ninjas. Every threat follows tell, window,
    /// gesture, result. The entry's types (Slash and Kick, Slip and Spin, or both with a pattern for Mixed) become a
    /// list of threat steps; each step runs for every player's lane at once with that player's own enemy, while the
    /// Boss takes the steps in turn so one large enemy stays readable. Results burst the crowd or chip the Boss.
    /// </summary>
    public class FightBlock : ChapterBlock
    {
        [Header("Solo Ninja Spot")]
        [SerializeField] Transform soloSpot = null!;
        [Header("Duo Ninja Spots")]
        [SerializeField] Transform[] duoSpots = null!;
        [Header("Solo Camera Pose")]
        [SerializeField] Transform soloCameraPose = null!;
        [Header("Duo Camera Pose")]
        [SerializeField] Transform duoCameraPose = null!;
        [Header("Boss Camera Pose")]
        [Tooltip("Lower and looking up, so the whole Boss reads from across the arena.")]
        [SerializeField] Transform bossCameraPose = null!;
        [Header("Attacker Offset")]
        [Tooltip("Where a player's enemy stands, from that player's spot; the staff reaches the ninja from here.")]
        [SerializeField] Vector3 attackerOffset = new(0f, 0f, 4.4f);
        [Header("Solo Hang Point")]
        [Tooltip("Where a thrown rock or leaping monster hangs in front of a lone ninja.")]
        [SerializeField] Transform soloHang = null!;
        [Header("Duo Hang Points")]
        [SerializeField] Transform[] duoHangs = null!;
        [Header("Crowd Spots")]
        [SerializeField] Transform[] crowdSpots = null!;
        [Header("Boss Spot")]
        [SerializeField] Transform bossSpot = null!;
        [Header("Enemy Prefab")]
        [SerializeField] ChapterEnemy enemyPrefab = null!;
        [Header("Boss Prefab")]
        [SerializeField] ChapterEnemy bossPrefab = null!;
        [Header("Rock Prefab")]
        [SerializeField] Transform rockPrefab = null!;
        [Header("Stones Prefab")]
        [SerializeField] RockPieces piecesPrefab = null!;
        [Header("Window Marker Prefab")]
        [SerializeField] WindowMarker markerPrefab = null!;
        [Header("Enemy Colors")]
        [SerializeField] Color[] enemyColors = { new(0.45f, 0.75f, 0.3f), new(0.85f, 0.45f, 0.75f), new(0.35f, 0.55f, 0.95f), new(0.95f, 0.65f, 0.2f) };
        [Header("Boss Color")]
        [SerializeField] Color bossColor = new(0.35f, 0.3f, 0.4f);
        [Header("Fight Config")]
        [Tooltip("Owner of the slip threshold, the hand storm and the counter's slow motion.")]
        [SerializeField] FightConfig fightConfig = null!;
        [Header("Stone Kick Config")]
        [Tooltip("Owner of the slash speed and the kick reading.")]
        [SerializeField] StoneKickConfig stoneKickConfig = null!;
        [Header("One Punch Config")]
        [Tooltip("Owner of the hip wiggle that breaks a pin.")]
        [SerializeField] OnePunchConfig onePunchConfig = null!;
        [Header("Kick Text")]
        [Tooltip("Smart string with {count}.")]
        [SerializeField] LocalizedString kickText = new();
        [Header("Wiggle Text")]
        [SerializeField] LocalizedString wiggleText = new();

        readonly List<ChapterFightLane> lanes = new();
        readonly List<ChapterEnemy> attackers = new();
        readonly List<ChapterEnemy> crowd = new();
        readonly List<FightPattern.Step> steps = new();
        readonly Dictionary<BlockType, IFightThreat> threats = new();
        ChapterEnemy? boss;
        System.Random random = null!;
        int seed;
        bool running;

        public override Vector3 FocusPoint => (boss != null ? bossSpot : soloSpot).position + Vector3.up;
        public BlockDifficulty Difficulty => Entry.difficulty;
        public FightConfig FightConfig => fightConfig;
        public StoneKickConfig StoneKickConfig => stoneKickConfig;
        public OnePunchConfig OnePunchConfig => onePunchConfig;
        public float BossDamage { get; private set; }

        #region Initialization

        public override void Initialize(BlockContext context, BlockEntry entry, int aSeed)
        {
            base.Initialize(context, entry, aSeed);
            seed = aSeed;
            threats[BlockType.SlashKick] = new SlashKickThreat(this);
            threats[BlockType.SlipSpin] = new SlipSpinThreat(this);
            FightPattern.Build(entry, context.Catalog, steps);
            for (var slot = 0; slot < duoSpots.Length; slot++)
            {
                var attacker = Instantiate(enemyPrefab, transform);
                attacker.Initialize(enemyColors[slot % enemyColors.Length]);
                attackers.Add(attacker);
            }

            foreach (var _ in crowdSpots)
            {
                var minion = Instantiate(enemyPrefab, transform);
                minion.Initialize(enemyColors[(crowd.Count + 2) % enemyColors.Length]);
                crowd.Add(minion);
            }

            if (entry.boss)
            {
                boss = Instantiate(bossPrefab, transform);
                boss.Initialize(bossColor);
            }

            for (var slot = 0; slot < context.Players.Count; slot++)
            {
                var hang = context.Players.Count > 1 ? duoHangs[slot] : soloHang;
                var marker = Instantiate(markerPrefab, transform);
                var arrow = Instantiate(markerPrefab, transform);
                marker.Initialize(context.Camera);
                arrow.Initialize(context.Camera);
                var firstSafe = slot == 0 ? fightConfig.FirstSafeSide : fightConfig.FirstSafeSide.Opposite();
                lanes.Add(new ChapterFightLane(context.Players[slot], context.Ninjas[slot], boss != null ? boss : attackers[slot],
                    Instantiate(rockPrefab, transform), Instantiate(piecesPrefab, transform), marker, arrow, hang, firstSafe));
            }
        }

        #endregion

        #region Block

        public override void Stage()
        {
            running = false;
            random = new System.Random(seed);
            BossDamage = 0f;
            var cameraPose = boss != null ? bossCameraPose : lanes.Count > 1 ? duoCameraPose : soloCameraPose;
            ViewCamera.transform.SetPositionAndRotation(cameraPose.position, cameraPose.rotation);
            for (var slot = 0; slot < attackers.Count; slot++)
            {
                var attacker = attackers[slot];
                var used = boss == null && slot < lanes.Count;
                attacker.gameObject.SetActive(used);
                if (!used) continue;
                var spot = Spot(slot);
                attacker.Spawn(spot.TransformPoint(attackerOffset), spot.rotation);
                attacker.SetOwner(lanes[slot].PlayerIndex, lanes[slot].Color);
                attacker.ShowStaff(false);
            }

            var crowdCount = boss != null ? 0 : Mathf.Min(crowd.Count, steps.Count);
            for (var i = 0; i < crowd.Count; i++)
            {
                crowd[i].gameObject.SetActive(i < crowdCount);
                if (i < crowdCount) crowd[i].Spawn(crowdSpots[i].position, crowdSpots[i].rotation);
                crowd[i].SetOwner(null, Color.white);
            }

            if (boss != null)
            {
                boss.Spawn(bossSpot.position, bossSpot.rotation);
                boss.ShowStaff(false);
            }

            foreach (var lane in lanes)
            {
                lane.Pinned = lane.Frozen = lane.KickOpen = false;
                lane.Rock.gameObject.SetActive(false);
                lane.Marker.Hide();
                lane.Arrow.Hide();
                lane.Pieces.Initialize(lane.HangPoint, Context.Camera, lane.Attacker.Thrower, stoneKickConfig);
                lane.Ninja.Fight.ReturnToCenter();
                lane.Ninja.Fight.ShowSwirl(false);
            }
        }

        public override Pose ArrivalPose(int slot)
        {
            var spot = Spot(slot);
            return new Pose(spot.position, spot.rotation);
        }

        public override async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            Context.Storms.SetDetecting(UsesType(BlockType.SlipSpin));
            running = true;
            try
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    if (steps[i].overlapsNext && i + 1 < steps.Count)
                    {
                        await RunStepsAsync(i, i + 1, cancellationToken);
                        i++;
                    }
                    else
                    {
                        await RunStepsAsync(i, -1, cancellationToken);
                    }

                    if (AnyoneOut()) return false;
                    await WaitAsync(Context.Tuning.ThreatGapSeconds / Difficulty.speedScale, cancellationToken);
                }

                return true;
            }
            finally
            {
                running = false;
                Context.Storms.SetDetecting(false);
                foreach (var lane in lanes)
                {
                    lane.Marker.Hide();
                    lane.Arrow.Hide();
                    lane.Ninja.Status.Hide();
                    lane.Clock.EaseTo(1f, 0f);
                }
            }
        }

        public override async UniTask PlayExitAsync(LinkEntry link, CancellationToken cancellationToken)
        {
            var away = transform.forward;
            if (boss != null)
            {
                BgmManager.Instance.PlayStinger(BgmManager.StingerType.BossAppear);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, boss.Center, Quaternion.identity, 3f);
                if (link.state == LinkState.BossBroken) await boss.FleeAsync(away, 1.5f, cancellationToken);
                return;
            }

            var flee = link.state == LinkState.EnemyFled || link.moment == LinkMoment.EnemyFlees;
            var tasks = new List<UniTask>();
            foreach (var enemy in AliveEnemies())
            {
                if (flee) tasks.Add(enemy.FleeAsync(away + transform.right * (float)(random.NextDouble() - 0.5), 1.6f, cancellationToken));
                else enemy.Burst();
            }

            await UniTask.WhenAll(tasks);
        }

        public override void Finish()
        {
            foreach (var enemy in AliveEnemies()) enemy.gameObject.SetActive(false);
        }

        #endregion

        #region Threat Services

        public LocalizedString KickText(int count) => LocalizedStrings.WithArguments(kickText, ("count", count));

        public LocalizedString WiggleText => wiggleText;

        public bool RollLeap() => random.NextDouble() < Context.Tuning.LeapShare;

        /// <summary>A crowd monster that leaps at the ninja instead of a rock; null when the crowd is gone.</summary>
        public ChapterEnemy? TakeLeaper()
        {
            foreach (var minion in crowd)
            {
                if (minion.gameObject.activeSelf && minion.IsAlive) return minion;
            }

            return null;
        }

        /// <summary>A result landed: a crowd monster bursts, or the Boss loses bricks.</summary>
        public void OnThreatSuccess(ChapterFightLane lane, float strength)
        {
            if (boss != null)
            {
                BossDamage += strength;
                boss.PlayHit();
                return;
            }

            lane.Attacker.PlayHit();
            foreach (var minion in crowd)
            {
                if (!minion.gameObject.activeSelf || !minion.IsAlive) continue;
                minion.Burst();
                return;
            }
        }

        /// <returns>True when this player is out of hearts.</returns>
        public bool LoseHeart(ChapterFightLane lane)
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaHit);
            var pop = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaBrickPop, lane.Ninja.Center, Quaternion.identity);
            if (pop != null)
            {
                var main = pop.main;
                main.startColor = lane.Color;
            }

            lane.Ninja.Fight.PlayHit(fightConfig.HitRecoverSeconds);
            return Context.Hearts.Lose(lane.PlayerIndex);
        }

        public bool IsOut(ChapterFightLane lane) => Context.Hearts.IsOut(lane.PlayerIndex);

        public void BeginPin(ChapterFightLane pinned)
        {
            pinned.Pinned = true;
            foreach (var lane in lanes) lane.Frozen = lane != pinned;
        }

        public void EndPin(ChapterFightLane freed)
        {
            freed.Pinned = false;
            var anyPinned = false;
            foreach (var lane in lanes) anyPinned |= lane.Pinned;
            if (anyPinned) return;
            foreach (var lane in lanes) lane.Frozen = false;
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            foreach (var lane in lanes)
            {
                lane.Clock.Tick();
                lane.Attacker.Tick(lane.LocalDelta);
                if (running) lane.Ninja.TickFight(lane.LocalDelta);
                if (!running) continue;
                var pulse = lane.Kicks.Poll(Time.unscaledTime, stoneKickConfig.KickSettings);
                if (pulse == KickDetector.Pulse.Kick && lane.KickOpen) lane.QueuedKicks++;
            }
        }

        #endregion

        #region Helpers

        // Against the Boss the steps alternate between players; otherwise every lane gets the step at once.
        async UniTask RunStepsAsync(int first, int second, CancellationToken cancellationToken)
        {
            var tasks = new List<UniTask>();
            for (var slot = 0; slot < lanes.Count; slot++)
            {
                if (boss != null && slot != first % lanes.Count) continue;
                var lane = lanes[slot];
                if (IsOut(lane)) continue;
                tasks.Add(threats[steps[first].type].RunAsync(lane, cancellationToken));
                if (second >= 0) tasks.Add(DelayedAsync(lane, steps[second].type, cancellationToken));
            }

            await UniTask.WhenAll(tasks);
        }

        async UniTask DelayedAsync(ChapterFightLane lane, BlockType type, CancellationToken cancellationToken)
        {
            await WaitAsync(Context.Tuning.OverlapDelaySeconds * Difficulty.windowScale, cancellationToken);
            await threats[type].RunAsync(lane, cancellationToken);
        }

        bool AnyoneOut()
        {
            foreach (var lane in lanes)
            {
                if (IsOut(lane)) return true;
            }

            return false;
        }

        bool UsesType(BlockType type)
        {
            foreach (var step in steps)
            {
                if (step.type == type) return true;
            }

            return false;
        }

        IEnumerable<ChapterEnemy> AliveEnemies()
        {
            foreach (var attacker in attackers)
            {
                if (attacker.gameObject.activeSelf) yield return attacker;
            }

            foreach (var minion in crowd)
            {
                if (minion.gameObject.activeSelf) yield return minion;
            }
        }

        Transform Spot(int slot) => Context.Players.Count > 1 ? duoSpots[slot] : soloSpot;

        #endregion
    }
}
