#nullable enable

using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoVfxBuilder;

namespace Nex.Ninjago.Editor
{
    /// <summary>Robot Mecha particle prefabs, registered in the VfxManager prefab (it pools them). Kept light for frame rate.</summary>
    public static class RobotMechaVfxBuilder
    {
        static readonly Color laserPink = new(1f, 0.35f, 0.8f, 1f);

        #region Entry Point

        public static void Build()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var muzzle = Save("Vfx_LaserMuzzle", BuildMuzzle(soft));
            var impact = Save("Vfx_LaserImpact", BuildImpact(soft));
            var explosion = Save("Vfx_EnemyExplosion", BuildExplosion(brick, soft, cube));
            var hit = Save("Vfx_MechaHit", BuildHit(brick, cube));
            var powerDown = Save("Vfx_MechaPowerDown", BuildPuff("Vfx_MechaPowerDown", soft, new Color(0.45f, 0.45f, 0.5f, 0.8f), 14, 0.9f, 1.2f));
            var powerUp = Save("Vfx_MechaPowerUp", BuildPowerUp(soft));
            var fizzle = Save("Vfx_ArrowFizzle", BuildPuff("Vfx_ArrowFizzle", soft, new Color(0.85f, 0.85f, 0.8f, 0.85f), 10, 0.45f, 0.4f));

            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.LaserMuzzle, muzzle, 2, 4);
                Register(dict, VfxManager.VisualEffect.LaserImpact, impact, 2, 4);
                Register(dict, VfxManager.VisualEffect.EnemyExplosion, explosion, 6, 12);
                Register(dict, VfxManager.VisualEffect.MechaHit, hit, 2, 4);
                Register(dict, VfxManager.VisualEffect.MechaPowerDown, powerDown, 2, 6);
                Register(dict, VfxManager.VisualEffect.MechaPowerUp, powerUp, 2, 6);
                Register(dict, VfxManager.VisualEffect.ArrowFizzle, fizzle, 2, 6);
            });
        }

        #endregion

        #region Effects

        // Looping pink sparkle at the chest cannon, streaming along the beam (the system's forward).
        static ParticleSystem BuildMuzzle(Material material)
        {
            var system = NewSystem("Vfx_LaserMuzzle", material, 1f, true, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = laserPink;
            var emission = system.emission;
            emission.rateOverTime = 35f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.15f;
            FadeOut(system);
            GrowOverLife(system, 0.3f);
            return system;
        }

        // Looping sparks where the beam burns an enemy or the ground.
        static ParticleSystem BuildImpact(Material material)
        {
            var system = NewSystem("Vfx_LaserImpact", material, 1f, true, 50);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(laserPink, Color.white);
            main.gravityModifier = 0.6f;
            var emission = system.emission;
            emission.rateOverTime = 45f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            FadeOut(system);
            return system;
        }

        // Bricks blasting out in every direction (MechaField tints them per enemy type) with a short white flash.
        static ParticleSystem BuildExplosion(Material brick, Material soft, Mesh cube)
        {
            var system = NewSystem("Vfx_EnemyExplosion", brick, 1.2f, false, 24);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.gravityModifier = 1.6f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 18);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            MeshRenderer(system, cube);

            var flash = NewSystem("Flash", soft, 0.3f, false, 2);
            flash.transform.SetParent(system.transform, false);
            var flashMain = flash.main;
            flashMain.startLifetime = 0.18f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 3.2f;
            flashMain.startColor = new Color(1f, 0.95f, 0.85f, 0.9f);
            Burst(flash, 1);
            var flashShape = flash.shape;
            flashShape.enabled = false;
            FadeOut(flash);
            GrowOverLife(flash, 1.6f);
            return system;
        }

        // White and lavender bricks pop off the mecha and snap back.
        static ParticleSystem BuildHit(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_MechaHit", material, 0.8f, false, 16);
            var main = system.main;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.8f, 0.7f, 1f));
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 10);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 8f), new Keyframe(0.45f, 0f), new Keyframe(1f, -11f)));
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            MeshRenderer(system, cube);
            return system;
        }

        // Soft puff rising (pose lost: a little smoke off the mecha; arrow cancelled: a grey poof).
        static ParticleSystem BuildPuff(string name, Material material, Color color, int count, float lifetime, float size)
        {
            var system = NewSystem(name, material, lifetime + 0.2f, false, count + 4);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = -0.25f;
            Burst(system, count);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            FadeOut(system);
            GrowOverLife(system, 1.8f);
            return system;
        }

        // Pink ring bursting out along the ground when both arms make the L again.
        static ParticleSystem BuildPowerUp(Material material)
        {
            var system = NewSystem("Vfx_MechaPowerUp", material, 0.8f, false, 50);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startColor = laserPink;
            Burst(system, 36);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            return system;
        }

        #endregion
    }
}
