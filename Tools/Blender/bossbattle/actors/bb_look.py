"""Review-render helpers for the Boss Battle actors (Blender side; nothing here is a game asset).

* `outline_material` / `add_hull`: the inverted-hull outline of Tools/Blender/enemies/bl_look.py, but the hull never
  shadows its own model (backface-culled in the shadow pass). bl_look's hull casts a shadow over the whole mesh, which
  EEVEE resolves as diagonal hatching on every lit face.
* `tinted_toon`: bl_look toon whose emission is emission_map x tint x gain - the per-attack tell colour that BossActor
  drives on the tell renderers (_EmissionColor on a material instance).
* `pass_materials`: the raw data passes (albedo, emission, light, normal) that preview_compose.py re-shades with the
  ToonLit formula + URP post (the in-game look emulation used for the game-camera previews).
* Unity <-> Blender conversion for scene placement: the FBX round trip maps Blender (x, y, z) <-> Unity (-x, z, -y).
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402

sys.path.insert(0, bb_paths.ENEMIES_KIT)
import bl_look as look  # noqa: E402

OUTLINE_RGB = (0.05, 0.045, 0.07)


# ------------------------------------------------------------------ space conversion
def u2b(v):
    """Unity position/direction -> Blender (FBX round-trip convention)."""
    return Vector((-v[0], -v[2], v[1]))


def b2u(v):
    return Vector((-v[0], v[2], -v[1]))


def unity_euler_forward(euler_deg):
    """Forward (+Z) of a Unity transform with this Euler (Z, then X, then Y), in Unity space."""
    x, y = math.radians(euler_deg[0]), math.radians(euler_deg[1])
    return Vector((math.sin(y) * math.cos(x), -math.sin(x), math.cos(y) * math.cos(x)))


# ------------------------------------------------------------------ materials
def outline_material(name="M_BBOutline", rgb=OUTLINE_RGB):
    mat = look.outline_material(name, rgb)
    mat.use_backface_culling_shadow = True  # the flipped hull never shades its own model (no hatching)
    return mat


def add_hull(obj, mat, thickness):
    look.add_hull(obj, mat, thickness)


def toon(name, palette_png, emission_png, gain=1.6, ambient=(0.12, 0.11, 0.14), tint=(1.0, 0.97, 0.92)):
    return look.toon_material(name, palette_png, emission_png, bands=look.BANDS4, tint=tint, ambient=ambient,
                              emit_gain=gain)


def tinted_toon(name, palette_png, emission_png, rgb, gain):
    """Toon whose emission term is emission_map x rgb x gain (BossActor's tell tint on a material instance)."""
    mat = toon(name, palette_png, emission_png, gain)
    nt = mat.node_tree
    tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image and "Emission" in n.image.name)
    for link in list(tex.outputs["Color"].links):
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.clamp_result = False
        mul.inputs["Factor"].default_value = 1.0
        mul.inputs[7].default_value = tuple(rgb) + (1.0,)
        nt.links.new(tex.outputs["Color"], mul.inputs[6])
        nt.links.new(mul.outputs[2], link.to_socket)
    return mat


def tint_emission_pass(name, emission_png, rgb):
    """'emis' data pass for a tell renderer lit with `rgb` (emission map x rgb)."""
    mat = look.pass_material(name, "emis", emission_png=emission_png)
    nt = mat.node_tree
    tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
    em = next(n for n in nt.nodes if n.type == "EMISSION")
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.clamp_result = False
    mul.inputs["Factor"].default_value = 1.0
    mul.inputs[7].default_value = tuple(rgb) + (1.0,)
    nt.links.new(tex.outputs["Color"], mul.inputs[6])
    nt.links.new(mul.outputs[2], em.inputs["Color"])
    return mat


def pass_materials(palette_png, emission_png):
    return {
        "albedo": look.pass_material("P_Albedo", "albedo", albedo=palette_png),
        "emis": look.pass_material("P_Emis", "emis", emission_png=emission_png),
        "emis_off": look.pass_material("P_EmisOff", "emis", emission_png=None),
        "light": look.pass_material("P_Light", "light"),
        "normal": look.pass_material("P_Normal", "normal"),
    }


def flat_pass_materials(rgb, name):
    """Pass materials for context geometry with a flat albedo (ground, road) and no emission."""
    return {
        "albedo": look.pass_material(name + "_Albedo", "albedo", albedo=rgb),
        "emis": look.pass_material(name + "_Emis", "emis", emission_png=None),
        "light": look.pass_material(name + "_Light", "light"),
        "normal": look.pass_material(name + "_Normal", "normal"),
    }


def set_all_materials(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ------------------------------------------------------------------ scene
def unity_camera(scene, pos_u, pitch_deg, fov_deg, yaw_deg=0.0, name="GameCam"):
    """Camera at a Unity position with a Unity pitch (down positive) / yaw and vertical FOV."""
    fwd_u = unity_euler_forward((pitch_deg, yaw_deg, 0.0))
    loc = u2b(pos_u)
    return look.look_camera(scene, loc, loc + u2b(fwd_u), lens_fov_deg=fov_deg, name=name)


def unity_sun(scene, euler_deg, intensity=1.0, name="Sun"):
    """Directional light with a Unity Euler; returns (object, direction TOWARD the light in Blender space)."""
    fwd_b = u2b(unity_euler_forward(euler_deg)).normalized()
    obj = look.sun(scene, fwd_b, intensity, name)
    return obj, -fwd_b


def import_fbx(path):
    """Import one FBX; returns {node name: object} (names as written in the file, before any .001 suffix)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path))
    new = [o for o in bpy.data.objects if o not in before]
    out = {}
    for o in new:
        out[o.name.split(".")[0]] = o
    return out


def root_of(objs):
    return next(o for o in objs.values() if o.parent is None or o.parent not in objs.values())


def ground_plane(scene, name, x0, x1, y0, y1, z=0.0):
    import bmesh
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    bm.faces.new(vs)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    return obj


def pose_local_euler_unity(obj, euler_u_deg):
    """Rotate an IMPORTED FBX part by a Unity local Euler (degrees, Unity order Z, X, Y). Blender's importer keeps
    the FBX (Y-up, right-handed) frame on every node below the root (the root carries the +90 deg X conversion), and
    FBX -> Unity only mirrors X, so the Unity rotation in the FBX frame is M @ R @ M with M = diag(-1, 1, 1)."""
    ex, ey, ez = (math.radians(a) for a in euler_u_deg)
    ru = Matrix.Rotation(ey, 3, "Y") @ Matrix.Rotation(ex, 3, "X") @ Matrix.Rotation(ez, 3, "Z")
    m = Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1)))
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = (m @ ru @ m).to_quaternion()
