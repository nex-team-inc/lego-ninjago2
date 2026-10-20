#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    public enum ThemeRole
    {
        Ground = 0,
        Path = 1,
        Wall = 2,
        Roof = 3,
        Accent = 4,
        Foliage = 5,
    }

    /// <summary>
    /// Gives every station of a level its theme's look: renderers that use one of the role materials (the chapter's own
    /// and the reused Crouch Run pieces) get that role's color for the theme, and the sky, fog, ambient light and sun
    /// follow the theme. Same detection, new look; no new art per mode.
    /// </summary>
    public class ThemePainter : MonoBehaviour
    {
        [Serializable]
        public class RoleMaterial
        {
            public Material material = null!;
            public ThemeRole role;
        }

        [Header("Role Materials")]
        [Tooltip("Base materials in the station prefabs and the role each one paints as.")]
        [SerializeField] List<RoleMaterial> roleMaterials = new();

        readonly Dictionary<Material, ThemeRole> roleOf = new();
        readonly Dictionary<Material, Material> baseOf = new();
        readonly Dictionary<(ChapterTheme, ThemeRole, Material), Material> painted = new();
        readonly List<Renderer> renderers = new();

        #region Initialization

        public void Initialize()
        {
            foreach (var entry in roleMaterials) roleOf[entry.material] = entry.role;
        }

        void OnDestroy()
        {
            foreach (var material in painted.Values) Destroy(material);
        }

        #endregion

        #region Public API

        // Station content is partly laid out at runtime (the stealth path's zones), so the renderers are collected after building.
        public void Paint(GameObject root, ChapterTheme key, ForgeCatalog.ThemeSpec theme)
        {
            root.GetComponentsInChildren(true, renderers);
            foreach (var target in renderers)
            {
                var materials = target.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var current = materials[i];
                    if (current == null) continue;
                    var original = baseOf.TryGetValue(current, out var known) ? known : current;
                    if (!roleOf.TryGetValue(original, out var role)) continue;
                    materials[i] = Painted(key, role, original, theme);
                    changed = true;
                }

                if (changed) target.sharedMaterials = materials;
            }
        }

        public static void ApplyEnvironment(ForgeCatalog.ThemeSpec theme, Camera camera, Light sun)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = theme.sky;
            RenderSettings.fog = theme.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = theme.fog;
            RenderSettings.fogDensity = theme.fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambient;
            sun.color = theme.light;
            sun.intensity = theme.lightIntensity;
            sun.transform.rotation = Quaternion.Euler(theme.lightAngles.x, theme.lightAngles.y, 0f);
        }

        #endregion

        #region Helpers

        Material Painted(ChapterTheme key, ThemeRole role, Material original, ForgeCatalog.ThemeSpec theme)
        {
            if (painted.TryGetValue((key, role, original), out var material)) return material;
            material = new Material(original) { name = $"{original.name} ({key})", color = ColorOf(role, theme) };
            if (role == ThemeRole.Accent && theme.lanternsGlow)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", theme.accent * 1.6f);
            }

            painted[(key, role, original)] = material;
            baseOf[material] = original;
            return material;
        }

        static Color ColorOf(ThemeRole role, ForgeCatalog.ThemeSpec theme) => role switch
        {
            ThemeRole.Ground => theme.ground,
            ThemeRole.Path => theme.path,
            ThemeRole.Wall => theme.wall,
            ThemeRole.Roof => theme.roof,
            ThemeRole.Accent => theme.accent,
            _ => theme.foliage,
        };

        #endregion
    }
}
