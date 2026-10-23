#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.Ninjago
{
    /// <summary>
    /// One arena look (dusk road, night storm, dawn finale): post profile, sun, trilight ambient, fog, clear colour,
    /// god-ray tint and the tilt-shift band (viewport y centre and half width).
    /// </summary>
    [Serializable]
    public class PhaseLook
    {
        [Tooltip("Post-processing profile blended in for this look.")]
        public VolumeProfile? profile;
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        [Tooltip("Sun (or moon) rotation in degrees.")]
        public Vector3 sunEuler = new(32f, 215f, 0f);
        [Header("Trilight Ambient")]
        public Color ambientSky = new(0.5f, 0.5f, 0.55f);
        public Color ambientEquator = new(0.4f, 0.38f, 0.4f);
        public Color ambientGround = new(0.2f, 0.18f, 0.18f);
        [Header("Fog (exponential)")]
        public Color fogColor = new(0.6f, 0.55f, 0.55f);
        public float fogDensity = 0.006f;
        [Header("Camera And Shafts")]
        public Color clearColor = new(0.5f, 0.5f, 0.6f);
        public Color shaftColor = Color.white;
        [Header("Tilt-Shift Band")]
        [Range(0f, 1f)] public float tiltCenter = 0.55f;
        [Range(0f, 0.5f)] public float tiltHalfWidth = 0.3f;

        public static PhaseLook Default(BossLook look)
        {
            return look switch
            {
                BossLook.Night => new PhaseLook
                {
                    sunColor = new Color(0.55f, 0.62f, 1f), sunIntensity = 0.7f, sunEuler = new Vector3(40f, 160f, 0f),
                    ambientSky = new Color(0.26f, 0.22f, 0.45f), ambientEquator = new Color(0.18f, 0.15f, 0.3f),
                    ambientGround = new Color(0.08f, 0.06f, 0.12f), fogColor = new Color(0.12f, 0.1f, 0.22f), fogDensity = 0.008f,
                    clearColor = new Color(0.06f, 0.04f, 0.12f), shaftColor = new Color(0.6f, 0.7f, 1f), tiltCenter = 0.52f,
                    tiltHalfWidth = 0.25f,
                },
                BossLook.Dawn => new PhaseLook
                {
                    sunColor = new Color(1f, 0.86f, 0.74f), sunIntensity = 1.1f, sunEuler = new Vector3(20f, 150f, 0f),
                    ambientSky = new Color(0.62f, 0.62f, 0.76f), ambientEquator = new Color(0.55f, 0.46f, 0.46f),
                    ambientGround = new Color(0.25f, 0.2f, 0.2f), fogColor = new Color(0.86f, 0.7f, 0.65f), fogDensity = 0.005f,
                    clearColor = new Color(1f, 0.76f, 0.66f), shaftColor = new Color(1f, 0.9f, 0.75f), tiltCenter = 0.52f,
                    tiltHalfWidth = 0.25f,
                },
                _ => new PhaseLook
                {
                    sunColor = new Color(1f, 0.78f, 0.55f), sunIntensity = 1.3f, sunEuler = new Vector3(32f, 215f, 0f),
                    ambientSky = new Color(0.56f, 0.45f, 0.5f), ambientEquator = new Color(0.46f, 0.33f, 0.3f),
                    ambientGround = new Color(0.2f, 0.15f, 0.15f), fogColor = new Color(0.76f, 0.5f, 0.4f), fogDensity = 0.006f,
                    clearColor = new Color(0.95f, 0.6f, 0.4f), shaftColor = new Color(1f, 0.8f, 0.55f), tiltCenter = 0.55f,
                    tiltHalfWidth = 0.3f,
                },
            };
        }
    }

    /// <summary>
    /// Boss Battle look and camera data (TDD §3.2): looks per BossLook, base camera pose per phase, cutscene shot lists,
    /// tier and flash settings. Package F declares it with sensible defaults; package R fills the values.
    /// Asset: Assets/Configs/Ninjago/BossBattle/BossBattleVisualConfig.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "BossBattleVisualConfig", menuName = "Nex/Ninjago/Boss Battle Visual Config")]
    public class BossBattleVisualConfig : ScriptableObject
    {
        [Header("Looks")]
        [SerializeField] EnumDictionary<BossLook, PhaseLook> looks = DefaultLooks();
        [Header("Camera Poses")]
        [Tooltip("Base rig pivot pose per phase in arena local space.")]
        [SerializeField] EnumDictionary<BossPhase, CameraPose> cameraPoses = DefaultCameraPoses();
        [Header("Cutscene Shots")]
        [SerializeField] EnumDictionary<BossCutscene, List<CameraShot>> cutsceneShots = DefaultShots();
        [Header("Low Tier Height")]
        [Tooltip("World render height on the low GPU tier.")]
        [SerializeField] int lowTierHeight = 360;
        [Header("Degrade GPU Ms")]
        [Tooltip("Average GPU ms over 5 s of the intro above which the degrade ladder steps down.")]
        [SerializeField] float degradeGpuMs = 15f;
        [Header("PiP Preview Interval")]
        [Tooltip("Seconds between camera preview texture updates (0 = every frame), per tier.")]
        [SerializeField] float pipIntervalHighTier;
        [SerializeField] float pipIntervalLowTier = 0.125f;
        [Header("Flash")]
        [Tooltip("Colour of the full-screen flash (BossFxVolume).")]
        [SerializeField] Color flashColor = Color.white;
        [Tooltip("Peak weight of the flash volume.")]
        [SerializeField, Range(0f, 1f)] float flashPeakWeight = 1f;
        [Tooltip("Seconds of one lightning flicker.")]
        [SerializeField] float lightningSeconds = 0.35f;
        [SerializeField] float lightningIntensity = 0.8f;
        [Header("Pulse Lights")]
        [SerializeField, Range(0, 4)] int maxPulseLights = 2;
        [SerializeField] float pulseLightRange = 6f;

        #region Public API

        public int LowTierHeight => lowTierHeight;
        public float DegradeGpuMs => degradeGpuMs;
        public float PipInterval(bool lowTier) => lowTier ? pipIntervalLowTier : pipIntervalHighTier;
        public Color FlashColor => flashColor;
        public float FlashPeakWeight => flashPeakWeight;
        public float LightningSeconds => lightningSeconds;
        public float LightningIntensity => lightningIntensity;
        public int MaxPulseLights => maxPulseLights;
        public float PulseLightRange => pulseLightRange;

        public PhaseLook Look(BossLook look)
        {
            if (looks.TryGetValue(look, out var value) && value != null) return value;
            return looks[look] = PhaseLook.Default(look);
        }

        public CameraPose Pose(BossPhase phase)
        {
            return cameraPoses.TryGetValue(phase, out var pose) && pose.fov > 0f ? pose : DefaultCameraPose(phase);
        }

        /// <summary>The shot list of a cutscene; empty when none is authored.</summary>
        public IReadOnlyList<CameraShot> Shots(BossCutscene cutscene)
        {
            if (cutsceneShots.TryGetValue(cutscene, out var shots) && shots != null) return shots;
            return cutsceneShots[cutscene] = new List<CameraShot>();
        }

        #endregion

        #region Defaults

        /// <summary>Road (0, 4, -11) pitch 9 fov 50; Sky (0, 3.4, -8) pitch 2 fov 50 (TDD §3.2).</summary>
        public static CameraPose DefaultCameraPose(BossPhase phase)
        {
            return phase == BossPhase.Sky
                ? CameraPose.Of(new Vector3(0f, 3.4f, -8f), 2f, 50f)
                : CameraPose.Of(new Vector3(0f, 4f, -11f), 9f, 50f);
        }

        public static EnumDictionary<BossLook, PhaseLook> DefaultLooks()
        {
            var looks = new EnumDictionary<BossLook, PhaseLook>();
            foreach (BossLook look in Enum.GetValues(typeof(BossLook))) looks[look] = PhaseLook.Default(look);
            return looks;
        }

        public static EnumDictionary<BossPhase, CameraPose> DefaultCameraPoses()
        {
            var poses = new EnumDictionary<BossPhase, CameraPose>();
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase))) poses[phase] = DefaultCameraPose(phase);
            return poses;
        }

        /// <summary>Empty lists: the cutscene code (B) plays its own moves until R authors shots here.</summary>
        public static EnumDictionary<BossCutscene, List<CameraShot>> DefaultShots()
        {
            var shots = new EnumDictionary<BossCutscene, List<CameraShot>>();
            foreach (BossCutscene cutscene in Enum.GetValues(typeof(BossCutscene))) shots[cutscene] = new List<CameraShot>();
            return shots;
        }

        public void ResetToDefaults() => Reset();

        void Reset()
        {
            looks = DefaultLooks();
            cameraPoses = DefaultCameraPoses();
            cutsceneShots = DefaultShots();
            lowTierHeight = 360;
            degradeGpuMs = 15f;
            pipIntervalHighTier = 0f;
            pipIntervalLowTier = 0.125f;
            flashColor = Color.white;
            flashPeakWeight = 1f;
            lightningSeconds = 0.35f;
            lightningIntensity = 0.8f;
            maxPulseLights = 2;
            pulseLightRange = 6f;
        }

        /// <summary>Fills missing/null dictionary entries from the defaults, keeping authored values. Returns how many were filled.</summary>
        public int FillMissingDefaults()
        {
            var filled = 0;
            looks ??= new EnumDictionary<BossLook, PhaseLook>();
            foreach (BossLook look in Enum.GetValues(typeof(BossLook)))
            {
                if (looks.TryGetValue(look, out var value) && value != null) continue;
                looks[look] = PhaseLook.Default(look);
                filled++;
            }

            cameraPoses ??= new EnumDictionary<BossPhase, CameraPose>();
            foreach (BossPhase phase in Enum.GetValues(typeof(BossPhase)))
            {
                if (cameraPoses.TryGetValue(phase, out var pose) && pose.fov > 0f) continue;
                cameraPoses[phase] = DefaultCameraPose(phase);
                filled++;
            }

            cutsceneShots ??= new EnumDictionary<BossCutscene, List<CameraShot>>();
            foreach (BossCutscene cutscene in Enum.GetValues(typeof(BossCutscene)))
            {
                if (cutsceneShots.TryGetValue(cutscene, out var shots) && shots != null) continue;
                cutsceneShots[cutscene] = new List<CameraShot>();
                filled++;
            }

            return filled;
        }

        #endregion
    }
}
