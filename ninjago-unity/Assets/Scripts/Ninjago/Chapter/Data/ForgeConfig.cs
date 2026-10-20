#nullable enable

using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Forge's own settings: the short generated mode names (no typing needed), the labels its view shows for the
    /// Forge language, the validator's lines and the Surprise me generator's ranges.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeConfig", menuName = "Nex/Ninjago/Chapter/Forge Config")]
    public class ForgeConfig : ScriptableObject
    {
        /// <summary>The string table every chapter and Forge line lives in.</summary>
        public const string TableName = "LocalizationTable";

        [Header("Name Words: Places")]
        [Tooltip("Localization keys; a generated name is place + time + action, e.g. Harbor Night Run.")]
        [SerializeField] List<string> placeWords = new();
        [Header("Name Words: Times")]
        [SerializeField] List<string> timeWords = new();
        [Header("Name Words: Actions")]
        [SerializeField] List<string> actionWords = new();
        [Header("Name Format")]
        [Tooltip("Smart string with {place} {time} {action}.")]
        [SerializeField] LocalizedString nameFormat = new();
        [Header("Seeded Name Format")]
        [Tooltip("Smart string with {name} {seed}, for generated chapters.")]
        [SerializeField] LocalizedString seededNameFormat = new();
        [Header("Level Name")]
        [Tooltip("Smart string with {level}.")]
        [SerializeField] LocalizedString levelName = new();
        [Header("Boss Level Name")]
        [SerializeField] LocalizedString bossLevelName = new();

        [Header("Ride Names")]
        [SerializeField] EnumDictionary<RunnerRide, LocalizedString> rideNames = new();
        [Header("Preset Names")]
        [SerializeField] EnumDictionary<DifficultyPreset, LocalizedString> presetNames = new();
        [Header("Hint Policy Names")]
        [SerializeField] EnumDictionary<HintPolicy, LocalizedString> hintNames = new();
        [Header("Link Moment Names")]
        [SerializeField] EnumDictionary<LinkMoment, LocalizedString> momentNames = new();
        [Header("Validator Lines")]
        [Tooltip("One plain sentence per rule; smart strings with {level} {block} {other} where they apply.")]
        [SerializeField] EnumDictionary<ForgeRule, LocalizedString> ruleLines = new();

        [Header("Generator: Normal Levels")]
        [Tooltip("Normal levels before the Boss level.")]
        [SerializeField] Vector2Int normalLevels = new(2, 3);
        [Header("Generator: Blocks Per Level")]
        [SerializeField] Vector2Int blocksPerLevel = new(4, 5);
        [Header("Generator: Boss Fight Phases")]
        [Tooltip("Fight phases before the Boss chase.")]
        [SerializeField] Vector2Int bossFightPhases = new(2, 3);
        [Header("Generator: Hearts")]
        [SerializeField, Range(1, 9)] int generatedHearts = 5;
        [Header("Generator: Level Music")]
        [Tooltip("Tracks the generator deals to normal levels in turn.")]
        [SerializeField] List<BgmManager.BgmType> levelMusic = new();
        [Header("Generator: Boss Music")]
        [SerializeField] BgmManager.BgmType bossMusic = BgmManager.BgmType.StoneKick;
        [Header("Generator: Mix Pattern")]
        [Tooltip("The Boss's mixed phase pattern in generated chapters.")]
        [SerializeField] string bossMixPattern = "K S K S K+S S K K+S";

        #region Public API

        public int PlaceCount => placeWords.Count;
        public int TimeCount => timeWords.Count;
        public int ActionCount => actionWords.Count;
        public LocalizedString LevelName(int levelNumber) => LocalizedStrings.WithArguments(levelName, ("level", levelNumber));
        public LocalizedString BossLevelName => bossLevelName;
        public LocalizedString RideName(RunnerRide ride) => rideNames[ride];
        public LocalizedString PresetName(DifficultyPreset preset) => presetNames[preset];
        public LocalizedString HintName(HintPolicy policy) => hintNames[policy];
        public LocalizedString MomentName(LinkMoment moment) => momentNames[moment];
        public LocalizedString RuleLine(ForgeRule rule) => ruleLines[rule];
        public Vector2Int NormalLevels => normalLevels;
        public Vector2Int BlocksPerLevel => blocksPerLevel;
        public Vector2Int BossFightPhases => bossFightPhases;
        public int GeneratedHearts => generatedHearts;
        public IReadOnlyList<BgmManager.BgmType> LevelMusic => levelMusic;
        public BgmManager.BgmType BossMusic => bossMusic;
        public string BossMixPattern => bossMixPattern;

        /// <summary>The mode's title: a built-in asset's title, or the generated words (and the seed for Surprise me).</summary>
        public LocalizedString NameOf(ModeName name)
        {
            if (name.title != null) return name.title;
            var words = LocalizedStrings.WithArguments(nameFormat,
                ("place", Word(placeWords, name, 0)),
                ("time", Word(timeWords, name, 1)),
                ("action", Word(actionWords, name, 2)));
            return name.seed < 0 ? words : LocalizedStrings.WithArguments(seededNameFormat, ("name", words.GetLocalizedString()), ("seed", name.seed));
        }

        /// <summary>A short generated name from the random source, so no typing is needed.</summary>
        public ModeName RandomName(System.Random random, int seed = -1) => new()
        {
            words = new List<int> { random.Next(placeWords.Count), random.Next(timeWords.Count), random.Next(actionWords.Count) },
            seed = seed,
        };

        #endregion

        #region Helpers

        static string Word(List<string> keys, ModeName name, int slot)
        {
            if (keys.Count == 0 || name.words.Count <= slot) return "";
            var key = keys[Mathf.Abs(name.words[slot]) % keys.Count];
            return new LocalizedString(TableName, key).GetLocalizedString();
        }

        #endregion
    }
}
