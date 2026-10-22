# Boss Battle research: persistence, debug settings, localization, analytics

Scope: how the Ninjago game saves progress and settings (Easy Save 3 + `PlayerDataManager`), how live-tuning debug rows work,
how strings are localized and seeded without Google Sheets, and how analytics events are named and sent. Each section ends with
the exact pattern to copy for Boss Battle. Every path is relative to `ninjago-unity/` unless it says otherwise. Line numbers were
read from the tree at commit `4d326ccd`.

Related notes from the sibling project (their paths say `Starter`; here the project is `ninjago-unity`):
`Docs/BilliardRogue/research/bootstrap-persistence-debug.md` §2.4–2.5 (ES3 rules) and §3 (debug panel), and
`Docs/BilliardRogue/research/localization-fonts.md` (tables, fonts, smart strings).

---

## 0. TL;DR

| Topic | What exists | What Boss Battle should do |
|---|---|---|
| Save file | One ES3 JSON file, `SaveFile.es3` in `persistentDataPath`. Each `ES3.Save` rewrites the whole file synchronously. | Save only at checkpoints, never per frame. |
| Keys | `playerPreferenceData`, `debugSettingsData`, `ninjagoProgress`, `chapterSave`, `billiardRogueRun`, `billiardRogueMeta` | Add the key `bossBattleSave` for the mid-battle checkpoint. Put best results in `NinjagoProgress` (append fields). |
| Continue after restart | `NinjagoProgress.lastMiniGame` moves the menu focus to the last mode. `ChapterSave.ModeProgress` powers Continue in chapter modes. Billiard Rogue keeps a `RunState` checkpoint. | Copy the Billiard Rogue checkpoint pattern (`Has/Load/Save/Clear`). Add a start view with Continue and New, like `ChapterStartView`. |
| Debug rows | Every row is a member of `PlayerData/DebugSettings.cs`, listed by reflection. No other class registers rows. | Add `Boss:` rows in the free DebugOrder range −1100…−1041: fields for jumps and cheats, `ES3NonSerializable` properties for config tuning, and void methods wired through a `BossBattleDebugHooks` static class. |
| Localization | One collection, `LocalizationTable`, with locales en, fr-CA, ja, zh-Hans and zh-Hant. Ninjago keys are seeded in English only, and every other locale falls back to en. | Add `BossBattleLocalizationSeeder` with the keys `ninjago.boss.*` plus `ninjago.mode.boss_battle`. For real translations, copy the multi-locale writer from the Billiard Rogue seeder. |
| Analytics | `AnalyticsManager.TrackEvent/TrackGameStart/TrackGameStop/TrackScreen`. `NinjagoAnalytics` has the session and UI wrappers. Each mode has its own static class, such as `OnePunchAnalytics`. Event names and keys are snake_case. | Add `GameModeType.BossBattle => "boss_battle"` to `ContentName`. Write a `BossBattleLog` instance class (in the style of `ChapterLog`) with events prefixed `boss_`. |

---

## 1. Persistence: Easy Save 3 and `PlayerDataManager`

### 1.1 Easy Save configuration

Source: `Assets/Libraries/Easy Save 3/Resources/ES3/ES3Defaults.asset`.
- `_location: 0` is `Location.File`. `directory: 0` is `persistentDataPath`. `path: SaveFile.es3`. `format: 0` is JSON, with `prettyPrint: 1`. There is no encryption or compression. `typeChecking: 1` and `safeReflection: 1` are on.
- In the Editor the file is `~/Library/Application Support/Nex/ninjago-dragon-rising/SaveFile.es3` (companyName `Nex`, productName
  `ninjago-dragon-rising`). On this machine it now holds the keys `ninjagoProgress`, `playerPreferenceData` and `chapterSave`.
  `debugSettingsData` is written only after the Debug panel is saved once.
- Each root is stored as `{"__type":"Nex.Ninjago.NinjagoProgress,Assembly-CSharp","value":{...}}`, and enums are stored as their **int** value.
  For example, `lastMiniGame` is saved as `6`.
- Write path: `ES3.Save<T>(key, value)` → `ES3Writer.Save(overwriteKeys)` → `Merge()` (it copies every other key) → write a backup file
  → `ES3IO.CommitBackup` (`Scripts/ES3.cs:101-110` and `Scripts/Writers/ES3Writer.cs:503-521`). So **every save rewrites the whole file,
  synchronously, on the main thread**. The rename makes the commit atomic.
- `ES3GlobalManager` caches the default file at boot (`autoCacheDefaultFile`). The location is still `File`, so `Save` and `Load` go to disk.
- Serialization rules (also verified in `ES3Reflection.GetSerializableProperties`):
  - Public instance fields are saved.
  - Properties are saved **only** when they have `[SerializeField]` or `[ES3Serializable]`, because safe reflection is on.
  - Public **static** fields are saved too. The comment at `DebugSettings.cs:12-13` says "no const/static fields".
  - Fields added later keep their initializer value. Removed fields are skipped.
  - Renaming or moving a class, or changing its namespace, makes `Load` throw. The load paths catch this and fall back to defaults.
- The scripting defines `DISABLE_PERSISTENCE` and `ENABLE_DEBUG_SETTINGS` are **not** set for any platform (`ProjectSettings/ProjectSettings.asset:852`).
  The debug panel therefore exists only in the Editor and in `DEVELOPMENT_BUILD`.

### 1.2 `PlayerDataManager` (`Scripts/PlayerData/PlayerDataManager.cs`, `Singleton<PlayerDataManager>`, namespace `Nex`)

| Lines | Member | Notes |
|---|---|---|
| 18-24 | `MasterVolumeProperty/SfxVolumeProperty/BgmVolumeProperty` | `AsyncReactiveProperty<float>`. `Audio/VolumeManager.cs` applies them to the mixer and writes them back to `PlayerPreference`, with a 0.5 s debounce and a flush on pause or quit (lines 38-50). |
| 35-50 | `Awake` | Creates `DebugSettings` and `PlayerPreference`. Debug settings are loaded only under `#if ENABLE_DEBUG_SETTINGS \|\| DEVELOPMENT_BUILD \|\| UNITY_EDITOR`. |
| 77, 79-117 | `playerPreferenceData` · `PlayerPreference`, `ResetPlayerPreference()`, `SavePlayerPreference()`, `ScopedPlayerPreferenceUpdate(Action<PlayerPreference>)` | Loaded eagerly. A load that throws is reset. |
| 123-161 | `debugSettingsData` · `DebugSettings`, `event Action? DebugSettingsSaved`, `SaveDebugSettings()` | `DebugSettingsSaved` wakes `DebugPrinter`. |
| 168-259 | `billiardRogueRun` / `billiardRogueMeta` · `HasRunSave()`, `LoadRun()`, `SaveRun(RunState)`, `ClearRun()`, `MetaProgress`, `SaveMetaProgress()` | **This is the checkpoint template.** `LoadRun` deletes an unreadable save (lines 207-216). Under `DISABLE_PERSISTENCE` the run is kept as an in-memory JSON copy (lines 222-224, 239-241). |
| 265-303 | `ninjagoProgress` · `NinjagoProgress` (lazy), `ScopedNinjagoProgressUpdate(Action<NinjagoProgress>)`, `ResetNinjagoProgress()` | A load that throws falls back to `new NinjagoProgress()` **without overwriting** the file, but the next scoped update overwrites it. |
| 309-347 | `chapterSave` · `ChapterSave` (lazy), `ScopedChapterSaveUpdate(Action<ChapterSave>)`, `ResetChapterSave()` | Same pattern as `NinjagoProgress`. |
| 353-362 | `AppViewState appViewState` | In-memory only (`enableHighlighting`). It is not saved. |

The pattern to copy for a lazily loaded document (lines 265-303):
```csharp
const string ninjagoProgressKey = "ninjagoProgress";
NinjagoProgress? ninjagoProgress;
public NinjagoProgress NinjagoProgress => ninjagoProgress ??= LoadNinjagoProgress();
public void ScopedNinjagoProgressUpdate(Action<NinjagoProgress> modifier)
{
    modifier(NinjagoProgress);
#if !DISABLE_PERSISTENCE
    ES3.Save(ninjagoProgressKey, NinjagoProgress);
#endif
}
static NinjagoProgress LoadNinjagoProgress() { /* try ES3.Load(key, new T()) catch → LogError + new T() */ }
```

### 1.3 Data classes

- `Scripts/PlayerData/PlayerPreference.cs:8-30`: volumes, plus a Billiard Rogue region with `localeCode` ("" means the system locale), `numPlayers` (the
  Ninjago menu writes it at `Main/Coordinator/MainCoordinator.cs:92`), `aimGuideLength` and `screenShake`. The class comment says new
  fields are safe to append.
- `Scripts/Ninjago/Persistence/NinjagoProgress.cs:8-74`: `[Preserve] public class NinjagoProgress`, with "Public fields only; append new fields,
  never rename". Line 11 is `public GameModeType lastMiniGame`. Then come per-mode `xxxRuns` counters and bests. The convention is
  **−1 means not set yet** for "fewest" or "fastest" values (`chaseFewestCarHits = -1`, `buildBestSeconds = -1f`).
- `Scripts/Ninjago/Persistence/ChapterSave.cs:14-87`: `ChapterSave { List<ModeData> playerModes; List<ModeProgress> progress; int nextModeNumber }`
  and `[Serializable, Preserve] ModeProgress { modeId, nextLevel, bestStars, lastLevel, lastStars, lastHeartsLeft, finished; RecordLevel(..); Restart() }`.
- `Gameplay/Core/GameModeType.cs:3-21`: `None=0, PressButtonToWin=1, SlipAndSpin=2, ChestChase=3, StoneKick=4, EarthSeal=5,
  RobotMecha=6, CrouchRun=7, ThreeWeapons=8, OnePunch=9, Guard=10, Call=11, Build=12, Chapter=13, Forge=14`. The value is saved as an int,
  so **append `BossBattle = 15` and never renumber**.

### 1.4 How Continue after a restart works today

1. **Menu focus.** `NinjagoGame.RunAsync` (`Ninjago/Flow/NinjagoGame.cs:67`) stores
   `ScopedNinjagoProgressUpdate(p => p.lastMiniGame = Mode)`. `MainCoordinator.CreateGameModeSelectionView` (line 62) passes it to
   `GameModeSelectionView.Initialize`, which focuses that button (`Main/Views/GameModeSelectionView.cs:71-72`). `ChapterGame`
   overrides `RunAsync` and never sets `lastMiniGame`. If you override `RunAsync`, set the field yourself.
2. **Chapter progress.** `ChapterGame.PlayModeAsync` (`Ninjago/Flow/ChapterGame.cs:84-96`) works like this:
   - `canContinue = progress is { finished: false, nextLevel: > 0 }`.
   - `ChapterStartView` returns Continue or Start (`ChapterStartView.cs:49-73`, with UiAction `"continue"` or `"start"`).
   - Start calls `ScopedChapterSaveUpdate(save => save.ProgressOf(id).Restart())`.
   - After each level, `RecordLevel(...)` is saved (lines 127-128).
   - Continue resumes at the rest view before the next unplayed level (line 108).
3. **Billiard Rogue run checkpoint.** `BilliardRogue/Persistence/RunPersistence.cs` works like this:
   - `Load()` drops finished or unreadable saves (lines 40-52).
   - `BeginRun` clears the old save, then saves (lines 55-62).
   - `SaveTurnBoundary` saves "at stable points only" (line 65).
   - `CompleteRun` updates the meta progress and clears the run (lines 87-99).
   - `Abandon` clears the run (line 102).
   - It talks to storage through an `IRunStore` (`PlayerDataRunStore`), so EditMode tests can run without the singleton.

The result path for a mode's best values, from `ChestChaseGame.BuildOutcome` (`Ninjago/Flow/ChestChaseGame.cs:74-86`) and `BuildGame` (lines 89-98):
```csharp
var progress = PlayerDataManager.Instance.NinjagoProgress;
var isNewBest = progress.buildBestSeconds >= 0f && scene.Elapsed < progress.buildBestSeconds;   // read BEFORE updating
PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
{
    saved.buildRuns++;
    if (scene.IsOpen && (saved.buildBestSeconds < 0f || scene.Elapsed < saved.buildBestSeconds)) saved.buildBestSeconds = scene.Elapsed;
});
if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
return new NinjagoOutcome(title, lines, "win" /* analytics result */, details /* GameAnalyticsProperties */);
```

### 1.5 Proposed Boss Battle save design

**A. Append these best results to `NinjagoProgress`** (they survive a corrupt checkpoint):
```csharp
public int bossBattleRuns;
public int bossBattleWins;
// Fastest win in seconds (play time summed across continues); -1 until the first win.
public float bossBattleBestSeconds = -1f;
// Furthest point reached: 0 none, 1 car phase, 2 plane phase, 3 boss defeated.
public int bossBattleFurthestPhase;
// Fewest hits taken in a win; -1 until the first win.
public int bossBattleFewestHits = -1;
```

**B. Store the checkpoint in its own key, `bossBattleSave`**, using the same pattern as `RunState`. A separate key keeps a bad checkpoint from
resetting every other mode's bests. Put it in the new file `Scripts/Ninjago/Persistence/BossBattleSave.cs`:
```csharp
[Preserve]
public class BossBattleSave
{
    // Bump when a field changes meaning; Load drops checkpoints with another version.
    public int version = 1;
    public string runId = "";               // joins analytics sessions of a continued battle
    public int numPlayers = 1;
    public BossBattlePhase phase;           // enum with explicit ints: Car = 1, Plane = 2 (append only)
    public float bossHp01 = 1f;             // 0..1 of BossBattleConfig max HP
    public int pickupsCollected;            // phase 1: 0..(pickupsToPhaseTwo-1)
    public int rocketsFired;
    public int wakesPassed;                 // phase 2: thresholds 70/30/5 % already crossed (0..3)
    public int clapsDone;                   // 0, 1, 2 (second clap is bigger)
    public float elapsedSeconds;            // play time so far, for bossBattleBestSeconds
    public int hitsTaken;
    public long savedAtUtcTicks;
}
```
In `PlayerDataManager`, add the region `#region Boss Battle Save` as a copy of lines 167-241 with these members:
`const string bossBattleSaveKey = "bossBattleSave";`, `HasBossBattleSave()`, `BossBattleSave? LoadBossBattleSave()` (it deletes an unreadable
or wrong-version save), `SaveBossBattle(BossBattleSave)`, `ClearBossBattleSave()`, and the `DISABLE_PERSISTENCE` memory copy.

**Checkpoint moments.** Each one costs one full-file write.
1. Battle start, after calibration: new `runId`, `phase=Car`, HP at 100 %.
2. Each power-up pickup, once its rocket volley has landed. This saves the HP and the pickup count.
3. Phase 2 start, after the transform cutscene.
4. Each wake threshold crossed (70, 30 and 5 %).
5. After each boss clap resolves.
6. `OnApplicationPause(true)`: save the **last stable snapshot**, not the state mid-attack. `VolumeManager` (lines 45-50) shows why: on Android TV, Home can kill the process.

**Clear the checkpoint** on a win (after the explosion and before the result view), on a loss, or when the player picks New on the start view.
**Keep it** on Back or quit, so the next launch offers Continue.

**What Continue restores.** Phase 1 restarts the road with the boss HP and pickup count restored. The obstacles, rocks and minions start fresh.
Phase 2 skips the cutscene and restores the HP, the wakes and the claps.

**Settings.** Volumes and `numPlayers` are already saved. Append Boss settings to `PlayerPreference`, for example
`public int bossBattleDifficulty = 1; // 0 easy 1 normal 2 hard`, and reuse the existing `screenShake`. Change them only through
`ScopedPlayerPreferenceUpdate`.

**Locale.** The Ninjago app does **not** apply `PlayerPreference.localeCode`. Only `BilliardRogue/Flow/LocaleRestore.cs:17-40`
does (it is called from `BilliardRogueInitializer.cs:58`). If Boss Battle adds a language setting, call `LocaleRestore.ApplyAsync`, which is in namespace
`Nex.BilliardRogue`, at boot.

---

## 2. Debug settings

### 2.1 How the panel works

- To open it, enter the secret code ↑↑↓↓←→←→ on any view. `NinjagoGamesBuilder.AddSecretCode()` (`Ninjago/Editor/NinjagoGamesBuilder.cs:172-198`)
  writes that sequence into `Assets/Prefabs/Coordinators/MainViewManager.prefab`, which both MainScene and GameScene use.
  `ViewManager.Awake` adds `detector.AddListener(0, OpenDebugSettings)` only in debug builds (`Main/Views/Framework/ViewManager.cs:66-71`).
  `OpenDebugSettings` (line 442) raises `SecretCodeEntered`, plays `GenericEnter` and pushes `debugSettingsPrefab`. Debug builds also show a Debug button on the top
  control panel (`ComputeControlConfig`, lines 81-90).
- `Main/Views/Debug/DebugSettingsView.cs:16-33`: `debugSettingsPanel.Initialize(save: SaveDebugSettings + SavePlayerPreference, close: PopSelf)`,
  then `PopulateRows(debugSettings, 0)`. The `AnalyticsScreenName` is `"debug-settings"`.
- `Library/PackageCache/team.nex.debug-settings@c367bcc8ae17/Runtime/DebugSettingsPanel.cs`:
  - `PopulateRows<TSettings>` (line 212) reflects the **public** fields, the readable properties, and the public `void` methods with primitive parameters
    (`DebugSettingsMethodRow.IsValidMethod`). Property setters are skipped.
  - Rows are sorted **ascending** by `[DebugOrder]` (line 201), so −1000 appears above −900. Rows without an order go last.
  - Supported row types (line 353): bool, all numeric types, string, enum, DateTime, and long marked `[DateTimeTicks]`.
    `[IntChoices("Off","A",..)]` turns an int into a picker (the names map to 0..n−1). `[NumericSteps(Steps/Min/Max)]` is for floats,
    and `[NumericSteps(IntSteps/IntMin/IntMax)]` is for ints.
  - **Values are written to the target only on Save.** `Save()` (line 66) calls `UpdateSettings()`, and the Save button runs `CloseSelf(true)`.
    Back goes through `SimpleView.OnBackButton → PopSelf` and **discards the edits**, config properties included.
  - Method rows close the panel first and then invoke the method (`AddMethodRow`, line 315). `[SaveBeforeInvoking]` saves the edits first.
- Attributes are in `Runtime/Attributes/DebugAttributes.cs` (namespace `Nex.Dev.Attributes`): `DebugOrder(int)`, `Description(string)`,
  `NumericSteps`, `IntChoices`, `StringChoices`, `SaveBeforeInvoking`, `VisibilityLevel(int)`, `CopyableTextArea`, `DateTimeTicks`.

### 2.2 How Ninjago modes add live-tuning rows

There is **no separate registrar class**. Every Ninjago row is declared in `Scripts/PlayerData/DebugSettings.cs`
(namespace `Nex`, `using Nex.Ninjago;`). There are three kinds.

1. **Saved fields** (in `debugSettingsData`; they take effect only in debug builds):
   - `ninjaSimulatedBody` (line 104).
   - The Chapter jumps `chapterJumpLevel`, `chapterJumpBlock` and `chapterJumpMode` (lines 393-398). Gameplay reads them only under
     `#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`, for example `ChapterGame.ApplyDebugJump` (`ChapterGame.cs:282-290`).
2. **Config properties, edited live** (for this session only and never saved). They are `[ES3NonSerializable]` and forward to a ScriptableObject through `GameConfigsManager`:
   ```csharp
   [DebugOrder(-80), Description("Chase: Deadzone (in)"), NumericSteps(Steps = 0.25, Min = 0, Max = 6), ES3NonSerializable]
   public float ChaseDeadzone { get => Chase.DeadzoneInches; set => Chase.DeadzoneInches = value; }
   ...
   static ChaseConfig Chase => GameConfigsManager.Instance.ChaseConfig;                      // line 450
   static ChaseConfig.VehicleSettings Car => Chase.GetVehicle(VehicleType.Car);             // line 451
   ```
   The setters on the config clamp the value, for example `ChaseConfig.DeadzoneInches { set => deadzoneInches = Mathf.Max(0f, value); }`
   (`Ninjago/Configs/ChaseConfig.cs:82`), or write public fields of an `EnumDictionary` entry (`Car.obstacleSpacingSeconds`).
   Chase rows are at lines 365-384 (DebugOrder −80…−71).
3. **Method rows** (lines 400-420):
   ```csharp
   [DebugOrder(-997), Description("Chapter: Play Mode (with the jumps above)")]
   public void PlayChapterMode()
   {
   #if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
       UnityEngine.Debug.Log($"[DebugSettings] {ChapterDebugHooks.PlayMode(chapterJumpMode)}");
   #endif
   }
   [DebugOrder(-69), Description("Save: Reset Ninjago Progress"), SaveBeforeInvoking]
   public void ResetNinjagoProgress() => PlayerDataManager.Instance.ResetNinjagoProgress();
   ```
   Hook classes:
   - `Ninjago/Chapter/Forge/ChapterDebugHooks.cs` is static and works without a live scene. `PlayMode` sets `SelectedMode` and loads GameScene.
   - `BilliardRogue/Flow/DebugHooks.cs` holds `Func<…,bool>?` handlers that gameplay assigns while alive (`GameplayDebugCommands.Register/Unregister`,
     lines 51-71, which clears a handler only when `ReferenceEquals` matches). Its wrappers return `"X: ok" / "rejected" / "not registered"`,
     or `"disabled in release builds"`.

DebugOrder ranges in use:
- 0…64: Billiard Rogue.
- −69/−70: Save and Setup.
- −80…−71: Chase. −99…−87: Fight. −100: Ninja.
- −200…−176: Cursor and Kick. −170…−125: Seal.
- −300…−288: Mecha. −400…−394: Crouch. −500…−493: Weapons. −600…−589: Punch.
- −700…−695: Guard. −800…−794: Call. −900…−895: Build.
- −1000…−978: Chapter.

**−1100…−1001 is free**, and rows there sort to the top of the panel.

`DebugPrinter` (`Utils/DebugPrinter.cs:78`) is used through `DebugPrinter.Instance.Print(key, text)`. Guard each call with
`#if ENABLE_DEBUG_SETTINGS || ...` and `DebugSettings.enableDebugPrinter`, as `OnePunchStreet.PrintDebug` (lines 346-360) does. The printer's
OnGUI sleeps while it is off.

### 2.3 Proposed Boss Battle debug rows

Add `static BossBattleConfig Boss => GameConfigsManager.Instance.BossBattleConfig;`. This needs a new serialized field and an accessor in
`Configs/GameConfigsManager.cs:17-73`. Suggested rows:

| Order | Member | Kind |
|---|---|---|
| −1100 | `int bossJumpPhase = -1` "Boss: Jump To Phase (-1 = off, 1 car, 2 plane)" `NumericSteps(IntSteps=1, IntMin=-1, IntMax=2)` | saved field |
| −1099 | `int bossStartHpPercent = -1` "Boss: Start HP % (-1 = off)" `NumericSteps(IntSteps=5, IntMin=-1, IntMax=100)` | saved field |
| −1098 | `bool bossGodMode` "Boss: God Mode (car/plane take no damage)" | saved field |
| −1097 | `bool bossSkipCutscenes` "Boss: Skip Cutscenes" | saved field |
| −1096 | `int bossForceAttack` `[IntChoices("Off","Rock","Minion","Slam","Wind","Clap")]` "Boss: Only This Attack" | saved field |
| −1090…−1075 | `BossMaxHp`, `BossPickupsToPhaseTwo`, `BossRocketsPerPickupStep`, `BossRockFollow`, `BossRockLock`, `BossMinionDps`, `BossWallGrow`, `BossSlamBricks`, `BossWindSpacing`, `BossLaserDps`, `BossTiredSeconds`, `BossWakeHpPercents`, `BossClapSpeed`, `BossClapTwoScale` | `ES3NonSerializable` properties that forward to `BossBattleConfig` |
| −1060 | `void BossDamage(float percent)` | method, routed through `BossBattleDebugHooks` |
| −1059 | `void BossGrantPickup()` · −1058 `void BossTriggerAttack(int attack)` · −1057 `void BossWinNow()` | methods |
| −1050 | `[SaveBeforeInvoking] void ClearBossBattleSave() => PlayerDataManager.Instance.ClearBossBattleSave();` | method |

Rules: debug-only fields must default to "off", because release builds use the field initializers. Read them only inside
`#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`.

---

## 3. Localization

### 3.1 Assets

- `Assets/Localization/LocalizationSettings.asset`:
  - The project locale is `en`.
  - Selectors run in this order: CommandLine (`-language=`), then System, then Specific `en`.
  - `InitializeSynchronously: 0`.
  - The string database has `m_UseFallback: 1`, and a missing entry shows `No translation found for '{key}' …`.
- Locales are in `Assets/Localization/Locales/`: `en`, `fr-CA`, `ja`, `zh-Hans` and `zh-Hant`. Every non-English locale has a `FallbackLocale` set to `en`.
- There is one string table collection, **`LocalizationTable`** (`Assets/Localization/LocalizationTables/`). It has a Google Sheets extension, which is not needed.
  Each per-locale table is addressable with the `Preload` label.
- Keys today: `br.*` 235 (translated in all 5 locales), `ninjago.*` 177, `chapter.*` 80, `forge.*` 108, and 10 legacy keys. **All the `ninjago`, `chapter` and `forge` keys exist only in `en`**. The other locales show English through the fallback.

### 3.2 Runtime API

- `Assets/Libraries/Localization/NexLocalizedString.cs`, `Nex.Localization.NexLocalizedString : LocalizeStringEvent`. It needs a `TextMeshProUGUI`.
  - `SetSmartStringArgument(key, value)` (line 54) sets one argument and refreshes.
  - `SetSmartStringArguments(params (string,object)[])` (line 70) sets several arguments with **one** refresh.
  - `StringReference` takes the `LocalizedString`.
- `Scripts/Ninjago/Views/LocalizedStrings.cs:14`, `LocalizedStrings.WithArguments(LocalizedString template, params (string key, object value)[])`:
  it returns a **new** `LocalizedString` (same table and entry) with the arguments attached as a dictionary. This is the house rule.
  A smart-string label's prefab keeps an **empty** reference. The builder passes `key: null` to `NinjagoEditorUtils.Label`, and the owner assigns
  the reference together with its arguments at runtime. Example from `Ninjago/Chase/RunnerView.cs:81-82,141-142`:
  ```csharp
  dodgesLabel.StringReference = LocalizedStrings.WithArguments(dodgesText, ("count", 0));   // Initialize
  ...
  dodgesLabel.SetSmartStringArgument("count", world.GetDodges(world.CurrentVehicle));      // on CountsChanged only
  ```
  Text that changes every frame, such as a timer or boss HP number, uses a plain `TMP_Text` (`RunnerView.timeLeft`). It does not use a smart string.
- `ResultLine.Data(LocalizedString template, Color color, params (string,object)[] args)` (`Ninjago/Views/ResultLine.cs:20`) formats result lines.
- `Assets/Libraries/Localization/EnumStringSelector<TEnum>` maps an `EnumDictionary<TEnum, LocalizedString>` to a `NexLocalizedString`. Configs also keep
  `EnumDictionary<…, LocalizedString>` fields, for example `ForgeConfig.rideNames` (`Ninjago/Chapter/Data/ForgeConfig.cs:40-49`).

### 3.3 Seeding keys without Google Sheets

`Scripts/Ninjago/Editor/NinjagoLocalizationSeeder.cs` holds the rows `(string key, string english, bool smart)[]` (lines 16-194).
`Upsert(rows)` (lines 201-222) does the following:
- `shared.GetEntry(key) ?? shared.AddKey(key)`. GetEntry comes first, because `AddKey` on an existing key creates `"key 1"`.
- Writes the **`en` table only**.
- Sets `IsSmart`.
- Forces `StringDatabase.UseFallback = true`.
- Marks the assets dirty, raises `RaiseCollectionModified`, and calls `SaveAssets`.

`Scripts/Ninjago/Editor/Chapter/ChapterLocalizationSeeder.cs` (lines 9-202) uses the same approach in its own file, with `Run() => NinjagoLocalizationSeeder.Upsert(rows)`.
Callers:
- `NinjagoBuildAll.Run()` (line 26).
- Each per-mode builder, for example `GuardBuilder.Run()` (line 33).
- `ChapterForgeBuilder` (line 25).

Run the seeder **before** a builder binds keys. `NinjagoEditorUtils.KeyId` throws `Missing localization key '<k>' (run the seeder)` when a key is missing.

Binding helpers are in `Scripts/Ninjago/Editor/NinjagoEditorUtils.cs`:
- `TableName = "LocalizationTable"` (line 23).
- `KeyId(key)` (113).
- `WriteLocalized(SerializedProperty, key)` (119). It writes `m_TableReference.m_TableCollectionName = "GUID:<n>"` and `m_KeyId`.
- `SetLocalized(Object target, string field, string key)` (128).
- `Label(parent, name, key|null, preview, size, color, …)` (166). It creates TMP text with a `NexLocalizedString` in the font `FontPath`.
- `ChapterAssetsBuilder.Loc(key)` (`Ninjago/Editor/Chapter/ChapterAssetsBuilder.cs:52`) returns a `LocalizedString` for assets set by reflection, for example `rides[RunnerRide.Car] = Loc("forge.ride.car")`.
- The game-prefab fields come from `SetLocalized(game, "carLine", "ninjago.result.chase_car")` (`NinjagoGamesBuilder.cs:69-74`).
- The menu name comes from `Mode(settings, modes, GameModeType.X, prefabPath, "ninjago.mode.x")` (line 143). The menu order is set by `WriteMenuOrder` (line 119), which has a hardcoded list.

**Every locale, still without Sheets.** `Scripts/BilliardRogue/Editor/LocalizationSeeder.cs` reads
`Assets/Localization/BilliardRogue/BilliardRogueStrings.csv` (columns `key|en|fr-CA|zh-Hans|zh-Hant|ja|isSmart`) through
`Assets/Localization/BilliardRogue/Editor/LocalizationCsv.cs`. `LocalizationCsv` hardcodes `OwnedPrefix = "br."` and `LocaleCodes = {en, fr-CA, zh-Hans, zh-Hant, ja}`.
It rejects empty values and placeholders that differ from `en`.
- `WriteEntries` (line 176) writes one row into every locale table.
- `RemoveStale` (line 202) deletes owned-prefix keys that are no longer listed.
- `SetPreloadTableFlag(true)` (line 146) sets the preload flag.
- `Verify()` (line 60) looks each key up per locale with `FallbackBehavior.DontUseFallback`.

For translated Boss strings, either:
- extend the tuple to `(key, en, frCA, zhHans, zhHant, ja, smart)`, keep the English-only `Upsert` call for the `ninjago.mode.boss_battle` key, and copy `WriteEntries`; or
- generalize `LocalizationCsv` to accept a path and a prefix (`ninjago.boss.`).

### 3.4 Fonts and fallbacks

- The default Ninjago UI font is `Assets/Fonts/BrainParty/Play Chickens SDF.asset` (`NinjagoEditorUtils.FontPath`). It is static, and its fallback table holds
  `Assets/Fonts/Localization/GlowSans-ja/GlowSansJ-Normal-Medium SDF.asset`, which is **Dynamic** (`m_AtlasPopulationMode: 1`), multi-atlas, and
  has `ClearDynamicDataOnBuild`. Any CJK character renders at runtime, but the first appearance of a new glyph rebuilds the atlas, which causes a hitch on low-end Android.
  The global TMP fallback list (`TextMesh Pro/Resources/TMP Settings.asset`) is empty.
- **Pixel font (needed for HD-2D):**
  - `Assets/Fonts/BilliardRogue/BilliardPixel_TMP.asset` and `BilliardPixelBold_TMP.asset` are static, 1-bit raster-hinted and point-filtered.
    Their charset (`BilliardPixel_charset.txt`) covers ASCII, Latin-1, Œœ, quotes, dashes, €, the arrows ←↑→↓, ★ and ♥.
  - Both fall back to `BilliardPixel_CJK_TMP.asset`, which is **static** and baked from every character in every `LocalizationTable` locale.
  - Built by `Scripts/BilliardRogue/Editor/FontAssetsBuilder.cs` (`Run()`, line 74). **Re-run it after seeding CJK text**, or the missing glyphs render as boxes.
  - Use sizes of `fontSize = 16 × n` and no faux bold.
  - Outline variants exist (`BilliardPixelOutline_TMP`, `BilliardPixelBoldOutline_TMP`): place a second text layer underneath for HP or damage numbers.
  - A Boss builder should override `tmp.font` after `NinjagoEditorUtils.Label`, which always assigns Play Chickens.

---

## 4. Analytics

### 4.1 Core

`Scripts/Analytics/AnalyticsManager.cs` (`Singleton<AnalyticsManager>`, namespace `Nex`):
- `TrackEvent(string eventName, GameAnalyticsProperties? props = null)` (line 17). It logs `"[Analytics] EVENT: …"` only in debug builds, then calls `GameAnalytics.Instance.Track`.
- `TrackGameStart(string screen, int numPlayer, string gameMode = "default", props)` (38) and `TrackGameStop(props)` (44) define a play session.
- `TrackPause()` and `TrackResume()` (26/32). Ninjago never calls them; only the Billiard Rogue `RunAnalytics` does.
- `TrackScreen(screenName, props)` (50) is called **automatically** by `ViewManager` whenever a view with a non-empty `AnalyticsScreenName`
  becomes the top view (`ViewManager.cs:343-347`).
- `GameAnalyticsProperties` is a `Dictionary<string, GameAnalyticsValue>` (`team.nex.platform-utils@7615141761cc/Scripts/GameAnalyticsProperties.cs:58`).
  Values convert implicitly from `bool/int/float/double/string` and from arrays (lines 382-395). Build a **new** instance for every event.

### 4.2 Ninjago conventions (`Scripts/Ninjago/Analytics/NinjagoAnalytics.cs`)

- Every class is a static wrapper with `static AnalyticsManager Analytics => AnalyticsManager.Instance;`.
- Event names and property keys are **snake_case**. Screen names are **kebab-case** (`"runner"`, `"ninjago-setup"`, `"chapter-start"`).
- UI button names are lowercase words (`"back"`, `"retry"`, `"menu"`, `"continue"`, `"start"`, `"confirm"`). The menu sends `mode.ToString()` (`MainCoordinator.cs:72`).
- Floats are rounded to 2 decimals (`Round`). `player_index` is 0-based, and −1 means none or shared.

Session and UI methods:
- `MiniGameStart(GameModeType mode, int requestedPlayers)` (line 15) calls `TrackGameStart(ContentName(mode), players, mode.ToString())`.
- `MiniGameEnd(mode, string result, int activePlayers, GameAnalyticsProperties details)` (line 20) sends the event `minigame_end` with `minigame`, `result` and `active_players`, plus the details, then calls `TrackGameStop({result})`.
- `UiAction(string screen, string button, int index = -1)` (line 29) sends the event `ui_button_click` with `{screen, button, index}`.
- Setup events: `setup_player_calibrated`, `setup_hands_confirmed` and `setup_complete`, sent from `Ninjago/Views/NinjagoSetupView.cs:169,198,284`.
- Chase events: `segment_start`, `obstacle_dodge` and `obstacle_hit`, with `vehicle`, `obstacle`, `player_index` (the player who steered harder) and `p1/p2_steer_x/y`. They are sent from `Ninjago/Chase/RunnerWorld.cs:150,273,284`.
- `ContentName(mode)` (lines 129-143) maps each mode to a snake_case name. **Add `GameModeType.BossBattle => "boss_battle"`**. Without it, the content name would be `"BossBattle"`.

Calls from `NinjagoGame.RunAsync` (lines 65-89):
- `MiniGameStart` runs **once**, before the retry loop.
- `MiniGameEnd` runs at the end of **every** round.
- A back-out sends `"abandon"` with empty details.
- The result details come from `NinjagoOutcome.AnalyticsResult` and `AnalyticsDetails` (`Ninjago/Views/NinjagoOutcome.cs`).

Results already in use: `"complete"`, `"win"`/`"lose"` (Build), plus mode-specific values.
**Gotcha:** after a Retry, the second round's `TrackGameStop` has no matching `TrackGameStart`. Boss Battle should call `TrackGameStart` once per attempt.

Mode-specific patterns:
- `OnePunchAnalytics.cs`: static methods with the `punch_` prefix. Every event carries `player_index` and the 1-based `wave` through `Player(playerIndex, wave)` (line 103).
  `WaveEnd(int wave, string result, float seconds, GameAnalyticsProperties details)` merges the per-player counts `p1_*` and `p2_*`.
- `GuardAnalytics.cs`: `guard_throw`, `guard_raise_alone` and `guard_big_throw`. Each one is sent once per gameplay beat, never per frame.
- `ChapterLog.cs` is an **instance** class: `new ChapterLog(modeId, builtIn)` holds the `Level`, `Block` and `BlockType` context.
  Every event goes through `Props(playerIndex)`, which adds `mode_id/built_in/level/block/block_type/player_index` (lines 130-138).
  - `ModeStart` calls `TrackGameStart("chapter", players, modeId, Props())`.
  - `ModeEnd(result)` sends `chapter_mode_end` and then calls `TrackGameStop`.
  - This is the best fit for Boss Battle, because each event needs the phase, HP and run id.
- `BilliardRogue/Analytics/RunAnalytics.cs`:
  - `TryGetManager` (lines 25-29) guards against a destroyed singleton.
  - Calling `GameAnalytics.Instance.RegisterSessionProperties("run_id", …, SessionPropertiesScope.PLAY)` **after** `TrackGameStart` (lines 81-91) tags every event in the session.
  - It caches enum names, because `Enum.ToString` allocates.
  - It also sends `secret_code_entered` through `ViewManager.SecretCodeEntered` and `settings_changed`.

### 4.3 Proposed `BossBattleLog`

Create `Scripts/Ninjago/Analytics/BossBattleLog.cs`, a sealed instance class modeled on `ChapterLog`. Its `Props()` adds `run_id`, `phase`,
`boss_hp` (percent, int), `elapsed_s` and `player_index`. Use `NinjagoAnalytics.UiAction` for UI events and `NinjagoAnalytics.MiniGameStart/End` for the session.

| Event | When | Extra properties |
|---|---|---|
| `TrackGameStart("boss_battle", players, "new"\|"continue")` + PLAY props `run_id`, `is_continue` | each attempt, after the start view | `start_phase`, `start_boss_hp` |
| `boss_phase_start` / `boss_phase_end` | car phase and plane phase | `result` (`cleared`/`down`/`abandon`), `seconds`, `hits_taken` |
| `boss_cutscene` | car→plane transform, boss explosion | `name`, `seconds`, `skipped` |
| `boss_attack_start` | each rock, minion, slam, wind or clap | `attack`, `index`, `side` (`left`/`right`/`none`) |
| `boss_attack_result` | when that attack resolves | `attack`, `result` (`dodged`/`hit`/`cleared`), `reaction_s`, `damage` |
| `boss_minion_cleared` | the player smashed the wall on that side | `side`, `seconds_on_car`, `damage_taken` |
| `boss_powerup` | pickup collected | `pickup` (1-based), `rockets`, `hits`, `boss_hp_before` |
| `boss_wake` | 70, 30 or 5 % crossed | `threshold` |
| `boss_laser_window` | end of each tired window | `seconds`, `hit_ratio`, `damage` |
| `boss_clap` | each clap | `clap` (1/2), `result`, `wall_scale` |
| `boss_checkpoint_continue` | Continue pressed | `phase`, `boss_hp`, `pickups`, `wakes` |
| `minigame_end` (through `MiniGameEnd`) | win, lose or abandon | `result` = `win`/`lose`/`abandon`, `seconds`, `hits_taken`, `rockets`, `pickups`, `furthest_phase`, `pN_steer_share` |
| `ui_button_click` | start view `continue`/`start`/`back`; HUD `back`; result `retry`/`menu`/`back`; cutscene `skip` | — |
| `settings_changed` | a Boss setting changes | `setting`, `new_value` |

Screen names, one per view (each needs a `ViewIdentifier`, appended after the Ninjago block in `Main/Views/Framework/View.cs:51-65`):
`boss-start`, `boss-road`, `boss-cutscene`, `boss-sky`, `boss-result`. `ninjago-setup` is reused for calibration.
- Send at most one event per attack or pickup. Never send one per rocket, laser tick or minion damage tick.
- Aggregate those into the window and attack events.
- `TrackEvent` calls `Debug.Log` in development builds and allocates a dictionary, so keep events off the per-frame path.

---

## 5. Gotchas

1. **ES3 rewrites the whole file on every save**, synchronously on the main thread, by merging every key. Never save per frame or per hit. One write per checkpoint is fine.
2. **Saved classes are append-only.**
   - Do not rename fields, classes or namespaces. The `__type` string is checked, and a mismatch resets the data to defaults.
   - Give enums explicit ints and append new values (`GameModeType.BossBattle = 15`).
   - Add `[Preserve]` and a public parameterless constructor.
   - Keep `UnityEngine.Object` references out of saves; store ids or enums instead.
3. `Load…` falls back to defaults without rewriting the file, **but the next `Scoped…Update` overwrites it**. A checkpoint in its own key limits the damage.
4. `DebugSettings.ClearAllDataCache()` (lines 479-485) resets the preference, the meta progress and the run, **not** `NinjagoProgress`, `ChapterSave` or a new Boss save. Use the separate `Save:` rows.
5. **Any `public void` method on `DebugSettings` becomes a button**, including static ones. Keep helpers private.
   - Config properties need `[ES3NonSerializable]` by convention.
   - Their setters change the ScriptableObject in memory. In the Editor that change can reach disk if something else marks the asset dirty and saves.
   - Edits apply only when the panel is **saved**.
6. Debug fields are saved in dev builds, so a forgotten `bossJumpPhase` stays on after a restart. The Chapter jump fields behave the same way.
7. **Smart strings need `smart = true` in the seeder.** Otherwise `{hp}` shows literally.
   - Labels whose arguments are set at runtime keep an empty reference in the prefab.
   - Call `SetSmartStringArguments` (one refresh), not `SetSmartStringArgument` several times.
   - Never refresh a smart string every frame.
8. Ninjago keys are English only. If the pixel font is used and CJK translations are added, re-run `FontAssetsBuilder.Run()`.
9. **`ChapterGame` overrides `RunAsync`, so it skips `lastMiniGame`, `MiniGameStart` and `MiniGameEnd`.** A `BossBattleGame` that overrides
   `RunAsync` to add the start view must set `lastMiniGame` and send the session events itself.
10. The `TrackGameStart`/`TrackGameStop` pairs drift on Retry in `NinjagoGame` (see §4.2).
11. `AnalyticsManager.Instance` is a scene singleton. Keep analytics out of pure-logic classes so EditMode tests do not need it (no Ninjago test touches analytics), or guard with `TryGetManager`.
12. `WriteMenuOrder` (`NinjagoGamesBuilder.cs:119`) has a hardcoded list of modes. Add `GameModeType.BossBattle` to it, or the mode never shows in the menu.

---

## 6. Boss Battle checklist (this subsystem)

**Persistence**
- [ ] Add `BossBattle = 15` to `Gameplay/Core/GameModeType.cs`. Add the `"boss_battle"` case to `NinjagoAnalytics.ContentName`.
- [ ] Append the `bossBattle*` fields to `NinjagoProgress` (§1.5 A), using −1 for values not set yet.
- [ ] Add `Scripts/Ninjago/Persistence/BossBattleSave.cs` (`[Preserve]`, public fields, a `version` field, and the enum `BossBattlePhase { None = 0, Car = 1, Plane = 2 }`).
- [ ] Add the `#region Boss Battle Save` region to `PlayerDataManager`: key `bossBattleSave`, plus `HasBossBattleSave`, `LoadBossBattleSave`,
      `SaveBossBattle` and `ClearBossBattleSave`, with the `DISABLE_PERSISTENCE` memory copy. A version mismatch or an unreadable save deletes the key.
- [ ] In `BossBattleGame : NinjagoGame`:
  - Override `RunAsync`.
  - Set `lastMiniGame`.
  - Show the start view (Continue, New, Back).
  - Calibrate.
  - Play the phases.
  - Save at the checkpoints in §1.5.
  - Clear the save on win, loss or New.
  - Update the bests with `ScopedNinjagoProgressUpdate`, reading `isNewBest` before the update.
- [ ] Save on `OnApplicationPause(true)` from the owner of the live battle state, using the last stable snapshot.
- [ ] Append Boss settings, if any, to `PlayerPreference`, and change them through `ScopedPlayerPreferenceUpdate`.
- [ ] Optional: add an `IBossBattleStore` (like `IRunStore`) so checkpoint logic gets EditMode tests.

**Debug settings**
- [ ] Add the `BossBattleConfig` field and accessor to `GameConfigsManager`. Add the accessor `static BossBattleConfig Boss` to `DebugSettings`.
- [ ] Add the `Boss:` rows in DebugOrder −1100…−1050 (§2.3): saved fields defaulting to off, `ES3NonSerializable` config properties, and void methods.
- [ ] Add `Scripts/Ninjago/BossBattle/BossBattleDebugHooks.cs` (static `Func<…,bool>?` handlers plus wrappers that return a status string, as in `DebugHooks`).
      The world registers the handlers on start and clears them on destroy with a `ReferenceEquals` check.
- [ ] Apply `bossJumpPhase`, `bossStartHpPercent`, `bossGodMode` and `bossSkipCutscenes` only under `#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`.
- [ ] Add a `DebugPrinter` line `"Boss"` (phase, HP, pickups, current attack) behind `enableDebugPrinter`.
- [ ] Add a `[SaveBeforeInvoking] ClearBossBattleSave()` row.

**Localization**
- [ ] Add `Scripts/Ninjago/Editor/BossBattleLocalizationSeeder.cs`. Its `rows` use the prefix `ninjago.boss.*` plus `ninjago.mode.boss_battle`, and
      `Run() => NinjagoLocalizationSeeder.Upsert(rows)`. The Boss builder calls it **before** binding.
- [ ] Suggested keys (English, with smart = has `{}`):
  - Mode name: `ninjago.mode.boss_battle` "Boss Battle".
  - Start view: `ninjago.boss.continue` "Continue", `ninjago.boss.new` "New battle", `ninjago.boss.fresh` "Defeat the giant Boss!", `ninjago.boss.progress` "Phase {phase}    Boss HP {hp}%".
  - Phase banners: `ninjago.boss.phase` "PHASE {phase}", `ninjago.boss.hp` "BOSS".
  - Pickups: `ninjago.boss.rockets` "ROCKETS x{count}", `ninjago.boss.pickup_hint` "Grab the power-ups!".
  - Rock: `ninjago.boss.rock` "ROCK INCOMING!", `ninjago.boss.rock_hint` "Lean out of the shadow!".
  - Minion: `ninjago.boss.minion_left` "MINION ON THE LEFT!", `ninjago.boss.minion_right` "MINION ON THE RIGHT!", `ninjago.boss.minion_hint` "Lean into the wall to knock it off!".
  - Slam and wind: `ninjago.boss.slam` "GROUND SLAM!", `ninjago.boss.wind` "DEVIL WIND!".
  - Phase 2 transform: `ninjago.boss.transform` "Car to plane!", `ninjago.boss.to_sky` "After it! Into the sky!".
  - Tired, wake and clap: `ninjago.boss.tired` "THE BOSS IS TIRED!", `ninjago.boss.laser_hint` "Fly in front of the Boss!", `ninjago.boss.wake` "IT WOKE UP!", `ninjago.boss.clap` "BOSS CLAP!".
  - Result: `ninjago.boss.result_win` "Boss defeated!", `ninjago.boss.result_lose` "The Boss got away!", `ninjago.boss.result_line` "Time {time:0.0} s    Hits {hits}    Rockets {rockets}", `ninjago.boss.result_best` "Best    {time:0.0} s".
  - Reuse `ninjago.result.new_best`, `ninjago.result.retry`, `ninjago.result.menu` and `ninjago.result.chase_steer`.
- [ ] Bind keys with `SetLocalized` and `Label(…, key)`. Keep smart labels with `key: null` and set them with `LocalizedStrings.WithArguments` at runtime. Show HP and timer numbers with a plain `TMP_Text`.
- [ ] Pixel font: set `BilliardPixel_TMP` and `BilliardPixelBold_TMP` (sizes 16·n) on the Boss HUD labels. If CJK rows are added, use the multi-locale writer (§3.3) and re-run `FontAssetsBuilder.Run()`.
- [ ] Register the mode with `Mode(..., GameModeType.BossBattle, path, "ninjago.mode.boss_battle")`, add it to `WriteMenuOrder`, and decide `maxPlayers`.

**Analytics**
- [ ] Add `Scripts/Ninjago/Analytics/BossBattleLog.cs` (instance class, `Props()` with run, phase and HP context, the events in §4.3, and cached enum names).
- [ ] Call `TrackGameStart` once per attempt, with `"new"` or `"continue"`. Register `run_id` and `is_continue` as PLAY session properties after the start. Pair each start with one `MiniGameEnd`.
- [ ] Give each new view an `AnalyticsScreenName` (kebab-case). Call `NinjagoAnalytics.UiAction(AnalyticsScreenName, "<button>")` on every button and on Back, before acting.
- [ ] Send no per-frame events. Verify with Editor logs filtered on `[Analytics] EVENT: boss_` and `[Analytics] SCREEN: boss-`.
