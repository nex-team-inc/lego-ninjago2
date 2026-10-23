"""Review renders of the Boss Battle world FBXs (Blender 5.2 EEVEE, headless). Review tooling only: nothing here
reaches Unity. The FBXs are re-imported (so every preview also validates the export: names, parents, identity
rotations, Unity-space bounds against the build stats) and shaded with a node emulation of BilliardRogue/ToonLit as
M_BB_Palette will configure it (4 bands, shadow tint, trilight ambient x 0.35, rim, palette emission x 1.6,
exponential fog), on raw gamma values like the Gamma-space project. bbw_post.py adds bloom, grade, tilt-shift,
vignette and the nearest-neighbour upscale.

    Blender -b --factory-startup --python-exit-code 1 --python bbw_render.py -- --mode pieces --out DIR \
        [--only BB_Brick,...] [--stats build_stats.json]
    Blender ... --python bbw_render.py -- --mode scene --scene road|sky|road_props|sky_props --out DIR
        [--width 640 --height 360]

Writes <out>/<name>.npy (float32 RGBA, rows top-down, gamma-space HDR) + <name>.json (look + camera info).
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bbw_paths as P  # noqa: E402
import bbw_kit as kit  # noqa: E402
import bbw_looks as LK  # noqa: E402
import bbw_layout as LAY  # noqa: E402

sys.path.insert(0, P.ENV_KIT)
import bpy  # noqa: E402
import numpy as np  # noqa: E402
import render_common as rc  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

import bl_common as bc  # noqa: E402


def u2b(x, y, z):
    return Vector((-x, -z, y))


def unity_forward(pitch, yaw):
    p, y = math.radians(pitch), math.radians(yaw)
    return (math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p))


# ------------------------------------------------------------------ scene setup
def reset(width, height, pixel=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.resolution_percentage = 100
    scene.render.filter_size = 0.0 if pixel else 1.5
    scene.eevee.taa_render_samples = 1 if pixel else 16
    if hasattr(scene.eevee, "use_shadows"):
        scene.eevee.use_shadows = True
    scene.render.film_transparent = True
    scene.display_settings.display_device = "sRGB"
    scene.view_settings.view_transform = "Raw"
    scene.view_settings.look = "None"
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "32"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0, 0, 0, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.0
    scene.world = world
    return scene


def add_sun(look):
    """White sun of 'intensity 1' (a face pointing at it gets ShaderToRGB = 1): colour and intensity are applied in
    the material, exactly like ToonLit's mainLight.color x ramp."""
    data = bpy.data.lights.new("Sun", "SUN")
    data.energy = math.pi
    data.angle = 0.0
    data.use_shadow = True
    for attr, val in (("shadow_filter_radius", 0.0), ("use_shadow_jitter", False),
                      ("shadow_maximum_resolution", 0.002)):
        if hasattr(data, attr):
            setattr(data, attr, val)
    obj = bpy.data.objects.new("Sun", data)
    ex, ey, _ = look["sun_euler"]
    d = u2b(*unity_forward(ex, ey)).normalized()
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(obj)
    return obj


def add_camera(pos, pitch, fov, yaw=0.0, near=0.3, far=140.0, ortho=None):
    data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", data)
    cam.location = u2b(*pos)
    cam.rotation_euler = u2b(*unity_forward(pitch, yaw)).to_track_quat("-Z", "Y").to_euler()
    if ortho:
        data.type = "ORTHO"
        data.ortho_scale = ortho
    else:
        data.sensor_fit = "VERTICAL"
        data.angle_y = math.radians(fov)
    data.clip_start, data.clip_end = near, far
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


# ------------------------------------------------------------------ ToonLit emulation
def _img(path):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    img.colorspace_settings.name = "Non-Color"
    return img


def toonlit(name, look, fog=True):
    m = LK.MATERIAL
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new

    def vm(op, a, b=None, c=None):
        n = N("ShaderNodeVectorMath")
        n.operation = op
        for sock, arg in zip(n.inputs, (a, b, c)):
            if arg is None:
                continue
            if isinstance(arg, (tuple, list)):
                sock.default_value = tuple(arg)
            elif isinstance(arg, float):
                sock.default_value = arg
            else:
                L(arg, sock)
        return n.outputs[0]

    def fm(op, a, b=None):
        n = N("ShaderNodeMath")
        n.operation = op
        for sock, arg in zip(n.inputs, (a, b)):
            if arg is None:
                continue
            if isinstance(arg, (int, float)):
                sock.default_value = float(arg)
            else:
                L(arg, sock)
        return n.outputs[0]

    def tex(path):
        t = N("ShaderNodeTexImage")
        t.image = _img(path)
        t.interpolation = "Closest"
        return t.outputs["Color"]

    out = N("ShaderNodeOutputMaterial")
    alb, emi = tex(P.PALETTE_PNG), tex(P.PALETTE_EMISSION_PNG)
    diffuse = N("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    s2 = N("ShaderNodeShaderToRGB")
    L(diffuse.outputs["BSDF"], s2.inputs["Shader"])
    bw = N("ShaderNodeRGBToBW")
    L(s2.outputs["Color"], bw.inputs["Color"])
    ramp = N("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    stops = ((0.0, 0.0), (1 / 6, 1 / 3), (0.5, 2 / 3), (5 / 6, 1.0))     # ToonLit ToonRamp, 4 bands
    els = ramp.color_ramp.elements
    while len(els) < len(stops):
        els.new(0.9)
    for el, (pos, val) in zip(els, stops):
        el.position = pos
        el.color = (val, val, val, 1.0)
    L(bw.outputs["Val"], ramp.inputs["Fac"])
    rampv = ramp.outputs["Color"]
    tint = m["shadow_tint"]
    sun = tuple(c * look["sun_intensity"] for c in look["sun_color"])
    light = vm("MULTIPLY", vm("MULTIPLY_ADD", rampv, tuple(1 - t for t in tint), tuple(tint)), sun)
    amb = rc._trilight(nt, dict(look["ambient"], intensity=m["ambient_strength"]))
    light = vm("ADD", light, amb)
    col = vm("MULTIPLY", alb, light)
    # rim: (1 - N.V)^p x A x max(ramp, 0.2)
    lw = N("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    rbw = N("ShaderNodeRGBToBW")
    L(rampv, rbw.inputs["Color"])
    rimf = fm("MULTIPLY", fm("MULTIPLY", fm("POWER", lw.outputs["Facing"], m["rim_power"]), m["rim_a"]),
              fm("MAXIMUM", rbw.outputs["Val"], 0.2))
    col = vm("ADD", col, _scale(nt, tuple(m["rim"]), rimf))
    col = vm("ADD", col, _scale(nt, emi, m["emission"]))
    if fog and look["fog_density"] > 0:
        camd = N("ShaderNodeCameraData")
        f = fm("EXPONENT", fm("MULTIPLY", camd.outputs["View Z Depth"], -look["fog_density"]))
        fogc = tuple(look["fog_color"])
        col = vm("ADD", fogc, _scale(nt, vm("SUBTRACT", col, fogc), f))
    em = N("ShaderNodeEmission")
    em.inputs["Strength"].default_value = 1.0
    L(col, em.inputs["Color"])
    L(em.outputs["Emission"], out.inputs["Surface"])
    return mat


def _scale(nt, vec, s):
    n = nt.nodes.new("ShaderNodeVectorMath")
    n.operation = "SCALE"
    if isinstance(vec, tuple):
        n.inputs[0].default_value = vec
    else:
        nt.links.new(vec, n.inputs[0])
    if isinstance(s, (int, float)):
        n.inputs["Scale"].default_value = float(s)
    else:
        nt.links.new(s, n.inputs["Scale"])
    return n.outputs[0]


# ------------------------------------------------------------------ FBX import + verification
def import_fbx(path, coll_name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path), bake_space_transform=True)
    objs = [o for o in bpy.data.objects if o not in before]
    coll = bpy.data.collections.new(coll_name)
    for o in objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
    return coll, objs


def verify_import(name, objs, expect):
    """Compare the re-imported FBX with the build stats: part names / parents, identity rotation + unit scale on
    every part, Unity-space bounds."""
    errs = []
    bpy.context.view_layer.update()
    by = {o.name.split(".")[0]: o for o in objs if o.type == "MESH"}
    want = {p["name"]: p for p in expect["parts"]}
    if set(by) != set(want):
        errs.append(f"{name}: parts {sorted(by)} != {sorted(want)}")
    for pn, o in by.items():
        if pn not in want:
            continue
        par = o.parent.name.split(".")[0] if o.parent else None
        if par != want[pn]["parent"]:
            errs.append(f"{name}/{pn}: parent {par} != {want[pn]['parent']}")
        r = o.matrix_local.to_3x3()
        if any(abs(r[i][j] - (1.0 if i == j else 0.0)) > 1e-4 for i in range(3) for j in range(3)):
            errs.append(f"{name}/{pn}: local rotation/scale not identity")
    pts = [o.matrix_world @ v.co for o in by.values() for v in o.data.vertices]
    lo = [min(-p.x for p in pts), min(p.z for p in pts), min(-p.y for p in pts)]
    hi = [max(-p.x for p in pts), max(p.z for p in pts), max(-p.y for p in pts)]
    for a, b in zip(lo + hi, expect["bounds_min_unity"] + expect["bounds_max_unity"]):
        if abs(a - b) > 0.01:
            errs.append(f"{name}: imported bounds {lo} {hi} != built {expect['bounds_min_unity']} "
                        f"{expect['bounds_max_unity']}")
            break
    return errs


def set_material(objs, mat):
    for o in objs:
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(mat)


def place(coll, pos, yaw=0.0, scale=1.0, name=None, pitch=0.0, roll=0.0):
    inst = bpy.data.objects.new(name or coll.name, None)
    inst.instance_type = "COLLECTION"
    inst.instance_collection = coll
    s = scale if isinstance(scale, (tuple, list)) else (scale, scale, scale)
    m = kit.U2B @ kit.M(pos, yaw, pitch, roll, s) @ kit.U2B.inverted()
    inst.matrix_world = m
    bpy.context.scene.collection.objects.link(inst)
    return inst


# ------------------------------------------------------------------ output
def render_npy(out_dir, name, meta):
    os.makedirs(out_dir, exist_ok=True)
    exr = os.path.join(out_dir, name + ".exr")
    scene = bpy.context.scene
    scene.render.filepath = exr
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(exr)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    np.save(os.path.join(out_dir, name + ".npy"), px.reshape(h, w, 4)[::-1].astype(np.float32))
    bpy.data.images.remove(img)
    os.remove(exr)
    with open(os.path.join(out_dir, name + ".json"), "w") as f:
        json.dump(meta, f, indent=1)


# ------------------------------------------------------------------ modes
def mode_pieces(a, stats):
    names = [n for n in a.only.split(",") if n] or sorted(stats)
    errors = []
    for name in names:
        reset(a.piece_size, a.piece_size * 3 // 4, pixel=False)
        look = LK.NEUTRAL
        mat = toonlit("M_Preview", look, fog=False)
        coll, objs = import_fbx(P.fbx_path(name), name)
        if name in stats:
            errors += verify_import(name, objs, stats[name])
        bpy.context.scene.collection.children.link(coll)
        set_material(objs, mat)
        hint = stats.get(name, {}).get("preview", {}) if stats else {}
        lo = Vector(stats[name]["bounds_min_unity"])
        hi = Vector(stats[name]["bounds_max_unity"])
        centre, size = (lo + hi) / 2, (hi - lo)
        radius = max(size.length / 2, 0.3)
        for vi, (yaw, pitch) in enumerate(hint.get("views", [(hint.get("yaw", 25), hint.get("pitch", 20)),
                                                            (hint.get("yaw", 25) + 180, hint.get("pitch", 20))])):
            for o in [o for o in bpy.data.objects if o.type in ("CAMERA", "LIGHT")]:
                bpy.data.objects.remove(o, do_unlink=True)
            fwd = Vector(unity_forward(pitch, yaw))
            dist = radius * 6
            pos = centre - fwd * dist
            add_camera(tuple(pos), pitch, 0, yaw, near=0.05, far=dist * 3, ortho=radius * 2.25)
            sun_look = dict(look, sun_euler=(max(30.0, pitch + 25.0), yaw + 35.0, 0.0))
            add_sun(sun_look)
            render_npy(a.out, f"{name}_v{vi}", {"name": name, "look": "neutral", "view": [yaw, pitch]})
    return errors


def mode_scene(a, stats):
    sc = LAY.SCENES[a.scene]
    look = LK.LOOKS[sc["look"]]
    reset(a.width, a.height, pixel=True)
    mat = toonlit("M_ToonLit", look, fog=True)
    colls = {}
    errors = []
    needed = sorted({it["piece"] for it in sc["items"]})
    for name in needed:
        path = LAY.resolve_fbx(name)
        if path is None:
            continue
        coll, objs = import_fbx(path, name)
        if name in stats:
            errors += verify_import(name, objs, stats[name])
        set_material(objs, mat)
        colls[name] = coll
    proxies = LAY.build_proxies(sc, colls, mat)
    count, missing = 0, set()
    for it in sc["items"]:
        coll = colls.get(it["piece"]) or proxies.get(it["piece"])
        if coll is None:
            missing.add(it["piece"])
            continue
        place(coll, it["pos"], it.get("yaw", 0.0), it.get("scale", 1.0), pitch=it.get("pitch", 0.0),
              roll=it.get("roll", 0.0))
        count += 1
    for sh in sc.get("shafts", []):
        add_shaft(sh, look)
    add_sun(look)
    cam = sc.get("camera") or look["camera"]
    add_camera(cam["pos"], cam["pitch"], cam["fov"], cam.get("yaw", 0.0))
    render_npy(a.out, a.scene, {"scene": a.scene, "look": sc["look"], "items": count, "missing": sorted(missing),
                                "proxies": sorted(proxies), "camera": cam})
    return errors


def add_shaft(sh, look):
    """God-ray stand-in for R's M_BB_LightShaft on Env_LightShaft geometry: an open frustum along the sun direction."""
    import bmesh
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    sides, r0, r1 = 8, sh["r"], sh["r"] * 1.7
    rows = []
    for t in (0.0, 0.5, 1.0):
        r = r0 + (r1 - r0) * t
        ring = [bm.verts.new(Vector((math.cos(2 * math.pi * (i % sides) / sides) * r,
                                     math.sin(2 * math.pi * (i % sides) / sides) * r, -t))) for i in range(sides + 1)]
        rows.append((ring, 1.0 - t))
    for (ra, va), (rb, vb) in zip(rows, rows[1:]):
        for i in range(sides):
            f = bm.faces.new((ra[i], rb[i], rb[i + 1], ra[i + 1]))
            for loop in f.loops:
                ring, v = (ra, va) if loop.vert in ra else (rb, vb)
                loop[uv].uv = (ring.index(loop.vert) / sides, v)
    me = bpy.data.meshes.new("Shaft")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Shaft", me)
    obj.visible_shadow = False
    me.materials.append(rc.light_shaft_material("M_Shaft", tuple(look["shaft_color"]),
                                                look["shaft_intensity"] * sh.get("intensity", 1.0)))
    ex, ey, _ = look["sun_euler"]
    d = u2b(*unity_forward(ex, ey)).normalized()
    rot = d.to_track_quat("-Z", "Y").to_matrix().to_4x4()
    obj.matrix_world = Matrix.Translation(u2b(*sh["top"])) @ rot @ Matrix.Diagonal((1.0, 1.0, sh["length"], 1.0))
    bpy.context.scene.collection.objects.link(obj)
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--mode", choices=["pieces", "scene"], required=True)
    p.add_argument("--scene", default="road")
    p.add_argument("--out", required=True)
    p.add_argument("--only", default="")
    p.add_argument("--stats", default="")
    p.add_argument("--width", type=int, default=640)
    p.add_argument("--height", type=int, default=360)
    p.add_argument("--piece-size", type=int, default=400)
    a = p.parse_args(argv)
    stats = {}
    if a.stats and os.path.exists(a.stats):
        with open(a.stats) as f:
            stats = json.load(f)
    errors = mode_pieces(a, stats) if a.mode == "pieces" else mode_scene(a, stats)
    for e in errors:
        print("VERIFY_ERROR", e)
    if errors:
        raise SystemExit(1)


main()
