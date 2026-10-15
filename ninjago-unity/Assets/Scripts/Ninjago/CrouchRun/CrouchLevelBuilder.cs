#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Lays out one level from the config at runtime, so designers change zone lists without touching prefabs: a tinted
    /// path piece per zone, talking monsters behind each talking zone, the gate at the end, and layered scenery (hedge,
    /// trees, houses, hills) running the whole length so it reads as a side-scroller.
    /// </summary>
    public class CrouchLevelBuilder : MonoBehaviour
    {
        [Header("Level Root")]
        [Tooltip("Cleared and refilled for every level.")]
        [SerializeField] Transform levelRoot = null!;
        [Header("Path Piece Prefab")]
        [Tooltip("Unit cube along +X; scaled to each zone's length and tinted with its state color.")]
        [SerializeField] Renderer pathPiecePrefab = null!;
        [Header("Path Depth")]
        [SerializeField] float pathDepth = 3f;
        [Header("Monster Prefab")]
        [SerializeField] CrouchMonster monsterPrefab = null!;
        [Header("Monster Spacing")]
        [Tooltip("Meters between monsters in a talking zone.")]
        [SerializeField] float monsterSpacing = 5f;
        [Header("Monster Depth")]
        [Tooltip("Meters behind the path where the monsters stand.")]
        [SerializeField] float monsterDepth = 3.2f;
        [Header("Monster Colors")]
        [SerializeField] Color[] monsterColors = { new(0.45f, 0.75f, 0.3f), new(0.85f, 0.45f, 0.75f), new(0.35f, 0.55f, 0.95f), new(0.95f, 0.65f, 0.2f) };
        [Header("Gate Prefab")]
        [SerializeField] Transform gatePrefab = null!;
        [Header("Hedge Prefab")]
        [Tooltip("Unit cube along +X, stretched over the whole level just behind the path.")]
        [SerializeField] Transform hedgePrefab = null!;
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
            for (var i = levelRoot.childCount - 1; i >= 0; i--) Destroy(levelRoot.GetChild(i).gameObject);
            var level = config.GetLevel(levelIndex);
            // The same scenery on every retry of a level.
            var random = new System.Random(levelIndex * 7919 + 17);
            var start = 0f;
            for (var zoneIndex = 0; zoneIndex < level.zones.Count; zoneIndex++)
            {
                var zone = level.zones[zoneIndex];
                var look = config.GetLook(zone.state);
                var piece = Instantiate(pathPiecePrefab, levelRoot);
                piece.transform.localPosition = new Vector3(start + zone.length * 0.5f, -0.25f, 0f);
                piece.transform.localScale = new Vector3(zone.length, 0.5f, pathDepth);
                piece.material.color = Color.Lerp(look.color, Color.white, 0.35f);
                if (zone.state == CrouchZoneState.Talking) PlaceMonsters(look, start, zone.length, random);
                start += zone.length;
            }

            var gate = Instantiate(gatePrefab, levelRoot);
            gate.localPosition = new Vector3(start, 0f, 0f);
            PlaceScenery(-sceneryMargin, start + sceneryMargin, random);
            return gate.localPosition;
        }

        #endregion

        #region Helpers

        void PlaceMonsters(CrouchRunConfig.ZoneLook look, float zoneStart, float zoneLength, System.Random random)
        {
            var count = Mathf.Max(1, Mathf.FloorToInt(zoneLength / monsterSpacing));
            var step = zoneLength / count;
            for (var i = 0; i < count; i++)
            {
                var monster = Instantiate(monsterPrefab, levelRoot);
                monster.transform.localPosition = new Vector3(zoneStart + step * (i + 0.5f), 0f, monsterDepth + (float)random.NextDouble() * 0.8f);
                monster.Initialize(look.title, look.instruction, look.color, monsterColors[random.Next(monsterColors.Length)], (float)random.NextDouble() * 6f,
                    i % 2 == 0);
            }
        }

        void PlaceScenery(float from, float to, System.Random random)
        {
            var hedge = Instantiate(hedgePrefab, levelRoot);
            hedge.localPosition = new Vector3((from + to) * 0.5f, 0f, 2.2f);
            hedge.localScale = new Vector3(to - from, 1f, 1f);
            Scatter(treePrefab, from, to, 6f, 9f, 0.9f, 1.3f, random);
            Scatter(housePrefab, from, to, 16f, 18f, 0.9f, 1.2f, random);
            Scatter(hillPrefab, from, to, 32f, 40f, 0.8f, 1.4f, random);
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
