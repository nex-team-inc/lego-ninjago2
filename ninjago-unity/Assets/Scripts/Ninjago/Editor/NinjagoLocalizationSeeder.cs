#nullable enable

using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Upserts every ninjago.* line into the LocalizationTable collection (English first pass). Other locales have
    /// no entry yet and fall back to English, so the string database fallback is switched on.
    /// </summary>
    public static class NinjagoLocalizationSeeder
    {
        // (key, English, smart)
        static readonly (string key, string english, bool smart)[] rows =
        {
            ("ninjago.mode.slip_and_spin", "Slip and Spin", false),
            ("ninjago.mode.chest_chase", "Chest Chase", false),
            ("ninjago.players.title", "How many players?", false),
            ("ninjago.players.one", "1 Player", false),
            ("ninjago.players.two", "2 Players", false),
            ("ninjago.player.tag", "P{player}", true),
            ("ninjago.setup.stand", "Stand in the frame", false),
            ("ninjago.setup.hold", "Hold still", false),
            ("ninjago.setup.ready", "Ready!", false),
            ("ninjago.setup.solo", "Only one player found. Starting solo!", false),
            ("ninjago.setup.status_waiting", "Step in", false),
            ("ninjago.setup.status_holding", "Hold still", false),
            ("ninjago.setup.status_ready", "Ready!", false),
            ("ninjago.fight.get_ready", "Get ready!", false),
            ("ninjago.fight.slip", "SLIP", false),
            ("ninjago.fight.spin_hint", "Move your hands!", false),
            ("ninjago.fight.sweep", "Sweep {current}/{total}", true),
            ("ninjago.fight.out", "OUT", false),
            ("ninjago.fight.finished", "Finished!", false),
            ("ninjago.chase.car", "Car", false),
            ("ninjago.chase.skycraft", "Skycraft", false),
            ("ninjago.chase.lean_to_steer", "Lean to steer", false),
            ("ninjago.chase.whole_chest", "Use your whole chest", false),
            ("ninjago.chase.dodges", "Dodges {count}", true),
            ("ninjago.chase.hits", "Hits {count}", true),
            ("ninjago.result.victory", "Victory!", false),
            ("ninjago.result.defeat", "Defeated", false),
            ("ninjago.result.chase_title", "Ride complete!", false),
            ("ninjago.result.fight_player", "P{player}    Slips {slips}/{sweeps}    Fight-backs {backs}/{sweeps}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.fight_best", "Best    Slips {slips}    Fight-backs {backs}    Hearts {hearts}", true),
            ("ninjago.result.chase_car", "Car    Dodges {dodges}    Hits {hits}", true),
            ("ninjago.result.chase_sky", "Skycraft    Dodges {dodges}    Hits {hits}", true),
            ("ninjago.result.chase_steer", "P{player} steered {share}%", true),
            ("ninjago.result.chase_best", "Best    Car dodges {car}    Sky dodges {sky}", true),
            ("ninjago.result.new_best", "New best!", false),
            ("ninjago.result.retry", "Retry", false),
            ("ninjago.result.menu", "Menu", false),
            ("ninjago.mode.stone_kick", "Stone Kick", false),
            ("ninjago.mode.earth_seal", "Earth Seal", false),
            ("ninjago.setup.hands", "Raise a hand into your circle", false),
            ("ninjago.kick.kick", "KICK", false),
            ("ninjago.kick.stones", "x{count}", true),
            ("ninjago.kick.slash_count", "SLASH x{count}", true),
            ("ninjago.kick.throw", "Throw {current}/{total}", true),
            ("ninjago.kick.slash_hint", "Slash it like crazy!", false),
            ("ninjago.seal.wave", "Wave {current}/{total}", true),
            ("ninjago.seal.wave_banner", "Wave {current}", true),
            ("ninjago.seal.hold_hint", "Hold a hand on the cracks!", false),
            ("ninjago.result.kick_player", "P{player}    Slashes {slashes}    Returns {returns}/{throws}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.kick_stones", "P{player}    Stones returned {stones}/{cut}    Kick time {time:0.00} s", true),
            ("ninjago.result.kick_stones_no_kick", "P{player}    Stones returned {stones}/{cut}    Kick time -", true),
            ("ninjago.result.kick_best", "Best    Slashes {slashes}    Returns {returns}    Stones {stones}", true),
            ("ninjago.result.seal_held", "The wall held!", false),
            ("ninjago.result.seal_broke", "The wall broke!", false),
            ("ninjago.result.seal_wall", "Seals {seals}    Breakthroughs {breaks}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.seal_team", "Two-hand seals {twoHands}    Dropped seals {drops}", true),
            ("ninjago.result.seal_player", "P{player}    Held {held} seals    On cracks {seconds:0.0} s    Dropped {drops}", true),
            ("ninjago.result.seal_best", "Best    Seals {seals}    Two-hand seals {twoHands}", true),
            ("ninjago.mode.robot_mecha", "Robot Mecha", false),
            ("ninjago.mecha.robot_arms", "ROBOT ARMS", false),
            ("ninjago.mecha.setup_hold", "Make an L with both arms and hold it", false),
            ("ninjago.mecha.setup_hint", "Elbows down, forearms up in front, like a robot!", false),
            ("ninjago.mecha.time_left", "{seconds}", true),
            ("ninjago.mecha.kills", "Kills {count}", true),
            ("ninjago.mecha.down", "DOWN", false),
            ("ninjago.mecha.survived", "SURVIVED!", false),
            ("ninjago.result.mecha_survived", "Wave survived!", false),
            ("ninjago.result.mecha_down", "Mecha down!", false),
            ("ninjago.result.mecha_player", "P{player}    Time {time:0.0} s    Kills {kills}    Hearts lost {lost}    Pose {pose}%", true),
            ("ninjago.result.mecha_best", "Best    Time {time:0.0} s    Pose {pose}%", true),
            ("ninjago.mode.crouch_run", "Crouch Run", false),
            ("ninjago.crouch.crouch", "CROUCH", false),
            ("ninjago.crouch.heard", "!", false),
            ("ninjago.crouch.level", "LEVEL {level}", true),
            ("ninjago.crouch.clock", "{seconds}", true),
            ("ninjago.crouch.heard_count", "Seen {count}", true),
            ("ninjago.crouch.cleared", "CLEARED!", false),
            ("ninjago.crouch.time_up", "TIME UP", false),
            ("ninjago.result.crouch_all_clear", "All three levels cleared!", false),
            ("ninjago.result.crouch_failed", "Level failed", false),
            ("ninjago.result.crouch_cleared_line", "P{player}    Level {level}    Time left {time:0.0} s    Seen {heard}", true),
            ("ninjago.result.crouch_failed_line", "P{player}    Level {level} failed    Seen {heard}", true),
            ("ninjago.result.crouch_best", "Best    Level {level}    Time left {time:0.0} s", true),
            ("ninjago.mode.three_weapons", "Three Weapons", false),
            ("ninjago.weapons.sword", "SWORD", false),
            ("ninjago.weapons.nunchucks", "NUNCHUCKS", false),
            ("ninjago.weapons.shuriken", "SHURIKEN", false),
            ("ninjago.weapons.slash", "SLASH", false),
            ("ninjago.weapons.spin", "SPIN", false),
            ("ninjago.weapons.throw", "THROW", false),
            ("ninjago.weapons.pick", "PICK A WEAPON", false),
            ("ninjago.weapons.fight", "FIGHT {current}/{total}", true),
            ("ninjago.weapons.cleared", "GAUNTLET CLEARED!", false),
            ("ninjago.result.weapons_title", "Gauntlet cleared!", false),
            ("ninjago.result.weapons_order", "P{player}    {first}  >  {second}  >  {third}    {time:0.0} s", true),
            ("ninjago.result.weapons_counts", "P{player}    Slashes {slashes}    Spin bursts {spins}    Throws {throws}", true),
            ("ninjago.result.weapons_best", "Best clear    {time:0.0} s", true),
            ("ninjago.setup.joints", "Step back: show both hands and your hips", false),
            ("ninjago.mode.one_punch", "One Punch", false),
            ("ninjago.punch.wave", "Wave {current}/{total}    {left} left", true),
            ("ninjago.punch.wave_banner", "WAVE {wave}", true),
            ("ninjago.punch.wave_cleared", "WAVE CLEARED!", false),
            ("ninjago.punch.street_cleared", "STREET CLEARED!", false),
            ("ninjago.punch.down", "PINNED DOWN!", false),
            ("ninjago.punch.hint", "Short punch forward!", false),
            ("ninjago.punch.wiggle", "WIGGLE YOUR HIPS!", false),
            ("ninjago.punch.counts", "Ext {ext}   Cam {cam}   Slash {slash}   None {none}", true),
            ("ninjago.punch.signal_extension", "EXTENSION!", false),
            ("ninjago.punch.signal_toward_camera", "TOWARD CAMERA!", false),
            ("ninjago.punch.signal_side_slash", "SIDE SLASH - NO", false),
            ("ninjago.punch.signal_none", "NOTHING", false),
            ("ninjago.result.punch_cleared", "Street cleared!", false),
            ("ninjago.result.punch_down", "Pinned down!", false),
            ("ninjago.result.punch_tries", "P{player}    Extension {ext}    Toward camera {cam}    Side slash rejected {slash}    Nothing {none}", true),
            ("ninjago.result.punch_player", "P{player}    Thrown {thrown}    Pinned {pinned}    Wiggled free {wiggles}    Health {health}/{max}", true),
            ("ninjago.result.punch_waves_cue", "Waves cleared {waves}/{total}    Toward-camera cue: hand scale", true),
            ("ninjago.result.punch_waves_no_cue", "Waves cleared {waves}/{total}    Toward-camera cue: none (no depth cue)", true),
            ("ninjago.result.punch_best", "Best {waves} waves    All runs: Extension {ext}   Toward camera {cam}   Side slash {slash}   Nothing {none}", true),
            ("ninjago.mode.guard", "Guard", false),
            ("ninjago.guard.status", "Throw {current}/{total}", true),
            ("ninjago.guard.intro", "Keep your friend safe!", false),
            ("ninjago.guard.hint", "Both hands up, like a shield!", false),
            ("ninjago.guard.block", "BLOCK!", false),
            ("ninjago.guard.team_block", "BLOCK TOGETHER!", false),
            ("ninjago.guard.team_banner", "TEAM BLOCK!", false),
            ("ninjago.guard.end", "Your friend is safe!", false),
            ("ninjago.guard.count", "Blocks {count}", true),
            ("ninjago.guard.saved", "BLOCKED!", false),
            ("ninjago.guard.late", "Too late!", false),
            ("ninjago.guard.held", "Down, then up again!", false),
            ("ninjago.guard.half", "Both hands!", false),
            ("ninjago.guard.missed", "Bonk!", false),
            ("ninjago.result.guard_good", "Great guarding!", false),
            ("ninjago.result.guard_again", "Let's guard again!", false),
            ("ninjago.result.guard_outcomes", "P{player}    Blocked {saved}    Too late {late}    Held too long {held}    One hand {half}    Bonked {missed}", true),
            ("ninjago.result.guard_reading", "P{player}    Guard read on time {matched} of {tries} tries    Read with nothing coming {alone}", true),
            ("ninjago.result.guard_big", "Giant bricks blocked {saved}/{total}", true),
            ("ninjago.result.guard_best", "Best {saves} blocks", true),
        };

        #region Entry Point

        public static string Run()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(NinjagoEditorUtils.TableName);
            var shared = collection.SharedData;
            var english = (StringTable)collection.GetTable("en");
            foreach (var (key, value, smart) in rows)
            {
                var sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);
                var entry = english.GetEntry(sharedEntry.Id) ?? english.AddEntry(sharedEntry.Id, value);
                if (entry.Value != value) entry.Value = value;
                if (entry.IsSmart != smart) entry.IsSmart = smart;
            }

            var database = LocalizationEditorSettings.ActiveLocalizationSettings.GetStringDatabase();
            database.UseFallback = true;
            EditorUtility.SetDirty(LocalizationEditorSettings.ActiveLocalizationSettings);
            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(english);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
            return $"localization {rows.Length} keys";
        }

        #endregion
    }
}
