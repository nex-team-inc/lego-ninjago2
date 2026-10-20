#nullable enable

using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoVfxBuilder;

namespace Nex.Ninjago.Editor
{
    /// <summary>The chapter's own effects, registered in the VfxManager prefab: the brick rebuild and the link dust.</summary>
    public static class ChapterVfxBuilder
    {
        #region Entry Point

        public static void Build()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var rebuild = Save("Vfx_BrickRebuild", BuildRebuild(brick, cube));
            var dust = Save("Vfx_LinkDust", BuildDust(soft));
            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.BrickRebuild, rebuild, 2, 6);
                Register(dict, VfxManager.VisualEffect.LinkDust, dust, 2, 4);
            });
        }

        #endregion

        #region Effects

        // Colored bricks start on a wide shell and fly together into the center, spinning, then snap in place.
        static ParticleSystem BuildRebuild(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_BrickRebuild", material, 1.2f, false, 60);
            var main = system.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.25f), new Color(0.3f, 0.6f, 1f));
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            Burst(system, 44);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.4f;
            shape.radiusThickness = 0f;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, -1f), new Keyframe(0.6f, -4.5f), new Keyframe(0.85f, 0f), new Keyframe(1f, 0f)));
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            MeshRenderer(system, cube);
            return system;
        }

        // A low dust ring kicked up by a landing, a vehicle or a fleeing enemy.
        static ParticleSystem BuildDust(Material material)
        {
            var system = NewSystem("Vfx_LinkDust", material, 0.9f, false, 60);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new Color(0.86f, 0.8f, 0.68f, 0.8f);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            Burst(system, 36);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            GrowOverLife(system, 2f);
            return system;
        }

        #endregion
    }
}
