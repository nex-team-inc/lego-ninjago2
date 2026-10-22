# Boss Battle research: mode registration, app flow and views

Scope: how a mini-game is registered and listed in the menu, how GameScene loads and runs it, the shared Ninjago flow
(setup, play, result, Retry), the ViewManager and keyboard navigation API, the secret code, the editor builders, and how
Chapter modes share the menu. It ends with the exact checklist for `GameModeType.BossBattle = 15`.
All paths are relative to `ninjago-unity/` unless noted. Every fact below was read from source or asset YAML on
2026-10-22. Anything not verified that way is marked **(unverified)**.

---

## 0. Key facts

1. **Registration is done by editor builders, never by editing YAML.** A mode is listed in the menu only when all four hold:
   it has a `GameModeType` value, `GameModeConfig.modes[mode].gamePrefab` holds an Addressable prefab GUID, the mode is
   in the hard-coded array inside `NinjagoGamesBuilder.WriteMenuOrder()`, and `WriteMenuOrder()` has run since.
   Each per-mode builder calls `NinjagoGamesBuilder.Mode(...)` and then `WriteMenuOrder()`.
2. **`WriteMenuOrder()` rewrites `modeOrders` from scratch on every builder run** (`Scripts/Ninjago/Editor/NinjagoGamesBuilder.cs:119-141`).
   A mode missing from its array disappears from the menu the next time any builder runs, including `ChapterForgeBuilder`.
3. **EnumDictionary copies its neighbour.** Adding `BossBattle = 15` and then running any builder makes
   `EnumDictionaryEditorUtils` append a key-15 entry with `SerializedProperty.InsertArrayElementAtIndex`. Unity fills the
   new entry by copying the previous element, which is Forge's `displayName`, `gamePrefab` and `maxPlayers`. Until the
   Boss Battle builder overwrites that entry, the menu can show a second "Forge" button that loads ForgeGame.
   `ChapterGamesBuilder.Unregister` (lines 32-42) exists to guard against exactly this.
4. **GameScene runs exactly one `BaseGame`.** `GameDriver` reads `GameConfigsManager.Instance.SelectedMode`, loads that
   prefab through Addressables, instantiates it under itself, calls `Initialize` and then `RunAsync`. When `RunAsync`
   returns, it shows the screen blocker and loads MainScene. Nothing else returns the player to the menu.
5. **The Ninjago base flow is setup view (pushed) → play view (Replace) → result view (Replace).** Retry replaces the
   result view with a new setup view. Menu or Back ends `RunAsync`. `ChapterGame` overrides `RunAsync` completely for its
   start/Continue view, and Boss Battle should do the same.
6. **The secret code is wired today.** `NinjagoGamesBuilder.AddSecretCode()` puts a `SecretCodeSequenceDetector` with
   Up Up Down Down Left Right Left Right as `configs[0]` on `Assets/Prefabs/Coordinators/MainViewManager.prefab`, which
   both MainScene (through MainCoordinator) and GameScene use. `ViewManager.Awake` binds `configs[0]` to
   `OpenDebugSettings` only when `ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR` is defined.
   Boss Battle needs no new detector.
7. **`ReplaceView` and `PopView` always act on the top of the stack.** If Debug Settings is open (pushed on top of
   gameplay) when the game calls `Views.ReplaceView(next)`, the Debug Settings view is replaced instead of the gameplay
   view. A second transition during a running one throws `InvalidOperationException`. See §11.

---

## 1. File map

| Thing | Path |
|---|---|
| `GameModeType` enum | `Assets/Scripts/Gameplay/Core/GameModeType.cs` |
| `BaseGame` (abstract) | `Assets/Scripts/Gameplay/Core/BaseGame.cs` |
| `GameDriver` | `Assets/Scripts/Gameplay/Core/GameDriver.cs` |
| `DetectionManager` | `Assets/Scripts/Gameplay/Core/DetectionManager.cs` |
| `GameModeConfig` (SO) | `Assets/Scripts/Configs/GameModeConfig.cs`; asset `Assets/Configs/GameModeConfig.asset` (guid `646c11192551e47e393f535418882e57`) |
| `GameConfigsManager` (singleton) | `Assets/Scripts/Configs/GameConfigsManager.cs`; prefab `Assets/Prefabs/Singletons/GameConfigsManager.prefab` |
| `MainCoordinator` | `Assets/Scripts/Main/Coordinator/MainCoordinator.cs`; prefab `Assets/Prefabs/Coordinators/MainCoordinator.prefab` (Addressable) |
| Menu | `Assets/Scripts/Main/Views/GameModeSelectionView.cs`, `GameModeSelectionButton.cs`; prefabs `Assets/Prefabs/Views/GameModeSelectionView.prefab`, `Assets/Prefabs/Views/Widgets/GameModeSelectionButton.prefab` |
| View framework | `Assets/Scripts/Main/Views/Framework/{View,SimpleView,SimpleCanvasView,ViewManager,AbstractViewState}.cs` |
| Keyboard glue | `Assets/Scripts/Main/Views/Framework/KeyboardNavigation/{ViewManagerKeyboardNavigationContext,TopLevelControlProxyKeyResponder,AbstractCustomViewKeyResponder}.cs` |
| Top-level controls | `Assets/Scripts/Main/Views/Controls/{TopLevelControlPanel,MainTopLevelControlPanel,DismissableControl}.cs` |
| Debug view | `Assets/Scripts/Main/Views/Debug/DebugSettingsView.cs`; rows in `Assets/Scripts/PlayerData/DebugSettings.cs` |
| Singletons | `Assets/Scripts/Singleton/{Singleton,SingletonSpawner,ScreenBlockerManager}.cs`; prefabs `Assets/Prefabs/Singletons/*.prefab` |
| Ninjago base game | `Assets/Scripts/Ninjago/Flow/NinjagoGame.cs` |
| Chase game (reuse) | `Assets/Scripts/Ninjago/Flow/ChestChaseGame.cs`, `Assets/Scripts/Ninjago/Chase/RunnerView.cs`, `RunnerWorld.cs` |
| Chapter game (Continue pattern) | `Assets/Scripts/Ninjago/Flow/ChapterGame.cs`, `Assets/Scripts/Ninjago/Chapter/Views/ChapterStartView.cs`, `ChapterBlockView.cs` |
| Shared views | `Assets/Scripts/Ninjago/Views/{NinjagoSetupView,NinjagoResultView,NinjagoOutcome,ResultLine,PlayerCountView,LocalizedStrings}.cs` |
| Persistence | `Assets/Scripts/PlayerData/PlayerDataManager.cs`, `Assets/Scripts/Ninjago/Persistence/{NinjagoProgress,ChapterSave}.cs` |
| Analytics | `Assets/Scripts/Ninjago/Analytics/NinjagoAnalytics.cs` (+ per-mode `*Analytics.cs`) |
| Builders | `Assets/Scripts/Ninjago/Editor/{NinjagoGamesBuilder,NinjagoBuildAll,NinjagoEditorUtils,NinjagoViewsBuilder,NinjagoLocalizationSeeder,OnePunchBuilder,BuildBuilder}.cs`, `Editor/Chapter/{ChapterGamesBuilder,ChapterForgeBuilder,ChapterLocalizationSeeder}.cs` |
| Keyboard package (1.2.4) | `Library/PackageCache/team.nex.keyboard-navigation@85c1b57f3096/Runtime/` |
| EnumDictionary | `Library/PackageCache/team.nex.common-utils@d0649bb973c6/{Runtime,Editor}/EnumDictionary/` |
| Scenes (Build Settings order) | `Assets/Scenes/MainScene.unity` (0), `Assets/Scenes/GameScene.unity` (1) |

There is no asmdef under `Assets/Scripts`, so runtime code compiles into Assembly-CSharp and editor code into Assembly-CSharp-Editor.
`View.Manager`, `IsActive`, `ViewCamera` and `ViewOrder` are `internal`, so views must stay in Assembly-CSharp.

---

## 2. Boot, scenes and singletons

### 2.1 SingletonSpawner (`Scripts/Singleton/SingletonSpawner.cs`)
- `enum SingletonType { StartUp, Common }` (line 19). `SingletonConfig { type; AssetReferenceGameObject prefab; }`
- `Awake` (38-48) spawns only the types that are not already in the static `singletonDict`. Each type is spawned in
  order with `prefab.InstantiateAsync(null, true)` and then `DontDestroyOnLoad`. After that, `ActivatePostSpawn` (64-77)
  calls `SetActive(true)` on `activatePostSpawn`, enables `enablePostSpawn`, and **destroys the spawner's GameObject**.
- Deduplication is by type, so the GameScene spawner does nothing extra after MainScene has run. It still activates its
  post-spawn objects.
- `StartUpSingletons.prefab` contains ScreenBlockerManager, AnalyticsManager and ApplicationManager.
- `CommonSingletons.prefab` contains DebugPrinter, PlayerDataManager, VfxManager, VolumeManager, SfxManager,
  **GameConfigsManager**, CherryIntegrationManager and BgmManager. RemoteConfigManager is not in either prefab.
- Boss Battle needs no new singleton. Reach the managers through `X.Instance` only after spawn: anything inside the game
  prefab qualifies, because GameDriver starts inactive and the spawner activates it.

### 2.2 MainScene (`Assets/Scenes/MainScene.unity`)
Roots: `MainInitializerExample` (inactive), `EventSystem`, and `SingletonSpawner` (configs StartUp guid
`67aae64b32e144c8b91c503eb4041dca`, Common guid `af7dd7192fd8a423db7225be78323338`; `activatePostSpawn` =
MainInitializerExample). `MainInitializerExample.Start` (`Scripts/Examples/MainInitializerExample.cs:29-91`)
instantiates the Addressable `MainCoordinator` prefab, `await coordinator.Initialize()`, waits 0.1 s, calls
`ScreenBlockerManager.Instance.Hide()`, and then `coordinator.StartMain()`, which pushes `GameModeSelectionView`.

### 2.3 GameScene (`Assets/Scenes/GameScene.unity`)
Roots: `EventSystem`; `SingletonSpawner` (StartUp + Common, `activatePostSpawn: [GameDriver]`); `Main Camera`;
`MainViewManager` (instance of `Assets/Prefabs/Coordinators/MainViewManager.prefab`, with `baseCameras[0]` overridden
to the scene Main Camera); `UIBackground` (a Canvas); `DetectionManager` (instance of
`Assets/Prefabs/Detection/Core/DetectionManager.prefab`); and `GameDriver` (**inactive**, fields `gameModeConfig`,
`detectionManager`, `viewManager`).
**No PreviewsManager or SetupStateManager is placed in the scene.** `DetectionManager` exposes `SetupStateManager`,
`PlayAreaController`, `BodyPoseDetectionManager` and `CvDetectionManager`. `NinjagoSetupView.Initialize` instantiates
`Assets/Prefabs/Detection/Preview/PreviewsManager.prefab` and destroys it in `OnDestroy`.

### 2.4 GameDriver (`Scripts/Gameplay/Core/GameDriver.cs:44-59`)
```csharp
var mode = GameConfigsManager.Instance.SelectedMode;
gamePrefabHandle = Addressables.LoadAssetAsync<GameObject>(gameModeConfig.GetMode(mode).gamePrefab);
var game = Instantiate((await gamePrefabHandle).GetComponent<BaseGame>(), transform);
detectionManager.Initialize(game.NumOfPlayers);      // NumOfPlayers is read before Initialize
game.Initialize(detectionManager, viewManager);
await ScreenBlockerManager.Instance.Hide();
await game.RunAsync(destroyCancellationToken);
await ScreenBlockerManager.Instance.Show();
SceneManager.LoadScene(GameConfigsManager.Instance.MainScene);
```
- `BaseGame` (`Scripts/Gameplay/Core/BaseGame.cs`): `abstract int NumOfPlayers`,
  `abstract void Initialize(DetectionManager, ViewManager)`, `abstract UniTask RunAsync(CancellationToken)`.
- If you press Play on GameScene directly, `SelectedMode` is `None`, `modes[None].gamePrefab` is empty, and the load
  fails. Start from MainScene, or use a debug hook that sets `SelectedMode` and loads `GameConfigsManager.Instance.GameScene`
  (the `ChapterDebugHooks.PlayMode` pattern, `Scripts/Ninjago/Chapter/Forge/ChapterDebugHooks.cs:19-53`).

---

## 3. Mode registration

### 3.1 `GameModeType` (`Scripts/Gameplay/Core/GameModeType.cs`)
`None=0, PressButtonToWin=1, SlipAndSpin=2, ChestChase=3, StoneKick=4, EarthSeal=5, RobotMecha=6, CrouchRun=7,
ThreeWeapons=8, OnePunch=9, Guard=10, Call=11, Build=12, Chapter=13, Forge=14`. Values are persisted as integers in
`NinjagoProgress.lastMiniGame` (Easy Save) and in `GameModeConfig.asset` keys, so only append: `BossBattle = 15`.

### 3.2 `GameModeConfig` (`Scripts/Configs/GameModeConfig.cs`)
```csharp
[Serializable] public class ModeDefinition {
    public LocalizedString displayName = new();
    public AssetReferenceGameObject gamePrefab = null!;   // Addressable prefab with a BaseGame on its root
    [Range(1, 2)] public int maxPlayers = 1;              // >1: the menu asks how many players first
}
[SerializeField] EnumDictionary<GameModeType, ModeDefinition> modes;   // line 26
[SerializeField] List<GameModeType> modeOrders;                         // line 29, the menu order
public IReadOnlyList<GameModeType> ModeOrders; public ModeDefinition GetMode(GameModeType mode) => modes[mode];
```
The asset is referenced by `MainCoordinator.prefab`, `GameModeSelectionView.prefab` and GameScene's `GameDriver`.
- Current asset: keys 0-14 are present. Every Ninjago mode has `maxPlayers: 2` and a display name in the
  `LocalizationTable` collection (shared data guid `19d51c5b557f64038ab34ae2016b2cf6`).
- Current `modeOrders` = `[14 Forge, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]`. Chapter (13) and PressButtonToWin (1) are not listed.
- Runtime `EnumDictionary.OnAfterDeserialize` (Runtime/EnumDictionary.cs:88-113) fills missing keys *between* pairs
  with `default`, but not keys after the last pair. Until the asset has a key-15 pair, `GetMode(BossBattle)` throws
  `KeyNotFoundException`. That is harmless as long as BossBattle is not in `modeOrders`.

### 3.3 Addressables
`NinjagoGamesBuilder.Mode(...)` (lines 143-153) runs `settings.CreateOrMoveEntry(guid, settings.DefaultGroup)` with
`address = file name without extension`, so every game prefab lands in `AssetGroups/Default Local Group.asset`
(ChestChaseGame, OnePunchGame, ForgeGame, and so on). The game prefab's views, worlds and configs come along as implicit
dependencies, so they need no Addressable entries of their own. `m_BuildAddressablesWithPlayerBuild: 0` means "use the
Editor preference". Confirm that preference is on before an Android build, or run an Addressables content build
**(unverified which value the preference has on this machine)**.

### 3.4 The menu path (MainScene)
- `MainCoordinator.CreateGameModeSelectionView` (lines 57-66) calls
  `view.Initialize(NinjagoProgress.lastMiniGame, ChapterModes.All(ChapterModes, ChapterSave), mode => ForgeConfig.NameOf(mode.name))`.
- `GameModeSelectionView.Initialize` (lines 50-74) creates one `GameModeSelectionButton` per chapter mode **first**,
  with `GameModeType.Chapter` and the chapter mode id. It then creates one button per `ModeOrders` entry, with
  `displayName`. It calls `gridKeyResponder.ReinitializeResponders(...)` and focuses
  `chapterModes.Count + IndexOf(ModeOrders, lastMiniGame)`. The grid has 4 columns, 400x110 cells, and sits inside a
  ScrollRect with a `ScrollRectResponder`.
- When clicked (`HandleModeClicked`, 101-109; guarded by `IsActive` and `modeChosen`; plays `SfxManager.SoundEffect.UiSelect`),
  `MainCoordinator.GameModeSelectionOnModeSelected` (68-80) sets `SelectedMode` and `SelectedChapterModeId` and logs
  `NinjagoAnalytics.UiAction("game-mode-selection", mode.ToString())`, which gives `"BossBattle"`.
  If `maxPlayers > 1`, it pushes `PlayerCountView`, and the choice is saved to `PlayerPreference.numPlayers`.
  It then shows the blocker and loads `GameScene`.
- `NinjagoGame.NumOfPlayers` (NinjagoGame.cs:42) = `Clamp(PlayerPreference.numPlayers, 1, SimulatedBody.MaxPlayers=2)`.
  **If `maxPlayers` is 1, the count view is skipped but the saved value (possibly 2) is still used.** A one-player-only
  mode must override `NumOfPlayers => 1`, as `PressButtonToWinGame` does.

---

## 4. Chapter modes in the menu (avoiding collisions)

- Every chapter entry is `GameModeType.Chapter` (13) plus a string id from `GameConfigsManager.SelectedChapterModeId`.
  `ChapterModes.All` (`Scripts/Ninjago/Chapter/ChapterModes.cs:10-16`) returns the built-in `ChapterModeLibrary` modes
  first, then the player-made modes from `ChapterSave.playerModes`. The library asset
  (`Assets/Configs/Ninjago/Chapter/ChapterModeLibrary.asset`) currently holds 4 modes: NinjaChapterProof, QuickPlay,
  BossRush and Surprise2026. Their titles come from `ForgeConfig.NameOf`, not from `GameModeConfig`.
- `ChapterGamesBuilder.Build` registers key 13 (`chapter.mode.proof`), but 13 is not in `WriteMenuOrder`, so it never
  gets a plain button. Forge (14) is the first non-chapter button.
- To avoid collisions:
  1. **Do not make Boss Battle a chapter mode** (no `ModeDefinition` in the library, no `GameModeType.Chapter`). It is
     its own `GameModeType` with its own game prefab.
  2. The localized name must differ from `chapter.mode.boss_rush` ("Boss Rush"). Use the new key
     `ninjago.mode.boss_battle` ("Boss Battle"). Both buttons will be visible, so keep the labels distinct.
  3. Keep its save separate from `ChapterSave` and `ModeProgress`. Use a new ES3 key (see §9).
  4. The chapter Boss actor and `RunnerBlock`'s Boss runner/flyer (`Scripts/Ninjago/Chapter/Blocks/RunnerBlock.cs:11-34`)
     are shared by the Chapter and Forge modes. Make Boss Battle variants, or wrap these assets, instead of changing them in place.
  5. Debug rows: Chapter uses DebugOrder -1000..-994 and the `chapterJump*` fields. Pick another range (for example -1100..-1090).
  6. `ChapterEditorPlay` (`Editor/Chapter/ChapterEditorPlay.cs:50-74`) only clicks `GameModeType.Chapter` buttons. A
     Boss Battle quick-play would need its own hook. Clicking a `GameModeSelectionButton` with `Mode == BossBattle`
     works the same way.

---

## 5. NinjagoGame base flow (`Scripts/Ninjago/Flow/NinjagoGame.cs`)

Serialized fields, all wired by `NinjagoGamesBuilder.WireShared` (`NinjagoGamesBuilder.cs:78-96`):
`detectionEnginePrefab` (`Prefabs/Ninjago/Detection/NinjaDetectionEngine.prefab`), `enginesRoot`, `setupViewPrefab`
(`Prefabs/Ninjago/Views/NinjagoSetupView.prefab`), `resultViewPrefab` (`.../NinjagoResultView.prefab`) and `playersConfig`.
`WireShared` also adds a hard-shadow directional "Sun" and a `SimulatedBodyKeyboard` on the root.

- `Initialize` (46-59): one `OnePlayerDetectionEngine` and one `PlayerBody` per player (`Bodies`), and
  `Application.runInBackground = true` in the Editor. It exposes `Detection`, `Views`, `Bodies` and `abstract GameModeType Mode`.
- `RunAsync` (65-89):
```csharp
PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(p => p.lastMiniGame = Mode);  // menu focus next time
NinjagoAnalytics.MiniGameStart(Mode, NumOfPlayers);
var firstRound = true;
while (true) {
    var active = await CalibrateAsync(firstRound, ct);       // first: PushView(setup); later: ReplaceView(setup)
    firstRound = false;
    var outcome = active == null ? null : await PlayAsync(active, ct);   // the game: ReplaceView(playView)
    if (outcome == null) { NinjagoAnalytics.MiniGameEnd(Mode, "abandon", ...); break; }
    NinjagoAnalytics.MiniGameEnd(Mode, outcome.AnalyticsResult, active!.Count, outcome.AnalyticsDetails);
    var resultView = Instantiate(resultViewPrefab); resultView.Initialize(outcome);
    await Views.ReplaceView(resultView);
    if (!await resultView.WaitForChoiceAsync(ct)) break;      // true = Retry, false = Menu/Back
}
await BgmManager.Instance.FadeOut();
```
- `CalibrateAsync(bool push, ct)` (96-110) is `protected`, so a custom `RunAsync` can reuse it. `NinjagoSetupView.RunAsync`
  (`Views/NinjagoSetupView.cs:139-176`) calls `detection.ConfigForSetup()`, `previewsManager.Initialize(count, BodyPoseDetectionManager, PlayAreaController, SetupStateManager)`
  and `MoveIn`, calibrates each chest origin, shows Ready, `MoveOut`, and calls `detection.ConfigForGameplay()`. It
  returns the calibrated players (solo fallback) or null on Back. Optional extras are the overrides `SetupCursors`,
  `SetupRobotArms` and `SetupStoresRestJoints`. While another view sits on top, it hides the PreviewsManager overlay
  in `EnterBackground`.
- Outcome type (`Views/NinjagoOutcome.cs`): `new NinjagoOutcome(LocalizedString title, IReadOnlyList<ResultLine.Data> lines, string analyticsResult, GameAnalyticsProperties details)`.
  `ResultLine.Data(LocalizedString template, Color color, params (string key, object value)[] args)` takes smart-string arguments.
- `NinjagoResultView` (`Views/NinjagoResultView.cs`): `Identifier = NinjagoResult`, `Controls = Back`,
  `AnalyticsScreenName = "ninjago-result"`, buttons Retry/Menu in a `GroupKeyResponder` (initial index 0). `Choose`
  logs `NinjagoAnalytics.UiAction(screen, "retry"|"menu"|"back")` and plays UiSelect. Only two outcomes are possible
  (a `bool`). To restyle it, build a new prefab with the same component, as `OnePunchViewsBuilder` and
  `KidsViewsBuilder.BuildResultView` do with
  `NinjagoViewsBuilder.BuildView<NinjagoResultView>(path, (v, ui) => NinjagoViewsBuilder.BuildResult(v, ui, customLine))`.
- Chest Chase play (`Flow/ChestChaseGame.cs:42-50`) instantiates `RunnerView`, calls
  `view.Initialize(activePlayers, chaseConfig, Detection, NumOfPlayers)` and `Views.ReplaceView(view)`, then
  `BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.NinjaChase, ...)` and `view.RunAsync(ct)`, which returns false on Back.
  **`RunnerView` owns its `RunnerWorld`.** It instantiates the world at (2000,0,0), renders it into a screen-sized
  RenderTexture shown by a RawImage `feed`, and destroys both in `OnDestroy` (`Chase/RunnerView.cs:76-106`). Replacing
  the view therefore destroys the world.

### 5.1 Custom-flow pattern with Continue (`Flow/ChapterGame.cs`)
- `RunAsync` (67-73) runs its own flow and ends with `BgmManager.Instance.FadeOut()`. `PlayAsync` throws
  `NotSupportedException` (lines 294-296) because the base loop is never used. **It does not set `lastMiniGame` or call
  `MiniGameStart`/`MiniGameEnd`.** Boss Battle must do both itself.
- `PlayModeAsync` (79-143): read the saved progress, set `canContinue = progress is { finished: false, nextLevel: > 0 }`,
  then `StartViewAsync` (145-156) **pushes** `ChapterStartView` (Continue/Start; `true`/`false`/`null` = back). On a
  fresh start it calls `ScopedChapterSaveUpdate(s => s.ProgressOf(id).Restart())`, then `CalibrateAsync(false, ct)`,
  which **replaces** the start view. It saves after each level (`RecordLevel`), uses `ReplaceView` for the rest view
  between levels and for the ending view at the end.
- The world is owned by the game, not by a view: `stage = Instantiate(stagePrefab, transform)` on first use (line 203).
  The per-block views are empty overlays swapped with `views.ReplaceView(view, false)` (`Chapter/LevelRunner.cs:141-147`).
  `ChapterBlockView` (`Chapter/Views/ChapterBlockView.cs`) explains: "replacing one block's view with the next only
  changes the (empty) overlay; the world and its camera keep running underneath". **Use this pattern for Boss Battle
  phases.**
- `ChapterStartView` (`Chapter/Views/ChapterStartView.cs`): `Initialize(LocalizedString title, LocalizedString progressLine, bool canContinue)`
  hides Continue when it is not available and focuses `canContinue ? 0 : 1`. It has `event Action<bool> Chosen`
  and `event Action QuitRequested`, and logs `UiAction("chapter-start", "continue"|"start"|"back")`.

---

## 6. ViewManager and views

### 6.1 `View` (`Scripts/Main/Views/Framework/View.cs`)
- `abstract ViewIdentifier Identifier`, `virtual string AnalyticsScreenName => ""`,
  `abstract TopLevelControlPanel.ControlConfig Controls`, `virtual void OnBackButton()`,
  `virtual bool OnControlButton(ButtonKind)`, `virtual KeyResponder KeyResponder`,
  `virtual bool RequiresAdditionalBackgroundBlur => false`.
- Lifecycle: `Present`, `Dismiss`, `EnterBackground(childId)`, `EnterForeground(childId)`,
  `ViewDidBecomeTopView(bool afterPush)`, `ViewDidLoseTopView(bool dismissing)`. Durations default to 0.5 s.
  Protected helpers: `PushViewPrefab`, `PushView`, `PopSelf`.
- `enum ViewIdentifier` (lines 23-74, implicit values, "append only"): `Invalid=-1, Empty=0, WelcomeScreen=1,
  DebugSettings=2, Title=3 ... Settings=12, GameModeSelection=13, PressButtonToWin=14, PlayerCount=15, NinjagoSetup=16,
  Fight=17, Runner=18, NinjagoResult=19, StoneKick=20, EarthSeal=21, MechaFight=22, CrouchRun=23, ThreeWeapons=24,
  OnePunch=25, GuardPlay=26, CallPlay=27, BuildPlay=28, ChapterStart=29, ChapterBlock=30, ChapterRest=31,
  ChapterEnding=32, ForgeHome=33, ForgeEditor=34`. New Boss Battle identifiers go after `ForgeEditor` under a
  `// Boss Battle (append only).` comment.

### 6.2 `SimpleView` / `SimpleCanvasView`
- `SimpleView`: `entryAnimator` / `toBackgroundAnimator` (MMFeedbacks) drive Present/Dismiss and Background/Foreground.
  The `[SerializeField] KeyResponder keyResponder` becomes `View.KeyResponder`. The default `OnBackButton` is
  `if (!IsActive) return; PopSelf()`, so **every gameplay view must override `OnBackButton`.**
- `SimpleCanvasView` (`[RequireComponent(typeof(Canvas))]`): `ViewCamera` sets `renderMode = ScreenSpaceCamera` and
  `worldCamera`. `ViewOrder` sets `planeDistance = 300 - 10*order` (or `sortingOrder` when there is no camera).
  Optional `disableCanvasOnBackground`.

### 6.3 `ViewManager` (`Scripts/Main/Views/Framework/ViewManager.cs`)
- The stack is never empty. `Awake` (51-73) pushes an `EmptyView` root, adds a `KeyboardNavigationController`, and binds
  the secret code. Public members: `TopView`, `TopViewIdentifier`, `RootViewCamera` (= `cameraChains[0].GetCamera()`),
  `ControlPanel`, `IsInTransition`, `CollectStackViews<T>(List<T>)`, events `SecretCodeEntered(View)`, `Paused`,
  `PauseViewHomeClicked`, `PauseViewResumeClicked`, `CreateTransaction()`, and the static
  `SuspendKeyboardNavigation()` / `ResumeKeyboardNavigation()`.
- `UniTask<T> PushViewPrefab<T>(T prefab, bool animate = true)`: instantiate the prefab and push it.
- `UniTask PushView(View view, bool animate = true, bool skipScreenAnalytics = false)` (140-192): the old top view gets
  `DeactivateView` and then `EnterBackground`, while the new view gets `PresentWithDuration` and the control panel is
  reconfigured. Then `ActivateView(new, afterPush: true)`.
- `UniTask PopView(bool animate = true, bool keepPoppedViewAlive = false)` (200-246): dismisses the top view, the
  previous view gets `EnterForeground`, then `ActivateView(prev, false)`. **The popped view is destroyed** unless
  `keepPoppedViewAlive` is set.
- `UniTask<View> ReplaceView(View replacement, bool animate = true, bool keepPoppedViewAlive = false)` (248-323): pops the
  top view and puts `replacement` at the same depth. The view underneath is not told anything (same hierarchy level).
  The old top view is destroyed unless kept alive. Activation is `ActivateView(replacement, false)`, so **`afterPush`
  is false after a Replace.**
- `ActivateView` (331-350): `IsActive = true`, activates the view's KeyResponder with `appViewState.enableHighlighting`,
  calls **`AnalyticsManager.Instance.TrackScreen(AnalyticsScreenName)`** when the name is not empty (this fires on every
  re-activation), then `ViewDidBecomeTopView`. Inside a transaction, activation waits until the transaction is disposed.
- Every transition throws `InvalidOperationException("Cannot push / pop view while in transition.")` if another
  transition is running.
- `HandleTopLevelButton` (410-440) is ignored during a transition. DebugSettings calls `OpenDebugSettings`. Back plays
  `SfxManager.SoundEffect.GenericExit` and then `TopView.OnBackButton()`. Exit and Help call `TopView.OnControlButton`.
- `OpenDebugSettings` (442-447): `SecretCodeEntered?.Invoke(TopView)`, plays `GenericEnter`, then
  `PushViewPrefab(debugSettingsPrefab).Forget()`. It does not check for a running transition.
- `HandleKey` (504-540) drops keys during a transition, when no responder is active, when
  `CherryIntegrationManager.IsKeyboardControlDisabled()` is true, or while navigation is suspended. Escape calls
  `HandleBack`, Enter calls `HandleEnter`, and the arrows call `HandleNavigation(Up/Down/Left/Right)`, enabling
  highlighting on the way.

### 6.4 Top-level controls (`Main/Views/Controls/TopLevelControlPanel.cs`)
`[Flags] enum ControlConfig { None=0, Back=1, Exit=2, Help=4, DebugSettings=1<<31 }` and
`enum ButtonKind { Back=0, Exit=1, Help=2, DebugSettings=31 }`. `ViewManager.ComputeControlConfig` ORs in
`DebugSettings` for every view except DebugSettings itself in debug builds. `MainTopLevelControlPanel.prefab` has
`supportedControls: -1` (all). Conventions: the menu uses `Exit`, and every Ninjago step uses `Back`.

### 6.5 Conventions in the existing views
- Screen names are kebab-case (`game-mode-selection`, `player-count`, `ninjago-setup`, `runner`, `ninjago-result`,
  `chapter-start`, `chapter-block-<type>`). Button events use
  `NinjagoAnalytics.UiAction(AnalyticsScreenName, "<button>", index)`, which emits `ui_button_click {screen, button, index}`.
- Every click handler is guarded with `if (!IsActive || chosen) return;` and plays `SfxManager.SoundEffect.UiSelect`.
- Gameplay views use `keyResponder = NinjagoEditorUtils.BackProxy(ui)` (`NinjagoEditorUtils.cs:202-207`), a
  `TopLevelControlProxyKeyResponder` for `ControlConfig.Back`, so Escape and remote Back reach `OnBackButton`.
- The PiP camera preview with player indicators comes from `NinjagoViewsBuilder.CameraPreview(ui, anchor, pos, size, out AreaPreviewFrame)`
  (lines 369-391). At runtime it is wired with `pipFrame.Initialize(detection.PlayAreaController)` and
  `pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager)` (RunnerView.cs:93-96).

---

## 7. Keyboard navigation and the secret code

- Package `team.nex.keyboard-navigation` 1.2.4 (`Packages/manifest.json:27`), namespace `Nex.KeyboardNavigation`. The
  project uses the legacy Input Manager (`activeInputHandler: 0`), so `KeyboardNavigationController` takes its
  `#else` branch. It maps Escape to Back; Return, KeypadEnter, JoystickButton0 and Mouse2 to Enter; the mouse wheel to
  the arrows; and the arrow keys with key repeat. `enum Key { Escape, Enter, Up, Down, Left, Right }` (values 0-5).
- `KeyResponder` (abstract): `HandleNavigation(NavigationKey)`, `HandleEnter()`, `HandleBack()`,
  `Activate(context, isFocused)`, `Deactivate`, `EnableHighlighting`, plus highlight "tweets" (graphic, scale and so on).
- `ButtonKeyResponder` (`[SerializeField] Button button`): Enter calls `button.onClick.Invoke()`. It cannot activate
  while the button is inactive, so a hidden button is skipped automatically, as with Chapter's Continue.
- `GroupKeyResponder` has an `axis` (0 = horizontal). `GridKeyResponder` has `NumColumns` and `edgeBehavior`.
  Shared `AbstractGroupKeyResponder` API: `SetInitialActiveIndex(int)`, `ReinitializeResponders(IEnumerable<KeyResponder>)`,
  `ReinitializeWithChildResponders()`, `SelectedIndex`, and `SelectionFallback {First, Previous, Next, Last}`.
- `ViewManagerKeyboardNavigationContext` lets `TopLevelControlProxyKeyResponder` delegate to the control panel's
  responder for its `control`.
- **`SecretCodeSequenceDetector`** (`.../Runtime/SecretCodeSequenceDetector.cs`): `[SerializeField] SequenceConfig[] configs`
  (each has `Key[] sequence` and `UnityEvent onTriggered`, matched with KMP prefix matching so overlapping input
  works), an optional `keyboardController`, and `AddListener(int configIndex, UnityAction)`. In `Start` it subscribes to
  `GetComponent<KeyboardNavigationController>().OnKey`, which is the controller `ViewManager.Awake` added to the same
  GameObject. An empty `configs` array would throw in `ViewManager.Awake`.
- Where it is wired:
  - Ninjago: `NinjagoGamesBuilder.AddSecretCode()` (lines 172-200) writes `configs.arraySize = 1` with the sequence
    `Up,Up,Down,Down,Left,Right,Left,Right` on `Assets/Prefabs/Coordinators/MainViewManager.prefab`. Verified in the
    prefab YAML (line 248): `sequence: 0200000002000000030000000300000004000000050000000400000005000000`.
  - Billiard Rogue: `Scripts/BilliardRogue/Editor/FlowPrefabsBuilder.cs:20-43` (`BuildViewManagerVariant`) does the same
    on a variant prefab and subscribes `viewManager.SecretCodeEntered` to log analytics
    (`BilliardRogueCoordinator.cs:123,150-153` calls `RunAnalytics.SecretCode(view.AnalyticsScreenName)`).
- What it triggers: `ViewManager.OpenDebugSettings`, which raises `SecretCodeEntered(TopView)`, plays `GenericEnter` and
  pushes `DebugSettingsView` (`Assets/Prefabs/Views/DebugSettingsView.prefab`; rows from `PlayerDataManager.DebugSettings`;
  saved on close). The code works on any view in either scene. The arrows also move focus in the current view, and in
  gameplay views (BackProxy only) that does nothing. Nothing in Ninjago listens to `SecretCodeEntered` yet. For the
  "analytics for every UI action" requirement, Boss Battle should subscribe and log
  `NinjagoAnalytics.UiAction(view.AnalyticsScreenName, "secret_code")`.

---

## 8. Builders (how a mode's prefab is built and registered)

Convention (`NinjagoEditorUtils.cs:16`): "assets are authored through Unity APIs, never YAML". Builders are
idempotent and keep GUIDs. Helpers include `Load<T>`, `LoadOrCreate<T>`, `LoadComponent<T>(prefabPath)`,
`SavePrefab(root, path)` (which destroys `root`), `Set(obj, field, value|Action<SerializedProperty>)`, `SetArray`,
`SetLocalized(obj, field, key)` and `WriteLocalized(prop, key)` (which throws `Missing localization key` if the seeder
has not run). UI helpers include `Label`, `NewRect`, `Place`, `Stretch`, `NewImage`, `BackProxy` and `Feed`. Constants:
`PrefabsRoot = "Assets/Prefabs/Ninjago"`, `ConfigsRoot = "Assets/Configs/Ninjago"`, and
`TableName = "LocalizationTable"`.

The per-mode builder pattern, from `OnePunchBuilder.cs` and `BuildBuilder.cs`:
```csharp
public static class OnePunchBuilder {
    public const string ConfigPath = ConfigsRoot + "/OnePunchConfig.asset";
    const string GamePath = NinjagoGamesBuilder.GamesRoot + "/OnePunchGame.prefab";   // Assets/Prefabs/Ninjago/Games/
    [MenuItem("Nex/Ninjago/Build One Punch")] static void RunFromMenu() => Debug.Log(Run());
    public static string Run() { var loc = NinjagoLocalizationSeeder.Run(); Build(); AssetDatabase.SaveAssets(); return $"... ({loc})"; }
    public static void Build() { BuildConfig(); BuildAudioRegistry(); BuildVfx(); OnePunchWorldBuilder.Build();
                                 OnePunchViewsBuilder.Build(); WireSetupPrompt(); BuildGame(); RegisterMode(); }
    static void BuildGame() {
        var root = new GameObject("OnePunchGame");
        var game = root.AddComponent<OnePunchGame>();
        NinjagoGamesBuilder.WireShared(game, root.transform);         // engine, setup/result views, players config, sun
        Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(OnePunchViewsBuilder.ResultViewPath)); // override
        Set(game, "config", Load<OnePunchConfig>(ConfigPath));
        SetLocalized(game, "clearedTitle", "ninjago.result.punch_cleared");
        SavePrefab(root, GamePath);
    }
    static void RegisterMode() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
        var serialized = new SerializedObject(config);
        NinjagoGamesBuilder.Mode(settings, serialized.FindProperty("modes"), GameModeType.OnePunch, GamePath, "ninjago.mode.one_punch");
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config);
        NinjagoGamesBuilder.WriteMenuOrder();
    }
}
```
- `NinjagoGamesBuilder.Mode(settings, modes, mode, prefabPath, nameKey)` (143-153) creates the Addressable entry and
  writes `displayName` (through `WriteLocalized`), the `gamePrefab.m_AssetGUID`, and **`maxPlayers = 2`** (hard-coded).
- `WriteMenuOrder()` (119-141) keeps a mode only if its `gamePrefab` GUID is non-empty, in the order of the literal
  array `{Forge, SlipAndSpin, ChestChase, StoneKick, EarthSeal, RobotMecha, CrouchRun, ThreeWeapons, OnePunch, Guard, Call, Build}`.
- Config wiring for debug rows: `OnePunchBuilder.BuildConfig` (72-95) also sets `GameConfigsManager.prefab` →
  `onePunchConfig`, because `DebugSettings` reads tuning through `GameConfigsManager.Instance.XConfig`
  (`DebugSettings.cs:449-458`).
- Localization: `NinjagoLocalizationSeeder.rows` holds `(key, English, smart)` tuples, and
  `internal static string Upsert(rows)` adds or updates English entries and sets `UseFallback = true` (other locales:
  fr-CA, ja, zh-Hans and zh-Hant fall back to English). Chapter has its own seeder whose `Run() => NinjagoLocalizationSeeder.Upsert(rows)`
  (`Editor/Chapter/ChapterLocalizationSeeder.cs:202`). Copy that pattern into a separate `BossBattleLocalizationSeeder`
  so the shared row list does not need editing.
- Entry points: `NinjagoBuildAll.Run()` (`Editor/NinjagoBuildAll.cs:22-42`) runs every mini-game builder in order,
  ending with `BuildBuilder.Build()`. `ChapterForgeBuilder.Run()` is separate and calls `WriteMenuOrder` through
  `ChapterGamesBuilder.Register`. CLI form (from the builders' doc comments):
  `unity command eval 'return Nex.Ninjago.Editor.OnePunchBuilder.Run();' 300000 --timeout 320 --project-path <...>/ninjago-unity --json`.
- View prefabs: `NinjagoViewsBuilder.WithTemplate(() => BuildView<T>(path, (view, ui) => ...))` (lines 55-97) copies
  `Assets/Prefabs/Gameplay/Games/PressButtonToWin/PressButtonToWinView.prefab` the first time. It then strips the old
  View, the `UI` children and any proxies, adds `T`, and wires `entryAnimator` / `toBackgroundAnimator` from
  `Animators/EntryAnimator` and `Animators/BackgroundAnimator`. Helpers: `Title`, `CloneButton`, `Group`, `Banner`, `CameraPreview`.

---

## 9. Persistence, Continue and debug hooks (flow side)

- `PlayerDataManager` (`Scripts/PlayerData/PlayerDataManager.cs`) follows this pattern for each save: a lazy property,
  a `ScopedXUpdate(Action<X>)` that saves through `ES3.Save(key, X)`, `ResetX()`, and a `LoadX()` with
  `ES3.Load(key, new X())` inside try/catch, all guarded by `#if !DISABLE_PERSISTENCE`. Existing keys are
  `playerPreferenceData`, `debugSettingsData`, `billiardRogueRun`, `billiardRogueMeta`, `ninjagoProgress` (263-305) and
  `chapterSave` (307-347).
- `NinjagoProgress` (`Ninjago/Persistence/NinjagoProgress.cs`) is `[Preserve]`, public fields only, "append new fields,
  never rename". `lastMiniGame` drives which menu entry is focused.
- `ChapterSave.ModeProgress` gives the Continue semantics: `nextLevel`, `finished`, `lastLevel/lastStars/lastHeartsLeft`
  for the rest view shown on Continue, `RecordLevel(level, stars, hearts, count)` and `Restart()`.
- Debug: rows in `DebugSettings` use `[DebugOrder(n), Description("Area: Label"), NumericSteps(...)]`. Proxied
  properties carry `ES3NonSerializable`. Action methods are wrapped in `#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`,
  as `PlayChapterMode` and `ResetNinjagoProgress` (`SaveBeforeInvoking`) are, at lines 389-420.
  Android script defines do not include `ENABLE_DEBUG_SETTINGS`, so the panel exists only in development builds and the Editor.

---

## 10. Proposed Boss Battle flow (every step is a view)

```
Menu (GameModeSelection) --click "Boss Battle"--> [PlayerCount if maxPlayers 2] --> GameScene / GameDriver
BossBattleGame.RunAsync:
  Push   BossBattleStartView     (Continue at phase 2 | New battle | Back = quit)   "boss-battle-start"
  Replace NinjagoSetupView        (calibrate; PreviewsManager + SetupStateManager)    "ninjago-setup"
  Replace BossBattleRoadView      (phase 1: car, boss ahead, rockets/HP bar, PiP)     "boss-battle-road"
  Replace BossBattleCutsceneView  (car -> plane; save checkpoint here)                "boss-battle-cutscene"
  Replace BossBattleSkyView       (phase 2: plane, devil wind, lasers, clap)          "boss-battle-sky"
  Replace BossBattleFinaleView    (explode animation)                                  "boss-battle-finale"
  Replace BossBattleResultView    (Victory | Defeat lines; Continue/Retry/Menu)       "boss-battle-result"
  Retry -> Replace NinjagoSetupView ...;  Menu/Back -> return from RunAsync -> MainScene
```
- **The world is owned by the game.** Instantiate a `BossBattleWorld` (built on `RunnerWorld` / `RunnerView` pieces) as
  a child of the game, as `ChapterGame.CreateRunner` does. Phase views are thin overlays (HUD, PiP, Back) that reference
  it and are swapped with `Views.ReplaceView(view, false)`. Do not let a view's `OnDestroy` destroy the world, as
  RunnerView's does.
- **Continue semantics.** Create a new `[Preserve] public class BossBattleSave` (`Ninjago/Persistence/BossBattleSave.cs`)
  with public fields only. Suggested fields: `int checkpointPhase` (0 = none, 2 = phase 2 reached),
  `float checkpointBossHealth`, `int runs`, `int wins`, `float bestWinSeconds = -1`, `bool finished`. Add a
  `"bossBattleSave"` region in PlayerDataManager, copied from the ChapterSave region. Save the checkpoint when the
  cutscene starts and clear it on a win; on abandon, keep it. `canContinue = checkpointPhase >= 2 && !finished`. The
  start view offers Continue only then and focuses it. Continue skips phase 1 and the cutscene, and starts phase 2 at
  the saved HP. Because the save is on disk, this survives an app restart, and the menu focus survives through `lastMiniGame`.
- **Result view.** `NinjagoResultView` only returns a bool, and every Ninjago mini-game uses its class (the default
  prefab comes from `WireShared`; some modes build a restyled prefab). Make a `BossBattleResultView : SimpleCanvasView`
  with `enum BossBattleChoice { Continue, Retry, Menu }`. Show Continue only after a defeat in phase 2 with a checkpoint.
  Retry restarts from phase 1 through setup. Menu and Back end the run. Use a `GroupKeyResponder` and `UiAction` for
  each button. If there is no defeat state, reuse `NinjagoResultView` on a new prefab and keep Retry/Menu.
- Analytics: set `lastMiniGame`, then `MiniGameStart(Mode, NumOfPlayers)` at start and
  `MiniGameEnd(Mode, "win"|"defeat"|"abandon", players, details)` at the end of every run. Add
  `GameModeType.BossBattle => "boss_battle"` to `NinjagoAnalytics.ContentName` (`NinjagoAnalytics.cs:129-143`). Put
  per-attack events in a new `BossBattleAnalytics.cs`, following the pattern of `OnePunchAnalytics.cs`.

Skeleton:
```csharp
public class BossBattleGame : NinjagoGame
{
    protected override GameModeType Mode => GameModeType.BossBattle;
    public override async UniTask RunAsync(CancellationToken ct)
    {
        PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(p => p.lastMiniGame = Mode);
        NinjagoAnalytics.MiniGameStart(Mode, NumOfPlayers);
        Views.SecretCodeEntered += HandleSecretCode;            // remove in OnDestroy
        var continuing = await StartViewAsync(ct);              // PushView; null = back
        var players = continuing == null ? null : await CalibrateAsync(false, ct);   // Replace start view
        while (players != null) {
            var outcome = await PlayBattleAsync(players, continuing == true, ct);    // phase views via ReplaceView(v, false)
            if (outcome == null) break;                          // back during play -> "abandon"
            var choice = await ResultAsync(outcome, ct);         // ReplaceView(result)
            if (choice == BossBattleChoice.Menu) break;
            continuing = choice == BossBattleChoice.Continue;
            players = await CalibrateAsync(false, ct);           // Retry/Continue go back through setup
        }
        await BgmManager.Instance.FadeOut();
    }
    protected override UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> p, CancellationToken ct) =>
        throw new NotSupportedException();                        // as ChapterGame
}
```

---

## 11. Gotchas

1. **The menu order is a literal array.** Add `GameModeType.BossBattle` to the array in `WriteMenuOrder()`. Without it,
   any builder run removes Boss Battle from the menu.
2. **Copied neighbour entry.** After adding enum value 15, the first builder run gives key 15 a copy of Forge's entry.
   Run the Boss Battle builder before anything else lists it. Also add an `Unregister(GameModeType.BossBattle)` guard
   when `BossBattleGame.prefab` is missing, as `ChapterGamesBuilder.Build` does for Forge (line 27). Never hand-edit
   `GameModeConfig.asset`.
3. **`Mode()` forces `maxPlayers = 2`.** For one player only, overwrite it after `Mode()` and override `NumOfPlayers => 1`.
4. **A custom `RunAsync` skips the base bookkeeping**: `lastMiniGame`, `MiniGameStart`/`MiniGameEnd` and `BgmManager.FadeOut`.
   Do them yourself.
5. **`ReplaceView` replaces the top of the stack.** If Debug Settings (secret code or debug button) is on top when a phase
   ends, the debug view is replaced and the old phase view stays in the stack under the new one. Before each phase
   transition, wait for `!Views.IsInTransition && Views.TopView == currentPhaseView`. Alternatively, pause the boss world
   in the phase view's `EnterBackground` and resume it in `EnterForeground`. This is a latent issue in the existing modes too.
6. **Concurrent transitions throw.** `OpenDebugSettings` pushes without checking `IsInTransition`. Game-driven transitions
   should `await UniTask.WaitUntil(() => !Views.IsInTransition, cancellationToken: ct)` first. An exception from
   Present or Dismiss leaves `isInTransition` set to true for good.
7. **`afterPush` is false after `ReplaceView`.** `ViewDidBecomeTopView` also runs again after Debug Settings closes and
   tracks the screen again. Keep it idempotent, and start gameplay from the game's flow, not from `ViewDidBecomeTopView`.
8. **The default `OnBackButton` pops the view.** Override it in every phase view to signal quit (log `UiAction(screen, "back")`).
   Never pop the root.
9. **The setup PreviewsManager is a screen-space overlay.** It is hidden only while the setup view is backgrounded.
   Any other screen-space overlays Boss Battle adds must do the same.
10. **ViewManager plays no SFX on push, pop or replace**, only on Back and the debug button. Play UI SFX in handlers.
11. **`GetMode(BossBattle)` throws at runtime** until the asset has a key-15 entry. Only call it for modes in `ModeOrders`.
12. **Do not edit shared assets in place** (`NinjagoSetupView.prefab`, `NinjagoResultView.prefab`, `RunnerWorld.prefab`,
    chapter `Boss.prefab`). Builders regenerate them, and other modes use them. Copy or extend instead.
13. Playing GameScene directly needs `SelectedMode` to be set. Use a debug hook or start from MainScene.
14. A new Addressable game prefab needs an Addressables content build before a device build (see §3.3).

---

## 12. Checklist: add `GameModeType.BossBattle` end to end

Runtime code (Assembly-CSharp, namespace `Nex.Ninjago` unless noted):
- [ ] `Scripts/Gameplay/Core/GameModeType.cs`: append `BossBattle = 15,` after `Forge = 14` (namespace `Nex`).
- [ ] `Scripts/Main/Views/Framework/View.cs`: append under `// Boss Battle (append only).`:
      `BossBattleStart, BossBattleRoad, BossBattleCutscene, BossBattleSky, BossBattleFinale, BossBattleResult` (values 35-40).
- [ ] `Scripts/Ninjago/Flow/BossBattleGame.cs : NinjagoGame`: `Mode => GameModeType.BossBattle`, custom `RunAsync` (§10),
      `PlayAsync` throws, the world owned by the game, and serialized fields for the view prefabs, `BossBattleConfig`
      and the result `LocalizedString`s.
- [ ] Views (`SimpleCanvasView`, `Controls = Back`, kebab-case screen names, `OnBackButton` overridden, BackProxy or
      Group key responder, `UiAction` on every button): `BossBattleStartView`, `BossBattleRoadView` (HUD + boss HP bar +
      PiP via `CameraPreview`), `BossBattleCutsceneView`, `BossBattleSkyView`, `BossBattleFinaleView`, `BossBattleResultView`.
- [ ] Config: `Scripts/Ninjago/Configs/BossBattleConfig.cs` (ScriptableObject, `EnumDictionary<...>` for attack/phase
      tuning), asset `Assets/Configs/Ninjago/BossBattleConfig.asset`; add field + property `BossBattleConfig` to
      `GameConfigsManager` (for debug rows).
- [ ] Persistence: `Scripts/Ninjago/Persistence/BossBattleSave.cs` (`[Preserve]`, public fields, append-only) and a
      PlayerDataManager region with key `"bossBattleSave"`: `BossBattleSave`, `ScopedBossBattleSaveUpdate`,
      `ResetBossBattleSave`, `LoadBossBattleSave`.
- [ ] Analytics: `ContentName` adds `BossBattle => "boss_battle"`; new `Scripts/Ninjago/Analytics/BossBattleAnalytics.cs`
      (phase_start/phase_end, pickup, rocket_hit, rock_drop, minion_spawn/clear, wall_smash, clap, boss_wake,
      checkpoint_saved, continue_used); subscribe to `Views.SecretCodeEntered` and log `UiAction(screen, "secret_code")`.
- [ ] Debug rows in `DebugSettings.cs` (DebugOrder -1100..-1090, guarded by
      `#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`): start phase, boss HP scale,
      "Boss Battle: Play Now" (a hook that sets `SelectedMode = BossBattle` and loads `GameScene`), and
      "Save: Reset Boss Battle" (`SaveBeforeInvoking`).

Editor (Assembly-CSharp-Editor, namespace `Nex.Ninjago.Editor`):
- [ ] `Editor/BossBattleLocalizationSeeder.cs`: `Run() => NinjagoLocalizationSeeder.Upsert(rows)`, with
      `ninjago.mode.boss_battle` = "Boss Battle", the start/continue/result/HUD strings, and smart strings flagged `true`.
- [ ] `Editor/BossBattleBuilder.cs`: `[MenuItem("Nex/Ninjago/Build Boss Battle")]`, `Run()` (seeder first, then
      `Build()`, then `SaveAssets`). `Build()` runs config, then world/actors, then views (`NinjagoViewsBuilder.WithTemplate(() => BuildView<T>(...))`
      into `Assets/Prefabs/Ninjago/Views/BossBattle*View.prefab`), then `BuildGame()`
      (`new GameObject("BossBattleGame")`, `AddComponent<BossBattleGame>()`, `NinjagoGamesBuilder.WireShared(game, root.transform)`,
      `Set(...)`, `SavePrefab(root, NinjagoGamesBuilder.GamesRoot + "/BossBattleGame.prefab")`), then `RegisterMode()`
      (`NinjagoGamesBuilder.Mode(settings, modes, GameModeType.BossBattle, GamePath, "ninjago.mode.boss_battle")`,
      optionally set `maxPlayers`, then `WriteMenuOrder()`).
- [ ] `NinjagoGamesBuilder.WriteMenuOrder()`: add `GameModeType.BossBattle` to the literal array. Proposed position:
      right after `Forge`, so it is the first mini-game after the chapter entries. Update the comment at lines 115-118.
- [ ] `NinjagoBuildAll.Run()`: add `BossBattleBuilder.Build();` after `BuildBuilder.Build();`.
- [ ] Missing-prefab guard: if `BossBattleGame.prefab` does not exist, clear key 15's `gamePrefab` (copy `ChapterGamesBuilder.Unregister`).
- [ ] Run once via CLI: `unity command eval 'return Nex.Ninjago.Editor.BossBattleBuilder.Run();' 300000 --timeout 320 --project-path /Users/oscar/gitfolder/ninjago/ninjago-dragon-rising/ninjago-unity --json`.

Verify (in the implementation phase, not now):
- [ ] `GameModeConfig.asset` key 15 has its own `gamePrefab` GUID (not Forge's `d074ecee8b3e249be833e5a9a2a82d05`),
      the display name key is `ninjago.mode.boss_battle`, and `modeOrders` contains `0f000000`.
- [ ] `Default Local Group.asset` has `m_Address: BossBattleGame`.
- [ ] Menu shows "Boss Battle" distinct from the chapter "Boss Rush" entry. After one play, the menu focuses it again.
- [ ] Back on every step returns cleanly. Retry goes through setup. Continue after an app restart resumes at phase 2.
- [ ] The secret code opens Debug Settings during each Boss Battle view, and closing it resumes without corrupting the stack.
- [ ] `ui_button_click` events appear for every button. `minigame_end` has `minigame: boss_battle` and a result.

---

## 13. Unverified or to confirm in the Editor
- Exactly what `InsertArrayElementAtIndex` copies into the appended EnumDictionary entry (inferred from Unity behaviour
  and the comment in `ChapterGamesBuilder.cs:32`). Inspect key 15 after the first builder run.
- The Editor preference behind `m_BuildAddressablesWithPlayerBuild: 0`.
- Whether `DebugOrder` sorts ascending, so that -1100 appears above the Chapter rows (assumed from the existing layout).
