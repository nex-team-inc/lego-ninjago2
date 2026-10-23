"""Build ONE Boss Battle actor headless: geometry -> contract checks -> FBX (replaced only if changed) -> stats JSON
-> optional review renders (turntable views, cel look + hull outline).

Normally driven by build_bb_actors.py. Manual use:
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python Tools/Blender/bossbattle/actors/bl_build_actor.py -- --model BB_Boss \
      --fbx ninjago-unity/Assets/Models/Ninjago/BossBattle/Actors/BB_Boss.fbx --review-dir /tmp/bb_review
Prints one line `BB_ACTOR_STATS {json}` (also written to --stats).
"""
import argparse
import json
import math
import os
import sys
import time

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402

sys.path.insert(0, bb_paths.ENEMIES_KIT)
sys.path.insert(0, bb_paths.TOOLS_BLENDER)
import bl_common as bc  # noqa: E402
import bl_look as look  # noqa: E402
import ekit  # noqa: E402
import bb_look as bl  # noqa: E402
import contract_bb_actors as contract  # noqa: E402
import models_bb_actors as models  # noqa: E402


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--model", required=True, choices=sorted(models.REGISTRY))
    p.add_argument("--palette-json", default=bb_paths.PALETTE_JSON)
    p.add_argument("--palette-png", default=bb_paths.PALETTE_PNG)
    p.add_argument("--fbx", help="output .fbx (omit for a dry run)")
    p.add_argument("--stats", help="write the stats JSON here")
    p.add_argument("--review-dir", help="render review views <model>_<view>.png into this folder")
    p.add_argument("--review-size", type=int, default=420)
    p.add_argument("--blend", help="optional .blend dump for manual inspection")
    p.add_argument("--lenient", action="store_true", help="dev only: warn instead of failing on the tri budget")
    return p.parse_args(argv)


# ------------------------------------------------------------------ checks
def check_contract(model, objs, lenient):
    want = contract.HIERARCHY[model.name]
    have = {p.name: p.parent for p in model.parts}
    if have != want:
        raise RuntimeError(f"{model.name}: hierarchy {have} != contract {want}")
    tris = model.tris()
    if tris > model.budget:
        msg = f"{model.name}: {tris} tris > budget {model.budget}"
        if not lenient:
            raise RuntimeError(msg)
        print("WARNING " + msg)
    for o in objs.values():
        if any(abs(a) > 1e-9 for a in o.rotation_euler) or any(abs(s - 1) > 1e-9 for s in o.scale):
            raise RuntimeError(f"{model.name}/{o.name}: rotation/scale must be baked")
    pts = world_points(objs)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    kind = contract.PIVOT[model.name]
    if kind in ("feet", "bottom-centre") and lo.z < -0.02:
        raise RuntimeError(f"{model.name}: geometry below the pivot (z={lo.z:.3f})")
    if kind == "centre" and abs(lo.z + hi.z) / 2 > 0.12:
        raise RuntimeError(f"{model.name}: centred pivot expected, vertical centre {(lo.z + hi.z) / 2:.3f}")
    # Unity size (x width, y height, z length) vs the brief
    size_u = (hi.x - lo.x, hi.z - lo.z, hi.y - lo.y)
    want_size, tol = contract.SIZE[model.name]
    for axis, (got, exp) in enumerate(zip(size_u, want_size)):
        if exp is not None and abs(got - exp) > tol:
            msg = f"{model.name}: size axis {'xyz'[axis]} = {got:.2f} m, expected {exp} +- {tol}"
            if not lenient:
                raise RuntimeError(msg)
            print("WARNING " + msg)
    if model.facing != contract.FACING[model.name]:
        raise RuntimeError(f"{model.name}: facing {model.facing} != {contract.FACING[model.name]}")
    if any(not ok_uv(o) for o in objs.values() if o.type == "MESH"):
        raise RuntimeError(f"{model.name}: UVs off the palette texel centres")
    return lo, hi


def ok_uv(obj):
    w, h = 32, 16
    uv = obj.data.uv_layers.active
    for d in uv.data:
        u, v = d.uv
        if abs(u * w - 0.5 - round(u * w - 0.5)) > 1e-3 or abs(v * h - 0.5 - round(v * h - 0.5)) > 1e-3:
            return False
    return True


def world_points(objs):
    bpy.context.view_layer.update()
    return [o.matrix_world @ v.co for o in objs.values() if o.type == "MESH" for v in o.data.vertices]


# ------------------------------------------------------------------ review renders
def front_vector(model):
    """Blender direction the model faces: Unity -Z = Blender +Y, Unity +Z = Blender -Y."""
    return Vector((0.0, 1.0, 0.0)) if model.facing == "-Z" else Vector((0.0, -1.0, 0.0))


REVIEW_VIEWS = (("front", 0.0, 8.0), ("threeq", -35.0, 16.0), ("side", -90.0, 8.0), ("back", 150.0, 18.0),
                ("top", 20.0, 62.0), ("threeq2", 35.0, 16.0))


def set_material(obj, mat):
    """Swap the base slot (slot 0) keeping the hull outline slot."""
    obj.data.materials[0] = mat


def render_review(model, objs, args):
    scene = bpy.context.scene
    emis = bb_paths.PALETTE_EMISSION_PNG
    toon = look.toon_material("M_ReviewToon", args.palette_png, emis, bands=look.BANDS4, tint=(1.0, 0.97, 0.92),
                              ambient=(0.12, 0.11, 0.14), emit_gain=1.6)
    # tell renderers rest with emission off (wrapper prefab material M_BB_Palette_Tell, _EmissionColor black)
    toon_rest = look.toon_material("M_ReviewToonRest", args.palette_png, None, bands=look.BANDS4,
                                   tint=(1.0, 0.97, 0.92), ambient=(0.12, 0.11, 0.14))
    tell_parts = set(model.notes.get("tell_parts", {}))
    outline = bl.outline_material("M_ReviewOutline")
    meshes = [o for o in objs.values() if o.type == "MESH"]
    lo, hi = model_bounds(objs)
    size = (hi - lo).length
    for o in meshes:
        o.data.materials.clear()
        o.data.materials.append(toon_rest if o.name in tell_parts else toon)
        bl.add_hull(o, outline, size * 0.0035)
    look.setup_eevee(scene, args.review_size, args.review_size, pixel=False, samples=16)
    fv = front_vector(model)
    views = list(REVIEW_VIEWS)
    for name, yaw_d, pitch_d in views:
        _render_view(scene, model, meshes, fv, yaw_d, pitch_d, os.path.join(args.review_dir, f"{model.name}_{name}.png"))
    if tell_parts:  # tell demo: rock tell (orange) on the fists, wind tell (violet) in the mouth, jaw open 22 deg
        orange = bl.tinted_toon("M_TellOrange", args.palette_png, emis, (1.0, 0.45, 0.08), 3.0)
        violet = bl.tinted_toon("M_TellViolet", args.palette_png, emis, (0.7, 0.3, 1.0), 3.0)
        for part in tell_parts:
            set_material(objs[part], violet if part == "Head" else orange)
        if "Jaw" in objs:
            objs["Jaw"].rotation_euler = (math.radians(-22.0), 0.0, 0.0)
        bpy.context.view_layer.update()
        _render_view(scene, model, meshes, fv, -20.0, 8.0, os.path.join(args.review_dir, f"{model.name}_tell.png"))
        for part in tell_parts:
            set_material(objs[part], toon_rest)
        if "Jaw" in objs:
            objs["Jaw"].rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.view_layer.update()
    if model.name == "BB_Boss":  # wings spread (phase 2 flying pose) about their pivots
        spread = model.notes["wings"]["spread_blender_z_deg"]
        for part, deg in spread.items():
            objs[part].rotation_euler = (0.0, 0.0, math.radians(deg))
        bpy.context.view_layer.update()
        for name, yaw_d, pitch_d in (("spread_front", 0.0, 6.0), ("spread_threeq", -35.0, 14.0),
                                     ("spread_back", 160.0, 20.0)):
            _render_view(scene, model, meshes, fv, yaw_d, pitch_d,
                         os.path.join(args.review_dir, f"{model.name}_{name}.png"))
        for part in spread:
            objs[part].rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.view_layer.update()


def model_bounds(objs):
    pts = world_points(objs)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def _render_view(scene, model, meshes, fv, yaw_d, pitch_d, path):
    for o in list(scene.objects):
        if o.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(o)
    yaw, pitch = math.radians(yaw_d), math.radians(pitch_d)
    rot = Matrix.Rotation(yaw, 3, "Z")
    flat = rot @ fv
    cam_from = Vector((flat.x * math.cos(pitch), flat.y * math.cos(pitch), math.sin(pitch))).normalized()
    # key light from the camera's upper left, a little from the front
    right = cam_from.cross(Vector((0, 0, 1))).normalized()
    light_from = (cam_from * 0.55 - right * 0.6 + Vector((0, 0, 0.9))).normalized()
    look.sun(scene, -light_from, 1.15)
    look.frame_ortho(scene, meshes, -cam_from, fill=0.9)
    look.render(scene, path)


# ------------------------------------------------------------------ main
def main():
    args = parse()
    t0 = time.time()
    bc.reset_scene()
    bc.load_palette(args.palette_json)
    mat, *_ = bc.palette_material(args.palette_png)
    model = models.REGISTRY[args.model]()
    objs = model.build(mat)
    lo, hi = check_contract(model, objs, args.lenient)
    ordered = [objs[p.name] for p in model.parts]

    if model.name == "BB_Boss":
        fold = models.WING_FOLD
        # authored left wing (+X before the facing turn) folded by +fold about Z; spreading turns it back by -fold.
        # Rotations about the vertical axis commute with the 180 deg facing turn, so the same Blender angles hold.
        model.notes["wings"]["spread_blender_z_deg"] = {"WingL": -fold, "WingR": fold}
    stats = {
        "model": model.name, "tris": model.tris(), "budget": model.budget, "facing_unity": model.facing,
        "pivot": contract.PIVOT[model.name], "parts": model.stats(), "anchors": model.anchor_stats(),
        "notes": model.notes, "shadow_casters": model.shadow_casters,
        "bounds_min_blender": [round(c, 4) for c in lo], "bounds_max_blender": [round(c, 4) for c in hi],
        "size_unity": [round(hi.x - lo.x, 3), round(hi.z - lo.z, 3), round(hi.y - lo.y, 3)],
        "emissive_faces": sum(p["emissive_faces"] for p in model.stats()),
    }
    if args.fbx:
        seconds, changed = ekit.export_fbx(args.fbx, ordered)
        stats.update(fbx=os.path.abspath(args.fbx), fbx_changed=changed, fbx_bytes=os.path.getsize(args.fbx))
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
    if args.review_dir:
        render_review(model, objs, args)
    stats["seconds"] = round(time.time() - t0, 2)
    line = json.dumps(stats, sort_keys=True)
    if args.stats:
        os.makedirs(os.path.dirname(os.path.abspath(args.stats)), exist_ok=True)
        with open(args.stats, "w") as f:
            f.write(line)
    print("BB_ACTOR_STATS " + line)


main()
