# Runner (Chest Chase) runtime and the Chapter runner Boss: research for Boss Battle

Scope: the car/skycraft runtime (`Scripts/Ninjago/Chase/*`), its game and view (`Flow/ChestChaseGame.cs`, `Chase/RunnerView.cs`),
its config (`Configs/ChaseConfig.cs`, `Configs/VehicleType.cs`), the Chapter runner station with the Boss
(`Chapter/Blocks/RunnerBlock.cs`) and the Chapter plumbing around it (`ChapterBlock`, `BlockContext`, `LinkPlayer`,
`ChapterStage`, `ChapterTuning`, `ChapterEnemy`), and the editor builders that make the prefabs.
All paths are relative to `ninjago-unity/Assets/Scripts/Ninjago/` unless they start with `Assets/`. Namespace `Nex.Ninjago`
(editor code: `Nex.Ninjago.Editor`). No asmdefs: everything is `Assembly-CSharp` / `Assembly-CSharp-Editor`.

---

## 0. TL;DR

- The vehicle never moves forward. It sits at local `z = 0` of the `RunnerWorld` root; obstacles and scenery move toward it
  (`-z`). Steering only writes the vehicle's local `x`/`y` (`VehicleRig.SetPose`, `Chase/VehicleRig.cs:40-44`).
- The steering chain is pure and reusable as-is: `PlayerBody.TryGetLeanInches` -> `ChestSteer.Steer` -> `SharedSteer.Blend` ->
  `SmoothDamp` to a target -> `VehicleRig.SetPose`. Only the last two steps live inside `RunnerWorld.Step` (private).
- `RunnerWorld` is closed for extension: private nested `Row`, private `Spawn/Resolve/Step`, a static `segments` array, chase
  analytics hard-wired. **Do not subclass it. Build a new `BossBattleWorld` by composition** that reuses `VehicleRig`,
  `ChestSteer`, `SharedSteer`, `ScrollingStrip`, `ObstacleBlock(+Pool)`, `ObstaclePatterns` and the editor builders
  `NinjagoWorldBuilder.BuildRoad/BuildSky/BuildCar/BuildSkycraft` (all `internal static`).
- The car to skycraft change is a **hard cut** (`ShowSegment`, `RunnerWorld.cs:321-338`) plus 1.2 s of obstacle-free driving
  and a HUD hint. There is no transformation animation anywhere. The Phase 2 cutscene is new work. The recipes to copy are
  `RunnerBlock.EscapeAsync` (Boss leaps onto its flyer and takes off), `LinkPlayer.PlayMoment` (BrickBurst/BrickRebuild/BossRoar)
  and `ChapterStage.MoveCameraAsync` (Cinemachine crane blend).
- The Chapter runner Boss is a **static figure** (body blocks only, no rig). The animated Boss is `Assets/Prefabs/Ninjago/Chapter/Boss.prefab`
  (`ChapterEnemy` + `BossRig` + `BruteRig`). It has `PlayThrow`, `PlayFlinch`, `PlayHit`, `Burst`, `HandPosition` and `Center`.
  Use that prefab for the Boss Battle.

---

## 1. File map

| File | Lines | What it is |
|---|---|---|
| `Chase/RunnerWorld.cs` | 367 | The ride: vehicles, scenery toggles, obstacle rows, steering, hit test, camera pose and shake |
| `Chase/RunnerView.cs` | 154 | `SimpleCanvasView` (`ViewIdentifier.Runner`): instantiates the world, RT feed, HUD, PiP |
| `Chase/VehicleRig.cs` | 53 | Car/skycraft root: `SetPose(pos, vel)` bank and pitch, per-player `PlayerSteerMarker`s |
| `Chase/ChestSteer.cs` | 26 | Lean inches to -1..1 with a deadzone |
| `Chase/SharedSteer.cs` | 82 | Weighted blend of all tracked players, effort share, dominant player |
| `Chase/ScrollingStrip.cs` | 33 | Moves `items` by `-distance` and wraps by `wrapLength` past `minZ` |
| `Chase/ObstacleBlock.cs` / `ObstacleBlockPool.cs` | 47 / 6 | Pooled unit-cube brick, scale pop-in; pool = `ObjectPooler<ObstacleBlock>` |
| `Chase/ObstaclePatterns.cs` | 71 | 3x3 bit masks + shuffled bag (car uses the low row only) |
| `Chase/PlayerSteerMarker.cs` | 47 | World-space badge over the vehicle: player tag + steer arrow (the over-head indicator) |
| `Flow/ChestChaseGame.cs` | 92 | `NinjagoGame` subclass: view, BGM, outcome lines, PlayerData progress |
| `Configs/ChaseConfig.cs` | 94 | SO: `EnumDictionary<VehicleType, VehicleSettings>` + steering/bump/pacing |
| `Configs/VehicleType.cs` | 9 | `Car = 0, Skycraft = 1` (append only) |
| `Chapter/Blocks/RunnerBlock.cs` | 239 | Chapter station that drives a `RunnerWorld` + Boss runner/flyer + escape |
| `Chapter/Blocks/ChapterBlock.cs` | 97 | Abstract station: `CinemachineCamera viewCamera`, `Stage/RunAsync/Finish` |
| `Chapter/Blocks/BlockContext.cs` | 39 | Players, ninjas, hearts, hints, log, tuning, catalog, camera, HUD |
| `Chapter/LinkPlayer.cs` | 141 | Link between stations: moment VFX/SFX, camera move, leap, try-again |
| `Chapter/Stage/ChapterStage.cs` | 214 | World root + `CinemachineBrain` + link camera + stage canvas feed |
| `Chapter/ChapterTuning.cs` | 119 | SO; Runner section `:70-84` (Boss gap, escape, arrival) |
| `Chapter/Actors/ChapterEnemy.cs` | 113 | Monster/Boss actor: throw rig, staff rig, hit, burst, flee |
| `StoneKick/BossRig.cs` | 100 | Arm throw animation and flinch (unscaled time) |
| `StoneKick/RockPieces.cs` | 142 | Stones that fly into the boss: the closest existing "homing" projectile code |
| `Mecha/MechaBeam.cs` | 31 | Beam vs. upright body hit test (laser) |
| `Chapter/Actors/WindowMarker.cs`, `WorldPrompt.cs` | 88 / 133 | Camera-facing ring/arrow marker; world prompt with pop and auto-hide |
| `Editor/NinjagoWorldBuilder.cs` | 437 | `BuildRunnerWorld :182-219`, `BuildObstacleBlock :221`, `BuildRoad :239`, `BuildSky :280`, `BuildCar :308`, `BuildSkycraft :338`, `BuildSteerMarkers :358`, `NewCamera :381` |
| `Editor/Chapter/ChapterWorldBuilder.cs` | 287 | `BuildRunner :224-283` (RunnerTrack + Boss runner/flyer), `ViewCamera :47` |
| `Editor/Chapter/ChapterActorsBuilder.cs` | 321 | `BuildEnemy :182-235`, `BuildEnemyBody :257-296` (Boss horns/spikes/cape), rock, prompt, marker |

Prefabs and assets: `Assets/Prefabs/Ninjago/Chase/{RunnerWorld,BrickBarrier,FloatingBlock}.prefab`,
`Assets/Prefabs/Ninjago/Chapter/{Boss,BrickMonster,RunnerTrack,ChapterRock,WindowMarker,WorldPrompt}.prefab`,
`Assets/Configs/Ninjago/ChaseConfig.asset`, `Assets/Configs/Ninjago/Chapter/ChapterTuning.asset`.

---

## 2. Coordinate system and world layout

- Each runner world lives far from the GameScene main camera: `RunnerView.worldOrigin = (2000,0,0)` (`RunnerView.cs:23,83`).
  `ChapterStage.worldOrigin = (-6000,0,0)` (`ChapterStage.cs:22,69`). Everything below is **local to the `RunnerWorld` root**.
- **Vehicle**: `transform.localPosition = (x, y, 0)` (`VehicleRig.cs:42`). Forward is `+z`, right is `+x`, up is `+y`.
- **Road** (`NinjagoWorldBuilder.BuildRoad :239-277`): asphalt 6.4 wide (x in +-3.2), curbs at x = +-3.3, grass beyond. Lane dashes
  at x = +-1 every 6 m. Trees at x = +-5.6 every 10 m. The asphalt, grass and curbs are **static** long cubes. Only the dashes and trees scroll.
- **Lanes**: `CellCenter` (`RunnerWorld.cs:314-319`): `x = (column - 1) * cellWidth` gives -2, 0, +2 with `cellWidth = 2`.
  Car `y = 0`. Sky rows: `y = centerHeight + (row - 1) * cellHeight` gives 1.6 / 3.2 / 4.8 (asset values).
- **Steer range**: `x in [-halfWidth, +halfWidth] = +-2.2`. Sky `y in centerHeight +- halfHeight = 3.2 +- 1.6`.
  The car's outer edge reaches `2.2 + 0.7 = 2.9` (half extents 0.7). This matters for the side-wall smash (section 12).
- **Obstacle spawn z** = `speed * obstacleLeadSeconds` = 14 x 1.5 = 21 m (car) / 13 x 1.5 = 19.5 m (sky). **Resolve** happens when `z <= 0`.
  **Despawn** happens when `z < despawnZ = -8` (`RunnerWorld.cs:221,254,260`).
- **Camera poses** (`NinjagoWorldBuilder.cs:193-197`): car `(0, 3, -6.5)` pitch 8.8 deg. Sky `(0, 3.9, -5)` pitch 1 deg. FOV 60.
  Standalone far clip is 140 (`:192`). Chapter CM lens far clip is 400 (`ChapterWorldBuilder.cs:57,235`). `NewCamera` (`:381-396`) sets a SolidColor sky
  `(0.5,0.78,1)`, MSAA off, HDR off.
- **Boss model orientation**: the enemy body faces its local **-Z**, toward the player (`ChapterEnemy.cs:97`). If you place it at `+z`
  with identity rotation, it faces the car.

Live asset values (`Assets/Configs/Ninjago/ChaseConfig.asset`):

| | Car (key 0) | Skycraft (key 1) |
|---|---|---|
| segmentSeconds / speed | 20 / 14 | 20 / 13 |
| usesVerticalLean | false | true |
| halfWidth / centerHeight / halfHeight | 2.2 / **0** / 0 | 2.2 / 3.2 / 1.6 |
| steerSmoothingSeconds / maxSteerSpeed | 0.35 / 7 | 0.3 / 7 |
| obstacleSpacing / lead / opening (s) | 1.7 / 1.5 / 2 | 1.9 / 1.5 / 2 |
| cellWidth / cellHeight / vehicleHalfExtents | 2 / 1.6 / (0.7,0.3) | 2 / 1.6 / (0.65,0.25) |

Globals: deadzone 1.5 in, fullLean 7 in, fullRise 4 in, playerOneSteerShare 0.5, bumpSpeedDrop 0.35, bumpRecover 1.2 s,
intro 2 s, transition 1.2 s, wholeChestHint 3 s. (The code default `centerHeight = 0.35f` is overridden to 0 by
`NinjagoAssetsBuilder.BuildConfigs :195-198`. Trust the asset.)

---

## 3. Steering pipeline (reuse as-is)

1. `PlayerBody.TryGetLeanInches(out Vector2 lean)` (`Common/PlayerBody.cs:79-91`): chest minus calibrated `Origin`, divided by `RawPpi`.
   x is the player's right, y is up, in body inches. It returns false when the player is not calibrated or the chest is untracked. With Debug Settings
   "Ninja: Simulated Body" on, it returns `SimulatedBody.GetLeanInches` (keys A/D/W/S for P1 and J/L/I/K for P2,
   `Common/SimulatedBodyKeyboard.cs:25-28`).
2. `ChestSteer.Steer(lean, useVertical, deadzone, fullLean, fullRise)` (`ChestSteer.cs:19-24`) returns -1..1 per axis. Car passes `useVertical=false`.
3. `SharedSteer.Blend(Vector2?[] steers, playerOneShare, dt)` (`SharedSteer.cs:20-41`): weights P1 = share and P2 = 1 - share,
   **renormalized over tracked players only**, so a lone player drives at full strength. It also accumulates `EffortShare(i)` and `DominantPlayer`.
4. Target and smoothing (`RunnerWorld.cs:206-211`), the only part you must copy:

```csharp
var blended = sharedSteer.Blend(steers, config.PlayerOneSteerShare, deltaTime);
var target = new Vector2(blended.x * settings.halfWidth, settings.centerHeight + blended.y * settings.halfHeight);
vehiclePosition.x = Mathf.SmoothDamp(vehiclePosition.x, target.x, ref vehicleVelocity.x, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
vehiclePosition.y = Mathf.SmoothDamp(vehiclePosition.y, target.y, ref vehicleVelocity.y, settings.steerSmoothingSeconds, settings.maxSteerSpeed, deltaTime);
vehicle.SetPose(vehiclePosition, vehicleVelocity);
```

5. `VehicleRig.SetPose` banks `-vx * bankPerSpeed` (car 2, set at `NinjagoWorldBuilder.cs:334`. Skycraft keeps the default 4) and pitches
   `-vy * pitchPerSpeed` (5). `VehicleRig.SetSteer(playerIndex, steer, tracked)` drives the per-player badge (`PlayerSteerMarker.SetSteer`).
   The badges are world-space canvases at x = +-0.65, height 1.75 (car) / 0.75 (sky) (`NinjagoWorldBuilder.cs:358-375`).
   **This is the over-head player indicator for a shared vehicle.**

Tests already exist for `ChestSteer` and `SharedSteer` in `Editor/Tests/NinjagoLogicTests.cs:120-175`. Follow that pattern for the new pure logic.
The Debug Settings rows edit `ChaseConfig` live through its setters (`Scripts/PlayerData/DebugSettings.cs:366-384,450-452`, properties tagged
`ES3NonSerializable`).

---

## 4. Run loop (`RunnerWorld`)

```csharp
public void Initialize(IReadOnlyList<PlayerBody> activePlayers, ChaseConfig aConfig, RenderTexture? target)   // :102
public void Show(VehicleType type)                                                                           // :120
public UniTask RunAsync(CancellationToken ct)                                                                // :122 (Car then Skycraft, config settings)
public async UniTask RunAsync(IReadOnlyList<VehicleType> route, Func<VehicleType, ChaseConfig.VehicleSettings> settingsFor,
    float introSeconds, CancellationToken ct)                                                                // :128
public event Action<VehicleType>? VehicleShown; public event Action? CountsChanged;
public event Action<VehicleType, bool, int>? ObstacleResolved;  // (vehicle, hit, dominantPlayer)
public VehicleType CurrentVehicle; public VehicleRig Vehicle; public float SpeedFactor, TimeLeft;
public int GetDodges(VehicleType), GetHits(VehicleType); public float SteerShare(int playerIndex);
```

- `RunAsync(route,...)`: `ShowSegment(route[0])`, then `DriveAsync(intro)`, then for each vehicle: (if not first) `ShowSegment`,
  `DriveAsync(config.TransitionSeconds)`, then `RunSegmentAsync` (`:128-144`).
- `RunSegmentAsync` (`:146-171`): a per-frame `UniTask.Yield(PlayerLoopTiming.Update)` loop. It spawns a row when `elapsed >= nextSpawn` while
  `nextSpawn <= segmentSeconds - obstacleLeadSeconds`, steps steering, moves rows, and at the end calls `ClearRows()`. Analytics:
  `NinjagoAnalytics.SegmentStart`, then `ObstacleDodge/ObstacleHit` per resolve.
- `DriveAsync` (`:174-184`): steering and scenery, no obstacles. Used for the intro and the transition.
- Note: the first row **spawns** at `openingSeconds` and **arrives** `obstacleLeadSeconds` later. The tooltip in ChapterTuning says
  "before the first obstacle reaches"; the code means "before the first spawn".

## 5. Obstacles: spawn, move, resolve, hit test

- `Row { float z; int mask; int index; bool resolved; List<ObstacleBlock> blocks; }` is a private sealed class, allocated with `new` per spawn (`:60-67,221`).
- `Spawn` (`:219-240`): for each blocked cell, takes a block from `barrierPool` (car) or `floatingBlockPool` (sky).
  Size: car `(cellWidth*cellFill, 1.1, 0.7)` at `y = size.y/2`. Sky `(cellWidth*cellFill, cellHeight*cellFill, 0.9)` at the cell center.
  `cellFill = 0.9` leaves a forgiving margin.
- `MoveRows` (`:242-264`): `row.z -= speed * speedFactor * dt` and writes each block's local z. On the first frame with `z <= 0` it calls
  `Resolve`. Below `despawnZ` it releases the blocks.
- `Resolve` -> `Overlaps` (`:289-308`): an AABB in the x/y plane using the **smoothed** `vehiclePosition`, `halfCell = cell * cellFill/2`, and
  `vehicleHalfExtents`. For the car, `overlapY` is always true ("a barrier in its lane always hits").
  On a hit: `speedFactor = 1 - BumpSpeedDrop`, `shakeLeft = 0.3`, `SfxManager.SoundEffect.VehicleBump`, `VfxManager.VisualEffect.VehicleBump` at
  `vehicle.Position + forward*0.8`, then events. Hits never fail the run.
- `ObstaclePatterns` (`:17-33`): car masks `0b110` (open left), `0b101` (open center), `0b011` (open right). There are 7 sky masks
  (climb, dive, go right, go left, ring, two L-shapes). Bit = `row*3 + column`, column 0 = left, row 0 = low.
  `Next()` draws from a shuffled bag that never repeats across refills. It uses the global `UnityEngine.Random`, so it is not seeded.
- `ObstacleBlock` (`ObstacleBlock.cs`): `Show(localPosition, size)` sets scale 0 and `Update` scales uniformly to `size` over
  `popInSeconds = 0.15`. The pivot is the cube center, so it grows from its middle, **not up from the floor**. `Release()` fires `OnRelease`.

### Pooling (`Scripts/ObjectPooling/ObjectPooler.cs`)

```csharp
public interface IPoolableObject { event Action<Component>? OnRelease; }
public abstract class ObjectPooler<T> : MonoBehaviour where T : MonoBehaviour, IPoolableObject {
    public void Initialize(T prefab, int defaultCapacity = 10); public T Get(); public void Prewarm(int count);
    public IEnumerable<T> ActiveInstances { get; } }
```

Instances are parented to the pool transform (`CreatePooledItem :62-67`). `Get` sets them active and release sets them inactive.
`RunnerWorld.Initialize` calls `Initialize(prefab, 6/12)` but **never `Prewarm`**, so the first spawns instantiate mid-ride.
The pattern for a new pooled type is a two-line pool class plus `IPoolableObject` on the component:

```csharp
public class ObstacleBlockPool : ObjectPooler<ObstacleBlock> { }          // Chase/ObstacleBlockPool.cs
public class ObstacleBlock : MonoBehaviour, IPoolableObject {
    public event Action<Component>? OnRelease;
    public void Release() { OnRelease?.Invoke(this); } ... }
```

The pool GameObject must sit at the world root origin with no rotation, because `Show` writes `localPosition`.

## 6. Scrolling strips

`ScrollingStrip.Scroll(distance)` (`ScrollingStrip.cs:20-29`) moves every item `-distance` in local z and adds `wrapLength` once it passes
`minZ`. Items are spaced so the wrap is seamless. The road uses `wrapLength = length` (120 standalone, 48 Chapter) and `minZ = -10`. The sky uses clouds with
`minZ = -12` (`NinjagoWorldBuilder.cs:273-275,302-304`). Only the active vehicle's strip scrolls (`RunnerWorld.cs:215`). A new dark arena
for Phase 2 is just another `ScrollingStrip` with its own items (pillars, storm clouds, lightning rocks) built the same way.

## 7. Camera: poses, shake, Cinemachine

- **Standalone (Chest Chase)**: a plain `Camera` child of the world renders into `RunnerView`'s `RenderTexture(Screen.width, Screen.height, 24)`
  (`RunnerView.cs:79`), shown by a full-screen `RawImage feed`. `ShowSegment` **cuts** the camera to `carCameraPose`/`skyCameraPose`
  (`:334-335`). `ShakeCamera` (`:340-351`) runs inside every `Step` and **rewrites `worldCamera.transform.position` every frame**:
  `pose + Random.insideUnitSphere * 0.18 * shakeLeft/0.3`, or the bare pose when there is no shake. Any other camera animation
  that runs during `Step` is overwritten.
- **Chapter**: `ChapterStage` owns the real camera with a `CinemachineBrain` (`ChapterStageBuilder.cs:40-43`). Each block has a
  `CinemachineCamera viewCamera`. In `RunnerTrack` the RunnerWorld's `worldCamera` field points at a **disabled `Camera`** on the same GameObject
  as the block's `CinemachineCamera` (`ChapterWorldBuilder.cs:230-236,247`). The RunnerWorld's pose cut and shake therefore move the vcam and the brain follows.
  `Initialize(..., target: null)` leaves that disabled camera without a target.
- Cinemachine 3.1.7 (`Packages/manifest.json`, namespace `Unity.Cinemachine`) is used **only** by `ChapterBlock`, `ChapterStage` and their builders.
  The blend helper to copy:

```csharp
// ChapterStage.cs:205-210
async UniTask BlendAsync(CinemachineCamera target, CinemachineBlendDefinition.Styles style, float seconds, CancellationToken ct)
{
    brain.DefaultBlend = new CinemachineBlendDefinition(style, seconds);
    target.Priority = ++priority;
    await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
}
```

  `CutTo` (`:157-161`) uses `Styles.Cut`. `MoveCameraAsync` (`:167-199`) places a waypoint link camera: lift CraneUp 14 / FollowFlight 7 / else 4 m,
  swing +-10 m. It runs EaseIn for half the time to the waypoint, then EaseOut for the other half into the target. For the same station it pushes in on `FocusPoint`
  (40%/60%).
- `RunnerBlock.ArriveAsync` (`RunnerBlock.cs:188-206`) is the non-Cinemachine recipe. It lerps vehicle and camera local positions with ease-out
  `1-(1-t)^2`, **after** `world.RunAsync` returned, so no `Step` is fighting it.

## 8. How car to skycraft is presented today

- **Chest Chase**: `ShowSegment(Skycraft)` toggles `roadScenery` off and `skyScenery` on, car off and skycraft on, resets the position to
  `(0, centerHeight)`, cuts the camera, and sets `TimeLeft`. Then `DriveAsync(1.2 s)` runs with no obstacles. `RunnerView.HandleVehicleShown` swaps the
  localized vehicle label and shows the "Use your whole chest" banner for 3 s (`RunnerView.cs:132-137`). There is no animation, VFX or SFX.
- **Chapter, same station** (`RunnerRide.CarThenSkycraft`): the same hard cut inside `world.RunAsync`.
- **Chapter, between stations**: `LinkPlayer.PlayAsync` (`LinkPlayer.cs:33-53`) plays a caption, a stinger (`link.stinger`), the previous block's
  `PlayExitAsync`, a moment, `next.Stage()`, the camera move and the ninjas' leap. Moments (`PlayMoment :109-137`, private static):
  - `BrickRebuild`/`BrickBurst`: `VisualEffect.BrickBurst` at `from`, scale 2.5, with `SoundEffect.PieceHit`. For `BrickRebuild`, after
    `0.45*linkSeconds` it also plays `VisualEffect.BrickRebuild` at the target, scale 2.5, with `SoundEffect.BrickSnap` (`RebuildAsync :98-104`).
  - `BossRoar`: `VisualEffect.SlamShockwave` scale 3 with `SoundEffect.TeammateSlam`.
  - `VehicleArrives`: `VisualEffect.LinkDust` at `to`, scale 2, with `SoundEffect.VehicleBump`.
  - `EnemyFlees`: `LinkDust` with `Spotted`. The default plays `LinkDust` with `NinjaSlip`.
  The `LinkMoment` enum is in `Chapter/Data/ForgeEnums.cs:75-86` (BrickRebuild=0, BossRoar=1, Dust=2, BrickBurst=3, NinjaLanding=4,
  VehicleArrives=5, DropOffRoof=6, AroundCorner=7, EnemyFlees=8). `CameraMove`: Glide=0, SwingToSide=1, SwingBehind=2, CraneUp=3, FollowFlight=4.

## 9. The Chapter runner Boss (`RunnerBlock`)

Serialized fields: `world`, `chaseConfig`, `vehiclePrompt` (WorldPrompt), `promptHeight 2.4`, `bossRunner`, `bossFlyer`,
`escapeText/Color`, `dodgeText/How/Color`, `arrivalMargin 12`, `arrivalCameraOffset (0,3,-6.5)` (`:20-51`).

- `Initialize` (`:64-81`) copies each vehicle's `VehicleSettings` per entry (`Copy()` is a memberwise clone). It sets
  `segmentSeconds = rideSeconds/vehicleCount`, multiplies `speed` by `difficulty.speedScale`, sets `obstacleSpacingSeconds = clamp(10/count, 0.8, 6)`, and sets `openingSeconds`
  from tuning. **This is the pattern for per-phase settings without touching the shared asset.**
- `Stage` (`:92-102`): `world.Show(firstVehicle)`, `bossGap = BossStartGap (22)`, runner shown when `Entry.boss`, flyer hidden.
- Runs ahead (`Update :141-146`, `PlaceBoss :148-153`), in world-root local space:

```csharp
var vehicle = world.Vehicle.transform.localPosition;
var bob = Mathf.Abs(Mathf.Sin(time * 7f)) * 0.25f;
bossRunner.localPosition = new Vector3(Mathf.Sin(time * 0.6f) * 1.2f, vehicle.y + bob, bossGap);
```

  The Boss "moves with the car" because it holds a constant z while the world scrolls. Its y follows the vehicle, so it stays at the same
  height in the sky. `HandleObstacleResolved` (`:208-223`): a dodge gives `gap -= 1.4` (min 7), a hit gives `gap += 2.5`. These are **instant** jumps with no
  smoothing. `ChaseCloseness = InverseLerp(22, 7, gap)` feeds the level stars (`LevelRunner.cs:122`).
- Escape (`EscapeAsync :156-185`, 3.5 s): `StingerType.BossAppear` and `SoundEffect.FlyAway`. The flyer appears at `start + (0,3,6)`. The runner leaps
  onto it during the first third (`SmoothStep`, `+1.2` up). From `t > 0.33` the flyer goes `+(0, away^2*40, away*70)` with the runner riding. At the end
  `VisualEffect.BrickBurst` plays at the flyer and both hide. **This is a ready template for the Boss taking off at the start of Phase 2.**
- The model (`ChapterWorldBuilder.BuildRunner :261-270`) is `BossRunner/Figure` (scale 2.2) with `ChapterActorsBuilder.BuildEnemyBody(figure, true)` and
  every tinted part set to "Chapter Boss Body" `(0.35,0.3,0.4)`. **It has no rig components, so it cannot throw, flinch or burst.** The flyer is three primitive
  cubes (deck 3.4x0.2x2.4 and two wings angled 12 deg) with the emissive purple material "Chapter Boss Flyer". The flyer is built inline, not as a reusable method.

### The animated Boss (`Assets/Prefabs/Ninjago/Chapter/Boss.prefab`, `ChapterActorsBuilder.BuildEnemy(BossPath, true) :182-235`)

The root has `ChapterEnemy` + `BossRig` + `BruteRig`. The hierarchy is ThrowBody > StaffBody > body blocks. The root `localScale = 3`, so the Boss is about 8 m tall,
and `burstScale = 3`. The Boss variant adds horns, shoulder spikes and a cape (`BuildEnemyBody :284-293`). API:

```csharp
// ChapterEnemy.cs
public void Initialize(Color bodyColor);               // tints bodyRenderers via renderer.material (instanced materials)
public void Spawn(Vector3 position, Quaternion rot);    // world space; sets IsAlive
public BossRig Thrower; public Vector3 Center, HandPosition; public bool IsAlive;
public void PlayHit();                                  // BossRig.PlayFlinch + VisualEffect.BossHit (scale burstScale)
public void Burst();                                    // VisualEffect.BrickBurst + SoundEffect.PieceHit, deactivates
public UniTask FleeAsync(Vector3 dir, float seconds, CancellationToken ct);
// BossRig.cs
public void PlayThrow(float seconds);  // wind-up for 60% of the time, release for 40%; the rock leaves at the end
public void PlayFlinch();              // 0.35 s shake/lean back
```

FightBlock uses the prefab like this: `boss = Instantiate(bossPrefab, transform); boss.Initialize(bossColor); boss.Spawn(bossSpot.position, bossSpot.rotation)`
(`Chapter/Fight/FightBlock.cs:109-113,169-173`) and chips it with `boss.PlayHit()` (`:280-283`). `BossRig` animates on `Time.unscaledTime`.

---

## 10. View and game wiring (Chest Chase)

- `ChestChaseGame.PlayAsync` (`Flow/ChestChaseGame.cs:42-50`): `Instantiate(runnerViewPrefab)`, `view.Initialize(activePlayers, chaseConfig, Detection, NumOfPlayers)`,
  `await Views.ReplaceView(view)`, `BgmManager.Instance.CrossFadeTo(BgmType.NinjaChase, ...)`, then `await view.RunAsync`.
  Setup and calibration come before this in `NinjagoGame.RunAsync` / `CalibrateAsync` (`Flow/NinjagoGame.cs:65-110`).
- `RunnerView.Initialize` (`:76-95`): RT, localized labels via `LocalizedStrings.WithArguments`, `Instantiate(worldPrefab, worldOrigin, identity)`,
  event hooks, and **PiP**: `pipFrame.Initialize(detection.PlayAreaController)` and `pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame,
  detection.BodyPoseDetectionManager)`. `OnDestroy` destroys the world and releases the RT. Back: `OnBackButton` logs `NinjagoAnalytics.UiAction("runner","back")`
  and completes `quitSource`. `RunAsync` is `WhenAny(world.RunAsync, quitSource)`.
- Outcome and persistence (`ChestChaseGame.cs:52-88`): `ResultLine.Data` lines, `GameAnalyticsProperties` details,
  `PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved => {...})`.
- Builders: game `NinjagoGamesBuilder.BuildChestChase :62-76` (`WireShared :78-96` adds a hard-shadow sun, `SimulatedBodyKeyboard`, engines, setup/result
  views). View `NinjagoViewsBuilder.BuildRunner :166-196` (Feed, labels, banners, `CameraPreview(...)`, `BackProxy`). Menu: `RegisterModes :102-113`,
  `WriteMenuOrder :119-141`.
- Enums to append: `GameModeType` (`Scripts/Gameplay/Core/GameModeType.cs`, explicit values, next free is **15**). `View.ViewIdentifier`
  (`Scripts/Main/Views/Framework/View.cs:23-74`, **implicit values, append only at the end**).

---

## 11. Reuse matrix for the Boss Battle

| Need | Existing piece | Verdict |
|---|---|---|
| Chest lean to steer, 2 players | `PlayerBody.TryGetLeanInches`, `ChestSteer`, `SharedSteer` | **Reuse as-is** |
| Vehicle visuals + bank + per-player badge | `VehicleRig`, `PlayerSteerMarker`, `BuildCar/BuildSkycraft` | **Reuse as-is** |
| Target/SmoothDamp step | `RunnerWorld.Step :190-211` (private) | **Copy** into a `VehicleSteerer` and add a target offset (wind) and a clamp override |
| Road/sky scenery | `BuildRoad/BuildSky` + `ScrollingStrip` | **Reuse**. The dark arena is a new strip with the same pattern |
| Lane/cell geometry + AABB hit test | `CellCenter`, `Overlaps` (private) | **Copy** (about 25 lines) into a static `LaneGrid` helper |
| Brick rows / sky blocks | `ObstacleBlock(+Pool)`, `ObstaclePatterns` | Reuse pool + patterns. **Extend** `ObstacleBlock` with grow-from-floor for the slam |
| Per-phase settings | `ChaseConfig.VehicleSettings` (public, `Copy()`) | **Reuse the type** inside a new `BossBattleConfig` |
| Boss model + throw/flinch/burst | `Boss.prefab` (`ChapterEnemy`, `BossRig`) | **Reuse**. Add a slam pose/tween and a tired/sleep pose in new code |
| Boss runs ahead with the car | `RunnerBlock.PlaceBoss` | **Copy** and smooth the gap |
| Boss takes off on its flyer | `RunnerBlock.EscapeAsync`, flyer builder lines | **Copy**. Extract `BuildBossFlyer` in the editor |
| Rock with a tracking marker that locks and drops | `ChapterRock.prefab`, `WindowMarker`, the ninja `SwirlRing` (flat world-canvas ring, `ChapterActorsBuilder.cs:130-136`) | **New** `RockDropAttack` + ground marker. Reuse the rock mesh and ring sprite |
| Minions on the car sides + side walls | `BrickMonster.prefab` (small), `ObstacleBlock` visuals | **New** `MinionThrowAttack`, `SideWall` |
| Pickups + homing rockets | `RockPieces.LaunchAsync` (lerp + arc into `boss.ChestPosition`) | **New** `RoadPickup` + `HomingRocket` (pooled). Copy the arc idea and track a moving target |
| Laser auto-fire on an infinity path | `MechaBeam` (hit math), `VisualEffect.LaserMuzzle/LaserImpact`, `SoundEffect.LaserHum/MechaHit` | **New** `LaserVolley`. Use a 2D xy distance (simpler than `MechaBeam`) |
| Spiked clap walls | none | **New** `SpikeWall` (primitives) |
| Danger callouts | `WorldPrompt`, `ChapterHud.FlashDanger` + DangerGlow sprite | **Reuse the prefab and sprite**. Rebuild the glow on the Boss view |
| Cutscene camera blends | `ChapterStage.BlendAsync/MoveCameraAsync` | **Copy** (needs `CinemachineBrain` on the boss world camera) |
| Moments VFX/SFX | `LinkPlayer.PlayMoment` (private static) | **Copy** the 3-line recipes |
| Explode at HP 0 | `ChapterEnemy.Burst`, `VisualEffect.Explosion/EnemyExplosion/BrickBurst`, `SoundEffect.BossDeath(302)` | Reuse plus chain several bursts |

Existing enum values worth using. VFX (`Scripts/Vfx/VfxManager.cs:16-82`): `VehicleBump=110, RockImpact=123, BossHit=122, BrickBurst=172,
SlamShockwave=191, BrickRebuild=210, LinkDust=211, LaserMuzzle=140, LaserImpact=141, EnemyExplosion=142, Explosion=20, PickupSparkle=30,
DustPuff=31, PinSlam=173`. SFX (`Scripts/Sfx/SfxManager.cs`): `VehicleBump=704, BossHit=301, BossDeath=302, BossAppear=605,
PickupPower=502, Explosion=400, LaserHum=720, MechaHit=721, EnemyExplosion=722, TeammateSlam=771, FlyAway=751, PieceHit=712, BrickSnap=780,
Victory=607`. BGM (`Scripts/Bgm/BgmManager.cs`): `BgmType.Boss=20, NinjaChase=41`. Stingers: `BossAppear=1, Victory=2`.

---

## 12. Recommended architecture

### 12.1 Files (new)

```
Scripts/Ninjago/BossBattle/
  BossBattleEnums.cs        BossPhase { Road = 0, Sky = 1 }  (EnumDictionary keys: explicit, append only)
                            BossAttack { RockDrop = 0, MinionThrow = 1, GroundSlam = 2, DevilWind = 3, Clap = 4 }
  BossBattleConfig.cs       SO: EnumDictionary<BossPhase, ChaseConfig.VehicleSettings> vehicles;
                            EnumDictionary<BossAttack, AttackSettings>; boss HP, rockets per pickup, wake thresholds {0.7,0.3,0.05};
                            setters for Debug Settings rows
  VehicleSteerer.cs         pure C#: steers[], SharedSteer, position/velocity, TargetOffset, Step(...), Reset(...)
  LaneGrid.cs               static: CellCenter, Overlaps(mask, pos, settings, cellFill), LaneOf(x)
  BossBattleWorld.cs        MonoBehaviour: the scene of the fight (composition, below)
  BossActor.cs              wraps a ChapterEnemy instance: HP, Place modes (RunAhead/Fly/Infinity/Tired), roar/slam tweens, Explode
  Attacks/IBossAttack.cs    UniTask RunAsync(BossBattleWorld world, CancellationToken ct)
  Attacks/RockDropAttack.cs, MinionThrowAttack.cs, GroundSlamAttack.cs, DevilWindAttack.cs, ClapAttack.cs, LaserVolley.cs
  Props/FallingRock.cs(+Pool), GroundMarker.cs, SideWall.cs, SideMinion.cs, RoadPickup.cs(+Pool), HomingRocket.cs(+Pool), SpikeWall.cs, LaserBeam.cs
  Views/BossBattleHudView.cs, BossCutsceneView.cs, ...   (one SimpleCanvasView per step; ViewIdentifier appended)
Flow/BossBattleGame.cs      NinjagoGame, Mode => GameModeType.BossBattle (= 15)
Editor/BossBattleBuilder.cs, BossBattleWorldBuilder.cs, BossBattleViewsBuilder.cs
```

### 12.2 `BossBattleWorld` (composition, not inheritance)

Serialized fields mirror `RunnerWorld` (`:19-58`): world `Camera` (+ optional `CinemachineBrain` and vcams carPose/skyPose/cutscene),
`VehicleRig car, skycraft`, `GameObject roadScenery, arenaScenery`, `ScrollingStrip roadStrip, arenaStrip`, `ObstacleBlockPool risingBrickPool,
windBlockPool`, prop pools, `Transform bossRoot`. Runtime pieces:

```csharp
public sealed class VehicleSteerer
{
    readonly SharedSteer shared = new();
    readonly Vector2?[] steers = new Vector2?[SimulatedBody.MaxPlayers];
    Vector2 position, velocity;
    public Vector2 Position => position;  public Vector2 Velocity => velocity;
    public IReadOnlyList<Vector2?> Steers => steers;  public SharedSteer Shared => shared;
    public Vector2 TargetOffset;               // wind push / knockback in world units, added after the lean target
    public void Reset(Vector2 at) { position = at; velocity = Vector2.zero; }
    public void Step(IReadOnlyList<PlayerBody> players, VehicleRig rig, ChaseConfig steering, ChaseConfig.VehicleSettings s, float dt)
    {   // body = RunnerWorld.Step :192-211, with: target += TargetOffset; clamp y to [center - half, center + half]
    }
}
```

- **Keep the camera pose and the shake separate.** Store `basePose` (cutscenes may animate it) and add a shake offset in `LateUpdate`, so a cutscene and the shake never overwrite each other (unlike `RunnerWorld.ShakeCamera`).
- **One main loop per phase.** `async UniTask RunRoadPhaseAsync(ct)` / `RunSkyPhaseAsync(ct)` run `steerer.Step`, `strip.Scroll(speed * speedFactor * dt)`,
  row movement and resolve, the attack scheduler tick, and `boss.Place(...)`, all with `await UniTask.Yield(PlayerLoopTiming.Update, ct)`. Attacks run as child
  UniTasks with a linked CTS that the phase cancels when it ends (the HP threshold or the 5th pickup).
- **Rows**: copy the `Row` idea but pool the row objects (struct entries in a `List`, or a `Stack<Row>`) to avoid per-spawn GC.
  Rows carry a kind: `Brick`, `Pickup`, `Wind`.
- **Events** for the views and analytics: `PhaseChanged(BossPhase)`, `BossHpChanged(float hp01)`, `CarHpChanged`, `AttackStarted(BossAttack)`,
  `AttackResolved(BossAttack, bool hit, int dominantPlayer)`, `PickupCollected(int count)`.
- **The world outlives the views.** "Every step a view" means the 3D world cannot be owned by one view the way `RunnerView` owns it. Follow `ChapterStage`
  (`ChapterStage.cs:12-17,67-79`): the game instantiates the world plus a stage canvas (`ScreenSpaceCamera`, `planeDistance 295`, between the
  scene background at 300 and views at 290) holding the RT feed. Each step view (intro, Phase 1 HUD, cutscene, Phase 2 HUD, victory) is an overlay
  swapped with `Views.ReplaceView`.

### 12.3 Phase flow

```
BossBattleGame.PlayAsync(activePlayers, ct)
  world.Initialize(players, config, lowResTarget); stage.SetVisible(true)
  [IntroView]        boss roars (SlamShockwave + StingerType.BossAppear), lean hint, DriveAsync(intro)
  [RoadHudView]      RunRoadPhaseAsync: scheduler cycles RockDrop / MinionThrow / GroundSlam; pickups every N s;
                     pickup k fires k rockets (config); 5th pickup -> await rockets -> phase end
  [CutsceneView]     PlayTransformAsync (12.4) -> no input needed, Back still works
  [SkyHudView]       RunSkyPhaseAsync: Active (DevilWind rows, Clap) <-> Tired (LaserVolley on infinity path)
                     wake when hp crosses 0.70 / 0.30 / 0.05; hp 0 -> ExplodeAsync
  [VictoryView]      outcome, PlayerDataManager progress, analytics -> NinjagoResultView via NinjagoGame
```

Save progress at each phase boundary (phase index, boss HP, pickups) so a restart can continue. That is the persistence agent's pattern via `ScopedNinjagoProgressUpdate`.

### 12.4 Phase 2 transformation cutscene (all from existing recipes)

1. Stop spawning, release rows (`ClearRows`-style), and set `steerer.TargetOffset = 0`.
2. The Boss leaps onto its flyer and climbs: `RunnerBlock.EscapeAsync` math, but stop at the sky gap instead of flying off.
   Use `StingerType.BossAppear` and `SoundEffect.FlyAway`.
3. Car breaks: `VisualEffect.BrickBurst` at the car (scale 2.5) with `SoundEffect.PieceHit` (the `LinkMoment.BrickBurst` recipe). Hide the car.
4. After about 0.45 of the cutscene: `VisualEffect.BrickRebuild` at `(x, centerHeightSky, 0)` with `SoundEffect.BrickSnap`, then show the skycraft and tween its y from the
   car height to 3.2.
5. Camera: blend car pose to a crane waypoint to the sky pose (`ChapterStage.MoveCameraAsync` CraneUp: lift 14, EaseIn/EaseOut halves), or
   lerp the base pose by hand as in `RunnerBlock.ArriveAsync`. During the move, swap `roadScenery` for `arenaScenery`, apply the dark
   environment (camera background, fog, ambient, sun), and cross-fade the BGM to `BgmType.Boss`.
   Reference: `ThemePainter.ApplyEnvironment(theme, camera, sun)` (`Chapter/Stage/ThemePainter.cs:80-93`). It writes the **global** `RenderSettings`,
   so restore them on exit.

### 12.5 Attack notes (hit rules in world-root local space, vehicle at z = 0)

- **RockDrop**: `boss.Thrower.PlayThrow(tell)`, then a rock arcs from `boss.HandPosition` up and out of view. A `GroundMarker` (flat world-canvas
  ring like the ninja SwirlRing, radial `Image.Type.Filled`, `WindowMarker`-style pulse) sits on the road at `(markerX, 0.02, impactZ)`.
  - **Tracking**: `markerX = SmoothDamp(markerX, steerer.Position.x, trackSmoothing)`.
  - **Locked**: hold `markerX`, blink, and drain the ring fill.
  - **Drop**: the rock falls `y: 12 -> 0` with `t^2`.
  - **Impact**: hit if `|steerer.Position.x - markerX| < rockRadius + settings.vehicleHalfExtents.x`. Then `VisualEffect.RockImpact`, shake and damage.

  Keep `impactZ = 0` so the rule is instant. A marker under the car is visible because the camera sits 3 m up and 6.5 m back at 8.8 deg.
  Danger call: `WorldPrompt.Show(text, dangerColor, seconds)` and `FlashDanger(0.45)`.
- **MinionThrow (left/right)**: the Boss throws a small `BrickMonster` (or a primitive minion) along an arc onto the car. It parents to the `VehicleRig`
  at `(+-0.9, 0.6, 0)` and ticks damage every `dotSeconds`. At the same time a `SideWall` rises on that side at `x = +-wallInnerX`, running from z -6 to +30 and
  growing up from the floor. **Clear rule**: `side * steerer.Position.x >= smashX` (the car physically reaches only 2.2, so use `smashX ~ 0.85 *
  halfWidth`, configurable) while the wall is up. Smash: `VisualEffect.BrickBurst` at the contact, `SoundEffect.PieceHit`, shake, the minion bursts, the wall sinks.
  Put the wall's inner face at about 3.0 so the car visually touches it (outer edge 2.9).
- **GroundSlam**: a Boss slam tween (drop the body root and squash; `BossRig` has no slam pose, so it is new) with `VisualEffect.SlamShockwave` at the Boss's feet,
  `SoundEffect.TeammateSlam` and a big shake. Then spawn 1 to 3 rows with masks from `new ObstaclePatterns(VehicleType.Car).Next()`. Each blocked cell first
  shows a crack/glow decal and rises from the floor when `row.z <= riseZ` (about 10 m, which is 0.7 s at speed 14). Resolve exactly like `Overlaps` with the car. A hit
  bumps (`speedFactor`) and does damage.
  **Extend `ObstacleBlock`**: add `[SerializeField] bool growFromFloor` and, when true, scale only y and set `localPosition.y = size.y * 0.5 * k`.
  Chest Chase keeps the default `false`. Alternatively write a separate `RisingBrick : MonoBehaviour, IPoolableObject` + pool. Don't subclass and
  redeclare a private `Update`.
- **Pickups + homing rockets**: a `RoadPickup` row (one lane cell, floating, spinning). It resolves at `z <= 0` with the same overlap math and plays
  `VisualEffect.PickupSparkle` + `SoundEffect.PickupPower`. Collect k gives k rockets staggered by 0.1 s. `HomingRocket.Launch(from, Transform target, seconds)` evaluates a
  quadratic Bezier `p0 -> p0 + up*3 + side -> target.position` **re-read every frame**, so it always lands on the moving Boss
  (the `RockPieces.LaunchAsync :102-117` arc idea with a live end point). On arrival: `boss.PlayHit()`, `hp -= rocketDamage`, `VisualEffect.Explosion`.
  The 5th pickup triggers the cutscene after the last rocket lands.
- **DevilWind (sky)**: the existing sky rows (`Spawn` with sky masks), using a `windBlockPool` whose prefab is a swirl-looking block. The Boss "blows"
  (VFX at its mouth, wind streaks along z). Resolve uses `Overlaps` with the y test.
- **Tired + LaserVolley**: the Boss follows a lemniscate at fixed `z = skyGap`:
  `x = A * sin(w t)`, `y = center + B * sin(2 w t) / 2` (A about 2.0, B about 1.6, inside the steer box).
  The plane auto-fires every `fireInterval` along `+z`. A hit counts if `|plane.xy - boss.xy| < hitRadius`. Use `VisualEffect.LaserMuzzle` at the
  nose, `LaserImpact` + `SoundEffect.MechaHit` on a hit, and a stretched emissive cube or LineRenderer beam. The Boss wakes when hp01 crosses the next value in
  `wakeThresholds = {0.70, 0.30, 0.05}`, with a roar (SlamShockwave).
- **Clap**: the wind phase eases `steerer.TargetOffset.y` toward `+halfHeight` (the plane is pushed to the top). Two `SpikeWall`s appear at
  `x = +-(halfWidth + 3)`, spanning a configured height band, then close toward x = 0 over `closeSeconds` and hit at the center.
  **Open design question**: the safe zone is not specified. A proposal: the walls cover the band from the top down to `safeY`, and the
  player must dive below it against the wind. The second clap lowers `safeY` ("bigger"). The hit test at the clap moment is `steerer.Position.y > safeY`.
- **Explode**: chain 3 to 5 `VisualEffect.EnemyExplosion`/`Explosion` at random points on the Boss's bounds over about 1.5 s with shake, then `boss.Burst()`
  (BrickBurst at scale 3) + `SoundEffect.BossDeath` + `StingerType.Victory`.

### 12.6 Editor builder pattern (copy `ChapterWorldBuilder.BuildRunner :224-283`)

```csharp
var root = new GameObject("BossBattleWorld");
var world = root.AddComponent<BossBattleWorld>();
var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 3f, -6.5f), new Vector3(8.8f, 0f, 0f), 60f);
camera.farClipPlane = 140f;                                   // the giant Boss sits at z 25-40
var road = NinjagoWorldBuilder.BuildRoad(root.transform, out var roadStrip);          // length 120
var car = NinjagoWorldBuilder.BuildCar(root.transform);
var skycraft = NinjagoWorldBuilder.BuildSkycraft(root.transform);
var bricks = Empty("RisingBrickPool", root.transform, Vector3.zero).gameObject.AddComponent<ObstacleBlockPool>();
Set(world, "risingBrickPrefab", LoadComponent<ObstacleBlock>(NinjagoWorldBuilder.BarrierPath));
Set(world, "bossPrefab", LoadComponent<ChapterEnemy>(ChapterActorsBuilder.BossPath));
SavePrefab(root, PrefabsRoot + "/BossBattle/BossBattleWorld.prefab");
```

Helpers come from `using static Nex.Ninjago.Editor.NinjagoEditorUtils` (`Block`, `Empty`, `Set`, `SetArray`, `SavePrefab`, `PrefabsRoot =
"Assets/Prefabs/Ninjago"`, `ConfigsRoot = "Assets/Configs/Ninjago"`) and `NinjagoAssetsBuilder` (`Lit(name, color, smoothness, emission)`, `Particle`).
Extract the inline flyer (`ChapterWorldBuilder.cs:266-270`) into `internal static Transform BuildBossFlyer(Transform parent)` and call it from both places.

---

## 13. Gotchas

1. `RunnerWorld` cannot be extended: everything useful is private, `segments` is static (`:17`), and analytics are hard-wired to chase events. Compose instead.
2. `ShakeCamera` writes the camera position every `Step` (`:340-351`), which kills any concurrent camera tween. Separate the base pose from the shake.
3. Pools are never prewarmed in the runner, and `new Row` allocates per spawn. On low-end Android, call `Prewarm(n)` for every pool in `Initialize` and pool row records.
4. `ObstacleBlock` grows from its center, not from the floor, and its `Update`/`Show` are not virtual (see 12.5).
5. Pool transforms must sit at the world root origin, because `Show` writes `localPosition` relative to the pool parent.
6. The car's hit test ignores y (a car can't jump). The car's lateral reach is +-2.2 with an outer edge of 2.9. Side walls and the smash threshold must respect this.
7. Resolve is a one-shot test at the z crossing that uses the smoothed position. The boss-relative logic (rocks, lasers) is instantaneous too. Keep the
   tolerances generous for kids (`cellFill 0.9` is the existing margin).
8. In `RunnerBlock` the Boss gap jumps instantly (`:222`). Smooth `bossGap` with `SmoothDamp` in the new `BossActor`.
9. The RunnerTrack Boss figure has no rig. Use `Boss.prefab`. `ChapterEnemy.Initialize` uses `renderer.material` (instanced copies), which is fine for one Boss.
   `BossRig` animates in **unscaled** time, so slow motion via `timeScale` won't slow it. `Burst()` deactivates immediately.
10. Scale: `Boss.prefab` is already scaled 3 (about 8 m). For a "giant" Boss, parent it under a root scaled about 2 (16 m) at z 30 to 40. The vertical half view at 36 m
    with FOV 60 is about 21 m, so it fits. `burstScale` and VFX scale do not follow parent scale, so pass bigger scales to `PlayVisualEffect`.
11. `ObstaclePatterns` uses the global `UnityEngine.Random`. For reproducible attack schedules use a seeded `System.Random` (`FightBlock` does this).
12. `VehicleType` is shared with the Chapter (`RunnerRideExtensions`, `ChapterValidator`, `ChapterGenerator`) and the `ChaseConfig` asset. **Don't add a
    vehicle type for the boss fight.** Hold `ChaseConfig.VehicleSettings` per `BossPhase` in a new config.
13. Chest Chase Debug rows mutate the shared `ChaseConfig` asset. The Boss Battle can read steering globals (deadzone, full lean/rise, P1 share) from
    `GameConfigsManager.Instance.ChaseConfig` so one calibration tuning applies everywhere. Its vehicle settings live in `BossBattleConfig`.
14. HD-2D: `RunnerView` creates a full-screen RT (`:79`). For the low-res look, create e.g. 640x360 (aspect-matched) with `filterMode = Point`. The feed
    `RawImage` already upscales it. World-space canvases (`PlayerSteerMarker`, `WorldPrompt`) render **into** that RT and pixelate. That is acceptable with the
    pixel font, but check readability at 360p. The alternative is screen-space markers via `WorldToViewportPoint` (see `Docs/BilliardRogue/research/urp-hd2d-rendering.md:202`).
15. Cinemachine is optional. If you add a brain, the gameplay code must move **vcam** transforms, never the brain's camera (the Chapter pattern: a disabled
    `Camera` on the vcam object is what `RunnerWorld.worldCamera` points at).
16. `RenderSettings` (fog, ambient) are global. Restore them when the Boss Battle ends (the Chapter does not).
17. `ViewIdentifier` has implicit values: append at the end only. `GameModeType` next value is 15. EnumDictionary keys need explicit values, append only,
    ascending (see the comment in `VehicleType.cs:3`).
18. Every UI action and result needs analytics. The runner logs only `segment_start/obstacle_dodge/obstacle_hit` (`Analytics/NinjagoAnalytics.cs:107-123`). Add
    Boss events (`boss_phase_start`, `boss_attack`, `boss_attack_result`, `pickup`, `rocket_hit`, `laser_hit`, `boss_defeated`) in a new region.

---

## 14. Checklist (runner/boss subsystem)

- [ ] `BossBattleEnums.cs` (`BossPhase`, `BossAttack`, explicit values) and `BossBattleConfig` SO with `EnumDictionary<BossPhase, ChaseConfig.VehicleSettings>`,
      `EnumDictionary<BossAttack, AttackSettings>`, HP/rockets/wake thresholds, smash threshold, clap bands, and setters for Debug rows.
- [ ] `VehicleSteerer` (copy of `RunnerWorld.Step :192-211` + `TargetOffset` + clamp) with NUnit tests next to `NinjagoLogicTests`.
- [ ] `LaneGrid` static (copy of `CellCenter`/`Overlaps`) with tests for the car/sky overlap and the side-smash rule.
- [ ] `ObstacleBlock.growFromFloor` (default false; Chest Chase unchanged), or a separate `RisingBrick` + pool.
- [ ] `BossActor` around `Boss.prefab`: HP, smoothed gap, run-ahead placement, flyer mount/take-off, lemniscate, tired pose, slam tween, explode chain.
- [ ] `BossBattleWorld`: composition of car/skycraft `VehicleRig`, road strip + new arena strip, prewarmed pools, pooled rows, base pose + shake,
      `RunRoadPhaseAsync` / `RunSkyPhaseAsync` / `PlayTransformAsync`, events.
- [ ] Attacks: RockDrop (marker track, lock, drop), MinionThrow (DoT + side wall + smash), GroundSlam (rising bricks), DevilWind (sky rows),
      Clap (wind push + spike walls, bigger second time), LaserVolley (auto-fire, xy hit), pickups + homing rockets (k rockets on pickup k, 5th ends Phase 1).
- [ ] Editor: `BossBattleWorldBuilder` reusing `BuildRoad/BuildSky/BuildCar/BuildSkycraft`, `ChapterActorsBuilder.BossPath/EnemyPath/RockPath`,
      an extracted `BuildBossFlyer`, new props from primitives, and the dark arena strip. Add it to `NinjagoBuildAll`.
- [ ] Game: `BossBattleGame : NinjagoGame` (`GameModeType.BossBattle = 15`), the world plus a stage canvas outliving the step views, PiP via
      `AreaPreviewFrame`/`PlayerIndicatorsManager` exactly as `RunnerView.Initialize :91-94`, BGM `Boss`, outcome + `ScopedNinjagoProgressUpdate`.
- [ ] Views appended to `ViewIdentifier`, one per step. Back routes to `NinjagoAnalytics.UiAction(screen, "back")`.
- [ ] Low-res point RT for the world feed. Verify world-space badge and prompt readability at that resolution.
- [ ] Performance pass on a low-end Android device: prewarm everything, no per-frame allocations, shadow distance about 40 m, a cap on simultaneous rockets
      and VFX.
- [ ] Decide the clap safe-zone rule and the car HP / fail rule (Chest Chase never fails, but the spec's minion damage over time implies car HP).
