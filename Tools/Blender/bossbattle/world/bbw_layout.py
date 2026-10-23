"""Review dioramas for the Boss Battle world (pure data + one Blender helper). Review tooling only: these layouts are
the recommended placement for package A's BossBattleWorldBuilder (road strip, sky strip, backdrop), not a game asset.

Unity space, arena local (TDD 3.4): vehicle at z 0, +z ahead, x right, y up. Items = dict(piece, pos, yaw, pitch,
roll, scale). Pieces resolve to the world FBXs this package exports, then to the actor FBXs of package M part 1
(`Models/Ninjago/BossBattle/Actors/BB_*.fbx`); actors that are not exported yet are built in-process from the actor
scripts (read only), else replaced by blocky stand-ins of the right size, so the composition is always judged with
the boss / vehicle in place.

Road strip (phase 1, dusk): 20 m segments from z -10 to 110 (6 segments; ScrollingStrip wrap length 120 m, the
backdrop ridge at z 104 hides the wrap). Shoulders at x +-3.2, canyon cliffs at x +-5.4 alternating A/B variants
(left side offset by 10 m so the two walls never repeat in step), torii arches every 40 m (z 16 A, 56 B, 96 A),
braziers by the arch pillars + one between arches on alternating sides. Sky strip (phase 2, night): cloud floor slabs
at y -7 (3 across, 30 m apart), a cloud ceiling at y 19 (slabs rolled 180 deg), floating isles left / right outside
x +-12, moon at (26, 30, 96), backdrop ridge sunk to y -16 at z 108 so the peaks rise out of the cloud sea.
"""
import math
import os

import bbw_paths as P

SEG = 20.0
ACTORS = ("BB_Boss", "BB_NinjaCar", "BB_NinjaJet", "BB_Minion")


def it(piece, x, y, z, yaw=0.0, scale=1.0, pitch=0.0, roll=0.0):
    return dict(piece=piece, pos=(x, y, z), yaw=yaw, scale=scale, pitch=pitch, roll=roll)


# ------------------------------------------------------------------ road (phase 1)
def road_strip(z_from=0, count=6, cliffs=True):
    items = []
    for i in range(count):
        z = z_from + SEG * i
        items.append(it("BB_RoadSegment", 0, 0, z))
        items.append(it("BB_Shoulder", 3.2, 0, z))
        items.append(it("BB_Shoulder", -3.2, 0, z, yaw=180))
        if cliffs:
            items.append(it("BB_Cliff_R_A" if i % 2 == 0 else "BB_Cliff_R_B", 5.4, 0, z))
            items.append(it("BB_Cliff_L_B" if i % 2 == 0 else "BB_Cliff_L_A", -5.4, 0, z + 10.0))
    if cliffs:
        items.append(it("BB_Cliff_L_A", -5.4, 0, z_from - 10.0))
    return items


def road_dressing():
    items = [it("BB_Arch_A", 0, 0, 16), it("BB_Arch_B", 0, 0, 56), it("BB_Arch_A", 0, 0, 96)]
    for z in (16, 56, 96):
        items += [it("BB_Brazier", -4.25, 0, z - 1.6, yaw=15), it("BB_Brazier", 4.25, 0, z - 1.6, yaw=-20)]
    items += [it("BB_Brazier", 4.3, 0, 36, yaw=40), it("BB_Brazier", -4.3, 0, 76, yaw=-10)]
    items += [it("BB_Backdrop_Ridge", 0, 0, 104), it("BB_Backdrop_Temple", -27, 0, 107, scale=0.8),
              it("BB_Backdrop_Temple", 34, 0, 112, scale=0.6, yaw=20)]
    return items


ROAD_SHAFTS = [dict(top=(5.5, 14.0, 64.0), r=1.3, length=26.0, intensity=1.0),
               dict(top=(4.5, 13.0, 24.0), r=1.1, length=22.0, intensity=0.8),
               dict(top=(8.0, 15.0, 100.0), r=1.6, length=30.0, intensity=1.0)]

# ------------------------------------------------------------------ sky (phase 2)
CLOUD_FLOOR_Y = -7.0
CLOUD_CEIL_Y = 19.0


def sky_strip():
    items = []
    for row, z in enumerate((6, 36, 66, 96)):
        for col, x in enumerate((-32, 0, 32)):
            name = "BB_CloudSlab_A" if (row + col) % 2 == 0 else "BB_CloudSlab_B"
            items.append(it(name, x + (6 if row % 2 else -4), CLOUD_FLOOR_Y + (row % 2) * 0.8, z,
                            yaw=90 * ((row + col) % 4)))
    for row, z in enumerate((30, 70, 110)):
        for col, x in enumerate((-30, 6, 40)):
            name = "BB_CloudSlab_B" if (row + col) % 2 == 0 else "BB_CloudSlab_A"
            items.append(it(name, x - row * 5, CLOUD_CEIL_Y + row * 2.5, z, roll=180, yaw=60 * col))
    items += [it("BB_FloatingIsle_A", -17, -1.5, 34, yaw=25), it("BB_FloatingIsle_B", 19, 1.0, 56, yaw=-30),
              it("BB_FloatingIsle_B", -24, 4.0, 82, yaw=140, scale=1.2), it("BB_FloatingIsle_A", 26, -3.0, 22,
                                                                            yaw=-80, scale=0.9)]
    items += [it("BB_Moon", 26, 30, 96), it("BB_Backdrop_Ridge", 0, -16, 108),
              it("BB_Backdrop_Temple", -30, -14, 104, scale=0.9)]
    return items


SKY_SHAFTS = [dict(top=(14.0, 26.0, 52.0), r=2.0, length=34.0, intensity=1.0),
              dict(top=(30.0, 26.0, 80.0), r=2.4, length=40.0, intensity=0.8)]

# ------------------------------------------------------------------ gameplay props in place
ROAD_PROPS = [
    it("BB_NinjaCar", 0, 0, 0),
    it("BB_Reticle", -2, 0.03, 0, scale=1.25),          # hit footprint radius 0.55 + 0.7 (car half width)
    it("BB_Rock", -1.6, 6.5, 9, yaw=30, pitch=20),
    it("BB_PowerCrate", 2, 0.75, 9, yaw=30),
    it("BB_Brick", -2, 0, 21), it("BB_Brick", 0, 0, 21),
    it("BB_CrackGlow", 0, 0.02, 45), it("BB_CrackGlow", 2, 0.02, 45),
    it("BB_CrackGlow", 0, 0.02, 30.5), it("BB_CrackGlow", 0, 0.02, 38),
    it("BB_Brick", 0, 0, 45, scale=(1, 0.35, 1)), it("BB_Brick", 2, 0, 45, scale=(1, 0.35, 1)),
    it("BB_SideWall", 3.65, 0, 1.0),
    it("BB_Rocket", 0.3, 2.4, 8, pitch=-6), it("BB_Rocket", -0.4, 4.0, 17, pitch=-8),
    it("BB_Rocket", 0.6, 5.2, 24, pitch=-10),
]
SKY_PROPS = [
    it("BB_NinjaJet", 0, 3.2, 0),
    it("BB_WindGust", -2, 4.8, 13, yaw=20), it("BB_WindGust", 0, 4.8, 13, yaw=150), it("BB_WindGust", 2, 1.6, 13,
                                                                                         yaw=260),
    it("BB_WindGust", -2, 1.6, 13, yaw=80), it("BB_WindGust", 2, 4.8, 13, yaw=200),
    it("BB_SpikeWall", -4.1, 4.4, 4.0), it("BB_SpikeWall", 4.1, 4.4, 4.0, yaw=180),
    it("BB_LaserBolt", 0.15, 3.25, 5), it("BB_LaserBolt", 0.1, 3.2, 9), it("BB_LaserBolt", 0.05, 3.15, 15),
]

SCENES = {
    "road_dusk": dict(look="dusk", items=road_strip() + road_dressing() + [it("BB_Boss", 0, 0, 30),
                                                                            it("BB_NinjaCar", 0, 0, 0)],
                      shafts=ROAD_SHAFTS),
    "road_props": dict(look="dusk", items=road_strip() + road_dressing() + [it("BB_Boss", 0, 0, 30)] + ROAD_PROPS,
                       shafts=ROAD_SHAFTS),
    "sky_night": dict(look="night", items=sky_strip() + [it("BB_Boss", 0, -4.5, 30), it("BB_NinjaJet", 0, 3.2, 0)],
                      shafts=SKY_SHAFTS),
    "sky_props": dict(look="night", items=sky_strip() + [it("BB_Boss", 0, -4.5, 30)] + SKY_PROPS,
                      shafts=SKY_SHAFTS),
}


def resolve_fbx(name):
    for d in (P.MODELS_OUT, P.ACTORS_OUT):
        path = os.path.join(d, name + ".fbx")
        if os.path.exists(path):
            return path
    return None


# ------------------------------------------------------------------ actor stand-ins (Blender only)
def _proxy_piece(name):
    """Blocky stand-in of the right size / pivot (only when neither the actor FBX nor the actor script works)."""
    from bbw_kit import C, Piece, box, cbox
    p = Piece(name, 4000, pivot="free")
    g = p.part("Proxy")
    if name == "BB_Boss":
        for (sx, sy, sz, y0, x) in ((1.6, 4.2, 1.6, 0.0, -1.2), (1.6, 4.2, 1.6, 0.0, 1.2), (4.4, 4.0, 2.6, 4.0, 0.0),
                                    (2.2, 2.0, 2.0, 8.0, 0.0), (1.4, 4.0, 1.4, 4.4, -3.0), (1.4, 4.0, 1.4, 4.4, 3.0)):
            g.add(box(sx, sy, sz, C("purple", 7), y0=y0).copy(), _t(x, 0, 0))
        g.add(box(1.4, 0.3, 0.2, C("red", 13, True), y0=9.0), _t(0, 0, -1.05))
        g.add(box(1.0, 1.0, 0.2, C("magenta", 12, True), y0=6.0), _t(0, 0, -1.35))
    elif name == "BB_NinjaCar":
        g.add(box(1.6, 0.55, 2.4, C("red", 9)))
        g.add(box(1.1, 0.35, 1.2, C("yellow", 10), y0=0.55))
    elif name == "BB_NinjaJet":
        g.add(cbox(0.6, 0.5, 2.1, C("red", 9)))
        g.add(cbox(1.8, 0.12, 0.8, C("yellow", 10)))
    else:
        g.add(box(0.5, 1.2, 0.4, C("gray", 11)))
    return p


def _t(x, y, z):
    from bbw_kit import T
    return T(x, y, z)


def _actor_model(name):
    """Build the actor in-process from package M part 1's scripts (read only). Returns objects or raises."""
    import sys
    actors = os.path.join(os.path.dirname(P.HERE), "actors")
    if actors not in sys.path:
        sys.path.append(actors)
    import models_bb_actors as mba  # noqa: F401  (the actor package's registry)
    return mba.REGISTRY[name]()


def build_proxies(sc, colls, mat):
    """Collections for scene actors that have no FBX yet: {name: collection} (not linked to the scene)."""
    import bpy
    import bl_common as bc
    if bc._PALETTE is None:
        bc.load_palette(P.PALETTE_JSON)
    out = {}
    for name in sorted({i["piece"] for i in sc["items"]} - set(colls)):
        if name not in ACTORS:
            continue
        before = set(bpy.data.objects)
        source = "script"
        try:
            model = _actor_model(name)
            model.build(mat)
        except Exception as e:  # noqa: BLE001  (review only: fall back to a stand-in)
            print(f"PROXY {name}: actor script unavailable ({type(e).__name__}: {e}); using a stand-in")
            for o in set(bpy.data.objects) - before:
                bpy.data.objects.remove(o, do_unlink=True)
            source = "stand-in"
            _proxy_piece(name).to_model().build(mat)
        objs = [o for o in bpy.data.objects if o not in before]
        coll = bpy.data.collections.new(f"{name} ({source})")
        for o in objs:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            coll.objects.link(o)
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mat)
        out[name] = coll
    return out


# debug: BBW_ONLY=BB_NinjaJet,BB_Boss renders a scene with only those pieces (review tooling)
if os.environ.get("BBW_ONLY"):
    _keep = set(os.environ["BBW_ONLY"].split(","))
    for _sc in SCENES.values():
        _sc["items"] = [i for i in _sc["items"] if i["piece"] in _keep]
