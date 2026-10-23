#nullable enable

using System;

namespace Nex.Ninjago
{
    /// <summary>
    /// Registration points for driving a live Boss Battle from the Debug Settings rows and the CLI
    /// (`unity command eval 'return Nex.Ninjago.BossBattleDebugHooks.SkipToSky();'`). The running game registers its
    /// handlers with itself as owner and unregisters on teardown (cleared only when the owner matches, ReferenceEquals),
    /// and publishes its status for waits and the DebugPrinter "Boss" line. Wrappers return "X: ok" / "rejected" /
    /// "not registered"; release builds answer "disabled".
    /// </summary>
    public static class BossBattleDebugHooks
    {
        const string PrinterKey = "Boss";

        static object? owner;
        static Func<bool>? skipToSky;
        static Func<float, bool>? setBossHpPercent;
        static Func<bool>? grantPowerUp;
        static Func<int, bool>? forceBeat;
        static Func<bool>? winNow;
        static Func<bool>? knockOutNow;
        static Func<string>? status;

        #region Published State (read-only, for CLI waits)

        /// <summary>True while a battle is registered.</summary>
        public static bool IsLive => owner != null;
        public static BossPhase CurrentPhase { get; private set; }
        public static BossBeat CurrentBeat { get; private set; }
        /// <summary>Script index of the beat being played.</summary>
        public static int CurrentBeatIndex { get; private set; } = -1;
        public static float BossHpPercent { get; private set; } = 100f;
        public static int Hearts { get; private set; }
        public static int Pickups { get; private set; }

        #endregion

        #region Registration

        public static void Register(object registrant, Func<bool> onSkipToSky, Func<float, bool> onSetBossHpPercent, Func<bool> onGrantPowerUp,
            Func<int, bool> onForceBeat, Func<bool> onWinNow, Func<bool> onKnockOutNow, Func<string> onStatus)
        {
            owner = registrant;
            skipToSky = onSkipToSky;
            setBossHpPercent = onSetBossHpPercent;
            grantPowerUp = onGrantPowerUp;
            forceBeat = onForceBeat;
            winNow = onWinNow;
            knockOutNow = onKnockOutNow;
            status = onStatus;
        }

        /// <summary>Clears every handler if this registrant still owns them (a newer game may have registered since).</summary>
        public static void Unregister(object registrant)
        {
            if (!ReferenceEquals(owner, registrant)) return;
            owner = null;
            skipToSky = null;
            setBossHpPercent = null;
            grantPowerUp = null;
            forceBeat = null;
            winNow = null;
            knockOutNow = null;
            status = null;
            CurrentBeatIndex = -1;
            ClearPrinter();
        }

        /// <summary>The game's current status (call when phase, beat, HP, hearts or pickups change; no per-frame cost).</summary>
        public static void Publish(BossPhase phase, BossBeat beat, int beatIndex, float bossHpPercent, int hearts, int pickups)
        {
            CurrentPhase = phase;
            CurrentBeat = beat;
            CurrentBeatIndex = beatIndex;
            BossHpPercent = bossHpPercent;
            Hearts = hearts;
            Pickups = pickups;
            PrintLine();
        }

        #endregion

        #region Wrappers

        public static string SkipToSky() => Run(nameof(SkipToSky), skipToSky);
        /// <summary>Sets the current phase's boss bar (0..100).</summary>
        public static string SetBossHpPercent(float percent) => Run(nameof(SetBossHpPercent), setBossHpPercent, percent);
        public static string GrantPowerUp() => Run(nameof(GrantPowerUp), grantPowerUp);
        /// <summary>Plays this BossBeat (int value) next.</summary>
        public static string ForceBeat(int beat) => Run(nameof(ForceBeat), forceBeat, beat);
        public static string WinNow() => Run(nameof(WinNow), winNow);
        public static string KnockOutNow() => Run(nameof(KnockOutNow), knockOutNow);

        public static string Status()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return status?.Invoke() ?? $"Status: not registered (phase {CurrentPhase}, beat {CurrentBeat})";
#else
            return "Status: disabled in release builds";
#endif
        }

        #endregion

        #region Helpers

        static string Run(string name, Func<bool>? handler)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return Report(name, handler?.Invoke());
#else
            return $"{name}: disabled in release builds";
#endif
        }

        static string Run<T>(string name, Func<T, bool>? handler, T argument)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return Report(name, handler?.Invoke(argument));
#else
            return $"{name}: disabled in release builds";
#endif
        }

        static string Report(string name, bool? accepted)
        {
            if (accepted == null) return $"{name}: not registered";
            return accepted.Value ? $"{name}: ok" : $"{name}: rejected";
        }

        // The DebugPrinter "Boss" line (phase / beat / hp / hearts / pickups), only while the printer is on.
        static void PrintLine()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            var data = PlayerDataManager.Instance;
            var printer = DebugPrinter.Instance;
            if (data == null || printer == null || !data.DebugSettings.enableDebugPrinter) return;
            printer.Print(PrinterKey, $"{CurrentPhase} beat {CurrentBeatIndex} {CurrentBeat} hp {BossHpPercent:0}% hearts {Hearts} pickups {Pickups}");
#endif
        }

        static void ClearPrinter()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            var data = PlayerDataManager.Instance;
            var printer = DebugPrinter.Instance;
            if (data == null || printer == null || !data.DebugSettings.enableDebugPrinter) return;
            printer.Print(PrinterKey, "");
#endif
        }

        #endregion
    }
}
