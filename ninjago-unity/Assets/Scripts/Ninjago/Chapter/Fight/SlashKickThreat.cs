#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slash and Kick for one lane. Tell: the enemy winds up to throw, or a crowd monster crouches to leap. Window: the
    /// rock or monster hangs in slow motion in front of the ninja with a draining ring in the player's color. Gesture: a
    /// slash (a hand crossing the shoulder line fast). Result: it splits, KICK and a count appear, and every knee pulse
    /// launches one stone back into the enemy; leftovers drop. A missed rock costs a heart; a missed monster pins the
    /// ninja, freezing the other lanes, and drains hearts until the player wiggles their hips free.
    /// </summary>
    public sealed class SlashKickThreat : IFightThreat
    {
        readonly FightBlock block;

        public SlashKickThreat(FightBlock block)
        {
            this.block = block;
        }

        #region Threat

        public async UniTask RunAsync(ChapterFightLane lane, CancellationToken cancellationToken)
        {
            var tuning = block.Context.Tuning;
            var difficulty = block.Difficulty;
            var leaper = block.RollLeap() ? block.TakeLeaper() : null;
            var tell = tuning.ThrowTellSeconds / difficulty.speedScale;
            Transform thrown;
            Vector3 from;
            if (leaper != null)
            {
                leaper.SetOwner(lane.PlayerIndex, lane.Color);
                var crouchFrom = leaper.transform.localScale;
                await lane.AnimateAsync(tell, t => leaper.transform.localScale = new Vector3(crouchFrom.x, crouchFrom.y * (1f - 0.35f * Mathf.Sin(t * Mathf.PI)), crouchFrom.z),
                    cancellationToken);
                thrown = leaper.transform;
                from = leaper.transform.position;
            }
            else
            {
                lane.Attacker.Thrower.PlayThrow(tell);
                await lane.WaitAsync(tell, cancellationToken);
                thrown = lane.Rock;
                from = lane.Attacker.HandPosition;
                lane.Rock.position = from;
                lane.Rock.gameObject.SetActive(true);
            }

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
            var hang = lane.HangPoint.position - (leaper != null ? leaper.Center - leaper.transform.position : Vector3.zero);
            await lane.AnimateAsync(tuning.ThrowFlightSeconds / difficulty.speedScale, t =>
            {
                var eased = 1f - (1f - t) * (1f - t);
                thrown.position = Vector3.Lerp(from, hang, eased) + Vector3.up * (1.4f * 4f * eased * (1f - eased));
            }, cancellationToken);

            var slashed = await SlashWindowAsync(lane, thrown, hang, cancellationToken);
            if (!slashed)
            {
                if (leaper != null) await PinAsync(lane, leaper, cancellationToken);
                else await RockHitAsync(lane, cancellationToken);
                return;
            }

            var burstPoint = leaper != null ? leaper.Center : lane.Rock.position;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.RockSlash);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockSlash, burstPoint, block.Context.Camera.transform.rotation);
            if (leaper != null) leaper.Burst();
            else lane.Rock.gameObject.SetActive(false);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, burstPoint);
            await lane.Pieces.SplitAsync(burstPoint, tuning.StonesPerRock, cancellationToken);
            await KickWindowAsync(lane, tuning.StonesPerRock, cancellationToken);
        }

        #endregion

        #region Window

        // Real seconds, so the slow motion slows the rock, not the player's chance.
        async UniTask<bool> SlashWindowAsync(ChapterFightLane lane, Transform thrown, Vector3 hang, CancellationToken cancellationToken)
        {
            var tuning = block.Context.Tuning;
            var stoneKick = block.StoneKickConfig;
            lane.Clock.EaseTo(tuning.HangTimeScale, 0.1f);
            lane.Marker.ShowRing(thrown, lane.Color);
            block.TryHint(Gesture.Slash);
            lane.Slash.Begin(lane.Body);
            var window = tuning.HangSeconds * block.Difficulty.windowScale;
            var elapsed = 0f;
            var drift = 0f;
            try
            {
                while (elapsed < window)
                {
                    elapsed += lane.RealDelta;
                    drift += lane.LocalDelta;
                    lane.Marker.SetFill(1f - elapsed / window);
                    thrown.position = hang + new Vector3(0f, Mathf.Sin(drift * 2.4f) * 0.08f, -drift * 0.35f);
                    thrown.Rotate(new Vector3(70f, 35f, 0f) * lane.LocalDelta, Space.Self);
                    if (!lane.Frozen && lane.Slash.Poll(lane.Body, stoneKick.SlashSpeedInchesPerSecond, stoneKick.SlashMaxCrossSeconds, tuning.SlashRadiusShoulders))
                    {
                        block.Context.Log.Qte(lane.PlayerIndex, Gesture.Slash, true, elapsed);
                        return true;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Slash, false, -1f, "no_slash");
                return false;
            }
            finally
            {
                lane.Marker.Hide();
                lane.Clock.EaseTo(1f, 0.1f);
            }
        }

        async UniTask KickWindowAsync(ChapterFightLane lane, int stones, CancellationToken cancellationToken)
        {
            lane.QueuedKicks = 0;
            lane.KickOpen = true;
            block.TryHint(Gesture.Kick);
            var countPoint = lane.HangPoint.position + Vector3.up * 1.1f;
            lane.Ninja.Status.Show(block.KickText(stones), lane.Color, 0f, null, countPoint);
            var window = block.Context.Tuning.KickWindowSeconds * block.Difficulty.windowScale;
            var elapsed = 0f;
            var launched = 0;
            var firstKick = -1f;
            while (elapsed < window && lane.Pieces.Remaining > 0)
            {
                elapsed += lane.RealDelta;
                while (lane.QueuedKicks > 0 && lane.Pieces.Remaining > 0)
                {
                    lane.QueuedKicks--;
                    lane.Pieces.LaunchNext(cancellationToken);
                    launched++;
                    if (firstKick < 0f) firstKick = elapsed;
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.KickThud, 1f + 0.08f * launched);
                    lane.Ninja.Status.Show(block.KickText(lane.Pieces.Remaining), lane.Color, 0f, null, countPoint);
                }

                lane.Pieces.Bob();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            lane.KickOpen = false;
            lane.Ninja.Status.Hide();
            if (lane.Pieces.Remaining > 0) lane.Pieces.DropRemaining(cancellationToken);
            var reason = launched == 0 ? "no_kick" : launched < stones ? "few_kicks" : "";
            block.Context.Log.Qte(lane.PlayerIndex, Gesture.Kick, launched == stones, firstKick, reason);
            if (launched > 0) block.OnThreatSuccess(lane, launched / (float)stones);
            await lane.WaitAsync(block.StoneKickConfig.PieceFlySeconds, cancellationToken);
        }

        #endregion

        #region Misses

        async UniTask RockHitAsync(ChapterFightLane lane, CancellationToken cancellationToken)
        {
            var from = lane.Rock.position;
            var to = lane.Ninja.Center;
            await lane.AnimateAsync(block.StoneKickConfig.MissFlySeconds, t => lane.Rock.position = Vector3.Lerp(from, to, t * t), cancellationToken);
            lane.Rock.gameObject.SetActive(false);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, to);
            block.LoseHeart(lane);
            await lane.WaitAsync(block.StoneKickConfig.HitRecoverSeconds, cancellationToken);
        }

        // The monster lands on the ninja; the other lanes freeze and this player's hearts drain until the hips wiggle free.
        async UniTask PinAsync(ChapterFightLane lane, ChapterEnemy monster, CancellationToken cancellationToken)
        {
            var onePunch = block.OnePunchConfig;
            var from = monster.transform.position;
            await lane.AnimateAsync(onePunch.PinJumpSeconds, t => monster.transform.position = Vector3.Lerp(from, lane.Ninja.FeetPosition + Vector3.up * 1.1f, t),
                cancellationToken);
            block.BeginPin(lane);
            block.Context.Log.Pinned(lane.PlayerIndex);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Pin);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PinSlam, lane.Ninja.FeetPosition);
            lane.Ninja.Status.Show(block.WiggleText, lane.Color);
            lane.Wiggle.Reset();
            var pinnedAt = Time.unscaledTime;
            var heartTimer = 0f;
            var freed = false;
            while (true)
            {
                var deltaTime = Time.unscaledDeltaTime;
                monster.transform.position = lane.Ninja.FeetPosition + Vector3.up * 1.1f + Vector3.right * (Mathf.Sin(Time.unscaledTime * 18f) * 0.05f);
                if (lane.Body.TryGetHipInches(out var hips)) lane.Wiggle.Add(hips, Time.unscaledTime, onePunch.WiggleWindowSeconds);
                if (lane.Wiggle.Tick(Time.unscaledTime, deltaTime, onePunch.WiggleWindowSeconds, onePunch.WigglePathInches, onePunch.WiggleRadiusInches,
                        onePunch.WiggleFillSeconds))
                {
                    freed = true;
                    break;
                }

                heartTimer += deltaTime;
                if (heartTimer >= block.Context.Tuning.PinHeartSeconds)
                {
                    heartTimer = 0f;
                    if (block.LoseHeart(lane)) break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            lane.Ninja.Status.Hide();
            monster.Burst();
            if (freed)
            {
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.FlyAway);
                block.Context.Log.WiggleFree(lane.PlayerIndex, Time.unscaledTime - pinnedAt);
                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Wiggle, true, Time.unscaledTime - pinnedAt);
            }
            else
            {
                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Wiggle, false, -1f, "out_of_hearts");
            }

            block.EndPin(lane);
        }

        #endregion
    }
}
