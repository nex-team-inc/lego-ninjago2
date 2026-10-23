"""Boss Battle world props + arena: one-command build (venv orchestrator; package M, part 2).

    Tools/.venv/bin/python Tools/Blender/bossbattle/world/build_world.py \
        [--only BB_Brick,BB_Rock] [--no-render] [--no-scenes] [--scenes road,sky] [--preview-dir DIR] [--dry]

1. Blender (headless) runs bbw_build.py: builds every piece, checks the contracts (tri budget, pivot, baked
   transforms, palette texel-centre UVs) and writes `ninjago-unity/Assets/Models/Ninjago/BossBattle/World/BB_*.fbx`
   (rewritten only when the content changed, so repeated builds leave git clean).
2. Writes `bbw_world_manifest.json` next to this file: per piece the parts (name, parent, Unity pivot, local
   position), Unity-space bounds, tris, emissive face counts, pivot convention, shadow hint and notes. Deterministic
   (no timings), so it only changes when a model changes. Package A / the Unity art builder read it.
3. Review renders (never game assets) into the preview dir (default: $BBW_PREVIEW_DIR or Tools/Staging/Previews):
   per-piece close-ups under a neutral light (each one re-imports the FBX and verifies names / parents / identity
   transforms / bounds), contact sheets `props_sheet.png` + `arena_sheet.png`, and game-camera dioramas at 640x360
   (`road_dusk.png`, `sky_night.png`, `road_props.png`, `sky_props.png`, plus x2 nearest-neighbour upscales).
"""
import argparse
import concurrent.futures as cf
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bbw_paths as P  # noqa: E402

PROP_NAMES = ["BB_Brick", "BB_Rock", "BB_PowerCrate", "BB_Rocket", "BB_SideWall", "BB_SpikeWall", "BB_WindGust",
              "BB_LaserBolt", "BB_Reticle", "BB_CrackGlow"]
SCENES = {   # scene -> (look, post label)
    "road_dusk": ("dusk", "ROAD  dusk  cam (0,4,-11) pitch 9 fov 50"),
    "sky_night": ("night", "SKY  night  cam (0,3.4,-8) pitch 2 fov 50"),
    "road_props": ("dusk", "ROAD props in play"),
    "sky_props": ("night", "SKY props in play"),
}


def blender(script, args, log=None):
    cmd = [P.BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python", os.path.join(HERE, script),
           "--"] + args
    t = time.time()
    r = subprocess.run(cmd, capture_output=True, text=True)
    if log:
        with open(log, "w") as f:
            f.write(r.stdout + "\n" + r.stderr)
    if r.returncode != 0:
        tail = "\n".join((r.stdout + r.stderr).splitlines()[-40:])
        raise RuntimeError(f"{script} {' '.join(args)} failed ({r.returncode}):\n{tail}")
    return r.stdout, time.time() - t


def venv(script, args):
    r = subprocess.run([sys.executable, os.path.join(HERE, script)] + args, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(f"{script} failed:\n{r.stdout}\n{r.stderr}")
    return r.stdout.strip()


def manifest_entry(st):
    keep = ("name", "kind", "tris", "budget", "pivot", "shadows", "notes", "size_unity", "bounds_min_unity",
            "bounds_max_unity")
    out = {k: st[k] for k in keep}
    out["fbx"] = st["fbx"]
    out["parts"] = [{"name": p["name"], "parent": p["parent"], "tris": p["tris"], "pivot_unity": p["pivot_unity"],
                     "local_position_unity": p["local_position_unity"], "emissive_faces": p["emissive_faces"],
                     "bounds_min_unity": p["bounds_min_unity"], "bounds_max_unity": p["bounds_max_unity"]}
                    for p in st["parts"]]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--no-scenes", action="store_true")
    ap.add_argument("--no-pieces", action="store_true", help="skip the per-piece close-ups / sheets")
    ap.add_argument("--scenes", default=",".join(SCENES))
    ap.add_argument("--preview-dir", default=P.PREVIEW_DIR)
    ap.add_argument("--dry", action="store_true", help="contract checks only, no FBX writes")
    ap.add_argument("--jobs", type=int, default=4)
    a = ap.parse_args()
    pdir = os.path.abspath(a.preview_dir)
    raw = os.path.join(pdir, "raw")
    os.makedirs(raw, exist_ok=True)
    stats_path = os.path.join(pdir, "build_stats.json")

    # 1. build + export
    args = ["--stats", stats_path, "--palette-json", P.PALETTE_JSON, "--palette-png", P.PALETTE_PNG]
    if a.only:
        args += ["--only", a.only]
    if a.dry:
        args += ["--dry"]
    out, secs = blender("bbw_build.py", args, os.path.join(pdir, "build.log"))
    stats = json.load(open(stats_path))
    changed = sorted(n for n, s in stats.items() if s.get("fbx_changed"))
    total = sum(s["tris"] for s in stats.values())
    print(f"built {len(stats)} pieces in {secs:.1f}s, {total} tris total; FBX changed: {changed or 'none'}")
    for n, s in sorted(stats.items()):
        print(f"  {n:22s} {s['tris']:4d}/{s['budget']:<4d} tris  size {s['size_unity']}  pivot {s['pivot']}")

    # 2. manifest (merge so --only builds keep the other entries)
    if not a.dry:
        man = json.load(open(P.MANIFEST)) if os.path.exists(P.MANIFEST) else {}
        for n, s in stats.items():
            man[n] = manifest_entry(s)
        with open(P.MANIFEST, "w") as f:
            json.dump(dict(sorted(man.items())), f, indent=1, sort_keys=True)
            f.write("\n")
        print("manifest ->", os.path.relpath(P.MANIFEST, P.REPO))
    if a.no_render or a.dry:
        return

    # 3. review renders: pieces (parallel chunks) then scenes
    names = sorted(stats)
    chunks = [names[i::a.jobs] for i in range(a.jobs) if names[i::a.jobs]]
    jobs = [] if a.no_pieces else [
        ("bbw_render.py", ["--mode", "pieces", "--out", raw, "--stats", stats_path, "--only", ",".join(c)])
        for c in chunks]
    scenes = [] if a.no_scenes else [s for s in a.scenes.split(",") if s]
    jobs += [("bbw_render.py", ["--mode", "scene", "--scene", s, "--out", raw, "--stats", stats_path])
             for s in scenes]
    t = time.time()
    with cf.ThreadPoolExecutor(max_workers=a.jobs) as ex:
        futs = [ex.submit(blender, sc, ar) for sc, ar in jobs]
        for fu in futs:
            fu.result()
    print(f"rendered {len(jobs)} jobs in {time.time() - t:.1f}s")
    props = [n for n in names if n in PROP_NAMES]
    arena = [n for n in names if n not in PROP_NAMES]
    for sheet, group in (("props_sheet.png", props), ("arena_sheet.png", arena)):
        if group and not a.no_pieces:
            print(venv("bbw_post.py", ["pieces", "--dir", raw, "--out", os.path.join(pdir, sheet), "--names",
                                       ",".join(group), "--columns", "4"]))
    for s in scenes:
        look, label = SCENES[s]
        npy = os.path.join(raw, s + ".npy")
        print(venv("bbw_post.py", ["scene", "--npy", npy, "--look", look, "--out", os.path.join(pdir, s + ".png"),
                                   "--scale", "1", "--sky", "gradient"]))
        print(venv("bbw_post.py", ["scene", "--npy", npy, "--look", look, "--out", os.path.join(pdir, s + "_x2.png"),
                                   "--scale", "2", "--sky", "gradient", "--label", label]))


if __name__ == "__main__":
    main()
