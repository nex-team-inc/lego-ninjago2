"""Boss Battle synthesized SFX layers (numpy, deterministic per seed). Each preset -> mono float array at 44.1 kHz.

Used by build_boss_audio.py, either as the whole sound or as a layer under shared-repo samples (boss_picks.py).
Every preset returns its raw mix; `synth()` peak-normalises it to -1 dBFS (layer gains are applied by the caller).

  rock_whistle    rising "fwee" whistle that recedes upward: the rock hurled into the sky (~1.05 s, air time 1.0 s).
  lock_beeps      latch click + three rising beeps (F6, G6, C7) at the reticle's 6 Hz blink rate: the rock locks on.
  dive_klaxon     two submarine "ah-OO-gah" klaxon calls: the clap warning ("DIVE!").
  countdown_beep  one Ready 3-2-1 tick: A5 square beep with a low brick-drum knock under it.
  laser_pew       one auto-fire bolt (fast falling square chirp + click); the laser loop places 8 of them per second.
  laser_hum       seamless 8 Hz-pulsed energy bed under the bolts (integer-Hz partials, so any whole second loops).
  zap             crushed energy crunch: the bolt striking the Core (layer under an energy-impact sample).
  tink            inharmonic metal "tink": a bolt glancing off armour.
  brick_rise      short rising square blip with a knock at the end: a brick row growing out of the road.
  glow_sizzle     swelling, trembling hot-crack hum: the slam crack glowing before bricks grow.
  taiko           deep drum hit with skin modes + slap (TV-speaker audible): banner slam body.
  sub_rumble      low noise + sine weight with saturation harmonics (so TV speakers still carry it); `dur` in s.
  inhale          rising band-passed air swell that stops dead (0.80 s after trimming): the Titan inhales before the clap.
  whoom           downward shockwave sweep + sub drop: the Titan wakes.
  sparkle         C6-E6-G6-C7-E7 triangle arpeggio + air shimmer: rocket crate appears.
  power_up        fast rising C major square arpeggio + "lock-and-load" clunk: rockets armed.
  jet_whine       rising turbine whine + flame roar: the Titan's jets ignite.
  rebuild_chime   bright C6-G6-C7 chord ping: the jet snaps together.
  bwomp           falling square "bwomp": damage taken.
  sad_horn        cartoon "wah wah wah waaah" (G4 F#4 F4 E4 with vibrato on the last): knocked out.
"""
import numpy as np
from scipy.signal import butter, lfilter, sosfilt

from sfxsynth import SR, Voice, render

# ----------------------------------------------------------------------------------------------------------
# Building blocks
# ----------------------------------------------------------------------------------------------------------


def _t(dur: float) -> np.ndarray:
    return np.arange(int(round(dur * SR))) / SR


def _phase(freq: np.ndarray) -> np.ndarray:
    """Phase in cycles for a (possibly time-varying) frequency array."""
    return np.cumsum(freq) / SR


def _square(ph: np.ndarray, duty: float = 0.5) -> np.ndarray:
    return np.where((ph % 1.0) < duty, 1.0, -1.0)


def _saw(ph: np.ndarray) -> np.ndarray:
    return 2.0 * (ph % 1.0) - 1.0


def _tri(ph: np.ndarray) -> np.ndarray:
    return 4.0 * np.abs((ph % 1.0) - 0.5) - 1.0


def _sine(ph: np.ndarray) -> np.ndarray:
    return np.sin(2 * np.pi * ph)


def _bp(x: np.ndarray, lo: float, hi: float, order: int = 2) -> np.ndarray:
    sos = butter(order, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], "bandpass", output="sos")
    return sosfilt(sos, x)


def _lp(x: np.ndarray, hz: float, order: int = 2) -> np.ndarray:
    return sosfilt(butter(order, hz / (SR / 2), "low", output="sos"), x)


def _hp(x: np.ndarray, hz: float, order: int = 2) -> np.ndarray:
    return sosfilt(butter(order, hz / (SR / 2), "high", output="sos"), x)


def _sweep_bp(x: np.ndarray, f0: np.ndarray, q: float, block: int = 128) -> np.ndarray:
    """Band-pass with a centre frequency that follows `f0` (per-sample array), RBJ biquad updated per block."""
    y = np.zeros_like(x)
    zi = np.zeros(2)
    for s in range(0, len(x), block):
        fc = float(np.clip(f0[min(s + block // 2, len(f0) - 1)], 30.0, SR / 2 - 200))
        w = 2 * np.pi * fc / SR
        alpha = np.sin(w) / (2 * q)
        b = np.array([alpha, 0.0, -alpha])
        a = np.array([1 + alpha, -2 * np.cos(w), 1 - alpha])
        y[s:s + block], zi = lfilter(b / a[0], a / a[0], x[s:s + block], zi=zi)
    return y


def _env(n: int, attack: float, hold: float, release: float, curve: float = 2.0) -> np.ndarray:
    """Attack (linear) / hold / release (power curve) envelope over n samples."""
    t = np.arange(n) / SR
    e = np.ones(n)
    if attack > 0:
        e = np.where(t < attack, t / attack, e)
    rel_t = t - attack - hold
    e = np.where(rel_t > 0, np.clip(1 - rel_t / max(release, 1e-6), 0, 1) ** curve, e)
    return e


def _place(parts, total: float = 0.0) -> np.ndarray:
    """Sum (signal, offset_s, gain) tuples into one buffer."""
    n = max([int(round(off * SR)) + len(sig) for sig, off, _ in parts] + [int(total * SR)])
    out = np.zeros(n)
    for sig, off, g in parts:
        o = int(round(off * SR))
        out[o:o + len(sig)] += sig * g
    return out


def _sat(x: np.ndarray, drive: float) -> np.ndarray:
    m = np.max(np.abs(x)) or 1.0
    return np.tanh(x / m * drive) / np.tanh(drive)


def _rng(seed: int) -> np.random.Generator:
    return np.random.default_rng(1000 + seed)


# ----------------------------------------------------------------------------------------------------------
# Presets
# ----------------------------------------------------------------------------------------------------------


def rock_whistle(p: float = 1.0, seed: int = 0) -> np.ndarray:
    t = _t(1.05)
    u = t / t[-1]
    f = 700 * p * (2600 / 700) ** (1 - (1 - u) ** 2) * 2 ** (0.35 * np.sin(2 * np.pi * 6.0 * t) / 12)
    ph = _phase(f)
    tone = _sine(ph) + 0.18 * _tri(2 * ph)
    air = _sweep_bp(_rng(seed).uniform(-1, 1, len(t)), f, 6.0) * 2.5
    amp = np.minimum(t / 0.05, 1.0) * np.exp(-np.maximum(t - 0.16, 0) * 3.4)  # quick swell, then recedes (~-27 dB at the end)
    return (tone * 0.8 + air * 0.5) * amp


def lock_beeps(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    click_n = int(0.012 * SR)
    click = _hp(r.uniform(-1, 1, click_n), 2500) * np.exp(-np.arange(click_n) / (0.002 * SR))
    latch = _square(_phase(np.full(int(0.02 * SR), 1180.0 * p)), 0.3) * np.exp(-np.arange(int(0.02 * SR)) / (0.005 * SR))
    parts = [(click, 0.0, 0.9), (latch, 0.0, 0.35)]
    period = 1 / 6.0  # the reticle blinks at 6 Hz while locked
    for i, (note, dur) in enumerate(((1396.9, 0.075), (1568.0, 0.075), (2093.0, 0.13))):
        n = int(dur * SR)
        ph = _phase(np.full(n, note * p))
        beep = (_square(ph, 0.25) * 0.6 + _sine(ph) * 0.5 + _sine(2 * ph) * 0.15) * _env(n, 0.002, dur * 0.55, dur * 0.45, 1.5)
        parts.append((_lp(beep, 9000), 0.025 + i * period, 1.0))
    return _place(parts)


def _klaxon_call(p: float, dur: float = 0.42) -> np.ndarray:
    t = _t(dur)
    u = np.clip(t / 0.3, 0, 1)
    f = (220 + (560 - 220) * u ** 0.6) * p
    ph = _phase(f)
    raw = 0.6 * _saw(ph) + 0.4 * _square(ph, 0.4)
    raw *= 1 + 0.15 * np.sin(2 * np.pi * 30 * t)  # buzzy horn reed
    nasal = _bp(raw, 900, 1500) * 1.6 + _bp(raw, 2200, 3200) * 0.7 + _lp(raw, 700) * 0.6
    return _sat(nasal, 2.0) * _env(len(t), 0.02, dur - 0.08, 0.06, 1.0)


def dive_klaxon(p: float = 1.0, seed: int = 0) -> np.ndarray:
    return _place([(_klaxon_call(p), 0.0, 1.0), (_klaxon_call(p), 0.5, 1.0)])


def countdown_beep(p: float = 1.0, seed: int = 0) -> np.ndarray:
    n = int(0.16 * SR)
    ph = _phase(np.full(n, 880.0 * p))
    beep = (_square(ph, 0.5) * 0.45 + _tri(ph) * 0.6 + _sine(2 * ph) * 0.2) * _env(n, 0.002, 0.07, 0.088, 1.6)
    kn = int(0.07 * SR)
    knock = _sine(_phase(160 * p * 2 ** (-np.arange(kn) / SR / 0.07))) * np.exp(-np.arange(kn) / (0.02 * SR))
    return _place([(_lp(beep, 8000), 0.0, 1.0), (knock, 0.0, 0.8)])


def laser_pew(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    n = int(0.09 * SR)
    tt = np.arange(n) / SR
    f = 2600 * p * (700 / 2600) ** np.clip(tt / 0.07, 0, 1)
    ph = _phase(f)
    chirp = (_square(ph, 0.3) * 0.55 + _sine(ph) * 0.45) * np.exp(-tt / 0.03) * np.minimum(tt / 0.001, 1)
    cn = int(0.005 * SR)
    click = _hp(r.uniform(-1, 1, cn), 3000) * np.linspace(1, 0, cn)
    sn = int(0.04 * SR)
    thump = _sine(_phase(180 * 2 ** (-np.arange(sn) / SR / 0.04))) * np.exp(-np.arange(sn) / (0.012 * SR))
    return _place([(_lp(chirp, 9000), 0.0, 1.0), (click, 0.0, 0.5), (thump, 0.0, 0.3)])


def laser_hum(p: float = 1.0, seed: int = 0, dur: float = 1.0) -> np.ndarray:
    """Seamless for any whole number of seconds: every partial is an integer Hz and the AM is 8 Hz."""
    t = _t(dur)
    bed = 0.5 * _saw(110 * t) + 0.35 * np.sin(2 * np.pi * 220 * t) + 0.12 * np.sin(2 * np.pi * 331 * t)
    bed = _circ_lp(bed, 900)
    am = 0.55 + 0.45 * (0.5 + 0.5 * np.cos(2 * np.pi * 8 * t))  # peaks on the bolts (k / 8 s)
    sizzle = _circ_bp(_rng(seed).uniform(-1, 1, len(t)), 5000, 8000) * 0.18
    return bed * am + sizzle * am


def _circ_filter(x: np.ndarray, sos) -> np.ndarray:
    """Filter a periodic buffer without a seam: run over three copies and keep the middle one."""
    y = sosfilt(sos, np.concatenate([x, x, x]))
    return y[len(x):2 * len(x)]


def _circ_lp(x: np.ndarray, hz: float) -> np.ndarray:
    return _circ_filter(x, butter(2, hz / (SR / 2), "low", output="sos"))


def _circ_bp(x: np.ndarray, lo: float, hi: float) -> np.ndarray:
    return _circ_filter(x, butter(2, [lo / (SR / 2), hi / (SR / 2)], "bandpass", output="sos"))


def zap(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    n = int(0.12 * SR)
    tt = np.arange(n) / SR
    burst = _bp(r.uniform(-1, 1, n), 1200, 4500) * np.exp(-tt / 0.018)
    f = 900 * p * (300 / 900) ** np.clip(tt / 0.06, 0, 1)
    sq = _square(_phase(f), 0.4) * np.exp(-tt / 0.035)
    sq = np.round(sq * 8) / 8  # 4-bit crunch
    thump = _sine(_phase(140 * p * 2 ** (-tt / 0.06))) * np.exp(-tt / 0.02)
    return burst * 1.2 + sq * 0.45 + thump * 0.5


def tink(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    n = int(0.2 * SR)
    tt = np.arange(n) / SR
    f0 = 2400 * p * (1 - 0.03 * np.clip(tt / 0.05, 0, 1))
    out = np.zeros(n)
    for ratio, decay, amp in ((1.0, 0.11, 1.0), (2.76, 0.06, 0.55), (5.40, 0.03, 0.35), (8.93, 0.015, 0.2)):
        out += amp * np.sin(2 * np.pi * _phase(f0 * ratio)) * np.exp(-tt / decay)
    cn = int(0.003 * SR)
    out[:cn] += _hp(r.uniform(-1, 1, cn), 4000) * 0.6
    return out * np.minimum(tt / 0.0005, 1)


def brick_rise(p: float = 1.0, seed: int = 0) -> np.ndarray:
    t = _t(0.26)
    f = 180 * p * (480 / 180) ** (t / t[-1])
    blip = _square(_phase(f), 0.35) * _env(len(t), 0.004, 0.18, 0.076, 1.5)
    blip = _lp(np.round(blip * 16) / 16, 3500)
    kn = int(0.08 * SR)
    knock = _sine(_phase(210 * p * 2 ** (-np.arange(kn) / SR / 0.05))) * np.exp(-np.arange(kn) / (0.018 * SR))
    return _place([(blip, 0.0, 0.6), (knock, 0.25, 1.0)])


def glow_sizzle(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    t = _t(0.5)
    f = 220 * p * 2 ** (t / t[-1])
    tone = (_sine(_phase(f)) + 0.3 * _saw(_phase(f * 2.01))) * (1 + 0.35 * np.sin(2 * np.pi * 18 * t))
    tone = _lp(tone, 2500)
    crackle = np.zeros(len(t))
    idx = r.choice(len(t) - 200, 70, replace=False)
    crackle[idx] = r.uniform(-1, 1, 70)
    crackle = _bp(crackle, 2000, 7000) * 6
    amp = np.clip(t / 0.3, 0, 1) ** 1.5 * np.clip((0.5 - t) / 0.06, 0, 1)
    return (tone * 0.7 + crackle * 0.5) * amp


def taiko(p: float = 1.0, seed: int = 0) -> np.ndarray:
    """Odaiko hit. The 62 Hz body gives the weight on full-range speakers; the skin modes (~180/290 Hz), the slap and the
    saturation harmonics carry the "BOOM" on TV speakers, which roll off below ~150 Hz."""
    r = _rng(seed)
    n = int(0.75 * SR)
    tt = np.arange(n) / SR
    body = _sine(_phase(62 * p + 33 * p * np.exp(-tt / 0.03))) * np.exp(-tt / 0.22)
    over = _sine(_phase(141 * p * (1 - 0.06 * np.minimum(tt / 0.1, 1)))) * np.exp(-tt / 0.09) * 0.45
    skin = (_sine(_phase(182 * p * (1 - 0.08 * np.minimum(tt / 0.08, 1)))) * 0.8
            + _sine(_phase(291 * p * (1 - 0.08 * np.minimum(tt / 0.06, 1)))) * 0.45) * np.exp(-tt / 0.075)
    slap = _bp(r.uniform(-1, 1, n), 300, 2400) * np.exp(-tt / 0.014) * 1.2
    return _sat(body * 0.75 + over + skin + slap, 2.4) * np.minimum(tt / 0.0008, 1)


def sub_rumble(p: float = 1.0, seed: int = 0, dur: float = 1.2) -> np.ndarray:
    r = _rng(seed)
    t = _t(dur)
    noise = _lp(_lp(r.uniform(-1, 1, len(t)), 95 * p, 4), 140, 2) * 9
    tone = np.sin(2 * np.pi * _phase(48 * p * (1 - 0.15 * t / dur))) * (1 + 0.25 * np.sin(2 * np.pi * 7 * t))
    x = noise + tone * 0.8
    x = _sat(x, 2.2)  # 2nd/3rd harmonics (100-200 Hz) so small speakers still carry the weight
    return x * np.minimum(t / 0.008, 1) * np.exp(-t / (dur * 0.38))


def inhale(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    t = _t(0.87)
    u = t / t[-1]
    fc = 500 * p * (2400 / 500) ** u
    air = _sweep_bp(r.uniform(-1, 1, len(t)), fc, 1.4) * 3
    air += _sweep_bp(r.uniform(-1, 1, len(t)), fc * 0.5, 3.0) * 1.5
    whistle = np.sin(2 * np.pi * _phase(300 * p * (700 / 300) ** u)) * 0.12
    amp = u ** 1.6 * np.clip((t[-1] - t) / 0.008, 0, 1)
    return (air + whistle) * amp


def whoom(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    t = _t(0.9)
    fc = 3000 * p * (200 / 3000) ** np.clip(t / 0.6, 0, 1)
    sweep = _sweep_bp(r.uniform(-1, 1, len(t)), fc, 1.2) * 3
    sub = np.sin(2 * np.pi * _phase(90 * p * (40 / 90) ** np.clip(t / 0.8, 0, 1)))
    return (sweep * 0.8 + sub * 0.9) * np.minimum(t / 0.005, 1) * np.exp(-t / 0.3)


def sparkle(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    parts = []
    for i, f in enumerate((1046.5, 1318.5, 1568.0, 2093.0, 2637.0)):
        n = int(0.28 * SR)
        ph = _phase(np.full(n, f * p * (1 + 0.002 * (i % 2))))
        note = (_tri(ph) * 0.7 + _square(ph * 2, 0.25) * 0.12) * _env(n, 0.002, 0.01, 0.26, 2.2)
        parts.append((note, 0.045 * i, 1.0 - 0.08 * i))
    sn = int(0.55 * SR)
    shimmer = _bp(r.uniform(-1, 1, sn), 8000, 12500) * _env(sn, 0.05, 0.1, 0.4, 1.5) * 0.35
    parts.append((shimmer, 0.0, 1.0))
    return _place(parts)


def power_up(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    parts = []
    for i, f in enumerate((523.3, 659.3, 784.0, 1046.5, 1318.5, 1568.0, 2093.0)):
        n = int(0.12 * SR)
        ph = _phase(np.full(n, f * p))
        parts.append(((_square(ph, 0.25) * 0.5 + _tri(ph) * 0.5) * _env(n, 0.002, 0.02, 0.098, 1.8), 0.035 * i, 0.9))
    cn = int(0.06 * SR)
    ct = np.arange(cn) / SR
    clunk = _bp(r.uniform(-1, 1, cn), 800, 3000) * np.exp(-ct / 0.008) + _sine(_phase(200 * p * (1 - 0.3 * ct / 0.06))) * np.exp(-ct / 0.02)
    parts.append((clunk, 0.28, 1.1))
    tail_n = int(0.3 * SR)
    ph = _phase(np.full(tail_n, 2093.0 * p))
    parts.append((_tri(ph) * _env(tail_n, 0.002, 0.03, 0.27, 2.0), 0.28, 0.5))
    return _lp(_place(parts), 9000)


def jet_whine(p: float = 1.0, seed: int = 0) -> np.ndarray:
    r = _rng(seed)
    t = _t(1.6)
    u = np.clip(t / 1.2, 0, 1)
    f = 220 * p * (1400 / 220) ** (u ** 0.7)
    whine = _sweep_bp(_saw(_phase(f)), f * 2.0, 2.0) * 1.2 + np.sin(2 * np.pi * _phase(f)) * 0.3
    roar = _sweep_bp(r.uniform(-1, 1, len(t)), 300 + 1500 * u, 0.8) * 2
    amp = np.clip(t / 0.5, 0, 1) ** 1.5 * np.clip((1.6 - t) / 0.35, 0, 1)
    return (whine * 0.6 + roar * 0.7) * amp


def rebuild_chime(p: float = 1.0, seed: int = 0) -> np.ndarray:
    n = int(0.7 * SR)
    tt = np.arange(n) / SR
    out = np.zeros(n)
    for f, a in ((1046.5, 0.7), (1568.0, 0.5), (2093.0, 0.45), (3136.0, 0.15)):
        out += a * np.sin(2 * np.pi * _phase(np.full(n, f * p))) * np.exp(-tt / 0.22)
    return out * np.minimum(tt / 0.001, 1)


def bwomp(p: float = 1.0, seed: int = 0) -> np.ndarray:
    t = _t(0.24)
    f = 330 * p * (110 / 330) ** np.clip(t / 0.18, 0, 1)
    sq = _square(_phase(f), 0.5) * _env(len(t), 0.002, 0.12, 0.118, 1.5)
    return _lp(np.round(sq * 12) / 12, 2500)


def sad_horn(p: float = 1.0, seed: int = 0) -> np.ndarray:
    parts = []
    for i, f in enumerate((392.0, 370.0, 349.2)):
        n = int(0.26 * SR)
        tt = np.arange(n) / SR
        ph = _phase(f * p * (1 - 0.01 * tt / 0.26))
        parts.append(((_saw(ph) * 0.5 + _square(ph, 0.45) * 0.3) * _env(n, 0.012, 0.16, 0.088, 1.2), 0.3 * i, 1.0))
    n = int(0.95 * SR)
    tt = np.arange(n) / SR
    vib = 2 ** (0.45 * np.sin(2 * np.pi * 5.5 * tt) * np.clip(tt / 0.2, 0, 1) / 12)
    ph = _phase(329.6 * p * vib * (1 - 0.02 * tt))
    parts.append(((_saw(ph) * 0.5 + _square(ph, 0.45) * 0.3) * _env(n, 0.015, 0.45, 0.48, 1.4), 0.9, 1.0))
    x = _place(parts)
    return _lp(_bp(x, 150, 3500) * 0.8 + _lp(x, 1200) * 0.4, 3000)


def classic(voices) -> np.ndarray:
    """Hook for sfxsynth Voice lists (kept for quick experiments)."""
    return render(voices, peak_db=None)


PRESETS = {fn.__name__: fn for fn in (rock_whistle, lock_beeps, dive_klaxon, countdown_beep, laser_pew, laser_hum, zap, tink,
                                       brick_rise, glow_sizzle, taiko, sub_rumble, inhale, whoom, sparkle, power_up, jet_whine,
                                       rebuild_chime, bwomp, sad_horn)}


def synth(name: str, p: float = 1.0, seed: int = 0, peak_db: float = -1.0, **kwargs) -> np.ndarray:
    """Render a preset, remove DC and peak-normalise it (peak_db=None keeps the raw level)."""
    x = PRESETS[name](p=p, seed=seed, **kwargs)
    if name != "laser_hum":  # the hum is periodic; its mean is already ~0 and must not be shifted per segment
        x = x - np.mean(x)
    if peak_db is None:
        return x
    return x * (10 ** (peak_db / 20) / (np.max(np.abs(x)) or 1.0))


assert SR == 44100
_ = Voice  # re-exported for experiments
