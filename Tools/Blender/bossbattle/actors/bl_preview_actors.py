"""Game-camera readability previews + the boss portrait raw render, from the EXPORTED FBX files (so the previews also
prove the FBX round trip). Review only: everything is written to --out-dir (the session scratchpad), never the repo.

Scenes (Unity coordinates, TDD 3.2 camera poses, GDD 4 / 6 placements):
  road  : camera (0, 4, -11) pitch 9 FOV 50, car at the origin (+Z), minions on both roof corners, Titan at z = 30
          facing the car (wings folded), dusk sun Euler (32, 215, 0) = back light.
  sky   : camera (0, 3.4, -8) pitch 2 FOV 50, jet at (0, 3.2, 0), Titan hovering at z = 30 with the Core on the
          plane's height, wings spread (manifest spread angles), jets lit; night moon Euler (52, -120, 0).
For each scene the raw data passes (albedo, emis, tell, light, normal; 640 x 360, hard edges = the world RT) are
written as <scene>_<pass>.png; preview_compose.py (venv) re-shades them with the ToonLit formula + URP post.
`--portrait` additionally renders the boss head, front view, ortho, hard edged at the sprite size.

  Blender -b --factory-startup --python-exit-code 1 --python bl_preview_actors.py -- --fbx-dir <Actors> \
      --manifest bb_actors_manifest.json --out-dir <scratch>/game [--portrait <scratch>/portrait_raw.png]
"""
import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402
import bb_look as bl  # noqa: E402

sys.path.insert(0, bb_paths.ENEMIES_KIT)
import bl_look as look  # noqa: E402

W, H = 640, 360
ROAD_CAM = ((0.0, 4.0, -11.0), 9.0, 50.0)
SKY_CAM = ((0.0, 3.4, -8.0), 2.0, 50.0)
DUSK_SUN = (32.0, 215.0, 0.0)
NIGHT_MOON = (52.0, -120.0, 0.0)
BOSS_Z = 30.0
# tell colours (raw, <= 1; preview_compose.py applies the HDR gain): rock = orange, minion = green, slam = red,
# wind = violet (GDD 4.1-4.3, 6.1)
TELLS = {"rock": (1.0, 0.45, 0.08), "minion": (0.3, 1.0, 0.35), "slam": (1.0, 0.15, 0.1), "wind": (0.7, 0.3, 1.0)}


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--fbx-dir", default=bb_paths.MODELS_OUT)
    p.add_argument("--manifest", default=bb_paths.MANIFEST)
    p.add_argument("--out-dir", required=True)
    p.add_argument("--scenes", default="road,sky")
    p.add_argument("--portrait", help="raw portrait PNG (hard-edged, transparent)")
    p.add_argument("--portrait-size", type=int, default=64)
    return p.parse_args(argv)


# ------------------------------------------------------------------ scene assembly
class Actor:
    def __init__(self, model, fbx_dir):
        self.model = model
        self.objs = bl.import_fbx(os.path.join(fbx_dir, model + ".fbx"))
        self.root = self.objs["Body"]

    def place(self, pos_u):
        self.root.location = bl.u2b(pos_u)

    def meshes(self):
        return [o for o in self.objs.values() if o.type == "MESH"]


def build_scene(kind, args, manifest):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    actors = {}
    context = []  # (object, flat albedo rgb)
    boss = actors["boss"] = Actor("BB_Boss", args.fbx_dir)
    if kind == "road":
        car = actors["car"] = Actor("BB_NinjaCar", args.fbx_dir)
        car.place((0.0, 0.0, 0.0))
        boss.place((0.0, 0.0, BOSS_Z))
        anchors = manifest["BB_NinjaCar"]["anchors"]
        for side in ("L", "R"):
            mn = actors["minion" + side] = Actor("BB_Minion", args.fbx_dir)
            mn.place(tuple(anchors["RoofCorner" + side]["model_unity"]))
        context.append((bl.ground_plane(scene, "Road", -3.2, 3.2, -160.0, 14.0), (0.26, 0.22, 0.25)))
        for x0, x1 in ((-60.0, -3.2), (3.2, 60.0)):
            context.append((bl.ground_plane(scene, "Shoulder", x0, x1, -160.0, 14.0, -0.01), (0.56, 0.38, 0.28)))
        cam = ROAD_CAM
        sun_euler = DUSK_SUN
    else:
        jet = actors["jet"] = Actor("BB_NinjaJet", args.fbx_dir)
        jet.place((-1.2, 2.5, 0.0))  # off the boss core so both read
        core_y = manifest["BB_Boss"]["anchors"]["CorePoint"]["model_unity"][1]
        boss.place((0.0, 3.2 - core_y + 1.2, BOSS_Z))
        spread = manifest["BB_Boss"]["notes"]["wings"]["spread_unity_local_euler"]
        for part, e in spread.items():
            bl.pose_local_euler_unity(boss.objs[part], e)
        cam = SKY_CAM
        sun_euler = NIGHT_MOON
    bpy.context.view_layer.update()
    bl.unity_camera(scene, *cam)
    sun_obj, to_light = bl.unity_sun(scene, sun_euler, 1.0)
    look.setup_eevee(scene, W, H, pixel=True)
    return scene, actors, context, to_light


def render_passes(kind, scene, actors, context, args, manifest):
    pal, emis_png = bb_paths.PALETTE_PNG, bb_paths.PALETTE_EMISSION_PNG
    pm = bl.pass_materials(pal, emis_png)
    tell_parts = set(manifest["BB_Boss"]["notes"].get("tell_parts", {}))
    ctx_mats = {}
    for i, (obj, rgb) in enumerate(context):
        ctx_mats[obj.name] = bl.flat_pass_materials(rgb, f"Ctx{i}")
    tell_rgb = TELLS["rock"] if kind == "road" else TELLS["wind"]
    tell_mat = bl.tint_emission_pass("P_Tell", emis_png, tell_rgb)
    black = look.pass_material("P_Black", "emis", emission_png=None)
    for pname in ("albedo", "emis", "tell", "light", "normal"):
        for key, actor in actors.items():
            for o in actor.meshes():
                if pname == "tell":
                    mat = tell_mat if (key == "boss" and o.name.split(".")[0] in demo_tells(kind)) else black
                elif pname == "emis" and key == "boss" and o.name.split(".")[0] in tell_parts:
                    mat = pm["emis_off"]  # tell renderers rest dark (M_BB_Palette_Tell, emission 0)
                else:
                    mat = pm[pname]
                bl.set_all_materials(o, mat)
        for obj, _ in context:
            bl.set_all_materials(obj, ctx_mats[obj.name]["emis" if pname == "tell" else pname])
        scene.eevee.use_shadows = pname == "light"
        look.render(scene, os.path.join(args.out_dir, f"{kind}_{pname}.png"))


def demo_tells(kind):
    return ("FistL", "FistR") if kind == "road" else ("Head",)


def boss_screen_box(scene, actors):
    """Projected 2D box (pixels, top-left origin) of the boss + the vehicle, for the composer's crops."""
    from bpy_extras.object_utils import world_to_camera_view
    out = {}
    for key, actor in actors.items():
        xs, ys = [], []
        for o in actor.meshes():
            for v in o.data.vertices:
                p = world_to_camera_view(scene, scene.camera, o.matrix_world @ v.co)
                xs.append(p.x * W)
                ys.append((1.0 - p.y) * H)
        out[key] = [min(xs), min(ys), max(xs), max(ys)]
    return out


# ------------------------------------------------------------------ portrait
def render_portrait(args):
    """Boss head (Head + Jaw + horns + eyes), front view slightly from below like the road camera, ortho, hard
    edges at the sprite size; preview_compose.py adds the 1 px outline. Lit with a warm key from the front-left so
    the portrait reads on the menu (the game's back light would leave the face in the dark band)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    boss = Actor("BB_Boss", args.fbx_dir)
    keep = {"Head", "Jaw", "HornL", "HornR", "EyeL", "EyeR"}
    for o in list(boss.objs.values()):
        if o.type == "MESH" and o.name.split(".")[0] not in keep:
            o.hide_render = True
    meshes = [o for o in boss.meshes() if not o.hide_render]
    toon = bl.toon(f"M_Portrait", bb_paths.PALETTE_PNG, bb_paths.PALETTE_EMISSION_PNG, gain=1.6,
                   ambient=(0.2, 0.17, 0.24), tint=(1.08, 1.0, 0.92))
    for o in meshes:
        bl.set_all_materials(o, toon)
    size = args.portrait_size
    look.setup_eevee(scene, size, size, pixel=True)
    bpy.context.view_layer.update()
    # boss faces Unity -Z = Blender +Y: look from +Y toward -Y, a few degrees from below and the left
    yaw, pitch = math.radians(-12.0), math.radians(-6.0)
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), -math.sin(pitch)))
    look.frame_ortho(scene, meshes, d, fill=0.94)
    key_from = Vector((-0.55, 1.0, 0.75)).normalized()
    look.sun(scene, -key_from, 1.15)
    scene.eevee.use_shadows = True
    look.render(scene, args.portrait)


def main():
    args = parse()
    with open(args.manifest) as f:
        manifest = json.load(f)
    os.makedirs(args.out_dir, exist_ok=True)
    info = {}
    for kind in [s for s in args.scenes.split(",") if s]:
        scene, actors, context, to_light = build_scene(kind, args, manifest)
        render_passes(kind, scene, actors, context, args, manifest)
        cam = scene.camera
        view_back = (cam.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
        info[kind] = {"to_light": list(to_light), "view_dir": list(view_back), "boxes": boss_screen_box(scene, actors),
                      "tell_rgb": TELLS["rock"] if kind == "road" else TELLS["wind"]}
    with open(os.path.join(args.out_dir, "scenes.json"), "w") as f:
        json.dump(info, f, indent=1)
    if args.portrait:
        render_portrait(args)
    print("BB_PREVIEW_OK " + json.dumps(sorted(info)))


main()
