# Boss Battle research: setup, detection, multiplayer, PiP preview, player indicators, camera-free testing

Scope: how a new Boss Battle mini-game gets calibrated players, 1P/2P detection, the corner camera preview (PiP), over-head
player indicators (in-world and in the PiP), and how to drive it with no camera (keyboard and Unity CLI).

Everything below was read from source in `ninjago-unity/` on 2026-10-22, except items tagged **[UNVERIFIED]**.
Paths are relative to `ninjago-unity/Assets/Scripts/` unless they start with `Assets/` or `Prefabs/`.
The sibling note `Docs/BilliardRogue/research/detection-setup-preview.md` covers the MDK internals: Jazz types, the
smoother, coordinate spaces and the prefab YAML. This note only repeats what Boss Battle needs, and it records where the
Ninjago code differs from that note. In particular, the `ClearTrackers` bug and the ungated S key are both fixed here.

---

## 0. TL;DR

| Question | Answer |
|---|---|
| What does setup give me? | `NinjagoGame.CalibrateAsync` pushes or replaces `NinjagoSetupView` and returns `List<PlayerBody>?`. The list holds the **calibrated** players, a subset of the requested count, sorted by index. It is `null` when the player backs out. Each `PlayerBody` has a stored chest `Origin`. `TryGetLeanInches` returns the chest offset from that origin in body inches. |
| How is the car steered? | `RunnerWorld.Step` calls `player.TryGetLeanInches`, then `ChestSteer.Steer` (deadzone 1.5 in, full lean 7 in), then `SharedSteer.Blend` (P1 share 0.5, renormalized over tracked players). It then SmoothDamps the result to `x = steer.x * halfWidth (2.2)`. |
| Multiplayer | `GameDriver` calls `DetectionManager.Initialize(game.NumOfPlayers)`. `NinjagoGame.Initialize` then creates **one `OnePlayerDetectionEngine` + one `PlayerBody` per player index**. The count comes from `PlayerPreference.numPlayers` (clamped 1..2), saved by `PlayerCountView` in the menu. P1 stands left (x ratio 0.3) and P2 stands right (0.7). |
| PiP | Use the `CameraPreview` widget: a dark panel with an `AreaPreviewFrame`, a `RawImage` and a `PlayerIndicatorsManager`. It sits bottom-right, 384×216 plus an 8 px border, inside the view's own canvas. Call `pipFrame.Initialize(PlayAreaController)` and `pipIndicators.Initialize(requestedPlayers, activeIndexList, pipFrame, BodyPoseDetectionManager)`. |
| Indicators | In the **PiP**, `PreviewFramePlayerIndicator` draws a P1 red or P2 blue triangle 8 inches above the nose. In the **world**, `PlayerSteerMarker` is a world-space canvas over the vehicle with a "P1"/"P2" tag and a lean arrow. There is one per player on both the Car and the Skycraft, and it is driven by `VehicleRig.SetSteer`. |
| Testing with no camera | Turn on Debug Settings → **"Ninja: Simulated Body"** (`DebugSettings.ninjaSimulatedBody`). Setup then completes instantly and `PlayerBody.TryGetLeanInches` returns `SimulatedBody.GetLeanInches`. Keyboard: P1 uses **A/D** (lean ∓8 in) and **W/S** (rise ±6 in). P2 uses **J/L** and **I/K**. From the CLI, call `Nex.Ninjago.SimulatedBody.Set(player, leanX, leanY, false)` (verified to bind). `simulate_key` is unavailable because the project uses legacy Input only. |

---

## 1. Runtime chain (who calls what)

```
MainScene: GameModeSelectionView → MainCoordinator.GameModeSelectionOnModeSelected (Main/Coordinator/MainCoordinator.cs:68)
   maxPlayers > 1 ? push PlayerCountView : load GameScene
   PlayerCountView.PlayersChosen → ScopedPlayerPreferenceUpdate(p => p.numPlayers = n) (MainCoordinator.cs:90-94) → LoadScene(GameScene)
GameScene roots: SingletonSpawner, GameDriver (INACTIVE; in SingletonSpawner.activatePostSpawn), DetectionManager (prefab),
                 MainViewManager (prefab), Main Camera, EventSystem, UIBackground
GameDriver.RunSelectedGameAsync (Gameplay/Core/GameDriver.cs:44-59)
   gamePrefab = Addressables.Load(gameModeConfig.GetMode(GameConfigsManager.Instance.SelectedMode).gamePrefab)
   game = Instantiate(BaseGame) → detectionManager.Initialize(game.NumOfPlayers)  // once per scene
   game.Initialize(detectionManager, viewManager) → ScreenBlocker.Hide → await game.RunAsync → load MainScene
NinjagoGame (Ninjago/Flow/NinjagoGame.cs)
   Initialize (46-59): per playerIndex < NumOfPlayers: Instantiate(detectionEnginePrefab, enginesRoot); engine.Initialize(i, bpdm);
                       bodies.Add(new PlayerBody(i, playersConfig.GetPlayerColor(i), engine)); Editor: Application.runInBackground = true
   RunAsync (65-89): loop { active = CalibrateAsync(firstRound); outcome = PlayAsync(active); result view; Retry → loop }
   CalibrateAsync (96-110): Instantiate(setupViewPrefab).Initialize(bodies, Detection, playersConfig, SetupCursors, SetupRobotArms,
                       SetupStoresRestJoints); first round PushView, retry ReplaceView; return await setupView.RunAsync(ct)
   PlayAsync (113): abstract. Each game replaces the setup view with its gameplay view (ChestChaseGame.cs:42-50)
```

- `GameModeType` is defined in `Gameplay/Core/GameModeType.cs`. Its last value is `Forge = 14`, so Boss Battle appends **`BossBattle = 15`**.
- `GameModeConfig.ModeDefinition` (`Configs/GameModeConfig.cs:16-23`) has three fields: `displayName` (`LocalizedString`), `gamePrefab` (Addressable), and `maxPlayers` (`Range(1,2)`). Set `maxPlayers = 2` or the menu will skip the player-count view. `NinjagoGamesBuilder.Mode(...)` already writes 2 (`Ninjago/Editor/NinjagoGamesBuilder.cs:143-153`).
- `GameConfigsManager.SelectedMode` (`Configs/GameConfigsManager.cs:75`) defaults to `None`. Pressing Play on `GameScene` directly therefore fails, so start from `MainScene`. Section 8 has a CLI shortcut.

## 2. `Nex.DetectionManager` (`Gameplay/Core/DetectionManager.cs`)

```csharp
public CvDetectionManager CvDetectionManager; BodyPoseDetectionManager BodyPoseDetectionManager;
public SetupStateManager SetupStateManager;   BasePlayAreaController PlayAreaController;
public void Initialize(int aNumOfPlayers);                 // :27  ConfigMdk (numOfPlayers, playerPositions x=GetXRatioForPlayer, y=.5,
                                                           //      enableNativeZoom) + playArea.Initialize + setupStateManager.Initialize
public void ConfigForSetup();                              // :36  play area unlocked, auto-tilt Recovery, consistency OFF, trackers ON, mode Base
public void ConfigForGameplay(bool lock = true);           // :45  play area + dewarp locked, consistency ON, SetupDetectorMode.Gameplay
public UniTask WaitForFirstDetection();                    // :113 never completes without a camera; don't await it untimed
public void StopDetection(); PauseDetection(); UnPauseDetection();   // :130-146
public const float PreviewTextureOff = -1f;                // :153
public void SetPreviewTextureInterval(float seconds);      // :164 0 = every camera frame (default), -1 = none, >0 = throttle
```

`SetPreviewTextureInterval` is a **perf lever** for 60 fps on low-end devices. The comment at `:159-163` says each preview refresh "uploads the whole camera frame into two textures on the render thread (about 9 ms on the Nex Playground)". `RunnerView` does not use it. Billiard Rogue's `BilliardRogue/Flow/GameplayPip.cs:28-66` does: it sets an interval while the feed is visible, uses `PreviewTextureOff` while hidden, and **resets it to 0 in `OnDestroy`**, because the next calibration needs the full rate. Boss Battle should copy that.

## 3. Engines and `PlayerBody` (what "lean" is)

- Engine prefab: `Prefabs/Ninjago/Detection/NinjaDetectionEngine.prefab`. It is a variant of `OnePlayerDetectionEngine.prefab` whose `smoothedNodePrefab` is overridden with a hidden node, so no circles render in the 3D world. `NinjagoWorldBuilder.EnginePath` points to it (`Ninjago/Editor/NinjagoWorldBuilder.cs:14`).
- `BaseOnePlayerDetectionEngine` (`Gameplay/Detection/BaseOnePlayerDetectionEngine.cs`):
  - It subscribes to `bpdm.processed.captureAspectNormalizedDetection` (`:144`). That event fires on every smoother Update, not on every camera frame.
  - It updates `RawPpi` (a 0.5 s average of the raw pose ppi, `:196-210`).
  - It stores `LastBodyPoseDetectionResult` and raises `NewDetectionCapturedAndProcessed` **only once `RawPpi > 0`** (`:172-194`).
- `PlayerBody` (`Ninjago/Common/PlayerBody.cs`) is a sealed plain class, one per player index:

```csharp
public PlayerBody(int playerIndex, Color color, OnePlayerDetectionEngine engine);   // :33
public int PlayerIndex; Color Color; Vector2 Origin; bool IsCalibrated; float RawPpi; BodyShape Shape; RestJoints Rest;
public void SetOrigin(Vector2 chest, Vector2? restingKneeDrops, BodyShape? shape);  // :42  sets IsCalibrated = true
public void ClearOrigin();                                                          // :55
public bool TryGetChest(out Vector2 chest);        // :66  smoothed chest, aspect-normalized (x 0..16/9 right, y 0..1 up)
public bool TryGetLeanInches(out Vector2 lean);    // :79  SIMULATED ? SimulatedBody.GetLeanInches(i) : (chest - Origin) / RawPpi
public bool TryGetHandInches(bool left, out Vector2); TryGetHand; TryGetTorso; TryGetKneeDrops; TryGetHeadInches;
public bool TrySampleShape / TrySampleUpperBody / TrySampleRestJoints; TryGetArmInches; TryGetHipInches (simulated-aware, :254)
public double BodyFrameTime;                       // :270 camera timestamp, use it to detect new camera frames
```

- "Body inches" means `RawPpi` units: frame heights per inch, derived from the player's limb lengths. Player height and distance cancel out. +x is the player's right, which is screen right because the frame is mirrored, and +y is up.
- `TryGetLeanInches` returns **false** while the player is not calibrated or the chest is hidden. `RunnerWorld` then passes a `null` steer, `SharedSteer` renormalizes, and the marker dims.
- `ChestSteer` (`Ninjago/Chase/ChestSteer.cs`): `Axis(lean, deadzone, full) = sign * clamp01((|lean| - deadzone) / (full - deadzone))`. The car passes `useVertical = false`.
- `SharedSteer.Blend` (`Ninjago/Chase/SharedSteer.cs:20-41`) computes a weighted average: P1 weight is `playerOneShare` and P2 weight is `1 - share`, both clamped to `[0.001, 0.999]` and renormalized over the players tracked this frame. `DominantPlayer` and `EffortShare(i)` feed analytics and results.
- Config `Assets/Configs/Ninjago/ChaseConfig.asset`:

  | Setting | Car | Skycraft |
  |---|---|---|
  | `deadzoneInches` | 1.5 | 1.5 |
  | `fullLeanInches` | 7 | 7 |
  | `fullRiseInches` | 4 | 4 |
  | `playerOneSteerShare` | 0.5 | 0.5 |
  | `halfWidth` | 2.2 | 2.2 |
  | `centerHeight` | 0 | 3.2 |
  | `halfHeight` | 0 | 1.6 |
  | `steerSmoothingSeconds` | 0.35 | 0.3 |
  | `maxSteerSpeed` | 7 | 7 |
  | `usesVerticalLean` | 0 | 1 |

  The lanes are `x = (column - 1) * cellWidth (2)`, which gives -2, 0 and +2.

## 4. Setup flow (`Ninjago/Views/NinjagoSetupView.cs`)

`NinjagoSetupView : SimpleCanvasView`. Its `Identifier` is `ViewIdentifier.NinjagoSetup`, its `Controls` is `Back`, and its analytics screen name is `"ninjago-setup"`.

| Prefab | Used by |
|---|---|
| `Prefabs/Ninjago/Views/NinjagoSetupView.prefab` | body-only games, **Boss Battle included** |
| `CursorSetupView.prefab` | adds `HandCursorCheck` |
| `MechaSetupView.prefab` | adds `RobotArmsCheck` |

**`Initialize(bodies, detection, NinjagoPlayersConfig, HandCursorTracker?, RobotArmsTracker?, bool storeRestJoints)`** (`:108-127`)
- It instantiates `PreviewsManager.prefab` **as its own scene root**, because its canvas is Screen Space Overlay (`:120`).
- It shows one `SetupPlayerStatus` row per body (`:121-126`).
- It destroys the previews in `OnDestroy` (`:129-132`).
- It hides the previews in `EnterBackground` and shows them again in `EnterForeground` (`:89-99`). The overlay would otherwise draw over a pushed Debug Settings view.

**`RunAsync(ct) → List<PlayerBody>?`** (`:139-176`)
1. Calls `detection.ConfigForSetup()` and `previewsManager.Initialize(bodies.Count, bpdm, playArea, setupStateManager)`. That creates one `SetupInfoPreviewFrame` per player, each holding a `PlayerSetupPreviewFrame` slice, a `SetupWarningMessage` and an indicator.
2. Subscribes to `setupStateManager.PlayerTrackerUpdated`. A player counts as `inGoodPosition` when their tracker state is `WaitingForRaisingHand` or `Playing` (`:358-363`).
3. Calls `ClearOrigin()` on every body, then `await previewsManager.MoveIn(true)`.
4. Runs **`CalibrateAsync`** (`:182-214`) in a loop every Update with unscaled time. For each uncalibrated player it calls `UpdateHold` (`:217-275`):
   - **Simulated body:** `SetOrigin(chest or zero)` → status `Ready` → done (`:222-227`). Nothing is checked.
   - Not in a good position or no chest → hold reset, status `Waiting`.
   - Otherwise it accumulates chest, knee drops, `BodyShape`, and `RestJoints` if `storeRestJoints`. Status is `Holding`. Moving more than `calibrationStillnessInches` (3) restarts the hold.
   - After `calibrationSeconds` (1.0) it calls `SetOrigin(avg chest, avg knee drops, avg shape)` → status `Ready`, plays `SfxManager.UiSelect`, and logs `NinjagoAnalytics.PlayerCalibrated(i, secs)`.
   - The prompt is `jointsPrompt`, `holdPrompt` or `standPrompt` (keys `ninjago.setup.joints/hold/stand`).
   - The loop exits when **all** players are calibrated, or when `SoloTimeUp` (`:342-345`) is true. In 2P mode the missing player is dropped `soloFallbackSeconds` (8) after the first player calibrated, **counted from the last time the missing player's chest was seen**. A player still standing in frame is always waited for.
5. Shows `readyPrompt`, or `soloPrompt` when fewer players were calibrated than requested. Waits `readyHoldSeconds` (0.8), then `MoveOut`, then **`detection.ConfigForGameplay()`**.
6. Runs the optional hand-cursor and robot-arms checks. Boss Battle passes `null` for both.
7. Logs `NinjagoAnalytics.SetupComplete(requested, active, secs)` and returns the calibrated list. `OnBackButton` sets `quitRequested`, which makes it return `null`.

Config `Assets/Configs/Ninjago/NinjagoPlayersConfig.asset` (`Ninjago/Configs/NinjagoPlayersConfig.cs`):
- `playerColors`: P1 is (0.95, 0.3, 0.25), red. P2 is (0.2, 0.55, 1), blue. These match the PiP triangle sprites.
- `calibrationSeconds` 1, `calibrationStillnessInches` 3, `soloFallbackSeconds` 8, `readyHoldSeconds` 0.8, `handCheckSeconds` 0.8.

`SetupPlayerStatus` (`Ninjago/Views/SetupPlayerStatus.cs`):
- It has three states: `State { Waiting, Holding, Ready }`.
- `Initialize(int playerIndex, Color)` → `PlayerTagLabel` + ready check tint.
- `Show(State)` swaps the `NexLocalizedString.StringReference`.
- `PlayerTagLabel.Initialize(i, color)` uses the smart string key `ninjago.player.tag` with the argument `{player}` = i + 1 (`Ninjago/Views/PlayerTagLabel.cs:23-27`).

`SetupStateManager` (`Gameplay/Setup/SetupStateManager.cs`). NinjagoSetupView only reads tracker states from it:
- `Initialize(n, bpdm, playArea)` (`:31`).
- `SetTrackingEnabled(bool)` (`:44`) is a no-op when it is already tracking. Trackers are created once and **keep running during gameplay**.
- `ResetSetupStates()`.
- `PlayerTrackerUpdated` event (`:25`).
- `WaitForGoodPlayerPosition()` / `WaitForRaiseHand()` (`:148`, `:175`). These are not used by Ninjago.
- `ClearTrackers` now also clears `playerStates` (`:91-102`). The bug noted in the BR doc is fixed.
- The **S debug key** now goes through `DebugInput` (`:79-84`), so it does nothing in `PRODUCTION` builds.
- Tracker states, in `Gameplay/Setup/OnePlayerSetupStateTracker.cs:65`, with transitions at `:209-283`. Time is `Time.fixedTime`:
  - `enum SetupStateType { Preparing, WaitingForGoodPlayerPosition, WaitingForRaisingHand, Playing, PlayingButNoPose }`
  - `Preparing` → `WaitingForGoodPlayerPosition` once 0.5 s of data has arrived.
  - → `WaitingForRaisingHand` when the good-position ratio exceeds 0.7 over 1.5 s.
  - It goes back when that ratio drops below 0.5.
  - It moves to `Playing` only if `SetAllowPassingRaisingHandState(true)` was called. Ninjago never calls it except through the S key, so players normally **stay in `WaitingForRaisingHand`** during gameplay.
  - `Playing` → `PlayingButNoPose` after 2 s unseen.
- `SetupWarningMessage` (under the setup previews) is now **localized** (`Gameplay/Setup/SetupWarningMessage.cs:39-105`, `LocKeys.Setup`).

## 5. Multiplayer (1P / 2P)

- **Count.** Set once per scene load: `NinjagoGame.NumOfPlayers => Clamp(PlayerPreference.numPlayers, 1, SimulatedBody.MaxPlayers = 2)` (`NinjagoGame.cs:42`). `PlayerCountView` (`Ninjago/Views/PlayerCountView.cs`) has 1P/2P buttons and a `GroupKeyResponder` focused on the remembered count. It raises `PlayersChosen(int)` and logs `UiAction("player-count", "one_player"|"two_players")`. `MainCoordinator` saves the choice.
- **Engines.** Each player gets its own `OnePlayerDetectionEngine` (player index i) and `PlayerBody`. The MDK tracks `numOfPlayers` poses at standing spots x = 0.3 (P1, left) and x = 0.7 (P2, right) (`Gameplay/Player/PlayerPositionDefinition.cs:9-37`). `ConfigForGameplay` turns on tracking consistency so the identities stick. Players must not swap spots.
- **Requested vs active players.** Setup can return fewer players than requested, through the solo fallback. Gameplay code must iterate the **returned list** and key everything by `player.PlayerIndex`:
  - `VehicleRig.Initialize(activePlayers)` enables only those markers (`Ninjago/Chase/VehicleRig.cs:23-32`).
  - The PiP indicators get `requestedPlayers` plus the active index list (`RunnerView.cs:91-94`).
  - Steering arrays are sized `SimulatedBody.MaxPlayers` and indexed by player index (`RunnerWorld.cs:70-71`).
- **Shared vehicle.** In 2P mode, steering is the **average** of both leans. Opposite leans cancel out. For "smash the car into the left/right wall" in 2P, both players have to lean, or one leans fully while the other stays neutral. A lone player at full lean on a blend of 0.5 reaches only half steer, unless the other player is untracked. This is a game-design consequence of `SharedSteer`; tune it per attack.
- **Retry.** `RunAsync` loops through `CalibrateAsync(push: false)`, which replaces the result view with a fresh setup view. Engines, bodies, `DetectionManager` and trackers are reused. Every `PlayerBody.ClearOrigin()` runs at setup start.

## 6. PiP corner camera preview

Built by `NinjagoViewsBuilder.CameraPreview(ui, anchor, position, size, out AreaPreviewFrame frame)` (`Ninjago/Editor/NinjagoViewsBuilder.cs:369-388`). RunnerView's instance (`Prefabs/Ninjago/Views/RunnerView.prefab`, built at `NinjagoViewsBuilder.cs:179`) has this hierarchy:

```
RunnerView (Canvas; SimpleCanvasView sets RenderMode.ScreenSpaceCamera + ViewManager camera; CanvasScaler 1920x1080 match 0)
└─ UI
   └─ CameraPreview [Image "Panel" sliced, color (0.08,0.08,0.1,0.9), raycast off] [PlayerIndicatorsManager: indicator prefab
      │   Prefabs/Detection/Preview/PreviewFramePlayerIndicator.prefab, playerIndicatorSizeRatioToPreviewHeight 0.2]
      │   anchor = pivot (1,0) bottom-right, anchoredPosition (-24, 24), sizeDelta (400, 232) = 384x216 + 16
      └─ PreviewFrame [CanvasGroup][AreaPreviewFrame rawImage, canvasGroup, enableSmoothing 0] stretch, inset 8 px, pivot (0,0)
         └─ RawImage [RawImage, raycastTarget off] stretch
```

- **Other placements.** FightView centres it at the bottom with `(0.5,0),(0,24)`. CrouchRunView puts it top-left with `(0,1),(20,-20)`. ChapterStage uses bottom-right, the same as Runner.
- **No post-processing.** The view canvases draw with the `MainViewManager` `RootCamera`: orthographic, `m_RenderPostProcessing: 0` (`Prefabs/Coordinators/MainViewManager.prefab:111`). The 3D world is drawn by its own camera into a `RenderTexture` that is shown through a `RawImage` "WorldFeed". The PiP is therefore **not** pixelated or bloomed by the HD-2D world pass. Keep it on the view canvas and never put it in the world RT.
- **`AreaPreviewFrame`** (`Gameplay/Preview/AreaPreviewFrame.cs`):
  - `Initialize(BasePlayAreaController)` (`:33-46`) registers as `CvDetectionManager.previewController` handler and sets `canvasGroup.alpha = 0`.
  - The first texture fades the frame in over 0.5 s (`:68-73`).
  - It shows a centre crop of the play area with the frame's aspect. A 16:9 PiP shows exactly the play area: the one player in 1P, both players in 2P.
  - It caches the frame's world rect and aspect **once**, on the first texture (`PreviewFrameBase.cs:33-43`). Don't scale it from 0 or resize it around the first texture. Animate `CanvasGroup.alpha` or the position instead.
  - It unregisters in `OnDestroy`.
- **With no camera, nothing renders.** No texture arrives, so the feed stays at alpha 0. Only the dark panel is visible. That is expected in simulated tests.

## 7. Over-head player indicators

### 7.1 In the PiP: `PreviewFramePlayerIndicator` (`Gameplay/Preview/Indicator/`)

```csharp
// PlayerIndicatorsManager.cs
public void Initialize(int aNumOfPlayers, List<int> aPlayerIndexList, PreviewFrameBase aPreviewFrame, BodyPoseDetectionManager bpdm); // :22
public void Initialize(int aNumOfPlayers, PreviewFrameBase aPreviewFrame, BodyPoseDetectionManager bpdm);                        // :36 all 0..n-1
public void SetActivePlayer(int activePlayerIndex);   // :51 Active (scale 1.4 + yoyo bounce) / Dimmed (alpha 0.45) / -1 = Neutral
// PreviewFramePlayerIndicator.cs
public enum HighlightState { Neutral, Active, Dimmed }  // :14
public void Initialize(int playerIndex, BodyPoseDetectionManager bpdm, PreviewFrameBase previewFrame, float sizeRatioToPreviewHeight); // :76
public void SetHighlight(HighlightState state); public int PlayerIndex;
```

- **Instances.** Indicators are instantiated **under the preview frame transform** (`PlayerIndicatorsManager.cs:66`) and kept in a list, which gives you `SetActivePlayer`.
- **Data source.** Each indicator subscribes to the **raw** `bpdm.captureAspectNormalizedDetection`: camera rate, unsmoothed (`:88`).
- **Per detection** (`:108-159`) it:
  - clones the pose;
  - takes `nodeToFollow` (prefab: `Nose`) plus `offsetInInches` (0, 8) × ppi, where ppi is averaged over 0.3 s;
  - applies OneEuro(4, 10);
  - remaps from `PreviewRectInAspectNormalizedSpace()` into the frame rect, clamped so the icon stays inside;
  - sets `anchoredPosition` and `sizeDelta = previewHeight * ratio`.
- **Hiding.** The indicator hides when there is no pose, no node, or ppi ≤ 0.
- **Sprites.** `spriteByPlayerIndex` holds `player-indicator1` (red), `2` (blue), `3` (green), then `1` again.
- **Boss Battle.** The steering is shared, so call `SetActivePlayer(sharedSteer.DominantPlayer)` (-1 means neutral) if you want the PiP to pulse whoever is steering. The vehicle markers already show each player's lean, so this is optional.

### 7.2 In the world: `PlayerSteerMarker` over the vehicle (`Ninjago/Chase/PlayerSteerMarker.cs`)

```csharp
public void Initialize(int playerIndex, Color aColor);   // :25  tag "P{n}" in color, steer zeroed
public void SetSteer(Vector2 steer, bool tracked);       // :36  arrow hidden if |steer| <= 0.05; rotated atan2(steer); x-scale 0.4+0.6*|steer|;
                                                         //      color = player color, alpha 0.25 (untrackedAlpha) when untracked
```

- **Built by** `NinjagoWorldBuilder.BuildSteerMarkers(rig, vehicle, height)` (`Ninjago/Editor/NinjagoWorldBuilder.cs:358-375`). That method is `private static`; make it `internal` to reuse it.
- **Objects.** Each marker is a **world-space Canvas** `SteerMarkerP{1,2}` parented to the vehicle root, not to the banking `body`. It is 140×140 at scale 0.01, which is 1.4 m.
  - Car: local x = ∓0.65, y = 1.75.
  - Skycraft: local x = ∓0.65, y = 0.75.
  - Each holds a `PlayerTag` (`PlayerTagLabel`, size 56, at (0, 30)) and an `Arrow` Image (80×50, pivot (0, 0.5), at (0, -35)).
  - They have **no billboard**. They face the chase camera only because the camera sits behind the vehicle looking along +z.
- **Wiring.** `VehicleRig` (`Ninjago/Chase/VehicleRig.cs`) has `markers[]` indexed by player index (`:15`).
  - `Initialize(activePlayers)` activates and initializes only the active players' markers (`:23-32`).
  - `SetSteer(playerIndex, steer, tracked)` (`:46-49`) is called every `RunnerWorld.Step` for every active player (`RunnerWorld.cs:193-203`).
  - `RunnerWorld.Initialize` calls `car.Initialize(players)` and `skycraft.Initialize(players)`, so both vehicles are ready for the Phase 2 swap.
- **Other world tags.** A plain `PlayerTagLabel` world canvas with a down "Pointer" (`BuildPlayerTagCanvas`, `NinjagoWorldBuilder.cs:409-421`) is used over characters: `NinjaRig.marker`, `StreetNinja.marker`, `BossRig.marker`, `CrouchNinjaRig.marker` and `KidNinja.marker`. Use it if Boss Battle shows the ninjas on foot, for example during the cutscene.

## 8. Testing without a camera (keyboard, Debug Settings, Unity CLI)

### 8.1 The switch

`DebugSettings.ninjaSimulatedBody` (`PlayerData/DebugSettings.cs:103-104`) has the row label "Ninja: Simulated Body (keys A/D W/S Space, J/L I/K RightShift, mouse, punch Q/E U/O)" and `DebugOrder(-100)`, so it is near the top of the panel.

```csharp
// Ninjago/Common/SimulatedBody.cs:21-31
public static bool IsEnabled => #if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
                                  PlayerDataManager.Instance.DebugSettings.ninjaSimulatedBody  #else false
```

- **Where to toggle it.** Open the Debug Settings panel with the secret code ↑↑↓↓←→←→ (`SecretCodeSequenceDetector` on `MainViewManager.prefab`, added by `NinjagoGamesBuilder.AddSecretCode`, `:172-197`).
- **Persistence.** The setting is saved with Easy Save: `PlayerDataManager.SaveDebugSettings()` writes ES3 key `debugSettingsDataKey`. It **persists across Editor sessions** until it is turned off.
- **Live reads.** `IsEnabled` is read live, so a toggle takes effect immediately.

What the simulated body changes:

| Site | Effect |
|---|---|
| `NinjagoSetupView.UpdateHold` (`:222-227`) | Every player is "Ready" on the first frame. Setup takes about MoveIn + 0.8 s; nothing else is checked. |
| `PlayerBody.TryGetLeanInches` (`:81-85`) | Returns `SimulatedBody.GetLeanInches(i)` and **always true**, so the player is tracked. |
| `PlayerBody.TryGetHipInches` (`:256-261`) | Lean plus a hip shake while storm is held. |
| `UpperBodyStream` | Fixed poses for gesture games. Not used by the chase. |
| `ChapterGame.CountLostPlayersAsync` (`Flow/ChapterGame.cs:243`) | The lost-player check is skipped. |

The simulated body does **not** drive the PiP feed or PiP indicators, which only use the real MDK stream. It also does not drive `SetupStateManager` tracker states.

### 8.2 Keyboard (`Ninjago/Common/SimulatedBodyKeyboard.cs`)

`SimulatedBodyKeyboard` is a MonoBehaviour added to the root of **every** Ninjago game prefab by `NinjagoGamesBuilder.WireShared` (`:89`). Prefab values: `leanInches` 8, `riseInches` 6, `playerTwoCursorSpeed` 1.2. The bindings (`:25-35`) are:

| Player | Lean left / right | Rise / drop | Storm + knee pulse | Punch L / R |
|---|---|---|---|---|
| P1 (index 0) | **A / D** (x ∓8 in) | **W / S** (y ±6 in) | Space | Q / E |
| P2 (index 1) | **J / L** | **I / K** | RightShift | U / O |

- **Mouse.** It moves P1's right-hand cursor. In addition, I/J/K/L move P2's cursor.
- **Writes only on change.** `SimulatedBody.Set` runs only when one of that player's keys goes down or up (`AnyKeyChanged`, `:50`). Values set from the CLI therefore **persist** until a key for that player changes.
- **Full steer.** Lean 8 is past `fullLeanInches` 7, so A and D give full steer to ±2.2 (the road edge, which is the "wall" for side minions). W and S give full rise or dive on the Skycraft, because 6 is past `fullRiseInches` 4.
- **Production builds.** `DebugInput` (`Utils/DebugInput.cs`) returns false or zero under `PRODUCTION`.
- **S key overlap.** **S** is also `SetupStateManager`'s debug key (§4). Pressing it while simulated lowers P1's chest and sets `allowPassingRaisingHand`. This is harmless.

### 8.3 Unity CLI (automated playtest). `simulate_key` does not work.

- **Why not keys.** `ProjectSettings/ProjectSettings.asset` has `activeInputHandler: 0` (legacy only), and `Packages/manifest.json` has no `com.unity.inputsystem`. `simulate_key` therefore soft-fails, as the BR cookbook (§8) notes. An unfocused Editor also receives no key events.
- **What to do instead.** Drive `SimulatedBody` directly.
- **Verified 2026-10-22, edit mode, read-only.**
  - `eval` binds `Nex.Ninjago.SimulatedBody.MaxPlayers` and returns 2.
  - Build scenes are `MainScene`, `GameScene`, …, and the active scene is `Assets/Scenes/MainScene.unity`.
  - The probe below compiled with `run_script --dry_run true` and no diagnostics.
- **Not yet run.** The play-mode sequence itself was not run, because this phase does not allow play mode. **[UNVERIFIED end-to-end]**

```csharp
// e.g. ninjago-unity/AgentScripts/BossCliProbe.cs (outside Assets/ → no import/reload). Compiled clean (dry run).
using UnityEngine;
public static class BossCliProbe
{
    // Mirrors MainCoordinator: sets the mode + player count, then loads GameScene (GameDriver Hide()s the blocker).
    public static object Start(int players, int mode)
    {
        Application.runInBackground = true;                 // NinjagoGame also sets it in the Editor
        var data = Nex.PlayerDataManager.Instance;
        data.DebugSettings.ninjaSimulatedBody = true;
        data.SaveDebugSettings();                            // persists! turn it off after the session
        data.ScopedPlayerPreferenceUpdate(p => p.numPlayers = players);
        Nex.GameConfigsManager.Instance.SelectedMode = (Nex.GameModeType)mode;   // ChestChase = 3, BossBattle = 15 (new)
        UnityEngine.SceneManagement.SceneManager.LoadScene(Nex.GameConfigsManager.Instance.GameScene);
        return "loading";
    }
    public static object Steer(int player, float leanX) { Nex.Ninjago.SimulatedBody.Set(player, leanX, 0f, false); return leanX; }
    public static object State()   // swap RunnerView for BossBattleView once it exists
    {
        var view = Object.FindAnyObjectByType<Nex.Ninjago.RunnerView>();
        if (view == null) return new { inRunner = false, setup = Object.FindAnyObjectByType<Nex.Ninjago.NinjagoSetupView>() != null };
        var p = view.World.Vehicle.Position;
        return new { inRunner = true, vehicle = view.World.CurrentVehicle.ToString(), x = p.x, y = p.y, t = view.World.TimeLeft,
            dodges = view.World.GetDodges(view.World.CurrentVehicle), hits = view.World.GetHits(view.World.CurrentVehicle) };
    }
}
```

```bash
UPP=/Users/oscar/gitfolder/ninjago/ninjago-dragon-rising/ninjago-unity; P=AgentScripts/BossCliProbe.cs
unity command set_autotick --enable true --project-path $UPP
unity command editor_play --project-path $UPP --json        # then poll editor_status until playMode == "playing"
unity command run_script --file $P --entry BossCliProbe.Start --args '[1, 15]' --project-path $UPP --json
# setup completes by itself (~1.5 s game time; unfocused Editor runs ~10 fps, so allow several seconds)
unity command run_script --file $P --entry BossCliProbe.Steer --args '[0, -8.0]' --project-path $UPP --json   # full left (wall)
unity command run_script --file $P --entry BossCliProbe.State --project-path $UPP --json
unity command eval 'Nex.Ninjago.SimulatedBody.Set(0, 0f, 0f, false); return 0;' --project-path $UPP --json  # one-liner form
unity command editor_stop --project-path $UPP --json
```

Lean needed for a target position, from the car settings (deadzone 1.5, full 7, halfWidth 2.2):
- `leanX = sign(x) * (1.5 + |x|/2.2 * 5.5)`.
- Lane ±2 → ±6.5 in. Centre → 0. Wall (±2.2) → ≥ 7. Keyboard A/D give 8.
- For the Skycraft y: `leanY = sign * (1.5 + |y - 3.2|/1.6 * 2.5)`.
- In 2P, set **both** players. `SharedSteer` averages them, and the simulated body is always "tracked".

Boss Battle should add one more line to the probe: a debug hook to jump to a phase or set the boss HP. Expose public static methods or `DebugSettings` method rows, following the `DebugHooks` pattern used by BR (`DebugSettings.cs:70-93`). Read state with `wait_for` on public properties. Example: `{"findType":"Nex.Ninjago.BossBattleView","member":"Phase","op":"equals","value":"Phase2"}`.

## 9. Exact wiring for Boss Battle

### 9.1 Game (copy `Ninjago/Flow/ChestChaseGame.cs`)

```csharp
namespace Nex.Ninjago
{
    /// <summary>Boss Battle: the car chases the boss on the road, then the skycraft fights it in the dark arena.</summary>
    public class BossBattleGame : NinjagoGame
    {
        [Header("Boss Battle View")]
        [SerializeField] BossBattleView viewPrefab = null!;
        [Header("Boss Battle Config")]
        [SerializeField] BossBattleConfig config = null!;          // new SO (EnumDictionary-keyed tuning)
        [Header("Chase Config")]
        [SerializeField] ChaseConfig chaseConfig = null!;          // reuse the steering rule + vehicle settings

        protected override GameModeType Mode => GameModeType.BossBattle;   // append BossBattle = 15 to GameModeType
        // SetupCursors / SetupRobotArms stay null, SetupStoresRestJoints stays false: lean-only setup.

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, chaseConfig, Detection, NumOfPlayers);  // NumOfPlayers = requested count
            await Views.ReplaceView(view);                                                 // replaces NinjagoSetupView
            if (!await view.RunAsync(cancellationToken)) return null;                      // null = backed out → "abandon"
            return BuildOutcome(view, activePlayers);
        }
    }
}
```

### 9.2 View (copy `RunnerView.Initialize` lines 76-95 and `OnDestroy` 97-103, then add the preview throttle)

```csharp
public class BossBattleView : SimpleCanvasView
{
    [Header("Camera Preview")]
    [SerializeField] AreaPreviewFrame pipFrame = null!;
    [Header("Camera Preview Indicators")]
    [SerializeField] PlayerIndicatorsManager pipIndicators = null!;
    // ... feed RawImage, world prefab, HUD (boss HP bar, hints) ...

    DetectionManager detection = null!;

    public override ViewIdentifier Identifier => ViewIdentifier.BossBattle;   // append to View.ViewIdentifier (after ForgeEditor)
    public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
    public override string AnalyticsScreenName => "boss-battle";

    public void Initialize(IReadOnlyList<PlayerBody> players, BossBattleConfig aConfig, ChaseConfig chaseConfig,
        DetectionManager aDetection, int requestedPlayers)
    {
        detection = aDetection;
        // world = Instantiate(worldPrefab, worldOrigin, Quaternion.identity); world.Initialize(players, chaseConfig, texture);
        //   → RunnerWorld/VehicleRig.Initialize(players) enables + colors one PlayerSteerMarker per ACTIVE player on car and skycraft

        var indicatorPlayers = new List<int>(players.Count);
        foreach (var player in players) indicatorPlayers.Add(player.PlayerIndex);
        pipFrame.Initialize(detection.PlayAreaController);
        pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        detection.SetPreviewTextureInterval(aConfig.PipRefreshSeconds);   // e.g. 1/15 s: fewer ~9 ms camera uploads (perf)
    }

    void OnDestroy()
    {
        // The next calibration (Retry) needs the full preview rate.
        if (detection != null) detection.SetPreviewTextureInterval(0f);
        // destroy world + release RenderTexture, as RunnerView.OnDestroy
    }
}
```

- Per frame, steering is already in `RunnerWorld.Step`. If Boss Battle writes its own world loop, keep the same three calls: `TryGetLeanInches`, `ChestSteer.Steer`, `SharedSteer.Blend`. Then call `vehicle.SetSteer(i, steer ?? zero, steer.HasValue)` for every active player, every frame. That call drives the over-head markers.
- **Lost tracking (optional).** Subscribe to `Detection.SetupStateManager.PlayerTrackerUpdated` and watch for `PlayingButNoPose`. Ninjago normally sits in `WaitingForRaisingHand`, so a tracker can also drop back to `WaitingForGoodPlayerPosition`. Alternatively, poll `PlayerBody.TryGetChest`, as `ChapterGame.CountLostPlayersAsync` does. Skip this when `SimulatedBody.IsEnabled`.

### 9.3 Prefab building (Editor scripts; follow `Ninjago/Editor/CrouchRunBuilder.cs`)

- **Game prefab.** `BossBattleBuilder.BuildGame()`:
  - `new GameObject("BossBattleGame").AddComponent<BossBattleGame>()`, then `NinjagoGamesBuilder.WireShared(game, root.transform)` (`NinjagoGamesBuilder.cs:78-94`). That adds the `Sun` light (hard shadows), `SimulatedBodyKeyboard`, the `Engines` root, and `NinjaDetectionEngine`. It also wires `NinjagoSetupView.prefab`, `NinjagoResultView.prefab` and `NinjagoPlayersConfig.asset`.
  - Set the view, the configs and the localized result strings, then `SavePrefab` under `Prefabs/Ninjago/Games/`.
- **Menu entry.** `NinjagoGamesBuilder.Mode(settings, modes, GameModeType.BossBattle, GamePath, "ninjago.mode.boss_battle")` makes the prefab Addressable and sets `maxPlayers = 2`. Follow it with `WriteMenuOrder()`.
- **View PiP.** `var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(384f, 216f), out var pipFrame); Set(view, "pipFrame", pipFrame); Set(view, "pipIndicators", pip);`
- **Vehicle markers.** Reuse `RunnerWorld.prefab`, which already has both rigs with markers, or call `NinjagoWorldBuilder.BuildSteerMarkers` after making it `internal`.

## 10. Gotchas

1. **The Simulated Body setting persists.** It is saved with ES3 and stays on for later Editor sessions. Real-camera tests then silently use keyboard lean, and setup is skipped. Turn it off after a CLI session.
2. **`TryGetLeanInches` is always true when simulated.** Lost-tracking UI and the "untracked" marker alpha cannot be tested with the simulated body. Lean `0, 0` means centred, not lost.
3. **The PiP stays empty without a camera.** `AreaPreviewFrame` alpha stays 0 and the indicators stay hidden. Only the dark panel shows. Screenshots of simulated runs look like this by design.
4. **Never scale the PiP from 0, or resize it before its first texture.** The cached world rect and aspect would give a zero-width `uvRect`. Fade the `CanvasGroup` or move it instead. The same applies when hiding it for the cutscene.
5. **Overlay canvases draw over pushed views.** Anything you put on a Screen Space Overlay canvas, such as `PreviewsManager`, must be hidden in `EnterBackground` and shown in `EnterForeground`. The Debug Settings view needs this. The PiP inside the view canvas does not.
6. **Markers have no billboard.** If the Phase 2 camera orbits, or the cutscene camera looks from the side, the `PlayerSteerMarker` canvases will be seen edge-on. Hide them during cutscenes, or add a look-at for a free camera.
7. **World-space text goes through the HD-2D low-res RT.** The markers render with the world camera, so the "P1"/"P2" TMP text is pixelated and post-processed. Keep the font big (56 now) and readable at the low internal resolution. Otherwise project the tags into the screen-space HUD. **[UNVERIFIED]**: depends on the final RT scale.
8. **Two different detection streams.** The PiP indicators use the **raw** stream (camera rate, about 30 Hz). `PlayerBody` and the engines use the **processed** stream (smoothed, every Update). Don't compare their timing. Use `PlayerBody.BodyFrameTime` to detect new camera frames.
9. **`DetectionManager.Initialize` runs once per scene.** It is not idempotent. Change the player count only through the menu, which reloads GameScene.
10. **`PlayerPositionDefinition` throws outside 1..4 players**, and `SimulatedBody` arrays are sized 2. Keep Boss Battle at `maxPlayers` ≤ 2.
11. **Setup uses unscaled time; trackers and engines use `Time.fixedTime`.** If a cutscene uses `Time.timeScale = 0`, setup still works, but gameplay tracking histories freeze. Engine ppi is a 0.5 s fixed-time average.
12. **The S key does two things.** It is P1's "drop" in the simulated body and the setup debug shortcut. Avoid S in automated scripts. Use the CLI setters.
13. **Editor speed.** An unfocused Editor runs at about 10 fps (BR cookbook §8). Game time still advances. `NinjagoGame.Initialize` sets `runInBackground` in the Editor, but before GameScene loads, set it from the CLI, as `BossCliProbe.Start` does.
14. **`RunnerView` allocates a full-screen RT.** It uses `new RenderTexture(Screen.width, Screen.height, 24)` (`RunnerView.cs:79`). For 60 fps on low-end Android with HD-2D, use the low-res RT from the rendering note instead, and keep the PiP refresh throttled (§2).
15. **Indicator GC.** `PreviewFramePlayerIndicator` clones a `BodyPose` on every raw detection for every indicator (`:116`). That is small GC: about 30 Hz × 2. It is acceptable, but don't add more pose consumers that clone.

## 11. Boss Battle checklist (this subsystem)

- [ ] Append `BossBattle = 15` to `GameModeType` (`Gameplay/Core/GameModeType.cs`). Append `BossBattle` (plus any cutscene or phase views) to `View.ViewIdentifier` after `ForgeEditor` (`Main/Views/Framework/View.cs:67-73`). Both enums are append-only.
- [ ] `BossBattleGame : NinjagoGame`. Reuse `NinjagoSetupView.prefab` without a cursor, arms or rest-joints check. `PlayAsync` replaces setup with `BossBattleView` and passes `activePlayers`, `Detection` and `NumOfPlayers`.
- [ ] Builder: `WireShared` (which gives `SimulatedBodyKeyboard`, engines, setup and result views, and players config), then `Mode(..., maxPlayers 2)`, then `WriteMenuOrder`. Add the localization key `ninjago.mode.boss_battle`.
- [ ] `BossBattleView`:
  - `CameraPreview` bottom-right, 384×216.
  - `pipFrame.Initialize(PlayAreaController)`.
  - `pipIndicators.Initialize(requested, activeIndexList, pipFrame, bpdm)`.
  - `SetPreviewTextureInterval(n)`, reset to 0 in `OnDestroy`.
- [ ] Vehicle markers: `VehicleRig.Initialize(activePlayers)` on car and skycraft, and `SetSteer(i, steer, tracked)` every frame for every active player. Hide the markers in the Phase 1 to Phase 2 cutscene.
- [ ] Steering: reuse `ChestSteer` and `SharedSteer`. "Smash into wall" means blended `steer.x` at about ±1, so vehicle x ≥ `halfWidth` minus a margin. Decide the 2P rule, since averaging means both players must lean.
- [ ] Optional: `pipIndicators.SetActivePlayer(sharedSteer.DominantPlayer)`, and a lost-tracking hint from `PlayerTrackerUpdated` or `TryGetChest`, skipped when simulated.
- [ ] Analytics: setup events are already logged by `NinjagoSetupView` and `PlayerCountView`. Log every Boss Battle UI action through `NinjagoAnalytics.UiAction(AnalyticsScreenName, ...)`, as `RunnerView.OnBackButton` does.
- [ ] Testability:
  - Keep every boss attack resolvable with lean only, so the `SimulatedBody` keys and CLI can play the whole fight.
  - Add public static debug hooks (skip to phase 2, set boss HP, force an attack) and a `State()` probe that reads `BossBattleView` public properties.
  - Run the §8.3 sequence with `mode = 15`.
- [ ] Before shipping, confirm `ninjaSimulatedBody` is gated: `ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`, and `DebugInput` checks `PRODUCTION`. No Boss Battle code should read `Input.*` directly. Use `Nex.Dev.DebugInput`.
