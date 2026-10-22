# Boss Battle — Technical Design (v2, after design review)

Read: `GDD.md` (what), this file (how), and the research note for your area in `research/` (exact APIs, line numbers, gotchas).
`R = /Users/oscar/gitfolder/ninjago/ninjago-dragon-rising`, Unity project `R/ninjago-unity` (6000.3.9f1, URP 17.3, Android,
Gamma, GLES3). One assembly: all runtime code is `Assembly-CSharp`, editor code `Assembly-CSharp-Editor` (no asmdefs).

## 0. Ground rules (every package)

- **House style** of `Assets/Scripts/Ninjago`: `#nullable enable`; namespace `Nex.Ninjago` (editor `Nex.Ninjago.Editor`, tests
  `Nex.Ninjago.Editor.Tests`); `[Header("Readable Name")]` above every serialized field; `[Tooltip]` on tunables;
  `[SerializeField] T x = null!;` for references; `#region` blocks; XML `<summary>` on public types; UniTask + CancellationToken;
  no LINQ/allocations/string formatting in per-frame paths; comment density like `Chase/RunnerWorld.cs`.
- **Append-only, explicit, ascending enum values** for every enum that is saved or used as an EnumDictionary key.
- **Ownership (§10)**: edit only files your package owns. "Append" files: additive edits only; re-read right before editing.
  Need something from another package? Code against the F contract and write it in your report.
- **Never**: touch `Assets/Materials/Ninjago/Build *.mat` (user's WIP); `git add`/`git commit` (the lead commits per package);
  run `Tools/sync_staging.sh`, `Nex.BilliardRogue.Editor.VfxPrefabsBuilder.Run()`, `VfxMaterialLibrary.DeleteUnused()`,
  `NinjagoBuildAll.Run()`; edit shared BilliardRogue assets, `URPAsset*.asset`, `Volume_LowTier`, `M_LightShaft.mat`, the shared
  `Assets/Materials/Ninjago/*.mat`, `NinjagoSetupView.prefab`, `NinjagoResultView.prefab`, `RunnerWorld.prefab`, chapter prefabs.
- **Keep the tree compiling at all times**: write whole files, run `python3 Tools/compile_check.py` (≈3 s, no Editor) after each
  batch of edits, fix your errors immediately. If it fails only in another package's file, wait 60 s and retry; never edit it.
- **The Editor is shared**: anything that mutates it (recompile, eval/run_script that writes assets, play mode, run_tests) runs
  under `Tools/editor_lock.sh <label> <cmd…>` (or `ulock` from `Tools/unity-cli-helpers.sh`); keep each lock hold short (< 10 min).
  Before a recompile, compile_check must pass. Read-only queries need no lock. Cookbook: `research/cli-playtest.md`.
- Python: `Tools/.venv/bin/python` (numpy, pillow, scipy, fonttools). Blender:
  `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 --python X.py -- …`.
- Assets only through Editor APIs (builders run via `unity command eval`), never hand-written YAML/.meta.
- Everything the world camera draws is on **layer 8 `World`**. No world-space canvases in the arena.
- Read tuning only from `GameConfigsManager.Instance.BossBattleConfig` at runtime (one copy, debug rows edit it live).
- `EnumDictionary` values that are classes must be filled by a static default factory (field initializer + `Reset()`); readers
  use `TryGetValue` and treat missing/null as "default/none".

## 1. Boot chain and flow (U)

MainScene `SingletonSpawner` → menu (`GameModeSelectionView`, Boss Battle entry) → `GameConfigsManager.SelectedMode = 15` →
GameScene `SingletonSpawner` (activatePostSpawn `GameDriver`) → `GameDriver` loads the Addressable `BossBattleGame.prefab`,
`DetectionManager.Initialize(NumOfPlayers)`, `game.Initialize(detection, views)`, `RunAsync`. CLI/debug entry points always go
through this chain.

`BossBattleGame : NinjagoGame` — `Mode => GameModeType.BossBattle`; `PlayAsync` throws `NotSupportedException` (like
ChapterGame); custom `RunAsync`. Serialized field names must not collide with NinjagoGame's private fields
(`detectionEnginePrefab, enginesRoot, setupViewPrefab, resultViewPrefab, playersConfig`): use `startViewPrefab,
bossSetupViewPrefab` (set into the base `setupViewPrefab` by the builder), `introViewPrefab, roadViewPrefab, transformViewPrefab,
skyViewPrefab, finaleViewPrefab, knockoutViewPrefab, pauseViewPrefab, bossResultViewPrefab, arena, rig, hud`.

`Initialize`: base.Initialize → `rig.Initialize(Views.RootViewCamera)` → tier resolution
(`if (rig.Tier == Low) rig.Display.SetVisibleHeight(visual.lowTierHeight)`) → `hud.Initialize(...)` (PiP initialized **once**) →
`arena.Initialize(config, rig, hud, look, log)` (prewarms pools, VfxManager.Prewarm per cue, AudioClip.LoadAudioData) →
`SetWorldVisible(false)` → mode-scoped camera fix (§6.4) → subscribe `Views.SecretCodeEntered` (log `ui_button_click secret_code`)
and `CherryIntegrationManager.PreferGameStopped` (pause path).

```
RunAsync:
  lastMiniGame = BossBattle (ScopedNinjagoProgressUpdate)
  push StartView (depth 1) ──Back→ return (exit mode)
  loop:
    choice = await start.ChooseAsync()   // Continue | New | Exit ; New clears the save immediately
    attempt loop:
      SetWorldVisible(false); Detection.SetPreviewTextureInterval(0)
      players = await CalibrateAsync(push: setupDepth2NotOnStack)   // push from start (depth 2) or Replace result (depth 2)
      players == null → PopView to start view; continue outer loop
      state = continue ? BossRunState.FromSave(save, config) : BossRunState.New(difficulty, players.Count)
      MiniGameStart(is_continue); arena.BeginAttempt(players, state); SetWorldVisible(true)
      Detection.SetPreviewTextureInterval(visual PiP interval for rig.Tier)
      end = await PlayPhasesAsync(state)       // Intro/Road/Transform/Sky/Finale or Knockout views, each ReplaceView depth 2
      MiniGameEnd(...); bests; save or clear
      SetWorldVisible(false); Detection.SetPreviewTextureInterval(0)
      end == Quit → PopView to start; break attempt loop
      result = ReplaceView(ResultView) → Continue | Retry | Menu
      Continue → state from save, loop (setup replaces result) ; Retry → New ; Menu → PopView to start
  finally: BossTimeScale.Reset(); BgmManager.FadeOut(); preview interval 0
```
- **Phase view swaps** wait `UniTask.WhenAny(until !Views.IsInTransition && TopView == currentPhaseView, quitTask)` before
  `ReplaceView`, so a pushed Pause/Debug/tracking-lost view is never replaced.
- **Back** (all no-UI step views derive `BossPhaseView`): `if (!IsActive) return; UiAction(screen,"back"); BackRequested`.
  Game: in a cutscene already seen → skip to its end state; else push Pause. Start: Back = exit. Result: Back = Menu. Pause:
  Back = Resume. Keyboard: Start = vertical `GroupKeyResponder` (initial Continue if a save exists, else New); Pause initial
  Resume; Result initial Continue after a knockout (Continue on Easy after 2 knockouts in a phase), Retry after a win; step
  views use `BackProxy`.
- **Pause**: push `BossPauseView` first, then `BossTimeScale.SetPaused(true)`; Resume → `SetPaused(false)` then PopView. Quit →
  SetPaused(false), cancel the linked CTS of the running phase/cutscene (game maps `OperationCanceledException` → Quit), save,
  MiniGameEnd("abandon"). App pause (`OnApplicationPause(true)`) → save last checkpoint snapshot + pause path if a step view is top.
  `AnalyticsManager.TrackPause/TrackResume` + `boss_pause/resume`.
- **Tracking lost**: arena reports a calibrated player unseen ≥ 2 s (skipped when `SimulatedBody.IsEnabled`) → game pauses
  (`BossTimeScale`), `CalibrateAsync(push:true)` over the step view (depth 3); success → PopView, 2 s grace + i-frames, resume;
  Back → Pause. Log `boss_tracking_lost`.
- `BossTimeScale` (F, static) is the **only** writer of `Time.timeScale`: `SetPaused`, `SetSlowMo(scale)`, `HitStopAsync(sec)`,
  applies `paused ? 0 : slowMo × hitStop`, `Reset()` → 1. All boss views' MMF players use unscaled timescale (builder sets
  `ForceTimescaleMode`/`Unscaled`). Gameplay and cutscenes use scaled time (pause freezes them).

## 2. Enum appends (F)

| Enum | Append |
|---|---|
| `GameModeType` | `BossBattle = 15` |
| `View.ViewIdentifier` (implicit, append at end) | `BossBattleStart, BossIntro, BossRoad, BossTransform, BossSky, BossFinale, BossKnockout, BossPause, BossBattleResult, BossSetup` |
| `SfxManager.SoundEffect` | `BossRockThrow = 800, BossRockWhistle = 801, BossRockLock = 802, BossRockImpact = 803, BossRoar = 804, BossGroundSlam = 805, BossBrickGrow = 806, BossMinionThrow = 807, BossMinionLand = 808, BossMinionHit = 809, BossWallGrow = 810, BossWallSmash = 811, BossPowerUpSpawn = 812, BossPowerUpPickup = 813, BossRocketLaunch = 814, BossRocketHit = 815, BossHurt = 816, BossTakeOff = 817, BossTransform = 818, BossDevilWind = 819, BossTired = 820, BossLaserLoop = 821, BossLaserHit = 822, BossWake = 823, BossClapInhale = 824, BossClapWarning = 825, BossSpikeSlam = 826, BossExplode = 827, BossVehicleHit = 828, BossKnockout = 829, BossBannerSlam = 830, BossLaserDeflect = 831, BossCountdown = 832, BossLaugh = 833, BossCrackGlow = 834` |
| `BgmManager.BgmType` | `BossBattleRoad = 51, BossBattleSky = 52` |
| `VfxManager.VisualEffect` | `BossRockTrail = 300, BossRockImpact = 301, BossSlamShockwave = 302, BossBrickGrow = 303, BossCrackGlow = 304, BossMinionLand = 305, BossMinionHit = 306, BossWallSmash = 307, BossPowerUpGlint = 308, BossPowerUpPickup = 309, BossRocketTrail = 310, BossRocketHit = 311, BossHurt = 312, BossTakeOff = 313, BossTransformBurst = 314, BossDevilWind = 315, BossGustPass = 316, BossTiredZzz = 317, BossLaserMuzzle = 318, BossLaserHit = 319, BossWake = 320, BossClapPush = 321, BossSpikeSlam = 322, BossExplodeSmall = 323, BossExplodeBig = 324, BossVictoryConfetti = 325, BossVehicleHit = 326, BossEmbers = 327, BossStormDust = 328, BossLaserDeflect = 329, BossRebuildSparkle = 330, BossLightning = 331` |

After appending, F runs `Nex.BilliardRogue.Editor.EnumDictionaryRepair.Run()` under the lock so new keys get **empty** values
(Unity's InsertArrayElementAtIndex otherwise copies the neighbour: Forge's game prefab, WayOpen's clips, LinkDust's prefab).
V and I later write every boss key explicitly (empty when the asset is missing).

## 3. Contracts (F writes all of these; every other package compiles against F only)

### 3.1 Enums (`BossBattle/Core/BossBattleEnums.cs`, explicit ints)
```csharp
public enum BossPhase { Road = 0, Sky = 1 }
public enum BossDifficulty { Easy = 0, Normal = 1 }
public enum BossBeat { Breather = 0, GroundSlam = 1, RockDrop = 2, MinionThrow = 3, PowerUp = 4, Ready = 5, SteerCheck = 6,
                       DevilWind = 10, LaserWindow = 11, Clap = 12, RiseDuck = 13 }
public enum BossHazard { Brick = 0, Rock = 1, Minion = 2, Wind = 3, SpikeWall = 4 }
public enum MinionSides { Left = 1, Right = 2, Both = 3 }
public enum BossPhaseOutcome { Cleared = 0, KnockedOut = 1, Quit = 2 }
public enum BossRunEnd { Won = 0, KnockedOut = 1, Quit = 2 }
public enum BossLook { Dusk = 0, Night = 1, Dawn = 2 }
public enum BossCutscene { Intro = 0, Transform = 1, Finale = 2, Knockout = 3, SkyRejoin = 4 }
public enum BossAttackIcon { None = 0, Rock = 1, Minion = 2, Slam = 3, Wind = 4, Clap = 5, Tired = 6 }
public enum PromptArrow { None = 0, Left = 1, Right = 2, Up = 3, Down = 4, LeftRight = 5, UpDown = 6 }
public enum BossPromptId { LeanToSteer = 0, GrabRockets = 1, DodgeRock = 2, SmashLeft = 3, SmashRight = 4, SmashBoth = 5,
    DodgeBricks = 6, RiseAndDuck = 7, DodgeWind = 8, TiredFire = 9, FlyIntoRing = 10, OnTarget = 11, Dive = 12, Brace = 13,
    FinishIt = 14, Enraged = 15, MinionHurry = 16, GetReady = 17 }
public enum BossBannerId { BossName = 0, Phase1 = 1, Phase2 = 2, Victory = 3, KnockedOut = 4, Wake = 5 }
public enum BossPropKind { Brick = 0, Rock = 1, Reticle = 2, CrackGlow = 3, SideWall = 4, Minion = 5, PowerCrate = 6, Rocket = 7,
                           WindGust = 10, LaserBolt = 11, SpikeWall = 12, TargetRing = 13 }
public enum BossFxCue { RockThrow = 0, RockWhistle = 1, RockWarning = 2, RockLock = 3, RockImpact = 4, SlamWindup = 5,
    GroundSlam = 6, CrackGlow = 7, BrickGrow = 8, MinionThrow = 9, MinionLand = 10, MinionHit = 11, WallGrow = 12,
    WallSmash = 13, PowerUpSpawn = 14, PowerUpPickup = 15, RocketLaunch = 16, RocketHit = 17, BossHurt = 18, BossRoar = 19,
    TakeOff = 20, Transform = 21, DevilWind = 22, GustPass = 23, Tired = 24, LaserLoop = 25, LaserHit = 26, LaserDeflect = 27,
    Wake = 28, ClapInhale = 29, ClapPush = 30, ClapWarning = 31, SpikeSlam = 32, ExplodeSmall = 33, ExplodeBig = 34,
    Victory = 35, VehicleHit = 36, Knockout = 37, BannerSlam = 38, Enrage = 39, Countdown = 40, Laugh = 41, Lightning = 42,
    RebuildSparkle = 43, LaserMuzzle = 44, Embers = 45, StormDust = 46 }
```

### 3.2 Config (`BossBattleConfig : ScriptableObject`, menu `Nex/Ninjago/Boss Battle Config`)
All numbers = GDD v2. Public getters; setters for debug rows. `[Serializable]` sections (classes), each with a static
`Default…()` factory used by field initializers and `Reset()`:
- `SteerSettings steer` {deadzoneInches 1.5, fullLeanInches 7, fullRiseInches 4, playerOneShare 0.5}
- `EnumDictionary<BossPhase, ChaseConfig.VehicleSettings> vehicles` — Road {speed 14, halfWidth 2.2, centerHeight 0,
  steerSmoothing 0.35, maxSteerSpeed 7, cellWidth 2, vehicleHalfExtents (0.7, 0.3)}; Sky {speed 13, usesVerticalLean,
  halfWidth 2.2, centerHeight 3.2, halfHeight 1.6, steerSmoothing 0.3, maxSteerSpeed 7, cellWidth 2, cellHeight 1.6,
  vehicleHalfExtents (0.6, 0.25)}
- `EnumDictionary<BossDifficulty, DifficultySettings>` {hearts 8/5, timingScale 1.25/1, laserRadiusScale 1.3/1}
- `EnumDictionary<BossHazard, int> hazardDamage` {1,1,1,1,2}; `invulnerableSeconds 1`, `hitStopSeconds 0.06`,
  `bumpSpeedDrop 0.35`, `bumpRecoverSeconds 1.2`, `healPerVolley 1`, `healPerWake 1`, `trackingLostSeconds 2`
- `RoadSettings road` {bossDistance 30, bossSwayX 1.2, bossSwayPeriod 6, readySeconds 3, steerCheckReach 1.6,
  steerCheckMaxSeconds 8, breatherSeconds 1.2, enrageFraction 0.3, enrageWindupScale 0.85, `List<BeatEntry> script`,
  loopFrom 9 (0-based)}
- `RockSettings rock` {windup 0.8, air 1.0, follow 1.6, followSmooth 0.25, lock 1.2, firstLock 1.6, fall 0.35, rockRadius 0.55,
  nextLockAfterImpact 1.5, overlapMinGapFromRow 0.8}
- `SlamSettings slam` {windup 1.0, crackSeconds 0.45, growSeconds 0.3, finishZSeconds 1.5 (grown at speed×1.5 = 21 m),
  `List<float> rowSpacingBySlam` 1.9, 1.7, 1.7, 1.5, maxLaneJumpBelow 1.8, firstMasks [0b101, 0b110]}
- `MinionSettings minion` {windup 0.9, flight 1.0, grace 2.5, tick 2.5, maxTicks 2, first: windup 1.2, flight 1.6, grace 4,
  tick 3.5, maxTicks 1; wallGrow 0.6, walledReach 2.45, smashX 2.0, smashMinOutwardSpeed 0.5, soloSmashSteer 0.82,
  soloDriveSeconds 0.25, bothGrace 3.5, bothSecondStartMax 3, roofOffset (0.55, 1.0, −0.2)}
- `PowerUpSettings powerUp` {`List<RocketVolley> volleys` (1,1),(2,2),(3,2),(3,2),(3,2); baseRocketDamage 10 (→ RoadHp 230);
  pickupsToPhase2 5; leadSeconds 0.3; spawnLeadSeconds 1.6; rocketFlightSeconds 1.0; rocketStagger 0.15; pickupHalfWidth 0.9}
- `SkySettings sky` {bossDistance 30, readySeconds 3, riseDuckHigh 4.2, riseDuckLow 2.2, riseDuckMaxSeconds 8, skyHp 100,
  `List<float> wakeThresholds` 0.7, 0.3, 0.05, tiredSeconds 1.2, wakeSeconds 1.2, `List<BeatEntry> script`, timeoutRepeatGusts 3}
- `WindSettings wind` {windup 0.8, spacingStart 1.9, spacingMin 1.6, spacingStepPerBarrage 0.1, leadSeconds 1.5,
  frameLeadSeconds 1.0, noClimbDiveBelow 1.8}
- `LaserSettings laser` {fireInterval 0.125, `List<float> boltDamageByWindow` 0.7, 0.9, 0.6, 0.25, hitRadius 0.95,
  assistStart 6, assistFull 12, assistScale 1.4, pathHalfSize (1.8, 1.0), pathPeriod 8, maxWindowSeconds 18}
- `ClapSettings clap` {inhale 0.8, push 1.2, wallRise 0.5, `List<ClapSize> sizes` {band 0.45, warn 1.8},{band 0.7, warn 2.2},
  slam 0.22, recover 1.0, wallX 3.6}
- `CutsceneSettings cutscenes` {intro 5, transform 6.5, finale 6, knockout 3, skyRejoin 2, slowMoScale 0.35, slowMoSeconds 0.8,
  knockoutSlowMo 0.4, letterboxPixels 96}
- `EnumDictionary<BossFxCue, FxEntry> fx` — `FxEntry` {bool hasSound; SfxManager.SoundEffect sound; float volume = 1;
  bool loopSound; bool hasVisual; VfxManager.VisualEffect visual; float visualScale = 1; bool loopVisual; float shake;
  float shakeSeconds; bool pointLight; Color lightColor; float lightIntensity}; defaults map each cue to its §2 enum.
- `EnumDictionary<BossPromptId, PromptEntry>` {string key (loc key), Color color, float seconds, PromptArrow arrow}
- `EnumDictionary<BossAttackIcon, Color> tellColors` (rock orange, minion green, slam red, wind violet, clap white, HDR ≥ 2.5)

`BeatEntry` {BossBeat beat; int count (≤ 0 → 1); MinionSides sides; int size; bool overlapNext; bool skipBreather;
float extraDelay}. `RocketVolley` {int rockets; float damageMultiplier}. `ClapSize` {float band; float warnSeconds}.
Default road script = GDD §4.5; sky script = GDD §6.4 (Ready, RiseDuck, Wind 4, Laser, Clap 0, Wind 4, Laser, Clap 1, Wind 5,
Laser, Clap 1 overlap Wind 3, Laser).

`BossBattleVisualConfig : ScriptableObject` (R fills values, F declares the class): `EnumDictionary<BossLook, PhaseLook>`
{VolumeProfile profile; Color sunColor; float sunIntensity; Vector3 sunEuler; Color ambientSky, ambientEquator, ambientGround;
Color fogColor; float fogDensity; Color clearColor; Color shaftColor; float tiltCenter; float tiltHalfWidth};
`EnumDictionary<BossPhase, CameraPose>` {Vector3 position; float pitch; float fov} — Road (0, 4, −11) 9° 50, Sky (0, 3.4, −8)
2° 50; `EnumDictionary<BossCutscene, List<CameraShot>>` {Vector3 position; Vector3 euler; float fov; float seconds;
bool cut}; `int lowTierHeight 360`, `degradeGpuMs 15`, flash settings.

### 3.3 Pure logic (F, EditMode-tested in `Editor/Tests/BossBattleCoreTests.cs`)
- `BossHealth(float max)`: `Max, Current, Fraction, IsDepleted`, `float Damage(float amount, float floorFraction = 0f)` (clamps
  at the floor, returns dealt), `SetFraction(float)`, `event Action? Changed`.
- `RocketVolleys` (static): `RocketsFor(list, pickupIndex)`, `DamagePerRocket(list, pickupIndex, baseDamage)`,
  `TotalHp(list, count, baseDamage)`, `HpAfter(list, pickups, baseDamage)`; index past the list reuses the last entry.
- `BeatScript(IReadOnlyList<BeatEntry> script, int loopFrom, int startIndex, bool loops)`: `int Index` (index of the beat
  `Next` will return), `BeatEntry Next()`, `RequeuePowerUpAfterNextAttack()`, `bool Finished`.
- `VehicleSteerer`: `Configure(ChaseConfig.VehicleSettings, SteerSettings)`, `Reset(Vector2 position)`,
  `Step(Vector2?[] steersPerPlayer, float dt)` (SharedSteer blend → target → per-axis SmoothDamp, as RunnerWorld.Step),
  `Vector2 Position, Velocity, Target`, `Vector2? PlayerSteer(int)`, `float? ForcedX, ForcedY` (override targets),
  `float ReachLeft, ReachRight` (default halfWidth), `bool VerticalEnabled`, `int DominantPlayer`, `float SteerShare(int)`,
  `float Contribution(int)`.
- `ControllerFilter`: `int Update(float c0, float c1, int activeCount, float dt)` → −1/0/1 per GDD §2 (lead 0.2 for 0.6 s,
  neutral below 0.15); `bool Changed`.
- `LaneGrid` (static): `CellCenter(settings, column, row, isSky)`, `Overlaps(settings, position, mask, isSky, cellFill)`.
- `SlamPatternPicker(int seed)`: `int Next(float spacingSeconds, int slamIndexInRun, int rowIndex)` (first-slam fixed masks,
  max lane jump 1 under 1.8 s).
- `WindPatternPicker(int seed)`: `int Next(int barrageIndex, float spacing)` (single-axis masks in barrage 0, no climb↔dive
  back to back under 1.8 s, ≥ 1 free cell).
- `RockSchedule` (static): `float PlaceImpact(float desired, IReadOnlyList<float> rowArrivals, float minGap)`.
- `RockReticle`: `Start(float carX, RockSettings, bool first, float timingScale)`, `Tick(dt, carX)`, `State`
  (Hidden/Following/Locked/Falling/Done), `X`, `ImpactNow` (one frame), `IsHit(carX, carHalfWidth, rockRadius)`.
- `SideMinionState`: `Start(side, isFirst, MinionSettings, float timingScale)`, `int Tick(float dt)` (damage ticks this frame),
  `bool WallReady`, `bool TryClear(float vehicleX, float vehicleVelX, bool playerSoloSmash)`, `bool GaveUp`, `bool Cleared`.
- `LemniscatePath(Vector2 halfSize, float period, Vector2 centre)`: `Vector2 Evaluate(float t)` = centre + (hx·sin ωt,
  hy·sin 2ωt); bounds exactly ±halfSize.
- `LaserAim` (static): `OnTarget(Vector2 plane, Vector2 target, float radius)`, `AssistRadius(baseR, elapsed, LaserSettings)`,
  `Vector3 CoreOnCameraRay(Vector3 cameraLocal, Vector3 ringLocal, float bossZ)`.
- `ClapWalls(ClapSize size, float gridBottom 1.6, float gridTop 4.8)`: `BandBottom`, `IsHit(planeY, halfExtentY)`.
- `BossBattleStats`: hits per hazard, pickups, rocketsFired, boltsFired/Hit, minionsCleared/Failed, claps survived, elapsed,
  `HitsTaken`, `Accuracy`.
- `BossRunState`: `runId, phase, difficulty, numPlayers, hearts, maxHearts, RoadHealth, SkyHealth, pickupsCollected,
  wakesPassed, clapsDone, minionsThrown, beatIndex, isContinue, seenIntro…`, `Stats`; `static New(config, difficulty, players)`,
  `static FromSave(BossBattleSave, config)` (RoadHealth = `HpAfter(pickups)`, SkyHealth fraction = threshold[wakes−1] or 1),
  `BossBattleSave ToSave()`.
- `BossTimeScale` (static): see §1.

### 3.4 Runtime interfaces (F) — `BossBattle/Core/Contracts/*.cs`
```csharp
public interface IBossArena
{
    BossBattleConfig Config { get; }
    DifficultySettings Difficulty { get; }
    BossRunState State { get; }
    BossPhase Phase { get; }
    Transform Root { get; }                    // arena local space: vehicle at z = 0, +z ahead, x right, y up
    IReadOnlyList<PlayerBody> Players { get; }
    IBossActor Boss { get; }
    VehicleRig CarRig { get; }
    VehicleRig PlaneRig { get; }
    VehicleRig Vehicle { get; }                // active rig
    VehicleSteerer Steerer { get; }
    Vector2 VehiclePosition { get; }
    float GroundSpeed { get; }                 // m/s the ground moves toward −z (bumps included)
    bool SteeringEnabled { get; set; }         // false: rigs are not posed by the steerer (cutscenes); true resets steerer to rig pose
    bool IsInvulnerable { get; }
    float WindupScale { get; }                 // difficulty timing × enrage (wind-ups only)
    float TimingScale { get; }                 // difficulty timing (telegraph/lock/warn/spacing)
    IBossPools Pools { get; }
    IBossCamera Camera { get; }
    IBossLook Look { get; }
    IBossHud Hud { get; }
    BossBattleLog Log { get; }
    event Action<BossHazard>? VehicleHit;
    event Action? HeartsChanged;
    event Action<int>? TrackingLost;           // player index unseen ≥ trackingLostSeconds
    void BeginAttempt(IReadOnlyList<PlayerBody> players, BossRunState state); // reset props, hearts, boss visual, look, phase instantly
    void EnterPhase(BossPhase phase, bool instant);                            // vehicle, scenery, base camera pose, look
    void HitVehicle(BossHazard hazard, Vector3 localPoint);  // hearts (god mode/i-frames aware), bump, hit-stop, fx, log
    void Heal(int hearts);
    void PlayFx(BossFxCue cue, Vector3 localPoint, float scale = 1f, bool groundAnchored = false);
    BossFxHandle StartFx(BossFxCue cue, Transform follow, Vector3 localOffset);
    void StopFx(ref BossFxHandle handle);
    Vector3 ToWorld(Vector3 localPoint);
    Vector3 ToLocal(Vector3 worldPoint);
    UniTask WaitAsync(float seconds, CancellationToken cancellationToken);   // scaled time (pause/slow-mo aware)
    void SaveCheckpoint(int nextBeatIndex);                                   // only at calm moments
}

public interface IBossActor
{
    Transform Transform { get; }
    Vector3 ChestPoint { get; }    // world; rockets aim here
    Vector3 CorePoint { get; }     // world; laser weak point
    Vector3 MouthPoint { get; }
    Vector3 FeetPoint { get; }
    Vector3 HeadTopPoint { get; }  // attack icon anchor
    Vector3 HandPoint(bool left);
    void Initialize(BossBattleConfig config);
    void ResetVisual(BossPhase phase);
    void SetAnchor(Vector3 localPosition);          // arena base placement each frame; actor adds sway + action offsets
    void SetCoreTarget(Vector3? localCorePosition); // phase 2 laser: move so the Core sits here (null = free hover)
    void SetTell(BossAttackIcon icon);              // hand/mouth colour tell (None clears)
    UniTask RoarAsync(float seconds, CancellationToken cancellationToken);
    UniTask ThrowAsync(bool left, float windupSeconds, CancellationToken cancellationToken);   // returns at release
    UniTask SlamAsync(float windupSeconds, CancellationToken cancellationToken);               // returns at impact
    UniTask BlowAsync(float windupSeconds, CancellationToken cancellationToken);               // returns when blowing starts
    void StopBlowing();
    UniTask InhaleAsync(float seconds, CancellationToken cancellationToken);
    UniTask ClapAsync(float seconds, CancellationToken cancellationToken);
    UniTask TakeOffAsync(Vector3 toLocal, float seconds, CancellationToken cancellationToken);
    UniTask LandAsync(Vector3 fromLocal, Vector3 toLocal, float seconds, CancellationToken cancellationToken);
    UniTask LaughAsync(float seconds, CancellationToken cancellationToken);
    UniTask ExplodeAsync(CancellationToken cancellationToken);
    void SetTired(bool tired);
    void SetEnraged(bool enraged);
    void Flinch(float strength);
    void SetVisible(bool visible);
}

public interface IBossPools
{
    T Get<T>(BossPropKind kind) where T : PooledProp;
    void ReleaseAll();
}
public abstract class PooledProp : MonoBehaviour, IPoolableObject   // BossBattle/Core/Contracts/PooledProp.cs
{   // [Header("Kind")] [SerializeField] BossPropKind kind; public BossPropKind Kind; public event Action<Component>? OnRelease;
    // public void Release() (idempotent); protected virtual void OnSpawned(); protected virtual void OnReleased();
    // internal void NotifySpawned() called by the pool. Props move themselves; ground props scroll by arena.GroundSpeed. }

public interface IBossCamera
{
    void SetBasePose(BossPhase phase, float blendSeconds);
    void Cut(Vector3 localPosition, Vector3 euler, float fieldOfView);
    UniTask MoveAsync(Vector3 localPosition, Vector3 euler, float fieldOfView, float seconds, bool easeInOut, CancellationToken cancellationToken);
    UniTask PlayShotsAsync(IReadOnlyList<CameraShot> shots, CancellationToken cancellationToken);
    void ClearOverride(float blendSeconds);
    void Shake(float amount, float seconds);       // scaled by PlayerPreference screen shake
    void SetFocus(Transform? subject);             // PixelWorldDisplay.SetFocusPoint target (null = vehicle)
}

public interface IBossLook
{
    BossLook Current { get; }
    void Apply(BossLook look, float seconds);
    void Flash(float intensity, float seconds);
    void Lightning();
    void SetTiltCenter(float viewportY);           // phase 2 follows the plane (smoothed by the implementation)
    void PulseLight(Vector3 worldPoint, Color color, float intensity, float seconds);   // pooled no-shadow point lights
}

public interface IBossHud
{
    void SetVisible(bool visible);
    void SetCutsceneMode(bool cutscene, float seconds);   // letterbox on, bars/hearts/prompts hidden, PiP kept
    void SetBossBar(BossPhase phase, float fraction, bool animateRefill);
    void ShowDamagePopup(Vector3 worldPoint, int multiplier);
    void SetHearts(int hearts, int maxHearts);
    void SetPickups(int collected, int total);
    void ShowPrompt(BossPromptId id, float seconds = -1f);   // −1 = config default
    void HidePrompt();
    void ShowBanner(BossBannerId id, float seconds);
    void SetCountdown(int value);                    // 3,2,1, 0 hides
    void SetVehicleAnchor(Transform? vehicle);       // P1/P2 tags follow it
    void SetController(int playerIndex);             // filtered controller (−1 neutral); updates PiP + tags
    void SetRockWarning(int slot, bool visible, bool locked);   // "!" over the vehicle
    void SetAttackIcon(BossAttackIcon icon);         // over the boss head
    void SetTargetRing(bool visible, Vector3 worldPoint, float worldRadius, bool onTarget);
    void SetCellFrames(bool visible, int blockedMask, float localZ);
    void SetPipDimmed(bool dimmed);
}

public struct BossFxHandle { public ParticleSystem? system; public Transform? follow; public Vector3 offset;
                             public bool hasLoopSound; public SfxManager.SoundEffect loopSound; public bool IsValid => …; }
```

### 3.5 Persistence, analytics, debug (F)
- `BossBattleSave` ([Preserve], public fields, append-only, `version = 1`): runId, numPlayers, difficulty, phase, beatIndex,
  pickupsCollected, wakesPassed, clapsDone, minionsThrown, skyHpFraction, elapsedSeconds, hitsTaken, rocketsFired, boltsFired,
  boltsHit, knockoutsThisPhase, savedAtUtcTicks. `PlayerDataManager` region key `bossBattleSave`: `HasBossBattleSave`,
  `LoadBossBattleSave()` (null + delete on unreadable/wrong version), `SaveBossBattle(BossBattleSave)`, `ClearBossBattleSave()`.
- `NinjagoProgress` appends: `bossBattleRuns, bossBattleWins, bossBattleBestSeconds = -1, bossBattleFurthestPhase,
  bossBattleFewestHits = -1, bossBattleKnockouts`. `PlayerPreference` appends: `bossDifficulty = 1`, `bossSeenIntro`,
  `bossSeenTransform`.
- `BossBattleLog` (instance; guards a missing AnalyticsManager): every GDD §14 event, common props run_id, phase, boss_hp,
  hearts, elapsed_s, players, difficulty, is_continue. Never per bolt/tick.
- `NinjagoAnalytics.ContentName`: `BossBattle => "boss_battle"`.
- `GameConfigsManager.cs` append: `[SerializeField] BossBattleConfig bossBattleConfig = null!;` +
  `public BossBattleConfig BossBattleConfig => bossBattleConfig;` (I wires the prefab).
- `BossBattleDebugHooks` (static Func handlers registered by the live game, cleared with ReferenceEquals): SkipToSky,
  SetBossHpPercent(float), GrantPowerUp, ForceBeat(int), WinNow, KnockOutNow, Status() → string; public read-only
  `CurrentPhase, CurrentBeat, BossHpPercent, Hearts, Pickups` for CLI wait_for.
- `DebugSettings.cs` rows at DebugOrder −1100…−1050: saved `bossGodMode`, `bossSkipCutscenes`, `bossStartPhase`,
  `bossForceBeat` (−1 off); [ES3NonSerializable] config properties (hearts Normal, rock lock, minion tick, slam spacing, wind
  spacing, laser damage scale, laser radius, clap warn); methods BossSkipToSky, BossDamage10, BossGrantPowerUp, BossWinNow,
  BossKnockOut, [SaveBeforeInvoking] BossClearSave. A `DebugPrinter` "Boss" line (phase/beat/hp/hearts).
- `Editor/BossBattle/BossBattleConfigBuilder.cs` creates/updates `Assets/Configs/Ninjago/BossBattle/BossBattleConfig.asset`
  and an empty `BossBattleVisualConfig.asset` (R fills it) — keeps GUIDs, rewrites defaults only when asked.
- `Editor/BossBattle/BossToon.cs` (shared editor material factory): `Toon(name, color, emission?, bands = 4, shadowTint?, rim?)`,
  `Surface(name, sourceSurface, tiling, bump, cavity)`, `Glow(name, hdrColor)` (unlit additive for rings/frames),
  `Transparent(name, color)`; materials in `Assets/Materials/Ninjago/BossBattle/`; `SetLayerRecursively(go, World)`.

## 4. Arena (A) — `BossArena : MonoBehaviour, IBossArena` (+ `BossCameraDirector : IBossCamera`, `BossPools : IBossPools`,
`BossFx` inside the arena)
- Arena root at world (2000, 0, 0) under the game. Children: road scenery strip, sky scenery strip, backdrop silhouettes,
  `CarRig`, `PlaneRig` (direct children of Root; builder sets `markers` to an empty array; the arena never calls
  `VehicleRig.Initialize/SetSteer`, only `SetPose`), boss actor instance (B's prefab), pools, sun reference (the WireShared Sun).
- Each frame (not paused, steering enabled): lean → `ChestSteer.Steer` → `VehicleSteerer.Step` → `Vehicle.SetPose`;
  `ControllerFilter` → `Hud.SetController` on change; boss anchor `(0, 0, bossDistance)` (+ sway) → `Boss.SetAnchor`;
  scroll strips at `GroundSpeed`; tracking-lost timers; `Camera` focus at the vehicle.
- `BossCameraDirector` writes only the rig **pivot**: base pose (per phase, from visual config) + override (cutscene) + shake.
- `HitVehicle`: honours god mode + i-frames; hearts −dmg; bump; `BossTimeScale.HitStopAsync`; vehicle blink; fx; log; hearts 0 →
  the phase runner ends with KnockedOut.
- `PlayFx`: config fx entry → `SfxManager.PlaySoundEffect(sound, 1, volume)` + `VfxManager.PlayVisualEffect(visual, pos, rot,
  scale)` (guarded: skip missing entries) + shake + `Look.PulseLight`; ground-anchored systems are tracked in a fixed array
  and moved by GroundSpeed·dt until dead. `StartFx/StopFx`: looping systems follow a transform; loop sounds via
  `SfxManager.StartLoop/StopLoop`; Stop resets emission before `Stop()` (MechaLane pattern).
- Pools: one `ObjectPooler<PooledProp>`-style pool per `BossPropKind` from serialized prefab refs, prewarmed in `Initialize`
  (bricks 16, rocks 3, reticles 3, cracks 8, walls 2, minions 2, crates 2, rockets 4, gusts 12, bolts 16, spike walls 2,
  rings 1). `VfxManager.Prewarm` per cue (LaserHit 10, LaserMuzzle 6, BrickGrow/CrackGlow 8, RocketTrail/RocketHit 3,
  GustPass 4, WallSmash 2, ExplodeSmall 6). `AudioClip.LoadAudioData` for boss clips.
- `RoadPhaseRunner.RunAsync(IBossArena, CancellationToken) → UniTask<BossPhaseOutcome>`; attacks `GroundSlamAttack`,
  `RockDropAttack`, `MinionThrowAttack`, `PowerUpAttack` (+ `RocketProjectile`, `ReticleMarker`, `SideWall`, `PowerCrate`,
  `RisingBrick`, `CrackGlow` props), steer-check and ready beats. Each attack logs start/result, sets prompts/icons/tells,
  cleans up on cancel. Overlap rule via `RockSchedule`. Checkpoints at the breather after each volley.
- `Editor/BossBattle/BossBattleWorldBuilder.cs` (A) builds `Assets/Prefabs/Ninjago/BossBattle/BossArena.prefab` + road prop
  prefabs in `Props/`, using M's model wrapper prefabs when present (else primitives with `BossToon` materials), B's
  `BossActor.prefab`, and calls S's `BossSkyPropsBuilder.Build()` for sky prefabs. Road: flat 3-band asphalt (features ≥ 0.5 m),
  relief only on bricks/walls/canyon; canyon cliffs, ruined arches with god-ray shafts (R's `M_BB_LightShaft`), braziers, lava
  cracks (≤ HDR 1.3); sky: storm cloud layers, floating ruins, moon; backdrop silhouettes at z ≈ 100; boss blob shadow.

## 5. Actor and cutscenes (B)
- `BossActor : MonoBehaviour, IBossActor` on the boss prefab; finds parts by name (§8.1, missing parts tolerated); procedural
  animation in `Update` on **scaled** time; limb poses stepped at 15 fps (root motion smooth); hit flash via a material
  instance (`_FlashColor/_FlashAmount` on ToonLit), enrage tint, tired droop + Zzz fx handle, emissive tells at HDR ≥ 2.5;
  explode = chained `ExplodeSmall` over parts then `ExplodeBig`, parts fly apart (no physics), hide.
- `MinionActor` (PooledProp subclass, kind Minion): cling/flail/scraped-off animations.
- `BossCutscenes` (on the arena prefab): `PlayAsync(BossCutscene id, IBossArena arena, CancellationToken)` implementing the GDD
  shot lists via `arena.Camera.PlayShotsAsync` / `MoveAsync` / `Cut`, `arena.Look.Apply/Flash/Lightning`, `arena.Hud.SetCutsceneMode/
  ShowBanner/SetBossBar`, `BossTimeScale.SetSlowMo`, vehicle swap with `BossTransformBurst` + `BossRebuildSparkle`,
  `BgmManager.PlayStinger/CrossFadeTo`; honours `DebugSettings.bossSkipCutscenes` (jump to end state); logs `boss_cutscene`;
  returns `SteeringEnabled = true` at the end.
- `Editor/BossBattle/BossActorBuilder.cs` builds `BossActor.prefab` (M's `BB_Boss` wrapper, else a primitive placeholder with
  the same part names and pivots) and `Props/Minion.prefab`.

## 6. Rendering (R)
### 6.1 Rig
`Editor/BossBattle/BossBattleRenderingBuilder.cs` builds `Assets/Prefabs/Ninjago/BossBattle/BossWorldCameraRig.prefab` (mirror
of BilliardRogue `WorldCameraRigBuilder`: WorldCameraRig + PixelWorldDisplay + FrameTimingLogger; CameraPivot › WorldCamera
renderer index 1, post + HDR on, cull World, volume mask WorldVolume, far clip 140, near 0.3; volumes WorldVolume (prio 0),
PhaseBlendVolume (1), BossFxVolume (10, weight 0), shared LowTier (50, disabled) and FeatureOverrides (100); display canvas at
plane 295 on RootViewCamera) and `Assets/Configs/Ninjago/BossBattle/BossHD2DVisualConfig.asset` (640×360, margin 1, quality
override "Medium": 360, bloom ✓, **tilt ✓**, shadows ✓; low-tier GPU names as BR; PiP interval 0.125 on low tier).
### 6.2 Looks
`Assets/Settings/Ninjago/BossBattle/Volumes/Volume_BossDusk/Night/Dawn/Fx.asset` built with VolumeProfilesBuilder's pattern
(Neutral tonemap, bloom threshold 1.0 intensity 1.0 scatter 0.7, vignette 0.3, colour adjustments/split-toning per look,
TiltShiftVolume centre/halfWidth per GDD §12, maxBlur 2.5). Fills `BossBattleVisualConfig.asset` (looks, camera poses, shot
lists defaults). `BossWorldLook : MonoBehaviour, IBossLook` blends WorldVolume/PhaseBlendVolume weights, lerps RenderSettings
(sun = the WireShared "Sun", trilight ambient, exp fog, clear colour) and restores them on destroy; `Flash` animates
BossFxVolume weight; `Lightning`; `SetTiltCenter`; pooled point lights (max 2, no shadows, range 6).
### 6.3 Materials and lighting assets
`M_BB_LightShaft` (copy of M_LightShaft, tinted per look by a cached MPB), surface twins (ToonLit + world UV + normal/cavity
from `M_Surface_*` textures: MossyBrick for walls/bricks, CrystalRock/StoneFloor for canyon/ruins), fog stripping set to Custom
keeping Exponential (GraphicsSettings) so fog survives the APK.
### 6.4 Mode-scoped UI camera fix (I wires; R documents/tests)
While the mode runs: GameScene Main Camera `allowHDR = false`, `cullingMask &= ~(World|WorldVolume)`; `Views.RootViewCamera`
cull excludes World, `UniversalAdditionalCameraData.renderShadows = false`; restored on destroy. (Making RootCamera the lone
Base camera for every mode is a separate, signed-off task.)
### 6.5 Perf gate and degrade ladder
FrameTimingLogger `[Perf]` lines. During the intro, if avg GPU > `degradeGpuMs` over 5 s: tilt off → bloom iterations 2 → 270p
(once, never mid-phase). Targets GDD §15.

## 7. HUD and views (U)
- `BossHud : MonoBehaviour, IBossHud` prefab `Assets/Prefabs/Ninjago/BossBattle/Hud/BossHud.prefab`: ScreenSpaceCamera on
  RootViewCamera plane 294; three nested canvases (Static, Dynamic, Labels), raycast off. Elements per GDD §10. Text: two TMP
  layers (BilliardPixelBold_TMP colour on top, BilliardPixelBoldOutline_TMP #0a0c18 underneath, same rect), sizes 16·n;
  `NexLocalizedString` for localized labels, plain TMP_Text for numbers. Labels projected in LateUpdate (execution order 250)
  with `PixelWorldDisplay.TryWorldToCanvas`. PiP = `AreaPreviewFrame` + `PlayerIndicatorsManager` initialized once in
  `Initialize(Camera uiCamera, PixelWorldDisplay display, DetectionManager detection, int requestedPlayers,
  IReadOnlyList<int> activePlayers)`; `SetController` calls `SetActivePlayer` only on change.
- Views (`Views/*`): `BossPhaseView : SimpleCanvasView` base for Intro/Road/Transform/Sky/Finale/Knockout (no UI, BackProxy,
  BackRequested); `BossBattleStartView` (title art, Continue row with saved phase/difficulty, New, Difficulty toggle Easy/Normal
  saved via ScopedPlayerPreferenceUpdate + settings_changed, best stats line), `BossPauseView` (Resume/Quit),
  `BossBattleResultView` (title Victory/Knocked out, stat lines, Continue/Continue on Easy/Retry/Menu). All buttons log
  `NinjagoAnalytics.UiAction`. Screen names per GDD §14.
- `Editor/BossBattle/BossBattleViewsBuilder.cs`: view prefabs via `NinjagoViewsBuilder.WithTemplate/BuildView<T>` with pixel
  fonts; MMF players forced to unscaled time; `BossSetupView.prefab` (NinjagoSetupView component, pixel fonts); `BossHud.prefab`.
- Localization: `Editor/BossBattle/BossBattleLocalization.csv` (key, en, fr-CA, zh-Hans, zh-Hant, ja, isSmart) for
  `ninjago.mode.boss_battle` and every `ninjago.boss.*` key; `BossBattleLocalizationSeeder.Run()` writes all 5 locales
  (BilliardRogue LocalizationSeeder.WriteEntries/Verify pattern, owned prefix `ninjago.boss.`), then I re-runs
  `FontAssetsBuilder.Run()`. Test: every key resolves in all locales without fallback; longest (fr-CA) fits.
- `Flow/BossBattleGame.cs` per §1 (+ `SetWorldVisible`, tracking lost, pause, secret code, checkpoint saves, bests, debug hook
  registration).

## 8. Art contracts (M, T, V)
### 8.1 Boss `BB_Boss.fbx` (palette ToonLit, faces −Z, pivot at feet, ~11 m tall, ≤ 3500 tris, cavity baked in palette shading)
Names: `Body` › `Hips` › {`Torso` › {`Head` › {`Jaw`, `HornL`, `HornR`, `EyeL`, `EyeR`}, `ArmL` › `ForearmL` › `FistL`,
`ArmR` › `ForearmR` › `FistR`, `WingL`, `WingR`, `JetL`, `JetR`, `Core`, `ChestPlate`, `PadL`, `PadR`}, `LegL`, `LegR`}.
Pivots at joints. Emissive palette cells: eyes, core, jets, wing membranes, fists/mouth tell areas. Shadow casters: Hips, Torso,
ArmL, ArmR, LegL, LegR only.
### 8.2 Props
`BB_Minion` (≤ 500 tris, feet pivot, −Z; `Body`, `ArmL`, `ArmR`, `Weapon`), `BB_Rock` (centred, ≤ 160), `BB_Brick` (lane cell
2 × 1.1 × 0.7 m with studs, bottom-centre pivot), `BB_PowerCrate` (centred, emissive emblem), `BB_Rocket` (+Z, centred),
`BB_SideWall` (1 × 2 × 6 m along Z, bottom-centre), `BB_SpikeWall` (`Wall` + `Spikes` pointing +X, centre), `BB_WindGust`
(emissive helix, centred, 1.8 m), `BB_LaserBolt` (+Z), `BB_NinjaCar`, `BB_NinjaJet` (+Z, bottom-centre, car/skycraft
footprints), arena: road segment, canyon cliffs, ruined arches, braziers, lava cracks, storm clouds, floating isles, moon,
backdrop silhouettes. Wrapper prefabs `Assets/Prefabs/Ninjago/BossBattle/Models/<Name>.prefab` (M_BB_Palette, layer World,
shadows per §8.1/props: big actors only). Palette material and texture owned by M.
### 8.3 2D (T)
`Assets/Sprites/BossBattle/Particles/*.png` (8-frame strips, 16/32 px, white/grey, alpha 0/255, 1 px gutter, 12 fps;
new: WindStreak, Explosion, Zzz, Rubble, Shockwave, Confetti) + `particles.json`; `Assets/Sprites/BossBattle/Hud/*.png` (bar frame/
fill/trail/notch 9-slice, heart full/empty, crate icon, attack icons, "!" warning, target ring, arrows, letterbox, button frames,
banner plate, P1/P2 tag plates, Boss Battle menu art, start-view title art); point filter, no compression (importer helper).
### 8.4 Audio + VFX (V)
Audio in `Assets/Audio/Sfx/Ninjago/BossBattle/<Key>_<n>.wav`, `Assets/Audio/Bgm/Ninjago/BossBattle/*.ogg`; registered for every
§2 SoundEffect/BgmType key (mono only where it does not hurt; preload on). VFX prefabs `Assets/Prefabs/Ninjago/BossBattle/Vfx/`
from the BilliardRogue recipe pipeline (World layer, LitParticle/GlowParticle, 12 fps), registered for every §2 VisualEffect key
with pool sizes ≥ §4 prewarm counts; config `fx` defaults point at them.

## 9. Testing and playtest
- EditMode: `BossBattleCoreTests` (every §3.3 class incl. resume-at-beat, lemniscate bounds, overlap gap ≥ 0.8 s, clap bands,
  minion solo smash, controller filter), `BossBattleConfigTests` (every EnumDictionary key present/non-null, volleys sum, loopFrom
  in range, thresholds descending, fx enums registered), `BossBattleLocalizationTests`, `BossBattleRenderingTests` (rig layers,
  World layer on all arena renderers, no world-space canvases, tilt band contains car/boss projections).
- CLI playtest (I): MainScene → SelectedMode 15, numPlayers 1 and 2, `ninjaSimulatedBody = true` → GameScene; drive with
  `SimulatedBody.Set`; debug hooks to reach each beat; screenshots of every beat/cutscene to the scratchpad; console clean;
  frame stats (batches, SetPass, tris, GC/frame).
- Device (if adb device present): dev APK, `[Perf]` logcat gate per GDD §15.

## 10. Work packages and ownership

| Pkg | Owns (create/edit) | Appends to |
|---|---|---|
| **F** Foundation | `BossBattle/Core/**`, `BossBattle/Flow/BossBattleDebugHooks.cs`, `BossBattle/Flow/BossTimeScale.cs`, `Persistence/BossBattleSave.cs`, `Analytics/BossBattleLog.cs`, `Editor/BossBattle/BossBattleConfigBuilder.cs`, `Editor/BossBattle/BossToon.cs`, `Editor/Tests/BossBattleCoreTests.cs`, `BossBattleConfigTests.cs`, config assets | GameModeType, View, SfxManager, BgmManager, VfxManager enums; PlayerDataManager, NinjagoProgress, PlayerPreference, DebugSettings, DebugPrinter line, NinjagoAnalytics.ContentName, GameConfigsManager.cs |
| **A** Arena + Road | `BossBattle/Arena/**`, `BossBattle/Road/**`, `Editor/BossBattle/BossBattleWorldBuilder.cs`, `Prefabs/Ninjago/BossBattle/{BossArena.prefab,Props/Road*}` | — |
| **B** Actor + Cutscenes | `BossBattle/Actors/**`, `BossBattle/Cutscenes/**`, `Editor/BossBattle/BossActorBuilder.cs`, `Prefabs/Ninjago/BossBattle/{BossActor.prefab,Props/Minion.prefab}` | — |
| **S** Sky | `BossBattle/Sky/**`, `Editor/BossBattle/BossSkyPropsBuilder.cs`, `Prefabs/Ninjago/BossBattle/Props/Sky*` | — |
| **U** Flow + UI | `BossBattle/Flow/BossBattleGame.cs`, `BossBattle/Views/**`, `BossBattle/Hud/**`, `Editor/BossBattle/BossBattleViewsBuilder.cs`, `BossBattleLocalizationSeeder.cs`, `BossBattleLocalization.csv`, `Prefabs/Ninjago/BossBattle/{Views,Hud}/**`, `Editor/Tests/BossBattleLocalizationTests.cs`, `BossBattleFlowTests.cs` | — |
| **R** Rendering | `BossBattle/Rendering/**`, `Editor/BossBattle/BossBattleRenderingBuilder.cs`, `Configs/Ninjago/BossBattle/BossHD2DVisualConfig.asset`, `BossBattleVisualConfig.asset` values, `Settings/Ninjago/BossBattle/**`, `Prefabs/Ninjago/BossBattle/BossWorldCameraRig.prefab`, `Materials/Ninjago/BossBattle/M_BB_LightShaft*`, surface twins, `Editor/Tests/BossBattleRenderingTests.cs` | GraphicsSettings fog stripping |
| **M** Models | `Tools/Blender/bossbattle/**`, `Models/Ninjago/BossBattle/**`, `Textures/Ninjago/BossBattle/Palette*`, `Materials/Ninjago/BossBattle/M_BB_Palette*`, `Editor/BossBattle/BossBattleArtBuilder.cs`, `Prefabs/Ninjago/BossBattle/Models/**` | — |
| **T** 2D art | `Tools/Textures/*_bossbattle.py`, `Sprites/BossBattle/**`, `Editor/BossBattle/BossBattleSpriteImport.cs` | — |
| **V** Audio + VFX | `Tools/Audio/boss_*`, `Audio/{Sfx,Bgm}/Ninjago/BossBattle/**`, `Editor/BossBattle/BossBattleAudioVfxBuilder.cs`, `Prefabs/Ninjago/BossBattle/Vfx/**` | Sfx/Bgm/VfxManager prefab registries (boss keys only) |
| **I** Integration | `Editor/BossBattle/BossBattleBuilder.cs`, `Prefabs/Ninjago/Games/BossBattleGame.prefab`, `Tools/cli/NinjagoPlaytest.cs` boss entries | NinjagoGamesBuilder (WriteMenuOrder array + skip duplicate GUIDs), NinjagoBuildAll, GameModeConfig.asset key 15, GameConfigsManager.prefab |

Dependency order: F → {A, B, S, U, R, M, T, V-audio} in parallel → V-vfx (needs T sheets) → I (needs all). A/B/S/U compile
against F's contracts only; their builders tolerate missing art (placeholders) so I can integrate in any order.
