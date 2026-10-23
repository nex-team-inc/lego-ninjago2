"""Re-import the exported Boss Battle actor FBX files and check the part contract (contract_bb_actors.py) against
what Unity will see: exact node names AND parents, node types (mesh; empty only for transform-only parts), no
animation, one material slot per mesh, identity rotation/scale on every node (rotation/scale baked), pivots at the
manifest positions, palette UVs on texel centres with one texel per face, tri budgets and per-part bounds equal to
the build. Prints one `VERIFY <model> {json}` line per model and `VERIFY_ALL OK|FAIL`; exits 1 on FAIL.

  Blender -b --factory-startup --python-exit-code 1 --python bl_verify_fbx.py -- \
      --fbx-dir ninjago-unity/Assets/Models/Ninjago/BossBattle/Actors --manifest bb_actors_manifest.json
"""
import argparse
import json
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402
import contract_bb_actors as contract  # noqa: E402

W, H, HALF = 32, 16, 16
TOL = 2e-3
AXIS_CONV = Matrix.Rotation(1.5707963267948966, 3, "X")


def _close(a, b, eps=1e-4):
    return all(abs(a[i][j] - b[i][j]) <= eps for i in range(3) for j in range(3))


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--fbx-dir", default=bb_paths.MODELS_OUT)
    p.add_argument("--manifest", default=bb_paths.MANIFEST)
    p.add_argument("--models", default=",".join(contract.MODELS))
    return p.parse_args(argv)


def b2u(v):
    return [-v[0], v[2], -v[1]]


def world_bounds_unity(o):
    pts = [b2u(o.matrix_world @ v.co) for v in o.data.vertices]
    return [min(p[k] for p in pts) for k in range(3)], [max(p[k] for p in pts) for k in range(3)]


def check_model(model, fbx_dir, built):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(fbx_dir, model + ".fbx")
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path))
    bpy.context.view_layer.update()
    want = contract.HIERARCHY[model]
    problems = []
    objs = {o.name: o for o in bpy.data.objects}
    if sorted(objs) != sorted(want):
        problems.append("nodes %s != contract %s" % (sorted(objs), sorted(want)))
    if bpy.data.actions:
        problems.append("actions %s" % sorted(a.name for a in bpy.data.actions))
    parts_built = {p["name"]: p for p in built["parts"]} if built else {}
    tris, emissive, info = 0, {}, {}
    for name, o in sorted(objs.items()):
        parent = o.parent.name if o.parent else None
        if name in want and parent != want[name]:
            problems.append("%s parent %s != %s" % (name, parent, want[name]))
        is_empty = parts_built.get(name, {}).get("empty", False)
        if o.type != ("EMPTY" if is_empty else "MESH"):
            problems.append("%s is %s (expected %s)" % (name, o.type, "EMPTY" if is_empty else "MESH"))
        # Blender's importer puts its own axis conversion (+90 deg about X) on the FBX root; Unity does not (root
        # rotation 0 with bakeAxisConversion off). So every node's WORLD rotation must equal exactly that
        # conversion: no node may carry a rotation or scale of its own.
        if not o.parent:
            conv = o.matrix_world.to_3x3()
            if not (_close(conv, AXIS_CONV) or _close(conv, Matrix.Identity(3))):
                problems.append("%s root rotation is not the plain axis conversion" % name)
        world = o.matrix_world.to_3x3()
        if not _close(world, AXIS_CONV) and not _close(world, Matrix.Identity(3)):
            problems.append("%s carries its own rotation/scale %s" % (name, [list(map(lambda c: round(c, 3), r)) for r in world]))
        if any(abs(s - 1) > 1e-4 for s in o.scale):
            problems.append("%s scaled %s" % (name, tuple(round(s, 4) for s in o.scale)))
        off = o.matrix_world.translation - (o.parent.matrix_world.translation if o.parent else Vector())
        loc_u = b2u(off)
        if name in parts_built:
            exp = parts_built[name]["local_unity"]
            err = max(abs(a - b) for a, b in zip(loc_u, exp))
            if err > TOL:
                problems.append("%s local position %s != manifest %s" % (name, [round(c, 4) for c in loc_u], exp))
        row = dict(parent=parent, type=o.type, local_unity=[round(c, 4) for c in loc_u])
        if o.type == "MESH":
            me = o.data
            if len(me.materials) != 1:
                problems.append("%s has %d material slots" % (name, len(me.materials)))
            t = sum(len(p.vertices) - 2 for p in me.polygons)
            tris += t
            uv = me.uv_layers.active.data
            bad = emis = 0
            for p in me.polygons:
                cols = set()
                for li in p.loop_indices:
                    u, v = uv[li].uv
                    x, y = u * W - 0.5, (1 - v) * H - 0.5
                    if abs(x - round(x)) > 1e-3 or abs(y - round(y)) > 1e-3:
                        bad += 1
                    cols.add((round(x), round(y)))
                if len(cols) != 1:
                    bad += 1
                if next(iter(cols))[0] >= HALF:
                    emis += 1
            if bad:
                problems.append("%s: %d loops off texel centre / mixed texels" % (name, bad))
            if name in parts_built:
                if t != parts_built[name]["tris"]:
                    problems.append("%s tris %d != built %d" % (name, t, parts_built[name]["tris"]))
                bu = parts_built[name].get("bounds_unity")
                if bu:
                    lo, hi = world_bounds_unity(o)
                    err = max(abs(a - b) for a, b in zip(lo + hi, bu["min"] + bu["max"]))
                    if err > 2e-3 + 1e-3:
                        problems.append("%s bounds off by %.4f m after re-import" % (name, err))
            emissive[name] = emis
            row.update(tris=t, emissive_faces=emis)
        info[name] = row
    budget = contract.BUDGETS[model]
    if tris > budget:
        problems.append("tris %d > budget %d" % (tris, budget))
    if not built:
        problems.append("no build stats in the manifest")
    return dict(ok=not problems, tris=tris, budget=budget, parts=info, problems=problems)


def main():
    args = parse()
    with open(args.manifest) as f:
        manifest = json.load(f)
    ok_all = True
    for model in [m for m in args.models.split(",") if m]:
        res = check_model(model, args.fbx_dir, manifest.get(model))
        ok_all &= res["ok"]
        print("VERIFY", model, json.dumps(res, sort_keys=True))
    print("VERIFY_ALL", "OK" if ok_all else "FAIL")
    if not ok_all:
        sys.exit(1)


main()
