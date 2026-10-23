"""Boss Battle gameplay props (TDD 8.2), authored in Unity space with bbw_kit (x right, y up, z = away from the game
camera). Every builder returns a bbw_kit.Piece; REGISTRY maps the FBX name to the builder.

Read rules (GDD 12 + research/art-pipeline 8): the road camera (0, 4, -11), pitch 9, FOV 50 at 640x360 gives
~386 / d px per metre (34 px/m at the car, 12 px/m at the brick rows at z 21, 9 px/m at the boss). The sun is low
AHEAD (euler 32, 215): every face the camera sees is in the shadow band, so each hazard carries an emissive tell
element (molten seam, glowing cracks, red spike tips, gust ribbons) and stays the brightest thing on screen;
ambience never exceeds HDR 1.3. Studs: pitch 0.5 m, radius 0.16-0.17, height 0.13 (taller than LEGO so they show as
bumps on the silhouette from the low camera), 6 sides = 16 tris each.
"""
import math

from bbw_kit import (C, Geo, M, Piece, RX, RY, RZ, S, T, aim, box, by_y, cbox, chamfer_box, drop, flat_poly, hash01, ico,
                     lathe, loft, orient_out, paint, plastic, prism_z, rect_ring, rng, strip, stud, stud_grid)


# ================================================================== helpers
def oct_ring(hx, hz, y, c):
    return [(-hx + c, y, -hz), (-hx, y, -hz + c), (-hx, y, hz - c), (-hx + c, y, hz),
            (hx - c, y, hz), (hx, y, hz - c), (hx, y, -hz + c), (hx - c, y, -hz)]


def oct_inset(hx, hz, y, c):
    d = c * math.sqrt(2.0)
    return [(-hx + d, y, -hz + c), (-hx + c, y, -hz + d), (-hx + c, y, hz - d), (-hx + d, y, hz - c),
            (hx - d, y, hz - c), (hx - c, y, hz - d), (hx - c, y, -hz + d), (hx - d, y, -hz + c)]


def toy_block(sx, sy, sz, c, y0=0.0, band=None, bottom=False):
    """Plastic brick body: vertical + top chamfers `c`; optional horizontal ring at y0 + band (a coloured plinth)."""
    hx, hz = sx / 2, sz / 2
    rings = [oct_ring(hx, hz, y0, c)]
    if band:
        rings.append(oct_ring(hx, hz, y0 + band, c))
    rings += [oct_ring(hx, hz, y0 + sy - c, c), oct_inset(hx, hz, y0 + sy, c)]
    return loft(rings, cap_bottom=bottom, cap_top=True)


def course_block(sx, sy, sz, c, y0=0.0):
    """Wall brick: top chamfer only (the course line catches the light), no bottom."""
    hx, hz = sx / 2, sz / 2
    return loft([rect_ring(hx, hz, y0), rect_ring(hx, hz, y0 + sy - c), rect_ring(hx - c, hz - c, y0 + sy)],
                cap_bottom=False, cap_top=True)


def stud_paint(fam, side, top, em=False):
    return lambda c, n: C(fam, top if n.y > 0.9 else side, em)


# ================================================================== BB_Brick
def brick():
    """Lane-cell brick barrier 2 x 1.1 x 0.7 m (+ 0.13 m studs), bottom-centre pivot: grows from the floor by scaling
    the root Y 0 -> 1. A 1x4 row of chunky studs reads on the top silhouette from the low road camera; the molten
    plinth (emissive orange, HDR ~2.1) ties the brick to the crack glow and keeps it the brightest thing in its lane
    against the back-lit dusk road."""
    W, H, D, c = 2.0, 1.1, 0.7, 0.09
    p = Piece("BB_Brick", 160, pivot="bottom", notes="lane-cell brick barrier; scale root Y 0->1 to grow")
    b = p.part("Brick")
    body = toy_block(W, H, D, c, band=0.16)

    def body_rule(cc, n):
        if cc.y < 0.16 and n.y < 0.3:
            return C("orange", 11, True)            # molten plinth
        return plastic("red", 10, hi=2, rim=1)(cc, n)
    b.add(paint(body, body_rule))
    studs = stud_grid(4, 1, 0.5, 0.5, 0.17, 0.13, H)
    b.add(paint(studs, stud_paint("red", 11, 13)))
    p.preview = {"yaw": 25, "pitch": 18}
    return p


# ================================================================== BB_Rock
def split_shell(g, planes, gap, seed=0):
    """Split a closed shell into chunks by the sides of `planes` (normals through the origin); each chunk keeps its
    own vertex copies and moves `gap` outward along its mean direction, so glowing core geometry shows through the
    fissures between the chunks."""
    from mathutils import Vector
    chunks = {}
    for idx, col in g.f:
        cc, _ = g.center_normal(idx)
        key = tuple(1 if cc.dot(Vector(pn).normalized()) > 0 else 0 for pn in planes)
        chunks.setdefault(key, []).append((idx, col))
    out = Geo()
    for key, faces in sorted(chunks.items()):
        vids = sorted({i for idx, _ in faces for i in idx})
        centre = sum((g.v[i] for i in vids), Vector()) / len(vids)
        off = centre.normalized() * gap if centre.length > 1e-6 else Vector()
        remap = {i: out.vert(g.v[i] + off) for i in vids}
        for idx, col in faces:
            out.face([remap[i] for i in idx], col)
    return out


def rock():
    """Craggy boulder (~1.25 m) split into three chunks around an emissive orange core: the fissures glow (the rock
    tell colour, HDR ~1.7) and a 1x2 orange toy brick is wedged into the top. Centred pivot: it tumbles in flight."""
    p = Piece("BB_Rock", 160, pivot="centre", notes="thrown rock; tumbles; centred")
    r = p.part("Rock")
    g = ico(0.6, subdiv=1, jitter=0.17, seed=11).copy(S(1.06, 0.92, 1.0))

    def rock_rule(cc, n):
        r_ = hash01(cc.x, cc.y, cc.z, 3.0)
        if n.y > 0.55:
            return C("brown", 8 if r_ > 0.45 else 7)
        if n.y < -0.4:
            return C("indigo", 2 if r_ > 0.5 else 3)
        if n.z < -0.3:
            return C("brown", 6 if r_ > 0.5 else 5)
        return C("gray", 6 if r_ > 0.5 else 5)
    paint(g, rock_rule)
    r.add(split_shell(g, [(0.3, 0.85, -0.4), (-0.75, 0.15, 0.62)], 0.045))
    r.add(paint(ico(0.5, subdiv=0, jitter=0.05, seed=5), C("orange", 11, True)))
    chunk = Geo()
    chunk.add(paint(box(0.5, 0.3, 0.3, bottom=False), plastic("orange", 9, hi=2, rim=1)))
    chunk.add(paint(stud_grid(2, 1, 0.25, 0.25, 0.085, 0.07, 0.3), stud_paint("orange", 10, 12)))
    r.add(chunk, M((-0.12, 0.3, -0.16), yaw=24, pitch=-26, roll=12))
    p.preview = {"yaw": 30, "pitch": 20}
    return p


# ================================================================== BB_PowerCrate
ROCKET_EMBLEM = [(0.0, 0.25), (-0.085, 0.1), (-0.085, -0.08), (-0.16, -0.17), (-0.16, -0.23), (-0.06, -0.15),
                 (0.06, -0.15), (0.16, -0.23), (0.16, -0.17), (0.085, -0.08), (0.085, 0.1)]
FLAME_EMBLEM = [(-0.055, -0.165), (0.055, -0.165), (0.03, -0.25), (0.0, -0.33), (-0.03, -0.25)]


def power_crate():
    """Rocket crate (0.9 m gold toy-brick cube + 2x2 studs, ~1.05 m tall), centred pivot (spins + bobs in code).
    Each side carries a dark navy plate with a glowing cyan rocket + yellow flame emblem (HDR ~2.2): the only cyan on
    the dusk road, so the reward never reads as a hazard (hazards are red/orange/magenta)."""
    p = Piece("BB_PowerCrate", 300, pivot="centre", notes="rocket power-up crate; spins/bobs in code; centred")
    cr = p.part("Crate")
    Sz, c = 0.9, 0.08
    body = toy_block(Sz, Sz, Sz, c, y0=-Sz / 2, bottom=True)
    cr.add(paint(body, plastic("yellow", 10, hi=2, rim=1)))
    cr.add(paint(stud_grid(2, 2, 0.42, 0.42, 0.12, 0.1, Sz / 2), stud_paint("yellow", 11, 13)))
    for i in range(4):
        side = Geo()
        plate = box(0.64, 0.64, 0.035, y0=-0.32, bottom=True)
        side.add(paint(plate, lambda cc, n: C("blue", 3 if n.z < -0.5 else 2)), T(0, 0, -Sz / 2 - 0.0175 + 0.002))
        emb = flat_poly(ROCKET_EMBLEM, 0.0, C("cyan", 11, True))
        fl = flat_poly(FLAME_EMBLEM, 0.0, C("yellow", 12, True))
        # emblem polygons are drawn in (x, y-up); RX(-90) stands the +Y-facing polygon up facing -Z
        m = T(0, 0.02, -Sz / 2 - 0.037) @ RZ(-30) @ RX(-90)
        side.add(emb, m)
        side.add(fl, m)
        cr.add(side, RY(90 * i))
    p.preview = {"yaw": 30, "pitch": 22}
    return p


# ================================================================== BB_Rocket
def rocket():
    """Homing rocket 0.9 m along +Z, centred pivot (code aims +Z at the target). `Exhaust` child (pivot at the
    nozzle) can flicker / scale. The camera sees it from behind: the exhaust is a bright yellow-white disc
    (rear-facing faces) with an orange rim, so a volley reads as three hot dots streaking toward the boss."""
    p = Piece("BB_Rocket", 150, pivot="centre", notes="homing rocket along +Z; Exhaust child flickers")
    body = p.part("Rocket")
    prof = [(0.065, -0.45), (0.085, -0.38), (0.095, -0.3), (0.095, 0.12), (0.075, 0.26), (0.0, 0.45)]
    # lathe axis +Y -> rocket axis: RX(90) maps +Y onto +Z (paint afterwards, in the final orientation)
    g = Geo().add(lathe(prof, 6, phase=0.0), RX(90))

    def body_rule(cc, n):
        z = cc.z
        if z < -0.38:
            return C("gray", 3)
        if z < -0.2:
            return C("red", 10 if n.y > 0.2 else 8)
        if z < 0.15:
            return C("gray", 13 if n.y > 0.2 else 11)
        return C("red", 12 if n.y > 0.2 else 10)
    body.add(paint(g, body_rule))
    fin = [(0.08, -0.42), (0.21, -0.47), (0.21, -0.38), (0.08, -0.22)]   # (radial x, axial y)
    for i in range(4):
        f = Geo().add(prism_z(fin, -0.012, 0.012), RZ(45 + 90 * i) @ RX(90))
        body.add(paint(f, lambda cc, n: C("yellow", 11 if n.y > 0.1 else 9)))
    p.pivot_parts = ["Rocket"]
    ex = p.part("Exhaust", (0.0, 0.0, -0.45), parent="Rocket")
    flame = Geo().add(lathe([(0.0, -0.5), (0.14, -0.12), (0.065, 0.0)], 6, phase=math.pi / 6), T(0, 0, -0.45) @ RX(90))
    # rear-facing faces (seen by the chase camera) white-hot, the rest orange
    ex.add(paint(flame, lambda cc, n: C("yellow", 14, True) if n.z < -0.3 else C("orange", 12, True)))
    p.preview = {"yaw": 140, "pitch": 18}
    return p


# ================================================================== BB_SideWall
def side_wall():
    """Minion wall: 1 (x) x 2 (y) x 6 (z) m toy-brick wall along Z, bottom-centre pivot (grows from the floor by
    scaling Y, or rises). Running bond of 0.5 m courses in light bluish grey / sand (castle LEGO), studs on top at
    0.5 m pitch. Not emissive: the wall is the smash target, the minion and the arrow prompt carry the tell."""
    p = Piece("BB_SideWall", 800, pivot="bottom", notes="minion side wall along the road edge; grows from the floor")
    w = p.part("Wall")
    th, L, hc, c = 1.0, 6.0, 0.5, 0.07
    rows = [[(-3, -1), (-1, 1), (1, 3)], [(-3, -2), (-2, 0), (0, 2), (2, 3)]]
    tones = [("gray", 10), ("skin", 10), ("gray", 9), ("skin", 9), ("gray", 11)]
    k_ = 0
    for ci in range(4):
        for z0, z1 in rows[ci % 2]:
            fam, base = tones[k_ % len(tones)]
            k_ += 1
            blk = course_block(th, hc, z1 - z0, c, y0=ci * hc)
            w.add(paint(blk, plastic(fam, base - (1 if ci == 0 else 0), hi=2, rim=1)), T(0, 0, (z0 + z1) / 2))
    w.add(paint(stud_grid(2, 12, 0.5, 0.5, 0.16, 0.13, 4 * hc), stud_paint("gray", 10, 12)))
    p.preview = {"yaw": -55, "pitch": 20}
    return p


# ================================================================== BB_SpikeWall
def spike_wall():
    """Clap wall: `Wall` (1 x 3.5 x 3 m dark violet stone bricks, iron face plate) + `Spikes` child (pivot on the inner
    face centre (0.5, 0, 0), points +X; slide it out along X). Centre pivot. Steel spikes with glowing red tips
    (HDR ~2.3) on a near-black iron plate: the tips read as a grid of red dots from the far sky camera, the steel
    as bright cones up close. The right wall is the same prefab rotated 180 deg about Y."""
    p = Piece("BB_SpikeWall", 900, pivot="centre", notes="clap spike wall; Spikes child slides out +X")
    w = p.part("Wall")
    th, H, L, hc, c = 1.0, 3.5, 3.0, 0.7, 0.06
    y0 = -H / 2
    rows = [[(-1.5, 0.0), (0.0, 1.5)], [(-1.5, -0.75), (-0.75, 0.75), (0.75, 1.5)]]
    for ci in range(5):
        for j, (z0, z1) in enumerate(rows[ci % 2]):
            shade = 4 + (1 if (ci + j) % 2 else 0)
            blk = course_block(th, hc, z1 - z0, c, y0=y0 + ci * hc)
            w.add(paint(blk, plastic("purple", shade, hi=2, rim=1)), T(0, 0, (z0 + z1) / 2))
    w.add(paint(stud_grid(2, 6, 0.5, 0.5, 0.16, 0.13, H / 2), stud_paint("purple", 5, 7)))
    # iron face plate on the spike side + a glowing magenta warning trim along its top and bottom edges
    plate = box(0.08, 3.0, 2.7, y0=-1.5, bottom=True)
    w.add(paint(plate, lambda cc, n: C("gray", 2 if n.x > 0.5 else 3)), T(0.5 + 0.04, 0, 0))
    for yy in (-1.5, 1.5):
        trim = box(0.1, 0.12, 2.7, y0=-0.06, bottom=True)
        w.add(paint(trim, C("magenta", 10, True)), T(0.5 + 0.05, yy, 0))
    p.pivot_parts = ["Wall"]
    sp = p.part("Spikes", (0.5, 0.0, 0.0), parent="Wall")
    big = [(y, z) for y in (-1.0, 0.0, 1.0) for z in (-0.9, 0.0, 0.9)]
    small = [(y, z) for y in (-0.5, 0.5) for z in (-0.45, 0.45)]
    for (y, z), (r, L_) in [(q, (0.27, 1.05)) for q in big] + [(q, (0.17, 0.65)) for q in small]:
        g = drop(lathe([(r, 0.0), (r * 0.42, L_ * 0.55), (0.0, L_)], 4, phase=math.pi / 4), lambda c_, n_: n_.y < -0.99)
        g = Geo().add(g, aim((0.58, y, z), (1.0, 0.0, 0.0)))

        def spike_rule(cc, n, L_=L_):
            if cc.x > 0.58 + L_ * 0.5:
                return C("red", 11, True)            # hot tip
            return C("gray", 12 if n.y > 0.3 else 8 if n.y < -0.3 else 10)
        sp.add(paint(g, spike_rule))
    p.preview = {"yaw": -60, "pitch": 15}
    return p


# ================================================================== BB_WindGust
def _helix_ribbon(turns, steps, r0, r1, h, width, thick, phase, outer, inner, edge):
    """Closed helical band around +Y (centred): 4 sides per step (outer/inner faces, top/bottom edges), tapered ends."""
    g = Geo()
    rings = []
    for i in range(steps + 1):
        t = i / steps
        a = phase + TAU_ * turns * t
        r = r0 + (r1 - r0) * t
        y = -h / 2 + h * t
        taper = min(1.0, t / 0.18, (1.0 - t) / 0.12) if 0 < t < 1 else 0.08
        w = width * (0.25 + 0.75 * max(0.0, taper))
        rad = (math.cos(a), 0.0, math.sin(a))
        th = thick * (0.5 + 0.5 * max(0.0, taper))
        pts = []
        for du, dr in ((+1, +1), (-1, +1), (-1, -1), (+1, -1)):   # (up, radial) corners: top-out, bottom-out, ...
            rr = r + dr * th / 2
            pts.append(g.vert((rad[0] * rr, y + du * w / 2, rad[2] * rr)))
        rings.append(pts)
    sides = ((0, 1, outer, "out"), (1, 2, edge, "down"), (2, 3, inner, "in"), (3, 0, edge, "up"))
    for i in range(steps):
        a, b = rings[i], rings[i + 1]
        t = (i + 0.5) / steps
        for s0, s1, col, kind in sides:
            q = [a[s0], b[s0], b[s1], a[s1]]
            cc, n = g.center_normal(q)
            ang = math.atan2(cc.z, cc.x)
            want = {"out": (math.cos(ang), 0, math.sin(ang)), "in": (-math.cos(ang), 0, -math.sin(ang)),
                    "up": (0, 1, 0), "down": (0, -1, 0)}[kind]
            if n.x * want[0] + n.y * want[1] + n.z * want[2] < 0:
                q.reverse()
            g.face(q, col(t) if callable(col) else col)
    for ring, sign in ((rings[0], -1), (rings[-1], 1)):
        q = list(ring)
        cc, n = g.center_normal(q)
        a = phase + (TAU_ * turns if sign > 0 else 0.0)
        tang = (-math.sin(a) * sign, 0.0, math.cos(a) * sign)
        if n.x * tang[0] + n.z * tang[2] < 0:
            q.reverse()
        g.face(q, edge)
    return g


TAU_ = math.tau


def wind_gust():
    """Devil-wind gust: two intertwined helical ribbons forming a 1.8 m funnel column (axis +Y, centred pivot; spin
    it about Y in code). Outer faces cyan-white emissive (core HDR ~1.6), inner faces magenta emissive (the rim you
    see through the gaps), dark indigo edges as a 1 px outline so the gust never melts into the violet sky."""
    p = Piece("BB_WindGust", 360, pivot="centre", notes="devil wind gust column; spin about Y")
    gp = p.part("Gust")
    for k_, ph in enumerate((0.0, math.pi)):
        g = _helix_ribbon(1.5, 18, 0.36, 0.7, 1.8, 0.3, 0.09, ph,
                          outer=lambda t: C("cyan", 10 + int(round(2 * t)), True),
                          inner=lambda t: C("magenta", 8 + int(round(2 * t)), True),
                          edge=C("indigo", 1))
        gp.add(g)
    p.preview = {"yaw": 20, "pitch": 12}
    return p


# ================================================================== BB_LaserBolt
def laser_bolt():
    """Laser bolt along +Z (1.2 x 0.2 m), centred, all emissive gold (yellow 13 body, white-hot yellow 15 tail seen
    from the chase camera). Gold stays apart from every hazard tell colour (rock orange, minion green, slam red,
    wind violet/cyan, clap white)."""
    p = Piece("BB_LaserBolt", 48, pivot="centre", notes="laser bolt along +Z")
    b = p.part("Bolt")
    g = lathe([(0.0, -0.6), (0.06, -0.28), (0.1, 0.2), (0.0, 0.6)], 6, phase=0.0)
    g = paint(g, lambda cc, n: C("yellow", 15, True) if cc.y < -0.2 else C("yellow", 13, True))
    b.add(g, RX(90))
    p.preview = {"yaw": 150, "pitch": 20}
    return p


# ================================================================== BB_Reticle
def _ring(r_in, r_out, segs, y, color, dashes=None, gap=0.45):
    g = Geo()
    if dashes is None:
        for i in range(segs):
            a0, a1 = TAU_ * i / segs, TAU_ * (i + 1) / segs
            q = [(r_in * math.cos(a0), y, r_in * math.sin(a0)), (r_out * math.cos(a0), y, r_out * math.sin(a0)),
                 (r_out * math.cos(a1), y, r_out * math.sin(a1)), (r_in * math.cos(a1), y, r_in * math.sin(a1))]
            g.add(_flat_up(q, color))
        return g
    per = segs // dashes
    for d in range(dashes):
        span = TAU_ / dashes
        a_start = span * d + span * gap / 2
        a_end = span * (d + 1) - span * gap / 2
        for i in range(per):
            a0 = a_start + (a_end - a_start) * i / per
            a1 = a_start + (a_end - a_start) * (i + 1) / per
            q = [(r_in * math.cos(a0), y, r_in * math.sin(a0)), (r_out * math.cos(a0), y, r_out * math.sin(a0)),
                 (r_out * math.cos(a1), y, r_out * math.sin(a1)), (r_in * math.cos(a1), y, r_in * math.sin(a1))]
            g.add(_flat_up(q, color))
    return g


def _flat_up(pts, color):
    g = Geo()
    ids = [g.vert(p) for p in pts]
    _, n = g.center_normal(ids)
    if n.y < 0:
        ids.reverse()
    g.face(ids, color)
    return g


def _ticks(r_in, r_tip, half_w, y, color):
    g = Geo()
    for i in range(4):
        a = TAU_ * i / 4 + math.pi / 4
        ca, sa = math.cos(a), math.sin(a)
        px, pz = -sa, ca
        pts = [(r_in * ca + px * half_w, y, r_in * sa + pz * half_w), (r_in * ca - px * half_w, y, r_in * sa - pz * half_w),
               (r_tip * ca, y, r_tip * sa)]
        g.add(_flat_up(pts, color))
    return g


def reticle():
    """Rock target reticle: flat, y-up, centred; OUTER radius 1.0 m at scale 1 (scale the root by the rock radius
    0.55: the ring's outer edge is the hit footprint), 0.25 m line. Three sibling meshes so code switches state
    without material swaps: `Ring` (following: 8 orange dashes), `RingLocked` (solid magenta-red ring + 4 inward
    ticks, HDR ~2.3; blink it at 6 Hz), `Shadow` (dark disc r 0.72 just below the ring; scale it 0 -> 1 as the rock
    falls). Place the root 0.03 m above the road (no z-fighting at 360p)."""
    p = Piece("BB_Reticle", 200, pivot="centre", notes="rock reticle decal; Ring/RingLocked/Shadow siblings")
    ring = p.part("Ring")
    ring.add(_ring(0.75, 1.0, 32, 0.0, C("orange", 11, True), dashes=8, gap=0.38))
    lk = p.part("RingLocked")
    lk.add(_ring(0.75, 1.0, 24, 0.0, C("red", 11, True)))
    lk.add(_ticks(0.74, 0.46, 0.1, 0.0, C("red", 11, True)))
    sh = p.part("Shadow")
    disc = Geo()
    for i in range(16):
        a0, a1 = TAU_ * i / 16, TAU_ * (i + 1) / 16
        disc.add(_flat_up([(0.0, -0.01, 0.0), (0.72 * math.cos(a0), -0.01, 0.72 * math.sin(a0)),
                           (0.72 * math.cos(a1), -0.01, 0.72 * math.sin(a1))], C("gray", 0)))
    sh.add(disc)
    p.preview = {"yaw": 0, "pitch": 55}
    return p


# ================================================================== BB_CrackGlow
CRACK_MAIN = [(-0.98, 0.06), (-0.7, -0.08), (-0.52, 0.1), (-0.28, -0.02), (-0.08, 0.16), (0.12, -0.04),
              (0.36, 0.1), (0.58, -0.1), (0.8, 0.04), (0.98, -0.05)]
CRACK_BRANCHES = [
    [(-0.52, 0.1), (-0.6, 0.34), (-0.5, 0.52), (-0.58, 0.68)],
    [(-0.08, 0.16), (0.02, 0.36), (-0.06, 0.55), (0.04, 0.7)],     # runs on toward the car: rows chain into a line
    [(0.12, -0.04), (0.06, -0.3), (0.16, -0.5), (0.1, -0.7)],
    [(0.58, -0.1), (0.7, -0.32), (0.64, -0.5)],
    [(-0.7, -0.08), (-0.82, -0.3), (-0.74, -0.46)],
]


def crack_glow():
    """Ground-slam crack decal for one 2 m lane cell (2.0 x 1.4 m footprint, flat, y-up, centred; place 0.02 m above
    the road). Three stacked strips per crack: scorched dark rim, red-orange glow (HDR ~2.1), yellow-white core.
    Fade it in code by scaling / toggling; the bricks grow out of it."""
    p = Piece("BB_CrackGlow", 300, pivot="centre", notes="crack glow decal for one lane cell")
    cg = p.part("Crack")
    lines = [(CRACK_MAIN, 1.0)] + [(b, 0.7) for b in CRACK_BRANCHES]
    for pts, k_ in lines:
        n = len(pts)
        taper = [k_ * (0.35 + 0.65 * math.sin(math.pi * (0.15 + 0.7 * i / (n - 1)))) for i in range(n)]
        if k_ < 1.0:
            taper = [k_ * (1.0 - 0.75 * i / (n - 1)) for i in range(n)]
        cg.add(strip(pts, [0.3 * t for t in taper], 0.0, C("gray", 1)))
        cg.add(strip(pts, [0.17 * t for t in taper], 0.004, C("orange", 10, True)))
        cg.add(strip(pts, [0.07 * t for t in taper], 0.008, C("yellow", 13, True)))
    p.preview = {"yaw": 0, "pitch": 60}
    return p


REGISTRY = {
    "BB_Brick": brick,
    "BB_Rock": rock,
    "BB_PowerCrate": power_crate,
    "BB_Rocket": rocket,
    "BB_SideWall": side_wall,
    "BB_SpikeWall": spike_wall,
    "BB_WindGust": wind_gust,
    "BB_LaserBolt": laser_bolt,
    "BB_Reticle": reticle,
    "BB_CrackGlow": crack_glow,
}
