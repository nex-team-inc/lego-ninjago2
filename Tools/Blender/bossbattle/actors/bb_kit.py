"""Boss Battle actor kit: Ninjago brick helpers (studs, bricks, bevelled plates, spikes), a per-face ambient-occlusion
bake into palette shades, facing flips and empty (transform-only) parts - on top of Tools/Blender/enemies/ekit.py.

Runs inside Blender 5.2 (bpy/bmesh/mathutils only). Authoring space = ekit's Blender space:
  * front = Blender -Y (Unity +Z), up = +Z, the character's right hand = Blender -X (Unity +X),
  * models that must face Unity -Z (boss, minion) are authored like that and turned 180 deg about Z at the end
    (`BBModel.face_minus_z()`), which keeps L/R part names character-relative: ArmL ends up at Unity +X.
  * 1 unit = 1 m. Every part's origin is its animation pivot, parts carry location only (rotation/scale baked).
Colour spec = ekit.C(family, shade 0..15, emissive); emissive faces map to the palette's right half.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bb_paths  # noqa: E402

sys.path.insert(0, bb_paths.ENEMIES_KIT)
sys.path.insert(0, bb_paths.TOOLS_BLENDER)
import bl_common as bc  # noqa: E402,F401
import ekit as k  # noqa: E402
from ekit import C, R, S, T, Geo  # noqa: E402,F401

X = Vector((1.0, 0.0, 0.0))
Y = Vector((0.0, 1.0, 0.0))
Z = Vector((0.0, 0.0, 1.0))
TAU = math.tau


# ------------------------------------------------------------------ colour rules
def lit(family, base, top=3, bottom=-3, front=0, emissive=False, up=0.55, down=-0.4):
    """Top-lit flat shading baked into the palette: faces pointing up get `base + top`, down `base + bottom`,
    the rest `base` (+ `front` on faces turned toward the front, -Y)."""
    def rule(c, n):
        if n.z > up:
            s = base + top
        elif n.z < down:
            s = base + bottom
        else:
            s = base + (front if n.y < -0.5 else 0)
        return C(family, s, emissive)
    return rule


def by_height(rule_lo, rule_hi, z_split):
    def rule(c, n):
        return k.resolve(rule_hi if c.z >= z_split else rule_lo, c, n)
    return rule


def emissive(family, shade):
    return C(family, shade, True)


# ------------------------------------------------------------------ polygons
def rect(sx, sy, cx=0.0, cy=0.0):
    return [(cx - sx / 2, cy - sy / 2), (cx + sx / 2, cy - sy / 2), (cx + sx / 2, cy + sy / 2), (cx - sx / 2, cy + sy / 2)]


def chamfer_rect(sx, sy, ch, cx=0.0, cy=0.0):
    """Octagon: a rectangle with its 4 corners cut by `ch` (CCW)."""
    hx, hy = sx / 2, sy / 2
    pts = [(-hx + ch, -hy), (hx - ch, -hy), (hx, -hy + ch), (hx, hy - ch), (hx - ch, hy), (-hx + ch, hy), (-hx, hy - ch),
           (-hx, -hy + ch)]
    return [(x + cx, y + cy) for x, y in pts]


def offset_convex(poly, d):
    """Inset a convex CCW polygon by distance d (edges moved inward, adjacent edges intersected)."""
    n = len(poly)
    lines = []
    for i in range(n):
        ax, ay = poly[i]
        bx, by = poly[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        ln = math.hypot(ex, ey)
        nx, ny = -ey / ln, ex / ln  # inward normal of a CCW polygon
        lines.append(((ax + nx * d, ay + ny * d), (ex, ey)))
    out = []
    for i in range(n):
        (p1, d1), (p2, d2) = lines[i - 1], lines[i]
        den = d1[0] * d2[1] - d1[1] * d2[0]
        if abs(den) < 1e-12:
            out.append(p2)
            continue
        t = ((p2[0] - p1[0]) * d2[1] - (p2[1] - p1[1]) * d2[0]) / den
        out.append((p1[0] + d1[0] * t, p1[1] + d1[1] * t))
    return out


# ------------------------------------------------------------------ primitives
def bevel_prism(poly, z0, z1, color, bevel_top=0.0, bevel_bottom=0.0, top_scale=1.0, top_shift=(0.0, 0.0),
                cap_color=None, bevel_color=None):
    """Extruded convex CCW polygon with a chamfered top (and/or bottom) edge: the chamfer band catches its own cel
    band, which is what makes a low-poly plate read 'chunky' at 360p. top_scale/top_shift taper/shear the top."""
    g = Geo()
    cx = sum(p[0] for p in poly) / len(poly)
    cy = sum(p[1] for p in poly) / len(poly)

    def ring(pts, z, scale=1.0, shift=(0.0, 0.0)):
        return [g.vert(((x - cx) * scale + cx + shift[0], (y - cy) * scale + cy + shift[1], z)) for x, y in pts]

    rings = []
    if bevel_bottom > 0:
        rings.append(ring(offset_convex(poly, bevel_bottom), z0))
        rings.append(ring(poly, z0 + bevel_bottom))
    else:
        rings.append(ring(poly, z0))
    if bevel_top > 0:
        rings.append(ring(poly, z1 - bevel_top, top_scale, top_shift))
        rings.append(ring(offset_convex(poly, bevel_top), z1, top_scale, top_shift))
    else:
        rings.append(ring(poly, z1, top_scale, top_shift))
    n = len(poly)
    cap = color if cap_color is None else cap_color
    bev = color if bevel_color is None else bevel_color
    g.face(list(reversed(rings[0])), cap)
    g.face(list(rings[-1]), cap)
    for ri in range(len(rings) - 1):
        lo, hi = rings[ri], rings[ri + 1]
        is_bevel = (bevel_bottom > 0 and ri == 0) or (bevel_top > 0 and ri == len(rings) - 2)
        for i in range(n):
            i1 = (i + 1) % n
            g.face([lo[i], lo[i1], hi[i1], hi[i]], bev if is_bevel else color)
    if any(callable(c) for c in (color, cap, bev)):
        for fc in g.f:
            c, nrm = g.center_normal(fc[0])
            fc[1] = k.resolve(fc[1], c, nrm)
    return g


def bevel_box(sx, sy, sz, color, bevel=0.0, bevel_bottom=0.0, base_z=0.0, top_scale=1.0, top_shift=(0.0, 0.0),
              chamfer=0.0, cap_color=None, bevel_color=None):
    poly = chamfer_rect(sx, sy, chamfer) if chamfer > 0 else rect(sx, sy)
    return bevel_prism(poly, base_z, base_z + sz, color, bevel, bevel_bottom, top_scale, top_shift, cap_color,
                       bevel_color)


def stud(r, h, color, segs=6, phase=None, cap_color=None):
    """LEGO stud standing on z = 0: open bottom (it always sits on a surface), flat top cap.
    6 segments = 16 tris. Top cap gets `cap_color` (default: the side colour)."""
    phase = math.pi / segs if phase is None else phase
    g = Geo()
    lo = [g.vert((r * math.cos(phase + TAU * j / segs), r * math.sin(phase + TAU * j / segs), 0.0)) for j in range(segs)]
    hi = [g.vert((r * math.cos(phase + TAU * j / segs), r * math.sin(phase + TAU * j / segs), h)) for j in range(segs)]
    for j in range(segs):
        j1 = (j + 1) % segs
        g.face([lo[j], lo[j1], hi[j1], hi[j]], color)
    g.face(list(hi), cap_color if cap_color is not None else color)
    if callable(color) or callable(cap_color):
        for fc in g.f:
            c, nrm = g.center_normal(fc[0])
            fc[1] = k.resolve(fc[1], c, nrm)
    return g


def stud_grid(nx, ny, pitch, r, h, color, segs=6, cap_color=None):
    """nx x ny studs centred on the origin, standing on z = 0."""
    g = Geo()
    for ix in range(nx):
        for iy in range(ny):
            g.add(stud(r, h, color, segs, cap_color=cap_color), T((ix - (nx - 1) / 2) * pitch, (iy - (ny - 1) / 2) * pitch, 0))
    return g


def brick(nx, ny, pitch, height, color, stud_color=None, bevel=0.0, studs=True, stud_segs=6):
    """Toy brick of nx x ny studs (block nx*pitch x ny*pitch x height standing on z = 0) with studs on top
    (diameter 0.6 pitch, height 0.25 pitch - a touch taller than LEGO so they read at 360p)."""
    g = bevel_box(nx * pitch, ny * pitch, height, color, bevel=bevel)
    if studs:
        sc = color if stud_color is None else stud_color
        g.add(stud_grid(nx, ny, pitch, 0.3 * pitch, 0.25 * pitch, sc, stud_segs), T(0, 0, height))
    return g


def spike(r, h, color, sides=4, phase=math.pi / 4):
    """Pyramid spike along +Z standing on z = 0 (closed base)."""
    return k.cone(r, h, sides, color, phase=phase)


def place(piece, origin, normal, up_hint=Z, sink=0.0):
    """Put a piece built along +Z (standing on z = 0) onto a surface point with outward `normal`."""
    nrm = Vector(normal).normalized()
    hint = Vector(up_hint)
    if abs(hint.normalized().dot(nrm)) > 0.98:
        hint = Vector((0.0, -1.0, 0.0)) if abs(nrm.y) < 0.9 else X
    return piece.copy(k.frame(Vector(origin) - nrm * sink, nrm, hint))


def mirror_x(geo):
    return geo.copy(S(-1, 1, 1))


def sym(geo_fn):
    """Geo for side=+1 plus its X mirror (side=-1); geo_fn(side) -> Geo."""
    g = Geo()
    for side in (-1, 1):
        g.add(geo_fn(side))
    return g


# ------------------------------------------------------------------ model with empties + anchors
class BBModel(k.Model):
    """ekit.Model plus: transform-only parts (no geometry -> FBX Null node), named anchor points reported in Unity
    space (gameplay attach points for the wrapper prefabs), the 180 deg facing flip and the AO bake."""

    def __init__(self, name, budget, facing="+Z", pivot="feet"):
        super().__init__(name, budget, footprint=(1, 1))
        self.facing = facing        # "+Z" (vehicles) or "-Z" (boss, minion) in Unity
        self.pivot_kind = pivot     # "feet" | "bottom-centre" | "centre"
        self.anchors = {}           # name -> (part name, Vector model-space point)
        self.notes = {}             # free-form data copied into the manifest (fold angles, tell parts...)
        self.shadow_casters = []

    def anchor(self, name, part, point):
        self.anchors[name] = (part, Vector(point))

    def transform_all(self, m):
        for p in self.parts:
            p.geo = p.geo.copy(m)
            p.pivot = m @ p.pivot
        for name, (part, pt) in list(self.anchors.items()):
            self.anchors[name] = (part, m @ pt)
        return self

    def face_minus_z(self):
        """Turn the authored model (front = Blender -Y = Unity +Z) 180 deg about the vertical axis so it faces
        Unity -Z. Character-relative L/R names stay correct (the left arm moves to Unity +X)."""
        self.facing = "-Z"
        return self.transform_all(R(Z, 180.0))

    def build(self, material):
        objs = {}
        empties = [p for p in self.parts if not p.geo.f]
        meshes = super_build_meshes(self, material, skip={p.name for p in empties})
        for p in self.parts:
            if p.name in meshes:
                objs[p.name] = meshes[p.name]
                continue
            obj = bpy.data.objects.new(p.name, None)
            obj.empty_display_size = 0.25
            bpy.context.scene.collection.objects.link(obj)
            objs[p.name] = obj
        for p in self.parts:  # parent + location (pivot relative to the parent pivot)
            obj = objs[p.name]
            if p.parent:
                obj.parent = objs[p.parent]
                obj.location = p.pivot - self.get(p.parent).pivot
            else:
                obj.location = p.pivot
        bpy.context.view_layer.update()
        return objs

    def unity(self, v):
        return [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]

    def stats(self):
        out = []
        for p in self.parts:
            path = [p.name]
            q = p
            while q.parent:
                q = self.get(q.parent)
                path.insert(0, q.name)
            row = {"name": p.name, "path": "/".join(path), "parent": p.parent, "tris": p.geo.tris(),
                   "pivot_blender": [round(c, 4) for c in p.pivot], "pivot_unity": self.unity(p.pivot),
                   "local_unity": self.unity(p.pivot - (self.get(p.parent).pivot if p.parent else Vector())),
                   "emissive_faces": sum(1 for _, c in p.geo.f if c[2]), "empty": not p.geo.f}
            if p.geo.f:
                lo, hi = p.geo.bounds()
                row["bounds_unity"] = {"min": [round(-hi.x, 3), round(lo.z, 3), round(-hi.y, 3)],
                                       "max": [round(-lo.x, 3), round(hi.z, 3), round(-lo.y, 3)]}
            out.append(row)
        return out

    def anchor_stats(self):
        out = {}
        for name, (part, pt) in sorted(self.anchors.items()):
            piv = self.get(part).pivot
            out[name] = {"part": part, "model_unity": self.unity(pt), "local_unity": self.unity(pt - piv)}
        return out


def super_build_meshes(model, material, skip):
    """ekit.Model.build for the mesh parts only (identical geometry path: pivot subtraction, palette UVs,
    warped-quad triangulation, canonical vertex/face order). Parenting is done by BBModel.build."""
    import bmesh
    objs = {}
    for p in model.parts:
        if p.name in skip:
            continue
        bm = bmesh.new()
        bm.loops.layers.uv.verify()
        verts = [bm.verts.new(co - p.pivot) for co in p.geo.v]
        by_color = {}
        for idx, col in p.geo.f:
            if len(set(idx)) < 3:
                continue
            f = bm.faces.new([verts[i] for i in idx])
            by_color.setdefault(col, []).append(f)
        for (fam, shade, emis), faces in sorted(by_color.items()):
            bc.paint_faces(bm, faces, fam, shade, emissive=emis)
        for v in [v for v in bm.verts if not v.link_faces]:
            bm.verts.remove(v)
        bm.normal_update()
        k._triangulate_nonplanar(bm)
        objs[p.name] = bc.mesh_object(p.name, bm, material, smooth=False)
    return objs


# ------------------------------------------------------------------ AO bake into palette shades
def _fibonacci_dirs(n):
    out = []
    ga = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(n):
        z = 1.0 - 2.0 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a = ga * i
        out.append(Vector((r * math.cos(a), r * math.sin(a), z)))
    return out


def bake_ao(model, distance, strength=3.0, rays=96, ground=True, gamma=0.8, min_shade=0, skip_parts=(),
            max_drop=3):
    """Ambient occlusion per face, baked as darker palette shades (recesses, armpits, under plates, between
    studs, toward the ground). Deterministic: fixed Fibonacci ray set, cosine weighted, one BVH of the whole
    model (+ an optional ground plane at z = 0). Emissive faces are left alone (they glow)."""
    verts, polys = [], []
    for p in model.parts:
        base = len(verts)
        verts.extend(tuple(v) for v in p.geo.v)
        polys.extend([i + base for i in idx] for idx, _ in p.geo.f if len(set(idx)) >= 3)
    if ground:
        base = len(verts)
        verts.extend([(-500, -500, -0.001), (500, -500, -0.001), (500, 500, -0.001), (-500, 500, -0.001)])
        polys.append([base, base + 1, base + 2, base + 3])
    bvh = BVHTree.FromPolygons(verts, polys)
    dirs = _fibonacci_dirs(rays)
    eps = distance * 0.004
    changed = 0
    for p in model.parts:
        if p.name in skip_parts:
            continue
        for fc in p.geo.f:
            fam, shade, emis = fc[1]
            if emis or len(set(fc[0])) < 3:
                continue
            c, n = p.geo.center_normal(fc[0])
            wsum = hit = 0.0
            origin = c + n * eps
            for d in dirs:
                w = d.dot(n)
                if w <= 0.05:
                    continue
                wsum += w
                loc, _, _, _ = bvh.ray_cast(origin, d, distance)
                if loc is not None:
                    hit += w
            occ = (hit / wsum) if wsum > 0 else 0.0
            drop = min(max_drop, int(round(strength * occ ** gamma)))
            if drop:
                fc[1] = (fam, max(min_shade, shade - drop), emis)
                changed += 1
    return changed
