#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The one world a level plays in. Each level's stations are laid out as one continuous route and painted in the
    /// level's theme; the ninjas persist across blocks. A Cinemachine brain on the world camera blends between the
    /// stations' views, and the world is shown on a stage canvas that sits between the scene background and the views,
    /// so switching block views only changes the overlay while the world and its camera keep running.
    /// </summary>
    public class ChapterStage : MonoBehaviour
    {
        [Header("World Origin")]
        [Tooltip("Far from the GameScene main camera so only the world camera draws the route.")]
        [SerializeField] Vector3 worldOrigin = new(-6000f, 0f, 0f);
        [Header("World Camera")]
        [SerializeField] Camera worldCamera = null!;
        [Header("Cinemachine Brain")]
        [SerializeField] CinemachineBrain brain = null!;
        [Header("Link Camera")]
        [Tooltip("Waypoint view for swings and crane moves between two stations.")]
        [SerializeField] CinemachineCamera linkCamera = null!;
        [Header("Sun")]
        [SerializeField] Light sun = null!;
        [Header("Route Root")]
        [SerializeField] Transform routeRoot = null!;
        [Header("Station Gap")]
        [Tooltip("Meters of open route between two stations.")]
        [SerializeField] float stationGap = 14f;
        [Header("Stage Canvas")]
        [SerializeField] Canvas stageCanvas = null!;
        [Header("Stage Plane Distance")]
        [Tooltip("Between the scene background (300) and the first view (290).")]
        [SerializeField] float stagePlaneDistance = 295f;
        [Header("World Feed")]
        [SerializeField] RawImage feed = null!;
        [Header("HUD")]
        [SerializeField] ChapterHud hud = null!;
        [Header("Theme Painter")]
        [SerializeField] ThemePainter painter = null!;
        [Header("Ninja Prefab")]
        [SerializeField] ChapterNinja ninjaPrefab = null!;
        [Header("Ground Prefab")]
        [Tooltip("Unit plane stretched under the whole route.")]
        [SerializeField] Transform groundPrefab = null!;

        readonly List<ChapterNinja> ninjas = new();
        readonly List<ChapterBlock> blocks = new();
        RenderTexture texture = null!;
        Transform? ground;
        int priority;

        public ChapterHud Hud => hud;
        public Camera WorldCamera => worldCamera;
        public IReadOnlyList<ChapterNinja> Ninjas => ninjas;
        public IReadOnlyList<ChapterBlock> Blocks => blocks;

        #region Initialization

        public void Initialize(Camera viewCamera, DetectionManager detection, int requestedPlayers, PlayerHearts hearts)
        {
            transform.position = worldOrigin;
            texture = new RenderTexture(Screen.width, Screen.height, 24) { name = "ChapterWorld" };
            worldCamera.targetTexture = texture;
            feed.texture = texture;
            stageCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            stageCanvas.worldCamera = viewCamera;
            stageCanvas.planeDistance = stagePlaneDistance;
            painter.Initialize();
            hud.Initialize(detection, requestedPlayers, hearts);
            SetVisible(false);
        }

        void OnDestroy()
        {
            texture.Release();
            Destroy(texture);
        }

        #endregion

        #region Public API

        public void SetVisible(bool visible)
        {
            stageCanvas.enabled = visible;
            worldCamera.enabled = visible;
        }

        /// <summary>One ninja per active player, in player order.</summary>
        public void SetPlayers(IReadOnlyList<PlayerBody> players)
        {
            foreach (var ninja in ninjas) Destroy(ninja.gameObject);
            ninjas.Clear();
            foreach (var player in players)
            {
                var ninja = Instantiate(ninjaPrefab, transform);
                ninja.Initialize(player.PlayerIndex, player.Color, worldCamera);
                ninjas.Add(ninja);
            }

            hud.SetPlayers(players);
        }

        /// <summary>
        /// Lays out the level's stations along one route in the level's theme and initializes them. An entry the previous
        /// station can host (the next Boss phase) gets that same station, so the list may repeat a block.
        /// </summary>
        public IReadOnlyList<ChapterBlock> BuildLevel(LevelData level, ForgeCatalog catalog, BlockContext context, int levelSeed)
        {
            ClearLevel();
            var z = 0f;
            for (var i = 0; i < level.blocks.Count; i++)
            {
                var entry = level.blocks[i];
                var previous = i > 0 ? blocks[i - 1] : null;
                if (previous != null && catalog.Block(entry.type).prefab == catalog.Block(level.blocks[i - 1].type).prefab && previous.CanHost(entry))
                {
                    blocks.Add(previous);
                    continue;
                }

                var block = Instantiate(catalog.Block(entry.type).prefab!, routeRoot);
                block.transform.localPosition = new Vector3(0f, 0f, z);
                block.Initialize(context, entry, levelSeed * 31 + i);
                var themeKey = level.ThemeOf(entry);
                painter.Paint(block.gameObject, themeKey, catalog.Theme(themeKey));
                blocks.Add(block);
                z += block.RouteLength + stationGap;
            }

            ground = Instantiate(groundPrefab, routeRoot);
            ground.localPosition = new Vector3(0f, -0.02f, z * 0.5f - 20f);
            ground.localScale = new Vector3(400f, 1f, z + 200f);
            painter.Paint(ground.gameObject, level.theme, catalog.Theme(level.theme));
            ThemePainter.ApplyEnvironment(catalog.Theme(level.theme), worldCamera, sun);
            hud.ShowLevel(level, catalog);
            return blocks;
        }

        public void ClearLevel()
        {
            foreach (var block in blocks) Destroy(block.gameObject);
            blocks.Clear();
            if (ground != null) Destroy(ground.gameObject);
            ground = null;
        }

        /// <summary>Jumps to a station's view with no blend (behind a full-screen view, or at a level's start).</summary>
        public void CutTo(ChapterBlock block)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            block.ViewCamera.Priority = ++priority;
        }

        /// <summary>
        /// The link's camera move into the next station's view; swings and cranes pass through a waypoint. Between two
        /// phases of the same station the camera pushes in on that station's focus instead.
        /// </summary>
        public async UniTask MoveCameraAsync(ChapterBlock from, ChapterBlock to, CameraMove move, float seconds, CancellationToken cancellationToken)
        {
            var start = from.ViewCamera.transform;
            var end = to.ViewCamera.transform;
            if (from == to)
            {
                // A new phase in the same station: push in on its focus (the Boss roaring) and back out.
                var focus = to.FocusPoint + Vector3.up * 1.5f;
                var near = Vector3.Lerp(start.position, focus, 0.4f);
                linkCamera.transform.SetPositionAndRotation(near, Quaternion.LookRotation(focus - near, Vector3.up));
                await BlendAsync(linkCamera, CinemachineBlendDefinition.Styles.EaseInOut, seconds * 0.4f, cancellationToken);
                await BlendAsync(to.ViewCamera, CinemachineBlendDefinition.Styles.EaseInOut, seconds * 0.6f, cancellationToken);
                return;
            }

            if (move == CameraMove.Glide)
            {
                await BlendAsync(to.ViewCamera, CinemachineBlendDefinition.Styles.EaseInOut, seconds, cancellationToken);
                return;
            }

            var middle = Vector3.Lerp(start.position, end.position, 0.5f);
            var forward = Vector3.ProjectOnPlane(end.position - start.position, Vector3.up);
            if (forward.sqrMagnitude < 1e-3f) forward = routeRoot.forward;
            var side = Vector3.Cross(Vector3.up, forward.normalized);
            var lift = move == CameraMove.CraneUp ? 14f : move == CameraMove.FollowFlight ? 7f : 4f;
            var swing = move == CameraMove.SwingToSide ? 10f : move == CameraMove.SwingBehind ? -10f : 0f;
            var waypoint = middle + Vector3.up * lift + side * swing;
            var lookAt = Vector3.Lerp(middle, end.position + end.forward * 6f, 0.6f);
            linkCamera.transform.SetPositionAndRotation(waypoint, Quaternion.LookRotation(lookAt - waypoint, Vector3.up));
            await BlendAsync(linkCamera, CinemachineBlendDefinition.Styles.EaseIn, seconds * 0.5f, cancellationToken);
            await BlendAsync(to.ViewCamera, CinemachineBlendDefinition.Styles.EaseOut, seconds * 0.5f, cancellationToken);
        }

        #endregion

        #region Helpers

        async UniTask BlendAsync(CinemachineCamera target, CinemachineBlendDefinition.Styles style, float seconds, CancellationToken cancellationToken)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(style, seconds);
            target.Priority = ++priority;
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion
    }
}
