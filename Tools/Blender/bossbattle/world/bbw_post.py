"""Post-process the bbw_render.py review renders like the game's HD-2D stack (venv: numpy, pillow, scipy).

clear colour (or the look's sky gradient) under the alpha -> HDR bloom (game_look.bloom: threshold / knee / scatter,
on gamma values like URP in a Gamma project) -> white balance, contrast, split toning, saturation, Neutral tonemap
(game_look.grade) -> tilt-shift band (post_diorama.tilt_shift, GDD 12 centre / half width) -> vignette -> 8 bit ->
nearest-neighbour upscale. Review only.

    Tools/.venv/bin/python bbw_post.py scene  --npy road.npy --look dusk --out road.png [--scale 2] [--sky flat]
    Tools/.venv/bin/python bbw_post.py pieces --dir DIR --out sheet.png [--names A,B]
"""
import argparse
import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bbw_paths as P  # noqa: E402
import bbw_looks as LK  # noqa: E402

sys.path.insert(0, P.ENEMIES_KIT)
sys.path.insert(0, P.ENV_KIT)
import game_look as GL  # noqa: E402
import post_diorama as PD  # noqa: E402


def sky(look, h, w, mode):
    if mode == "gradient" and look.get("sky_gradient"):
        stops = look["sky_gradient"]
        ys = (np.arange(h) + 0.5) / h            # 0 = top
        t = 1.0 - ys                             # stops are given bottom (0) -> top (1)
        cols = np.zeros((h, 3), np.float32)
        for i in range(h):
            for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
                if t0 <= t[i] <= t1:
                    f = (t[i] - t0) / max(t1 - t0, 1e-6)
                    cols[i] = np.array(c0) * (1 - f) + np.array(c1) * f
                    break
        return np.repeat(cols[:, None, :], w, axis=1)
    return np.broadcast_to(np.array(look["clear"], np.float32), (h, w, 3))


def process(npy, look, sky_mode="flat", tilt=True):
    rgba = np.load(npy).astype(np.float32)
    rgb, a = rgba[..., :3], np.clip(rgba[..., 3:4], 0, 1)
    h, w = rgb.shape[:2]
    col = rgb + (1.0 - a) * sky(look, h, w, sky_mode)
    vol = look["volume"]
    if "Bloom" in vol:
        col = GL.bloom(col, vol["Bloom"])
    col = GL.grade(col, vol)
    if tilt and look.get("tilt"):
        c, hw = look["tilt"]
        top = 1.0 - c                             # GDD centre is measured from the bottom
        col = PD.tilt_shift(col, band=(max(0.0, top - hw), min(1.0, top + hw)), max_sigma=2.0)
    if look.get("vignette"):
        col = PD.vignette(col, look["vignette"])
    return np.clip(col, 0, 1)


def to_img(col, scale=1):
    img = Image.fromarray((col * 255 + 0.5).astype(np.uint8), "RGB")
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST) if scale > 1 else img


def label(img, text, xy=(4, 4)):
    d = ImageDraw.Draw(img)
    d.rectangle((xy[0], xy[1], xy[0] + 7 * len(text) + 8, xy[1] + 15), fill=(0, 0, 0))
    d.text((xy[0] + 4, xy[1] + 2), text, fill=(255, 236, 200))
    return img


def cmd_scene(a):
    look = LK.LOOKS[a.look]
    meta_path = os.path.splitext(a.npy)[0] + ".json"
    meta = json.load(open(meta_path)) if os.path.exists(meta_path) else {}
    img = to_img(process(a.npy, look, a.sky, not a.no_tilt), a.scale)
    if a.label:
        label(img, a.label)
    img.save(a.out, optimize=True)
    print("post ->", a.out, meta.get("missing", ""), "proxies:", meta.get("proxies", ""))


def cmd_pieces(a):
    look = LK.NEUTRAL
    files = sorted(glob.glob(os.path.join(a.dir, "*_v0.npy")))
    names = [os.path.basename(f)[:-7] for f in files]
    if a.names:
        keep = a.names.split(",")
        names = [n for n in names if n in keep]
    tiles = []
    for n in names:
        row = []
        for v in sorted(glob.glob(os.path.join(a.dir, f"{n}_v*.npy"))):
            row.append(to_img(process(v, look, "flat", tilt=False), 1))
        tiles.append((n, row))
    if not tiles:
        raise SystemExit("no piece renders")
    tw, th = tiles[0][1][0].size
    cols = max(len(r) for _, r in tiles)
    per_line = max(1, a.columns // cols)
    lines = (len(tiles) + per_line - 1) // per_line
    sheet = Image.new("RGB", (per_line * cols * tw + (per_line - 1) * 6, lines * th), (12, 12, 16))
    for i, (n, row) in enumerate(tiles):
        x0 = (i % per_line) * (cols * tw + 6)
        y0 = (i // per_line) * th
        for j, im in enumerate(row):
            sheet.paste(im, (x0 + j * tw, y0))
        label(sheet, n, (x0 + 4, y0 + 4))
    sheet.save(a.out, optimize=True)
    print("sheet ->", a.out, len(tiles), "pieces")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("scene")
    s.add_argument("--npy", required=True)
    s.add_argument("--look", required=True, choices=sorted(LK.LOOKS))
    s.add_argument("--out", required=True)
    s.add_argument("--scale", type=int, default=2)
    s.add_argument("--sky", default="flat", choices=["flat", "gradient"])
    s.add_argument("--no-tilt", action="store_true")
    s.add_argument("--label", default="")
    q = sub.add_parser("pieces")
    q.add_argument("--dir", required=True)
    q.add_argument("--out", required=True)
    q.add_argument("--names", default="")
    q.add_argument("--columns", type=int, default=4)
    a = ap.parse_args()
    (cmd_scene if a.cmd == "scene" else cmd_pieces)(a)


if __name__ == "__main__":
    main()
