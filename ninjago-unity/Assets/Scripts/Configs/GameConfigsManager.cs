#nullable enable

using NaughtyAttributes;
using Nex.Ninjago;
using UnityEngine;

namespace Nex
{
    public class GameConfigsManager : Singleton<GameConfigsManager>
    {
        [SerializeField, Scene] string mainScene = null!;
        [SerializeField, Scene] string arGameScene = null!;
        [SerializeField, Scene] string nonARGameScene = null!;
        [Header("Game Selection")]
        [SerializeField, Scene] string gameScene = null!;
        [Header("Ninjago Fight Tuning")]
        [SerializeField] FightConfig fightConfig = null!;
        [Header("Ninjago Chase Tuning")]
        [SerializeField] ChaseConfig chaseConfig = null!;
        [Header("Ninjago Players Tuning")]
        [SerializeField] NinjagoPlayersConfig playersConfig = null!;
        [Header("Ninjago Hand Cursor Tuning")]
        [SerializeField] HandCursorConfig handCursorConfig = null!;
        [Header("Ninjago Stone Kick Tuning")]
        [SerializeField] StoneKickConfig stoneKickConfig = null!;
        [Header("Ninjago Earth Seal Tuning")]
        [SerializeField] EarthSealConfig earthSealConfig = null!;
        [Header("Ninjago Robot Arms Tuning")]
        [SerializeField] RobotArmsConfig robotArmsConfig = null!;
        [Header("Ninjago Mecha Fight Tuning")]
        [SerializeField] MechaFightConfig mechaFightConfig = null!;
        [Header("Ninjago Crouch Run Tuning")]
        [SerializeField] CrouchRunConfig crouchRunConfig = null!;
        public string MainScene => mainScene;
        public string ARGameScene => arGameScene;
        public string NonARGameScene => nonARGameScene;
        public string GameScene => gameScene;
        public FightConfig FightConfig => fightConfig;
        public ChaseConfig ChaseConfig => chaseConfig;
        public NinjagoPlayersConfig PlayersConfig => playersConfig;
        public HandCursorConfig HandCursorConfig => handCursorConfig;
        public StoneKickConfig StoneKickConfig => stoneKickConfig;
        public EarthSealConfig EarthSealConfig => earthSealConfig;
        public RobotArmsConfig RobotArmsConfig => robotArmsConfig;
        public MechaFightConfig MechaFightConfig => mechaFightConfig;
        public CrouchRunConfig CrouchRunConfig => crouchRunConfig;

        public GameModeType SelectedMode { get; set; }

        protected override GameConfigsManager GetThis()
        {
            return this;
        }
    }
}
