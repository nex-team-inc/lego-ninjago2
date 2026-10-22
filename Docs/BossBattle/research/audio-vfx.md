# Boss Battle research: VFX, SFX and BGM

Scope: the three effect managers (`VfxManager`, `SfxManager`, `BgmManager`) and the mixer/volume code; how Ninjago and BilliardRogue build and
register particle prefabs and audio clips; the offline `Tools/Audio` pipeline; and the exact steps to add the Boss Battle entries. Checked against the code on 2026-10-22 (branch `main`, commit `4d326ccd`). Paths are relative to
`/Users/oscar/gitfolder/ninjago/ninjago-dragon-rising/ninjago-unity/`, except paths starting with `Tools/` or `Docs/`, which are relative to the repo root.

---

## 0. Summary

- **Registries.** All three managers are `Singleton<T>` MonoBehaviours under `CommonSingletons.prefab`. Each keeps an `EnumDictionary<TEnum, …>`
  registry serialized in its prefab in `Assets/Prefabs/Singletons/`. To add an effect: append an explicit, ascending int to the enum, build or copy
  the asset, then write the registry entry from an editor builder (`NinjagoAssetsBuilder.EditPrefab` + `EnumDictionaryEditorUtils.GetValueProperty`).
  A missing entry is tolerated at runtime: it plays nothing and logs one warning.
- **Two VFX styles, both playable by VfxManager today.**
  - *Ninjago:* procedural `ParticleSystem` prefabs (soft URP `Particles/Unlit` billboards or lit cube meshes) on the Default layer.
  - *BilliardRogue:* the HD-2D look the brief asks for. Point-filtered 8-frame pixel flipbooks stepped at 12 fps, drawn with
    `BilliardRogue/LitParticle` (banded lighting, alpha clip) and `BilliardRogue/GlowParticle` (additive HDR). They are built from data recipes,
    sit on layer `World` (8), and are **already registered** in the same VfxManager (keys 0–42).
- **Audio files.** Clips live in `Assets/Audio/Sfx/{Ninjago,BilliardRogue}/<Key>_<n>.wav` and `Assets/Audio/Bgm/{…}/*.ogg`. Ninjago code registers
  them with `KidsAudio.Register/RegisterMusic`.
- **`Tools/Audio` can synthesize new SFX offline.** Verified: numpy + scipy in a scratch venv rendered a rock whistle and a clap alarm with
  `sfxsynth.py`. Sample picks from the shared asset repo also work but need GitHub SSH and git-lfs. It cannot compose music (it only processes
  loops), there is no Python venv on this machine yet, and `build_audio.py` is hard-wired to BilliardRogue, so the boss needs its own small script.
- **Recommendation:** (1) add dedicated Boss Battle enum keys now, pointed at existing clips and prefabs, so the mode works on day 1; (2) build the
  boss VFX with the BilliardRogue recipe pipeline for the HD-2D look; (3) for final audio, use shared-repo samples for roar, explosions, wind,
  rocket, laser, impacts and both music loops, and synth for the whistle, clap alarm, brick-grow riser and minion tick.

---

## 1. Runtime APIs

### 1.1 `VfxManager`: `Assets/Scripts/Vfx/VfxManager.cs` (namespace `Nex`)

- Class: `public class VfxManager : Singleton<VfxManager>` (l.11).
- Registry: `[SerializeField] EnumDictionary<VisualEffect, VisualEffectSpec> effectSpecs` (l.192), stored in `Assets/Prefabs/Singletons/VfxManager.prefab`.
- `enum VisualEffect` (l.16–81). The rule on l.15: *"explicit ints, append only, keep ascending order"*.
  - BilliardRogue: `HitSpark=0, CritSpark=1, WallSpark=2, EnemyPoof=10, BossPoof=11, Explosion=20, FreezeBurst=21, BurnBurst=22, PoisonBurst=23,
    LightningHit=24, HealSparkle=25, SplitPop=26, PortalFlash=27, PickupSparkle=30, DustPuff=31, PlayerHurtFlash=32, CratePieces=33, LevelUpBurst=34,
    AmbientAct1..3=40..42`.
  - Ninjago: `NinjaDustKick=100, NinjaBrickPop=101, SpinjitzuSwirl=102, SpinjitzuBurst=103, BruteBrickSpray=104, VehicleBump=110, RockSlash=120,
    PieceLaunch=121, BossHit=122, RockImpact=123, SealDust=130, SealComplete=131, Breakthrough=132, LaserMuzzle=140, LaserImpact=141,
    EnemyExplosion=142, MechaHit=143, MechaPowerDown=144, MechaPowerUp=145, ArrowFizzle=146, Heard=150, GateCleared=151, WeaponSlash=160,
    NunchuckSpin=161, ShurikenHit=162, MonsterBreak=163, PunchImpact=170, BrickTrail=171, BrickBurst=172, PinSlam=173, ShieldBurst=180,
    DizzyStars=181, Hearts=182, CallBeam=190, SlamShockwave=191, BrickSnapPop=200`.
  - Chapter: `BrickRebuild=210, LinkDust=211`. **211 is the last value today.**
- `VisualEffectSpec` (l.168) holds `prefab` (a ParticleSystem; may be null), `defaultPoolSize` and `maxPoolSize`. `Awake` (l.198) adds one pool per filled spec.
- Pooling (l.85–165): `ObjectPool<ParticleSystem>` with `collectionCheck=false`. `HandleCreate` instantiates the prefab **as a child of
  VfxManager** and forces `main.stopAction = Callback` on the root (l.129); `Returner.OnParticleSystemStopped` (l.101) releases and deactivates it.
- API:
  - `void PlayVisualEffect(VisualEffect, Vector3)` (l.213).
  - `ParticleSystem? PlayVisualEffect(VisualEffect, Vector3, Quaternion, float scale = 1f)` (l.219): sets pose and `localScale = one*scale`, plays,
    and returns the instance (to move or `Stop()`), or null when unregistered. A missing effect logs `No prefab registered for …` once (l.240).
  - `void Prewarm(VisualEffect, int count)` (l.230). Example: `OnePunchStreet.cs:87-90`.

### 1.2 `SfxManager`: `Assets/Scripts/Sfx/SfxManager.cs`

`enum SoundEffect` (l.14–107) follows the same append-only, ascending rule (l.13):

| Group | Values |
|---|---|
| Framework / UI | `None=-1, GenericEnter=0, GenericExit=1`, `UiMove=100 … CountdownTick=105` |
| Billiard / enemies | 200–209, `EnemyDeath=300, BossHit=301, BossDeath=302, EnemyStep=303, EnemyAttack=304, EnemyCast=305, CrateBreak=306` |
| Abilities / pickups | `Explosion=400 … Portal=407`, `PickupBall=500, PickupHeal=501, PickupPower=502, PlayerHurt=503, LowHpWarning=504` |
| Flow | `TurnStart=600, RewardReveal=601, RewardPick=602, LevelUp=603, StageClear=604, BossAppear=605, GameOver=606, Victory=607` |
| Ninjago | `StaffWhoosh=700 … VehicleBump=704, RockSlash=710, KickThud=711, PieceHit=712, SealComplete=713, Breakthrough=714, LaserHum=720, MechaHit=721, EnemyExplosion=722, RunStep=730, SneakStep=731, Spotted=732, WeaponSlash=740, WeaponSpin=741, WeaponThrow=742, Punch=750, FlyAway=751, Pin=752, GuardBlock=760, Bonk=761, Cheer=762, CallSignal=770, TeammateSlam=771, GoblinPile=772, BrickSnap=780, WayOpen=781` (**the last value today**) |

- Registry: `EnumDictionary<SoundEffect, SoundEffectSpec> soundEffectDict` (l.142), where `SoundEffectSpec` holds `AudioClip[] clips`. Clips play
  round-robin from a random start and never repeat back to back (l.128–139).
- Playback API:

  | Method | Behaviour |
  |---|---|
  | `PlaySoundEffect(effect, AudioSource? custom = null)` (l.175) | PlayOneShot |
  | `PlaySoundEffect(effect, float pitch, float volume = 1f)` (l.182) | Uses one of 6 pooled sources (l.113); steals the oldest when all are busy (l.299) |
  | `AudioSource? StartLoop(effect, float volume = 1f)` (l.189) / `StopLoop(source)` (l.201) | Each loop gets its own source (e.g. `MechaFightView.cs:120,127`) |
  | `HasClips(effect)` (l.206) | — |

- Throttles: 35 ms per effect (`minRepeatInterval`, unscaled time, l.115/l.242), and each `AudioClip` starts at most once per frame
  (`MarkStarted`, l.251). `StartLoop` skips both throttles.

### 1.3 `BgmManager`: `Assets/Scripts/Bgm/BgmManager.cs`

- `enum BgmType` (l.14–34): `Main=0, Title=1, Act1=10, Act2=11, Act3=12, Boss=20, Reward=30, NinjaFight=40, NinjaChase=41, StoneKick=42,
  EarthSeal=43, RobotMecha=44, CrouchRun=45, ThreeWeapons=46, OnePunch=47, Guard=48, Call=49, Build=50` (**the last value today**).
- `enum StingerType` (l.36–42): `StageClear=0, BossAppear=1, Victory=2, Defeat=3`. All four are registered with the BilliardRogue `Stinger_*.ogg` files.
- Registries: `bgmDict` (l.45) and `stingerDict` (l.52). Every BgmType has a clip. `Boss=20` is `Bgm/BilliardRogue/Boss.ogg` (UGMC "Boss Battle 1 Loop", 157 BPM).
- API:
  - `Play(BgmType)` (l.86, no fade), `Stop()` (l.101), `Duck(volume01, holdSeconds)` (l.159), `FadeIn/FadeOut(0.5f)` (l.172/177), `Current`, `IsPlaying`.
  - `UniTask CrossFadeTo(BgmType, float duration = 0.8f, CancellationToken = default)` (l.111): two sources, unscaled time; does nothing if
    `Current == type` and it is already playing.
  - `float PlayStinger(StingerType)` (l.150): one-shot that ducks the music to 0.3 for its length, and returns that length.
- Existing usage: `ChestChaseGame.cs:47` `CrossFadeTo(BgmType.NinjaChase, cancellationToken: ct).Forget()`; `NinjagoGame.cs:88`
  `await FadeOut()` when the mode exits; `RunnerBlock.cs:158-159` `PlayStinger(BossAppear)` + `PlaySoundEffect(FlyAway)`.

### 1.4 Mixer and volume: `Assets/Scripts/Audio/*`, `Assets/Audio/AudioMixer/Main.mixer`

- The mixer exposes `MasterVolume`, `SfxVolume` and `MusicVolume`. The 0..1 slider maps to `log10(max(v,0.001))*20` dB. SfxManager outputs to the
  Sfx group and BgmManager to the Music group.
- `VolumeManager.Start` subscribes to `PlayerDataManager.Instance.{Master,Sfx,Bgm}VolumeProperty` and saves `PlayerPreference` after a 0.5 s
  debounce (and on pause or quit). **Volume persistence already exists.**
- The −6 dB master trim requested in `Tools/Audio/README.md` (l.213–220) is **not** applied in `VolumeManager.SetMasterVolume` (l.56). See G12.

### 1.5 `EnumDictionary` rules

- **Runtime** (`Library/PackageCache/team.nex.common-utils@d0649bb973c6/Runtime/EnumDictionary/EnumDictionary.cs`): `OnAfterDeserialize`
  (l.88–113) fills missing keys with `default` (a null spec is tolerated); unknown keys log `Encountered undefined key` (l.107).
- **Editor** (`…/Editor/EnumDictionary/EnumDictionaryEditorUtils.cs`): `GetValueProperty(dict, intKey)` (l.147) first runs `RectifyPropertyIfNeeded`,
  which sorts entries by `enumValueIndex` — the **declaration index** (l.62), not the int — then drops unknown keys and inserts missing ones. So a
  value declared out of numeric order corrupts the registry.
- **Repair tool:** `unity command eval 'return Nex.BilliardRogue.Editor.EnumDictionaryRepair.Run();'` makes every key exist in all four dictionaries.

---

## 2. Ninjago VFX: how prefabs are built and registered

Source: `Assets/Scripts/Ninjago/Editor/NinjagoVfxBuilder.cs`. Other builders use it via `using static Nex.Ninjago.Editor.NinjagoVfxBuilder;`.

- **Materials** (`NinjagoAssetsBuilder.Particle`, l.143):

  | Call | Shader | Look |
  |---|---|---|
  | `Particle("Particle Soft", true)` | URP `Particles/Unlit` | transparent, `Circle` sprite |
  | `Particle("Particle Brick", false)` | URP `Particles/Simple Lit` | opaque |

  The cube mesh comes from `Resources.GetBuiltinResource<Mesh>("Cube.fbx")`.
- **Helpers:**
  - `NewSystem(name, mat, duration, loop, maxParticles)` (l.279): no play-on-awake, World simulation space, emission rate 0, no shadows.
  - `Burst` (l.300), `Orbit` (l.306), `FadeOut` (l.316), `GrowOverLife` (l.326).
  - `MeshRenderer(system, mesh)` (l.333): turns shadow casting **On**.
  - `Save(name, system)` (l.64) writes `Assets/Prefabs/Ninjago/Vfx/<name>.prefab`.
  - `Register(dict, effect, prefab, defaultSize, maxSize)` (l.56).
- **Templates worth copying:**
  - `BuildBrickSpray` (l.154), `BuildChunks` (l.215, a rock impact), `BuildPuff` (l.197).
  - `BuildSwirl` (l.118): a looping orbit, a good base for devil wind.
  - `RobotMechaVfxBuilder.BuildMuzzle/BuildImpact` (l.46/66, laser loops) and `BuildExplosion` (l.85, bricks plus a child flash).
  - `CallBuilder.BuildShockwave`.
- **Registration pattern** (`Chapter/ChapterVfxBuilder.cs:13-25`):

```csharp
var rebuild = Save("Vfx_BrickRebuild", BuildRebuild(brick, cube));
NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
{
    Register(dict, VfxManager.VisualEffect.BrickRebuild, rebuild, 2, 6);
});
```

- **Builders using this pattern:** `NinjagoVfxBuilder` (called from `NinjagoBuildAll.Run` l.28), `RobotMechaVfxBuilder`, `CrouchRun`, `ThreeWeapons`,
  `OnePunch`, `Guard`, `Call`, `ChapterVfxBuilder` (from `ChapterForgeBuilder.cs:31`). Rebuild everything with
  `unity command eval 'return Nex.Ninjago.Editor.NinjagoBuildAll.Run();' 300000 --timeout 320`.
- **Call sites the boss code will reuse:**
  - `RunnerWorld.cs:282-283`: bump sound plus `PlayVisualEffect(VehicleBump, vehicle.Position + forward*0.8)`.
  - `RunnerBlock.cs:114,182,233`: `NinjaBrickPop`, `BrickBurst`.
  - Scaled bursts: `ChapterEnemy.cs:82`, `FightBlock.cs:239`.
  - Held loop: `MechaLane.cs:98-99` plays it, l.206-210 moves it each frame, l.223-236 resets its emission **before** `Stop()`. Pooled module state
    carries over to the next user, so the reset matters.

---

## 3. BilliardRogue HD-2D pixel particles (the target look)

### 3.1 Shaders: `Assets/Shaders/BilliardRogue/`

- **`LitParticle.shader`** (`"BilliardRogue/LitParticle"`): AlphaTest queue, `ZWrite On`, `Cull Off`, `clip(a - _Cutoff)` (no sorting issues, no
  ShadowCaster pass). Properties `_BaseMap, _BaseColor, _Cutoff(0.5), _LightInfluence, _EmissionStrength(0..8), _Bands(2..5), _ShadowTint`. Lighting
  is half-lambert banded into `_Bands` steps × main-light shadow attenuation (hard banded shadows); additional lights and the URP 17 cluster loop
  work. Vertex colour tints the sprite; the CBUFFER is SRP-Batcher compatible.
- **`GlowParticle.shader`**: additive `SrcAlpha One`, `ZWrite Off`, colour `[HDR] _BaseColor * _Intensity`, optional `_SOFT_DISC`; fog fades it to
  black, values above 1 bloom.

### 3.2 Pixel sheets: `Assets/Sprites/BilliardRogue/Particles/*.png` + `particles.json`

- 15 strips, 8 frames each:
  - 16 px frames (128×16): Spark, Dust, Leaf, Ember, Snow, Mote, Smoke, Star, IceShard, Bubble, Heart, Debris.
  - 32 px frames (256×32): Ring, Bolt, Flash.
- 12 fps, 28 texels/m (so a 16 px frame is 0.571 m). Import: Point filter, no mipmaps, uncompressed.
- Generator: `Tools/Textures/make_particles.py` (numpy + PIL; `SHEETS` at l.545, `main` at l.629). It writes grey, alpha-0/255 strips to
  `Tools/Staging/Assets/Sprites/BilliardRogue/Particles/`. Vertex colour does the tinting.
- New sheet (e.g. a wind swirl or rocket puff): add a draw function and a `SHEETS` row, re-run the generator, copy the PNG and JSON into the project.

### 3.3 Recipe pipeline: `Assets/Scripts/BilliardRogue/Presentation/Vfx/Editor/` (`Nex.BilliardRogue.Editor`)

All classes are public and live in an `Editor` folder without an asmdef, so they compile into Assembly-CSharp-Editor, the same assembly as `Nex.Ninjago.Editor`.

- **`VfxLayerSpec`** — one particle system.
  - Constructors: `Lit|Emissive|Glow(name, sheet)`.
  - Fluent setters: `.Burst .Stream(rate, alive, loopSeconds) .Life .Speed .Gravity .Drag .Drift .Noise .Sphere/Hemisphere/Circle/Cone/Box .Size
    .Colors .TintTo .Fade .SizeOverLife .Frames(first,last) .Flat .Bounce .Delay`.
  - Shading choices:

    | Shading | Shader setup | Use for |
    |---|---|---|
    | `Lit` | LitParticle, light influence 0.7–0.9 | debris, smoke, dust |
    | `Emissive` | LitParticle, influence 0.1 + per-sheet emission boost; opaque and blooms | sparks, rings, anything in the dark Phase 2 arena |
    | `Glow` | additive | Flash and Mote only |

- **`VfxRecipe.Burst(effect, defaultPool, maxPool)`** names the prefab `Vfx_<effect>`. `VfxRecipe.Ambient(act)` makes looping play-on-awake atmospheres.
- **`VfxPalette`**: colours, `Frame = 1/12 s`, `FullSheet`, and the curves `Punch()`, `Taper()`, `PopInOut()`.
- **`VfxParticleFactory.Configure(system, spec, sheet, material, floorPlane, isRoot, ambient)`** (l.20) sets every module explicitly:
  - World simulation space; **`scalingMode = Hierarchy`** (l.63), so the `scale` of `PlayVisualEffect` reaches child systems; `loop = ambient`
    (l.47); root `stopAction = Callback`; bursts `AlwaysSimulate`.
  - **Texture Sheet Animation in FPS mode at 12 fps** (l.184–199). This is the "stepped" look: frames step at 12 fps while motion stays smooth.
    Nothing in the project steps the simulation itself.
  - Renderer (l.249): billboard, `maxParticleSize = 0.5`, no shadows, probes or motion vectors. `FitRootDuration` stretches the root over its children.
- **`VfxMaterialLibrary(warnings)`** owns the shared `M_Vfx_{Lit|Emissive|Glow}_{Sheet}` materials in `Assets/Materials/BilliardRogue/Vfx/` (18 exist).
  `Material()` is at l.85, `DeleteUnused()` at l.140.
- **`VfxPrefabWriter.Write(recipe, library, stagingScene, worldLayer)`** (l.29) writes `Assets/Prefabs/BilliardRogue/Vfx/<name>.prefab` (`PrefabRoot`
  is a const, l.19). It edits prefabs in place (stable file IDs), works in a preview scene (the open scene stays clean), and sets the `World` layer.
- **`VfxRegistryWriter.Register(recipes, warnings)`** (l.20) runs `EnsureAllKeys`, then writes **only** keys that have a recipe; all other keys are left as is.
- **`Assets/Scripts/BilliardRogue/Editor/VfxPrefabsBuilder.Run()`** is the BilliardRogue entry point. **Do not run it** (see G1).
- Example recipe (`VfxCombatRecipes.cs`):

```csharp
static VfxRecipe Explosion() => VfxRecipe.Burst(Fx.Explosion, 2, 6)
    .Add(L.Glow("Flash", "Flash").Burst(1).Life(4 * Frame).Size(2.2f).Colors(PaleYellow).SizeOverLife(Punch()))
    .Add(L.Emissive("Ring", "Ring").Burst(1).Life(5 * Frame).Size(2.4f).Colors(Orange).Frames(1, 1))
    .Add(L.Emissive("Fire", "Smoke").Burst(8, 9).Life(0.33f, 0.5f).Speed(1.8f, 3.2f).Sphere(0.4f).Drag(4f).Gravity(-0.2f)
        .Size(0.9f, 1.2f).Colors(PaleYellow, Orange).TintTo(DeepOrange).Frames(0, 1))
    .Add(L.Lit("Rubble", "Debris").Burst(6).Life(0.9f, 1.2f).Speed(2.5f, 4.5f).Hemisphere(0.4f).Gravity(2.2f)
        .Size(0.4f, 0.5f).Colors(Rubble, RubbleDark).Frames(0, 7).Bounce(0.4f, 0.3f));
```

### 3.4 Can VfxManager play them? Yes

All 21 BilliardRogue prefabs are already registered (for example, key 0 is `Vfx_HitSpark`). From Ninjago code,
`PlayVisualEffect(VisualEffect.Explosion, pos, Quaternion.identity, 2f)` works today. Two caveats:
- **Layer:** they are on layer 8 (`World`), while Ninjago prefabs use layer 0. The Ninjago runner camera renders Everything, so both show. A
  BilliardRogue-style world camera culling to `World` only (mask 256) would hide all Ninjago VFX.
- **Size:** they are tuned for about 30 px/m. Pass `scale` for the far boss; this works because these prefabs use Hierarchy scaling.

---

## 4. Audio clips: storage and registration

### 4.1 Where the files are

- **Ninjago SFX:** `Assets/Audio/Sfx/Ninjago/<Key>_<n>.wav` — 50 mono files, mostly 44.1 kHz (`MechaHit` is 48 kHz, `WeaponSlash` 96 kHz), 0.14–2.4 s long.
- **Ninjago BGM:** `Assets/Audio/Bgm/Ninjago/<BgmType>.ogg` — 11 loops.
- **BilliardRogue:** 76 SFX in `Sfx/BilliardRogue` (peaks −3 to −13 dBFS by design), plus BGM and stingers in `Bgm/BilliardRogue/{Title,Act1-3,Boss,Reward,Stinger_*}.ogg`.
- `.wav`, `.ogg` and `.png` are stored in Git LFS (`git check-attr filter` reports `lfs`).

### 4.2 Registration code

From `Assets/Scripts/Ninjago/Editor/KidsAudio.cs`:

```csharp
public static void Register(SfxManager.SoundEffect effect, params string[] names)   // names under Assets/Audio/Sfx/Ninjago, no ".wav"
{
    foreach (var name in names) Import($"{SfxRoot}/{name}.wav", false);
    NinjagoAssetsBuilder.EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict",
        dict => NinjagoAssetsBuilder.Clips(dict, effect, SfxRoot, names));
}
public static void RegisterMusic(BgmManager.BgmType type, string name)               // Assets/Audio/Bgm/Ninjago/<name>.ogg
```

- `Import` (l.53): SFX get `DecompressOnLoad + ADPCM + forceToMono=true`; music gets `Streaming + Vorbis`. It never touches `normalize` (default 1)
  or `preloadAudioData`.
- Callers: `CallBuilder.BuildAudio` (l.45–51), `GuardBuilder` (l.47–50), `BuildBuilder` (l.46–48).
- Older inline form: `NinjagoAssetsBuilder.BuildAudioRegistry` (l.241–273) and the per-mode builders.
  - SFX: `NinjagoAssetsBuilder.Clips(dict, effect, root, names…)` (l.275) accepts **any** folder, e.g.
    `Clips(dict, SoundEffect.RocketHit, "Assets/Audio/Sfx/BilliardRogue", "Explosion_1", "Explosion_2")`.
  - BGM: `GetValueProperty(dict, (int)BgmType.X).objectReferenceValue = Load<AudioClip>(path)`.
  - Stingers: no helper exists; use `EditPrefab<BgmManager>(…, "stingerDict", …)`.
- BilliardRogue: `AudioRegistryBuilder.cs` reads `Tools/Audio/audio_manifest.json`; `ImportSettingsBuilder.ConfigureSfx` (l.305) sets
  `forceToMono=false` and `preloadAudioData=true`.

### 4.3 Import settings that differ

| | Ninjago SFX (`VehicleBump_1.wav.meta`) | BilliardRogue SFX (`BossHit_1.wav.meta`) |
|---|---|---|
| load / format | DecompressOnLoad / ADPCM | same |
| forceToMono / normalize | **1 / 1**: Unity peak-normalizes the clip | 0 / 1: built loudness is kept |
| preloadAudioData | **0**: loads synchronously on first play (hitch risk) | 1 |

### 4.4 Registered clips that can stand in now

Every key listed in §1.2 already has clips. Good boss stand-ins:
- Ninjago: `VehicleBump`, `KickThud`, `Breakthrough` (1.5 s stone crumble), `TeammateSlam`, `GoblinPile`, `Bonk`, `BrickSnap`×3, `WayOpen`,
  `LaserHum` (2.4 s loop), `MechaHit`, `EnemyExplosion`, `FlyAway`.
- BilliardRogue: `BossHit`×3, `BossDeath` (3 s), `Explosion`×2 (short, chain-safe), `PickupPower`, `PlayerHurt`, `LowHpWarning`, `BossAppear`,
  `Victory`, `Blocked` (metal clang).

---

## 5. The `Tools/Audio` pipeline: can it make new SFX and music?

### 5.1 Files

| File | Role |
|---|---|
| `README.md` | Rationale, mix plan, integration. Paths are written for `/Users/simonbut/project/VibeProject3/{Starter,Tools}`; here they are `ninjago-unity` and `Tools`. |
| `sfxsynth.py` | sfxr-style synth. `Voice` (l.17): waves `square/saw/triangle/sine/noise/pnoise`, `slide`, `arp`, `vib`, `duty_sweep`, ADSR+`punch`, `lowpass/highpass`, `crush_bits/rate`, `start`. `render(voices, seed, peak_db=-1)` (l.114) and `write_wav` (l.131) output 44.1 kHz mono 16-bit, deterministic per seed. |
| `synth_presets.py` | 16 presets in `PRESETS` (l.154): `hit_ping, cue_thump, enemy_step, heartbeat, turn_start, ui_pause, ui_resume, crit_shing, power_boom, ice_chime, bubbles, enemy_pop, player_hurt, boss_thud, sub_thump, blast_crack`. `synth(name, p, seed)` (l.160) renders at −1 dBFS. |
| `picks.py` | `SFX = {key: Sfx(category, [V([R(repo_path…), S(preset…)], drive_db, semis…)])}`, plus `BGM`, `STINGERS`, `TARGETS`, and the checks `LADDER/INSTANT/PING_FAMILIES/HEADROOM_SCENARIOS`. |
| `build_audio.py` | `render_sfx` (l.70), `normalize_variants` (l.88), `render_stinger_stereo` (l.96), `process_bgm` (l.114: loop-safe static gain to −18 LUFS, −3 dBFS ceiling, Vorbis q5), `main` (l.155). Output paths are hard-coded to `Assets/Audio/{Sfx,Bgm}/BilliardRogue` (l.37–38) under `Tools/Staging/`. It also writes `audio_manifest.json`. |
| `audiolib.py`, `shared_repo.py` | ffmpeg/soxr decoding, BS.1770 loudness, trim/fade/filter/repitch, WAV/OGG writers, `loop_report`. `shared_repo.py` fetches one file at a time from `~/Documents/music-cell-shared-assets` (`git show HEAD:<p> \| git lfs smudge`), never copies `.meta` files, and checks each fetch with ffprobe. |
| `audition.py`, `sheet.py` | Candidate comparison (`--out DIR --slot X --sheet`) and review PNGs (PIL). |

### 5.2 Dependencies on this machine (checked)

- **Present:** `/opt/homebrew/bin/ffmpeg` (with libsoxr and libvorbis) and `ffprobe` (paths hard-coded in `audiolib.py:14`, `shared_repo.py:18`);
  `git-lfs`; the shared repo `~/Documents/music-cell-shared-assets`, a partial (`blob:none`) clone of
  `git@github.com:nex-team-inc/music-cell-shared-assets.git` with an empty work tree and a 505 MB `.git`. New picks need network and GitHub SSH.
- **Missing:** `Tools/.venv` (already in `.gitignore` l.157), and numpy, scipy and Pillow in both `/usr/bin/python3` (3.9.6) and
  `/opt/homebrew/bin/python3.13`. Fix: `python3 -m venv Tools/.venv && Tools/.venv/bin/pip install numpy scipy pillow`.
- **Verified:** the synth alone needs only numpy and scipy. In a scratch venv (numpy 2.0.2, scipy 1.13.1) I rendered `BossRockWhistle_1.wav` (0.9 s sine
  glide plus noise), `ClapWarning_1.wav` (two-tone square alarm) and `synth("boss_thud")`, all as 44.1 kHz mono PCM16.
- **Other requirements:** `build_audio.py` also needs Pillow (it imports `sheet`) and the shared repo.
- **Broken path:** `Tools/sync_staging.sh` copies into `Starter/Assets`, which does not exist here. Copy into `ninjago-unity/Assets` instead, or fix the script.

### 5.3 Limits

- Good at: retro one-shots such as whistles, alarms, risers, blips and thuds.
- Weak at: roars, wind, real explosions and metal or stone impacts. Use samples for these.
- No loop rendering. A loop needs a sample, or custom code that renders whole cycles and passes `al.loop_report`.
- No music composition. Music must come from UGMC loops.

### 5.4 Candidate samples (all paths exist in the shared repo's git tree)

`U = Universal Sound FX/`, `UGMC = Ultimate Game Music Collection/`.

| Need | Candidates |
|---|---|
| Roar | `U MONSTERS_CREATURES/MONSTER_Growl_Deep_0{1..8}_*`, `U ALIENS/ALIEN_Attack_Roar_0{1..9}` |
| Rocks | `U DEMOLISH/DEMOLISH_Stone_0{1..5}`, `U DESTRUCTION/DESTRUCTION_Rocks_01`, `U RETRO_LOFI/RETRO_Collapse_Fall_Ground_01` |
| Whistle / rocket | `U WHOOSHES/Mixed/WHOOSH_Whistle_Fast_01`; `U WEAPONS/Rockets/ROCKET_Launch_{Bright,Deep_Short,Deep_Slow}`, `FLYBY_Missile_0{1,2}` |
| Laser / wind | `U WEAPONS/SciFi/Energy_Beams/WEAPON_Energy_Beam_0{1..5}_loop`; `U WIND/WIND_Storm_Blowing_Howling_0{1..6}_loop` |
| Spikes / explosions | `U HAZARDS_TRAPS/{HAZARD_Hammer_Impact_01, TRAP_Metal_Snap_Crunch_0{1,2}}`; `U EXPLOSIONS/{Medium,Long}/…Debris…` |
| Alarm / power-up | `U ALARMS/Digital/*` (loops; cut them); `U 8BIT/Powerups/*` |
| Music | `UGMC Combat/Boss Battle {2,3,4,5} Loop` (1 is already BilliardRogue `Boss`), `Boss Battle {1,3,4} (drums only) Loop`, `Frantic Pursuit LOOP`, `Chasing LOOP`, `Barren Boss LOOP`, `Desperate Battle LOOP` |

To compare candidates: add slots to `audition.py`, then run `Tools/.venv/bin/python Tools/Audio/audition.py --out <scratch> --slot …`.

### 5.5 How to produce the boss audio

1. **Day 1 (no new files):** add the enum keys (§6.1) and register them against existing clips with `NinjagoAssetsBuilder.Clips`, or point the
   config (§6.6) at existing keys.
2. **Production:**
   - Write `Tools/Audio/boss_picks.py` (the picks.py dataclasses plus boss-only `SFX`/`BGM` tables).
   - Write `Tools/Audio/build_boss_audio.py`:
     - Import `audiolib`, `shared_repo`, `synth_presets.synth` and `build_audio.render_sfx/normalize_variants/process_bgm`. This is safe because
       `build_audio.main` is guarded by `if __name__`.
     - Output to `Tools/Staging/Assets/Audio/Sfx/Ninjago/<Key>_<n>.wav` and `…/Bgm/Ninjago/BossBattle{1,2}.ogg`.
     - Write a separate `boss_audio_manifest.json`. **Never overwrite `audio_manifest.json`; `AudioRegistryBuilder` reads it.**
     - Replace the BilliardRogue ladder, ping and headroom checks with boss chains (3 rockets + 3 hits + BossHurt over the music).
   - Add synth presets `rock_whistle`, `clap_alarm`, `brick_riser`, `minion_tick`.
3. **Import:** copy the files into `ninjago-unity/Assets/Audio/...` and register them with the §6.4 helper (mono not forced, preload on).
4. **Loudness targets** (from the README plan):

   | Content | Target |
   |---|---|
   | All SFX peaks | ≤ −3 dBFS |
   | Impacts | −15.5 to −14 short LUFS |
   | Roar / explode | ~−14 |
   | Ticks / warnings | −20 to −18 |
   | Music | −18 LUFS, peak ≤ −3 dBFS |

---

## 6. Adding the Boss Battle entries: exact steps

### 6.1 `SfxManager.SoundEffect`: append after `WayOpen = 781` (`SfxManager.cs:106`)

```csharp
            // Boss Battle
            BossRockWhistle = 800, BossRockImpact = 801, BossRoar = 802, BossGroundSlam = 803, BrickGrow = 804,
            MinionLand = 805, MinionDamageTick = 806, WallSmash = 807, PowerUpPickup = 808, RocketLaunch = 809,
            RocketHit = 810, BossLaserLoop = 811, BossHurt = 812, DevilWind = 813, ClapWarning = 814,
            SpikeWallSlam = 815, BossExplode = 816,
```

For victory, reuse `SoundEffect.Victory` (607) together with `StingerType.Victory`. Both are registered and designed to play together (README l.187–190).

### 6.2 `BgmManager.BgmType`: append after `Build = 50` (`BgmManager.cs:33`)

```csharp
            BossBattle1 = 51,
            BossBattle2 = 52,
```

Keep `StingerType` as is: `BossAppear` for the phase-2 cutscene, `Victory` for the win, `Defeat` for a loss. If a separate boss-wake sting is wanted,
append `BossWake = 4` and register it through `EditPrefab<BgmManager>(…, "stingerDict", …)`.

### 6.3 `VfxManager.VisualEffect`: append after `LinkDust = 211` (`VfxManager.cs:80`)

```csharp
            // Boss Battle (loops: BossRockTrail, PowerUpGlint, RocketTrail, DevilWind, WindGust — caller Stop()s them)
            BossRockTrail = 220, BossRockImpact = 221, BossGroundSlam = 222, BrickGrow = 223, MinionLand = 224,
            MinionDamage = 225, WallSmash = 226, PowerUpGlint = 227, PowerUpPickup = 228, RocketTrail = 229,
            RocketHit = 230, BossHurt = 231, DevilWind = 232, WindGust = 233, SpikeWallSlam = 234,
            BossWake = 235, BossExplode = 236, VictoryBurst = 237,
```

- Laser: reuse the `LaserMuzzle=140` and `LaserImpact=141` loops. Tint the returned instance, and reset the tint before `Stop()`.
- Boss tired: reuse `DizzyStars=181`.

### 6.4 Builder: `Assets/Scripts/Ninjago/Editor/BossBattle/BossBattleAudioVfxBuilder.cs`

Model it on `CallBuilder`: a `Run()` with `[MenuItem("Nex/Ninjago/Build Boss Battle")]`, plus `BuildAudio()` and `BuildVfx()`. Call it from
`NinjagoBuildAll.Run()` (l.22–42).

```csharp
// Audio: own import so the boss clips keep their built loudness and are preloaded (KidsAudio forces mono + normalize).
const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
static void Import(string root, string name)
{
    var importer = (AudioImporter)AssetImporter.GetAtPath($"{root}/{name}.wav");
    var s = importer.defaultSampleSettings;
    s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.ADPCM; s.preloadAudioData = true;
    importer.defaultSampleSettings = s; importer.forceToMono = false; importer.SaveAndReimport();
}
public static void BuildAudio()
{
    // Import(SfxRoot, "BossRockImpact_1") ... for every file first, then one prefab edit (like RobotMechaBuilder l.148-173)
    NinjagoAssetsBuilder.EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict", dict =>
    {
        NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.BossRockImpact, SfxRoot, "BossRockImpact_1", "BossRockImpact_2");
        NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.RocketHit, "Assets/Audio/Sfx/BilliardRogue", "Explosion_1", "Explosion_2"); // placeholder
        // ... one line per key in §6.1 (Clips/Load throw on a missing file: register only clips that exist)
    });
    KidsAudio.RegisterMusic(BgmManager.BgmType.BossBattle1, "BossBattle1");   // Streaming + Vorbis
    KidsAudio.RegisterMusic(BgmManager.BgmType.BossBattle2, "BossBattle2");
}
```

**VFX option A (quick, Ninjago style):** use the `NinjagoVfxBuilder` helpers with `Save` and `Register`, as in §2. Set
`main.scalingMode = Hierarchy` on every prefab that has child systems.

**VFX option B (recommended for the HD-2D look):** use the BilliardRogue recipe pipeline.

```csharp
using Nex.BilliardRogue; using Nex.BilliardRogue.Editor; using UnityEditor.SceneManagement;
using Fx = Nex.VfxManager.VisualEffect; using L = Nex.BilliardRogue.Editor.VfxLayerSpec; using static Nex.BilliardRogue.Editor.VfxPalette;

var warnings = new List<string>();
var library = new VfxMaterialLibrary(warnings);            // reuses M_Vfx_* materials; NEVER call library.DeleteUnused()
var recipes = new List<VfxRecipe>
{
    VfxRecipe.Burst(Fx.BossRockImpact, 2, 4)
        .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(2.5f).Colors(WarmWhite).SizeOverLife(Punch()))
        .Add(L.Emissive("Ring", "Ring").Burst(1).Life(5 * Frame).Size(3f).Colors(DustDark).Frames(1, 1).Flat())
        .Add(L.Lit("Rubble", "Debris").Burst(10).Life(0.8f, 1.1f).Speed(3f, 6f).Hemisphere(0.6f).Gravity(2f)
            .Size(0.5f, 0.7f).Colors(Rubble, RubbleDark).Frames(0, 7))
        .Add(L.Lit("Dust", "Dust").Burst(8).Life(6 * Frame, 8 * Frame).Speed(1f, 2.5f).Circle(1f).Drag(3f).Gravity(-0.1f)
            .Size(0.9f, 1.3f).Colors(Dust, DustDark).Frames(0, 1)),
    // ... one recipe per key in §6.3
};
var staging = EditorSceneManager.NewPreviewScene();
try { foreach (var r in recipes) VfxPrefabWriter.Write(r, library, staging, WorldLayers.Resolve()); }   // or 0 = Default layer (G6)
finally { EditorSceneManager.ClosePreviewScene(staging); }
AssetDatabase.SaveAssets();
VfxRegistryWriter.Register(recipes, warnings);             // safe: keys without a recipe are left untouched
```

Notes on option B:
- **Prefab location:** prefabs land in `Assets/Prefabs/BilliardRogue/Vfx/` because the folder is a const. To put them under `Prefabs/Ninjago/Vfx`,
  add a `prefabRoot` parameter to `Write`/`PathOf`.
- **Loops** (trails, devil wind, glint): give them a `.Stream(rate, alive, loopSeconds)` layer and set `system.main.loop = true` after `Write`,
  because `Configure` sets `loop = ambient` (`VfxParticleFactory.cs:47`).
- **Don't use `VfxRecipe.Ambient` for loops:** it turns on `playOnAwake` and `prewarm`, so a pooled copy would play at the VfxManager origin the
  moment it is instantiated.

### 6.5 Runtime patterns

```csharp
// one-shot at a world point (rock lands, slam, wall smash, rocket hit)
SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BossRockImpact);
VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BossRockImpact, landing, Quaternion.identity, rockScale);

// attached loop (rocket trail, falling-rock trail, devil wind, laser): keep the handle, move it, stop it, forget it
trail = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RocketTrail, rocket.position, rocket.rotation);
// each frame: if (trail != null) trail.transform.SetPositionAndRotation(rocket.position, rocket.rotation);
if (trail != null) { trail.Stop(); trail = null; }          // Stop() returns it to the pool via OnParticleSystemStopped

// sound loop (also stop it in OnDestroy, like MechaFightView.cs:98)
laserHum = SfxManager.Instance.StartLoop(SfxManager.SoundEffect.BossLaserLoop, 0.8f);
if (laserHum != null) { SfxManager.Instance.StopLoop(laserHum); laserHum = null; }

// music by phase
BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.BossBattle1, cancellationToken: ct).Forget();   // phase 1 start
BgmManager.Instance.PlayStinger(BgmManager.StingerType.BossAppear);                                 // phase-2 cutscene
SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BossRoar);
BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.BossBattle2, 1.2f, ct).Forget();                 // after the cutscene
BgmManager.Instance.Duck(0.4f, 1.5f);                                                               // boss wakes / clap warning
// win: the stinger and the accent fire together (README l.187); NinjagoGame.cs:88 FadeOut()s on exit
SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BossExplode);
var stingSeconds = BgmManager.Instance.PlayStinger(BgmManager.StingerType.Victory);
SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Victory);
```

- **Prewarm** every boss effect when the arena loads, as in `OnePunchStreet.cs:87-90`.
- **Rocket salvos (1/2/3…):** space launches at least 40 ms apart. Otherwise the 35 ms throttle and the one-start-per-frame clip rule drop the later
  `RocketLaunch`/`RocketHit`. Using the `(pitch, volume)` overload with pitch `1 + 0.06*i` still goes through the same throttle.

### 6.6 Config indirection (ScriptableObject + EnumDictionary, as the brief requires)

Map game events to effects in the Boss Battle config. This lets you swap placeholders for final keys without code changes, and lets the debug
view audition each cue.

```csharp
public enum BossFxCue { RockWhistle, RockImpact, Roar, GroundSlam, BrickGrow, MinionLand, MinionTick, WallSmash, PowerUp,
                        RocketLaunch, RocketHit, Laser, BossHurt, DevilWind, ClapWarning, SpikeSlam, BossWake, BossExplode, Victory }
[Serializable] public struct BossFxEntry
{
    public SfxManager.SoundEffect sound;      // None = silent
    [Range(0f, 1f)] public float volume;
    public bool hasVisual;                    // VisualEffect has no None member; do not add one
    public VfxManager.VisualEffect visual;
    public float visualScale;
}
[SerializeField] EnumDictionary<BossFxCue, BossFxEntry> fx = new();
```

The builder fills it with `EnumDictionaryEditorUtils.GetValueProperty(serialized.FindProperty("fx"), (int)cue)`, the same way
`NinjagoAssetsBuilder.WriteVehicle` (l.218) fills `ChaseConfig.vehicles`.

---

## 7. Boss event → asset map

Legend: **S** = synth, **R** = shared-repo sample, **E** = already registered. Each cell reads "day-1 placeholder → final".

| Event | SFX | VFX |
|---|---|---|
| Rock warning locks, rock falls | BossRockWhistle: E `StaffWhoosh` → S `rock_whistle` / R `WHOOSH_Whistle_Fast_01` | BossRockTrail: E `NinjaDustKick` → Lit Dust + Emissive Ember stream |
| Rock lands | BossRockImpact: E `KickThud`/`Breakthrough` → R `DEMOLISH_Stone_*` + S `boss_thud` | BossRockImpact: E `RockImpact=123` + `SlamShockwave=191` → §6.4 recipe |
| Roar (phase change, wake) | BossRoar: E `BossAppear` → R `MONSTER_Growl_Deep_*` pitched −2 to −4 st | BossWake: E `SpinjitzuBurst` → Flash + Ring + Emissive Star |
| Ground slam | BossGroundSlam: E `TeammateSlam` → R stone/thud + S `power_boom` | BossGroundSlam: E `SlamShockwave` scaled → flat Ring + Dust + Debris |
| Bricks grow | BrickGrow: E `BrickSnap`×3 → E + S `brick_riser` | BrickGrow: E `LinkDust=211` → Dust circle + mesh bricks |
| Minion lands / damage tick | MinionLand: E `GoblinPile`/`Bonk`. MinionDamageTick: E `PlayerHurt` → S `minion_tick` (quiet, ≥ 0.5 s apart) | MinionLand: E `NinjaBrickPop`. MinionDamage: E `HitSpark=0` → small red Emissive Spark |
| Car smashes wall | WallSmash: E `VehicleBump`/`Breakthrough` → R `DESTRUCTION_Rocks_01` + E `VehicleBump` | WallSmash: E `VehicleBump=110`/`BruteBrickSpray` → sideways brick spray |
| Power-up | PowerUpPickup: E `PickupPower` (keep) | PowerUpGlint loop + PowerUpPickup: E `PickupSparkle=30`/`MechaPowerUp=145` |
| Rocket launch / hit | RocketLaunch: E `BallLaunch` → R `ROCKET_Launch_Deep_Short`. RocketHit: E `Explosion`×2 (keep) | RocketTrail loop → Lit Smoke + Glow Mote core. RocketHit: E `Explosion=20`/`EnemyExplosion=142` |
| Auto-laser | BossLaserLoop: E `LaserHum` → R `WEAPON_Energy_Beam_0x_loop` | E `LaserMuzzle=140` + `LaserImpact=141`, tinted |
| Boss hurt / tired | BossHurt: E `BossHit`×3 (keep) | BossHurt: E `BossHit=122`. Tired: E `DizzyStars=181` |
| Devil wind | DevilWind: E `FlyAway` → R `WIND_Storm_Blowing_Howling_*` cut to 1–1.5 s | DevilWind loop: E `SpinjitzuSwirl=102` tinted → violet Emissive Smoke orbit |
| Clap warning, wind | ClapWarning: E `LowHpWarning` → S `clap_alarm` | WindGust loop → streaks across the screen |
| Spike walls smash | SpikeWallSlam: E `Blocked`/`GuardBlock` → R `TRAP_Metal_Snap_Crunch`/`HAZARD_Hammer_Impact_01` + S `boss_thud` | SpikeWallSlam: E `ShieldBurst=180` → Flash + Spark + Debris |
| Boss explodes | BossExplode: E `BossDeath` (keep) or R `EXPLOSION_Long_*` | BossExplode: E `BossPoof=11` + `BrickBurst` at ×3 scale → staged multi-layer recipe |
| Victory | `Victory` (607) + `StingerType.Victory` | VictoryBurst: E `GateCleared=151` confetti |
| BGM phase 1 / 2 | BossBattle1: E `Boss`/`NinjaChase` → R `Frantic Pursuit LOOP` / `Boss Battle 2 Loop`. BossBattle2: E `Boss` → R `Boss Battle 3/4/5 Loop` / `Barren Boss LOOP` | — |

---

## 8. Gotchas

- **G1. Never run `Nex.BilliardRogue.Editor.VfxPrefabsBuilder.Run()`.** Its `AddPlaceholders` (l.86–95) makes a spark for every `VisualEffect`
  without a BilliardRogue recipe (all Ninjago keys 100–211 and the boss keys), and `VfxRegistryWriter.Register` then **overwrites** those
  registry entries with the sparks. `VfxRegistryWriter.Register(onlyYourRecipes, …)` on its own is safe.
- **G2. Never call `VfxMaterialLibrary.DeleteUnused()` from a partial builder.** It deletes every `M_Vfx_*` material the run did not create, which
  breaks the BilliardRogue prefabs.
- **G3. Enum values: explicit, append only, ascending in declaration order.** The editor sorts by declaration index (`EnumDictionaryEditorUtils.cs:62`).
  Renaming is safe (the int is stored); removing or renumbering deletes the registry entry and the runtime logs an error.
- **G4. The pool follows the root system.** An instance is reclaimed when its **root** stops (`VfxManager.cs:101`), and deactivation kills children
  still in flight, so the root's duration must cover them (`FitRootDuration`). A loop that is never `Stop()`ped never comes back.
- **G5. Changes to a pooled instance leak to the next user** (start colour, emission…; only scale is reset on every play). Reset them before
  `Stop()` (`MechaLane.cs:226-231`), and null your handle after: with `collectionCheck=false` a stale second `Stop()` stops someone else's effect.
- **G6. Layers.** BilliardRogue prefabs are on `World` (8), Ninjago prefabs on Default (0). Match the boss VFX layer to the boss camera stack: a
  `World`-only pixel camera hides Default VFX, and a camera that excludes `World` hides BilliardRogue VFX.
- **G7. The world scrolls; the vehicle stays put.** `RunnerWorld` moves rows and `ScrollingStrip`s toward −z (`RunnerWorld.cs:214-215, 247`) while
  the car and plane stay near z = 0. World-space particles spawned on the road (slam dust, brick-grow dust, crater) don't scroll with it and
  seem to follow the car. Use `simulationSpace = Local` and move the instance by the scroll distance each frame, or keep them under 0.4 s.
- **G8. Scale.** `PlayVisualEffect(…, scale)` scales only the root. Ninjago `NewSystem` prefabs keep `scalingMode = Local`, so child systems ignore
  it (e.g. the `Flash` child of `Vfx_EnemyExplosion`). Use Hierarchy for any boss prefab with child systems.
- **G9. SFX throttles.** 35 ms per effect, one start per clip per frame, and 6 pooled sources that steal the oldest, so simultaneous rocket hits or
  minion ticks merge or drop. The `(effect, AudioSource)` overload has no volume control.
- **G10. `KidsAudio.Import` settings.** `forceToMono=true` with `normalize=1` makes Unity peak-normalize every clip, undoing the `Tools/Audio`
  loudness matching, and `preloadAudioData` stays off, so the first play can hitch. Use the §6.4 helper, or call `clip.LoadAudioData()` in setup.
- **G11. `CrossFadeTo` always restarts at 0, and does nothing if that track is already playing.** Switching between stems (full mix and drums-only)
  for the 70/30/5 % wake beats would drift unless BgmManager copies `timeSamples`. Use `Duck` + stinger + roar for those beats instead.
- **G12. Headroom.** The README's −6 dB master trim was never added, so boss chains (3 hits + BossHurt + explosion over −18 LUFS music) can clip on
  Android. Keep SFX peaks ≤ −3 dBFS, don't stack `drive_db`, or add a limiter on the Master group (this needs the Editor).
- **G13. Builders and scenes.** `NinjagoVfxBuilder.NewSystem` creates objects in the active scene; `VfxPrefabWriter` uses a preview scene. Run builders
  through `Tools/editor_lock.sh` and `unity command eval` after a recompile, and never in play mode.
- **G14. LFS and staging.** New `.wav`, `.ogg` and `.png` files are stored in Git LFS, `Tools/Staging/` is gitignored, and `sync_staging.sh`
  targets a `Starter/` folder that doesn't exist here.
- **G15. The dark Phase 2 arena.** `Lit` shading depends on the main light. Use `Emissive` or `Glow` for the laser, devil wind, rocket core and
  explosions. Emissive bloom also needs a camera with HDR and post-process Bloom.

---

## 9. Low-end Android budget (60 fps)

- **Particle counts:** bursts ≤ 60 particles (the BilliardRogue `BurstParticleBudget`), loops ≤ 40 alive, `maxParticleSize = 0.5` (the factory
  default).
- **Overdraw:** alpha-clip LitParticle writes depth, so it avoids sorting overdraw. Use at most one Glow Flash per burst, and no big soft `Particle Soft` quads.
- **Modules:** no Lights or Trails modules. Collision only against the single `VfxFloorPlane` (world y = 0); only `.Bounce` if the road is at y = 0.
- **Shadows:** the Ninjago `MeshRenderer()` helper turns shadow casting On. Turn it off for boss debris, or use the `Debris` pixel sheet.
- **Pools:** `Prewarm` before each phase and size pools to the real maximum alive: rockets = largest salvo, devil wind ≈ 3 rows (lead 1.5 s /
  spacing 1.9 s), minion damage = 2 sides.
- **Audio formats:**
  - SFX: ADPCM, DecompressOnLoad, preloaded, mono.
  - BGM: Vorbis, Streaming; keep crossfades ≤ 1.5 s, since two streams decode during one.
  - Stingers: CompressedInMemory.

---

## 10. Checklist

- [ ] Append `SoundEffect` 800–816, `BgmType` 51–52 and `VisualEffect` 220–237 (§6.1–6.3) as explicit ascending values, then recompile with the
      unity pipeline.
- [ ] Add `BossFxCue` + `EnumDictionary<BossFxCue, BossFxEntry>` to the Boss Battle config SO (§6.6), filled with the day-1 placeholders from §7.
- [ ] Write `Ninjago/Editor/BossBattle/BossBattleAudioVfxBuilder.cs`: `BuildAudio` (preload on, mono not forced, one `EditPrefab`, `RegisterMusic`
      ×2) and `BuildVfx` (recipes + `VfxRegistryWriter.Register`; no `VfxPrefabsBuilder.Run()`, no `DeleteUnused()`). Hook it into
      `NinjagoBuildAll.Run()` and add a `[MenuItem]`.
- [ ] Choose the VFX layer to match the boss camera stack (G6).
- [ ] For loops: `.Stream()` + `main.loop = true`, pool size = maximum alive, and reset + `Stop()` from gameplay (G4, G5).
- [ ] For road-anchored effects: Local simulation space and scroll them, or keep them under 0.4 s (G7).
- [ ] Python: `python3 -m venv Tools/.venv && Tools/.venv/bin/pip install numpy scipy pillow`.
- [ ] Write `boss_picks.py` + `build_boss_audio.py`, add the presets `rock_whistle`, `clap_alarm`, `brick_riser`, `minion_tick`, and audition the
      R picks and BGM with `audition.py`.
- [ ] Copy the WAV/OGG files into `ninjago-unity/Assets/Audio/...` and re-run the builder. Verify that `HasClips` is true and that there are no
      `[VfxManager] No prefab registered` warnings.
- [ ] Gameplay audio calls: phase 1 `CrossFadeTo(BossBattle1)`; cutscene `PlayStinger(BossAppear)` + `BossRoar`, then `CrossFadeTo(BossBattle2)`;
      wake beats `Duck` + roar + `BossWake` VFX; win `BossExplode`, then `PlayStinger(Victory)` + `SoundEffect.Victory`.
- [ ] Prewarm VFX pools during the setup/calibration view. Call `LoadAudioData()` there for any clips that are not preloaded.
- [ ] Hand gameplay-relevant SFX/VFX events to the analytics owner, and optionally add a debug "audition cue" list.
- [ ] Low-end Android pass: particle counts, Glow overdraw, no mesh-particle shadows, and a Frame Debugger batch check.
