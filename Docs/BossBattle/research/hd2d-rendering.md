# Boss Battle: HD-2D rendering on URP 17.3 for Android at 60 fps

Scope: how to give the Boss Battle world (phase 1 car on a road at dusk, phase 2 plane in a dark arena) the full
HD-2D look while it holds 60 fps on the Nex Playground (Mali-G52 MC2, GLES 3.2, 1920x1080). The look is a low-res
point-upscaled world, 3-5 band toon shading with normal and cavity maps, a tilt-shift band, bloom, vignette, grading,
hard shadows, god rays, and lit pixel particles at 12 fps.
Unity 6000.3.9f1, URP 17.3.0 (`Library/PackageCache/com.unity.render-pipelines.universal@7327e77c1cc2`).
All paths are relative to `ninjago-unity/` unless they start with `Docs/`. Every line reference was checked against
the files on 2026-10-22. Items marked **[estimate]** or **[verify on device]** are not measured.

Sources read: `Assets/Scripts/BilliardRogue/Rendering/*.cs`, `Assets/Scripts/BilliardRogue/Configs/HD2DVisualConfig.cs`,
`Assets/Shaders/BilliardRogue/*`, `Assets/Settings/BilliardRogue/**`, `Assets/URP/*.asset`,
`ProjectSettings/{QualitySettings,GraphicsSettings,ProjectSettings,TagManager}.asset`, the BilliardRogue rendering
builders, Ninjago Chase/Chapter rendering code and builders, `Assets/Scenes/GameScene.unity`,
`Docs/BilliardRogue/research/urp-hd2d-{rendering,shaders}.md` and `Docs/BilliardRogue/progress/{rendering,final-perf}.md`.

---

## 0. Decisions

| Topic | Decision |
|---|---|
| Code reuse | **Reuse the BilliardRogue rendering classes as they are.** They compile into `Assembly-CSharp` like Ninjago, so Ninjago code only needs `using Nex.BilliardRogue;` (see §3). Do not move or rename them in this feature. |
| Rig | A **new `BossWorldCameraRig.prefab`**, built by a Ninjago editor builder from the same components (`WorldCameraRig`, `PixelWorldDisplay`, `FrameTimingLogger`). It is wired to a **Boss copy of `HD2DVisualConfig`** and the Boss volume profiles. |
| Renderer | **Reuse `WorldRenderer.asset`** (renderer index 1, World-layer masks, `TiltShiftFeature`). A separate renderer asset is not needed. |
| URP asset / quality | No change to the shared `URPAsset.asset`. The asset already has HDR, 1024 hard shadows with 1 cascade, HDR grading with a 32 LUT, and 4 per-pixel lights per object. Changing it would break the BilliardRogue contract tests (§8). |
| Resolution | **640x360 (3x integer upscale) on the low tier and on every Android device by default. 960x540 (2x) only on the full tier.** Do not use 480p: on 1080p it is a 2.25x scale, which gives uneven texel widths and shimmer. |
| Materials | New **ToonLit** twins of the Ninjago URP/Lit materials in a Boss folder. Do not edit the shared `Assets/Materials/Ninjago/*.mat`, because Chest Chase and Chapter use them. |
| Grading | Two look profiles, `Volume_BossDusk` (phase 1, warm) and `Volume_BossNight` (phase 2, deep cool), plus a `Volume_BossFx` flash volume. Reuse `Volume_LowTier` and `Volume_FeatureOverrides` as they are. |
| Lighting | Reuse `ActLightingPreset` (as data) and `EnvironmentLook.From/Lerp` (for blending) to drive the sun, trilight ambient, fog and god-ray tint for each phase. Restore `RenderSettings` when the mode exits. |
| Perf | The world pass costs ~1/9 of today's Chest Chase pass, which renders URP/Lit into a full 1080p RT. Main risks: the GameScene HDR camera stack, PiP camera-preview uploads, and shadow casters (§7). |

---

## 1. The existing HD-2D stack (BilliardRogue): exact API

### 1.1 Runtime classes (`Assets/Scripts/BilliardRogue/Rendering/`, namespace `Nex.BilliardRogue`)

**`PixelWorldDisplay : MonoBehaviour`**, `[DefaultExecutionOrder(200)]` (PixelWorldDisplay.cs:17-21). It owns the low-res RT,
snaps the camera to the texel grid and maps world points to UI.
- `void Initialize(Camera aWorldCamera, RawImage aDisplay, HD2DVisualConfig aConfig)` (L49-62). The camera's
  **parent becomes the pivot** (L55). Height = `config.RenderResolution.y`, unless a `QualityOverride` whose name matches the
  active QualitySettings level sets `renderHeight` (L58-61).
- `void SetVisibleHeight(int height)` (L101-107). Width = height x config aspect. Rebuilds the RT. Minimum 90.
- `void SetBaseFieldOfView(float fov)` (L110-120). The FOV is widened to cover the margin texels (L213-218).
- `void SetFocusPoint(Vector3 world)` (L123-127). The texel size is measured at this point. The default is where the view
  axis meets y = 0 (L241), or 20 m when the camera looks level.
- `bool TryWorldToCanvas(Vector3 world, RectTransform canvasRect, out Vector2 anchored)` (L133-146). Returns
  centre-relative anchored coordinates, so labels need their anchors and pivot at (0.5, 0.5).
- `Vector2 WorldToScreenNormalized(Vector3 world)` (L149-152). `void SetPixelationEnabled(bool)` (L155-160).
  `QualityOverride? FindQualityOverride()` (L86-98). Properties: `Target`, `WorldCamera`, `VisibleWidth/Height`, `PixelationEnabled`.
- RT (L184-196): `R8G8B8A8_SRGB` in Linear, **`R8G8B8A8_UNorm` in Gamma** (the project is Gamma:
  `ProjectSettings.asset m_ActiveColorSpace: 0`), depth 24, Point filter, Clamp, no mips, no MSAA. Size = visible + 2 x
  `MarginTexels` per axis.
- `LateUpdate` → `SnapToTexelGrid(pivot)` (L220-229). The pivot's world pose is rounded to texels in pivot space. The camera is
  **rewritten from the pivot every frame**, and the sub-texel remainder goes into `RawImage.uvRect`.

**`WorldCameraRig : MonoBehaviour`** (WorldCameraRig.cs). Serialized wiring at L19-35: `worldCamera`, `cameraPivot`, `worldVolume`,
`featureOverrideVolume`, `lowTierVolume`, `displayCanvas`, `display`, `pixelDisplay`, `config`, `displayPlaneDistance` (295).
- `void Initialize(Camera uiCamera)` (L55-79). It sets the display canvas to ScreenSpaceCamera on the UI camera at plane 295, calls
  `pixelDisplay.Initialize`, reads the quality override, detects the tier with
  `config.DetectTier(SystemInfo.graphicsDeviceName, SystemInfo.graphicsShaderLevel)`, then `ApplyToggles()`.
- `void SetVolumeProfile(VolumeProfile)` (L94-97). Sets `worldVolume.sharedProfile`.
- `void SetPose(Vector3 localPosition, float pitchDeg, float fov)` (L100-105). Moves the **pivot**.
- `void SetQualityFeatures(bool bloom, bool tiltShift, bool shadows)` (L107-113).
- `RenderQualityTier Tier` (L51). `WorldCamera`, `CameraPivot`, `Display`, `WorldVolume`.
- `ApplyToggles()` (L119-152). It enables the low-tier volume on `Low`, and turns tilt-shift and shadows off on the low tier unless
  `config.LowTierTiltShift/LowTierShadows`. It sets `bloomOff.active`/`tiltShiftOff.active` on the runtime copy of the override
  profile, plus `cameraData.renderShadows` and pixelation. In Editor, development and `ENABLE_DEBUG_SETTINGS` builds it **runs every
  LateUpdate** (L81-87) and reads `PlayerDataManager.Instance.DebugSettings.{disableBloom, disableTiltShift, disablePixelation,
  disableShadows, freezeWorld, renderTier}`.

**`RenderQualityTier`** (RenderQualityTier.cs:7-11): `Full = 0`, `Low = 1`.

**`TiltShiftVolume : VolumeComponent, IPostProcessComponent`**, menu "Billiard Rogue/Tilt Shift" (TiltShiftVolume.cs:14-37).
Parameters: `intensity` 0..1 (default 0 = off), `center` 0..1 (0 = bottom, default 0.45), `bandWidth` (half height,
0..0.5), `falloff` 0.01..1, `maxBlur` 0.5..4 (texel step), `sampleCount` 1..8 (taps per side). `IsActive() => intensity > 0`.

**`TiltShiftFeature : ScriptableRendererFeature`** (TiltShiftFeature.cs:21-36). The pass runs at `BeforeRenderingPostProcessing`,
only on Game cameras with post on and an active `TiltShiftVolume`. It reads `UnityEngine.Rendering.VolumeManager` fully qualified (L30-31).
**`TiltShiftPass`** (TiltShiftPass.cs:58-93) uses Render Graph only. It runs a half-res H blur, a V blur and a composite, then swaps
`resourceData.cameraColor`, so bloom and grading run on the tilted image. Each draw gets its own MPB.

**`FrameTimingLogger`** (FrameTimingLogger.cs). Every 5 s it prints a `[Perf]` line with fps, missed frames, CPU main, render and GPU ms,
the bottleneck, GC, batches, SetPass and tris, `tier` and `DebugHooks.State()` (L243-249, BilliardRogue's hook). It runs only while
`DebugSettings.logFrameTiming` is set (L89-96), and is disabled in release (L53-58). Read it with `adb logcat -s Unity | grep Perf`.

**`HD2DVisualConfig : ScriptableObject`** (`Assets/Scripts/BilliardRogue/Configs/HD2DVisualConfig.cs`, menu
"Nex/Billiard Rogue/HD2D Visual Config").
- `QualityOverride { string qualityName; int renderHeight; bool bloom, tiltShift, shadows; }` (L12-22).
- Fields (L24-72): `renderResolution` (640x360), `marginTexels` (1), `pixelSnapping`, `pixelationEnabled`, the tilt-shift
  defaults, bloom defaults, `vignetteIntensity`, `shadowDistance` (42), `qualityOverrides[]`, `lowTierGpuNames`
  {"Mali-G52","Mali-G51","Mali-G31"}, `lowTierMaxShaderLevel` 35, `lowTierBloomMaxIterations` 3,
  `lowTierBloomQuarterResolution`, `lowTierTiltShiftSampleCount` 2, `lowTierTiltShift` true, `lowTierShadows` true,
  `lowTierPipPreviewHz` 8.
- `RenderQualityTier DetectTier(string gpuName, int shaderLevel)` (L104-121).
- `float PipPreviewInterval(RenderQualityTier tier)` (L124-127): 1/8 s on the low tier, else 0.

The asset `Assets/Configs/BilliardRogue/HD2DVisualConfig.asset` has these quality overrides: **Low** 270 / bloom off / tilt off / shadows off,
**Medium** 360 / bloom on / **tilt-shift OFF** / shadows on, **High** 360 / all on. **Android's default quality is Medium**
(`QualitySettings.asset` L328-329 `Android: 2`), and no code calls `SetQualityLevel`. So with this asset **tilt-shift is off
on every Android device.** The Editor uses Standalone, which defaults to Ultra with no override, so all effects are on there. The Boss needs its own asset (§4.2).

### 1.2 Shaders (`Assets/Shaders/BilliardRogue/`). All are SRP-Batcher compatible and compiled for GLES3 + Metal (progress/rendering.md)

| Shader | Queue / blend | Properties (contract) | Keywords |
|---|---|---|---|
| `BilliardRogue/ToonLit` (ToonLit.shader + ToonLitInput.hlsl + ToonLitForwardPass.hlsl) | Geometry, opaque. Passes: ForwardLit, ShadowCaster, DepthOnly, DepthNormals | `_BaseMap`, `_BaseColor`, `_AlphaClip`/`_Cutoff`, `_UseWorldUv`/`_Tiling`, `_UseNormalMap`/`_BumpMap`/`_BumpScale` (0-4, default 2), `_UseCavityMap`/`_CavityMap` (R, 0.5 neutral)/`_CavityStrength` (0-2), **`_Bands` (3-5, default 4)**, `_BandSoftness`, `_ShadowTint`, `_AmbientStrength`, `_RimColor` (A = strength)/`_RimPower`, `_UseEmission`/`_EmissionMap`/`_EmissionColor` (HDR)/`_EmissionStrength`, `_FlashColor`/`_FlashAmount`, `_StatusTint`, `_Alpha`, `_Cull` (ToonLit.shader:7-37) | local: `_NORMALMAP _WORLD_UV _CAVITYMAP _EMISSION _ALPHATEST_ON`. Global: `_MAIN_LIGHT_SHADOWS(_CASCADE) _ADDITIONAL_LIGHTS _CLUSTER_LIGHT_LOOP`, fog, instancing (L59-68) |
| `BilliardRogue/ToonLitTransparent` | Transparent, SrcAlpha/OneMinusSrcAlpha, no depth write, ForwardLit only | same + `_Alpha` (default 0.5) | same |
| `BilliardRogue/LitParticle` | **AlphaTest**, alpha clip, ZWrite On, Cull Off, no shadow caster | `_BaseMap` (sheet), `_BaseColor`, `_Cutoff`, `_LightInfluence`, `_EmissionStrength`, `_Bands` (2-5), `_ShadowTint` | shadows, add. lights, cluster, fog |
| `BilliardRogue/GlowParticle` | Transparent, **SrcAlpha One** (additive) | `_BaseMap`, `_BaseColor` (HDR), `_Intensity`, `_SoftDisc` | `_SOFT_DISC`, fog |
| `BilliardRogue/LightShaft` | Transparent, **One One**, ZWrite Off, Cull Off | `_Color` (HDR), `_Intensity`, `_NoiseTex`, `_NoiseScroll`, `_EdgeSoftness`, `_EdgePower`, `_FadeDistance`, `_UseDepthFade`/`_DepthFade` | `_DEPTH_FADE` (needs a depth texture; keep off) |
| `BilliardRogue/TiltShiftBlur` | full-screen, used only by TiltShiftPass | `_TiltShiftBlur`, `_TiltShiftParams`, `_TiltShiftBlurTex` | none |

ToonLit lighting (ToonLitForwardPass.hlsl:95-121) works like this:
`ramp = ToonRamp(NdotL * shadowAtten)` (the shadow is banded with the light), then
`lighting = mainLight.color * lerp(_ShadowTint, 1, ramp) + SH * _AmbientStrength`. Additional lights band both NdotL and
the distance falloff, which gives stepped pools. Cavity: `color *= 1 + (cavity - 0.5) * 2 * _CavityStrength` (L113-116). After that
come rim, emission, flash and fog. All maps use the global `sampler_PointRepeat` (ToonLitInput.hlsl:32-35), so importer filter settings do not
matter. `_WORLD_UV` box-projects for primitives (L48-55).
Note: `ActEnvironmentController` publishes `_WorldRimColor` (ActEnvironmentController.cs:25, :201), but **no shader reads it**.
The rim comes from each material's `_RimColor`.

### 1.3 Pipeline assets

- `ProjectSettings/GraphicsSettings.asset:40` sets the default pipeline to `Assets/URP/URPAsset.asset`. Every quality level has
  `customRenderPipeline: {fileID: 0}`, so all levels use this one asset.
- `Assets/URP/URPAsset.asset`: renderer list L19-21 = `[0] URPAsset_Renderer` (UI/default, all layers, no features),
  `[1] Settings/BilliardRogue/WorldRenderer`, default index 0 (L22). HDR on (L27, `_32Bits` = B10G11R11), MSAA 1 (L29),
  main shadow 1024 (L47), 4 per-pixel additional lights per object (L48-49), shadow distance **42** (L58), **1 cascade** (L59),
  **soft shadows off** (L67), SRP batcher (L73), cookies off (L76), **ColorGradingMode HDR (1), LUT 32** (L81-82),
  depth and opaque textures off (L23-24).
- `Assets/Settings/BilliardRogue/WorldRenderer.asset`: Forward, IntermediateTextureMode Auto, features = `[TiltShiftFeature]`
  (shader `BilliardRogue/TiltShiftBlur`), **opaque and transparent layer masks = 256 = layer 8 "World"** only.
- Layers (`TagManager.asset`): 8 = `World`, 9 = `WorldVolume`.
- Android: GLES3 only (`m_APIs: 0b000000`), Blit Type Auto, Frame Timing Stats on, Swappy on.
- Fog stripping: `GraphicsSettings.asset:47 m_FogStripping: 0` (Automatic) keeps only the fog modes used by **build scenes**.
  The build scenes are MainScene, GameScene and examples, and all of them have `m_Fog: 0`. **[verify on device]** Fog variants may be stripped from the APK. See §8.

### 1.4 Volume profiles (`Assets/Settings/BilliardRogue/Volumes/`, built by `VolumeProfilesBuilder`)

The look profiles each carry Bloom (Dual filter, Half downscale, maxIterations 4, HQ off), Tonemapping Neutral, ColorAdjustments,
WhiteBalance, SplitToning, LiftGammaGain, Vignette and TiltShiftVolume. Measured values:

| Profile | Bloom thr / int / tint | Contrast / Sat | WB temp / tint | Split shadows → highlights (balance) | Vignette |
|---|---|---|---|---|---|
| Volume_Act1 (golden hour) | 1.25 / 0.42 / (1,.94,.82) | 18 / 6 | 3 / 0 | (.28,.26,.48) → (1,.92,.76) (4) | 0.20 (.1,.06,.05) |
| Volume_Act2 (deep blue night) | 1.2 / 0.6 / (.92,.95,1) | 20 / -4 | -12 / 3 | (.3,.36,.72) → (1,.88,.74) (-12) | 0.30 (.01,.02,.08) |
| Volume_Title | 1.1 / 0.6 | 12 / 12 | 10 / 0 | (.28,.26,.48) → (1,.85,.55) (8) | 0.30 |

Tilt-shift in all looks: intensity 0.8, center 0.49, bandWidth 0.44, falloff 0.06, maxBlur 1.5, taps 4.
`Volume_LowTier` overrides only cost: Bloom `maxIterations` 3, `downscale` Half, HQ off, and Tilt `sampleCount` 2.
`Volume_FeatureOverrides` overrides only Bloom `intensity` 0 and Tilt `intensity` 0. Rig volume priorities: World 0, LowTier 50 (disabled
unless low), FeatureOverride 100 (WorldCameraRigBuilder.cs:93-97).
**Build time vs runtime:** `HD2DVisualConfig.lowTierBloomMaxIterations / lowTierBloomQuarterResolution /
lowTierTiltShiftSampleCount` are read **only by `VolumeProfilesBuilder.BuildLowTier`** (VolumeProfilesBuilder.cs:223-238), which bakes
them into `Volume_LowTier`. At runtime the rig reads only `DetectTier`'s fields, `LowTierTiltShift`, `LowTierShadows`
(WorldCameraRig.cs:146-147) and `PipPreviewInterval`. All rig volumes sit on layer `WorldVolume`, and the camera's
`volumeLayerMask` = 512.

---

## 2. How the Ninjago modes render today

- **Materials.** `NinjagoAssetsBuilder.Lit(name, color, smoothness = 0.15f, emission = null)`
  (`Assets/Scripts/Ninjago/Editor/NinjagoAssetsBuilder.cs:122-140`) creates **`Universal Render Pipeline/Lit`** (full PBR Lit,
  not Simple Lit) at `Assets/Materials/Ninjago/<name>.mat`. It sets `_BaseColor`, `_Smoothness` and optionally `_EMISSION` +
  `_EmissionColor`. `MaterialAsset` (L155-164) **returns an existing asset untouched** when the name exists, so names are shared
  across modes ("Grass", "Road", "Ninja Dark", ...). VFX use `Particle(...)` (L143-153) with URP `Particles/Unlit` and
  `Particles/Simple Lit`.
- **Chest Chase world.** `RunnerView.Initialize` (`Chase/RunnerView.cs:76-95`) allocates
  **`new RenderTexture(Screen.width, Screen.height, 24)`** (L79). That is a full 1080p ARGB32 RT, shown in the view's `feed` RawImage. `RunnerWorld`
  is instantiated at `worldOrigin = (2000, 0, 0)` (L23) so the GameScene ortho cameras never see it. `RunnerWorld.Initialize`
  sets `worldCamera.targetTexture` (`Chase/RunnerWorld.cs:102-113`). `ShowSegment` writes the camera **world pose** from
  `carCameraPose`/`skyCameraPose` (L321-338), and `ShakeCamera` writes `worldCamera.transform.position` (L340-351). The camera is
  built by `NinjagoWorldBuilder.NewCamera` (`Editor/NinjagoWorldBuilder.cs:381-396`): SolidColor sky (0.5, 0.78, 1), FOV 60,
  near 0.1, `allowHDR = false`, `allowMSAA = false`, **no UniversalAdditionalCameraData settings**. That means default renderer 0, **post-processing
  off**, culling Everything. Runner far clip 140 (L192). Car pose (0, 3, -6.5) with pitch 8.8°, sky pose (0, 3.9, -5) with pitch 1° (L193-197).
- **Chapter stage** does the same: `ChapterStage.cs:70-71` creates a full-screen RT, and a Cinemachine brain sits on the world camera.
  `ThemePainter.cs:84-89` sets `RenderSettings` fog (Exponential) and Flat ambient.
- **Light.** `NinjagoGamesBuilder.WireShared` (`Editor/NinjagoGamesBuilder.cs:78-96`) adds a `Sun` directional light to every game
  prefab: Euler (50, -30, 0), color (1, .96, .88), intensity 1.25, **Hard** shadows, strength 0.75. GameScene itself has no lights,
  fog off, and Flat ambient (0.212, 0.227, 0.259) (GameScene.unity L17-27).
- **Layers.** `NinjagoEditorUtils.Block` (`Editor/NinjagoEditorUtils.cs:223-236`) creates primitives on layer 0 that cast
  shadows (default). Ninjago VFX prefabs (`Assets/Prefabs/Ninjago/Vfx/*.prefab`) are on **layer 0**. BilliardRogue VFX
  prefabs are on **layer 8 World**.
- **Screen cameras in GameScene.** `Main Camera` (Base, ortho, **HDR on**, culls Everything, stack = [RootCamera], GameScene.unity
  L620-701) + `MainViewManager/RootCamera` (Overlay, HDR on, culls Everything; MainViewManager.prefab L46-111). This is the
  Main+Root stack that BilliardRogue's perf pass removed from its own scene (final-perf.md "Pass 3"). It always renders
  through a 1080p intermediate plus a final blit, and HDR is on because the URP asset supports HDR. `UIBackground` canvas is
  ScreenSpaceCamera on RootCamera at plane 300. Views use plane 300 - 10 x order (≤ 290).
- `ViewManager.RootViewCamera` (`Main/Views/Framework/ViewManager.cs:95`) is public, and `NinjagoGame.Views`
  (`Flow/NinjagoGame.cs:32`) exposes the ViewManager to a game.

So today no Ninjago mode has post-processing, toon shading or a low-res world, and Chest Chase shades URP/Lit at full 1080p.

---

## 3. Can Ninjago reference the BilliardRogue rendering code? Yes, directly

- The only asmdefs under `Assets/Scripts` are `BilliardRogue/Simulation` and `BilliardRogue/Input/Core` (+ tests).
  `BilliardRogue/Rendering`, `BilliardRogue/Configs`, `BilliardRogue/Presentation` and every `Ninjago/**` folder compile into
  **Assembly-CSharp**. `HD2DVisualConfig.asset` confirms this (`m_EditorClassIdentifier: Assembly-CSharp::Nex.BilliardRogue.HD2DVisualConfig`).
  Editor folders of both compile into **Assembly-CSharp-Editor**.
- Precedent: `Assets/Scripts/PlayerData/DebugSettings.cs:6-8` already has `using Nex.BilliardRogue; using Nex.Ninjago;`.
  The render debug rows there (L54-68: Disable Pixelation, Tilt-Shift, Bloom, Shadows, Freeze World, Quality Tier, Log Frame
  Timing) are generic, and `WorldCameraRig` reads them. The Boss Battle gets them at no cost.
- Editor reuse: `Nex.BilliardRogue.Editor.RenderPipelineBuilder.WorldRendererIndex()` and `.WorldLayerName` / `.WorldVolumeLayerName`
  (`BilliardRogue/Editor/RenderPipelineBuilder.cs:26-27, 88-101`) and `VolumeProfilesBuilder.LowTierPath` / `.FeatureOverridesPath`
  (`VolumeProfilesBuilder.cs:22-24`) are public. `VolumeProfilesBuilder.Component<T>` (L260-274) and
  `MaterialsBuilder.LoadOrCreate/SetKeyword` (`MaterialsBuilder.cs:242-280`) are **private**. Copy those few lines into the Boss builder
  rather than widening BilliardRogue.
- Name clash: inside any `Nex.*` namespace, `VolumeManager` resolves to the audio `Nex.VolumeManager`
  (`Assets/Scripts/Audio/VolumeManager.cs:12`). Write `UnityEngine.Rendering.VolumeManager` in full.
- No move or refactor to a shared `Nex.Rendering` namespace now. Volume components and MonoBehaviours bind by script GUID, so a
  later move is possible, but it touches BilliardRogue assets and tests for no Boss benefit.

---

## 4. Recommended setup for the Boss Battle world

### 4.1 Hierarchy (the game prefab owns everything, so destroying the mode releases the RT in `PixelWorldDisplay.OnDestroy`)

```
BossBattleGame (game prefab, Addressable)          Sun (directional, Hard shadows) from WireShared
├─ BossWorldCameraRig  [WorldCameraRig, PixelWorldDisplay, FrameTimingLogger]   layer World
│  ├─ CameraPivot                      ← pose, shake, cutscene moves go HERE
│  │  └─ WorldCamera (local identity)  Base, cull = World, renderer 1, post ON, HDR, no MSAA, volumeMask = WorldVolume
│  ├─ WorldVolume       (prio 0,  Volume_BossDusk / Volume_BossNight)   layer WorldVolume
│  ├─ PhaseBlendVolume  (prio 1,  next look faded in by weight)          layer WorldVolume   [new]
│  ├─ BossFxVolume      (prio 10, Volume_BossFx, weight 0 at rest)       layer WorldVolume   [new]
│  ├─ LowTierVolume     (prio 50, Volume_LowTier, disabled)              layer WorldVolume
│  ├─ FeatureOverrideVolume (prio 100, Volume_FeatureOverrides)          layer WorldVolume
│  └─ WorldDisplayCanvas (ScreenSpaceCamera on RootViewCamera, plane 295) └─ WorldDisplay RawImage
└─ BossWorld (road / arena / car / plane / boss)   ALL on layer World, placed at (2000, 0, 0)
```
- Call `rig.Initialize(Views.RootViewCamera)` before the first gameplay view is pushed. The world display sits at plane 295, in front of
  UIBackground (300) and behind every view (≤ 290). It persists across the gameplay, cutscene, pause and result views ("every step
  a view"). Gameplay views must have **no opaque full-screen background**. The current `RunnerView.feed` RawImage is not used. If one
  view must own the display instead, `PixelWorldDisplay.Initialize(camera, view.feed, config)` also works without the rig, but then
  you lose the tier, volume and debug toggles.
- Build the rig with a Ninjago builder that mirrors `WorldCameraRigBuilder.Build/BuildCamera/BuildVolume/BuildDisplayCanvas`
  (`BilliardRogue/Editor/WorldCameraRigBuilder.cs:63-207`). Change: `config` = Boss HD2D config; `farClipPlane` ≈ 140 (the road
  wraps at 120 m, and the rig default is 80); `nearClipPlane` 0.3-1; FOV from the Boss config; `backgroundColor` per phase.

### 4.2 Boss `HD2DVisualConfig` instance (same class, new asset, e.g. `Assets/Configs/Ninjago/BossBattle/BossHD2DVisualConfig.asset`)

| Field | Value | Why |
|---|---|---|
| renderResolution | 640x360 | 16:9, 3x upscale to 1080p |
| marginTexels | 1 | smooth sub-texel pans and shakes |
| qualityOverrides | `Medium`: renderHeight 360, bloom ✓, **tiltShift ✓**, shadows ✓; `High`/`Very High`/`Ultra`: 540, all ✓; `Low`/`Very Low`: 270, bloom ✓, tilt ✗, shadows ✓ | Android runs Medium. The BilliardRogue asset turns tilt-shift off there |
| tiltShift* | intensity 0.85, center 0.48, halfWidth 0.30, falloff 0.10, maxBlur 1.5, taps 3 | car (≈0.2) and boss (≈0.7) stay sharp; the near road and the sky blur. **[tune with captures]** |
| bloom* | threshold 1.0, intensity 1.0, scatter 0.7, maxIterations 4 | "heavy bloom" baseline (BilliardRogue is 1.2 / 0.45). The look profiles scale it |
| vignetteIntensity | 0.3 | |
| lowTier* | GPU names as in the BR asset, tilt ✓, shadows ✓, PiP Hz 8 (runtime fields) | Playground = Mali-G52 |

The low-tier **cost** fields (bloom iterations, quarter-res bloom, tilt taps) take effect only through a volume asset. Reuse the shared
`Volume_LowTier` (bloom 3 iterations at Half, tilt 2 taps). If the device needs quarter-res bloom, build a Boss copy
(`BossVolume_LowTier`, with BuildLowTier's pattern) and wire it into the Boss rig. Do not rebuild the shared one: `RenderingPerfContractTests`
compares it with the BR config.

Full tier at 540p: the 540 rows give it on desktop and in the Editor. To give 540p to strong Android GPUs, add one line after
`rig.Initialize(...)`: `if (rig.Tier == RenderQualityTier.Full && Application.isMobilePlatform) rig.Display.SetVisibleHeight(540);`.
Tier comes from the GPU name, but the resolution comes from the quality level name, and these are independent (`PixelWorldDisplay.cs:58-61`).

### 4.3 Camera, pivot and pixel snapping (required change to the runner world)

`PixelWorldDisplay` takes the **camera's parent as the pivot** and **overwrites the camera's world pose every LateUpdate**
(PixelWorldDisplay.cs:55, 220-229). Today's `RunnerWorld` moves the camera itself (L335, L345, L350), so it would be overridden. Make
these changes in the Boss world (a Boss subclass or copy of RunnerWorld, or an optional `cameraPivot` field):
- Write `ShowSegment`'s pose and `ShakeCamera`'s offset to the **pivot**. The WorldCamera stays at local identity.
- Call `rig.Display.SetBaseFieldOfView(fov)` for each phase, not `camera.fieldOfView`. HD-2D reads best at 30-40° with the
  camera pulled back, compared with 60° today **[tune]**. Call `rig.Display.SetFocusPoint(vehicle.Position)` (car or plane) each frame or per phase, so the snap is
  exact at the player's depth. The default focus is the y = 0 hit, ~19.6 m ahead at 8.8° pitch.
- Expect crawl. The world scrolls toward a fixed camera, so geometry crawls at texel edges, and camera rotation (the cutscene)
  always crawls (research doc §3.4). This is accepted HD-2D behaviour. Snapping only removes camera-translation swim.
- Cinemachine: if a cutscene uses a `CinemachineBrain` (as `ChapterStage` does), put the brain on a **proxy transform that
  drives the pivot**, not on the WorldCamera. A brain on the camera fights the snap.

### 4.4 Layers and renderers

- Everything the world camera should draw must be on **layer 8 World**: road, arena, vehicles, boss, minions, walls, bricks,
  rocks, markers, line renderers, pooled obstacles, VFX. The WorldRenderer's filters are World-only, so the camera's culling mask alone is not enough.
  Set the layer recursively at the end of the builder. For runtime-spawned objects, use World-layer prefabs.
- **VfxManager**: Ninjago effects such as `VehicleBump` are layer-0 prefabs and will be **invisible** in the boss world.
  Give the Boss its own World-layer VFX prefabs (BilliardRogue's `VfxPrefabWriter`/`VfxParticleFactory` recipes are already
  World-layer, LitParticle/GlowParticle, 12 fps), or set the layer on `spec.Get()` instances.
- Do not put world-space canvases on the vehicles. `RunnerWorld`'s `PlayerSteerMarker` world canvases
  (`NinjagoWorldBuilder.cs:360-374`) would render at 360p, get bloom and tilt blur, and are filtered out anyway (layer 0).
  Move over-head player indicators, the rock warning text and similar labels to a screen-space layer in a view, re-projected every
  LateUpdate with `rig.Display.TryWorldToCanvas(world, canvasRect, out anchored)`, as in `WorldLabelLayer.Project`
  (`BilliardRogue/Presentation/WorldLabelLayer.cs:311-320`, below). Run after `PixelWorldDisplay` (order > 200) so labels follow the snapped camera.

```csharp
void Project(RectTransform target, Vector3 world, Vector2 offset)
{
    if (display.TryWorldToCanvas(world, rect, out var anchored)) { target.anchoredPosition = anchored + offset; return; }
    target.anchoredPosition = new Vector2(-9999f, -9999f);   // behind the camera
}
```

### 4.5 Materials: ToonLit twins (exaggerated normal + cavity, 3-5 bands)

Add a Boss editor factory, modelled on `MaterialsBuilder.BuildSurface/BuildPalette` (`MaterialsBuilder.cs:85-124`),
in `Assets/Materials/Ninjago/BossBattle/`:

```csharp
// Boss builder (Assembly-CSharp-Editor). Flat-colour low-poly part; HDR emission blooms (gamma threshold ~1).
public static Material Toon(string name, Color color, Color? emission = null, int bands = 4, Color? shadowTint = null)
{
    var m = LoadOrCreate($"{BossMaterialsRoot}/{name}.mat", "BilliardRogue/ToonLit");   // copy of MaterialsBuilder.LoadOrCreate
    m.SetColor("_BaseColor", color);
    m.SetFloat("_Bands", bands);                        // 3..5
    m.SetFloat("_BandSoftness", 0.02f);
    m.SetColor("_ShadowTint", shadowTint ?? new Color(0.34f, 0.32f, 0.5f));   // BR ActorShadowTint
    m.SetFloat("_AmbientStrength", 0.35f);
    m.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.55f));            // actors: strong rim
    m.SetFloat("_RimPower", 3f);
    SetKeyword(m, "_EMISSION", "_UseEmission", emission.HasValue);       // copy of MaterialsBuilder.SetKeyword
    if (emission.HasValue) { m.SetColor("_EmissionColor", emission.Value); m.SetFloat("_EmissionStrength", 1f); }
    SetKeyword(m, "_NORMALMAP", "_UseNormalMap", false);
    SetKeyword(m, "_CAVITYMAP", "_UseCavityMap", false);
    EditorUtility.SetDirty(m);
    return m;
}
```
- Relief (exaggerated normal + cavity) for large surfaces: reuse the ToonLit surface sets. These are `Assets/Materials/BilliardRogue/M_Surface_{Grass,Dirt,StoneFloor,
  MossyBrick,CrystalRock,RuinFloor,HollowFloor,CryptFloor,CryptBrick,WoodPlank}.mat`, each with 64 px albedo, normal and cavity in
  `Assets/Textures/BilliardRogue/Surfaces/`. For primitives, use twins of these with **`_WORLD_UV` on**,
  `_Tiling` ≈ 0.5, `_BumpScale` 2-3 (props) / 1.1 (floors), `_CavityStrength` 0.75 / 0.4. Suggested mapping: road shoulders = Grass/Dirt,
  canyon walls and side walls = MossyBrick, phase 2 arena = CrystalRock/HollowFloor. Keep the asphalt flat-coloured (Bands 3) so the
  danger markers read.
- To reuse Ninjago geometry builders such as `NinjagoWorldBuilder.BuildRoad/BuildSky/BuildCar/BuildSkycraft`, which call `Lit(...)` internally, run a
  **`Toonify(root)` pass** on the built hierarchy. It maps each `sharedMaterial` (URP/Lit) to a cached ToonLit twin named
  `"<name> Toon"` with `_BaseColor` and `_EmissionColor` copied. That leaves the shared `Assets/Materials/Ninjago/*.mat` untouched.
- The boss uses the strongest rim (A 0.6), `_Bands` 4, an HDR emissive aura and eyes (2-4x) so it blooms against the night.
  Hit flash = `_FlashColor`/`_FlashAmount`, set on a **material instance**, not an MPB. An MPB drops the renderer out of the SRP batch
  (research doc §7.1).
- Telegraphs (the rock landing marker, wall-grow warnings): `ToonLitTransparent` quads, `_Alpha` 0.4-0.6, 0.02-0.05 m above
  the road (z-fighting is very visible at 360p), `ShadowCastingMode.Off`, `receiveShadows` off.

### 4.6 Post-processing looks (new `VolumeProfile`s, e.g. `Assets/Settings/Ninjago/BossBattle/Volumes/`)

Build them like `VolumeProfilesBuilder.BuildLook` (`VolumeProfilesBuilder.cs:153-207`): Bloom (Dual, Half, HQ off,
maxIterations from the config), Tonemapping **Neutral** (ACES darkens toon palettes), ColorAdjustments, WhiteBalance,
SplitToning, LiftGammaGain, Vignette, `TiltShiftVolume`. Sub-assets must be added with `AssetDatabase.AddObjectToAsset`
(`Component<T>`, L260-274). Starting values **[tune with captures]**:

| | `Volume_BossDusk` (phase 1, warm dusk) | `Volume_BossNight` (phase 2, deep cool night) |
|---|---|---|
| Bloom threshold / intensity / scatter / tint | 1.05 / 0.9 / 0.7 / (1, .86, .66) | 0.95 / 1.3 / 0.75 / (.8, .86, 1) |
| ColorAdjustments contrast / saturation / exposure | 22 / 8 / 0 | 28 / -8 / -0.2 |
| WhiteBalance temperature / tint | +18 / +4 | -22 / +6 |
| SplitToning shadows → highlights (balance) | (.30,.24,.50) → (1,.78,.52) (+10) | (.18,.22,.60) → (.70,.85,1) (-18) |
| LiftGammaGain lift / gain | (1,.98,1.02,0) / (1.03,1,.96,0) | (.96,.98,1.06,0) / (1,1,1.03,0) |
| Vignette intensity / colour | 0.30 / (.12,.05,.04) | 0.38 / (.01,.01,.06) |
| TiltShift | the config values (§4.2) | same, halfWidth 0.34 (the plane climbs and dives over ±halfHeight) |
| Camera clear colour | dusk sky (.98,.6,.42), or a far unlit gradient card | (.02,.02,.06) |

`Volume_BossFx` (weight 0 at rest, prio 10): ColorAdjustments `postExposure` +1.5, Bloom `intensity` +2 (only
these overrides). Tween its `weight` 1 → 0 over 0.2-0.4 s (unscaled) for the boss explosion, the clap wall impact and the
car→plane cut. LUT and Uber costs do not change with weight.

Phase change (cutscene): fade `PhaseBlendVolume.weight` 0 → 1 with the Night profile, then `rig.SetVolumeProfile(night)` and
reset the weight to 0. This is `ActEnvironmentController`'s blend-volume pattern (`BilliardRogue/Presentation/ActEnvironmentController.cs`
`BeginVolumeBlend` L219, `CompleteVolumeBlend` L230, which hands the profile to `cameraRig.SetVolumeProfile` at L233). Do not mutate profile assets at runtime. If you must, use `volume.profile` (an instanced copy) as
`WorldCameraRig.Initialize` does for the override volume (L63-66).

### 4.7 Lighting per phase (sun, ambient, fog, god-ray tint)

Reuse `Nex.BilliardRogue.ActLightingPreset` (`BilliardRogue/Configs/ActLightingPreset.cs`, [Serializable]) as config data, and
`EnvironmentLook.From(preset)` / `EnvironmentLook.Lerp(a, b, t)` (`Presentation/Environment/EnvironmentLook.cs:25, 46`) to blend
during the cutscene. Apply them like `ActEnvironmentController.ApplyLook` (L190-202): sun colour, intensity, shadowStrength and rotation, then
`RenderSettings.ambient{Sky,Equator,Ground}Color` with `ambientMode = Trilight`, then `fogColor/fogDensity` with `fog = true`, `fogMode = Exponential`.
Starting values, modelled on `EnvironmentLooks.Title/SunkenCrypt` (`BilliardRogue/Editor/EnvironmentLooks.cs:40-102`):

| | Dusk (phase 1) | Night (phase 2) |
|---|---|---|
| sunColor / intensity / Euler | (1,.62,.36) / 1.35 / (20, 160, 0): low back-light, long shadows toward camera, strong rims | (.55,.62,1) / 0.7 / (52, -120, 0): moon key |
| shadowStrength | 0.85 | 0.9 |
| ambient sky / equator / ground | (.55,.42,.58) / (.5,.34,.3) / (.2,.14,.14) | (.12,.14,.28) / (.08,.08,.16) / (.03,.03,.06) |
| fog colour / density (Exp) | (1,.62,.4) / 0.012. This also hides the 120 m strip wrap | (.04,.05,.12) / 0.02 |
| godRay colour / intensity | (1,.72,.4) / 1.4 | (.6,.72,1) / 0.5 |

- The Sun comes from `WireShared` and lives on the game prefab root (layer 0). Its default culling mask (Everything) lights World. Set
  `RenderSettings.sun` to it.
- **Restore `RenderSettings` when the mode ends** (GameScene: fog off, ambient Flat (0.212, 0.227, 0.259)). Otherwise the next mode
  inherits dusk fog. `ThemePainter` has the same issue for Chapter.
- Phase 2 point lights: keep **≤ 4 additional lights in view** (asset per-object limit 4, GLES3 total 16) and no
  additional-light shadows (unsupported in the asset). Prefer emissive + bloom (boss aura, lasers, spikes) over real lights.

### 4.8 God rays (LightShaft)

Reuse the mesh `Assets/Models/BilliardRogue/Environment/Env_LightShaft.fbx` and the material
`Assets/Materials/BilliardRogue/M_LightShaft.mat` (`BilliardRogue/LightShaft`, noise `Generated/T_Noise64.asset`). Placement:
`EnvironmentActBuilder.BuildShafts` (L163-178) with no shadow casting or receiving, on layer World, Transparent queue. Per-shaft
colour and intensity go through one shared MPB (`ActEnvironment.cs:77-81`: `_Color = shaft.color * godRayColor`,
`_Intensity = shaft.intensity * godRayIntensity`). Values above the bloom threshold bloom.
Phase 1: 3-5 shafts slanting from the low sun across the road ahead, placed in the scrolling strip so they pass by. Phase 2: 2-3 cold
shafts behind the boss silhouette. Keep `_DEPTH_FADE` off: it needs a camera depth texture (asset `m_RequireDepthTexture 0`), which
would add a depth copy.

### 4.9 Particles (billboard, lit, pixel, 12 fps)

- Lit sprites: `BilliardRogue/LitParticle` (AlphaTest, alpha clip, no blending overdraw, receives shadows, banded half-lambert
  lighting) for dust, debris, brick chips and wind streak chunks. Glows: `BilliardRogue/GlowParticle` (additive HDR) for sparks,
  laser hits, rocket trails and power-ups. Defaults: `M_LitParticle_Default.mat`, `M_GlowParticle_Default.mat`, `Materials/BilliardRogue/Vfx/*`.
- 12 fps flipbooks: `VfxParticleFactory` (`BilliardRogue/Presentation/Vfx/Editor/VfxParticleFactory.cs:184-198`) uses
  `textureSheetAnimation.timeMode = FPS` with `fps = 12` (`VfxPalette.Frame = 1/12`). Renderer settings (L251-267): View alignment, no sorting,
  shadows off, probes off, `maxParticleSize` capped. Only the frames step at 12 fps; positions update every frame.
  Pixel sprite sheets must import Point, no mips, uncompressed or RGBA32. ETC2 smears pixel edges (research doc §2.1).
- "Devil wind" obstacles (phase 2): ToonLitTransparent swirl meshes or LitParticle streaks, on layer World.

---

## 5. New code vs reuse

| Reuse as is | Path |
|---|---|
| `WorldCameraRig`, `PixelWorldDisplay`, `TiltShiftFeature/Pass/Volume`, `RenderQualityTier`, `FrameTimingLogger` | `Assets/Scripts/BilliardRogue/Rendering/` |
| `HD2DVisualConfig` (class, new asset) | `Assets/Scripts/BilliardRogue/Configs/HD2DVisualConfig.cs` |
| `ActLightingPreset`, `EnvironmentLook.From/Lerp` | `BilliardRogue/Configs/ActLightingPreset.cs`, `BilliardRogue/Presentation/Environment/EnvironmentLook.cs` |
| Shaders ToonLit / ToonLitTransparent / LitParticle / GlowParticle / LightShaft | `Assets/Shaders/BilliardRogue/` |
| `WorldRenderer.asset` (renderer index 1), `URPAsset.asset`, layers World / WorldVolume | `Assets/Settings/BilliardRogue/`, `Assets/URP/` |
| `Volume_LowTier.asset`, `Volume_FeatureOverrides.asset` | `Assets/Settings/BilliardRogue/Volumes/` |
| Surface materials, LightShaft mesh and material, noise, particle materials | `Assets/Materials/BilliardRogue/`, `Assets/Models/BilliardRogue/Environment/Env_LightShaft.fbx` |
| Debug render rows (pixelation, tilt, bloom, shadows, freeze world, tier, perf log) | `Assets/Scripts/PlayerData/DebugSettings.cs:54-68` |
| PiP preview throttle | `DetectionManager.SetPreviewTextureInterval(float)` (`Gameplay/Core/DetectionManager.cs:153-164`) + `HD2DVisualConfig.PipPreviewInterval(tier)`; pattern in `BilliardRogue/Flow/GameplayPip.cs:28-56` |
| Builder references | `RenderPipelineBuilder.WorldRendererIndex()`, `.WorldLayerName`; `VolumeProfilesBuilder.LowTierPath/FeatureOverridesPath` |

| New (Boss) | What |
|---|---|
| `BossHD2DVisualConfig.asset` | §4.2 |
| `BossVisualConfig : ScriptableObject` | `EnumDictionary<BossPhase, PhaseLook>` (`PhaseLook`: `VolumeProfile look`, `ActLightingPreset lighting`, `Color clearColor`, `Vector3 cameraPosition`, `float pitchDeg`, `float fov`), plus refs to the HD2D config, `Volume_BossFx`, flash timings, and `int fullTierMobileHeight` (540). Same pattern as `ChaseConfig.vehicles` (`EnumDictionary<VehicleType, VehicleSettings>`, `Ninjago/Configs/ChaseConfig.cs:51`), written by builders with `EnumDictionaryEditorUtils.GetValueProperty` |
| `BossWorldLook : MonoBehaviour` | `Initialize(WorldCameraRig rig, Light sun, BossVisualConfig)`, `Apply(BossPhase, float seconds)` (volume blend + EnvironmentLook lerp + clear colour + pose/FOV), `Flash(float seconds)` (BossFx weight), `RestoreRenderSettings()` in OnDestroy. Calls `rig.Display.SetFocusPoint` |
| Boss world camera pivot | pose and shake on the pivot (§4.3) |
| Editor `BossBattleRenderBuilder` | builds BossWorldCameraRig.prefab, the Boss volumes, `Toon(...)`/`Toonify(root)`, layer World recursively, shadow-caster trimming, shaft placement. Add it to the Ninjago build-all order **after** `RenderPipelineBuilder` |
| Screen-space label layer | over-head P1/P2 tags, danger callouts via `TryWorldToCanvas` |
| World-layer VFX prefabs | boss rock dust, brick burst, rocket trail, laser hit, explosion (LitParticle/GlowParticle, 12 fps) |
| Optional | `FrameTimingLogger` state provider: today it prints `DebugHooks.State()`, which says "not registered" outside BilliardRogue. You can instead set `Nex.BilliardRogue.DebugHooks.StateHandler` (public static `Func<string>?`, `DebugHooks.cs:35`) to report the boss phase |

---

## 6. 60 fps on the Playground: budget and the low tier

Reference numbers (`Docs/BilliardRogue/progress/final-perf.md`): budget ≤ 120 batches (shadow casters included), ≤ 60
SetPass, ≤ 150k tris, ≤ 2 ms UI, **0 B GC per frame**. On the Playground, BilliardRogue's title with a 642x362 world measured 59.9 fps with
**GPU 15.0 / 16.4 ms** (avg/max), tier Low, 100 batches, 20 SetPass and 46k tris, after it removed the Main+Root camera stack.
The GPU headroom is small.

Per-frame GPU on Mali-G52, 1080p surface **[estimate]**:

| Item | Today (Chest Chase) | Boss HD-2D low tier |
|---|---|---|
| World shading | URP/Lit at **1920x1080** (2.07 M px) + shadows | ToonLit at **642x362** (0.23 M px, ~1/9) + shadows, ~3 ms |
| World post | none | Bloom Dual 3 mips (Half) + tilt 2 half-res passes x 5 taps + composite + Uber (LUT, vignette), ~0.6-1 ms at 360p |
| Composite | RawImage 1080p (1 screen fill) | the same |
| UI stack | Main Camera (HDR) + RootCamera overlay: 1080p intermediate + final blit | the same, **unless GameScene is fixed (below)** |
| PiP camera preview | full rate | 8 Hz on the low tier (render thread ~9 ms per refresh on the Playground, per the HD2DVisualConfig tooltip) |

Levers, in order of payoff:
1. **The low-res world (this design).** It is the biggest cut compared with today's full-res RT.
2. **PiP throttle.** On the low tier, call `Detection.SetPreviewTextureInterval(bossConfig.PipPreviewInterval(rig.Tier))` while the gameplay
   view shows. Reset it to `0` before any setup or calibration view reuses the camera session (GameplayPip.cs:38-56).
3. **GameScene camera stack** (cross-cutting, affects every Ninjago mode; do it as a separate task with sign-off). Main Camera has `m_HDR: 1`
   (GameScene.unity:670), and with the asset's HDR the 1080p UI stack renders HDR through an intermediate. Minimum fix: Main Camera
   `allowHDR = false`. Full fix: make RootCamera the lone Base camera with HDR, MSAA, post and shadows off, as BilliardRogue's
   `FlowPrefabsBuilder` does (checked by `RenderingPerfContractTests.UiCameraRendersStraightToTheBackbuffer`). Final-perf estimated ~6-8 ms of
   copies before that fix, Android blit included. Blit Type is already Auto here.
4. **Shadow casters.** `Block()` primitives all cast. Turn casting off for asphalt, dashes, curbs, grass, cards, telegraphs, walls far
   from the road, and every particle. Keep casters on the vehicle body, the boss body (one part), bricks, rocks and minions. Final-perf cut casters
   from ~99 to ~25 this way. The shadow distance is a global 42 m (`URPAsset.asset:58`, also checked by a contract test), so keep the
   boss within ~40 m of the camera if its shadow must land.
5. **Draw calls.** The SRP Batcher keeps SetPass low across many ToonLit materials, but every primitive is still a draw. Today's road strip has ~70
   renderers and the sky ~31 spheres (`NinjagoWorldBuilder.cs:239-305`). Merge each tree (trunk + leaves) into one mesh, and use
   a palette-atlas ToonLit material (as `BuildPalette` does) for multi-colour models, so a whole car, plane or boss is 1-3 draws. Pool and prewarm
   everything the boss spawns (bricks, rocks, minions, rockets, labels) so nothing calls Instantiate or allocates GC mid-fight.
6. **Low tier knobs**, already wired: `Volume_LowTier` (bloom 3 iterations, tilt 2 taps) is enabled by `DetectTier`. Optional
   runtime knobs: `lowTierTiltShift = false`, `lowTierShadows = false` in the Boss config. Build-time knobs: quarter-res bloom or fewer taps
   through a Boss low-tier volume (§4.2), or 270p via the `Medium` quality override.
7. Verify on the device with `DebugSettings.logFrameTiming` and the `[Perf]` lines. GPU A/B tests use the Debug Settings rows (secret code ↑↑↓↓←→←→):
   Freeze World, Disable Shadows, Disable Bloom, Disable Tilt-Shift, Quality Tier 1 vs 2 (final-perf.md "Device verification").

---

## 7. Gotchas

1. **Tilt-shift is off on Android with the BilliardRogue config.** The `Medium` override has `tiltShift: 0` and Android defaults to
   Medium. The Editor (Ultra) shows it on. Use the Boss config (§4.2) and check on the device.
2. **The camera pose is owned by the pivot.** Anything that writes `WorldCamera.transform` is overwritten in LateUpdate (order 200).
   Shake, cutscene moves and Cinemachine must drive the pivot.
3. **World-layer only.** `WorldRenderer` filters opaque and transparent draws to layer 8. Default-layer children (Ninjago `Block`s, Ninjago VFX,
   world canvases, LineRenderers) silently disappear.
4. **Dev-build per-frame toggles.** In Editor, development and `ENABLE_DEBUG_SETTINGS` builds, `WorldCameraRig.ApplyToggles` reassigns
   `worldCamera.enabled`, `cameraData.renderShadows` and pixelation **every LateUpdate**. To hide the world, disable the
   display canvas or the rig GameObject, not the camera. To change shadows, use `SetQualityFeatures`.
5. **Volume reads.** Profiles are shared assets. Write tweaks to an instanced `volume.profile` or animate a separate volume's
   `weight`. Never edit `sharedProfile` at runtime: in the Editor that dirties the asset.
6. **`VolumeManager` name clash** inside `Nex.*`. Use `UnityEngine.Rendering.VolumeManager` (TiltShiftFeature.cs:30).
7. **Gamma colour space.** Bloom thresholds are in gamma space. HDR emission above ~1.0-1.2 blooms. The RT is `R8G8B8A8_UNorm`.
   Do not switch the project to Linear: it changes all starter UI and TMP blending.
8. **Shared Ninjago materials.** `NinjagoAssetsBuilder.MaterialAsset` returns existing assets by name. Changing "Grass", "Road" or
   "Ninja Dark" to ToonLit would restyle Chest Chase, Chapter and other modes. Use Boss-named twins.
9. **Global `RenderSettings`.** Fog, ambient and sun are scene-global. Restore them on exit.
10. **Fog variants [verify on device].** Fog stripping is Automatic (`GraphicsSettings.asset:47`), and no build scene enables fog. If the
    APK shows no fog, set fog stripping to Custom with Exponential kept (`m_FogKeepExp: 1` is already set) through an editor script,
    or enable fog in GameScene's Lighting settings. This also affects Chapter's `ThemePainter` fog.
11. **Integer scale.** 360p = 3x and 540p = 2x on a 1920x1080 surface. 480p (2.25x) gives uneven pixels. `Screen` sizes other than 1080p
    (Editor Game view) give non-integer scales, and that is fine for authoring.
12. **Perspective snapping is exact only at the focus depth.** Set `SetFocusPoint` to the vehicle. Near and far props still crawl
    slightly.
13. **Labels.** `TryWorldToCanvas` returns centre-relative coordinates. Anchors and pivot must be (0.5, 0.5) on a full-stretch rect.
14. **Contract tests guard the shared assets.** `RenderingContractTests.PipelineAssetIsTunedForAndroidTv` asserts HDR, MSAA 1,
    per-pixel lights, 4 lights per object, shadows 1024, 1 cascade, shadowDistance == BR config (42), SRP batcher, no depth texture, and a World
    renderer index > 0. `RenderingPerfContractTests` asserts the low-tier volume shape. Do not "tune" `URPAsset.asset` for the
    boss.
15. **`_DEPTH_FADE` on LightShaft** and any soft particles need a depth texture, which costs a depth copy. Keep them off.
16. **`_WorldRimColor` is dead.** No shader reads it. Set `_RimColor` on materials.
17. **Render Graph only.** Any new full-screen effect must implement `RecordRenderGraph`. `Execute`/`Configure` are no-op stubs in
    17.3 (research doc §1). Prefer volume overrides of the existing stack over new passes.

---

## 8. Implementation checklist (Boss Battle rendering)

- [ ] Create `BossHD2DVisualConfig.asset` (HD2DVisualConfig class): 640x360, margin 1, quality overrides with **Medium tilt ✓**,
      540 for High+, the low-tier fields as in §4.2.
- [ ] Builder: `Volume_BossDusk`, `Volume_BossNight`, `Volume_BossFx` (BuildLook pattern, Neutral tonemap, Dual bloom, the TiltShiftVolume
      values from the Boss config). Sub-assets go in with `AddObjectToAsset`.
- [ ] Builder: `BossWorldCameraRig.prefab`, a copy of `WorldCameraRigBuilder`: WorldCamera Base, cull World, `SetRenderer(WorldRendererIndex())`,
      post on, HDR on, MSAA off, depth and colour textures off, volume mask WorldVolume, far ≈ 140, plus volumes at prio 0 / 1 / 10 / 50 / 100
      (LowTier and FeatureOverrides are the shared BR assets), display canvas at plane 295, FrameTimingLogger wired.
- [ ] Builder: ToonLit twins (`Toon`, `Toonify`) in `Assets/Materials/Ninjago/BossBattle/`. World-UV surface twins for ground and walls.
      Boss emissive HDR parts.
- [ ] Builder: set layer **World** recursively on the boss world, the vehicles, the boss and all pooled prefabs. Turn shadow casting off on flat or decorative
      parts.
- [ ] Builder: LightShaft instances (Env_LightShaft.fbx + M_LightShaft), no shadows, World layer, colours from the phase lighting.
- [ ] Runtime: the game creates or holds the rig, calls `rig.Initialize(Views.RootViewCamera)` before the first gameplay view, then
      optionally sets 540p on full-tier mobile.
- [ ] Runtime: world camera pose and shake go through the pivot. Set `SetBaseFieldOfView` and `SetFocusPoint(vehicle)` for each phase.
- [ ] Runtime: `BossWorldLook.Apply(Phase1Dusk, 0)` on start. For the cutscene: `Flash` + blend to `Night` (volume weight + EnvironmentLook lerp +
      clear colour + pose), then `rig.SetVolumeProfile(night)`. Explosion: `Flash`. Restore `RenderSettings` on destroy.
- [ ] Runtime: PiP throttle with `SetPreviewTextureInterval(config.PipPreviewInterval(rig.Tier))` while the gameplay view is up, and 0 before
      setup or calibration.
- [ ] UI: over-head player tags, rock and minion warnings and boss name labels go on a screen-space layer via `TryWorldToCanvas`. No world-space
      canvases in the world.
- [ ] VFX: World-layer LitParticle and GlowParticle prefabs, 12 fps flipbooks, Point-imported sheets, pooled and prewarmed.
- [ ] Telegraph decals: ToonLitTransparent, offset above the road, no shadows.
- [ ] Device pass on the Playground (development build, `logFrameTiming` on). Sit 20 s each on: phase 1 cruise, rock storm,
      minion walls, brick slam, rockets, the cutscene, phase 2 wind, lasers, boss clap and the explosion. The target is `fps ≥ 59`,
      `gpu ≤ 15 ms`, batches ≤ 120, SetPass ≤ 60, GC 0 B on quiet frames, `tier Low`. Then run the GPU A/B checks with the Debug Settings rows.
- [ ] Check the picture on the device: tilt band visible (Medium override), fog present (stripping), bloom on emissives only, no
      white haze, crisp UI, labels on target.
- [ ] Separate task with sign-off: GameScene Main Camera `allowHDR = false`, or remove the camera stack (§6 item 3).
