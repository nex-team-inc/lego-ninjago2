#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Forge vocabulary every mode is made of: what each block type is (its station prefab, start and end states,
    /// gestures, presets, two-player share), what each vehicle ends at, the themes, and the link library. The chapter
    /// runtime, the validator, the generator and the Forge view all read block types only from here, so a fifth type
    /// is registered by adding its entry.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeCatalog", menuName = "Nex/Ninjago/Chapter/Forge Catalog")]
    public class ForgeCatalog : ScriptableObject
    {
        [Serializable]
        public class BlockSpec
        {
            public LocalizedString displayName = new();
            [Tooltip("Strip icon in the HUD and the Forge.")]
            public Sprite? icon;
            public Color color = Color.white;
            [Tooltip("The station placed on the level route for an entry of this type.")]
            public ChapterBlock? prefab;
            public LinkState startStates;
            public LinkState endStates;
            [Tooltip("Added to the start states when the entry has the Boss flag.")]
            public LinkState bossStartStates;
            [Tooltip("Added to the end states when the entry has the Boss flag.")]
            public LinkState bossEndStates;
            [Tooltip("Fight types can be mixed into one fight entry with a pattern.")]
            public bool isFight;
            [Tooltip("The pattern letter of this fight type, e.g. K or S.")]
            public string patternLetter = "";
            [Tooltip("Entries pick a ride (Car, Skycraft, Car then Skycraft); the last vehicle narrows the end states.")]
            public bool usesVehicle;
            [Tooltip("Seen side-on, like a 2D platformer; links into and out of it swing the camera.")]
            public bool sideView;
            [Tooltip("Off for types the Boss level may not use.")]
            public bool allowedInBossLevel = true;
            public TwoPlayerShare twoPlayers;
            [Tooltip("Gestures this type asks for, in the order they first appear.")]
            public List<Gesture> gestures = new();
            [Tooltip("Seconds a new entry aims to last.")]
            public float defaultSeconds = 60f;
            public EnumDictionary<DifficultyPreset, BlockDifficulty> presets = new();
        }

        [Serializable]
        public class ThemeSpec
        {
            public LocalizedString displayName = new();
            [Tooltip("The level's track when a level picks this theme.")]
            public BgmManager.BgmType music = BgmManager.BgmType.NinjaFight;
            public Color sky = new(0.5f, 0.78f, 1f);
            public Color ground = new(0.45f, 0.7f, 0.35f);
            public Color path = new(0.86f, 0.78f, 0.62f);
            public Color wall = new(0.86f, 0.32f, 0.22f);
            public Color roof = new(0.12f, 0.62f, 0.5f);
            public Color accent = new(1f, 0.8f, 0.3f);
            public Color foliage = new(0.15f, 0.6f, 0.25f);
            public Color fog = new(0.7f, 0.8f, 0.9f);
            [Range(0f, 0.05f)] public float fogDensity = 0.008f;
            public Color light = new(1f, 0.96f, 0.88f);
            [Range(0f, 3f)] public float lightIntensity = 1.25f;
            [Tooltip("Sun pitch and yaw in degrees; low pitch reads as dusk.")]
            public Vector2 lightAngles = new(50f, -30f);
            public Color ambient = new(0.55f, 0.6f, 0.7f);
            [Tooltip("Emissive lanterns read best at night.")]
            public bool lanternsGlow;
        }

        [Serializable]
        public class LinkSpec
        {
            public LinkMoment moment;
            public CameraMove cameraMove;
            [Tooltip("Localization keys of story beats; auto links take them in turn.")]
            public List<string> storyKeys = new();
            public BgmManager.StingerType stinger;
        }

        [Serializable]
        public class NaturalLink
        {
            [Tooltip("One state.")]
            public LinkState state;
            public LinkSpec spec = new();
        }

        [Header("Block Types")]
        [SerializeField] EnumDictionary<BlockType, BlockSpec> blocks = new();
        [Header("Vehicle End States")]
        [Tooltip("A ride ends where its last vehicle can stop.")]
        [SerializeField] EnumDictionary<VehicleType, LinkState> vehicleEndStates = new();
        [Header("Hint Words")]
        [Tooltip("The one-word hint shown in the world the first time a gesture appears.")]
        [SerializeField] EnumDictionary<Gesture, LocalizedString> hintWords = new();
        [Header("Themes")]
        [SerializeField] EnumDictionary<ChapterTheme, ThemeSpec> themes = new();
        [Header("Mode Icons")]
        [SerializeField] EnumDictionary<ModeIcon, Sprite?> icons = new();
        [Header("Natural Links")]
        [Tooltip("Preferred in this order when a block's end meets the next block's start in more than one state.")]
        [SerializeField] List<NaturalLink> naturalLinks = new();
        [Header("Brick Rebuild")]
        [Tooltip("Defeated enemies burst into bricks that fly together into the next threat or vehicle; links any two blocks.")]
        [SerializeField] LinkSpec brickRebuild = new();

        #region Public API

        public IReadOnlyList<BlockType> BlockTypes => EnumDictionary<BlockType, BlockSpec>.allKeys;
        public BlockSpec Block(BlockType type) => blocks[type];
        public LocalizedString HintWord(Gesture gesture) => hintWords[gesture];
        public ThemeSpec Theme(ChapterTheme theme) => themes[theme];
        public Sprite? Icon(ModeIcon icon) => icons[icon];
        public LinkSpec BrickRebuild => brickRebuild;
        public IReadOnlyList<NaturalLink> NaturalLinks => naturalLinks;

        public LinkSpec? NaturalLinkFor(LinkState state)
        {
            foreach (var link in naturalLinks)
            {
                if (link.state == state) return link.spec;
            }

            return null;
        }

        /// <summary>Every type this entry fights with: its own and its mix.</summary>
        public void TypesOf(BlockEntry entry, List<BlockType> result)
        {
            result.Clear();
            result.Add(entry.type);
            foreach (var type in entry.mix)
            {
                if (!result.Contains(type)) result.Add(type);
            }
        }

        public LinkState StartStates(BlockEntry entry)
        {
            var states = LinkState.None;
            foreach (var type in Types(entry))
            {
                var spec = blocks[type];
                states |= spec.startStates | (entry.boss ? spec.bossStartStates : LinkState.None);
            }

            return states;
        }

        public LinkState EndStates(BlockEntry entry)
        {
            var states = LinkState.None;
            foreach (var type in Types(entry))
            {
                var spec = blocks[type];
                var ends = spec.endStates | (entry.boss ? spec.bossEndStates : LinkState.None);
                if (spec.usesVehicle) ends &= vehicleEndStates[entry.ride.LastVehicle()] | spec.bossEndStates;
                states |= ends;
            }

            return states;
        }

        public bool UsesVehicle(BlockEntry entry) => blocks[entry.type].usesVehicle;

        public bool IsSideView(BlockEntry entry) => blocks[entry.type].sideView;

        /// <summary>Gestures this entry asks for, in first-appearance order.</summary>
        public void GesturesOf(BlockEntry entry, List<Gesture> result)
        {
            result.Clear();
            foreach (var type in Types(entry))
            {
                foreach (var gesture in blocks[type].gestures)
                {
                    if (!result.Contains(gesture)) result.Add(gesture);
                }
            }
        }

        public BlockEntry NewEntry(BlockType type, DifficultyPreset preset)
        {
            var spec = blocks[type];
            return new BlockEntry
            {
                type = type,
                preset = preset,
                difficulty = spec.presets[preset].Copy(),
                targetSeconds = spec.defaultSeconds,
            };
        }

        #endregion

        #region Helpers

        readonly List<BlockType> typesBuffer = new();

        List<BlockType> Types(BlockEntry entry)
        {
            TypesOf(entry, typesBuffer);
            return typesBuffer;
        }

        #endregion
    }
}
