#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace Nex
{
    [CreateAssetMenu(fileName = "GameModeConfig", menuName = "Nex/Configs/GameModeConfig")]
    public class GameModeConfig : ScriptableObject
    {
        [Serializable]
        public class ModeDefinition
        {
            public LocalizedString displayName = new();
            [Tooltip("Addressable prefab with a BaseGame component on its root.")]
            public AssetReferenceGameObject gamePrefab = null!;
            [Tooltip("Above 1, the menu asks how many players before loading the game.")]
            [Range(1, 2)] public int maxPlayers = 1;
        }

        [Header("Modes")]
        [SerializeField] EnumDictionary<GameModeType, ModeDefinition> modes = new();

        [Serializable]
        public class PlaytestEntry
        {
            public GameModeType mode;
            [Tooltip("Chapter only: id of the built-in or player-made chapter mode to start (e.g. ninja_chapter_proof).")]
            public string chapterModeId = "";
        }

        [Header("Menu Order")]
        [SerializeField] List<GameModeType> modeOrders = new();

        [Header("Playtest")]
        [Tooltip("Entries of the menu's Playtest column, in order.")]
        [SerializeField] List<PlaytestEntry> playtestEntries = new();

        [Header("Playtest Only")]
        [Tooltip("The menu shows only the playtest entries and hides every other mode, chapter modes included.")]
        [SerializeField] bool playtestOnly;

        public IReadOnlyList<GameModeType> ModeOrders => modeOrders;
        public IReadOnlyList<PlaytestEntry> PlaytestEntries => playtestEntries;
        public bool PlaytestOnly => playtestOnly;

        #region Public API

        public ModeDefinition GetMode(GameModeType mode) => modes[mode];

        #endregion
    }
}
