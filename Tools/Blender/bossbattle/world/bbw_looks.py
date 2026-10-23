"""Review-render look presets (pure Python: imported by the Blender renderer and the venv post step).

Values follow GDD 12 / TDD 6 and the starting values of research/hd2d-rendering.md 4.6-4.7 (the rendering
package R owns the real ones; these only make the previews judge composition under the intended light):
ToonLit M_BB_Palette constants = MaterialsBuilder.BuildPalette (bands 4, shadow tint (0.34, 0.32, 0.5), ambient
strength 0.35, rim (1, 0.95, 0.85) A 0.55 power 3, emission x 1.6). All colours are gamma-space (the project is
Gamma), so the renderer works on raw sRGB values like the game shader.
"""

MATERIAL = dict(shadow_tint=(0.34, 0.32, 0.5), ambient_strength=0.35, rim=(1.0, 0.95, 0.85), rim_a=0.55,
                rim_power=3.0, emission=1.6)

DUSK = dict(
    name="dusk",
    sun_color=(1.0, 0.62, 0.36), sun_intensity=1.35, sun_euler=(32.0, 215.0, 0.0),        # GDD 4: low, ahead, right
    ambient=dict(sky=(0.55, 0.42, 0.58), equator=(0.5, 0.34, 0.3), ground=(0.2, 0.14, 0.14)),
    fog_color=(1.0, 0.62, 0.4), fog_density=0.006,                                         # GDD 12
    clear=(0.98, 0.6, 0.42),
    sky_gradient=((0.0, (0.99, 0.66, 0.42)), (0.45, (0.93, 0.47, 0.38)), (1.0, (0.42, 0.25, 0.42))),
    shaft_color=(1.0, 0.72, 0.4), shaft_intensity=1.4,
    camera=dict(pos=(0.0, 4.0, -11.0), pitch=9.0, fov=50.0),                              # TDD 3.2 Road pose
    tilt=(0.55, 0.30),                                                                      # centre (0 = bottom), half width
    volume={
        "Bloom": {"threshold": 1.0, "intensity": 1.0, "scatter": 0.7, "tint": (1.0, 0.86, 0.66)},
        "WhiteBalance": {"temperature": 18.0, "tint": 4.0},
        "ColorAdjustments": {"contrast": 22.0, "saturation": 8.0, "postExposure": 0.0},
        "SplitToning": {"shadows": (0.30, 0.24, 0.50), "highlights": (1.0, 0.78, 0.52), "balance": 10.0},
        "Tonemapping": {"mode": 1},
    },
    vignette=0.3,
)

NIGHT = dict(
    name="night",
    sun_color=(0.55, 0.62, 1.0), sun_intensity=0.7, sun_euler=(52.0, -120.0, 0.0),        # moon key
    ambient=dict(sky=(0.12, 0.14, 0.28), equator=(0.08, 0.08, 0.16), ground=(0.03, 0.03, 0.06)),
    fog_color=(0.04, 0.05, 0.12), fog_density=0.008,
    clear=(0.02, 0.02, 0.06),
    sky_gradient=((0.0, (0.08, 0.06, 0.16)), (0.5, (0.04, 0.035, 0.1)), (1.0, (0.015, 0.015, 0.05))),
    shaft_color=(0.6, 0.72, 1.0), shaft_intensity=0.5,
    camera=dict(pos=(0.0, 3.4, -8.0), pitch=2.0, fov=50.0),                               # TDD 3.2 Sky pose
    tilt=(0.52, 0.25),
    volume={
        "Bloom": {"threshold": 1.0, "intensity": 1.0, "scatter": 0.7, "tint": (0.8, 0.86, 1.0)},
        "WhiteBalance": {"temperature": -22.0, "tint": 6.0},
        "ColorAdjustments": {"contrast": 28.0, "saturation": -8.0, "postExposure": -0.2},
        "SplitToning": {"shadows": (0.18, 0.22, 0.60), "highlights": (0.70, 0.85, 1.0), "balance": -18.0},
        "Tonemapping": {"mode": 1},
    },
    vignette=0.38,
)

# piece close-ups: plain white key from the camera side, soft neutral fill, no fog / grade beyond Neutral tonemap
NEUTRAL = dict(
    name="neutral",
    sun_color=(1.0, 0.97, 0.92), sun_intensity=1.15, sun_euler=(45.0, 30.0, 0.0),
    ambient=dict(sky=(0.6, 0.62, 0.7), equator=(0.5, 0.5, 0.55), ground=(0.25, 0.24, 0.28)),
    fog_color=(0.0, 0.0, 0.0), fog_density=0.0,
    clear=(0.16, 0.15, 0.2),
    sky_gradient=None,
    shaft_color=(1.0, 1.0, 1.0), shaft_intensity=0.0,
    camera=None, tilt=None,
    volume={"Bloom": {"threshold": 1.0, "intensity": 0.6, "scatter": 0.7}, "Tonemapping": {"mode": 1}},
    vignette=0.0,
)

LOOKS = {"dusk": DUSK, "night": NIGHT, "neutral": NEUTRAL}
