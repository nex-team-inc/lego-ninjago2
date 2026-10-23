"""Compose the game-camera previews (venv Python: numpy, Pillow, scipy) - review only, never a game asset.

Re-shades the raw passes written by bl_preview_actors.py with the ToonLit formula (gamma space, M_BB_Palette
constants: 4 bands, shadow tint, trilight ambient x 0.35, rim, emission x 1.6) and an approximate URP post (bloom,
white balance / contrast / split toning, Neutral tonemap) using the starting values of research/hd2d-rendering.md
4.6 / 4.7, over a flat sky gradient. Writes per scene: <scene>.png (640 x 360 = the world RT), <scene>_x2.png
(nearest upscale), <scene>_boss_x4.png / <scene>_vehicle_x4.png (nearest crops), and the portrait sprite.

  Tools/.venv/bin/python preview_compose.py --dir <scratch>/game [--portrait-raw raw.png --portrait-out out.png]
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402

sys.path.insert(0, bb_paths.ENEMIES_KIT)
import game_look as gl  # noqa: E402  (toon_ramp / bloom / grade; its Starter paths are never touched here)

# M_BB_Palette (MaterialsBuilder.BuildPalette constants; boss rim A 0.6 per hd2d-rendering 4.5)
MAT = dict(shadow_tint=(0.34, 0.32, 0.5), ambient_strength=0.35, rim=(1.0, 0.95, 0.85), rim_a=0.55, rim_power=3.0,
           bands=4.0, softness=0.02, emission=1.6, tell_gain=2.5)
LOOKS = {
    "road": dict(sun=(1.0, 0.62, 0.36), intensity=1.35, shadow_strength=0.85,
                 sky=(0.55, 0.42, 0.58), equator=(0.5, 0.34, 0.3), ground=(0.2, 0.14, 0.14),
                 bg_top=(0.36, 0.22, 0.42), bg_bottom=(0.98, 0.6, 0.42), horizon=0.62,
                 vol={"Bloom": dict(threshold=1.05, intensity=0.9, scatter=0.7, tint=(1.0, 0.86, 0.66)),
                      "ColorAdjustments": dict(contrast=22, saturation=8, postExposure=0.0),
                      "WhiteBalance": dict(temperature=18, tint=4),
                      "SplitToning": dict(shadows=(0.30, 0.24, 0.50), highlights=(1.0, 0.78, 0.52), balance=10),
                      "Tonemapping": dict(mode=1)}),
    "sky": dict(sun=(0.55, 0.62, 1.0), intensity=0.7, shadow_strength=0.9,
                sky=(0.12, 0.14, 0.28), equator=(0.08, 0.08, 0.16), ground=(0.03, 0.03, 0.06),
                bg_top=(0.02, 0.02, 0.06), bg_bottom=(0.16, 0.08, 0.24), horizon=0.75,
                vol={"Bloom": dict(threshold=0.95, intensity=1.3, scatter=0.75, tint=(0.8, 0.86, 1.0)),
                     "ColorAdjustments": dict(contrast=28, saturation=-8, postExposure=-0.2),
                     "WhiteBalance": dict(temperature=-22, tint=6),
                     "SplitToning": dict(shadows=(0.18, 0.22, 0.60), highlights=(0.70, 0.85, 1.0), balance=-18),
                     "Tonemapping": dict(mode=1)}),
}


def load(path):
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float64) / 255.0


def shade(d, scene, info):
    lk = LOOKS[scene]
    albedo, alpha = d["albedo"][..., :3], d["albedo"][..., 3:4]
    normal = d["normal"][..., :3] * 2.0 - 1.0
    n_len = np.linalg.norm(normal, axis=-1, keepdims=True)
    normal = normal / np.maximum(n_len, 1e-6)
    to_light = np.array(info["to_light"])
    light = d["light"][..., 0]
    ndl = np.clip(normal @ to_light, 0.0, 1.0)
    shadow = np.where(ndl > 0.03, np.clip(light / np.maximum(ndl, 1e-4), 0.0, 1.0), 1.0)
    att = 1.0 - lk["shadow_strength"] * (1.0 - shadow)
    ramp = gl.toon_ramp(ndl * att, MAT["bands"], MAT["softness"])[..., None]
    sun = np.array(lk["sun"]) * lk["intensity"]
    tint = np.array(MAT["shadow_tint"])
    lighting = sun * (tint + (1.0 - tint) * ramp)
    up = normal[..., 2:3]
    sky, eq, gr = (np.array(lk[k]) for k in ("sky", "equator", "ground"))
    amb = np.where(up >= 0, eq + (sky - eq) * up, eq + (gr - eq) * (-up))
    col = albedo * (lighting + amb * MAT["ambient_strength"])
    ndv = np.clip(normal @ np.array(info["view_dir"]), 0.0, 1.0)[..., None]
    col = col + np.array(MAT["rim"]) * (1.0 - ndv) ** MAT["rim_power"] * MAT["rim_a"] * np.maximum(ramp, 0.2)
    col = col + d["emis"][..., :3] * MAT["emission"] + d["tell"][..., :3] * MAT["tell_gain"]
    h = col.shape[0]
    t = np.clip(np.arange(h)[:, None, None] / (h * lk["horizon"]), 0.0, 1.0)
    bg = np.array(lk["bg_top"]) * (1 - t) + np.array(lk["bg_bottom"]) * t
    col = col * alpha + bg * (1.0 - alpha)
    return gl.post(col, lk["vol"])


def save(arr, path, scale=1, box=None):
    img = Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    if box is not None:
        img = img.crop(box)
    if scale != 1:
        img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    img.save(path)


def crop_box(b, w, h, pad=6):
    x0, y0, x1, y1 = b
    return (max(0, int(x0) - pad), max(0, int(y0) - pad), min(w, int(x1) + pad + 1), min(h, int(y1) + pad + 1))


def compose_scenes(folder):
    with open(os.path.join(folder, "scenes.json")) as f:
        scenes = json.load(f)
    out = []
    for scene, info in scenes.items():
        d = {p: load(os.path.join(folder, f"{scene}_{p}.png")) for p in ("albedo", "emis", "tell", "light", "normal")}
        img = shade(d, scene, info)
        h, w = img.shape[:2]
        save(img, os.path.join(folder, f"{scene}.png"))
        save(img, os.path.join(folder, f"{scene}_x2.png"), 2)
        save(img, os.path.join(folder, f"{scene}_boss_x4.png"), 4, crop_box(info["boxes"]["boss"], w, h))
        veh = "car" if "car" in info["boxes"] else "jet"
        vb = info["boxes"][veh]
        for k in ("minionL", "minionR"):
            if k in info["boxes"]:
                b = info["boxes"][k]
                vb = [min(vb[0], b[0]), min(vb[1], b[1]), max(vb[2], b[2]), max(vb[3], b[3])]
        save(img, os.path.join(folder, f"{scene}_vehicle_x4.png"), 4, crop_box(vb, w, h))
        boss_h = info["boxes"]["boss"][3] - info["boxes"]["boss"][1]
        out.append((scene, round(boss_h, 1)))
    return out


OUTLINE_RGB = (12, 12, 16)  # palette gray 0


def finalize_sprite(raw, out):
    """Hard alpha + 1 px dark outline (4-neighbour), the build_enemies.finalize_icon rule; fails if the art touches
    the border (no room for the outline)."""
    a = np.array(Image.open(raw).convert("RGBA"))
    solid = a[..., 3] >= 128
    if solid[0, :].any() or solid[-1, :].any() or solid[:, 0].any() or solid[:, -1].any():
        raise RuntimeError(f"{raw}: art touches the sprite border (no room for the outline)")
    grow = solid.copy()
    grow[1:, :] |= solid[:-1, :]
    grow[:-1, :] |= solid[1:, :]
    grow[:, 1:] |= solid[:, :-1]
    grow[:, :-1] |= solid[:, 1:]
    px = np.zeros_like(a)
    px[solid, :3] = a[solid, :3]
    px[solid, 3] = 255
    ring = grow & ~solid
    px[ring, :3] = OUTLINE_RGB
    px[ring, 3] = 255
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    Image.fromarray(px, "RGBA").save(out, optimize=True)
    return px


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--portrait-raw")
    p.add_argument("--portrait-out")
    a = p.parse_args()
    res = compose_scenes(a.dir)
    if a.portrait_raw and a.portrait_out:
        px = finalize_sprite(a.portrait_raw, a.portrait_out)
        big = Image.fromarray(px, "RGBA").resize((px.shape[1] * 6, px.shape[0] * 6), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (40, 30, 52, 255))
        bg.alpha_composite(big)
        bg.convert("RGB").save(os.path.join(a.dir, "portrait_x6.png"))
    print("COMPOSE " + json.dumps({"boss_px_height": dict(res)}))


if __name__ == "__main__":
    main()
