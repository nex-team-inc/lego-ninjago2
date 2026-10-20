#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Lays out one level from the config at runtime, so designers change zone lists without touching prefabs. The
    /// ninja runs along a wooden floor beside a tall indoor wall: clear zones are plain wall with pillars and scrolls,
    /// talking zones are a row of windows (sunlight on the floor, monsters outside looking in) whose sill a crouched
    /// ninja stays below. The garden behind the wall shows through the windows; the gate stands at the end.
    /// </summary>
    public class CrouchLevelBuilder : MonoBehaviour
    {
        [Header("Level Root")]
        [Tooltip("Cleared and refilled for every level.")]
        [SerializeField] Transform levelRoot = null!;
        [Header("Floor Prefab")]
        [Tooltip("Unit length along +X, stretched under the whole level.")]
        [SerializeField] Transform floorPrefab = null!;
        [Header("Floor Seam Prefab")]
        [SerializeField] Transform floorSeamPrefab = null!;
        [Header("Floor Seam Spacing")]
        [Tooltip("Meters between plank seams; they show the speed.")]
        [SerializeField] float floorSeamSpacing = 1.6f;
        [Header("Wall Prefab")]
        [Tooltip("Unit length along +X, stretched over each clear zone.")]
        [SerializeField] Transform wallPrefab = null!;
        [Header("Window Wall Prefab")]
        [Tooltip("Unit length along +X with the window opening, stretched over each talking zone.")]
        [SerializeField] Transform windowWallPrefab = null!;
        [Header("Window Post Prefab")]
        [SerializeField] Transform windowPostPrefab = null!;
        [Header("Window Pane Width")]
        [Tooltip("About this many meters between window posts.")]
        [SerializeField] float paneWidth = 2.4f;
        [Header("Sunlight Prefab")]
        [Tooltip("Unit length along +X, stretched over the floor of each talking zone.")]
        [SerializeField] Transform sunlightPrefab = null!;
        [Header("Pillar Prefab")]
        [SerializeField] Transform pillarPrefab = null!;
        [Header("Pillar Spacing")]
        [Tooltip("About this many meters between pillars along plain wall; every zone edge has one.")]
        [SerializeField] float pillarSpacing = 6f;
        [Header("Scroll Prefab")]
        [SerializeField] Transform scrollPrefab = null!;
        [Header("Scroll Chance")]
        [Tooltip("Chance that a bay of plain wall between two pillars has a hanging scroll.")]
        [SerializeField, Range(0f, 1f)] float scrollChance = 0.5f;
        [Header("Monster Prefab")]
        [SerializeField] CrouchMonster monsterPrefab = null!;
        [Header("Monster Spacing")]
        [Tooltip("Meters between monsters along a row of windows.")]
        [SerializeField] float monsterSpacing = 5f;
        [Header("Monster Depth")]
        [Tooltip("Meters behind the path where the monsters stand, outside the wall.")]
        [SerializeField] float monsterDepth = 3.4f;
        [Header("Monster Colors")]
        [SerializeField] Color[] monsterColors = { new(0.45f, 0.75f, 0.3f), new(0.85f, 0.45f, 0.75f), new(0.35f, 0.55f, 0.95f), new(0.95f, 0.65f, 0.2f) };
        [Header("Gate Prefab")]
        [SerializeField] Transform gatePrefab = null!;
        [Header("Ground Prefab")]
        [Tooltip("The garden ground behind the wall; unit length along +X.")]
        [SerializeField] Transform groundPrefab = null!;
        [Header("Tree Prefab")]
        [SerializeField] Transform treePrefab = null!;
        [Header("House Prefab")]
        [SerializeField] Transform housePrefab = null!;
        [Header("Hill Prefab")]
        [SerializeField] Transform hillPrefab = null!;
        [Header("Scenery Margin")]
        [Tooltip("Meters of scenery before the start and after the gate.")]
        [SerializeField] float sceneryMargin = 30f;

        #region Public API

        /// <returns>The gate position (lane-local).</returns>
        public Vector3 Build(CrouchRunConfig config, int levelIndex)
        {
            // The same scenery on every retry of a level.
            return Build(config.GetLevel(levelIndex), levelIndex * 7919 + 17);
        }

        /// <returns>The gate position (lane-local).</returns>
        public Vector3 Build(CrouchRunConfig.Level level, int seed)
        {
            for (var i = levelRoot.childCount - 1; i >= 0; i--) Destroy(levelRoot.GetChild(i).gameObject);
            var random = new System.Random(seed);
            var end = level.Length;
            var start = 0f;
            foreach (var zone in level.zones)
            {
                if (zone.state == CrouchZoneState.Talking) WindowWall(start, start + zone.length, random);
                else PlainWall(start, start + zone.length, random);
                PlaceAt(pillarPrefab, start);
                start += zone.length;
            }

            PlaceAt(pillarPrefab, end);
            PlainWall(-sceneryMargin, 0f, random);
            PlainWall(end, end + sceneryMargin, random);
            Stretch(floorPrefab, -sceneryMargin, end + sceneryMargin);
            for (var x = -sceneryMargin; x < end + sceneryMargin; x += floorSeamSpacing) PlaceAt(floorSeamPrefab, x);
            var gate = PlaceAt(gatePrefab, end);
            PlaceGarden(-sceneryMargin, end + sceneryMargin, random);
            return gate.localPosition;
        }

        #endregion

        #region Helpers

        void PlainWall(float from, float to, System.Random random)
        {
            Stretch(wallPrefab, from, to);
            var bays = Mathf.Max(1, Mathf.RoundToInt((to - from) / pillarSpacing));
            var bay = (to - from) / bays;
            for (var i = 0; i < bays; i++)
            {
                if (i > 0) PlaceAt(pillarPrefab, from + bay * i);
                if (random.NextDouble() < scrollChance) PlaceAt(scrollPrefab, from + bay * (i + 0.5f));
            }
        }

        void WindowWall(float from, float to, System.Random random)
        {
            Stretch(windowWallPrefab, from, to);
            Stretch(sunlightPrefab, from, to);
            var panes = Mathf.Max(1, Mathf.RoundToInt((to - from) / paneWidth));
            var pane = (to - from) / panes;
            for (var i = 1; i < panes; i++) PlaceAt(windowPostPrefab, from + pane * i);
            var count = Mathf.Max(1, Mathf.FloorToInt((to - from) / monsterSpacing));
            var step = (to - from) / count;
            for (var i = 0; i < count; i++)
            {
                var monster = Instantiate(monsterPrefab, levelRoot);
                monster.transform.localPosition = new Vector3(from + step * (i + 0.5f), 0f, monsterDepth + (float)random.NextDouble() * 0.6f);
                // Faces the corridor (-Z), looking in through the window.
                monster.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                monster.Initialize(monsterColors[random.Next(monsterColors.Length)], (float)random.NextDouble() * 6f);
            }
        }

        void PlaceGarden(float from, float to, System.Random random)
        {
            Stretch(groundPrefab, from, to);
            Scatter(treePrefab, from, to, 6f, 9f, 0.9f, 1.3f, random);
            Scatter(housePrefab, from, to, 16f, 18f, 0.9f, 1.2f, random);
            Scatter(hillPrefab, from, to, 32f, 40f, 0.8f, 1.4f, random);
        }

        Transform PlaceAt(Transform prefab, float x)
        {
            var piece = Instantiate(prefab, levelRoot);
            piece.localPosition = new Vector3(x, 0f, 0f);
            return piece;
        }

        void Stretch(Transform prefab, float from, float to)
        {
            var piece = Instantiate(prefab, levelRoot);
            piece.localPosition = new Vector3((from + to) * 0.5f, 0f, 0f);
            piece.localScale = new Vector3(to - from, 1f, 1f);
        }

        // One piece every spacing meters (with jitter) at a depth behind the path, randomly sized.
        void Scatter(Transform prefab, float from, float to, float spacing, float depth, float minScale, float maxScale, System.Random random)
        {
            for (var x = from; x < to; x += spacing)
            {
                var piece = Instantiate(prefab, levelRoot);
                var jitter = ((float)random.NextDouble() - 0.5f) * spacing * 0.6f;
                piece.localPosition = new Vector3(x + jitter, 0f, depth + (float)random.NextDouble() * 3f);
                piece.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
            }
        }

        #endregion
    }
}
