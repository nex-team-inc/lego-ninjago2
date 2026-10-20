#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slip and Spin for one lane. Tell: the enemy cocks its staff to the danger side and an arrow in the player's color
    /// shows the safe side. Window and gesture: lean the chest to the safe side in time (hands do not count; the first
    /// threshold crossing decides). A failed slip costs a heart and gives no counter. A good slip opens the counter in
    /// this lane's slow motion: wild hands (total travel plus a speed peak) fill the Spinjitzu swirl, and when it is full
    /// the ninja spins and bricks fly off the enemy.
    /// </summary>
    public sealed class SlipSpinThreat : IFightThreat
    {
        const float restLiftDegrees = 50f;
        const float cockYawDegrees = 85f;
        const float cockLiftDegrees = 20f;

        readonly FightBlock block;

        public SlipSpinThreat(FightBlock block)
        {
            this.block = block;
        }

        #region Threat

        public async UniTask RunAsync(ChapterFightLane lane, CancellationToken cancellationToken)
        {
            var fight = block.FightConfig;
            var difficulty = block.Difficulty;
            var attacker = lane.Attacker;
            var safeSide = lane.NextSafeSide;
            lane.NextSafeSide = safeSide.Opposite();
            var cockYaw = -safeSide.Opposite().Sign() * cockYawDegrees;
            attacker.ShowStaff(true);
            lane.Arrow.ShowArrow(lane.Ninja.transform, Vector3.up * 1.3f + lane.Ninja.transform.right * (safeSide.Sign() * 0.9f), safeSide.Sign(), lane.Color);
            block.TryHint(Gesture.Slip);
            await lane.AnimateAsync(block.Context.Tuning.SwingTellSeconds * difficulty.windowScale / difficulty.speedScale, t =>
            {
                var eased = 1f - (1f - t) * (1f - t);
                attacker.Staff.SetStaff(Mathf.Lerp(0f, cockYaw, eased), Mathf.Lerp(restLiftDegrees, cockLiftDegrees, eased));
                FollowLean(lane);
            }, cancellationToken);

            var slipped = await SlipWindowAsync(lane, safeSide, cockYaw, cancellationToken);
            lane.Arrow.Hide();
            if (!slipped)
            {
                block.LoseHeart(lane);
                await lane.WaitAsync(fight.HitRecoverSeconds, cancellationToken);
                Reset(lane);
                return;
            }

            await CounterAsync(lane, cancellationToken);
            Reset(lane);
        }

        #endregion

        #region Slip

        // Real seconds: the staff reaches the center line at the end, or sooner once the player has slipped.
        async UniTask<bool> SlipWindowAsync(ChapterFightLane lane, SweepSide safeSide, float cockYaw, CancellationToken cancellationToken)
        {
            var fight = block.FightConfig;
            var attacker = lane.Attacker;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
            lane.Slip.Begin(safeSide, fight.LeanThresholdInches);
            attacker.Staff.SetTrail(true);
            var progress = 0f;
            var progressPerSecond = 1f / (fight.SlipWindowSeconds * block.Difficulty.windowScale);
            var elapsed = 0f;
            var decidedAt = 0f;
            var tracked = false;
            var outcome = SlipDetector.Outcome.Pending;
            while (progress < 1f)
            {
                var deltaTime = lane.RealDelta;
                elapsed += deltaTime;
                if (outcome == SlipDetector.Outcome.Pending && !lane.Frozen && lane.Body.TryGetLeanInches(out var lean))
                {
                    tracked = true;
                    outcome = lane.Slip.Update(lean.x);
                    lane.Ninja.Fight.SetLeanHint(lean.x / fight.LeanThresholdInches);
                    if (outcome != SlipDetector.Outcome.Pending)
                    {
                        decidedAt = elapsed;
                        lane.Ninja.Fight.SlipTo(outcome == SlipDetector.Outcome.Slipped ? safeSide : safeSide.Opposite());
                        if (outcome == SlipDetector.Outcome.Slipped)
                        {
                            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaSlip);
                            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaDustKick, lane.Ninja.FeetPosition);
                            progressPerSecond = (1f - progress) / fight.SlipResolveSeconds;
                        }
                    }
                }

                progress = Mathf.Min(1f, progress + deltaTime * progressPerSecond);
                attacker.Staff.SetStaff(Mathf.Lerp(cockYaw, 0f, progress * progress), Mathf.Lerp(cockLiftDegrees, 0f, progress));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            attacker.Staff.SetTrail(false);
            if (outcome == SlipDetector.Outcome.Slipped)
            {
                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Slip, true, decidedAt);
                attacker.Staff.SetStance(1f);
                return true;
            }

            var reason = outcome == SlipDetector.Outcome.WrongWay ? "wrong_way" : tracked ? "no_lean" : "not_tracked";
            block.Context.Log.Qte(lane.PlayerIndex, Gesture.Slip, false, -1f, reason);
            return false;
        }

        #endregion

        #region Counter

        async UniTask CounterAsync(ChapterFightLane lane, CancellationToken cancellationToken)
        {
            var fight = block.FightConfig;
            var storms = block.Context.Storms;
            lane.Clock.EaseTo(fight.SlowMotionScale, fight.SlowMotionEaseSeconds);
            var meter = storms.Begin(lane.Body);
            block.TryHint(Gesture.Spin);
            lane.Ninja.Fight.ShowSwirl(true);
            var swirl = PlayTinted(VfxManager.VisualEffect.SpinjitzuSwirl, lane.Ninja.FeetPosition, lane.Color);
            var window = fight.CounterWindowSeconds * block.Difficulty.windowScale;
            var elapsed = 0f;
            while (elapsed < window && !meter.IsFilled)
            {
                elapsed += lane.RealDelta;
                lane.Ninja.Fight.SetSwirlFill(meter.Fill01);
                if (swirl != null)
                {
                    var main = swirl.main;
                    main.simulationSpeed = lane.Clock.Scale;
                    var emission = swirl.emission;
                    emission.rateOverTimeMultiplier = Mathf.Lerp(6f, 90f, meter.Fill01);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            storms.End(lane.PlayerIndex);
            lane.Clock.EaseTo(1f, fight.SlowMotionEaseSeconds);
            if (swirl != null)
            {
                // Pooled: the next player to get this instance must not inherit the slow motion.
                var main = swirl.main;
                main.simulationSpeed = 1f;
                swirl.Stop();
            }

            lane.Ninja.Fight.ShowSwirl(false);
            if (!meter.IsFilled)
            {
                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Spin, false, -1f, "too_little");
                await lane.WaitAsync(fight.WhiffRecoverSeconds, cancellationToken);
                return;
            }

            block.Context.Log.Qte(lane.PlayerIndex, Gesture.Spin, true, elapsed);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaSpin);
            PlayTinted(VfxManager.VisualEffect.SpinjitzuBurst, lane.Ninja.FeetPosition, lane.Color);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BruteBrickSpray, lane.Attacker.Center);
            lane.Ninja.Fight.PlaySpin(fight.SpinSeconds);
            block.OnThreatSuccess(lane, 1f);
            await lane.WaitAsync(fight.SpinSeconds, cancellationToken);
        }

        #endregion

        #region Helpers

        void FollowLean(ChapterFightLane lane)
        {
            if (lane.Body.TryGetLeanInches(out var lean)) lane.Ninja.Fight.SetLeanHint(lean.x / block.FightConfig.LeanThresholdInches);
        }

        static void Reset(ChapterFightLane lane)
        {
            lane.Ninja.Fight.ReturnToCenter();
            lane.Attacker.Staff.SetStance(0f);
            lane.Attacker.Staff.SetStaff(0f, restLiftDegrees);
            lane.Attacker.ShowStaff(false);
        }

        static ParticleSystem? PlayTinted(VfxManager.VisualEffect effect, Vector3 position, Color color)
        {
            var system = VfxManager.Instance.PlayVisualEffect(effect, position, Quaternion.identity);
            if (system == null) return null;
            var main = system.main;
            main.startColor = color;
            return system;
        }

        #endregion
    }
}
