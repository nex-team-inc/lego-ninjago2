#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Call's play view: the courtyard from behind the ninjas, the shared HUD and the corner camera preview.</summary>
    public class CallView : KidsSceneView
    {
        [Header("Scene Prefab")]
        [SerializeField] CallScene scenePrefab = null!;
        [Header("Scene Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 sceneOrigin = new(17000f, 0f, 0f);

        CallConfig config = null!;

        public CallScene Scene { get; private set; } = null!;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.CallPlay;
        public override string AnalyticsScreenName => "call";

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, CallConfig aConfig, DetectionManager detection, int requestedPlayers)
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

        /// <summary>Plays every wave, then holds the last banner; false when the player backed out.</summary>
        public UniTask<bool> RunAsync(CancellationToken cancellationToken) => RunSceneAsync(Scene.RunAsync, config.EndHoldSeconds, cancellationToken);

        #endregion
    }
}
