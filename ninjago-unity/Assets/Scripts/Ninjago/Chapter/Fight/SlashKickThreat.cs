#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slash and Kick for one lane. Tell: INCOMING! while the enemy winds up to throw, or a crowd monster crouches to
    /// leap. Window: the rock or monster hangs in slow motion in front of the ninja with a draining ring. Gesture: a
    /// slashing frenzy — the first fast stroke in any direction starts it, and every stroke until it ends chips the rock
    /// and cuts one more stone, up to the most stones. Result: it bursts into those stones and ONE kick sends them all
    /// back into the enemy; no kick and they drop. A missed rock costs a heart; a missed monster pins the ninja, freezing
    /// the other lanes, and drains hearts until the player shakes both hands fast enough to throw it off.
    /// </summary>
    public sealed class SlashKickThreat : IFightThreat
    {
        // Above the ninja's player tag from every fight camera.
        const float promptHeight = 1.6f;
        const float pinPromptHeight = 3f;
        const float pinJumpSeconds = 0.35f;
        const float popSeconds = 0.18f;
        const float smallestScale = 0.55f;

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
            block.Calls.Incoming(lane, leaper != null, PromptPoint(lane));
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

            var slashes = await SlashFrenzyAsync(lane, thrown, hang, cancellationToken);
            if (slashes == 0)
            {
                FightCalls.Clear(lane);
                if (leaper != null) await PinAsync(lane, leaper, cancellationToken);
                else await RockHitAsync(lane, cancellationToken);
                return;
            }

            var stones = block.StoneKickConfig.StonesFor(slashes);
            block.Context.Hints.Learned(Gesture.Slash);
            block.Context.Log.SlashFrenzy(lane.PlayerIndex, slashes, stones);
            var burstPoint = leaper != null ? leaper.Center : lane.Rock.position;
            if (leaper != null) leaper.Burst();
            else lane.Rock.gameObject.SetActive(false);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, burstPoint);
            await lane.Pieces.SplitAsync(burstPoint, stones, cancellationToken);
            await KickAsync(lane, stones, cancellationToken);
        }

        #endregion

        #region Window

        // Real seconds, so the slow motion slows the rock, not the player's chance.
        async UniTask<int> SlashFrenzyAsync(ChapterFightLane lane, Transform thrown, Vector3 hang, CancellationToken cancellationToken)
        {
            var tuning = block.Context.Tuning;
            var stoneKick = block.StoneKickConfig;
            var restScale = thrown.localScale;
            lane.Clock.EaseTo(tuning.HangTimeScale, 0.1f);
            lane.Marker.ShowRing(thrown, lane.Color);
            block.Calls.Slash(lane, 0, PromptPoint(lane));
            lane.Slash.Begin(lane.Body);
            var window = tuning.HangSeconds * block.Difficulty.windowScale;
            var elapsed = 0f;
            var drift = 0f;
            var slashes = 0;
            var frenzyLeft = 0f;
            var firstSlash = -1f;
            var poppedAt = float.NegativeInfinity;
            try
            {
                while (slashes == 0 ? elapsed < window : frenzyLeft > 0f)
                {
                    var deltaTime = lane.RealDelta;
                    elapsed += deltaTime;
                    if (slashes > 0) frenzyLeft -= deltaTime;
                    drift += lane.LocalDelta;
                    lane.Marker.SetFill(slashes == 0 ? 1f - elapsed / window : frenzyLeft / stoneKick.SlashFrenzySeconds);
                    var strokes = lane.Frozen ? 0 : lane.Slash.Poll(lane.Body, stoneKick.SlashSpeedInchesPerSecond, tuning.SlashStrokeGapSeconds);
                    for (var i = 0; i < strokes && stoneKick.StonesFor(slashes) < stoneKick.MaxStones; i++)
                    {
                        slashes++;
                        OnSlash(thrown, slashes);
                    }

                    if (strokes > 0)
                    {
                        if (firstSlash < 0f)
                        {
                            firstSlash = elapsed;
                            frenzyLeft = stoneKick.SlashFrenzySeconds;
                        }

                        poppedAt = Time.unscaledTime;
                        block.Calls.Slash(lane, slashes, PromptPoint(lane));
                    }

                    var pop = 1f - Mathf.Clamp01((Time.unscaledTime - poppedAt) / popSeconds);
                    var carved = stoneKick.StonesFor(slashes) / (float)stoneKick.MaxStones;
                    thrown.localScale = restScale * (Mathf.Lerp(1f, smallestScale, carved) * (1f + 0.2f * pop));
                    thrown.position = hang + new Vector3(Mathf.Sin(Time.unscaledTime * 90f) * 0.08f * pop, Mathf.Sin(drift * 2.4f) * 0.08f, -drift * 0.35f);
                    thrown.Rotate(new Vector3(70f, 35f, 0f) * lane.LocalDelta, Space.Self);
                    if (stoneKick.StonesFor(slashes) >= stoneKick.MaxStones) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                block.Context.Log.Qte(lane.PlayerIndex, Gesture.Slash, slashes > 0, firstSlash, slashes > 0 ? "" : "no_slash");
                return slashes;
            }
            finally
            {
                thrown.localScale = restScale;
                lane.Marker.Hide();
                lane.Clock.EaseTo(1f, 0.1f);
            }
        }

        // Every slash: a rising slash sound, a streak at a new angle, stone chips and the count.
        void OnSlash(Transform thrown, int slashes)
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.RockSlash, 1f + 0.06f * Mathf.Min(slashes - 1, 8));
            var facing = block.Context.Camera.transform.rotation;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockSlash, thrown.position, facing * Quaternion.Euler(0f, 0f, slashes * 67f));
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, thrown.position);
        }

        async UniTask KickAsync(ChapterFightLane lane, int stones, CancellationToken cancellationToken)
        {
            lane.QueuedKicks = 0;
            lane.KickOpen = true;
            block.Calls.Kick(lane, stones, PromptPoint(lane));
            var window = block.Context.Tuning.KickWindowSeconds * block.Difficulty.windowScale;
            var elapsed = 0f;
            while (elapsed < window && lane.QueuedKicks == 0)
            {
                elapsed += lane.RealDelta;
                lane.Pieces.Bob();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            var kicked = lane.QueuedKicks > 0;
            lane.KickOpen = false;
            lane.QueuedKicks = 0;
            block.Context.Log.Qte(lane.PlayerIndex, Gesture.Kick, kicked, kicked ? elapsed : -1f, kicked ? "" : "no_kick");
            if (!kicked)
            {
                FightCalls.Clear(lane);
                lane.Pieces.DropAll(cancellationToken);
                await lane.WaitAsync(block.StoneKickConfig.PieceDropSeconds, cancellationToken);
                return;
            }

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.KickThud);
            block.Context.Hints.Learned(Gesture.Kick);
            block.Calls.Smash(lane, PromptPoint(lane));
            await lane.WaitAsync(lane.Pieces.LaunchAll(cancellationToken), cancellationToken);
            block.OnThreatSuccess(lane);
        }

        static Vector3 PromptPoint(ChapterFightLane lane) => lane.HangPoint.position + Vector3.up * promptHeight;

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

        // The monster lands on the ninja; the other lanes freeze and this player's hearts drain until both hands shake
        // fast: the hands' summed travel over the last burst seconds must reach the pin travel.
        async UniTask PinAsync(ChapterFightLane lane, ChapterEnemy monster, CancellationToken cancellationToken)
        {
            var tuning = block.Context.Tuning;
            var from = monster.transform.position;
            await lane.AnimateAsync(pinJumpSeconds, t => monster.transform.position = Vector3.Lerp(from, lane.Ninja.FeetPosition + Vector3.up * 1.1f, t),
                cancellationToken);
            block.BeginPin(lane);
            block.Context.Log.Pinned(lane.PlayerIndex);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Pin);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PinSlam, lane.Ninja.FeetPosition);
            block.Calls.Pinned(lane, lane.Ninja.FeetPosition + Vector3.up * pinPromptHeight);
            lane.Ninja.Fight.ShowSwirl(true);
            var storms = block.Context.Storms;
            var meter = storms.Begin(lane.Body);
            var history = new Queue<Vector2>();
            var pinnedAt = Time.unscaledTime;
            var heartTimer = 0f;
            var freed = false;
            try
            {
                while (true)
                {
                    var now = Time.unscaledTime;
                    monster.transform.position = lane.Ninja.FeetPosition + Vector3.up * 1.1f + Vector3.right * (Mathf.Sin(now * 18f) * 0.05f);
                    history.Enqueue(new Vector2(now, meter.TravelInches));
                    while (history.Peek().x < now - tuning.PinBurstSeconds) history.Dequeue();
                    var fill = Mathf.Clamp01((meter.TravelInches - history.Peek().y) / tuning.PinTravelInches);
                    lane.Ninja.Fight.SetSwirlFill(fill);
                    if (fill >= 1f)
                    {
                        freed = true;
                        break;
                    }

                    heartTimer += Time.unscaledDeltaTime;
                    if (heartTimer >= tuning.PinHeartSeconds)
                    {
                        heartTimer = 0f;
                        if (block.LoseHeart(lane)) break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            finally
            {
                storms.End(lane.PlayerIndex);
                lane.Ninja.Fight.ShowSwirl(false);
                FightCalls.Clear(lane);
            }

            monster.Burst();
            if (freed)
            {
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.FlyAway);
                block.Context.Log.ShookFree(lane.PlayerIndex, Time.unscaledTime - pinnedAt);
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
