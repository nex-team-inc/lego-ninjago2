"""Boss Battle arena pieces (TDD 4 + 8.2), authored in Unity space with bbw_kit (x right, y up, z = away from the
game camera; the vehicle sits at z = 0 and the world scrolls toward -z).

Scrolling pieces are 20 m long (z -10..+10) so a 120 m ScrollingStrip wraps after six of them (GDD 12: a backdrop
silhouette hides the wrap); patterns that cross the segment ends repeat with the 20 m period, so neighbours tile.
Conventions (also in the manifest per piece):
  * BB_RoadSegment: 6.4 x 20 m, top surface at y 0, pivot at the centre of the top face. Flat palette colour
    blocks only (lane slabs, gold lane dashes 0.5 x 2.5 m): no feature below 0.5 m, no relief (GDD 12).
  * BB_Shoulder: one road edge, 2.4 m wide. Pivot on the road edge line (local x 0 = road edge, +x outward),
    centred in z, ground at y 0. Right edge: position (+3.2, 0, z); left edge: (-3.2, 0, z) rotated 180 about Y.
  * BB_Cliff_L_* / BB_Cliff_R_*: pivot on the cliff foot line at the road side (local x 0), centred in z; the mass
    extends outward (-x for L, +x for R). Place at x = -5.4 / +5.4.
  * BB_Arch_*: torii spanning the road, pillars at x +-5.0, bottom-centre pivot; god rays (R) go through them.
  * BB_Brazier: bottom-centre; `Flame` child (pivot at the bowl top) for flicker; flames stay <= HDR 1.3.
  * Sky pieces (phase 2, dark violet): BB_CloudSlab_* (pivot at the centre of the flat base), BB_FloatingIsle_*
    (pivot at the centre of the top surface), BB_Moon (disc facing -Z, centred, radius 10 m).
  * BB_Backdrop_Ridge / BB_Backdrop_Temple: bottom-centre, front face at z 0 facing the camera; place at z ~100.
"""
import math

from mathutils import Vector

from bbw_kit import (C, Geo, M, Piece, RX, RY, RZ, S, T, box, cbox, drop, flat, hash01, ico, jitter, lathe, loft,
                     orient_out, paint, prism_y, prism_z, rect_ring, rng, strip, stud, stud_grid)

SEG = 20.0          # scrolling segment length (m)
ROAD_HALF = 3.2     # road half width
SHOULDER = 2.4      # shoulder width (road edge -> cliff foot + overlap)
CLIFF_X = 5.4       # cliff foot line


def quad_xz(x0, x1, z0, z1, y, color):
    return flat([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], color)


def wobble(z, seed, amp, period=SEG):
    """Smooth periodic offset (period = segment length) so crack lines tile across segments."""
    a = 2 * math.pi * z / period
    return amp * (0.6 * math.sin(a + seed) + 0.3 * math.sin(2 * a + seed * 1.7) + 0.2 * math.sin(3 * a + seed * 0.3))


# ================================================================== road
def road_segment():
    """3-lane road surface 6.4 x 20 m (lanes centred at x -2 / 0 / +2). Large staggered stone slabs in two close
    shades (gray 5/6, an occasional 4) and muted gold lane dashes on the lane borders at x +-1 (0.5 m wide, 2.5 m
    long, every 5 m) so the lanes and the speed read while the hazards stay the brightest thing on the road."""
    p = Piece("BB_RoadSegment", 400, pivot="top", notes="road surface 6.4 x 20 m; pivot centre of the top face")
    r = p.part("Road")
    xs = [-ROAD_HALF, -1.25, -0.75, 0.75, 1.25, ROAD_HALF]
    dz = 1.25
    nz = int(SEG / dz)
    stagger = {0: 0, 2: 1, 4: 0}
    for ci in range(5):
        for zi in range(nz):
            z0 = -SEG / 2 + zi * dz
            if ci in (1, 3):
                dash = (zi // 2) % 2 == 0
                col = C("skin", 8) if dash else C("gray", 5)
            else:
                slab = ((zi + stagger[ci]) // 2) % (nz // 2)
                h = hash01(ci * 3.1 + (1.3 if ci == 4 else 0.0), slab * 1.7, 5.0)
                col = C("gray", 5 if h < 0.5 else 6 if h < 0.9 else 4)
            r.add(quad_xz(xs[ci], xs[ci + 1], z0, z0 + dz, 0.0, col))
    p.preview = {"views": [(0, 35), (180, 35)]}
    return p


# ================================================================== shoulder (kerb + basalt + lava cracks)
def shoulder():
    """Road shoulder (one side), 2.4 m wide x 20 m: sandstone kerb (0.5 m, 0.14 m high, 1.25 m blocks), dark basalt
    ground and meandering lava cracks. The lava is ambience: dim emissive orange 6 / yellow 6 (HDR ~1.2), wide strips
    (0.45 m glow, 0.18 m core) so nothing thinner than ~0.2 m crawls at 360p."""
    p = Piece("BB_Shoulder", 450, pivot="free", notes="road shoulder; local x 0 = road edge, +x outward; y 0 ground")
    s = p.part("Shoulder")
    kw, kh, kc = 0.5, 0.14, 0.05
    nz = 16
    for zi in range(nz):
        z0, z1 = -SEG / 2 + zi * SEG / nz, -SEG / 2 + (zi + 1) * SEG / nz
        shade = 9 if zi % 2 == 0 else 8
        prof = [(0.0, -0.02), (0.0, kh - kc), (kc, kh), (kw, kh), (kw, 0.0)]
        g = Geo()
        for (xa, ya), (xb, yb) in zip(prof, prof[1:]):
            q = [(xa, ya, z0), (xb, yb, z0), (xb, yb, z1), (xa, ya, z1)]
            nx, ny = (yb - ya), -(xb - xa)        # 2D normal of the profile edge, pointing to the road / up
            g.add(flat(q, normal=(-abs(nx) if xa == xb == 0.0 else nx, abs(ny) if ny else 0.0, 0.0)))
        g.add(flat([(0.0, -0.02, z0), (kc, kh, z0), (kw, kh, z0), (kw, 0.0, z0), (0.0, -0.02, z0)][:4] and
                   [(0.0, -0.02, z0), (0.0, kh - kc, z0), (kc, kh, z0), (kw, kh, z0), (kw, 0.0, z0)],
                   normal=(0, 0, -1)))
        s.add(paint(g, lambda c, n, sh=shade: C("skin", sh + (2 if n.y > 0.3 and n.y < 0.95 else 1 if n.y > 0.95
                                                               else 0) - (2 if n.z < -0.5 else 0))))
    # basalt ground in 2.5 m blocks, slightly darker toward the cliff
    for zi in range(8):
        z0 = -SEG / 2 + zi * 2.5
        for xa, xb, base in ((kw, 1.45, 3), (1.45, SHOULDER, 2)):
            h = hash01(zi * 1.9, xa, 2.0)
            s.add(quad_xz(xa, xb, z0, z0 + 2.5, 0.0, C("purple" if h > 0.6 else "gray", base + (1 if h > 0.85 else 0))))
    # lava cracks: one meandering main crack + short branches, periodic in z (tiles across segments)
    n = 13
    main = [(1.4 + wobble(-SEG / 2 + SEG * i / (n - 1), 0.7, 0.32), -SEG / 2 + SEG * i / (n - 1)) for i in range(n)]
    branches = [[main[3], (1.0, main[3][1] + 0.9), (0.72, main[3][1] + 1.6)],
                [main[7], (1.85, main[7][1] - 0.8), (2.15, main[7][1] - 1.7)],
                [main[10], (0.95, main[10][1] - 0.6), (0.7, main[10][1] - 1.5)]]
    for pts, k in [(main, 1.0)] + [(b, 0.75) for b in branches]:
        taper = [k] * len(pts) if k == 1.0 else [k * (1.0 - 0.6 * i / (len(pts) - 1)) for i in range(len(pts))]
        s.add(strip(pts, [0.75 * t for t in taper], 0.004, C("gray", 1)))
        s.add(strip(pts, [0.45 * t for t in taper], 0.008, C("orange", 6, True)))
        s.add(strip(pts, [0.18 * t for t in taper], 0.012, C("yellow", 6, True)))
    # rubble along the cliff foot
    for i, (x, z, r) in enumerate(((2.05, -7.5, 0.38), (2.2, -2.8, 0.3), (1.95, 1.6, 0.45), (2.25, 5.4, 0.32),
                                   (2.0, 8.6, 0.26))):
        g = ico(r, subdiv=0, jitter=0.2, seed=40 + i).copy(T(x, r * 0.35, z) @ S(1.2, 0.7, 1.0))
        g.deform(lambda v: Vector((v.x, max(v.y, -0.02), v.z)))
        s.add(paint(g, lambda c, n_: C("purple", 4 if n_.y > 0.5 else 2)))
    p.preview = {"views": [(-30, 35), (150, 35)]}
    return p


# ================================================================== canyon cliffs
STRATA = [("brown", 6), ("orange", 6), ("brown", 7), ("skin", 6), ("orange", 5)]


def _layer_poly(x_in, depth, z0, z1, r, jag=0.45):
    """Footprint of one stratum (road side at x_in, extending +x): faceted road-side edge, rounded back."""
    zs = [z0, z0 + (z1 - z0) * 0.33 + r.uniform(-0.4, 0.4), z0 + (z1 - z0) * 0.68 + r.uniform(-0.4, 0.4), z1]
    front = [(x_in + r.uniform(-jag, jag) * (0.4 if i in (0, 3) else 1.0), z) for i, z in enumerate(zs)]
    back = [(x_in + depth + r.uniform(-0.6, 0.6), z1 - 0.3), (x_in + depth + r.uniform(-0.6, 0.6), z0 + 0.3)]
    return front + back


def cliff(name, seed, mirror):
    """Canyon wall chunk, 21 m long (overlaps its neighbours by 0.5 m), 11-16 m tall, ~9 m deep: three column blocks
    of stacked sandstone strata that step back from the road (ledges catch the low sun, bands read at 360p), big
    LEGO studs along the top edges (they bump the silhouette against the sky) and a fallen giant brick on a ledge."""
    p = Piece(name, 800, pivot="free", notes="canyon cliff chunk; local x 0 = cliff foot at the road side")
    c = p.part("Cliff")
    r = rng(seed)
    breaks = [-SEG / 2 - 0.5, -3.6 + r.uniform(-1, 1), 3.4 + r.uniform(-1, 1), SEG / 2 + 0.5]
    g_all = Geo()
    tops = []
    for bi in range(3):
        z0, z1 = breaks[bi], breaks[bi + 1]
        y = 0.0
        x_in = r.uniform(-0.2, 0.4)
        heights = [r.uniform(2.6, 3.4), r.uniform(2.8, 3.6), r.uniform(2.6, 3.8), r.uniform(2.4, 3.6)]
        nlayers = 4 if r.uniform(0, 1) > 0.35 else 3
        for li in range(nlayers):
            h = heights[li]
            depth = r.uniform(7.0, 9.0) - li * 0.6
            poly = _layer_poly(x_in, depth, z0 + r.uniform(0, 0.4), z1 - r.uniform(0, 0.4), r)
            cx = sum(q[0] for q in poly) / len(poly)
            cz = sum(q[1] for q in poly) / len(poly)
            local = [(q[0] - cx, q[1] - cz) for q in poly]
            lay = prism_y(local, 0.0, h, top_scale=r.uniform(0.9, 0.96)).copy(T(cx, y, cz))
            drop(lay, lambda cc, n: n.y < -0.99)
            fam, base = STRATA[(li + bi + seed) % len(STRATA)]

            def rule(cc, n, fam=fam, base=base, li=li):
                h_ = hash01(cc.x, cc.y, cc.z, seed * 1.3)
                s = base + (2 if n.y > 0.6 else 0) + (-1 if h_ < 0.25 else 1 if h_ > 0.85 else 0)
                if n.x > 0.4:                      # back faces (never seen): flat dark
                    s = base - 2
                return C(fam, s)
            g_all.add(paint(lay, rule))
            tops.append((x_in, y + h, z0, z1))
            y += h
            x_in += r.uniform(0.6, 1.3)
        # big studs along the top front edge of the column (silhouette bumps)
        x_top, y_top = x_in - 0.2, y
        nst = max(2, int((z1 - z0) / 2.6))
        for si in range(nst):
            z = z0 + (z1 - z0) * (si + 0.5) / nst
            g_all.add(paint(stud(0.55, 0.4, 6), lambda cc, n: C("orange", 8 if n.y > 0.9 else 6)),
                      T(x_top + 0.9, y_top - 0.02, z))
    # fallen giant 1x2 brick on the first ledge of the middle column, tilted
    x_l, y_l, z0, z1 = tops[len(tops) // 2 - 1] if len(tops) > 3 else tops[0]
    brick = Geo()
    brick.add(paint(box(2.2, 1.1, 1.1, bottom=False), lambda cc, n: C("red", 7 if n.y > 0.9 else 8 if n.y > 0.3 else 5)))
    brick.add(paint(stud_grid(2, 1, 1.1, 1.1, 0.33, 0.25, 1.1), lambda cc, n: C("red", 8 if n.y > 0.9 else 5)))
    g_all.add(brick, M((1.2, 0.0, -6.0), yaw=r.uniform(15, 35), roll=-8))
    # boulders at the foot
    for i, (z, rad) in enumerate(((-8.0, 0.9), (0.5, 1.2), (6.8, 0.8))):
        b = ico(rad, subdiv=0, jitter=0.2, seed=seed * 10 + i).copy(T(0.2 + rad * 0.3, rad * 0.4, z + r.uniform(-1, 1)))
        b.deform(lambda v: Vector((v.x, max(v.y, -0.05), v.z)))
        g_all.add(paint(b, lambda cc, n: C("brown", 6 if n.y > 0.5 else 4)))
    if mirror:
        g_all = Geo().add(g_all, S(-1.0, 1.0, 1.0))
    c.add(g_all)
    p.preview = {"views": [(-50 if not mirror else 50, 15), (130 if not mirror else -130, 15)]}
    return p


# ================================================================== torii arches
PILLAR_X = 5.0


def _kasagi_bottom(x):
    t = max(0.0, (abs(x) - 5.6) / 1.5)
    return 7.62 + 0.55 * t * t


def _kasagi_top(x):
    return 8.36 + 0.6 * (abs(x) / 7.1) ** 3


def _beam(xa, xb, bot, top, n=8, jag_a=None, jag_b=None):
    """Front-view polygon of a beam between xa and xb (bottom/top curves); jag_* = jagged broken end offsets."""
    xs = [xa + (xb - xa) * i / n for i in range(n + 1)]
    poly = [(x, bot(x)) for x in xs]
    if jag_b:
        mid = (bot(xb) + top(xb)) / 2
        poly += [(xb + jag_b[0], mid - 0.15), (xb - jag_b[1], mid + 0.1)]
    poly += [(x, top(x)) for x in reversed(xs)]
    if jag_a:
        mid = (bot(xa) + top(xa)) / 2
        poly += [(xa - jag_a[0], mid + 0.12), (xa + jag_a[1], mid - 0.1)]
    return poly


def torii(name, ruined):
    """Ruined torii gate spanning the road and shoulders (pillars at x +-5, top beam 14 m wide, 8.9 m tall at the
    tips): weathered vermilion pillars on stone plinths, dark kasagi with gold tips and a row of studs riding its top
    (brick-built flavour, reads as bumps on the silhouette), a dark plaque. Variant B is broken: the right pillar
    snapped at 4 m, the beams end in jagged breaks past the centre and a beam chunk lies on the right shoulder."""
    p = Piece(name, 800, pivot="bottom" if not ruined else "free", shadows=True,
              notes="torii arch spanning the road; pillars at x +-5")
    a = p.part("Arch")
    red = lambda cc, n: C("red", 9 if n.y > 0.5 else 8 if n.z < -0.5 else 7)  # noqa: E731
    pillar_top = 7.25
    for side in (-1, 1):
        x = side * PILLAR_X
        top = pillar_top if not (ruined and side > 0) else 4.1
        pl = lathe([(0.46, 0.55), (0.44, 1.25), (0.4, top)], 8, phase=math.pi / 8)
        drop(pl, lambda cc, n: n.y < -0.99)
        if ruined and side > 0:                   # jagged snapped top
            pl.deform(lambda v, top=top: Vector((v.x, v.y + (0.35 * hash01(v.x, v.z, 3.0) - 0.1 if v.y > top - 0.01
                                                             else 0.0), v.z)))
        a.add(paint(pl, lambda cc, n: C("gray", 1) if cc.y < 1.25 else red(cc, n)), T(x, 0, 0))
        plinth = box(1.5, 0.55, 1.5)
        a.add(paint(plinth, lambda cc, n: C("gray", 7 if n.y > 0.9 else 5)), T(x, 0, 0))
    xa, xb = -6.95, 6.95
    jag = None
    if ruined:
        xb, jag = 1.6, (0.35, 0.3)
    kasagi = prism_z(_beam(xa, xb, _kasagi_bottom, _kasagi_top, 10, jag_b=jag), -0.42, 0.42)

    def kasagi_rule(cc, n):
        if abs(cc.x) > 6.3:
            return C("yellow", 9 if n.y > 0.3 else 8)            # gold tips
        if n.y > 0.4:
            return C("gray", 2)
        return C("gray", 2 if n.y > -0.3 else 1) if n.z < -0.5 or n.z > 0.5 else C("gray", 1)
    a.add(paint(kasagi, kasagi_rule))
    shimaki = prism_z(_beam(-6.25, 6.25 if not ruined else 1.3, lambda x: 7.25, lambda x: 7.62, 2,
                            jag_b=(0.25, 0.2) if ruined else None), -0.36, 0.36)
    a.add(paint(shimaki, red))
    nuki = prism_z(_beam(-6.1, 6.1 if not ruined else 2.2, lambda x: 5.9, lambda x: 6.42, 2,
                         jag_b=(0.3, 0.25) if ruined else None), -0.24, 0.24)
    a.add(paint(nuki, red))
    strut = box(0.6, 0.85, 0.36, y0=6.42)
    a.add(paint(strut, red))
    plaque = box(1.05, 0.95, 0.14, y0=6.37)
    a.add(paint(plaque, lambda cc, n: C("yellow", 8) if n.z > -0.5 else C("gray", 1)), T(0, 0, -0.25))
    inner = box(0.75, 0.65, 0.04, y0=6.52)
    a.add(paint(inner, C("indigo", 2)), T(0, 0, -0.34))
    # studs riding the kasagi
    for i in range(11):
        x = -6.0 + 1.2 * i
        if x > xb - 0.4:
            break
        a.add(paint(stud(0.22, 0.17, 6), lambda cc, n: C("gray", 3 if n.y > 0.9 else 2)), T(x, _kasagi_top(x) - 0.04, 0))
    if ruined:
        # fallen beam chunk on the right shoulder (rotated, half sunk)
        chunk = prism_z(_beam(2.2, 6.2, _kasagi_bottom, _kasagi_top, 4, jag_a=(0.3, 0.3)), -0.42, 0.42)
        chunk = chunk.copy(T(0, -_kasagi_bottom(4.2), 0))
        a.add(paint(chunk, kasagi_rule), M((5.6, 0.75, -1.4), yaw=-62, roll=-14))
    p.preview = {"views": [(0, 8), (35, 15)]}
    return p


# ================================================================== brazier
def brazier():
    """Stone brazier (2.2 m): stepped plinth with corner studs, octagonal post, wide bowl with a gold rim; `Flame`
    child (pivot at the bowl top, y 1.66) with four flame tongues in dim emissive yellow 6 / orange 5 / red 6
    (ambience, HDR ~1.0-1.3) - V's ember particles and R's point light pulses do the rest."""
    p = Piece("BB_Brazier", 360, pivot="bottom", notes="stone brazier; Flame child flickers; ambience <= HDR 1.3")
    b = p.part("Brazier")
    stone = lambda cc, n: C("gray", 8 if n.y > 0.9 else 6 if n.z < -0.5 or n.x < -0.5 else 5)  # noqa: E731
    b.add(paint(box(0.95, 0.28, 0.95), stone))
    b.add(paint(box(0.7, 0.18, 0.7, y0=0.28), stone))
    b.add(paint(stud_grid(2, 2, 0.7, 0.7, 0.08, 0.07, 0.28), lambda cc, n: C("gray", 9 if n.y > 0.9 else 6)))
    post = lathe([(0.2, 0.46), (0.17, 1.05), (0.25, 1.16)], 8, phase=math.pi / 8)
    drop(post, lambda cc, n: n.y < -0.99)
    b.add(paint(post, stone))
    bowl = lathe([(0.2, 1.14), (0.52, 1.42), (0.6, 1.58), (0.56, 1.66)], 8, phase=math.pi / 8)

    def bowl_rule(cc, n):
        if n.y > 0.95 and cc.y > 1.6:
            return C("red", 3)                    # embers bed
        if cc.y > 1.5:
            return C("yellow", 8 if n.y > -0.2 else 6)
        return C("gray", 6 if n.y > -0.3 else 4)
    b.add(paint(bowl, bowl_rule))
    f = p.part("Flame", (0.0, 1.66, 0.0), parent="Brazier")
    tongues = (((0.0, 0.0), 0.75, 0.24, 0, 0), ((0.2, 0.08), 0.5, 0.17, 110, 12), ((-0.17, 0.12), 0.46, 0.16, 250, 14),
               ((0.03, -0.2), 0.42, 0.15, 30, -12))
    for i, ((x, z), h, r, yaw, lean) in enumerate(tongues):
        t = lathe([(r, 0.0), (r * 1.1, h * 0.22), (r * 0.55, h * 0.58), (0.0, h)], 6, phase=i * 0.4)
        jitter(t, 0.025, 50 + i)
        t = Geo().add(t, M((x, 1.64, z), yaw=yaw, pitch=lean))

        def flame_rule(cc, n, h=h):
            k = (cc.y - 1.64) / h
            return C("yellow", 6, True) if k < 0.3 else C("orange", 5, True) if k < 0.68 else C("red", 6, True)
        f.add(paint(t, flame_rule))
    p.preview = {"yaw": 25, "pitch": 15}
    return p


# ================================================================== sky: storm clouds
def cloud(name, seed):
    """Storm cloud slab (~36 x 6 x 24 m) for the phase-2 cloud layers: flat base, lumpy faceted top. Dark violet:
    moon-lit tops indigo/purple 4-5, sides 2-3, underside 1; a few dim magenta emissive faces (purple 4, HDR ~0.6)
    hint at lightning inside the storm. Pivot at the centre of the flat base."""
    p = Piece(name, 650, pivot="free", notes="storm cloud slab; pivot centre of the flat base")
    c = p.part("Cloud")
    r = rng(seed)
    g = Geo()
    lumps = [(0.0, 0.0, 9.0, 0.5, 1)]
    for i in range(6):
        a = r.uniform(0, math.tau)
        d = r.uniform(5.5, 11.0)
        lumps.append((math.cos(a) * d * 1.4, math.sin(a) * d * 0.9, r.uniform(4.0, 7.0), r.uniform(0.45, 0.7),
                      1 if i < 3 else 0))
    for i, (x, z, rad, sq, sub) in enumerate(lumps):
        b = ico(rad, subdiv=sub, jitter=0.16, seed=seed * 7 + i).copy(T(x, 0.0, z) @ S(1.25, sq, 1.0))
        b.deform(lambda v: Vector((v.x, v.y if v.y > 0 else v.y * 0.06, v.z)))
        g.add(b)

    def rule(cc, n):
        h = hash01(cc.x, cc.y, cc.z, seed * 0.7)
        if n.y < -0.5:
            return C("indigo", 1)
        if n.y > 0.55:
            return C("purple" if h > 0.5 else "indigo", 5 if h > 0.3 else 4)
        if h > 0.93:
            return C("purple", 4, True)
        return C("indigo", 3 if n.y > 0.1 else 2)
    c.add(paint(g, rule))
    p.preview = {"views": [(0, 12), (90, 25)]}
    return p


# ================================================================== sky: floating ruined islands
def isle(name, seed, ruin):
    """Floating island (~11 m across, 10 m deep): jagged inverted rock cone with a hanging chunk, mossy flat top and
    a small ruin: 'torii' (gate + stone lantern with a dim warm light) or 'pagoda' (broken roof on three columns).
    Pivot at the centre of the top surface."""
    p = Piece(name, 900, pivot="free", notes="floating ruined island; pivot centre of the top surface")
    i_ = p.part("Isle")
    r = rng(seed)
    prof = [(0.0, -10.0), (1.4, -7.2), (3.0, -4.4), (4.5, -2.0), (5.4, -0.6), (5.6, 0.0)]
    body = lathe(prof, 9, phase=r.uniform(0, 1))
    jitter(body, 0.45, seed)
    body.deform(lambda v: Vector((v.x, 0.0 if v.y > -0.3 else v.y, v.z)))

    def rock_rule(cc, n):
        h = hash01(cc.x, cc.y, cc.z, seed)
        if n.y > 0.9 and cc.y > -0.05:
            return C("teal", 4 if h > 0.4 else 3)        # moss top (cold night green)
        s = 4 if cc.y > -2.5 else 3 if cc.y > -6 else 2
        return C("purple" if h > 0.55 else "indigo", s + (1 if n.y > 0.3 else 0))
    i_.add(paint(body, rock_rule))
    hang = lathe([(0.0, -4.5), (1.2, -1.8), (1.6, 0.0)], 6, phase=0.3)
    jitter(hang, 0.25, seed + 3)
    i_.add(paint(hang, rock_rule), T(3.2, -2.5, 1.5))
    for k in range(4):                               # rubble on the top
        a = r.uniform(0, math.tau)
        d = r.uniform(2.5, 4.6)
        rb = ico(r.uniform(0.35, 0.6), subdiv=0, jitter=0.2, seed=seed + 20 + k)
        i_.add(paint(rb, lambda cc, n: C("gray", 5 if n.y > 0.5 else 3)), T(math.cos(a) * d, 0.15, math.sin(a) * d))
    red = lambda cc, n: C("red", 7 if n.y > 0.5 else 5)  # noqa: E731
    if ruin == "torii":
        for sx in (-1.6, 1.6):
            i_.add(paint(lathe([(0.25, 0.0), (0.22, 3.4)], 6), red), T(sx, 0, -1.0))
        i_.add(paint(prism_z(_beam(-2.5, 2.5, lambda x: 3.4, lambda x: 3.8 + 0.08 * x * x / 2.5, 6), -0.3, 0.3),
                     lambda cc, n: C("gray", 2)), T(0, 0, -1.0))
        i_.add(paint(box(3.6, 0.3, 0.25, y0=2.7), red), T(0, 0, -1.0))
        lantern = Geo()
        lantern.add(paint(box(0.5, 0.3, 0.5), lambda cc, n: C("gray", 6 if n.y > 0.9 else 4)))
        lantern.add(paint(lathe([(0.1, 0.3), (0.1, 1.0)], 6), C("gray", 5)))
        lantern.add(paint(box(0.42, 0.36, 0.42, y0=1.0), C("orange", 6, True)))
        lantern.add(paint(box(0.62, 0.18, 0.62, y0=1.36, top_scale=(0.3, 0.3)), C("gray", 3)))
        i_.add(lantern, T(1.8, 0, 1.6))
    else:
        for k, (x, z, h) in enumerate(((-1.6, -0.6, 3.0), (1.5, -0.9, 2.2), (0.0, 1.5, 3.0))):
            col = lathe([(0.3, 0.0), (0.27, h)], 6, phase=0.2 * k)
            i_.add(paint(col, lambda cc, n: C("gray", 7 if n.y > 0.5 else 5)), T(x, 0, z))
        roof = prism_z(_beam(-2.8, 2.8, lambda x: 0.0, lambda x: 0.55 + 0.9 * (1 - (abs(x) / 2.8)), 6), -1.8, 1.8)
        roof = Geo().add(roof, M((0.2, 3.0, 0.0), roll=-9, yaw=12))
        i_.add(paint(roof, lambda cc, n: C("red", 6 if n.y > 0.5 else 3)))
        i_.add(paint(cbox(0.5, 0.5, 0.5), C("cyan", 6, True)), T(-0.5, 0.35, 0.4))   # glowing shard
    p.preview = {"views": [(0, 10), (120, 20)]}
    return p


# ================================================================== sky: moon
def moon():
    """Moon disc, radius 10 m, facing -Z (the camera), centred: pale emissive face (gray 8, HDR ~1.25 with its lit
    part, ambience), darker emissive craters and a dim indigo halo ring behind it (a stepped pixel-art glow)."""
    p = Piece("BB_Moon", 160, pivot="centre", notes="moon disc facing -Z; radius 10")
    m = p.part("Moon")
    R_ = 10.0

    def disc(cx, cy, rad, segs, z, color):
        pts = [(cx + rad * math.cos(math.tau * i / segs), cy + rad * math.sin(math.tau * i / segs), z)
               for i in range(segs)]
        return flat(pts, color, normal=(0, 0, -1))
    m.add(disc(0, 0, R_, 20, 0.0, C("gray", 8, True)))
    m.add(disc(-0.6, 0.5, R_ * 0.86, 20, -0.02, C("gray", 9, True)))        # lit crescent offset
    for (x, y, rad, s) in ((-3.5, 3.2, 2.1, 7), (3.8, -1.5, 2.6, 7), (-1.0, -5.0, 1.5, 7), (4.2, 4.6, 1.2, 8),
                           (-5.6, -1.8, 1.1, 8)):
        m.add(disc(x, y, rad, 7, -0.05, C("gray", s, True)))
    for (r0, r1, z, col) in ((R_ * 1.06, R_ * 1.22, 0.3, C("indigo", 4, True)), (R_ * 1.22, R_ * 1.42, 0.35,
                                                                                    C("indigo", 2, True))):
        for i in range(20):
            a0, a1 = math.tau * i / 20, math.tau * (i + 1) / 20
            q = [(r0 * math.cos(a0), r0 * math.sin(a0), z), (r1 * math.cos(a0), r1 * math.sin(a0), z),
                 (r1 * math.cos(a1), r1 * math.sin(a1), z), (r0 * math.cos(a1), r0 * math.sin(a1), z)]
            m.add(flat(q, col, normal=(0, 0, -1)))
    p.preview = {"views": [(0, 0)]}
    return p


# ================================================================== backdrop silhouettes
def _ridge_outline(seed, x0, x1, step, lo, hi, peaks):
    r = rng(seed)
    pts = []
    x = x0
    while x < x1:
        h = r.uniform(lo, hi)
        for px, ph, pw in peaks:
            if abs(x - px) < pw:
                h = max(h, ph * (1 - abs(x - px) / pw) + lo * 0.5)
        pts.append((x, h))
        x += step * r.uniform(0.7, 1.3)
    pts.append((x1, r.uniform(lo, hi)))
    return [(x0, 0.0)] + pts + [(x1, 0.0)]


def backdrop_ridge():
    """Distant mountain backdrop for z ~100: a 240 m wide jagged front ridge (dark purple, 12-34 m) and a taller
    back ridge 30 m behind (lighter, aerial perspective) with two needle peaks. Front faces look along -Z.
    Bottom-centre pivot; hides the scenery wrap of the 120 m strips."""
    p = Piece("BB_Backdrop_Ridge", 700, pivot="front", notes="backdrop mountains; front face at z 0; place at z ~100")
    b = p.part("Ridge")
    front = _ridge_outline(3, -120, 120, 11.0, 9.0, 22.0, ((-62, 34, 14), (48, 30, 12), (95, 26, 10)))
    back = _ridge_outline(9, -120, 120, 13.0, 18.0, 34.0, ((-20, 52, 9), (14, 46, 7), (-90, 44, 12)))
    fg = prism_z(front, 0.0, 6.0)
    bg = prism_z(back, 12.0, 16.0)
    drop(fg, lambda cc, n: n.y < -0.99)
    drop(bg, lambda cc, n: n.y < -0.99)
    b.add(paint(fg, lambda cc, n: C("purple", 3 if cc.y > 14 else 2) if n.z < -0.5 else C("purple", 2)))
    b.add(paint(bg, lambda cc, n: C("purple", 5 if cc.y > 28 else 4) if n.z < -0.5 else C("purple", 4)))
    p.preview = {"views": [(0, 3)]}
    return p


def _roof(width, depth, h, curl):
    """Pagoda roof tier (front view polygon extruded in depth): wide eaves with upturned tips."""
    w = width / 2
    poly = [(-w - curl, h * 0.35 + curl * 0.6), (-w, 0.0), (w, 0.0), (w + curl, h * 0.35 + curl * 0.6),
            (w * 0.55, h * 0.45), (w * 0.3, h), (-w * 0.3, h), (-w * 0.55, h * 0.45)]
    return prism_z(poly, -depth / 2, depth / 2)


def backdrop_temple():
    """Ninjago temple silhouette on a rock spire (~46 m tall) for the backdrop: four pagoda tiers with upturned
    eaves, a gold spire tip and dim warm windows (orange 5 emissive, HDR ~0.9) that twinkle against dusk and night.
    Front faces look along -Z; bottom-centre pivot; place at z ~100-115, off-centre."""
    p = Piece("BB_Backdrop_Temple", 600, pivot="free", notes="backdrop temple on a spire; place at z ~100-115")
    t = p.part("Temple")
    spire = prism_z([(-11, 0), (-7, 9), (-6.5, 16), (-4.5, 22), (4.8, 22), (6.2, 15), (8.5, 7), (12, 0)], -5, 5)
    jitter(spire, 0.6, 77)
    spire.deform(lambda v: Vector((v.x, max(v.y, 0.0), v.z)))
    t.add(paint(spire, lambda cc, n: C("purple", 3 if cc.y > 12 else 2)))
    y = 22.0
    for k, (w, h) in enumerate(((12.0, 5.0), (9.5, 4.2), (7.2, 3.6), (5.0, 3.0))):
        body = box(w * 0.62, h * 0.6, 6.0 - k, y0=y)
        t.add(paint(body, lambda cc, n: C("red", 3 if n.z < -0.5 else 2)))
        for wx in ([-1.6, 0.0, 1.6] if k < 2 else [-0.8, 0.8] if k < 3 else [0.0]):
            win = box(0.7, 0.9, 0.1, y0=y + h * 0.12)
            t.add(paint(win, C("orange", 5, True)), T(wx * (w / 10), 0, -(6.0 - k) / 2 - 0.05))
        roof = _roof(w, 7.0 - k, h * 0.55, 1.2 - k * 0.15)
        t.add(paint(roof, lambda cc, n: C("indigo", 2 if n.y > 0.3 else 1)), T(0, y + h * 0.6, 0))
        y += h * 0.6 + h * 0.5
    tip = lathe([(0.5, 0.0), (0.3, 3.5), (0.0, 6.0)], 6)
    t.add(paint(tip, C("yellow", 7)), T(0, y - 0.3, 0))
    p.preview = {"views": [(0, 3)]}
    return p


REGISTRY = {
    "BB_RoadSegment": road_segment,
    "BB_Shoulder": shoulder,
    "BB_Cliff_R_A": lambda: cliff("BB_Cliff_R_A", 3, False),
    "BB_Cliff_R_B": lambda: cliff("BB_Cliff_R_B", 8, False),
    "BB_Cliff_L_A": lambda: cliff("BB_Cliff_L_A", 5, True),
    "BB_Cliff_L_B": lambda: cliff("BB_Cliff_L_B", 12, True),
    "BB_Arch_A": lambda: torii("BB_Arch_A", False),
    "BB_Arch_B": lambda: torii("BB_Arch_B", True),
    "BB_Brazier": brazier,
    "BB_CloudSlab_A": lambda: cloud("BB_CloudSlab_A", 4),
    "BB_CloudSlab_B": lambda: cloud("BB_CloudSlab_B", 9),
    "BB_FloatingIsle_A": lambda: isle("BB_FloatingIsle_A", 6, "torii"),
    "BB_FloatingIsle_B": lambda: isle("BB_FloatingIsle_B", 13, "pagoda"),
    "BB_Moon": moon,
    "BB_Backdrop_Ridge": backdrop_ridge,
    "BB_Backdrop_Temple": backdrop_temple,
}
