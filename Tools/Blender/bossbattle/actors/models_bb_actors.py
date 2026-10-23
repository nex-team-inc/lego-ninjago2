"""Boss Battle actor models (package M part 1): BB_Boss (the Brick Titan), BB_Minion, BB_NinjaCar, BB_NinjaJet.

Authoring space = ekit (Blender): front = -Y, up = +Z, the character's right = -X, 1 unit = 1 m. The boss and the
minion are authored facing -Y like every ekit model and turned 180 deg at the end (BBModel.face_minus_z) so they
face Unity -Z (toward the chase camera); the vehicles keep the ekit facing (Unity +Z).
Every builder returns a bb_kit.BBModel whose part names/parents match contract_bb_actors.HIERARCHY.

Readability rules (research/art-pipeline.md section 8): boss at ~41 m from the road camera = ~9.4 px/m on the
640x360 world RT, so boss details are >= 0.2 m and the big reads are value blocks: dark purple armour, bone mask and
horns, gold trim, emissive eyes/core/wing edges/jets. The road camera is BELOW the boss's chest, so its top
surfaces are only seen in cutscenes: studs on tops make silhouette bumps, studs that must read sit on FRONT faces
(knuckles, toes, shins). The dusk sun back-lights the boss (GDD sun euler (32, 215, 0)), so the front albedos stay
mid-value and recesses are darkened by the baked AO (bb_kit.bake_ao) instead of by dark albedo everywhere.
"""
import math

from mathutils import Matrix, Vector

import bb_kit as kit
import ekit as k
from bb_kit import X, Y, Z
from contract_bb_actors import BUDGETS
from ekit import C, R, S, T, Geo

V = Vector

# ------------------------------------------------------------------ boss palette roles
ARMOR = "purple"     # plates: lit tops, mid fronts
SUIT = "indigo"      # the black under-suit (cool near-black, never pure grey)
BONE = "gray"        # mask, horns, fangs, spikes
GOLD = "yellow"      # trims, collar, belt, crest
GLOW_CORE = "magenta"
TELL = ("gray", 8, True)   # neutral emissive cells: the actor tints them per attack (see manifest notes)

ARMOR_RULE = kit.lit(ARMOR, 8, top=3, bottom=-4, front=1)
ARMOR_DARK = kit.lit(ARMOR, 6, top=3, bottom=-3, front=1)
ARMOR_LIGHT = kit.lit(ARMOR, 10, top=3, bottom=-4, front=0)
SUIT_RULE = kit.lit(SUIT, 3, top=3, bottom=-2, front=1)
BONE_RULE = kit.lit(BONE, 12, top=2, bottom=-3, front=0)
GOLD_RULE = kit.lit(GOLD, 11, top=2, bottom=-3, front=0)
DARK = C("gray", 1)

# boss joints (authoring space, side s = +1 is the character's LEFT)
HIP_Z = 3.6
NECK = V((0.0, -0.2, 8.3))
JAW_HINGE = V((0.0, -0.75, 8.42))


def shoulder(s):
    return V((s * 2.45, 0.2, 7.3))


def elbow(s):
    return V((s * 3.15, -0.05, 5.25))


def wrist(s):
    return V((s * 3.35, -0.45, 3.5))


# local (x, y, z) -> world (x, -z, y): a polygon drawn in the FRONT view (x right, z up) extruded toward -Y
FRONT_M = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
# local (x, y, z) -> world (z, x, y): a polygon drawn in the SIDE view from +X (y right, z up) extruded along +X
SIDE_M = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def front_plate(poly_xz, y_back, depth, rule, bevel=0.0, top_scale=1.0):
    """Plate drawn in the front view (CCW, x right / z up), from y_back toward the viewer: front face at
    y_back - depth. `bevel` chamfers the front face edges. Painted in world space after placing."""
    g = kit.bevel_prism(poly_xz, 0.0, depth, C("gray", 8), bevel_top=bevel, top_scale=top_scale)
    return g.copy(T(0, y_back, 0) @ FRONT_M).paint(rule)


def side_prism(poly_yz, x0, x1, rule, bevel=0.0):
    """Prism drawn in the side view from +X (CCW, y right / z up), spanning x0..x1 (bevel on the +X cap)."""
    g = kit.bevel_prism(poly_yz, x0, x1, C("gray", 8), bevel_top=bevel)
    return g.copy(SIDE_M).paint(rule)


def mirror_poly_x(poly):
    """Mirror a CCW polygon across x = 0 and keep it CCW."""
    return [(-x, z) for x, z in reversed(poly)]


def aimed(geo, origin, direction, rule, roll=Z):
    """Piece built along +Z (from z = 0) aimed from `origin` along `direction`, painted in world space."""
    return geo.copy(k.aim(V(origin), V(direction), V(roll))).paint(rule)


def hexagon(r, cx=0.0, cz=0.0, phase=math.pi / 6):
    return [(cx + r * math.cos(phase + math.tau * i / 6), cz + r * math.sin(phase + math.tau * i / 6)) for i in range(6)]


def ring_strip(points, inner_ratio, w):
    """For a polyline `points` (list of V) and a fan centre w: inner/outer split points."""
    return [w.lerp(p, inner_ratio) for p in points]


def two_sided(poly, rule_front, rule_back, thickness, front=V((0.0, -1.0, 0.0))):
    """A thin membrane: the polygon facing `front` plus a reversed copy pushed `thickness` behind it (each side
    is back-face culled from the other, so the pair never z-fights). poly must be convex."""
    g = Geo()
    pts = [V(p) for p in poly]
    n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
    if n.dot(front) < 0:
        pts.reverse()
        n = -n
    n.normalize()
    ids = [g.vert(p) for p in pts]
    g.face(ids, C("gray", 8))
    g.paint(rule_front)
    back = Geo()
    bids = [back.vert(p - n * thickness) for p in reversed(pts)]
    back.face(bids, C("gray", 8))
    back.paint(rule_back)
    g.add(back)
    return g


# ================================================================== BB_Boss - the Brick Titan
WING_SWEEP = 12.0   # spread pose: wing plane swept back from the shoulder line
WING_FOLD = 66.0    # extra yaw (deg) that folds the wings back along the body = the exported (phase 1) pose


def bb_boss():
    m = kit.BBModel("BB_Boss", BUDGETS["BB_Boss"], facing="-Z", pivot="feet")
    m.part("Body", (0.0, 0.0, 0.0))                       # transform-only root at the feet
    hips = m.part("Hips", (0.0, 0.15, HIP_Z), parent="Body")
    torso = m.part("Torso", (0.0, 0.2, 4.4), parent="Hips")

    _boss_legs(m)
    _boss_hips(hips)
    _boss_torso(m, torso)
    _boss_head(m)
    _boss_pads(m)
    _boss_arms(m)
    _boss_wings(m)
    _boss_jets(m)

    m.anchor("FeetPoint", "Body", (0.0, 0.0, 0.0))
    m.anchor("ChestPoint", "Torso", (0.0, -1.55, 7.05))
    m.anchor("CorePoint", "Core", (0.0, -1.95, 6.45))
    m.anchor("MouthPoint", "Head", (0.0, -1.45, 8.38))
    m.anchor("HeadTopPoint", "Head", (0.0, -0.45, 12.1))
    for s, tag in ((1, "L"), (-1, "R")):
        m.anchor("Hand" + tag, "Fist" + tag, (s * 3.4, -0.6, 2.55))
        m.anchor("JetNozzle" + tag, "Jet" + tag, _jet_axis(s)[1])
    m.shadow_casters = ["Hips", "Torso", "ArmL", "ArmR", "LegL", "LegR"]
    m.notes["tell_parts"] = {
        "FistL": "knuckle studs, finger bars and palm (rock/minion/slam/clap tells)",
        "FistR": "knuckle studs, finger bars and palm",
        "Head": "throat wall behind the teeth (devil wind / inhale tells; shows when the Jaw opens)",
    }
    kit.bake_ao(m, distance=2.2, strength=3.2, rays=96, ground=True)
    m.face_minus_z()
    return m


def _boss_legs(m):
    for name, s in (("LegL", 1), ("LegR", -1)):
        x = s * 1.38
        leg = m.part(name, (s * 1.2, 0.15, HIP_Z), parent="Hips")
        leg.add(kit.bevel_prism(kit.chamfer_rect(1.2, 1.3, 0.3, x, 0.15), 2.3, 3.75, SUIT_RULE, top_scale=1.1))
        # greave: knee to ankle, flared toward the knee, armour purple
        leg.add(kit.bevel_prism(kit.chamfer_rect(1.25, 1.35, 0.32, x, 0.1), 0.8, 2.5, ARMOR_DARK, bevel_top=0.14,
                                top_scale=1.12))
        # shin guard on the front + a column of two studs (brick turned sideways: they read from the low camera)
        guard = [(x - 0.42, 1.0), (x + 0.42, 1.0), (x + 0.52, 2.35), (x - 0.52, 2.35)]
        leg.add(front_plate(guard, -0.55, 0.26, ARMOR_LIGHT, bevel=0.07))
        for z in (1.35, 1.95):
            leg.add(kit.place(kit.stud(0.17, 0.14, kit.lit(ARMOR, 11, top=2, bottom=-3)), (x, -0.8, z), (0, -1, 0)))
        # knee cop: a gold boss with a bone spike
        leg.add(kit.place(kit.stud(0.36, 0.22, GOLD_RULE, segs=6), (x, -0.85, 2.62), (0, -1, 0.15)))
        leg.add(kit.place(kit.spike(0.16, 0.38, BONE_RULE), (x, -1.06, 2.66), (0, -1, 0.25)))
        # boot: heel block + a 2x2 toe brick with studs (the one top surface the road camera sees)
        leg.add(kit.bevel_box(1.5, 1.7, 0.9, SUIT_RULE, bevel=0.12).copy(T(x, 0.3, 0.0)))
        toe = kit.brick(2, 2, 0.7, 0.55, kit.lit(ARMOR, 8, top=3, bottom=-3),
                        stud_color=kit.lit(GOLD, 11, top=2, bottom=-3), bevel=0.06)
        leg.add(toe.copy(T(x, -0.75, 0.0)))


def _boss_hips(hips):
    hips.add(kit.bevel_prism(kit.chamfer_rect(2.8, 2.0, 0.45, 0, 0.15), 3.0, 4.3, SUIT_RULE))
    hips.add(kit.bevel_prism(kit.chamfer_rect(3.1, 2.3, 0.5, 0, 0.15), 3.95, 4.5, GOLD_RULE))
    # buckle: gold hexagon with a dark centre and a stud (the warlord's seal)
    hips.add(front_plate(hexagon(0.52, 0.0, 4.22), -0.98, 0.2, kit.lit(GOLD, 12, top=2, bottom=-3)))
    hips.add(front_plate(hexagon(0.3, 0.0, 4.22), -1.16, 0.06, C(SUIT, 2)))
    # front sash flap (dark cloth) with a gold hem
    hips.add(front_plate([(-0.48, 2.6), (0.48, 2.6), (0.62, 3.95), (-0.62, 3.95)], -0.9, 0.14,
                         kit.lit(ARMOR, 4, top=2, bottom=-2)))
    hips.add(front_plate([(-0.5, 2.45), (0.5, 2.45), (0.52, 2.7), (-0.52, 2.7)], -0.95, 0.1, GOLD_RULE))
    # kusazuri: two-lame armour skirts over the front of each thigh + one lame on each side
    for s in (-1, 1):
        for i, (z0, z1, dy) in enumerate(((3.4, 4.0, 0.0), (2.75, 3.45, -0.1))):
            poly = [(s * 0.72, z0), (s * 1.95, z0 - 0.05), (s * 1.9, z1), (s * 0.75, z1)]
            if s < 0:
                poly = mirror_poly_x([(-x, z) for x, z in poly])
            hips.add(front_plate(poly, -0.95 - dy, 0.22, ARMOR_RULE, bevel=0.06))
        hem = [(s * 0.72, 2.66), (s * 1.97, 2.61), (s * 1.97, 2.82), (s * 0.72, 2.85)]
        if s < 0:
            hem = mirror_poly_x([(-x, z) for x, z in hem])
        hips.add(front_plate(hem, -1.0, 0.18, GOLD_RULE))
        side = kit.bevel_box(0.24, 1.5, 1.05, ARMOR_DARK, bevel=0.05).copy(R(Y, -s * 14))
        hips.add(side.copy(T(s * 1.75, 0.25, 2.95)))


def _boss_torso(m, torso):
    torso.add(kit.bevel_prism(kit.chamfer_rect(2.4, 1.8, 0.45, 0, 0.25), 4.2, 5.6, SUIT_RULE))
    for z0 in (4.6, 5.05):  # armoured abs
        torso.add(front_plate([(-0.72, z0), (0.72, z0), (0.78, z0 + 0.36), (-0.78, z0 + 0.36)], -0.62, 0.16,
                              ARMOR_DARK, bevel=0.05))
    # barrel chest, V taper toward the shoulders
    torso.add(kit.bevel_prism(kit.chamfer_rect(3.9, 2.6, 0.75, 0, 0.2), 5.3, 8.0, kit.lit(ARMOR, 6, top=3, bottom=-3),
                              bevel_top=0.25, top_scale=1.12))
    for s in (-1, 1):  # shoulder balls (the arm pivots sit in their centres)
        torso.add(k.sphere(0.95, 6, 3, SUIT_RULE, phase=math.pi / 6).copy(T(*shoulder(s))).paint(SUIT_RULE))
    torso.add(k.cyl(0.78, 0.8, 6, SUIT_RULE, phase=math.pi / 6).copy(T(0, -0.1, 7.85)).paint(SUIT_RULE))
    # gold gorget around the neck
    torso.add(kit.bevel_prism(kit.chamfer_rect(2.6, 2.15, 0.6, 0, 0.0), 7.85, 8.3, GOLD_RULE, bevel_top=0.12))
    # back pack: wing roots + jet mounts
    torso.add(kit.bevel_box(2.5, 1.1, 2.4, SUIT_RULE, bevel=0.15).copy(T(0, 1.55, 5.6)))
    # dark hexagonal socket the Core sits in
    torso.add(front_plate(hexagon(0.86, 0.0, 6.45, phase=math.pi / 2), -1.05, 0.38, C("gray", 1)))

    core = m.part("Core", (0.0, -1.5, 6.45), parent="Torso")
    gem = k.lathe([(0.0, 0.0), (0.6, 0.16), (0.6, 0.34), (0.0, 0.66)], 6, C("gray", 8), phase=math.pi / 2)
    gem = gem.copy(S(1.0, 1.25, 1.0))

    def gem_rule(c, n):
        if n.y < -0.8:
            return C(GLOW_CORE, 12, True)
        return C(GLOW_CORE, 11 if n.z > 0.3 else 10 if n.z > -0.3 else 8, True)
    core.add(kit.place(gem, (0.0, -1.32, 6.45), (0, -1, 0)).paint(gem_rule))

    plate = m.part("ChestPlate", (0.0, -1.35, 7.0), parent="Torso")
    pec = [(0.75, 6.0), (1.75, 5.95), (2.05, 6.6), (1.95, 7.8), (0.6, 7.7), (0.6, 6.6)]
    for s in (-1, 1):
        poly = pec if s > 0 else mirror_poly_x(pec)
        rim = kit.offset_convex(poly, -0.2)
        plate.add(front_plate(rim, -1.12, 0.32, kit.lit(GOLD, 11, top=2, bottom=-3)))
        plate.add(front_plate(poly, -1.2, 0.36, kit.lit(ARMOR, 9, top=3, bottom=-4, front=0), bevel=0.1))
        # a stud pair on each pec (front facing) - the 'brick' read at the chest
        for z in (7.25,):
            for dx in (0.95, 1.5):
                plate.add(kit.place(kit.stud(0.17, 0.14, kit.lit(ARMOR, 11, top=2, bottom=-3)),
                                    (s * dx, -1.56, z), (0, -1, 0)))
    # gold claws gripping the core
    for ang in (90.0, 210.0, 330.0):
        a = math.radians(ang)
        base = V((0.95 * math.cos(a), -1.42, 6.45 + 0.95 * math.sin(a) * 1.15))
        tip_dir = V((-math.cos(a), -0.35, -math.sin(a)))
        plate.add(aimed(kit.spike(0.14, 0.42, C(GOLD, 11)), base, tip_dir, kit.lit(GOLD, 12, top=1, bottom=-3)))


def _boss_head(m):
    """Oni warlord head: bone mask with cheekbones and kabuki stripes, angry V brow over glowing eyes, interlocking
    fangs/tusks across a mouth slot whose throat wall carries the mouth tell cells, kabuto bowl with a gold band,
    a 2x2 stud brick on top, turned-back flaps and big bone horns."""
    head = m.part("Head", NECK, parent="Torso")
    hy = -0.45  # head centre line (y): pushed forward of the neck (hunched warlord)
    head.add(kit.bevel_prism(kit.chamfer_rect(2.3, 2.05, 0.5, 0, hy), 8.75, 10.1, SUIT_RULE))
    bowl = k.lathe([(1.46, 9.78), (1.48, 10.12), (1.18, 10.52), (0.6, 10.76), (0.0, 10.81)], 8, C(ARMOR, 6),
                   phase=math.pi / 8).copy(T(0, hy, 0))
    head.add(bowl.paint(kit.lit(ARMOR, 7, top=3, bottom=-3)))
    head.add(k.cyl(1.57, 0.18, 8, C(GOLD, 11), phase=math.pi / 8).copy(T(0, hy, 9.7)).paint(GOLD_RULE))
    cap = kit.brick(2, 2, 0.6, 0.16, kit.lit(ARMOR, 8, top=3, bottom=-3), bevel=0.0)
    head.add(cap.copy(T(0, hy, 10.66)))
    for s in (-1, 1):  # fukigaeshi: turned-back flaps framing the face
        flap = kit.bevel_box(0.22, 0.95, 0.72, C(ARMOR, 8), bevel=0.05)
        head.add(flap.copy(T(s * 1.55, hy - 0.75, 9.3) @ R(Z, s * 32)).paint(kit.lit(ARMOR, 9, top=3, bottom=-3)))
    # oni mask (bone), wide cheekbones; kabuki stripes from the outer eye corners
    mask = [(-1.0, 8.78), (-0.6, 8.58), (0.6, 8.58), (1.0, 8.78), (1.3, 9.3), (1.24, 9.86), (-1.24, 9.86),
            (-1.3, 9.3)]
    head.add(front_plate(mask, hy - 0.9, 0.4, kit.lit(BONE, 13, top=1, bottom=-4), bevel=0.1))
    for s in (-1, 1):
        stripe = [(0.86, 8.84), (0.98, 8.82), (1.12, 9.24), (0.98, 9.18)]
        poly = stripe if s > 0 else mirror_poly_x(stripe)
        head.add(front_plate(poly, hy - 1.28, 0.05, C(ARMOR, 4)))
    head.add(kit.place(kit.spike(0.16, 0.26, BONE_RULE), (0.0, hy - 1.28, 9.12), (0, -1, -0.2)))  # nose ridge
    brow = [(-1.32, 9.76), (-0.18, 9.42), (0.18, 9.42), (1.32, 9.76), (1.27, 10.1), (-1.27, 10.1)]
    head.add(front_plate(brow, hy - 0.85, 0.6, kit.lit(ARMOR, 5, top=4, bottom=-3, front=1), bevel=0.08))
    head.add(front_plate(hexagon(0.38, 0.0, 10.42), hy - 1.08, 0.22, kit.lit(GOLD, 12, top=2, bottom=-3), bevel=0.05))
    for s, poly in ((1, EYE_L), (-1, mirror_poly_x(EYE_L))):  # dark sockets round the eyes
        head.add(front_plate(kit.offset_convex(poly, -0.1), hy - 1.28, 0.08, C("gray", 0)))
    # upper fangs crossing the mouth slot; throat wall (tell cells) behind; dark cheek walls at the sides
    for x, r, h in ((-0.56, 0.16, 0.52), (0.56, 0.16, 0.52), (-0.22, 0.1, 0.28), (0.22, 0.1, 0.28)):
        head.add(kit.place(kit.spike(r, h, kit.lit(BONE, 14, top=1, bottom=-2)), (x, hy - 1.12, 8.66), (0, -0.15, -1)))
    head.add(front_plate([(-0.78, 8.02), (0.78, 8.02), (0.78, 8.72), (-0.78, 8.72)], hy - 0.55, 0.12, C(*TELL)))
    for s in (-1, 1):
        head.add(kit.bevel_box(0.16, 0.9, 0.8, C("gray", 1)).copy(T(s * 0.94, hy - 0.75, 7.98)).paint(
            kit.lit("gray", 1, top=1, bottom=0)))

    for name, s in (("EyeL", 1), ("EyeR", -1)):
        poly = EYE_L if s > 0 else mirror_poly_x(EYE_L)
        cx = sum(p[0] for p in poly) / len(poly)
        cz = sum(p[1] for p in poly) / len(poly)
        eye = m.part(name, (cx, hy - 1.42, cz), parent="Head")

        def eye_rule(c, n):
            return C("yellow", 14 if n.y < -0.8 else 12, True)
        eye.add(front_plate(poly, hy - 1.3, 0.12, eye_rule))

    jaw = m.part("Jaw", JAW_HINGE, parent="Head")
    jpoly = [(-0.5, 7.66), (0.5, 7.66), (0.96, 7.96), (1.08, 8.3), (-1.08, 8.3), (-0.96, 7.96)]
    jaw.add(front_plate(jpoly, hy - 0.1, 1.2, kit.lit(BONE, 12, top=2, bottom=-4), bevel=0.08))
    for x in (-0.8, 0.8):  # the oni's lower tusks rise over the upper lip
        jaw.add(kit.place(kit.spike(0.21, 0.8, kit.lit(BONE, 14, top=1, bottom=-2)), (x, hy - 1.12, 8.22),
                          (x * 0.16, -0.14, 1)))
    for x in (-0.28, 0.28):
        jaw.add(kit.place(kit.spike(0.1, 0.22, kit.lit(BONE, 13, top=1, bottom=-2)), (x, hy - 1.18, 8.27), (0, -0.1, 1)))

    for name, s in (("HornL", 1), ("HornR", -1)):
        base = V((s * 1.25, hy - 0.05, 10.12))
        horn = m.part(name, base, parent="Head")
        pts = [base, V((s * 1.9, hy - 0.1, 10.3)), V((s * 2.38, hy - 0.16, 10.68)), V((s * 2.5, hy - 0.24, 11.07)),
               V((s * 2.33, hy - 0.34, 11.42))]
        hg = k.sweep(pts, [0.48, 0.4, 0.3, 0.17, 0.0], 5, C(BONE, 12), phase=math.pi / 5)

        def horn_rule(c, n):
            t = max(0.0, min(1.0, (c.z - 10.1) / 1.3))
            return C(BONE, min(15, 12 + round(t * 2) + (1 if n.z > 0.4 else -2 if n.z < -0.4 else 0)))
        horn.add(hg.paint(horn_rule))
        horn.add(aimed(k.cyl(0.53, 0.24, 5, C(GOLD, 11), phase=math.pi / 5), base - (pts[1] - base).normalized() * 0.1,
                       pts[1] - base, GOLD_RULE))


def _boss_pads(m):
    """Samurai sode: three stacked lames sloping outward-down, gold hem on the lowest, a 2x3 stud brick on the
    top lame and one bone spike (the shoulder silhouette)."""
    for name, s in (("PadL", 1), ("PadR", -1)):
        pivot = V((s * 2.5, 0.2, 8.0))
        pad = m.part(name, pivot, parent="Torso")
        for i in range(3):
            sx, sy, th = 1.9 + 0.16 * i, 2.45 + 0.12 * i, 0.4
            c = V((s * (2.68 + 0.36 * i), 0.2, 7.95 - 0.5 * i))
            xf = T(*c) @ R(Y, s * 22)
            rule = kit.lit(ARMOR, 8 + (1 if i == 0 else 0), top=3, bottom=-4, front=1)
            pad.add(kit.bevel_box(sx, sy, th, C(ARMOR, 8), bevel=0.09).copy(xf).paint(rule))
            if i == 2:
                hem = kit.bevel_box(sx + 0.06, sy + 0.06, 0.12, C(GOLD, 11)).copy(xf @ T(0, 0, -0.1))
                pad.add(hem.paint(GOLD_RULE))
            if i == 0:
                studs = kit.stud_grid(2, 3, 0.66, 0.2, 0.17, C(ARMOR, 10)).copy(xf @ T(0, 0, th))
                pad.add(studs.paint(kit.lit(ARMOR, 10, top=2, bottom=-3)))
                spk = aimed(kit.spike(0.32, 1.15, C(BONE, 12)), xf @ V((s * -0.35, 0.1, th - 0.05)),
                            (s * 0.35, 0.05, 1.0), kit.lit(BONE, 13, top=1, bottom=-3))
                pad.add(spk)


# left eye in the front view (x > 0 = the character's left): inner corner low, outer corner high (angry slant)
EYE_L = [(0.22, 9.13), (0.98, 9.28), (1.03, 9.62), (0.32, 9.43)]


def _boss_arms(m):
    for s, tag in ((1, "L"), (-1, "R")):
        sh, el, wr = shoulder(s), elbow(s), wrist(s)
        arm = m.part("Arm" + tag, sh, parent="Torso")
        arm.add(k.sweep([sh + V((s * 0.2, 0, -0.2)), el + V((0, 0, 0.2))], [0.74, 0.64], 6, SUIT_RULE,
                        phase=math.pi / 6))
        # rerebrace plate on the outside of the upper arm + gold band above the elbow
        rb = kit.bevel_box(0.3, 1.3, 1.45, ARMOR_RULE, bevel=0.08)
        arm.add(rb.copy(T(s * 3.42, 0.08, 5.65) @ R(Y, -s * 16)).paint(ARMOR_RULE))
        arm.add(aimed(k.cyl(0.7, 0.24, 6, C(GOLD, 11), phase=math.pi / 6), el + (sh - el).normalized() * 0.55,
                      sh - el, GOLD_RULE))

        fore = m.part("Forearm" + tag, el, parent="Arm" + tag)
        axis = wr - el
        L = axis.length
        gaunt = k.lathe([(0.6, 0.0), (0.74, 0.3), (0.9, L - 0.45), (0.98, L - 0.1)], 8, C(ARMOR, 8), phase=math.pi / 8)
        fore.add(aimed(gaunt, el, axis, kit.lit(ARMOR, 8, top=3, bottom=-3, front=1), roll=V((0, -1, 0))))
        fore.add(aimed(k.cyl(1.04, 0.3, 8, C(GOLD, 11), phase=math.pi / 8), el + axis.normalized() * (L - 0.42), axis,
                       GOLD_RULE, roll=V((0, -1, 0))))
        fore.add(kit.place(kit.spike(0.3, 0.55, BONE_RULE), el + V((s * 0.05, 0.42, 0.1)), (s * 0.3, 1, 0.2)))
        for t, h in ((0.4, 0.7), (0.68, 0.55)):  # bone spikes along the outer side
            p = el + axis * t + V((s * 0.82, 0.0, 0.0))
            fore.add(kit.place(kit.spike(0.2, h, BONE_RULE), p, (s, 0.0, 0.25)))
        for t in (0.35, 0.62):  # front studs (sideways brick)
            p = el + axis * t + V((0.0, -0.84, 0.0))
            fore.add(kit.place(kit.stud(0.17, 0.14, kit.lit(ARMOR, 11, top=2, bottom=-3)), p, (0, -1, 0.1)))

        fist = m.part("Fist" + tag, wr, parent="Forearm" + tag)
        fc = V((s * 3.4, -0.6, 2.55))
        fist.add(kit.bevel_box(1.6, 1.75, 1.55, kit.lit(SUIT, 4, top=3, bottom=-2), bevel=0.12).copy(T(fc.x, fc.y, 1.78)))
        fist.add(kit.bevel_box(1.66, 1.8, 0.3, kit.lit(ARMOR, 8, top=3, bottom=-3), bevel=0.06).copy(T(fc.x, fc.y, 3.15)))
        top_studs = kit.stud_grid(2, 2, 0.66, 0.2, 0.16, kit.lit(ARMOR, 10, top=2, bottom=-3))
        fist.add(top_studs.copy(T(fc.x, fc.y, 3.45)))
        # four finger bars + knuckle studs on the front: the tell glow area that faces the camera
        fy = fc.y - 0.875
        for i in range(4):
            x = fc.x + (i - 1.5) * 0.37
            fist.add(front_plate([(x - 0.15, 1.98), (x + 0.15, 1.98), (x + 0.15, 2.92), (x - 0.15, 2.92)], fy + 0.02,
                                 0.14, C(*TELL)))
            fist.add(kit.place(kit.stud(0.15, 0.14, C(*TELL)), (x, fy - 0.05, 3.08), (0, -1, 0.35)))
        # thumb across the lower inner front (armour, so the finger bars stay a clean glow block)
        tx = fc.x - s * 0.3
        fist.add(kit.bevel_box(0.85, 0.36, 0.34, kit.lit(ARMOR, 9, top=3, bottom=-3))
                 .copy(T(tx, fy - 0.12, 1.92)))
        # palm (inner face, toward the body): tell cells for the clap
        palm = kit.bevel_box(1.1, 1.0, 0.1, C(*TELL))
        fist.add(kit.place(palm, (fc.x - s * 0.8, fc.y, 2.55), (-s, 0, 0), up_hint=Z))
        # gold plate on the back of the hand (outer face)
        fist.add(kit.place(kit.bevel_box(1.0, 0.9, 0.1, GOLD_RULE), (fc.x + s * 0.8, fc.y, 2.55),
                           (s, 0, 0), up_hint=Z).paint(GOLD_RULE))


def _wing_spread(s, pivot):
    """Bat/dragon wing in the SPREAD pose (plane facing the front, swept back WING_SWEEP deg). Returns Geo."""
    sweep = math.radians(WING_SWEEP)
    u = V((s * math.cos(sweep), math.sin(sweep), 0.0))  # outward, swept back
    v = Z
    pt = lambda a, b: pivot + u * a + v * b  # noqa: E731
    R0, E, W = pt(0.0, 0.0), pt(1.9, 1.3), pt(3.5, 2.5)
    F1, F2, F3, B = pt(6.4, 1.5), pt(5.7, -0.75), pt(3.6, -2.2), pt(0.8, -1.7)
    g = Geo()
    bone_rule = kit.lit(SUIT, 3, top=3, bottom=-1)
    g.add(k.sweep([R0, E, W], [0.3, 0.24, 0.17], 5, bone_rule, phase=math.pi / 5))
    for f in (F1, F2, F3):
        g.add(k.sweep([W, f], [0.13, 0.0], 4, bone_rule))
    g.add(aimed(kit.spike(0.22, 0.85, C(BONE, 13)), W, (W - E).normalized() + V((0, 0, 0.6)),
                kit.lit(BONE, 13, top=1, bottom=-2)))  # wrist claw (the folded silhouette's spike)
    # membrane: fan from the wrist over the scalloped trailing edge; outer 22% = emissive magenta edge band
    chain = [F1]
    for a, b in ((F1, F2), (F2, F3), (F3, B)):
        mid = (a + b) / 2
        chain += [mid + (W - mid) * 0.16, b]
    inner = 0.78
    front_rule = kit.lit(ARMOR, 4, top=2, bottom=-1)
    back_rule = C(ARMOR, 3)
    edge_rule = C(GLOW_CORE, 11, True)
    for a, b in zip(chain, chain[1:]):
        ai, bi = W.lerp(a, inner), W.lerp(b, inner)
        g.add(two_sided([W, ai, bi], front_rule, back_rule, 0.06))
        g.add(two_sided([ai, a, b, bi], edge_rule, edge_rule, 0.06))
    g.add(two_sided([W, B, R0], front_rule, back_rule, 0.06))
    g.add(two_sided([W, R0, E], front_rule, back_rule, 0.06))
    return g


def _boss_wings(m):
    for name, s in (("WingL", 1), ("WingR", -1)):
        pivot = V((s * 1.0, 1.85, 7.35))
        wing = m.part(name, pivot, parent="Torso")
        g = _wing_spread(s, pivot)
        fold = T(*pivot) @ R(Z, s * WING_FOLD) @ T(*(-pivot))
        wing.add(g.copy(fold))
    # spread = rotate each wing back by -s*WING_FOLD about the vertical axis through its pivot (Blender space,
    # valid before and after the 180 deg facing turn); Unity local Euler y = +s*WING_FOLD (see bl_preview_actors)
    m.notes["wings"] = {
        "exported_pose": "folded (phase 1 / road)",
        "spread_unity_local_euler": {"WingL": [0.0, WING_FOLD, 0.0], "WingR": [0.0, -WING_FOLD, 0.0]},
        "spread_span_m": None,
    }


def _jet_axis(s):
    top = V((s * 1.25, 1.75, 6.35))
    bottom = V((s * 1.6, 2.2, 4.65))
    return top, bottom


def _boss_jets(m):
    for name, s in (("JetL", 1), ("JetR", -1)):
        top, bottom = _jet_axis(s)
        jet = m.part(name, top, parent="Torso")
        axis = bottom - top
        L = axis.length
        pod = k.lathe([(0.0, -0.1), (0.42, 0.0), (0.52, 0.3), (0.52, L - 0.45), (0.62, L - 0.25), (0.62, L)], 6,
                      C(GOLD, 10), phase=math.pi / 6)
        jet.add(aimed(pod, top, axis, kit.lit(GOLD, 10, top=3, bottom=-3), roll=V((0, -1, 0))))
        jet.add(aimed(k.cyl(0.56, 0.16, 6, C(SUIT, 2), phase=math.pi / 6), top + axis.normalized() * (L - 0.62), axis,
                      kit.lit(SUIT, 2, top=2, bottom=0), roll=V((0, -1, 0))))
        glow = k.cyl(0.46, 0.06, 6, C("orange", 13, True), phase=math.pi / 6)
        jet.add(aimed(glow, bottom - axis.normalized() * 0.04, axis, lambda c, n: C("orange", 13, True)))
        # emissive vent slit on the outer side: the part of the jet the road camera can see
        vent_p = top + axis * 0.55 + V((s * 0.53, 0.0, 0.0))
        jet.add(kit.place(kit.bevel_box(0.16, 0.9, 0.06, C("orange", 12, True)), vent_p, (s, 0.0, 0.0),
                          up_hint=axis).paint(lambda c, n: C("orange", 12, True)))


# ================================================================== BB_Minion - skeleton warrior (~1 m)
def bb_minion():
    m = kit.BBModel("BB_Minion", BUDGETS["BB_Minion"], facing="-Z", pivot="feet")
    body = m.part("Body", (0.0, 0.0, 0.0))
    bone = kit.lit(BONE, 12, top=2, bottom=-3)
    for s in (-1, 1):
        body.add(k.box(0.13, 0.19, 0.065, kit.lit(BONE, 11, top=2, bottom=-3), top_scale=(0.9, 0.8)).copy(
            T(s * 0.085, -0.03, 0.0)).paint(kit.lit(BONE, 11, top=2, bottom=-3)))
        body.add(k.sweep([(s * 0.08, 0.0, 0.26), (s * 0.085, -0.01, 0.05)], 0.042, 4, kit.lit(BONE, 11, top=2),
                         phase=math.pi / 4))
    body.add(k.box(0.25, 0.14, 0.08, bone, top_scale=(1.0, 0.9)).copy(T(0, 0, 0.22)).paint(bone))
    # purple loin flap (the Titan's colours)
    body.add(front_plate([(-0.075, 0.1), (0.075, 0.1), (0.09, 0.29), (-0.09, 0.29)], -0.055, 0.03,
                         kit.lit(ARMOR, 8, top=2, bottom=-3)))
    body.add(k.cyl(0.1, 0.2, 6, C(BONE, 4), phase=math.pi / 6).copy(S(1.0, 0.8, 1.0) @ T(0, 0, 0.29)).paint(C(BONE, 4)))
    for z0, z1, r in ((0.31, 0.355, 0.15), (0.37, 0.415, 0.165)):
        body.add(k.cyl(r, z1 - z0, 8, C(BONE, 13), phase=math.pi / 8, r_top=r * 0.94).copy(S(1.0, 0.8, 1.0) @ T(0, 0, z0))
                 .paint(kit.lit(BONE, 12, top=2, bottom=-3)))
    # purple sash + knot tail (the minion's team colour) and one gold-trimmed pauldron
    body.add(k.lathe([(0.19, 0.42), (0.185, 0.47), (0.1, 0.5)], 8, C(ARMOR, 8), phase=math.pi / 8).copy(S(1.0, 0.85, 1.0))
             .paint(kit.lit(ARMOR, 8, top=2, bottom=-3)))
    body.add(front_plate([(-0.04, 0.0), (0.04, 0.0), (0.05, 0.16), (-0.05, 0.19)], 0.0, 0.025, C(ARMOR, 7))
             .copy(T(0.1, 0.17, 0.33) @ R(Z, 180)).paint(kit.lit(ARMOR, 7, top=2, bottom=-2)))
    pad = kit.bevel_box(0.17, 0.2, 0.06, kit.lit(ARMOR, 9, top=3, bottom=-3), bevel=0.015)
    body.add(pad.copy(T(0.18, 0.0, 0.45) @ R(Y, -25)).paint(kit.lit(ARMOR, 9, top=3, bottom=-3)))
    body.add(kit.place(kit.spike(0.03, 0.08, C(GOLD, 12)), (0.21, 0.0, 0.5), (0.4, 0, 1)).paint(GOLD_RULE))

    # skull (chibi), tilted up toward the high chase camera
    hz = 0.69
    head = Geo()
    skull = k.sphere(0.215, 8, 5, C(BONE, 13), phase=math.pi / 8)
    skull.deform(lambda v: V((v.x * (1.0 if v.z > -0.05 else 0.84), v.y * 0.92, v.z * 0.9)))
    skull = skull.copy(T(0, 0, hz))
    skull.paint(k.vgrad(BONE, 11, 14, hz - 0.15, hz + 0.15))
    head.add(skull)
    head.add(k.box(0.22, 0.14, 0.075, kit.lit(BONE, 11, top=2, bottom=-2), top_scale=(1.05, 1.0)).copy(
        T(0, -0.05, hz - 0.235)).paint(kit.lit(BONE, 11, top=2, bottom=-2)))
    head.add(k.box(0.15, 0.02, 0.016, C(BONE, 3)).copy(T(0, -0.125, hz - 0.19)))
    sockets, _ = k.eye_pair(skull, hz - 0.01, 0.088, 0.083, 0.088, "socket", elevation=28, glow=C("green", 11, True),
                            segs=6, look=(0.0, 0.0))
    head.add(sockets)
    nose = k.slab([(-0.025, 0.022), (0.0, -0.022), (0.025, 0.022)], 0.02, C(BONE, 2))
    hit, n = k.surface(skull, (0.0, -3.0, hz - 0.085), (0, 1, 0))
    head.add(nose.copy(k.frame(hit - n * 0.005, n)))
    # purple bandana + LEGO stud on the crown + two bandana tails at the back
    band = k.lathe([(0.205, hz + 0.06), (0.21, hz + 0.1), (0.185, hz + 0.135)], 8, C(ARMOR, 9), phase=math.pi / 8)
    head.add(band.copy(S(1.04, 0.98, 1.0)).paint(kit.lit(ARMOR, 9, top=2, bottom=-2)))
    head.add(k.cyl(0.075, 0.07, 6, C(BONE, 13), phase=math.pi / 6).copy(T(0, 0, hz + 0.18)).paint(kit.lit(BONE, 12, top=2)))
    for s in (-1, 1):
        tail = kit.bevel_box(0.05, 0.03, 0.15, C(ARMOR, 8))
        head.add(tail.copy(T(s * 0.05, 0.2, hz + 0.0) @ R(X, -35) @ R(Y, s * 20)).paint(kit.lit(ARMOR, 8, top=2)))
    neck = V((0.0, 0.0, 0.48))
    body.add(head.copy(T(*neck) @ R(X, -14) @ T(*(-neck))))

    for name, s in (("ArmL", 1), ("ArmR", -1)):
        sh = V((s * 0.17, 0.0, 0.43))
        hand = V((s * 0.31, -0.09, 0.3))
        arm = m.part(name, sh, parent="Body")
        arm.add(k.sweep([sh, (s * 0.25, -0.03, 0.36), hand], 0.036, 4, kit.lit(BONE, 12, top=2), phase=math.pi / 4))
        arm.add(k.sphere(0.058, 5, 3, kit.lit(BONE, 12, top=2), phase=math.pi / 5).copy(T(*hand))
                .paint(kit.lit(BONE, 12, top=2, bottom=-3)))
    grip = V((-0.31, -0.09, 0.3))
    weapon = m.part("Weapon", grip, parent="ArmR")
    top = grip + V((-0.05, -0.08, 0.42))
    sd = (top - grip).normalized()
    weapon.add(k.sweep([grip - sd * 0.08, top], [0.024, 0.028], 5, kit.lit("brown", 6, top=2)))
    # cleaver-axe head: chunky steel blade with a bright edge + a back spike (reads at ~30 px/m)
    blade = [(0.0, -0.1), (0.15, -0.13), (0.2, 0.0), (0.15, 0.13), (0.0, 0.1)]
    side_dir = sd.cross(V((0, 1, 0))).normalized()
    bm = k.frame(top - sd * 0.08, side_dir.cross(sd).normalized(), sd)
    axe = k.slab(blade, 0.035, C("gray", 9), cap_color=C("gray", 12)).copy(bm)
    axe.paint(lambda c, n: C("gray", 14) if (c - (top - sd * 0.08)).dot(side_dir) < -0.15 else
              C("gray", 12 if abs(n.dot(side_dir.cross(sd))) > 0.7 else 8))
    weapon.add(axe)
    weapon.add(aimed(kit.spike(0.03, 0.09, C(BONE, 13)), top - sd * 0.06 + side_dir * 0.04, side_dir,
                     kit.lit(BONE, 13, top=1)))
    m.anchor("FeetPoint", "Body", (0.0, 0.0, 0.0))
    m.anchor("HeadTopPoint", "Body", (0.0, 0.0, 1.0))
    m.anchor("WeaponTip", "Weapon", tuple(top))
    m.shadow_casters = ["Body"]
    kit.bake_ao(m, distance=0.22, strength=2.5, rays=64, ground=True)
    m.face_minus_z()
    return m


# ================================================================== BB_NinjaCar - golden ninja car (faces +Z)
CAR_GOLD = kit.lit(GOLD, 11, top=2, bottom=-4, front=0)
CAR_BLACK = kit.lit("gray", 2, top=2, bottom=-1)
WHEEL_X, WHEEL_Y, WHEEL_R = 0.71, 0.8, 0.3
ROOF_Z = 0.96


def bb_ninja_car():
    m = kit.BBModel("BB_NinjaCar", BUDGETS["BB_NinjaCar"], facing="+Z", pivot="bottom-centre")
    body = m.part("Body", (0.0, 0.0, 0.0))
    # chassis: side profile (nose at -Y = Unity +Z)
    chassis = [(-1.2, 0.2), (1.2, 0.2), (1.2, 0.56), (1.08, 0.64), (-0.5, 0.62), (-1.2, 0.42)]
    body.add(side_prism(chassis, -0.6, 0.6, CAR_GOLD))
    body.add(kit.bevel_box(1.26, 2.3, 0.1, CAR_BLACK).copy(T(0, 0, 0.15)))     # black underbody
    # gold fender arches over the exposed wheels
    for sx in (-1, 1):
        for sy in (-1, 1):
            f = kit.bevel_box(0.3, 0.74, 0.1, kit.lit(GOLD, 12, top=2, bottom=-5), bevel=0.03)
            body.add(f.copy(T(sx * WHEEL_X, sy * WHEEL_Y, 0.6)))
            body.add(kit.bevel_box(0.08, 0.74, 0.12, CAR_BLACK).copy(T(sx * (WHEEL_X - 0.15), sy * WHEEL_Y, 0.5)))
    # katana side blades between the wheels (the ninja read from behind) + black side skirts
    for sx in (-1, 1):
        body.add(kit.bevel_box(0.08, 0.86, 0.16, CAR_BLACK).copy(T(sx * 0.62, 0.0, 0.2)))
        blade = [(0.0, -0.5), (0.07, -0.18), (0.07, 0.42), (0.0, 0.42)]
        bl = k.prism(blade, 0.3, 0.38, C("gray", 12))
        bl = bl.copy(T(sx * 0.62, 0, 0) @ S(sx, 1, 1)) if sx > 0 else bl.copy(T(sx * 0.62, 0, 0) @ S(sx, 1, 1))
        body.add(bl.paint(lambda c, n: C("gray", 14 if n.z > 0.5 else 12 if abs(n.x) > 0.5 else 10)))
    # cabin (dark glass) + gold roof plate = the minions' roof
    cabin = [(-0.55, 0.6), (0.78, 0.6), (0.55, 0.92), (-0.18, 0.92)]

    def glass(c, n):
        if n.z > 0.85:
            return C("sky", 5)
        return C("sky", 6 if n.y < -0.3 else 3)
    body.add(side_prism(cabin, -0.5, 0.5, glass))
    for sx in (-1, 1):  # gold pillars between the windows
        body.add(kit.bevel_box(0.04, 0.1, 0.3, CAR_GOLD).copy(T(sx * 0.505, 0.16, 0.61)))
    roof = kit.bevel_box(1.12, 0.84, 0.05, kit.lit(GOLD, 12, top=1, bottom=-5), bevel=0.02)
    body.add(roof.copy(T(0, 0.17, ROOF_Z - 0.05)))
    for sx in (-1, 1):  # grab rails at the roof edges (minions hold these)
        body.add(kit.bevel_box(0.06, 0.66, 0.05, CAR_BLACK).copy(T(sx * 0.53, 0.17, ROOF_Z)))
        for sy in (-0.12, 0.46):
            body.add(kit.bevel_box(0.06, 0.06, 0.05, CAR_BLACK).copy(T(sx * 0.53, sy, ROOF_Z - 0.01)))
    # roof rocket launcher: black mount + gold pod pitched up 14 deg, three tubes with red rocket noses, a lit
    # 'armed' strip on the back (what the chase camera sees)
    body.add(kit.bevel_box(0.34, 0.3, 0.06, CAR_BLACK).copy(T(0, 0.26, ROOF_Z)))
    pod_c = V((0.0, 0.22, ROOF_Z + 0.15))
    pod_m = T(*pod_c) @ R(X, -14)
    pod = kit.bevel_box(0.5, 0.46, 0.2, C(GOLD, 11), bevel=0.03).copy(pod_m @ T(0, 0, -0.1))
    body.add(pod.paint(kit.lit(GOLD, 11, top=2, bottom=-4)))
    for i in range(3):
        x = (i - 1) * 0.15
        mouth = pod_m @ V((x, -0.235, 0.0))
        fwd = (pod_m.to_3x3() @ V((0, -1, 0))).normalized()
        body.add(aimed(k.cyl(0.062, 0.03, 6, C("gray", 0)), mouth - fwd * 0.005, fwd, C("gray", 0)))
        body.add(aimed(k.cone(0.045, 0.09, 6, C("red", 10)), mouth + fwd * 0.02, fwd, kit.lit("red", 10, top=2)))
    back_p = pod_m @ V((0.0, 0.235, -0.02))
    back_n = (pod_m.to_3x3() @ V((0, 1, 0))).normalized()
    body.add(kit.place(kit.bevel_box(0.34, 0.06, 0.02, C("orange", 12, True)), back_p, back_n)
             .paint(lambda c, n: C("orange", 12, True)))
    # spoiler on two struts, red end plates
    for sx in (-1, 1):
        body.add(kit.bevel_box(0.05, 0.08, 0.2, CAR_BLACK).copy(T(sx * 0.42, 1.04, 0.6)))
        body.add(kit.bevel_box(0.05, 0.34, 0.18, kit.lit("red", 9, top=2, bottom=-3)).copy(T(sx * 0.75, 1.07, 0.7)))
    body.add(kit.bevel_box(1.46, 0.28, 0.06, kit.lit(GOLD, 12, top=2, bottom=-6), bevel=0.02).copy(T(0, 1.07, 0.79)))
    # rear: tail lights, gold shuriken emblem on a black plate, exhausts
    for sx in (-1, 1):
        body.add(kit.place(kit.bevel_box(0.36, 0.1, 0.03, C("red", 11, True)), (sx * 0.38, 1.2, 0.47), (0, 1, 0))
                 .paint(lambda c, n: C("red", 12 if n.y > 0.8 else 10, True)))
        body.add(aimed(k.cyl(0.055, 0.08, 6, C("gray", 3)), (sx * 0.22, 1.18, 0.27), (0, 1, 0), kit.lit("gray", 4, top=2)))
    body.add(kit.place(kit.bevel_box(0.3, 0.2, 0.025, CAR_BLACK), (0.0, 1.2, 0.33), (0, 1, 0)).paint(C("gray", 1)))
    star = []
    for i in range(8):
        r = 0.085 if i % 2 == 0 else 0.03
        a = math.pi / 2 + math.tau * i / 8
        star.append((r * math.cos(a), r * math.sin(a)))
    body.add(kit.place(k.prism(star, 0.0, 0.02, C(GOLD, 13)), (0.0, 1.225, 0.33), (0, 1, 0))
             .paint(kit.lit(GOLD, 13, top=1, bottom=-2)))
    # front: headlights, black bumper with two forward fang blades
    for sx in (-1, 1):
        body.add(kit.place(kit.bevel_box(0.24, 0.1, 0.03, C("gray", 14, True)), (sx * 0.4, -1.2, 0.36), (0, -1, 0.35))
                 .paint(lambda c, n: C("gray", 14, True)))
        fang = k.prism([(0.0, 0.0), (0.06, 0.0), (0.0, 0.2)], -0.03, 0.03, C("gray", 12)).copy(R(X, 90) @ R(Z, 180))
        body.add(fang.copy(T(sx * 0.45, -1.24, 0.22)).paint(kit.lit("gray", 12, top=2, bottom=-2)))
    body.add(kit.bevel_box(1.26, 0.14, 0.14, CAR_BLACK, bevel=0.02).copy(T(0, -1.19, 0.18)))

    for name, sx, sy in (("WheelFL", 1, -1), ("WheelFR", -1, -1), ("WheelRL", 1, 1), ("WheelRR", -1, 1)):
        c = V((sx * WHEEL_X, sy * WHEEL_Y, WHEEL_R))
        wheel = m.part(name, c, parent="Body")
        tyre = k.cyl(WHEEL_R, 0.24, 8, C("gray", 2), phase=math.pi / 8)

        def tread(cc, n, c=c):
            if abs(n.x) > 0.9:
                return C("gray", 2)
            a = math.atan2(cc.z - c.z, cc.y - c.y)
            return C("gray", 3 if int(round(a / (math.tau / 8))) % 2 else 1)
        wheel.add(aimed(tyre, c - V((0.12, 0, 0)), (1, 0, 0), tread))
        out = V((sx, 0, 0))
        wheel.add(aimed(k.cyl(0.17, 0.04, 6, C(GOLD, 11), phase=math.pi / 6), c + out * 0.115, out,
                        kit.lit(GOLD, 11, top=2, bottom=-3)))
        wheel.add(kit.place(kit.stud(0.07, 0.04, C(GOLD, 14)), c + out * 0.155, out).paint(kit.lit(GOLD, 13, top=1)))

    m.anchor("RoofCornerL", "Body", (0.55, -0.2, ROOF_Z))
    m.anchor("RoofCornerR", "Body", (-0.55, -0.2, ROOF_Z))
    m.anchor("LauncherMuzzle", "Body", tuple(pod_m @ V((0.0, -0.27, 0.0))))
    m.anchor("ExhaustL", "Body", (0.22, 1.27, 0.27))
    m.anchor("ExhaustR", "Body", (-0.22, 1.27, 0.27))
    m.shadow_casters = ["Body"]
    kit.bake_ao(m, distance=0.3, strength=2.5, rays=64, ground=True)
    return m


# ================================================================== BB_NinjaJet - the jet the car rebuilds into (+Z)
def bb_ninja_jet():
    m = kit.BBModel("BB_NinjaJet", BUDGETS["BB_NinjaJet"], facing="+Z", pivot="centre")
    body = m.part("Body", (0.0, 0.0, 0.0))
    hull = k.lathe([(0.0, 0.0), (0.12, 0.18), (0.21, 0.5), (0.26, 1.05), (0.25, 1.65), (0.2, 2.02)], 8, C(GOLD, 11),
                   phase=math.pi / 8)
    hull = hull.copy(k.aim(V((0.0, -1.0, 0.0)), V((0.0, 1.0, 0.0)), Z) @ S(1.0, 0.72, 1.0))

    def hull_rule(c, n):
        if n.z < -0.45:
            return C("gray", 2)
        return C(GOLD, 13 if n.z > 0.6 else 11 if n.z > -0.1 else 8)
    body.add(hull.paint(hull_rule))
    body.add(k.dome(0.15, 0.34, 0.13, 6, C("sky", 5)).copy(T(0, -0.36, 0.13)).paint(
        lambda c, n: C("sky", 7 if n.z > 0.75 else 4)))
    # red ninja-visor stripe across the nose + black side intakes
    body.add(kit.place(kit.bevel_box(0.3, 0.08, 0.02, kit.lit("red", 9, top=2)), (0.0, -0.78, 0.1), (0, -0.25, 1),
                       up_hint=V((0, -1, 0))).paint(kit.lit("red", 9, top=2)))
    for sx in (-1, 1):
        body.add(kit.bevel_box(0.1, 0.5, 0.12, CAR_BLACK).copy(T(sx * 0.24, 0.1, -0.06)))
        stab = [(0.16, 0.66), (0.48, 0.84), (0.48, 0.96), (0.16, 0.98)]
        sp = stab if sx > 0 else [(-x, y) for x, y in reversed(stab)]
        body.add(k.prism(sp, -0.02, 0.02, C(GOLD, 11)).paint(kit.lit(GOLD, 11, top=2, bottom=-6)))
    fin = [(0.5, 0.1), (0.98, 0.1), (1.0, 0.44), (0.84, 0.44)]
    body.add(side_prism(fin, -0.025, 0.025, lambda c, n: C("red", 9) if c.z > 0.34 else C(GOLD, 12 if n.z > 0.5 else 10)))

    for name, sx in (("WingL", 1), ("WingR", -1)):
        root = V((sx * 0.2, 0.05, -0.02))
        wing = m.part(name, root, parent="Body")
        wp = [(0.18, -0.36), (0.88, 0.12), (0.88, 0.34), (0.18, 0.46)]
        wp = wp if sx > 0 else [(-x, y) for x, y in reversed(wp)]
        wg = k.prism(wp, -0.035, 0.035, C(GOLD, 11)).copy(T(0, 0, -0.02))

        def wing_rule(c, n, sx=sx):
            if n.z < -0.5:
                return C("gray", 2)
            if n.z > 0.5:
                return C("gray", 2) if 0.5 < abs(c.x) < 0.6 else C(GOLD, 13)  # black stripe across the top
            return C(GOLD, 9)
        wing.add(wg.paint(wing_rule))
        # black stripe as geometry (the stripe faces are separate so the rule above can see them)
        stripe = [(0.5, -0.12), (0.6, -0.05), (0.6, 0.4), (0.5, 0.42)]
        stripe = stripe if sx > 0 else [(-x, y) for x, y in reversed(stripe)]
        wing.add(k.prism(stripe, 0.016, 0.03, C("gray", 2)).paint(kit.lit("gray", 2, top=1)))
        blade = [(0.86, -0.5), (0.95, 0.06), (0.95, 0.36), (0.86, 0.36)]
        blade = blade if sx > 0 else [(-x, y) for x, y in reversed(blade)]
        wing.add(k.prism(blade, -0.045, 0.045, C("gray", 12)).copy(T(0, 0, -0.02))
                 .paint(lambda c, n: C("gray", 14 if n.z > 0.5 else 11)))
        wing.add(kit.place(kit.bevel_box(0.07, 0.16, 0.025, C("red", 11, True)), (sx * 0.9, 0.36, -0.02), (0, 1, 0))
                 .paint(lambda c, n: C("red", 11, True)))   # wing-tip nav light, seen from the chase camera

    engine = m.part("Engine", (0.0, 1.0, 0.0), parent="Body")
    engine.add(aimed(k.cyl(0.2, 0.14, 8, C("gray", 2), phase=math.pi / 8, r_top=0.22), (0.0, 0.98, 0.0), (0, 1, 0),
                     kit.lit("gray", 3, top=2, bottom=-1)))
    engine.add(aimed(k.cyl(0.17, 0.02, 8, C("orange", 13, True), phase=math.pi / 8), (0.0, 1.11, 0.0), (0, 1, 0),
                     lambda c, n: C("orange", 13, True)))
    engine.add(aimed(k.cyl(0.08, 0.02, 6, C("yellow", 15, True)), (0.0, 1.125, 0.0), (0, 1, 0),
                     lambda c, n: C("yellow", 15, True)))

    cannon = m.part("Cannon", (0.0, -0.8, -0.1), parent="Body")
    cannon.add(kit.bevel_box(0.2, 0.32, 0.09, CAR_BLACK).copy(T(0.0, -0.78, -0.16)))
    for sx in (-1, 1):
        cannon.add(aimed(k.cyl(0.032, 0.32, 6, C("gray", 4)), (sx * 0.06, -0.86, -0.11), (0, -1, 0),
                         kit.lit("gray", 5, top=2)))
        cannon.add(aimed(k.cyl(0.04, 0.03, 6, C("cyan", 12, True)), (sx * 0.06, -1.17, -0.11), (0, -1, 0),
                         lambda c, n: C("cyan", 12, True)))
    m.anchor("CannonMuzzle", "Cannon", (0.0, -1.22, -0.11))
    m.anchor("EngineExhaust", "Engine", (0.0, 1.15, 0.0))
    m.anchor("WingTipL", "WingL", (0.95, 0.2, -0.02))
    m.anchor("WingTipR", "WingR", (-0.95, 0.2, -0.02))
    m.shadow_casters = ["Body"]
    kit.bake_ao(m, distance=0.22, strength=2.0, rays=64, ground=False)
    return m


REGISTRY = {
    "BB_Boss": bb_boss,
    "BB_Minion": bb_minion,
    "BB_NinjaCar": bb_ninja_car,
    "BB_NinjaJet": bb_ninja_jet,
}
