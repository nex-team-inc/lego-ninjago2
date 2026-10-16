#nullable enable

using Nex.Util;
using UnityEditor;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>Builder steps shared by the children's games: audio import settings and registry entries, and the config on GameConfigsManager.</summary>
    public static class KidsAudio
    {
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Public API

        /// <param name="names">Clip file names under the Ninjago SFX folder, without ".wav".</param>
        public static void Register(SfxManager.SoundEffect effect, params string[] names)
        {
            foreach (var name in names) Import($"{SfxRoot}/{name}.wav", false);
            NinjagoAssetsBuilder.EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict",
                dict => NinjagoAssetsBuilder.Clips(dict, effect, SfxRoot, names));
        }

        public static void RegisterMusic(BgmManager.BgmType type, string name)
        {
            var path = $"{BgmRoot}/{name}.ogg";
            Import(path, true);
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict",
                dict => EnumDictionaryEditorUtils.GetValueProperty(dict, (int)type).objectReferenceValue = Load<AudioClip>(path));
        }

        public static void SetConfig(string field, ScriptableObject config)
        {
            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                Set(root.GetComponent<GameConfigsManager>(), field, config);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Helpers

        static void Import(string path, bool music)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
            importer.SaveAndReimport();
        }

        #endregion
    }
}
