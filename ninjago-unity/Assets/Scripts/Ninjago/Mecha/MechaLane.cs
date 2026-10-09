#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's Robot Mecha world: the courtyard, the mecha on the center mark and a camera behind it that turns with
    /// the mecha and renders into that player's screen region. Runs that player's copy of the wave: while both of that
    /// player's arms hold the L the hands steer the mecha, the laser always burns the nearest enemy in front of it, and
    /// rushers and arrows cost hearts.
    /// </summary>
    public class MechaLane : MonoBehaviour
    {
        [Header("Mecha")]
        [SerializeField] MechaRig mecha = null!;
        [Header("Field")]
        [SerializeField] MechaField field = null!;
        [Header("Camera Pivot")]
        [Tooltip("On the center mark; turns with the mecha's yaw, never its pitch.")]
        [SerializeField] Transform cameraPivot = null!;
        [Header("Lane Camera")]
        [SerializeField] Camera laneCamera = null!;
        [Header("Solo Field Of View")]
        [SerializeField] float soloFieldOfView = 58f;
        [Header("Split Field Of View")]
        [SerializeField] float splitFieldOfView = 72f;

        readonly PoseLatch latch = new();
        PlayerBody body = null!;
        RobotArmsTracker arms = null!;
        RobotArmsConfig armsConfig = null!;
        MechaFightConfig config = null!;
        MechaHud hud = null!;
        TurnFollower yaw;
        TurnFollower pitch;
        ParticleSystem? muzzleEffect;
        ParticleSystem? impactEffect;
        float waveTime;
        float poseGainedAt;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        public int Hearts { get; private set; }
        public int HeartsLost { get; private set; }
        public int Kills { get; private set; }
        public bool IsDown { get; private set; }
        public float SurvivedSeconds { get; private set; }
        /// <summary>Seconds of the survived time both arms held the L.</summary>
        public float PoseSeconds { get; private set; }
        public int PosePercent => SurvivedSeconds > 0f ? Mathf.RoundToInt(100f * PoseSeconds / SurvivedSeconds) : 0;

        #region Initialization

        public void Initialize(PlayerBody aBody, RobotArmsTracker aArms, RobotArmsConfig aArmsConfig, MechaFightConfig aConfig, MechaHud aHud,
            RenderTexture target, bool splitScreen)
        {
            body = aBody;
            arms = aArms;
            armsConfig = aArmsConfig;
            config = aConfig;
            hud = aHud;
            laneCamera.targetTexture = target;
            laneCamera.fieldOfView = splitScreen ? splitFieldOfView : soloFieldOfView;
            mecha.Initialize(body.PlayerIndex, body.Color);
            field.Initialize(config);
            field.MechaHit += TakeHit;
            Hearts = config.Hearts;
            hud.Initialize(body.PlayerIndex, body.Color, Hearts);
            hud.SetTimeLeft(Mathf.CeilToInt(config.WaveSeconds));
            ApplyAim();
        }

        void OnDestroy()
        {
            StopEffects();
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            mecha.Tick(Time.deltaTime);
        }

        #endregion

        #region Wave

        /// <summary>Plays this player's wave until it is survived or the mecha is down.</summary>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            hud.ShowPrompt(true);
            muzzleEffect = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.LaserMuzzle, mecha.MuzzlePosition, Quaternion.identity);
            impactEffect = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.LaserImpact, mecha.MuzzlePosition, Quaternion.identity);
            while (waveTime < config.WaveSeconds && !IsDown)
            {
                var deltaTime = Time.deltaTime;
                waveTime += deltaTime;
                if (waveTime >= config.PromptSeconds) hud.ShowPrompt(false);
                UpdatePose(deltaTime);
                UpdateAim(deltaTime);
                field.Tick(waveTime, deltaTime);
                if (!IsDown) FireBeam(deltaTime);
                field.PointEdgeArrows(hud.EdgeArrows, laneCamera);
                hud.SetTimeLeft(Mathf.CeilToInt(Mathf.Max(0f, config.WaveSeconds - waveTime)));
                PrintDebug();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            Finish();
        }

        void Finish()
        {
            SurvivedSeconds = Mathf.Min(waveTime, config.WaveSeconds);
            StopEffects();
            mecha.SetBeam(false, 0f, 0f);
            field.Clear();
            hud.EdgeArrows.Begin();
            hud.EdgeArrows.End();
            if (IsDown)
            {
                hud.ShowDown();
            }
            else
            {
                hud.ShowSurvived();
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            }

            MechaAnalytics.WaveEnd(PlayerIndex, SurvivedSeconds, IsDown ? "down" : "survived", Kills, HeartsLost, PosePercent);
        }

        #endregion

        #region Pose And Aim

        void UpdatePose(float deltaTime)
        {
            var reading = arms.Get(PlayerIndex);
            hud.SetArms(reading.leftArmL, reading.rightArmL);
            var change = latch.Update(reading.BothArmsL, waveTime, armsConfig.PoseGainSeconds, armsConfig.PoseLossSeconds);
            if (change == PoseLatch.Change.Gained)
            {
                poseGainedAt = waveTime;
                mecha.SetPowered(true);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.MechaPowerUp, mecha.transform.position);
                MechaAnalytics.PoseGained(PlayerIndex, waveTime, yaw.Value);
            }
            else if (change == PoseLatch.Change.Lost)
            {
                // Steering cuts off: the mecha stays facing wherever it was.
                yaw.Stop();
                pitch.Stop();
                mecha.SetPowered(false);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.MechaPowerDown, mecha.MuzzlePosition);
                MechaAnalytics.PoseLost(PlayerIndex, waveTime, waveTime - poseGainedAt, LossReason(reading));
            }

            if (latch.Held) PoseSeconds += deltaTime;
        }

        void UpdateAim(float deltaTime)
        {
            if (latch.Held && arms.TryGetAimInches(body, out var inches))
            {
                var desired = MechaAim.Desired(inches, armsConfig.AimSettings);
                yaw.Step(desired.x, armsConfig.YawSpeed, armsConfig.TurnAcceleration, deltaTime);
                pitch.Step(desired.y, armsConfig.PitchSpeed, armsConfig.TurnAcceleration, deltaTime);
            }

            pitch.Value = Mathf.Clamp(pitch.Value, -armsConfig.PitchClampDegrees, armsConfig.PitchClampDegrees);
            ApplyAim();
        }

        void ApplyAim()
        {
            mecha.SetAim(yaw.Value, pitch.Value);
            cameraPivot.localRotation = Quaternion.Euler(0f, yaw.Value, 0f);
        }

        static string LossReason(RobotArmsTracker.Reading reading)
        {
            if (!reading.armsTracked) return "not_tracked";
            if (!reading.leftArmL && !reading.rightArmL) return "both_arms";
            return reading.leftArmL ? "right_arm" : "left_arm";
        }

        #endregion

        #region Beam

        // The beam never stops: it burns the nearest enemy inside it, else it runs to the ground or its full reach.
        void FireBeam(float deltaTime)
        {
            var origin = mecha.MuzzlePosition;
            var direction = mecha.MuzzleForward;
            var reach = MechaBeam.GroundDistance(origin, direction, config.BeamRange);
            var target = field.FindBeamTarget(origin, direction, reach, config.BeamWidth * 0.5f, out var length);
            mecha.SetBeam(true, length, config.BeamWidth);
            if (muzzleEffect != null) muzzleEffect.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            if (impactEffect != null)
            {
                impactEffect.transform.position = origin + direction * length;
                var emission = impactEffect.emission;
                emission.enabled = target != null || length < config.BeamRange;
            }

            if (target == null || !target.Damage(config.BeamDamagePerSecond * deltaTime)) return;
            var type = target.Type;
            var cancelledArrow = field.Break(target);
            Kills++;
            hud.SetKills(Kills);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.EnemyExplosion);
            MechaAnalytics.Kill(PlayerIndex, waveTime, type, Kills, cancelledArrow);
        }

        void StopEffects()
        {
            if (muzzleEffect != null) muzzleEffect.Stop();
            if (impactEffect != null)
            {
                // Pooled: the next user of this instance must start with its emission on.
                var emission = impactEffect.emission;
                emission.enabled = true;
                impactEffect.Stop();
            }

            muzzleEffect = null;
            impactEffect = null;
        }

        #endregion

        #region Hearts

        void TakeHit(string source)
        {
            if (IsDown) return;
            Hearts--;
            HeartsLost++;
            hud.SetHearts(Hearts);
            hud.FlashHit();
            mecha.PlayHit();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.MechaHit, mecha.MuzzlePosition);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.MechaHit);
            MechaAnalytics.Damage(PlayerIndex, waveTime, source, Hearts);
            if (Hearts > 0) return;
            IsDown = true;
            latch.Release();
            mecha.SetDown();
            MechaAnalytics.Down(PlayerIndex, waveTime, Kills);
        }

        #endregion

        #region Debug

        void PrintDebug()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!PlayerDataManager.Instance.DebugSettings.enableDebugPrinter) return;
            var reading = arms.Get(PlayerIndex);
            var reference = arms.ElbowReference(PlayerIndex);
            arms.TryGetAimInches(body, out var inches);
            DebugPrinter.Instance.Print($"Mecha P{PlayerIndex + 1}",
                $"elbows {reading.leftElbowAngle:0}/{reading.rightElbowAngle:0} (setup {reference.x:0}/{reference.y:0}) hands {inches.x:0.0},{inches.y:0.0} in yaw {yaw.Value:0} pitch {pitch.Value:0}");
#endif
        }

        #endregion
    }
}
