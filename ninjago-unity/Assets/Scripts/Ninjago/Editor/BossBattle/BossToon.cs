#nullable enable

using Nex.BilliardRogue;
using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Shared editor material factory for the Boss Battle builders (TDD §3.5, research hd2d-rendering.md §4.5): ToonLit
    /// twins in Assets/Materials/Ninjago/BossBattle/, built on the BilliardRogue shaders and their property contract
    /// (ToonLit.shader / MaterialsBuilder). Each call loads or creates the material (same asset, same GUID) and rewrites
    /// its properties; the caller saves the assets. Never touches the shared Assets/Materials/Ninjago/*.mat.
    /// </summary>
    public static class BossToon
    {
        public const string MaterialsRoot = NinjagoEditorUtils.MaterialsRoot + "/BossBattle";
        const string SurfaceTexturesRoot = "Assets/Textures/BilliardRogue/Surfaces";
        const string ToonLitShader = "BilliardRogue/ToonLit";
        const string ToonLitTransparentShader = "BilliardRogue/ToonLitTransparent";
        const string GlowShader = "BilliardRogue/GlowParticle";
        const string FallbackShader = "Universal Render Pipeline/Lit";

        // MaterialsBuilder's tints: light × lerp(tint, 1, ramp), darker = harder sun shadows.
        static readonly Color ActorShadowTint = new(0.34f, 0.32f, 0.5f);
        static readonly Color SurfaceShadowTint = new(0.3f, 0.29f, 0.44f);
        static readonly Color ActorRim = new(1f, 0.95f, 0.85f, 0.55f);

        #region Materials

        /// <summary>Flat-colour cel-shaded part; HDR emission blooms (keep ambience ≤ 1.3, hazards and tells above 2).</summary>
        /// <param name="bands">Light bands, 3..5.</param>
        /// <param name="rim">Rim colour, alpha = strength (default: the actors' strong rim).</param>
        public static Material Toon(string name, Color color, Color? emission = null, int bands = 4, Color? shadowTint = null, Color? rim = null)
        {
            var material = LoadOrCreate(name, ToonLitShader);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Bands", Mathf.Clamp(bands, 3, 5));
            material.SetFloat("_BandSoftness", 0.02f);
            material.SetColor("_ShadowTint", shadowTint ?? ActorShadowTint);
            material.SetFloat("_AmbientStrength", 0.35f);
            material.SetColor("_RimColor", rim ?? ActorRim);
            material.SetFloat("_RimPower", 3f);
            SetKeyword(material, "_EMISSION", "_UseEmission", emission.HasValue);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", emission ?? Color.black);
            material.SetFloat("_EmissionStrength", 1f);
            SetKeyword(material, "_WORLD_UV", "_UseWorldUv", false);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", false);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", false);
            SetKeyword(material, "_ALPHATEST_ON", "_AlphaClip", false);
            material.SetFloat("_FlashAmount", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A relief surface for primitives (walls, bricks, rocks, canyon): the BilliardRogue surface set's albedo, normal and
        /// cavity, box-projected in world space. Props: bump 2-3, cavity 0.75; floors: bump 1.1, cavity 0.4.
        /// </summary>
        /// <param name="sourceSurface">StoneFloor, MossyBrick, CryptBrick, CryptFloor, WoodPlank, CrystalRock, Dirt, Grass ...</param>
        /// <param name="tiling">UV per metre (0.5 = a 2 m tile).</param>
        public static Material Surface(string name, string sourceSurface, float tiling = 0.5f, float bump = 2f, float cavity = 0.75f)
        {
            var material = LoadOrCreate(name, ToonLitShader);
            var albedo = SetTexture(material, "_BaseMap", $"{SurfaceTexturesRoot}/{sourceSurface}_Albedo.png");
            var normal = SetTexture(material, "_BumpMap", $"{SurfaceTexturesRoot}/{sourceSurface}_Normal.png");
            var cavityMap = SetTexture(material, "_CavityMap", $"{SurfaceTexturesRoot}/{sourceSurface}_Cavity.png");
            if (!albedo) Debug.LogWarning($"[BossToon] {sourceSurface}_Albedo.png missing; {name} is flat grey.");
            material.SetColor("_BaseColor", albedo ? Color.white : new Color(0.5f, 0.5f, 0.52f));
            SetKeyword(material, "_WORLD_UV", "_UseWorldUv", true);
            material.SetFloat("_Tiling", tiling);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", normal);
            material.SetFloat("_BumpScale", bump);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", cavityMap);
            material.SetFloat("_CavityStrength", cavity);
            material.SetFloat("_Bands", 4f);
            material.SetFloat("_BandSoftness", 0.02f);
            material.SetColor("_ShadowTint", SurfaceShadowTint);
            material.SetFloat("_AmbientStrength", 0.35f);
            material.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.12f));
            material.SetFloat("_RimPower", 4f);
            SetKeyword(material, "_EMISSION", "_UseEmission", false);
            SetKeyword(material, "_ALPHATEST_ON", "_AlphaClip", false);
            material.SetFloat("_FlashAmount", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Unlit additive HDR glow for rings, frames and markers (GlowParticle: texture × _BaseColor × vertex colour ×
        /// _Intensity; values above the bloom threshold bloom). Meshes without vertex colours read white.
        /// </summary>
        public static Material Glow(string name, Color hdrColor)
        {
            var material = LoadOrCreate(name, GlowShader);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", hdrColor);
            material.SetFloat("_Intensity", 1f);
            SetKeyword(material, "_SOFT_DISC", "_SoftDisc", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>See-through cel-shaded telegraph or ghost (ToonLitTransparent, _Alpha = color.a); no shadows.</summary>
        public static Material Transparent(string name, Color color)
        {
            var material = LoadOrCreate(name, ToonLitTransparentShader);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 1f));
            material.SetFloat("_Alpha", color.a);
            material.SetFloat("_Bands", 3f);
            material.SetColor("_ShadowTint", new Color(0.7f, 0.68f, 0.8f));
            material.SetColor("_RimColor", new Color(1f, 1f, 1f, 0f));
            SetKeyword(material, "_EMISSION", "_UseEmission", false);
            SetKeyword(material, "_WORLD_UV", "_UseWorldUv", false);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", false);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        #endregion

        #region Layers

        /// <summary>Layer 8 "World" (everything the boss world camera draws), or -1 when it does not exist.</summary>
        public static int WorldLayer => WorldLayers.Resolve();

        /// <summary>Puts the hierarchy on the World layer (warns and leaves it when the layer is missing).</summary>
        public static void SetLayerRecursively(GameObject root)
        {
            var layer = WorldLayer;
            if (layer < 0)
            {
                Debug.LogWarning($"[BossToon] No '{WorldLayers.WorldLayerName}' layer; {root.name} keeps its layers.");
                return;
            }

            SetLayerRecursively(root, layer);
        }

        public static void SetLayerRecursively(GameObject root, int layer) => WorldLayers.Apply(root, layer);

        #endregion

        #region Helpers

        static Material LoadOrCreate(string name, string shaderName)
        {
            NinjagoEditorUtils.EnsureFolder(MaterialsRoot);
            var path = $"{MaterialsRoot}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[BossToon] Shader '{shaderName}' not found; {name} falls back to {FallbackShader}.");
                shader = Shader.Find(FallbackShader);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        static bool SetTexture(Material material, string property, string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (material.HasProperty(property)) material.SetTexture(property, texture);
            return texture != null;
        }

        static void SetKeyword(Material material, string keyword, string toggleProperty, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
            if (material.HasProperty(toggleProperty)) material.SetFloat(toggleProperty, enabled ? 1f : 0f);
        }

        #endregion
    }
}
