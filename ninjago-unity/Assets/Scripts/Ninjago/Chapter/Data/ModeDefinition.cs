#nullable enable

using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>A built-in mode: title, icon and chapter. Player-made modes are the same structure saved with Easy Save.</summary>
    [CreateAssetMenu(fileName = "Mode", menuName = "Nex/Ninjago/Chapter/Mode Definition")]
    public class ModeDefinition : ScriptableObject
    {
        [Header("Id")]
        [Tooltip("Saved progress is keyed by this; never change it once shipped.")]
        [SerializeField] string id = "";
        [Header("Title")]
        [SerializeField] LocalizedString title = new();
        [Header("Icon")]
        [SerializeField] ModeIcon icon;
        [Header("Chapter")]
        [SerializeField] ChapterDefinition chapter = null!;

        public string Id => id;
        public ChapterDefinition Chapter => chapter;

        public ModeData ToModeData() => new()
        {
            id = id,
            name = new ModeName { title = title },
            icon = icon,
            builtIn = true,
            chapter = chapter.ToData(),
        };
    }
}
