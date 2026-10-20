#nullable enable

using Cysharp.Threading.Tasks;
using Nex.Ninjago;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class MainCoordinator : MonoBehaviour
    {
        [SerializeField] ViewManager viewManager = null!;
        [Header("Game Selection")]
        [SerializeField] GameModeSelectionView gameModeSelectionViewPrefab = null!;
        [Header("Game Selection Config")]
        [SerializeField] GameModeConfig gameModeConfig = null!;
        [Header("Player Count")]
        [SerializeField] PlayerCountView playerCountViewPrefab = null!;

        bool prepared;

        UniTaskCompletionSource? preparationSource;

        #region Life Cycle

        void OnEnable()
        {
            if (prepared) return;

            prepared = true;
            preparationSource?.TrySetResult();
            preparationSource = null;
        }

        #endregion

        #region Public

        public async UniTask Initialize()
        {
            if (!prepared)
            {
                preparationSource = new UniTaskCompletionSource();
                await preparationSource.Task;
            }
        }

        public async UniTask StartMain()
        {
            await viewManager.PushView(CreateGameModeSelectionView(), animate: true);
        }

        #endregion

        #region Game Selection

        GameModeSelectionView CreateGameModeSelectionView()
        {
            var gameModeSelectionView = Instantiate(gameModeSelectionViewPrefab);
            var configs = GameConfigsManager.Instance;
            var chapterModes = ChapterModes.All(configs.ChapterModes, PlayerDataManager.Instance.ChapterSave);
            gameModeSelectionView.Initialize(PlayerDataManager.Instance.NinjagoProgress.lastMiniGame, chapterModes, mode => configs.ForgeConfig.NameOf(mode.name));
            gameModeSelectionView.ModeSelected += GameModeSelectionOnModeSelected;
            gameModeSelectionView.ExitRequested += GameModeSelectionOnExitRequested;
            return gameModeSelectionView;
        }

        void GameModeSelectionOnModeSelected(GameModeType mode, string chapterModeId)
        {
            GameConfigsManager.Instance.SelectedMode = mode;
            GameConfigsManager.Instance.SelectedChapterModeId = chapterModeId;
            NinjagoAnalytics.UiAction(gameModeSelectionViewPrefab.AnalyticsScreenName, mode == GameModeType.Chapter ? $"chapter:{chapterModeId}" : mode.ToString());
            if (gameModeConfig.GetMode(mode).maxPlayers > 1)
            {
                viewManager.PushView(CreatePlayerCountView()).Forget();
                return;
            }

            LoadGameSceneAsync().Forget();
        }

        PlayerCountView CreatePlayerCountView()
        {
            var playerCountView = Instantiate(playerCountViewPrefab);
            playerCountView.Initialize(PlayerDataManager.Instance.PlayerPreference.numPlayers);
            playerCountView.PlayersChosen += PlayerCountOnPlayersChosen;
            return playerCountView;
        }

        void PlayerCountOnPlayersChosen(int numPlayers)
        {
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.numPlayers = numPlayers);
            LoadGameSceneAsync().Forget();
        }

        async UniTaskVoid LoadGameSceneAsync()
        {
            await ScreenBlockerManager.Instance.Show();
            SceneManager.LoadScene(GameConfigsManager.Instance.GameScene);
        }

        void GameModeSelectionOnExitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Nex.Platform.DeviceActionDelegate.Instance.ExitGame();
#endif
        }

        #endregion
    }
}
