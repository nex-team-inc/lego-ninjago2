"""Unity-space geometry kit for the Boss Battle world props + arena (package M, part 2; Blender 5.2 headless).

Every piece in this folder is authored in UNITY space (x right, y up, z forward = away from the game camera,
1 unit = 1 m), like env_lib, but with ekit's pure-Python `Geo` container (vertex list + one palette colour spec per
face) so the vertex / face order is deterministic and colours are chosen per face in Unity terms (n.y = up).
One conversion happens at the very end (`Piece.to_model`): Unity point (x, y, z) -> Blender (-x, -z, y), the same
mapping env_lib.u2b uses; bl_common's FBX export maps it back, so a vertex authored at Unity (1, 2, 3) imports at
(1, 2, 3) in the Unity FBX root. Both conversions are mirror maps (det -1): `Geo.add` reverses the winding, so the
outward (right-hand rule) normals authored here stay outward in Blender and in Unity.

ekit primitives are built around +Z (z up); `yup()` swaps y/z (another mirror, handled the same way) so a lathe,
cone, box or prism made by ekit stands on Unity +Y. Rotations use mathutils matrices directly on Unity coordinates:
RY(+a) turns +Z toward +X and RX(+a) turns +Y toward +Z, exactly like Unity's Euler angles per axis.

Colour spec = ekit.C(family, shade 0..15, emissive). Emissive faces point into the palette's right half
(Palette_Emission.png), so M_BB_Palette adds `colour x _EmissionColor (1.6)` on top of the lit albedo.
HDR budget (GDD 12): ambience <= 1.3 -> emissive shades with max channel <= ~0.5 (e.g. orange 6-7, red 8-9, yellow 6);
hazards / tells >= 2.0 -> max channel >= ~0.8 (e.g. orange 12+, red 13+, cyan 12+, yellow 13+).
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bbw_paths as P  # noqa: E402

sys.path.insert(0, P.ENEMIES_KIT)
sys.path.insert(0, P.TOOLS_BLENDER)
import ekit as k  # noqa: E402
from ekit import C, Geo  # noqa: E402,F401
from mathutils import Matrix, Vector  # noqa: E402

TAU = math.tau

# ekit z-up primitive -> Unity y-up: (x, y, z) -> (x, z, y)
SWAP_YZ = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
# Unity point -> Blender point (env_lib.u2b): (x, y, z) -> (-x, -z, y)
U2B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


# ------------------------------------------------------------------ transforms (Unity space)
def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def RX(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def RY(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def RZ(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1.0))


def M(pos=(0.0, 0.0, 0.0), yaw=0.0, pitch=0.0, roll=0.0, scale=1.0):
    """Unity TRS: Quaternion.Euler(pitch, yaw, roll) applies z, then x, then y."""
    s = scale if isinstance(scale, (tuple, list)) else (scale, scale, scale)
    return T(*pos) @ RY(yaw) @ RX(pitch) @ RZ(roll) @ S(*s)


def aim(origin, direction, up_hint=(0.0, 1.0, 0.0)):
    """Matrix mapping local +Y onto `direction` (for y-up primitives: spikes, cones, flames)."""
    y = Vector(direction).normalized()
    up = Vector(up_hint)
    if abs(y.dot(up.normalized())) > 0.98:
        up = Vector((0.0, 0.0, 1.0)) if abs(y.z) < 0.9 else Vector((1.0, 0.0, 0.0))
    z = (up - y * up.dot(y)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


# ------------------------------------------------------------------ deterministic noise
def hash01(*vals):
    """Same formula as env_lib.hash01 (stable across runs)."""
    s = 0.0
    for i, v in enumerate(vals):
        s += v * (12.9898 + 31.7 * i) + (i + 1) * 4.1414
    x = math.sin(s) * 43758.5453
    return x - math.floor(x)


def rng(seed):
    return random.Random(seed)


# ------------------------------------------------------------------ primitives (Unity space, constant colour)
PLACEHOLDER = C("gray", 8)


def yup(g):
    """ekit z-up geometry -> Unity y-up."""
    return Geo().add(g, SWAP_YZ)


def lathe(profile, segs, color=PLACEHOLDER, phase=0.0):
    """Surface of revolution around Unity +Y; profile [(r, y), ...] bottom -> top (see ekit.lathe)."""
    return yup(k.lathe(profile, segs, color, phase))


def cyl(r, h, segs, color=PLACEHOLDER, r_top=None, phase=0.0):
    return yup(k.cyl(r, h, segs, color, phase=phase, r_top=r_top))


def cone(r, h, segs, color=PLACEHOLDER, phase=0.0):
    return yup(k.cone(r, h, segs, color, phase=phase))


def ico(radius, color=PLACEHOLDER, subdiv=1, jitter=0.0, seed=0):
    g = k.ico(radius, color, subdiv)
    if jitter:
        k.jitter(g, jitter * radius, seed)
    return g


def box(sx, sy, sz, color=PLACEHOLDER, y0=0.0, top_scale=(1.0, 1.0), top_shift=(0.0, 0.0), bottom=True):
    """Box standing on y = y0, centred on x/z. top_scale/top_shift taper/shear the top face (x, z)."""
    corners = []
    for iz in (0, 1):
        for iy in (0, 1):
            for ix in (0, 1):
                kx, kz = top_scale if iy else (1.0, 1.0)
                dx, dz = top_shift if iy else (0.0, 0.0)
                corners.append(((ix - 0.5) * sx * kx + dx, y0 + iy * sy, (iz - 0.5) * sz * kz + dz))
    g = k.hexa(corners, color)
    if not bottom:
        drop(g, lambda c, n: n.y < -0.99)
    return g


def cbox(sx, sy, sz, color=PLACEHOLDER, **kw):
    """Box centred on the origin."""
    return box(sx, sy, sz, color, y0=-sy / 2, **kw)


def rect_ring(hx, hz, y):
    """CCW (seen from +Y) rectangle ring for loft()."""
    return [(-hx, y, -hz), (-hx, y, hz), (hx, y, hz), (hx, y, -hz)]


def loft(rings, color=PLACEHOLDER, cap_bottom=True, cap_top=True):
    """Loft through rings of equal length (lists of Unity points), bottom -> top. Faces are oriented away from the
    loft axis (centroid of their two rings), caps away from the neighbouring ring: right for convex lofts."""
    g = Geo()
    ids = [[g.vert(p) for p in ring] for ring in rings]
    cents = [sum((Vector(p) for p in ring), Vector()) / len(ring) for ring in rings]
    faces = []
    for a, b, ca, cb in zip(ids, ids[1:], cents, cents[1:]):
        n = len(a)
        mid = (ca + cb) / 2
        for i in range(n):
            j = (i + 1) % n
            faces.append(([a[i], a[j], b[j], b[i]], mid))
    if cap_bottom:
        faces.append((list(ids[0]), cents[0] + (cents[0] - cents[1]) * -10.0))
    if cap_top:
        faces.append((list(ids[-1]), cents[-1] + (cents[-1] - cents[-2]) * -10.0))
    for idx, ref in faces:
        cen, nrm = g.center_normal(idx)
        if nrm.dot(cen - ref) < 0:
            idx = list(reversed(idx))
        g.face(idx, color)
    return g


def chamfer_box(sx, sy, sz, c, color=PLACEHOLDER, y0=0.0, bottom=False, vertical=True):
    """Box standing on y0 with a chamfer `c` on the top edges (and on the 4 vertical edges when vertical=True):
    the bevel faces catch the light so plastic bricks read as separate blocks at 360p. No bottom face by default."""
    hx, hz = sx / 2, sz / 2
    if vertical:
        def oct_ring(ix, iz, y):
            return [(-ix + c, y, -iz), (-ix, y, -iz + c), (-ix, y, iz - c), (-ix + c, y, iz),
                    (ix - c, y, iz), (ix, y, iz - c), (ix, y, -iz + c), (ix - c, y, -iz)]
        # top ring = the octagon inset by c (every edge moved inward by c, diagonals included)
        d = c * math.sqrt(2.0)
        y = y0 + sy
        top = [(-hx + d, y, -hz + c), (-hx + c, y, -hz + d), (-hx + c, y, hz - d), (-hx + d, y, hz - c),
               (hx - d, y, hz - c), (hx - c, y, hz - d), (hx - c, y, -hz + d), (hx - d, y, -hz + c)]
        rings = [oct_ring(hx, hz, y0), oct_ring(hx, hz, y0 + sy - c), top]
    else:
        rings = [rect_ring(hx, hz, y0), rect_ring(hx, hz, y0 + sy - c), rect_ring(hx - c, hz - c, y0 + sy)]
    return loft(rings, color, cap_bottom=bottom, cap_top=True)


def stud(r, h, segs=6, color=PLACEHOLDER, phase=None):
    """LEGO stud: open-bottom cylinder with a flat top (segs side quads + one top n-gon)."""
    phase = math.pi / segs if phase is None else phase
    g = Geo()
    lo = [g.vert((r * math.cos(phase + TAU * i / segs), 0.0, r * math.sin(phase + TAU * i / segs))) for i in range(segs)]
    hi = [g.vert((r * math.cos(phase + TAU * i / segs), h, r * math.sin(phase + TAU * i / segs))) for i in range(segs)]
    for i in range(segs):
        j = (i + 1) % segs
        g.face([lo[i], hi[i], hi[j], lo[j]], color)
    g.face(list(reversed(hi)), color)
    return orient_out(g, Vector((0.0, h * 0.5, 0.0)))


def stud_grid(nx, nz, pitch_x, pitch_z, r, h, y, segs=6, center=(0.0, 0.0)):
    g = Geo()
    for ix in range(nx):
        for iz in range(nz):
            x = center[0] + (ix - (nx - 1) / 2) * pitch_x
            z = center[1] + (iz - (nz - 1) / 2) * pitch_z
            g.add(stud(r, h, segs), T(x, y, z))
    return g


def prism_y(poly_xz, y0, y1, color=PLACEHOLDER, top_scale=1.0):
    """Extrude a 2D outline [(x, z), ...] (any winding) from y0 to y1."""
    poly = _ccw(poly_xz)
    return yup(k.prism(poly, y0, y1, color, top_scale=top_scale))


def prism_z(poly_xy, z0, z1, color=PLACEHOLDER):
    """Extrude a 2D outline [(x, y), ...] (any winding) from z0 to z1 (front silhouettes: arches, ridges)."""
    return k.prism(_ccw(poly_xy), z0, z1, color)


def _ccw(poly):
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    return list(poly) if area > 0 else list(reversed(poly))


def flat(points, color=PLACEHOLDER, normal=(0.0, 1.0, 0.0)):
    """One flat polygon (convex) facing `normal`."""
    g = Geo()
    ids = [g.vert(p) for p in points]
    _, n = g.center_normal(ids)
    if n.dot(Vector(normal)) < 0:
        ids.reverse()
    g.face(ids, color)
    return g


def flat_poly(points_xz, y=0.0, color=PLACEHOLDER):
    """A possibly concave polygon in the XZ plane facing +Y, triangulated deterministically."""
    poly = _ccw(points_xz)
    g = Geo()
    ids = [g.vert((x, y, z)) for x, z in poly]
    for a, b, c in k._tessellate(poly):
        tri = [ids[a], ids[b], ids[c]]
        _, n = g.center_normal(tri)
        if n.y < 0:
            tri.reverse()
        g.face(tri, color)
    return g


def strip(points_xz, widths, y=0.0, color=PLACEHOLDER):
    """Flat ribbon along a polyline in the XZ plane (mitred joints), facing +Y. widths: scalar or per point."""
    pts = [Vector((x, 0.0, z)) for x, z in points_xz]
    ws = list(widths) if hasattr(widths, "__len__") else [widths] * len(pts)
    g = Geo()
    left, right = [], []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        t = (b - a).normalized()
        side = Vector((-t.z, 0.0, t.x))
        w = ws[i] / 2
        left.append(g.vert((p.x + side.x * w, y, p.z + side.z * w)))
        right.append(g.vert((p.x - side.x * w, y, p.z - side.z * w)))
    for i in range(len(pts) - 1):
        q = [left[i], left[i + 1], right[i + 1], right[i]]
        if ws[i] <= 1e-6:
            q = [left[i], left[i + 1], right[i + 1]]
        elif ws[i + 1] <= 1e-6:
            q = [left[i], left[i + 1], right[i]]
        _, n = g.center_normal(q)
        if n.y < 0:
            q.reverse()
        g.face(q, color)
    return g


def sweep(points, radii, segs, color=PLACEHOLDER, phase=0.0, up_hint=(0.0, 1.0, 0.0)):
    return k.sweep(points, radii, segs, color, phase=phase, up_hint=up_hint)


def orient_out(g, inside):
    """Flip faces whose normal points toward `inside` (a point inside a convex-ish primitive)."""
    p = Vector(inside)
    for fc in g.f:
        c, n = g.center_normal(fc[0])
        if n.dot(c - p) < 0:
            fc[0] = list(reversed(fc[0]))
    return g


def drop(g, pred):
    """Delete faces for which pred(centre, normal) is true (hidden caps). Unused vertices are pruned at build."""
    g.f = [fc for fc in g.f if not pred(*g.center_normal(fc[0]))]
    return g


def jitter_xz(g, amount, seed, keep_y_below=None):
    """Deterministic horizontal-only vertex noise (keeps flat tops / bottoms flat)."""
    r = rng(seed)
    offsets = {}
    for i, co in enumerate(g.v):
        key = (round(co.x, 4), round(co.y, 4), round(co.z, 4))
        if key not in offsets:
            offsets[key] = Vector((r.uniform(-1, 1), 0.0, r.uniform(-1, 1))) * amount
        if keep_y_below is not None and co.y <= keep_y_below:
            continue
        g.v[i] = co + offsets[key]
    return g


def jitter(g, amount, seed):
    return k.jitter(g, amount, seed)


# ------------------------------------------------------------------ colour rules (Unity space: n.y = up)
def paint(g, rule, where=None):
    """Recolour faces: rule = colour spec or callable(centre, normal) -> spec."""
    return g.paint(rule, where)


def facing(fam, top, side, bottom=None, em=False, up=0.55, down=-0.4):
    bottom = side - 2 if bottom is None else bottom

    def rule(c, n):
        return C(fam, top if n.y > up else bottom if n.y < down else side, em)
    return rule


def by_y(bands, em=False):
    """bands = [(y_max, family, shade), ...] ascending by face-centre height; last entry catches the rest."""
    def rule(c, n):
        for y_max, fam, s in bands:
            if c.y <= y_max:
                return C(fam, s, em)
        return C(bands[-1][1], bands[-1][2], em)
    return rule


def plastic(fam, base, em=False, hi=2, lo=-2, rim=1):
    """Glossy toy-brick shading baked into the palette: tops +hi, bottoms lo, chamfers (diagonal normals) +rim
    above the top shade (the 'glint' line), sides base."""
    def rule(c, n):
        if n.y > 0.95:
            return C(fam, base + hi, em)
        if n.y > 0.3:
            return C(fam, base + hi + rim, em)
        if n.y < -0.5:
            return C(fam, base + lo, em)
        if abs(n.x) > 0.3 and abs(n.z) > 0.3:   # vertical chamfers
            return C(fam, base + 1, em)
        return C(fam, base, em)
    return rule


def stone(fam, base, seed=0.0, alt=None, top=1, bottom=-2, jit=1, patch=1.5):
    """Chunky stone: top +1, bottom -2, coherent patches of `alt` (family, shade, share), per-face jitter +-jit."""
    def rule(c, n):
        r = hash01(c.x, c.y, c.z, seed)
        r2 = hash01(math.floor(c.x * patch), math.floor(c.y * patch), math.floor(c.z * patch), seed + 7.0)
        fm, s = fam, base
        if alt is not None and r2 < alt[2]:
            fm, s = alt[0], alt[1]
        if n.y > 0.65:
            s += top
        elif n.y < -0.45:
            s += bottom
        if jit:
            s += -jit if r < 0.22 else (jit if r > 0.82 else 0)
        return C(fm, s)
    return rule


# ------------------------------------------------------------------ pieces
class UPart:
    def __init__(self, name, pivot, parent):
        self.name = name
        self.pivot = Vector(pivot)
        self.parent = parent
        self.geo = Geo()

    def add(self, geo, m=None, color=None):
        self.geo.add(geo, m, color)
        return self


class Piece:
    """A model authored in Unity space. kind 'prop' | 'arena'. pivot: 'centre' | 'bottom' | 'top' | 'free'
    (checked by bbw_build.check_piece). Parts: (name, Unity pivot, parent name)."""

    def __init__(self, name, budget, pivot="centre", notes="", shadows=False):
        self.name = name
        self.budget = budget
        self.pivot = pivot
        self.notes = notes
        self.shadows = shadows
        self.parts = []
        self.pivot_parts = None   # parts the pivot check measures (default: all; e.g. the rocket body w/o exhaust)
        self.preview = {}   # optional review-render hints: yaw, pitch, dist, target

    def part(self, name, pivot=(0.0, 0.0, 0.0), parent=None):
        p = UPart(name, pivot, parent)
        self.parts.append(p)
        return p

    def get(self, name):
        return next(p for p in self.parts if p.name == name)

    def tris(self):
        return sum(p.geo.tris() for p in self.parts)

    def bounds(self, names=None):
        pts = [v for p in self.parts if names is None or p.name in names for v in p.geo.v]
        lo = Vector((min(v.x for v in pts), min(v.y for v in pts), min(v.z for v in pts)))
        hi = Vector((max(v.x for v in pts), max(v.y for v in pts), max(v.z for v in pts)))
        return lo, hi

    def to_model(self):
        """ekit.Model in Blender space (geometry + pivots mirrored by U2B)."""
        m = k.Model(self.name, self.budget)
        for p in self.parts:
            bp = m.part(p.name, U2B @ p.pivot, p.parent)
            bp.add(p.geo, U2B)
        return m

    def stats(self):
        lo, hi = self.bounds()
        out = []
        for p in self.parts:
            plo, phi = p.geo.bounds()
            out.append({"name": p.name, "parent": p.parent, "tris": p.geo.tris(),
                        "pivot_unity": [round(c, 4) for c in p.pivot],
                        "bounds_min_unity": [round(c, 3) for c in plo], "bounds_max_unity": [round(c, 3) for c in phi],
                        "emissive_faces": sum(1 for _, c in p.geo.f if c[2]),
                        "faces": len(p.geo.f)})
        return {"name": self.name, "tris": self.tris(), "budget": self.budget, "pivot": self.pivot,
                "bounds_min_unity": [round(c, 3) for c in lo], "bounds_max_unity": [round(c, 3) for c in hi],
                "size_unity": [round(h - l, 3) for l, h in zip(lo, hi)], "shadows": self.shadows,
                "notes": self.notes, "preview": self.preview, "parts": out}
