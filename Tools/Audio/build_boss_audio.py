"""Boss Battle audio build: shared-repo samples + synth layers -> SFX WAVs, two BGM loops, manifest, checks, review sheets.

    Tools/.venv/bin/python Tools/Audio/build_boss_audio.py [--preview DIR] [--sfx-out DIR] [--bgm-out DIR] [--no-manifest]

Deterministic (seeded synth, bit-exact Vorbis); ~1 min warm. Sources are materialized one file at a time from
~/Documents/music-cell-shared-assets (shared_repo.py) into the gitignored Tools/Staging/.cache/boss_audio_src.
Picks and rationale: boss_picks.py. Synth layers: boss_presets.py (+ the Billiard Rogue synth_presets.py).

Outputs (Docs/BossBattle/TDD.md §8.4):
  ninjago-unity/Assets/Audio/Sfx/Ninjago/BossBattle/<SoundEffect>_<n>.wav   44.1 kHz mono 16-bit PCM, peak <= -3 dBFS,
        trimmed, loudness-matched per category; BossLaserLoop_1.wav is an exact 1.000 s seamless loop.
  ninjago-unity/Assets/Audio/Bgm/Ninjago/BossBattle/<BgmType>.ogg            44.1 kHz stereo Vorbis q5, -14.5 LUFS static
        gain (loop-safe), peak <= -1 dBFS, same sample count as the source loop.
  Tools/Audio/boss_audio_manifest.json   enum key/value -> asset paths, sources + licence family, levels, trigger notes.
Never touches audio_manifest.json (Billiard Rogue's AudioRegistryBuilder reads it) or any other audio folder.
Checks (exit 1): format, peak ceilings, leading silence (<= 2 ms at -50 dBFS), onset timing of instant SFX, round-robin
level match, TV-speaker loss (<= 4 dB through a 150 Hz HP model), laser loop length/seam, BGM loudness/peak/length/seam.
Headroom of worst-case event chains over the loops is reported (the mixer has no master limiter yet).
Iterate into scratch (`--sfx-out/--bgm-out <scratch> --no-manifest`, or `--only Key1,Key2`) and write into the Unity
project once: each rewrite of the Assets folder makes the open Editor re-import the clips.
"""
import argparse
import json
import os
import sys
import wave

import numpy as np
from scipy.signal import butter, sosfilt

import audiolib as al
import boss_picks as bp
import boss_presets
import shared_repo
import sheet
import synth_presets

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
UNITY = os.path.join(ROOT, "ninjago-unity")
SFX_REL = "Assets/Audio/Sfx/Ninjago/BossBattle"
BGM_REL = "Assets/Audio/Bgm/Ninjago/BossBattle"
CEILING_DB = bp.SFX_PEAK_CEILING_DB - 0.05  # margin for 16-bit rounding
SR = al.SR

# SFX whose loudest moment must land on the frame they fire (impact on a visual): -6 dB of the peak within 30 ms.
INSTANT = ["BossRockImpact", "BossRockLock", "BossGroundSlam", "BossMinionLand", "BossMinionHit", "BossWallSmash", "BossRocketHit",
           "BossLaserHit", "BossSpikeSlam", "BossExplode", "BossVehicleHit", "BossKnockout", "BossBannerSlam", "BossLaserDeflect",
           "BossCountdown", "BossCrackGlow"]
INSTANT_MS = 30.0
NATURAL_TAIL_FADE_MS = 40.0
# What the Unity-side builder (BossBattleAudioVfxBuilder.BuildAudio, TDD §8.4) must set on import. Written into the manifest.
UNITY_IMPORT = {
    "sfx": {"loadType": "DecompressOnLoad", "compressionFormat": "ADPCM", "preloadAudioData": True, "forceToMono": False,
            "normalize": False, "loadInBackground": False, "ambisonic": False,
            "why": "files are already mono and loudness-matched; forceToMono+normalize (KidsAudio.Import) would re-normalise "
                   "every clip to 0 dBFS and undo the levels; preload avoids a first-play hitch"},
    "bgm": {"loadType": "Streaming", "compressionFormat": "Vorbis", "quality": 1.0, "preloadAudioData": False,
            "forceToMono": False, "normalize": False,
            "why": "as KidsAudio.RegisterMusic and the other Ninjago loops (quality 1 = importer default); keep the exact "
                   "sample count (no trimming) so AudioSource.loop wraps seamlessly"},
    "loops": {"BossLaserLoop_1.wav": "SfxManager.StartLoop (AudioSource.loop = true); exactly 44100 samples"},
}
TV_DROP_MAX_DB = 4.0  # check: no SFX may lose more than this through the TV-speaker model (tv_drop_db)
_TV_HP = butter(4, 150 / (SR / 2), "high", output="sos")
_TV_LP = butter(2, 12000 / (SR / 2), "low", output="sos")
SHEET_CATEGORY = {"impact_big": "impact", "impact": "enemy", "voice": "player", "action": "magic", "tell": "flow",
                  "frequent": "ball", "soft": "ui", "loop": "bgm"}


# ----------------------------------------------------------------------------------------------------------
# DSP helpers
# ----------------------------------------------------------------------------------------------------------

def peak_db(x: np.ndarray) -> float:
    return al.db(float(np.max(np.abs(x)))) if len(x) else -120.0


def to_ceiling(x: np.ndarray, ceiling_db: float = bp.SFX_PEAK_CEILING_DB) -> np.ndarray:
    return x * (10 ** (ceiling_db / 20) / (np.max(np.abs(x)) or 1.0))


def limit(x: np.ndarray, ceiling_db: float, lookahead_ms: float = 1.5, release_ms: float = 60.0) -> np.ndarray:
    """Look-ahead peak limiter (no latency: the gain looks ahead instead of the audio being delayed).
    gain[n] <= min over the next L samples of the required gain, released with a one-pole, then smoothed backwards over
    L samples (each average only covers gains that already respect the coming peak)."""
    c = 10 ** (ceiling_db / 20)
    need = np.minimum(1.0, c / np.maximum(np.abs(x), 1e-12))
    L = max(1, int(lookahead_ms / 1000 * SR))
    ahead = np.lib.stride_tricks.sliding_window_view(np.pad(need, (0, L), constant_values=1.0), L + 1).min(axis=1)
    r = 1 - np.exp(-1.0 / (release_ms / 1000 * SR))
    g = np.empty_like(ahead)
    prev = 1.0
    for i, v in enumerate(ahead):  # release: recover slowly, follow down instantly
        prev = min(v, prev + (1 - prev) * r)
        g[i] = prev
    sm = np.convolve(np.pad(g, (L - 1, 0), mode="edge"), np.ones(L) / L, mode="valid")
    return np.clip(x * sm, -c, c)


def render_gain(x: np.ndarray, gain_db: float, ceiling_db: float) -> np.ndarray:
    y = x * 10 ** (gain_db / 20)
    return limit(y, ceiling_db) if peak_db(y) > ceiling_db else y


def circ_highpass(x: np.ndarray, hz: float) -> np.ndarray:
    y = al.highpass(np.concatenate([x, x, x]), hz)
    return y[len(x):2 * len(x)]


# ----------------------------------------------------------------------------------------------------------
# Rendering
# ----------------------------------------------------------------------------------------------------------

def synth_layer(layer: bp.S) -> np.ndarray:
    if layer.preset in boss_presets.PRESETS:
        return boss_presets.synth(layer.preset, layer.p, layer.seed, **layer.args)
    return synth_presets.synth(layer.preset, layer.p, layer.seed)


def sample_layer(layer: bp.R, paths) -> np.ndarray:
    x = al.decode(paths[layer.rel], 1)
    x = al.cut(x, layer.start, layer.end)
    if layer.trim:
        x = al.trim(x, start_db=-45.0, end_db=-60.0)
    if layer.from_peak_ms >= 0:
        hop = int(0.001 * SR)
        onset = max(0, int(np.argmax(al.envelope(x, 1.0))) - int(layer.from_peak_ms))
        x = al.fade(x[onset * hop:], in_ms=min(3.0, max(0.5, layer.from_peak_ms / 2)), out_ms=0.0)
    if layer.reverse:
        x = al.trim(x[::-1].copy(), start_db=-45.0, end_db=-60.0)
    if layer.hp:
        x = al.highpass(x, layer.hp)
    if layer.lp:
        x = al.lowpass(x, layer.lp)
    if layer.semis:
        x = al.repitch(x, layer.semis)
    if layer.max_s:
        x = al.fit_length(x, layer.max_s, fade_ms=min(120.0, layer.max_s * 300))
    if layer.fade_in_ms or layer.fade_out_ms:
        x = al.fade(x, layer.fade_in_ms, layer.fade_out_ms)
    return x


def roll_layers(layer: bp.Roll, paths) -> list:
    rng = np.random.default_rng(500 + layer.seed)
    out = []
    n = layer.count
    for i in range(n):
        u = i / max(n - 1, 1)
        rel = layer.rels[int(rng.integers(len(layer.rels)))]
        x = al.trim(al.decode(paths[rel], 1), start_db=-45.0, end_db=-60.0)
        semis = float(rng.uniform(-layer.jitter, layer.jitter))
        x = al.repitch(x, semis) if abs(semis) > 0.01 else x
        if layer.hp:
            x = al.highpass(x, layer.hp)
        gain = layer.gain + (layer.gain_end - layer.gain) * u
        out.append((to_ceiling(x), gain, layer.start + (layer.end - layer.start) * u ** layer.curve))
    return out


def mix_layers(layers, paths) -> np.ndarray:
    parts = []
    for layer in layers:
        if isinstance(layer, bp.Roll):
            parts.extend(roll_layers(layer, paths))
        elif isinstance(layer, bp.S):
            parts.append((to_ceiling(synth_layer(layer)), layer.gain, layer.at))
        else:
            parts.append((to_ceiling(sample_layer(layer, paths)), layer.gain, layer.at))
    return al.mix(*parts)


def render_variant(v: bp.V, paths) -> np.ndarray:
    x = mix_layers(v.layers, paths)
    if v.semis:
        x = al.repitch(x, v.semis)
    if v.drive_db:
        d = 10 ** (v.drive_db / 20)
        x = np.tanh(x / np.max(np.abs(x)) * d) / np.tanh(d)
    if v.lp:
        x = al.lowpass(x, v.lp)
    x = al.highpass(x, 25.0)
    x = al.trim(x, start_db=-50.0, end_db=-60.0, preroll_ms=0.5)
    if v.max_s and len(x) > int(v.max_s * SR):
        return al.fade(al.fit_length(x, v.max_s, v.fade_ms), in_ms=0.5, out_ms=0.0)
    # Ends naturally (trimmed at -60 dB): a short safety fade only. The long `fade_ms` is for truncation; on a natural
    # ending it would squash late content (e.g. the last pant of BossTired).
    return al.fade(x, in_ms=0.5, out_ms=min(v.fade_ms, NATURAL_TAIL_FADE_MS))


def trim_lead(x: np.ndarray, floor_db: float = -50.0) -> np.ndarray:
    """After levelling: drop leading samples below `floor_db` dBFS (swells start near silence), keep a 0.5 ms pre-roll
    and re-apply a 0.5 ms fade-in so the new first sample is click-free."""
    above = np.nonzero(np.abs(x) > 10 ** (floor_db / 20))[0]
    if len(above) == 0:
        return x
    s = max(0, int(above[0]) - int(0.0005 * SR))
    return al.fade(x[s:], in_ms=0.5, out_ms=0.0) if s > 0 else x


def tv_drop_db(x: np.ndarray) -> float:
    """Short-LUFS lost through a TV-speaker model (4th-order 150 Hz high-pass + 12 kHz low-pass). Ninjago's own SFX lose
    0.8 dB (median), 3.1 dB (p90); a sound far above that is sub-dominated and plays much quieter on a TV than its level."""
    return al.max_short_lufs(x) - al.max_short_lufs(sosfilt(_TV_LP, sosfilt(_TV_HP, x)))


def render_loop(lp: bp.Loop, paths) -> np.ndarray:
    n = int(round(lp.length_s * SR))
    out = np.zeros(n)
    count = int(round(lp.length_s / lp.period_s))
    for k in range(count):
        kern = mix_layers(lp.kernels[k % len(lp.kernels)].layers, paths)
        kern = al.trim(kern, start_db=-50.0, end_db=-70.0, preroll_ms=0.0)
        pos = int(round(k * lp.period_s * SR))
        idx = (pos + np.arange(len(kern))) % n
        np.add.at(out, idx, kern)
    if lp.bed is not None:
        bed = boss_presets.synth(lp.bed.preset, lp.bed.p, lp.bed.seed, peak_db=None, dur=lp.length_s)
        out += to_ceiling(bed) * 10 ** (lp.bed.gain / 20) * (np.max(np.abs(out)) / 10 ** (bp.SFX_PEAK_CEILING_DB / 20))
    return circ_highpass(out - out.mean(), 25.0)


def level_variants(spec: bp.Sfx, mixes: list) -> list:
    """Every variant to the same short LUFS: the category target, or the quietest variant's reachable level when the
    -3 dBFS ceiling (plus the allowed limiter gain reduction) stops it earlier."""
    target, max_gr = bp.CATEGORIES[spec.category]
    target = spec.target if spec.target is not None else target
    max_gr = spec.max_gr_db if spec.max_gr_db is not None else max_gr
    tops = [CEILING_DB - peak_db(x) + max_gr for x in mixes]
    reach = [al.max_short_lufs(render_gain(x, g, CEILING_DB)) for x, g in zip(mixes, tops)]
    common = min([target] + reach)
    out = []
    for x, top in zip(mixes, tops):
        lo, hi = top - 40.0, top
        for _ in range(22):
            mid = 0.5 * (lo + hi)
            if al.max_short_lufs(render_gain(x, mid, CEILING_DB)) < common:
                lo = mid
            else:
                hi = mid
        out.append(render_gain(x, hi if al.max_short_lufs(render_gain(x, hi, CEILING_DB)) <= common + 0.05 else lo, CEILING_DB))
    return out


def source_labels(variant) -> list:
    labels = []
    layers = [l for k in variant.kernels for l in k.layers] + ([variant.bed] if variant.bed else []) if isinstance(variant, bp.Loop) \
        else variant.layers
    for layer in layers:
        if isinstance(layer, bp.S):
            labels.append(f"synth:{layer.preset}")
        elif isinstance(layer, bp.Roll):
            labels.extend(sorted({shared_repo.repo_path(r) for r in layer.rels}))
        else:
            labels.append(shared_repo.repo_path(layer.rel))
    return list(dict.fromkeys(labels))


def license_of(label: str) -> str:
    for prefix, lic in bp.SOURCE_LICENSES.items():
        if label.startswith("synth:") and prefix == "synth:":
            return lic
        if label.startswith(shared_repo.ASSETS_PREFIX + prefix):
            return lic
    return "unknown"


# ----------------------------------------------------------------------------------------------------------
# BGM
# ----------------------------------------------------------------------------------------------------------

def process_bgm(src: str):
    x = al.decode(src, 2)
    info = {"source_samples": len(x)}
    if len(x) > 1 and np.array_equal(x[-1], x[0]) and np.any(x[0] != 0):
        x = x[:-1]
        info["dropped_duplicate_wrap_frame"] = True
    before = al.integrated_lufs(x)
    y, gain, capped = al.normalize_integrated(x, bp.BGM_TARGET_LUFS, bp.BGM_PEAK_CEILING_DB - 0.3)
    info.update({"source_lufs": round(before, 2), "gain_db": round(gain, 2), "peak_capped": bool(capped)})
    return y, info


def verify_ogg(path: str, reference: np.ndarray) -> dict:
    z = al.decode(path, 2)
    steps = np.abs(np.diff(z, axis=0)).max(axis=1)
    return {
        "samples": len(z),
        "seconds": round(len(z) / SR, 3),
        "length_matches_source": len(z) == len(reference),
        "int_lufs": round(al.integrated_lufs(z), 2),
        "peak_db": round(peak_db(z), 2),
        "wrap_step_vs_p999": round(float(np.max(np.abs(z[0] - z[-1])) / np.percentile(steps, 99.9)), 3),
        "loop": al.loop_report(z),
        "music": al.music_features(z),
    }


# ----------------------------------------------------------------------------------------------------------
# Checks
# ----------------------------------------------------------------------------------------------------------

def read_wav(path: str):
    with wave.open(path, "rb") as w:
        fmt = (w.getnchannels(), w.getsampwidth(), w.getframerate())
        pcm = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0
    return fmt, pcm


def loop_seam(x: np.ndarray) -> dict:
    steps = np.abs(np.diff(x))
    edge = int(0.05 * SR)
    return {"wrap_step": round(float(abs(x[0] - x[-1])), 5),
            "wrap_step_vs_p999": round(float(abs(x[0] - x[-1]) / (np.percentile(steps, 99.9) + 1e-12)), 3),
            "edge_rms_jump_db": round(al.db(al._rms(x[:edge])) - al.db(al._rms(x[-edge:])), 2),
            "int_lufs_tiled": round(al.integrated_lufs(np.tile(x, 4)), 2)}


def check(report) -> list:
    problems = []
    for fname, r in report["sfx"].items():
        if r["format"] != [1, 2, SR]:
            problems.append(f"{fname}: format {r['format']} (want mono 16-bit 44.1 kHz)")
        if r["peak_db"] > bp.SFX_PEAK_CEILING_DB + 0.01:
            problems.append(f"{fname}: peak {r['peak_db']} dBFS > {bp.SFX_PEAK_CEILING_DB}")
        if r["key"] == "BossLaserLoop":
            if r["samples"] != SR:
                problems.append(f"{fname}: loop length {r['samples']} samples (want exactly {SR})")
            if r["seam"]["wrap_step_vs_p999"] > 1.0:
                problems.append(f"{fname}: loop seam step {r['seam']['wrap_step_vs_p999']} x p99.9")
            continue
        if r["lead_ms"] > 2.0:
            problems.append(f"{fname}: {r['lead_ms']} ms of leading silence")
        if r["tv_drop_db"] > TV_DROP_MAX_DB:
            problems.append(f"{fname}: loses {r['tv_drop_db']} dB on TV speakers (> {TV_DROP_MAX_DB}; sub-dominated mix)")
        if r["key"] in INSTANT and r["to_6db_ms"] > INSTANT_MS:
            problems.append(f"{fname}: instant SFX reaches -6 dB of its peak only at {r['to_6db_ms']} ms")
    levels = {}
    for fname, r in report["sfx"].items():
        levels.setdefault(r["key"], []).append(r["short_lufs"])
    for key, ls in levels.items():
        if max(ls) - min(ls) > 0.3:
            problems.append(f"{key}: round-robin variants differ by {max(ls) - min(ls):.2f} LU")
    for fname, r in report["bgm"].items():
        if not r["length_matches_source"]:
            problems.append(f"{fname}: length changed by Vorbis ({r['samples']} vs {r['source_samples']})")
        if abs(r["int_lufs"] - bp.BGM_TARGET_LUFS) > 0.5:
            problems.append(f"{fname}: {r['int_lufs']} LUFS (target {bp.BGM_TARGET_LUFS})")
        if r["peak_db"] > bp.BGM_PEAK_CEILING_DB:
            problems.append(f"{fname}: peak {r['peak_db']} dBFS > {bp.BGM_PEAK_CEILING_DB}")
        if r["wrap_step_vs_p999"] > 1.0:
            problems.append(f"{fname}: loop seam step {r['wrap_step_vs_p999']} x p99.9")
    return problems


# Worst-case sums as Unity plays them: mono clip on a 2D source = same signal on both channels, unity gain, default
# sliders, music at full level (no duck), every 0.25 s offset over the whole loop.
HEADROOM = [
    ("rocket volley x3 + hurt", "BossBattleRoad", [("BossRocketLaunch_1", 0.0), ("BossRocketLaunch_2", 0.15), ("BossRocketLaunch_3", 0.3),
                                                   ("BossRocketHit_1", 1.0), ("BossRocketHit_2", 1.15), ("BossRocketHit_3", 1.3),
                                                   ("BossHurt_1", 1.35)]),
    ("slam + crack + bricks + hit", "BossBattleRoad", [("BossGroundSlam_1", 0.0), ("BossCrackGlow_1", 0.1), ("BossBrickGrow_1", 0.55),
                                                       ("BossVehicleHit_1", 0.6)]),
    ("rock impact on car", "BossBattleRoad", [("BossRockImpact_1", 0.0), ("BossVehicleHit_2", 0.0), ("BossCrackGlow_2", 0.05)]),
    ("wall smash", "BossBattleRoad", [("BossWallSmash_1", 0.0), ("BossMinionHit_1", 0.0)]),
    ("laser on target", "BossBattleSky", [("BossLaserLoop_1", 0.0)] + [(f"BossLaserHit_{1 + i % 3}", 0.125 * i) for i in range(8)]),
    ("wake: roar + shockwave", "BossBattleSky", [("BossWake_1", 0.0), ("BossRoar_1", 0.0), ("BossBannerSlam_1", 0.1)]),
    ("clap slam on jet", "BossBattleSky", [("BossSpikeSlam_1", 0.0), ("BossVehicleHit_3", 0.0)]),
    ("finale chain", "BossBattleSky", [("BossRocketHit_1", 0.0), ("BossRocketHit_2", 0.3), ("BossRocketHit_3", 0.6), ("BossExplode_1", 0.9),
                                       ("BossBannerSlam_1", 1.4)]),
]


def headroom(sfx: dict, bgm: dict, step_s: float = 0.25) -> list:
    rows = []
    for label, loop, events in HEADROOM:
        n = max(int(t * SR) + len(sfx[f]) for f, t in events)
        chain = np.zeros(n)
        for f, t in events:
            o = int(t * SR)
            chain[o:o + len(sfx[f])] += sfx[f]
        music = bgm[loop]
        worst, values = -120.0, []
        for off in range(0, len(music), int(step_s * SR)):
            seg = np.take(music, np.arange(off, off + n), axis=0, mode="wrap")
            v = peak_db(seg + chain[:, None])
            values.append(v)
            worst = max(worst, v)
        rows.append({"scenario": label, "loop": loop, "chain_only_db": round(peak_db(chain), 2), "max_db": round(worst, 2),
                     "median_db": round(float(np.median(values)), 2), "max_after_trim_db": round(worst - 6.0, 2)})
    return rows


# ----------------------------------------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------------------------------------

def collect_rels() -> set:
    rels = {b.rel for b in bp.BGM.values()}
    for spec in bp.SFX.values():
        for v in spec.variants:
            layers = [l for k in v.kernels for l in k.layers] if isinstance(v, bp.Loop) else v.layers
            for layer in layers:
                if isinstance(layer, bp.R):
                    rels.add(layer.rel)
                elif isinstance(layer, bp.Roll):
                    rels.update(layer.rels)
    return rels


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sfx-out", default=os.path.join(UNITY, SFX_REL))
    ap.add_argument("--bgm-out", default=os.path.join(UNITY, BGM_REL))
    ap.add_argument("--cache", default=os.path.join(ROOT, "Tools", "Staging", ".cache", "boss_audio_src"))
    ap.add_argument("--preview", default="", help="directory for the review PNGs + report JSON")
    ap.add_argument("--no-manifest", action="store_true", help="do not (re)write Tools/Audio/boss_audio_manifest.json")
    ap.add_argument("--only", default="", help="comma-separated SFX keys to render (iteration aid; no BGM, no manifest)")
    ap.add_argument("--jobs", type=int, default=12)
    a = ap.parse_args()
    only = set(filter(None, a.only.split(",")))
    os.makedirs(a.sfx_out, exist_ok=True)
    os.makedirs(a.bgm_out, exist_ok=True)

    rels = collect_rels()
    paths = shared_repo.fetch_all(rels, a.cache, a.jobs)
    print(f"sources: {len(paths)} shared-repo files materialized/verified")

    manifest = {"about": "Boss Battle audio (Tools/Audio/build_boss_audio.py; picks: boss_picks.py). Paths are relative to "
                         "ninjago-unity/. Do not hand-edit.",
                "sfx": {}, "bgm": {}, "sources": {}, "sfx_levels": {}, "loudness_plan": {
                    "sfx_peak_ceiling_dbfs": bp.SFX_PEAK_CEILING_DB, "bgm_integrated_lufs": bp.BGM_TARGET_LUFS,
                    "bgm_peak_ceiling_dbfs": bp.BGM_PEAK_CEILING_DB,
                    "sfx_categories_short_lufs": {k: v[0] for k, v in bp.CATEGORIES.items()}},
                "unity_import": UNITY_IMPORT}
    report = {"sfx": {}, "bgm": {}}
    rendered, written_files = {}, set()
    for key in bp.ORDER:
        if only and key not in only:
            continue
        spec = bp.SFX[key]
        if spec.loop:
            mixes = [render_loop(v, paths) for v in spec.variants]
            mixes = [m * 10 ** ((min(bp.CATEGORIES["loop"][0] - al.max_short_lufs(m), CEILING_DB - peak_db(m))) / 20) for m in mixes]
        else:
            mixes = [trim_lead(m) for m in level_variants(spec, [render_variant(v, paths) for v in spec.variants])]
        files = []
        for i, (variant, x) in enumerate(zip(spec.variants, mixes), start=1):
            fname = f"{key}_{i}.wav"
            path = os.path.join(a.sfx_out, fname)
            al.write_wav_mono16(path, x)
            fmt, pcm = read_wav(path)
            info = al.analyze(pcm)
            info.update({"key": key, "enum": spec.enum, "category": spec.category, "format": list(fmt),
                         "lead_ms": round(al._silence_len(pcm, -50.0) / SR * 1000, 2), "bytes": os.path.getsize(path),
                         "tv_drop_db": round(tv_drop_db(pcm), 2)})
            if spec.loop:
                info["seam"] = loop_seam(pcm)
            report["sfx"][fname] = info
            rendered[fname[:-4]] = pcm
            written_files.add(fname)
            files.append(f"{SFX_REL}/{fname}")
            labels = source_labels(variant)
            manifest["sources"][fname] = [{"source": s, "licence": license_of(s)} for s in labels]
            manifest["sfx_levels"][f"{SFX_REL}/{fname}"] = {"peak_db": info["peak_db"], "short_lufs": info["short_lufs"],
                                                            "samples": info["samples"], "seconds": info["dur_s"]}
        manifest["sfx"][key] = {"enum": spec.enum, "files": files, "category": spec.category, "loop": spec.loop,
                                "trigger": spec.trigger, "notes": spec.notes}

    bgm_signals = {}
    if not only:
        for name, bgm in bp.BGM.items():
            fname = f"{name}.ogg"
            y, info = process_bgm(paths[bgm.rel])
            out = os.path.join(a.bgm_out, fname)
            al.encode_ogg(out, y, bp.BGM_QUALITY)
            info.update(verify_ogg(out, y))
            info["bytes"] = os.path.getsize(out)
            report["bgm"][fname] = info
            bgm_signals[name] = al.decode(out, 2)
            src = shared_repo.repo_path(bgm.rel)
            manifest["bgm"][name] = {"enum": bgm.enum, "file": f"{BGM_REL}/{fname}", "source": src, "licence": license_of(src),
                                     "notes": bgm.notes, "int_lufs": info["int_lufs"], "peak_db": info["peak_db"],
                                     "seconds": info["seconds"]}
        report["headroom"] = headroom(rendered, bgm_signals)
        stale = [f for f in os.listdir(a.sfx_out) if f.endswith(".wav") and f not in written_files]
        for f in stale:
            os.remove(os.path.join(a.sfx_out, f))
            if os.path.exists(os.path.join(a.sfx_out, f + ".meta")):  # Unity's importer record of the removed clip
                os.remove(os.path.join(a.sfx_out, f + ".meta"))
            print(f"removed stale {f}")

    problems = check(report)
    print_table(report)
    if not a.no_manifest and not only:
        manifest["sfx_levels"] = dict(sorted(manifest["sfx_levels"].items()))
        with open(os.path.join(HERE, "boss_audio_manifest.json"), "w") as fh:
            json.dump(manifest, fh, indent=2)
            fh.write("\n")
    if a.preview:
        write_preview(a.preview, report, rendered, bgm_signals)
    if problems:
        print("PROBLEMS:\n  " + "\n  ".join(problems))
        return 1
    print(f"OK: {len(report['sfx'])} SFX files ({len({r['key'] for r in report['sfx'].values()})} keys), {len(report['bgm'])} BGM")
    return 0


def print_table(report):
    print(f"{'file':28s} {'dur':>6s} {'peak':>6s} {'sLUFS':>6s} {'onset':>6s} {'-6dB':>6s} {'cen':>6s} {'tvdrop':>6s}")
    for fname, r in report["sfx"].items():
        extra = f" seam {r['seam']}" if "seam" in r else ""
        print(f"{fname:28s} {r['dur_s']:6.2f} {r['peak_db']:6.2f} {r['short_lufs']:6.1f} {r['onset_lufs']:6.1f} {r['to_6db_ms']:6.1f} "
              f"{r['centroid_hz']:6d} {r['tv_drop_db']:6.1f}{extra}")
    for fname, r in report["bgm"].items():
        print(f"{fname}: {r['seconds']} s, {r['int_lufs']} LUFS, peak {r['peak_db']} dBFS, gain {r['gain_db']} dB, "
              f"seam {r['wrap_step_vs_p999']} x p99.9, same length {r['length_matches_source']}, {r['music']}")
    for row in report.get("headroom", []):
        print(f"headroom {row['scenario']:30s} over {row['loop']:15s}: chain {row['chain_only_db']:+6.2f}, with music max "
              f"{row['max_db']:+6.2f} (median {row['median_db']:+6.2f}) dBFS; with a -6 dB master trim {row['max_after_trim_db']:+6.2f}")


def write_preview(out_dir: str, report, rendered, bgm_signals):
    os.makedirs(out_dir, exist_ok=True)
    rows = []
    for fname, r in report["sfx"].items():
        rows.append({"label": f"{r['enum']} {fname}", "category": SHEET_CATEGORY[r["category"]], "samples": rendered[fname[:-4]],
                     "sub": f"{r['dur_s']:.2f}s pk {r['peak_db']:.1f} sL {r['short_lufs']:.1f} on {r['onset_lufs']:.1f} "
                            f"-6dB@{r['to_6db_ms']:.0f}ms cen {r['centroid_hz']} [{r['category']}]"})
    sheet.sfx_sheet(rows, os.path.join(out_dir, "boss_sfx_contact_sheet.png"),
                    "Boss Battle SFX 800-834 (waveform + log spectrogram 40 Hz-18 kHz)", cols=4, max_s=3.2)
    per = 21
    for page in range(0, len(rows), per):
        sheet.sfx_sheet(rows[page:page + per], os.path.join(out_dir, f"boss_sfx_page{page // per + 1}.png"),
                        f"Boss Battle SFX ({page + 1}-{min(page + per, len(rows))} of {len(rows)})", cols=3, max_s=3.2)
    sheet.loudness_bars([{"label": f"{r['enum']} {f}", "value_db": r["short_lufs"], "onset_db": r["onset_lufs"],
                          "category": SHEET_CATEGORY[r["category"]],
                          "target": bp.SFX[r["key"]].target if bp.SFX[r["key"]].target is not None else bp.CATEGORIES[r["category"]][0]}
                         for f, r in report["sfx"].items()],
                        os.path.join(out_dir, "boss_sfx_loudness.png"), "Boss SFX short LUFS (bar), onset (black tick), target (white)")
    sheet.onset_sheet([{"label": f, "samples": rendered[f[:-4]], "category": SHEET_CATEGORY[report["sfx"][f]["category"]]}
                       for f in report["sfx"] if report["sfx"][f]["key"] in INSTANT], os.path.join(out_dir, "boss_sfx_onsets.png"),
                      "Instant boss SFX: first 250 ms")
    loops = [{"label": f"{name}.ogg", "samples": x, "sub": json.dumps({k: report['bgm'][name + '.ogg'][k] for k in
                                                                         ('seconds', 'int_lufs', 'peak_db', 'wrap_step_vs_p999')})}
             for name, x in bgm_signals.items()]
    if "BossLaserLoop_1" in rendered:
        lx = np.tile(rendered["BossLaserLoop_1"], 3)
        loops.append({"label": "BossLaserLoop_1.wav x3 (wrap close-up = the file's own end | start)",
                      "samples": np.stack([lx, lx], axis=1), "sub": json.dumps(report["sfx"]["BossLaserLoop_1.wav"]["seam"])})
    if loops:
        sheet.bgm_sheet(loops, os.path.join(out_dir, "boss_loops.png"), "Boss loops: BGM + laser loop (right = wrap point)")
    if report.get("headroom"):
        sheet.headroom_bars(report["headroom"], os.path.join(out_dir, "boss_headroom.png"),
                            "Boss worst-case chains over the boss loops (no master trim exists yet)", -6.0)
    with open(os.path.join(out_dir, "boss_audio_report.json"), "w") as fh:
        json.dump(report, fh, indent=1, default=float)


if __name__ == "__main__":
    sys.exit(main())
