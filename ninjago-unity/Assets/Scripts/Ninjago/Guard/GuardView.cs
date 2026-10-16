#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Guard's play view: the courtyard from behind the ninjas, the shared HUD and the corner camera preview.</summary>
    public class GuardView : KidsSceneView
    {
        [Header("Scene Prefab")]
        [SerializeField] GuardScene scenePrefab = null!;
        [Header("Scene Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 sceneOrigin = new(14000f, 0f, 0f);

        GuardConfig config = null!;

        public GuardScene Scene { get; private set; } = null!;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.GuardPlay;
        public override string AnalyticsScreenName => "guard";

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, GuardConfig aConfig, DetectionManager detection, int requestedPlayers)
        {
            config = aConfig;
            InitializeFeed(players, detection, requestedPlayers);
            Scene = Instantiate(scenePrefab, sceneOrigin, Quaternion.identity);
            Scene.Initialize(players, config, Hud, Target);
        }

        protected override void OnDestroy()
        {
            if (Scene != null) Destroy(Scene.gameObject);
            base.OnDestroy();
        }

        #endregion

        #region Public API

        /// <summary>Plays every throw, then holds the last banner; false when the player backed out.</summary>
        public UniTask<bool> RunAsync(CancellationToken cancellationToken) => RunSceneAsync(Scene.RunAsync, config.EndHoldSeconds, cancellationToken);

        #endregion
    }
}
