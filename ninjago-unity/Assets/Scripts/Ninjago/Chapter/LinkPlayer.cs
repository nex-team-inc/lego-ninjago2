#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Plays one link between two blocks: a moment in the world that covers the swap (a roar, dust, a brick burst, the
    /// brick rebuild, a landing), the camera move into the next station's view, the one-line story beat and a stinger
    /// over the level's music. The ninjas leap to the next station while the camera moves; between two phases in the same
    /// station (the Boss arena) they stay put and only the camera, the moment and the story line play.
    /// </summary>
    public sealed class LinkPlayer
    {
        readonly ChapterStage stage;
        readonly ChapterTuning tuning;
        readonly ChapterLog log;

        public LinkPlayer(ChapterStage stage, ChapterTuning tuning, ChapterLog log)
        {
            this.stage = stage;
            this.tuning = tuning;
            this.log = log;
        }

        #region Public API

        /// <summary>Plays the link; the next block is staged at its start and the ninjas land on it as the camera settles.</summary>
        public async UniTask PlayAsync(ChapterBlock previous, ChapterBlock next, LinkEntry link, CancellationToken cancellationToken)
        {
            log.LinkStart(link);
            var seconds = tuning.LinkSeconds;
            if (!string.IsNullOrEmpty(link.storyKey)) stage.Hud.ShowCaption(new LocalizedString(ForgeConfig.TableName, link.storyKey), tuning.CaptionSeconds);
            BgmManager.Instance.PlayStinger(link.stinger);
            var exit = previous.PlayExitAsync(link, cancellationToken);
            PlayMoment(link.moment, previous.FocusPoint, next.FocusPoint, DustScale(previous.FocusPoint));
            next.Stage();
            var stays = previous == next;
            var tasks = new List<UniTask>
            {
                exit,
                stage.MoveCameraAsync(previous, next, link.cameraMove, seconds, cancellationToken),
                RebuildAsync(link.moment, next.FocusPoint, seconds * 0.45f, cancellationToken),
            };
            if (!stays) tasks.Add(LeapAsync(next, seconds - tuning.TellLeadSeconds, cancellationToken));
            await UniTask.WhenAll(tasks);
            if (!stays) previous.Finish();
            log.LinkEnd();
        }

        /// <summary>The soft "try again" beat in the same scene: the ninjas burst into bricks and snap back at the block's start.</summary>
        public async UniTask TryAgainAsync(ChapterBlock block, LocalizedString line, CancellationToken cancellationToken)
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Bonk);
            foreach (var ninja in stage.Ninjas)
            {
                ninja.Status.Show(line, ninja.Color, tuning.TryAgainSeconds);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, ninja.Center);
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(tuning.TryAgainSeconds * 0.5f), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            block.Stage();
            for (var slot = 0; slot < stage.Ninjas.Count; slot++)
            {
                var pose = block.ArrivalPose(slot);
                stage.Ninjas[slot].SetVisible(true);
                stage.Ninjas[slot].Place(pose.position, pose.rotation);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickRebuild, stage.Ninjas[slot].Center);
            }

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BrickSnap);
            await UniTask.Delay(System.TimeSpan.FromSeconds(tuning.TryAgainSeconds * 0.5f), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion

        #region Helpers

        async UniTask LeapAsync(ChapterBlock next, float seconds, CancellationToken cancellationToken)
        {
            var leaps = new List<UniTask>();
            for (var slot = 0; slot < stage.Ninjas.Count; slot++)
            {
                var ninja = stage.Ninjas[slot];
                ninja.SetVisible(true);
                var pose = next.ArrivalPose(slot);
                leaps.Add(ninja.LeapToAsync(pose.position, pose.rotation, Mathf.Max(0.5f, seconds), tuning.LeapHeight, cancellationToken));
            }

            await UniTask.WhenAll(leaps);
        }

        // The brick rebuild: the bricks of the defeated fly together into the next threat or vehicle.
        static async UniTask RebuildAsync(LinkMoment moment, Vector3 target, float delay, CancellationToken cancellationToken)
        {
            if (moment != LinkMoment.BrickRebuild) return;
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BrickRebuild, target, Quaternion.identity, 2.5f);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BrickSnap);
        }

        // A takeoff right in front of the camera (leaving the ride) gets a small puff, not a cloud over the screen.
        float DustScale(Vector3 at) => Mathf.Clamp(Vector3.Distance(at, stage.WorldCamera.transform.position) * 0.15f, 0.6f, 2f);

        static void PlayMoment(LinkMoment moment, Vector3 from, Vector3 to, float fromDustScale)
        {
            var vfx = VfxManager.Instance;
            var sfx = SfxManager.Instance;
            switch (moment)
            {
                case LinkMoment.BrickRebuild:
                case LinkMoment.BrickBurst:
                    vfx.PlayVisualEffect(VfxManager.VisualEffect.BrickBurst, from, Quaternion.identity, 2.5f);
                    sfx.PlaySoundEffect(SfxManager.SoundEffect.PieceHit);
                    break;
                case LinkMoment.BossRoar:
                    vfx.PlayVisualEffect(VfxManager.VisualEffect.SlamShockwave, from, Quaternion.identity, 3f);
                    sfx.PlaySoundEffect(SfxManager.SoundEffect.TeammateSlam);
                    break;
                case LinkMoment.VehicleArrives:
                    vfx.PlayVisualEffect(VfxManager.VisualEffect.LinkDust, to, Quaternion.identity, 2f);
                    sfx.PlaySoundEffect(SfxManager.SoundEffect.VehicleBump);
                    break;
                case LinkMoment.EnemyFlees:
                    vfx.PlayVisualEffect(VfxManager.VisualEffect.LinkDust, from, Quaternion.identity, fromDustScale);
                    sfx.PlaySoundEffect(SfxManager.SoundEffect.Spotted);
                    break;
                default:
                    vfx.PlayVisualEffect(VfxManager.VisualEffect.LinkDust, from, Quaternion.identity, fromDustScale);
                    sfx.PlaySoundEffect(SfxManager.SoundEffect.NinjaSlip);
                    break;
            }
        }

        #endregion
    }
}
