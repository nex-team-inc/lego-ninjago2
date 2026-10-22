#nullable enable

// CLI playtest driver for the Ninjago modes. NOT compiled by Unity (lives outside Assets/): run an entry with run_script,
// e.g. (helpers: source Tools/unity-cli-helpers.sh)
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.Status
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.PlayMode '["ChestChase", 1]' 90
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.WaitForView '["Runner", 60]' 90
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.Lean '[0, 6.0, 0.0, false]'
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.Chase
//   urun Tools/cli/NinjagoPlaytest.cs NinjagoPlaytest.FrameStats '[120]' 120
// Every entry returns a JSON-serializable object; failures come back as { ok = false, error }.
// Play mode must already be running from MainScene (uplay) for PlayMode / WaitForView / FrameStats.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Nex;
using Nex.Ninjago;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NinjagoPlaytest
{
    #region State

    /// <summary>Play state, active scene, top view, selected mode and the debug flags the CLI relies on.</summary>
    public static object Status()
    {
        var views = UnityEngine.Object.FindFirstObjectByType<ViewManager>();
        var top = TopView(views);
        var data = PlayerDataManager.Instance;
        var configs = GameConfigsManager.Instance;
        return new
        {
            playing = EditorApplication.isPlaying,
            scene = SceneManager.GetActiveScene().path,
            frame = Time.frameCount,
            time = Math.Round(Time.time, 2),
            timeScale = Time.timeScale,
            runInBackground = Application.runInBackground,
            focused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
            topView = top == null ? null : top.GetType().Name,
            topViewId = top == null ? null : top.Identifier.ToString(),
            inTransition = views != null && views.IsInTransition,
            selectedMode = configs == null ? null : configs.SelectedMode.ToString(),
            simulatedBody = data != null && data.DebugSettings.ninjaSimulatedBody,
            players = data == null ? 0 : data.PlayerPreference.numPlayers,
        };
    }

    /// <summary>Waits (real time) until the top view's ViewIdentifier name equals viewId and no transition runs.</summary>
    public static async Task<object> WaitForView(string viewId, double timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var views = UnityEngine.Object.FindFirstObjectByType<ViewManager>();
            var top = TopView(views);
            if (views != null && !views.IsInTransition && top != null && top.Identifier.ToString() == viewId) return new { ok = true, status = Status() };
            await Task.Delay(100);
        }

        return new { ok = false, error = $"Top view never became {viewId} within {timeoutSeconds}s.", status = Status() };
    }

    #endregion

    #region Flow

    /// <summary>
    /// From the running main menu: runInBackground on, Simulated Body on, numPlayers (in memory, not saved), then the
    /// same path as MainCoordinator.LoadGameSceneAsync: SelectedMode, ScreenBlockerManager.Show, load GameScene.
    /// mode is a GameModeType name (ChestChase, Chapter, ...; a new mode once it is in the enum and GameModeConfig).
    /// </summary>
    public static async Task<object> PlayMode(string mode, int players)
    {
        if (!EditorApplication.isPlaying) return new { ok = false, error = "Not in play mode: open MainScene and uplay first." };
        if (!Enum.TryParse<GameModeType>(mode, out var type) || type == GameModeType.None)
            return new { ok = false, error = $"Unknown GameModeType '{mode}'. Values: {string.Join(", ", Enum.GetNames(typeof(GameModeType)))}" };
        Application.runInBackground = true;
        var menu = await WaitForView(nameof(View.ViewIdentifier.GameModeSelection), 30);
        if (menu.GetType().GetProperty("ok")?.GetValue(menu) is false) return menu;

        var data = PlayerDataManager.Instance;
        data.DebugSettings.ninjaSimulatedBody = true;
        data.PlayerPreference.numPlayers = Mathf.Clamp(players, 1, SimulatedBody.MaxPlayers);
        for (var p = 0; p < SimulatedBody.MaxPlayers; p++) SimulatedBody.Set(p, 0f, 0f, false);
        var configs = GameConfigsManager.Instance;
        configs.SelectedMode = type;
        await ScreenBlockerManager.Instance.Show();
        SceneManager.LoadScene(configs.GameScene);
        return new { ok = true, mode = type.ToString(), players = data.PlayerPreference.numPlayers, scene = configs.GameScene };
    }

    /// <summary>
    /// Clicks a Button field of the top view by its field name (private [SerializeField] included), like a player:
    /// e.g. "retryButton" / "menuButton" on NinjagoResultView. Returns the field names that exist when it is missing.
    /// </summary>
    public static object PressButton(string fieldName)
    {
        var top = TopView(UnityEngine.Object.FindFirstObjectByType<ViewManager>());
        if (top == null) return new { ok = false, error = "No top view." };
        var fields = new List<string>();
        for (var type = top.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!typeof(UnityEngine.UI.Button).IsAssignableFrom(field.FieldType)) continue;
                fields.Add(field.Name);
                if (field.Name != fieldName) continue;
                if (field.GetValue(top) is not UnityEngine.UI.Button button || button == null) return new { ok = false, error = $"{fieldName} is not assigned." };
                button.onClick.Invoke();
                return new { ok = true, view = top.GetType().Name, pressed = fieldName };
            }
        }

        return new { ok = false, error = $"{top.GetType().Name} has no Button field '{fieldName}'.", buttons = fields };
    }

    /// <summary>Opens the Debug Settings view on top of whatever is showing (same as the secret code).</summary>
    public static object OpenDebugSettings()
    {
        var views = UnityEngine.Object.FindFirstObjectByType<ViewManager>();
        if (views == null) return new { ok = false, error = "No ViewManager." };
        var method = typeof(ViewManager).GetMethod("OpenDebugSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) return new { ok = false, error = "ViewManager.OpenDebugSettings not found." };
        method.Invoke(views, null);
        return new { ok = true };
    }

    #endregion

    #region Simulated Body

    /// <summary>Lean in body inches (x right, y up) and whether both hands storm; persists until changed.</summary>
    public static object Lean(int player, float x, float y, bool storm)
    {
        SimulatedBody.Set(player, x, y, storm);
        return new { player, x, y, storm };
    }

    public static object Kick(int player)
    {
        SimulatedBody.PulseKnee(player);
        return new { player, kneePulses = SimulatedBody.GetKneePulses(player) };
    }

    public static object Punch(int player, int hand)
    {
        SimulatedBody.PulsePunch(player, hand);
        return new { player, hand, punchPulses = SimulatedBody.GetPunchPulses(player, hand) };
    }

    #endregion

    #region Chase Readout

    /// <summary>The Chest Chase world: vehicle, time left, speed factor, dodges and hits, vehicle position.</summary>
    public static object Chase()
    {
        var world = UnityEngine.Object.FindFirstObjectByType<RunnerWorld>();
        if (world == null) return new { ok = false, error = "No RunnerWorld yet.", status = Status() };
        var position = world.Vehicle != null ? world.Vehicle.transform.localPosition : Vector3.zero;
        return new
        {
            ok = true,
            vehicle = world.CurrentVehicle.ToString(),
            timeLeft = Math.Round(world.TimeLeft, 2),
            speedFactor = Math.Round(world.SpeedFactor, 3),
            carDodges = world.GetDodges(VehicleType.Car),
            carHits = world.GetHits(VehicleType.Car),
            skyDodges = world.GetDodges(VehicleType.Skycraft),
            skyHits = world.GetHits(VehicleType.Skycraft),
            vehicleLocal = new { x = Math.Round(position.x, 2), y = Math.Round(position.y, 2), z = Math.Round(position.z, 2) },
        };
    }

    #endregion

    #region Performance

    /// <summary>
    /// Samples the next N player-loop frames: wall-clock frame ms (throttled to ~10 fps while the Editor is unfocused,
    /// so only meaningful when focused), PlayerLoop CPU ms (the game's own work, valid unfocused), and per-frame
    /// batches / SetPass / triangles / GC bytes. Editor numbers are not device numbers.
    /// </summary>
    public static async Task<object> FrameStats(int frames)
    {
        if (!EditorApplication.isPlaying) return new { ok = false, error = "Not in play mode." };
        var capacity = Math.Max(frames, 1) + 4;
        using var playerLoop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop", capacity);
        using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", capacity);
        using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", capacity);
        using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", capacity);
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", capacity);
        var wall = new List<double>();
        var loop = new List<double>();
        var batchList = new List<double>();
        var setPassList = new List<double>();
        var triangleList = new List<double>();
        var gcList = new List<double>();
        var lastFrame = Time.frameCount;
        var guard = DateTime.UtcNow.AddSeconds(Math.Max(30, frames));
        while (wall.Count < frames && DateTime.UtcNow < guard)
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            if (Time.frameCount == lastFrame) continue;
            lastFrame = Time.frameCount;
            wall.Add(Time.unscaledDeltaTime * 1000.0);
            loop.Add(playerLoop.LastValue / 1e6);
            batchList.Add(batches.LastValue);
            setPassList.Add(setPass.LastValue);
            triangleList.Add(triangles.LastValue);
            gcList.Add(gc.LastValue);
        }

        return new
        {
            ok = true,
            frames = wall.Count,
            focused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
            topView = TopView(UnityEngine.Object.FindFirstObjectByType<ViewManager>())?.Identifier.ToString(),
            wallMs = Summary(wall),
            framesOver16_7Ms = wall.Count(ms => ms > 16.7),
            playerLoopMs = Summary(loop),
            batches = Summary(batchList),
            setPass = Summary(setPassList),
            triangles = Summary(triangleList),
            gcBytesPerFrame = Summary(gcList),
        };
    }

    #endregion

    #region Helpers

    static View? TopView(ViewManager? views)
    {
        if (views == null) return null;
        try
        {
            return views.TopView;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    static object Summary(List<double> values)
    {
        if (values.Count == 0) return new { avg = 0.0, p50 = 0.0, p95 = 0.0, max = 0.0 };
        var sorted = values.OrderBy(v => v).ToList();
        double At(double q) => sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(q * (sorted.Count - 1) + 0.5))];
        return new { avg = Math.Round(values.Average(), 3), p50 = Math.Round(At(0.5), 3), p95 = Math.Round(At(0.95), 3), max = Math.Round(sorted[^1], 3) };
    }

    #endregion
}
