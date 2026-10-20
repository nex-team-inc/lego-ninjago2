#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Scripting;

namespace Nex.Ninjago
{
    // The Forge language. Authored assets (ModeDefinition, ChapterDefinition, LevelDefinition) and player-made modes
    // saved with Easy Save share these classes. Public fields only; append new fields, never rename.

    [Serializable, Preserve]
    public class BlockDifficulty
    {
        [Tooltip("Reaction windows (tells, hangs, slip, kick and counter windows) times this; below 1 is tighter.")]
        public float windowScale = 1f;
        [Tooltip("Enemy tells, vehicle cruise and obstacle pace times this.")]
        public float speedScale = 1f;
        [Tooltip("Threats in a fight, obstacles per ten seconds in a ride, talking zones in stealth.")]
        public int count = 6;
        [Tooltip("Stealth: meters of each talking zone.")]
        public float zoneLength = 16f;
        [Tooltip("Fights: every Nth threat overlaps the next one lightly (0 = never).")]
        public int overlapEvery;

        /// <summary>One number to compare levels: faster and tighter is harder.</summary>
        public float Intensity => speedScale / Mathf.Max(0.1f, windowScale);

        public BlockDifficulty Copy() => (BlockDifficulty)MemberwiseClone();
    }

    [Serializable, Preserve]
    public class BlockEntry
    {
        public BlockType type;
        [Tooltip("Fight entries only: other fight types mixed in with the pattern. This is how Mixed is made; it is not a fifth type.")]
        public List<BlockType> mix = new();
        [Tooltip("Fight entries with a mix: one pattern letter per threat (K slash and kick, S slip and spin), '+' joins two threats that overlap. Empty repeats the types in turn.")]
        public string pattern = "";
        [Tooltip("Use Theme below instead of the level's theme.")]
        public bool overrideTheme;
        public ChapterTheme theme;
        [Tooltip("The preset last picked; the fine values below are what plays.")]
        public DifficultyPreset preset = DifficultyPreset.Normal;
        public BlockDifficulty difficulty = new();
        [Tooltip("Seconds this block aims to last (rides last exactly this; stealth uses it as the timer).")]
        public float targetSeconds = 60f;
        [Tooltip("Runner only.")]
        public RunnerRide ride;
        public HintPolicy hints;
        [Tooltip("Swaps in the Boss: fights face it, a ride chases it and it always escapes.")]
        public bool boss;

        public BlockEntry Copy()
        {
            var copy = (BlockEntry)MemberwiseClone();
            copy.mix = new List<BlockType>(mix);
            copy.difficulty = difficulty.Copy();
            return copy;
        }
    }

    [Serializable, Preserve]
    public class LinkEntry
    {
        [Tooltip("Filled in from the end and start states. Turn off to keep hand edits.")]
        public bool auto = true;
        [Tooltip("The shared state this link uses. None is the brick rebuild, which links any two blocks.")]
        public LinkState state;
        public LinkMoment moment;
        public CameraMove cameraMove;
        [Tooltip("Localization key of the one-line story beat.")]
        public string storyKey = "";
        [Tooltip("Stinger over the level's music; the track itself never restarts.")]
        public BgmManager.StingerType stinger;

        public LinkEntry Copy() => (LinkEntry)MemberwiseClone();
    }

    [Serializable, Preserve]
    public class LevelData
    {
        public ChapterTheme theme;
        [Tooltip("One track for the whole level.")]
        public BgmManager.BgmType music = BgmManager.BgmType.NinjaFight;
        [Tooltip("Hearts each player starts the level with.")]
        public int hearts = 5;
        public List<BlockEntry> blocks = new();
        [Tooltip("Link i joins block i and block i + 1.")]
        public List<LinkEntry> links = new();

        public bool IsBoss
        {
            get
            {
                foreach (var block in blocks)
                {
                    if (block.boss) return true;
                }

                return false;
            }
        }

        public float TargetSeconds(float linkSeconds)
        {
            var seconds = Mathf.Max(0, blocks.Count - 1) * linkSeconds;
            foreach (var block in blocks) seconds += block.targetSeconds;
            return seconds;
        }

        public ChapterTheme ThemeOf(BlockEntry block) => block.overrideTheme ? block.theme : theme;

        public LevelData Copy()
        {
            var copy = (LevelData)MemberwiseClone();
            copy.blocks = new List<BlockEntry>(blocks.Count);
            foreach (var block in blocks) copy.blocks.Add(block.Copy());
            copy.links = new List<LinkEntry>(links.Count);
            foreach (var link in links) copy.links.Add(link.Copy());
            return copy;
        }
    }

    [Serializable, Preserve]
    public class StarRules
    {
        [Tooltip("Finishing the level earns one star.")]
        public bool starForFinish = true;
        [Tooltip("A star when the hearts left (the lowest player) are at least this share of the level's hearts.")]
        public float heartsShareForStar = 0.6f;
        [Tooltip("A star when no block had to be tried again.")]
        public bool starForNoRetry = true;

        public StarRules Copy() => (StarRules)MemberwiseClone();
    }

    [Serializable, Preserve]
    public class RestRules
    {
        [Tooltip("After confirm, check the active players are still in frame and calibrate again only if tracking was lost.")]
        public bool checkTracking = true;
        [Tooltip("Seconds the check looks for every active player.")]
        public float trackingCheckSeconds = 1.5f;

        public RestRules Copy() => (RestRules)MemberwiseClone();
    }

    [Serializable, Preserve]
    public class ChapterData
    {
        [Tooltip("In play order; the Boss level is last.")]
        public List<LevelData> levels = new();
        public StarRules stars = new();
        public RestRules rest = new();

        public ChapterData Copy()
        {
            var copy = new ChapterData { stars = stars.Copy(), rest = rest.Copy() };
            foreach (var level in levels) copy.levels.Add(level.Copy());
            return copy;
        }
    }

    /// <summary>A built-in mode shows its asset's title; a player-made mode shows generated words (and a seed for Surprise me).</summary>
    [Serializable, Preserve]
    public class ModeName
    {
        [Tooltip("Indexes into ForgeConfig's name word lists (place, time, action).")]
        public List<int> words = new();
        [Tooltip("Shown after the words for generated chapters; -1 for none.")]
        public int seed = -1;
        /// <summary>Built-in modes only; never saved.</summary>
        [NonSerialized] public LocalizedString? title;

        public ModeName Copy() => new() { words = new List<int>(words), seed = seed, title = title };
    }

    [Serializable, Preserve]
    public class ModeData
    {
        public string id = "";
        public ModeName name = new();
        public ModeIcon icon;
        public bool builtIn;
        public ChapterData chapter = new();

        /// <summary>A player-made copy with a new id; built-in copies get the given generated name.</summary>
        public ModeData CopyAsPlayerMade(string newId, ModeName newName) => new()
        {
            id = newId,
            name = newName,
            icon = icon,
            builtIn = false,
            chapter = chapter.Copy(),
        };
    }
}
