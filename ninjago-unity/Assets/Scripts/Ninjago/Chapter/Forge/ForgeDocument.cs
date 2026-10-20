#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The mode being made in the Forge view and every edit it allows: pick a level, add, remove and move blocks in
    /// its strip, cycle a block's type, ride, preset, fine values, hints, Boss and mix, pick the link after a block, and
    /// the level's theme. Links fill in after every edit; the validator runs live on the result.
    /// </summary>
    public sealed class ForgeDocument
    {
        public const int MaxLevels = 6;

        static readonly float[] windowSteps = { 0.7f, 0.8f, 0.9f, 1f, 1.2f, 1.4f, 1.6f };
        static readonly float[] speedSteps = { 0.8f, 0.9f, 1f, 1.1f, 1.2f, 1.3f };
        static readonly float[] zoneSteps = { 8f, 10f, 12f, 14f, 16f, 18f, 20f };

        readonly ForgeCatalog catalog;
        readonly List<ForgeProblem> problems = new();
        readonly List<LinkState> linkOptions = new();

        public ModeData Mode { get; }
        /// <summary>The player-made mode this edits (saving overwrites it), or empty for a new mode.</summary>
        public string EditingId { get; private set; }
        /// <summary>The Surprise me seed this came from, or -1.</summary>
        public int Seed { get; }
        public int Level { get; private set; }
        public int Block { get; private set; }
        public float LinkSeconds { get; }
        public IReadOnlyList<ForgeProblem> Problems => problems;

        public LevelData CurrentLevel => Mode.chapter.levels[Level];
        public BlockEntry? CurrentBlock => Block < CurrentLevel.blocks.Count ? CurrentLevel.blocks[Block] : null;

        public ForgeDocument(ModeData mode, string editingId, int seed, ForgeCatalog catalog, float linkSeconds)
        {
            Mode = mode;
            EditingId = editingId;
            Seed = seed;
            this.catalog = catalog;
            LinkSeconds = linkSeconds;
            if (Mode.chapter.levels.Count == 0) Mode.chapter.levels.Add(NewLevel());
            Refresh();
        }

        #region Levels

        public void SelectLevel(int delta)
        {
            Level = (Level + delta + Mode.chapter.levels.Count) % Mode.chapter.levels.Count;
            Block = Mathf.Min(Block, Mathf.Max(0, CurrentLevel.blocks.Count - 1));
        }

        public bool AddLevel()
        {
            if (Mode.chapter.levels.Count >= MaxLevels) return false;
            var level = NewLevel();
            level.theme = (ChapterTheme)((int)(CurrentLevel.theme + 1) % Enum.GetValues(typeof(ChapterTheme)).Length);
            Mode.chapter.levels.Insert(Level + 1, level);
            Level++;
            Block = 0;
            Refresh();
            return true;
        }

        public bool RemoveLevel()
        {
            if (Mode.chapter.levels.Count <= 1) return false;
            Mode.chapter.levels.RemoveAt(Level);
            Level = Mathf.Min(Level, Mode.chapter.levels.Count - 1);
            Block = 0;
            Refresh();
            return true;
        }

        public void CycleTheme()
        {
            var count = Enum.GetValues(typeof(ChapterTheme)).Length;
            CurrentLevel.theme = (ChapterTheme)(((int)CurrentLevel.theme + 1) % count);
            CurrentLevel.music = catalog.Theme(CurrentLevel.theme).music;
            Refresh();
        }

        #endregion

        #region Blocks

        public void SelectBlock(int slot)
        {
            Block = Mathf.Clamp(slot, 0, Mathf.Max(0, CurrentLevel.blocks.Count - 1));
        }

        /// <summary>Adds a block after the strip's end, of a type that does not repeat the one before it.</summary>
        public bool AddBlock()
        {
            var blocks = CurrentLevel.blocks;
            if (blocks.Count >= ChapterValidator.MaxBlocks) return false;
            var previous = blocks.Count > 0 ? blocks[^1].type : (BlockType?)null;
            var type = catalog.BlockTypes[0];
            foreach (var candidate in catalog.BlockTypes)
            {
                if (candidate == previous || catalog.Block(candidate).prefab == null) continue;
                type = candidate;
                break;
            }

            var entry = catalog.NewEntry(type, CurrentPreset());
            entry.hints = Level == 0 ? HintPolicy.FirstTime : HintPolicy.NewOnly;
            blocks.Add(entry);
            Block = blocks.Count - 1;
            Refresh();
            return true;
        }

        public bool RemoveBlock()
        {
            if (CurrentBlock == null) return false;
            CurrentLevel.blocks.RemoveAt(Block);
            Block = Mathf.Max(0, Mathf.Min(Block, CurrentLevel.blocks.Count - 1));
            Refresh();
            return true;
        }

        public bool MoveBlock(int delta)
        {
            var target = Block + delta;
            if (CurrentBlock == null || target < 0 || target >= CurrentLevel.blocks.Count) return false;
            (CurrentLevel.blocks[Block], CurrentLevel.blocks[target]) = (CurrentLevel.blocks[target], CurrentLevel.blocks[Block]);
            Block = target;
            Refresh();
            return true;
        }

        public void CycleType() => Edit(block =>
        {
            var types = catalog.BlockTypes;
            var next = types[(IndexOf(types, block.type) + 1) % types.Count];
            var fresh = catalog.NewEntry(next, block.preset);
            fresh.hints = block.hints;
            fresh.boss = block.boss && catalog.Block(next).allowedInBossLevel;
            CurrentLevel.blocks[Block] = fresh;
        });

        public void CycleRide() => Edit(block =>
        {
            if (catalog.UsesVehicle(block)) block.ride = (RunnerRide)(((int)block.ride + 1) % 3);
        });

        /// <summary>Easy, Normal, Hard: picking a preset sets the fine values underneath.</summary>
        public void CyclePreset() => Edit(block =>
        {
            block.preset = (DifficultyPreset)(((int)block.preset + 1) % 3);
            block.difficulty = catalog.Block(block.type).presets[block.preset].Copy();
        });

        public void CycleHints() => Edit(block => block.hints = (HintPolicy)(((int)block.hints + 1) % 3));

        public void ToggleBoss() => Edit(block => block.boss = !block.boss);

        /// <summary>Mixes every other fight type into a fight block (Mixed), or back to one type.</summary>
        public void ToggleMix() => Edit(block =>
        {
            if (!catalog.Block(block.type).isFight) return;
            if (block.mix.Count > 0)
            {
                block.mix.Clear();
                return;
            }

            foreach (var type in catalog.BlockTypes)
            {
                if (type != block.type && catalog.Block(type).isFight) block.mix.Add(type);
            }
        });

        public void CycleWindow() => Edit(block => block.difficulty.windowScale = Next(windowSteps, block.difficulty.windowScale));

        public void CycleSpeed() => Edit(block => block.difficulty.speedScale = Next(speedSteps, block.difficulty.speedScale));

        public void CycleCount() => Edit(block => block.difficulty.count = block.difficulty.count >= 12 ? 2 : block.difficulty.count + 1);

        public void CycleZone() => Edit(block => block.difficulty.zoneLength = Next(zoneSteps, block.difficulty.zoneLength));

        public void CycleLength() => Edit(block => block.targetSeconds = block.targetSeconds >= 90f ? 40f : block.targetSeconds + 5f);

        #endregion

        #region Links

        /// <summary>The link after the selected block, or null when it is the last one.</summary>
        public LinkEntry? CurrentLink => Block < CurrentLevel.links.Count ? CurrentLevel.links[Block] : null;

        /// <summary>Cycles the link after the selected block through its natural options and the brick rebuild; it stops filling in by itself.</summary>
        public void CycleLink()
        {
            if (CurrentLink == null) return;
            var previous = CurrentLevel.blocks[Block];
            var next = CurrentLevel.blocks[Block + 1];
            LinkResolver.Options(previous, next, catalog, linkOptions);
            var index = linkOptions.IndexOf(CurrentLink.state);
            var link = LinkResolver.ForState(linkOptions[(index + 1) % linkOptions.Count], previous, next, catalog, Block);
            link.auto = false;
            CurrentLevel.links[Block] = link;
            Refresh();
        }

        #endregion

        #region Saving

        public bool IsValid => !ChapterValidator.HasErrors(problems);

        /// <summary>Marks the document as the saved player-made mode with this id.</summary>
        public void SavedAs(string id)
        {
            EditingId = id;
            Mode.id = id;
            Mode.builtIn = false;
        }

        public void Rename(ModeName name) => Mode.name = name;

        #endregion

        #region Helpers

        LevelData NewLevel() => new()
        {
            theme = ChapterTheme.TownDusk,
            music = catalog.Theme(ChapterTheme.TownDusk).music,
            hearts = 5,
        };

        DifficultyPreset CurrentPreset() => CurrentLevel.blocks.Count > 0 ? CurrentLevel.blocks[^1].preset : DifficultyPreset.Normal;

        void Edit(Action<BlockEntry> change)
        {
            if (CurrentBlock == null) return;
            change(CurrentBlock);
            Refresh();
        }

        void Refresh()
        {
            foreach (var level in Mode.chapter.levels) LinkResolver.Refresh(level, catalog);
            ChapterValidator.Validate(Mode.chapter, catalog, LinkSeconds, problems);
        }

        static float Next(float[] steps, float value)
        {
            for (var i = 0; i < steps.Length; i++)
            {
                if (steps[i] > value + 0.001f) return steps[i];
            }

            return steps[0];
        }

        static int IndexOf(IReadOnlyList<BlockType> types, BlockType type)
        {
            for (var i = 0; i < types.Count; i++)
            {
                if (types[i] == type) return i;
            }

            return -1;
        }

        #endregion
    }
}
