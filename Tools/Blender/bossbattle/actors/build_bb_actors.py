"""Boss Battle actors (package M part 1) - one command, venv Python:

  Tools/.venv/bin/python Tools/Blender/bossbattle/actors/build_bb_actors.py --preview-dir <scratchpad>/art/actors

1. builds every actor in its own headless Blender (bl_build_actor.py, in parallel): geometry, contract checks
   (part names/parents, tri budget, baked transforms, size, facing, pivot, texel-centre UVs) and the FBX, written
   to ninjago-unity/Assets/Models/Ninjago/BossBattle/Actors/<Model>.fbx only when its content changed;
2. writes bb_actors_manifest.json next to this file (parts, pivots, anchors, bounds, tris, tell parts, wing
   angles, shadow casters, Unity import contract) - deterministic, no timings or machine paths;
3. re-imports every FBX and checks it against the contract + manifest (bl_verify_fbx.py);
4. renders the review turntables, the game-camera previews (bl_preview_actors.py + preview_compose.py) and the
   64 x 64 boss portrait -> ninjago-unity/Assets/Sprites/BossBattle/Hud/BossPortrait.png.
Previews and review sheets go to --preview-dir only (never the repo). Exit code 1 on any failure.
"""
import argparse
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402
import contract_bb_actors as contract  # noqa: E402

REVIEW_ORDER = ("front", "threeq", "side", "back", "threeq2", "top", "tell", "spread_front", "spread_threeq",
                "spread_back")

# what the later Unity step (BossBattleArtBuilder, package M) must apply - copied into the manifest
UNITY_CONTRACT = {
    "import": {
        "globalScale": 1, "useFileScale": True, "bakeAxisConversion": False, "preserveHierarchy": True,
        "sortHierarchyByName": False, "meshCompression": "Off", "isReadable": False, "importNormals": "Import",
        "importTangents": "None", "materialImportMode": "None", "animationType": "None", "importAnimation": False,
        "importCameras": False, "importLights": False, "importBlendShapes": False, "importVisibility": False,
        "addCollider": False, "weldVertices": False, "optimizeMeshVertices": True,
        "note": "same as Nex.BilliardRogue.Editor.ImportSettingsBuilder.ConfigureModel; preserveHierarchy keeps the "
                "single FBX root 'Body' as a child of the model prefab root (else Unity collapses it)",
    },
    "palette": {
        "texture": "Assets/Textures/BilliardRogue/Palette/Palette_Main.png (shared, read only, 32x16, point, "
                   "no mips, clamp, uncompressed)",
        "emission": "Assets/Textures/BilliardRogue/Palette/Palette_Emission.png",
        "M_BB_Palette": "BilliardRogue/ToonLit, _BaseMap Palette_Main, _Bands 4, _BandSoftness 0.02, _ShadowTint "
                        "(0.34,0.32,0.5), _AmbientStrength 0.35, _RimColor (1,0.95,0.85,0.55) (boss: A 0.6), _RimPower 3, "
                        "_EMISSION on, _EmissionMap Palette_Emission, _EmissionColor white x 1.6",
        "M_BB_Palette_Tell": "copy of M_BB_Palette with _EmissionColor black at rest; BossActor drives a material "
                             "INSTANCE per tell renderer (_EmissionColor = tell colour x HDR >= 2.5). The tell cells "
                             "are palette gray 8 emissive, so the instance colour alone sets the hue",
    },
}


def blender(script, *args):
    cmd = [bb_paths.BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python",
           os.path.join(HERE, script), "--", *args]
    return subprocess.run(cmd, capture_output=True, text=True)


def tail(proc, n=30):
    return "\n".join((proc.stdout + proc.stderr).splitlines()[-n:])


def build_one(model, work, review_dir, lenient):
    args = ["--model", model, "--fbx", bb_paths.fbx_path(model), "--stats", os.path.join(work, model + ".json"),
            "--palette-json", bb_paths.PALETTE_JSON, "--palette-png", bb_paths.PALETTE_PNG]
    if review_dir:
        args += ["--review-dir", review_dir]
    if lenient:
        args.append("--lenient")
    t = time.time()
    proc = blender("bl_build_actor.py", *args)
    line = next((ln for ln in proc.stdout.splitlines() if ln.startswith("BB_ACTOR_STATS ")), None)
    if proc.returncode != 0 or line is None:
        return model, None, f"FAILED ({proc.returncode})\n{tail(proc)}"
    return model, json.loads(line[len("BB_ACTOR_STATS "):]), f"{time.time() - t:.1f}s"


def manifest_entry(stats, model):
    keep = {k: v for k, v in stats.items() if k not in ("seconds", "fbx_changed", "fbx_bytes", "fbx")}
    keep["fbx"] = os.path.relpath(bb_paths.fbx_path(model), bb_paths.UNITY)
    keep["hierarchy"] = contract.HIERARCHY[model]
    keep["size_expected"] = contract.SIZE[model][0]
    keep["wrapper_prefab"] = f"Assets/Prefabs/Ninjago/BossBattle/Models/{model}.prefab"
    return keep


def review_sheet(model, review_dir, out):
    tiles = [os.path.join(review_dir, f"{model}_{v}.png") for v in REVIEW_ORDER]
    tiles = [t for t in tiles if os.path.exists(t)]
    if not tiles:
        return
    ims = [Image.open(t).convert("RGBA") for t in tiles]
    w, h = ims[0].size
    cols = min(5, len(ims))
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * w, rows * h), (138, 143, 158, 255))
    for i, im in enumerate(ims):
        tile = Image.new("RGBA", (w, h), (138, 143, 158, 255))
        tile.alpha_composite(im)
        sheet.paste(tile, ((i % cols) * w, (i // cols) * h))
    sheet.convert("RGB").save(out)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--models", default=",".join(contract.MODELS))
    p.add_argument("--preview-dir", default=bb_paths.PREVIEW_DIR)
    p.add_argument("--no-review", action="store_true", help="skip the turntable review renders")
    p.add_argument("--no-preview", action="store_true", help="skip game-camera previews and the portrait")
    p.add_argument("--lenient", action="store_true", help="dev only: warn on budget/size instead of failing")
    a = p.parse_args()
    models = [m for m in a.models.split(",") if m]
    work = os.path.join(a.preview_dir, "work")
    review_dir = None if a.no_review else os.path.join(a.preview_dir, "review")
    for d in (work, review_dir):
        if d:
            os.makedirs(d, exist_ok=True)

    t0 = time.time()
    with ThreadPoolExecutor(max_workers=4) as pool:
        results = list(pool.map(lambda m: build_one(m, work, review_dir, a.lenient), models))
    failed = False
    manifest = {}
    if os.path.exists(bb_paths.MANIFEST):
        with open(bb_paths.MANIFEST) as f:
            manifest = json.load(f)
    for model, stats, msg in results:
        if stats is None:
            failed = True
            print(f"{model}: {msg}")
            continue
        manifest[model] = manifest_entry(stats, model)
        print(f"{model}: {stats['tris']}/{stats['budget']} tris, size_unity {stats['size_unity']}, "
              f"emissive faces {stats['emissive_faces']}, fbx_changed={stats.get('fbx_changed')} ({msg})")
    if failed:
        sys.exit(1)
    manifest["_unity"] = UNITY_CONTRACT
    with open(bb_paths.MANIFEST, "w") as f:
        json.dump({k: manifest[k] for k in sorted(manifest)}, f, indent=1, sort_keys=True)
        f.write("\n")

    proc = blender("bl_verify_fbx.py", "--fbx-dir", bb_paths.MODELS_OUT, "--manifest", bb_paths.MANIFEST,
                   "--models", ",".join(models))
    for ln in proc.stdout.splitlines():
        if ln.startswith("VERIFY"):
            rec = ln.split(" ", 2)
            if rec[0] == "VERIFY_ALL":
                print(ln)
            else:
                res = json.loads(rec[2])
                print(f"VERIFY {rec[1]}: ok={res['ok']} tris={res['tris']}/{res['budget']} "
                      f"nodes={len(res['parts'])} problems={res['problems']}")
    if proc.returncode != 0:
        print(tail(proc))
        sys.exit(1)

    if review_dir:
        for m in models:
            review_sheet(m, review_dir, os.path.join(a.preview_dir, f"{m}_review.png"))
    if not a.no_preview and all(m in manifest for m in contract.MODELS):
        game = os.path.join(a.preview_dir, "game")
        raw = os.path.join(game, "portrait_raw.png")
        proc = blender("bl_preview_actors.py", "--fbx-dir", bb_paths.MODELS_OUT, "--manifest", bb_paths.MANIFEST,
                       "--out-dir", game, "--portrait", raw)
        if proc.returncode != 0:
            print(tail(proc))
            sys.exit(1)
        comp = subprocess.run([sys.executable, os.path.join(HERE, "preview_compose.py"), "--dir", game,
                               "--portrait-raw", raw, "--portrait-out", bb_paths.PORTRAIT_OUT],
                              capture_output=True, text=True)
        print((comp.stdout + comp.stderr).strip())
        if comp.returncode != 0:
            sys.exit(1)
    print(f"done in {time.time() - t0:.1f}s; manifest {os.path.relpath(bb_paths.MANIFEST, bb_paths.REPO)}")


if __name__ == "__main__":
    main()
