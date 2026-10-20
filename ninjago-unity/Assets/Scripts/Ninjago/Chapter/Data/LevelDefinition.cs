#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    [CreateAssetMenu(fileName = "Level", menuName = "Nex/Ninjago/Chapter/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Level")]
        [SerializeField] LevelData level = new();

        public LevelData Level => level;
    }
}
