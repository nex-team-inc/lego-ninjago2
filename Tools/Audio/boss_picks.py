"""Boss Battle audio assignments (single source of truth): every SfxManager.SoundEffect 800-834 and BgmType 51-52.

Docs/BossBattle/TDD.md §2 (enum values), §8.4 (paths); GDD §4-7 (what each cue accompanies). Built by build_boss_audio.py.

An SFX variant (V) is a list of layers, mixed after each layer is decoded/rendered, onset-trimmed and peak-normalised to
the SFX ceiling (-3 dBFS) and then offset by its `gain` (dB, relative to the loudest layer):
  R(<path under the shared repo's Assets/>, ...)   sample from ~/Documents/music-cell-shared-assets (shared_repo.py)
  S(<preset>, ...)                                  synth: boss_presets.py first, then the Billiard Rogue synth_presets.py
  Roll(<paths>, count, start, end, ...)             a run of short one-shots (Lego clicks) at accelerating offsets
The mix gets optional tanh drive, a 25 Hz high-pass, silence trim, fades, and is set to the category loudness (short
LUFS = loudest 100 ms K-weighted window). A sound may push into a 1.5 ms look-ahead limiter by at most `max_gr_db` to reach
its level under the ceiling (dense impacts); all variants of a key land on the same level so round-robin never jumps.

Loudness plan: BGM -14.5 LUFS integrated (the boss brief asks for ~-14; the other Ninjago loops measure -14.9..-17.0, median
-15.3, so the boss climax sits 0.8 LU over the house median and within 0.4 LU of the loudest loop), SFX peaks <= -3 dBFS (GDD §11). Ninjago's own SFX are imported with
Normalize on (peak -> 0 dBFS) and sit at about -10..-11 short LUFS for impacts; the boss impacts reach -11 under the
-3 dBFS ceiling with the limiter, so the boss mix keeps the Ninjago SFX-over-music balance.
"""
from dataclasses import dataclass, field

UGMC = "Ultimate Game Music Collection/"
UGS = UGMC + "Short Cues/"
U = "Universal Sound FX/"
MC = U + "MONSTERS_CREATURES/"
C = "Cute UI _ Interact Sound Effects Pack/AUDIO/"
LEGO = "NEX/Sound effect/Lego/"

# Licence/source families for the manifest (all live in the team's shared asset repo).
SOURCE_LICENSES = {
    UGMC: "Ultimate Game Music Collection (royalty-free asset-store music pack, team-licensed; music-cell-shared-assets)",
    U: "Universal Sound FX (royalty-free asset-store SFX pack, team-licensed; music-cell-shared-assets)",
    C: "Cute UI & Interact Sound Effects Pack (royalty-free asset-store pack, team-licensed; music-cell-shared-assets)",
    "NEX/": "NEX team sound library (music-cell-shared-assets/Assets/NEX)",
    "synth:": "original, synthesized by Tools/Audio/boss_presets.py / synth_presets.py",
}


@dataclass
class R:
    rel: str
    gain: float = 0.0  # dB relative to the layer's own ceiling-normalised peak
    at: float = 0.0  # offset in the mix (s)
    start: float = 0.0  # cut the source (s) ...
    end: float = 0.0  # ... (0 = to the end)
    max_s: float = 0.0  # cap length with a fade
    lp: float = 0.0
    hp: float = 0.0
    semis: float = 0.0  # tape-style re-pitch (changes duration)
    from_peak_ms: float = -1.0  # >= 0: start this many ms before the loudest point
    reverse: bool = False
    fade_in_ms: float = 0.0
    fade_out_ms: float = 0.0
    trim: bool = True  # onset-trim the layer (off for windows cut out of loops, which have no silence anyway)


@dataclass
class S:
    preset: str
    gain: float = 0.0
    at: float = 0.0
    p: float = 1.0  # pitch multiplier
    seed: int = 0
    args: dict = field(default_factory=dict)  # extra preset arguments (e.g. dur)


@dataclass
class Roll:
    """`count` short one-shots from `rels` (seeded pick, +/- `jitter` semitones) between `start` and `end` s.
    Offsets follow (i/(count-1))**curve: curve < 1 accelerates (intervals shrink), 1 is even. Gain ramps gain -> gain_end."""
    rels: list
    count: int
    start: float
    end: float
    curve: float = 1.0
    gain: float = -6.0
    gain_end: float = -6.0
    jitter: float = 1.0
    seed: int = 0
    hp: float = 0.0


@dataclass
class V:
    """One output file (= one round-robin variant)."""
    layers: list
    semis: float = 0.0
    max_s: float = 0.0
    fade_ms: float = 12.0
    drive_db: float = 0.0  # tanh saturation pre-gain (denser body at the same peak)
    lp: float = 0.0  # whole-mix low-pass


@dataclass
class Loop:
    """Seamless loop: `kernels` (cycled) placed every `period_s` around a circular buffer of `length_s`, over a `bed` preset
    rendered for exactly `length_s`. No trim/fades (the buffer wraps)."""
    kernels: list
    period_s: float
    length_s: float
    bed: S = None


@dataclass
class Sfx:
    enum: int
    category: str
    variants: list  # list[V] or [Loop]
    trigger: str  # when gameplay should fire it (for packages A/B/S/U)
    notes: str = ""  # why these sources
    target: float = None  # overrides the category level (short LUFS)
    max_gr_db: float = None  # overrides the category's limiter allowance
    loop: bool = False


SFX_PEAK_CEILING_DB = -3.0  # GDD §11
BGM_TARGET_LUFS = -14.5  # brief: ~-14 LUFS; Assets/Audio/Bgm/Ninjago/*.ogg measure -14.9..-17.0 (median -15.3)
BGM_PEAK_CEILING_DB = -1.0
BGM_QUALITY = 5.0  # Vorbis q5, as every other loop

# Category -> (short LUFS target, limiter allowance dB).
CATEGORIES = {
    "impact_big": (-11.0, 6.0),  # rock impact, slam, wall smash, spike slam, explosions
    "impact": (-12.5, 5.0),  # rocket hits, vehicle hit, minion land, banner slam
    "voice": (-11.5, 3.0),  # roar, hurt, laugh, wake
    "action": (-13.5, 3.0),  # throws, launches, take-off, transform, wind, inhale, pickups
    "tell": (-14.0, 1.5),  # warnings and cues the player must react to
    "frequent": (-16.5, 3.0),  # per-row / per-bolt sounds that repeat a lot
    "soft": (-20.0, 2.0),  # deflect tinks
    "loop": (-18.5, 0.0),  # laser loop (gain only: a limiter would break the wrap)
}

LEGO_CLICKS = [LEGO + n for n in ("Lego connect.wav", "Lego connect2.wav", "Lego connect3.wav", "Lego connect4.wav",
                                  "Lego connect5.wav", "Lego connect6.wav", "Lego click.wav", "Lego click4.wav")]
STONE_DEEP = U + "IMPACTS/Stone/IMPACT_Stone_Deep_mono.wav"
HAMMER = U + "HAZARDS_TRAPS/HAZARD_Hammer_Impact_01_mono.wav"
BRICK_GROUND = U + "IMPACTS/Bricks/IMPACT_Brick_vs_Hard_Ground_RR{}_mono.wav"
BRICK_BRICK = U + "IMPACTS/Bricks/IMPACT_Brick_vs_Brick_RR{}_mono.wav"

SFX = {
    # ---------------------------------------------------------------- Phase 1: Rock Drop (GDD §4.1) ----------
    "BossRockThrow": Sfx(800, "action", [
        V([R(MC + "MONSTER_Effort_06_mono.wav", semis=-2, hp=140, max_s=0.9), R(U + "WHOOSHES/Classic/WHOOSH_Wide_Deep_Slow_mono.wav", gain=-1, at=0.1, hp=120),
           R(U + "WHOOSHES/Air/WHOOSH_Air_Fast_Wind_RR1_mono.wav", gain=-8, at=0.12), S("sub_rumble", gain=-15, args={"dur": 0.6})], max_s=1.1, fade_ms=200)],
        "At the release, end of the 0.8 s wind-up (arm above head, orange hand glow), as the rock leaves the hand.",
        "Giant effort grunt (-3 st) + deep wide whoosh: the throw reads as heavy; the whistle (801) carries the flight."),
    "BossRockWhistle": Sfx(801, "tell", [
        V([S("rock_whistle"), R(U + "WHOOSHES/Air/WHOOSH_Air_Fast_Wind_RR1_mono.wav", gain=-8)], fade_ms=120)],
        "Together with BossRockThrow: the rock is hurled up into the sky (air time 1.0 s); ~1.05 s, recedes upward.",
        "Synth rising whistle with air noise (GDD 4.1 step 1 'whistle'): rises and fades out as the rock leaves the screen.",
        target=-15.0),
    "BossRockLock": Sfx(802, "tell", [V([S("lock_beeps"), R(LEGO + "Lego connect4.wav", gain=-5)], fade_ms=30)],
        "When the reticle locks (turns magenta-red, '!' shakes). Beeps sit on the 6 Hz blink (0.167 s apart), 0.5 s.",
        "Latch click + three rising beeps F6-G6-C7: 'locked on' reads instantly and is unlike any other cue."),
    "BossRockImpact": Sfx(803, "impact_big", [
        V([R(U + "DEMOLISH/DEMOLISH_Stone_01_mono.wav", max_s=1.2), R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Debris_Rumble_mono.wav", gain=-5, max_s=1.2),
           R(STONE_DEEP, gain=-1), R(BRICK_GROUND.format(1), gain=-6, at=0.02), S("sub_rumble", gain=-6, args={"dur": 0.9})], max_s=1.3, fade_ms=350),
        V([R(U + "DEMOLISH/DEMOLISH_Stone_04_mono.wav", max_s=1.0), R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Fast_with_Rock_Debris_mono.wav", gain=-5, max_s=1.2),
           R(STONE_DEEP, gain=-1, semis=-1), R(BRICK_GROUND.format(4), gain=-6, at=0.02), S("sub_rumble", gain=-6, seed=1, args={"dur": 0.9})],
          max_s=1.3, fade_ms=350)],
        "When the rock lands (end of the 0.35 s fall), with the crater dust and shake.",
        "Stone demolish crack + debris rumble + deep stone thud + sub: heavy, instant (impact on the visual frame)."),
    # ---------------------------------------------------------------- Boss voice ------------------------------
    "BossRoar": Sfx(804, "voice", [
        V([R(MC + "MONSTER_Roar_02_mono.wav", semis=-2), R(U + "ALIENS/ALIEN_Attack_Roar_07_mono.wav", semis=-5, gain=-7),
           R(U + "DEMOLISH/DEMOLISH_Stone_02_mono.wav", gain=-13, at=0.05), S("sub_rumble", gain=-8, args={"dur": 1.8})], max_s=2.2, fade_ms=500),
        V([R(MC + "MONSTER_Roar_01_mono.wav", semis=-2, max_s=2.3), R(U + "ALIENS/ALIEN_Attack_Roar_09_mono.wav", semis=-5, gain=-7, at=0.15),
           R(U + "DEMOLISH/DEMOLISH_Stone_04_mono.wav", gain=-13, at=0.3), S("sub_rumble", gain=-8, seed=1, args={"dur": 2.0})], max_s=2.4, fade_ms=500)],
        "Ready beat (after 3-2-1), transform cutscene (0.8-2.0 s, with the BossAppear stinger), wake (with BossWake). Duck music.",
        "Deep monster roar (-2 st) for the body, alien roar (-5 st) for the bite, stone grind under it (a brick titan), sub weight."),
    # ---------------------------------------------------------------- Phase 1: Ground Slam (GDD §4.3) --------
    "BossGroundSlam": Sfx(805, "impact_big", [
        V([R(HAMMER, max_s=1.4), R(STONE_DEEP, gain=0), R(U + "DEMOLISH/DEMOLISH_Stone_02_mono.wav", gain=-3),
           R(U + "EXPLOSIONS/Long/EXPLOSION_Long_Distant_Impact_Rumble_mono.wav", gain=-6, max_s=1.6), S("sub_rumble", gain=-9, args={"dur": 1.4})],
          max_s=1.6, fade_ms=450),
        V([R(HAMMER, semis=-1, max_s=1.4), R(U + "IMPACTS/Concrete/IMPACT_Concrete_Slab_on_Concrete_Slab_Deep_mono.wav", gain=0),
           R(U + "DEMOLISH/DEMOLISH_Stone_03_mono.wav", gain=-4), R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Debris_Rumble_mono.wav", gain=-5, max_s=1.4),
           S("sub_rumble", gain=-9, seed=1, args={"dur": 1.4})], max_s=1.6, fade_ms=450)],
        "On the slam (both fists hit the ground after the 1.0 s red wind-up), with the shockwave ring and shake.",
        "Hammer-impact boom (209 Hz centroid) + deep stone + rumble tail: the biggest ground hit in the mode."),
    "BossBrickGrow": Sfx(806, "frequent", [
        V([Roll(LEGO_CLICKS, 5, 0.0, 0.2, curve=0.8, gain=-7, gain_end=-3, seed=s), S("brick_rise", gain=-9, p=p),
           R(BRICK_GROUND.format(g), gain=-2, at=0.24, max_s=0.32)], max_s=0.58, fade_ms=90) for s, p, g in ((0, 1.0, 1), (1, 1.06, 4), (2, 0.94, 6))],
        "When a brick row starts growing out of the road (0.3 s grow, after its crack glowed). One per row, not per brick.",
        "Fast Lego-click stack + a retro rising blip, landing on a real brick knock at 0.24 s: brick-built, readable, short."),
    # ---------------------------------------------------------------- Phase 1: Minion Throw (GDD §4.2) -------
    "BossMinionThrow": Sfx(807, "action", [
        V([R(U + "WHOOSHES/Air/WHOOSH_Air_Fast_Wind_RR1_mono.wav", gain=-2), R(MC + "MONSTER_Grunt_Short_02_mono.wav", semis=-3, gain=-3),
           R(U + "VOICES/Exclamations/EXCLAMATION_Male_B_Wooah_01_mono.wav", semis=6, gain=-2, at=0.12),
           R(U + "WHOOSHES/Classic/WHOOSH_Wide_Deep_Slow_mono.wav", gain=-6, at=0.05)], max_s=1.0, fade_ms=200)],
        "When the Titan releases the minion (end of the wind-up); the minion's 'wooah!' rides the start of the arc.",
        "Titan grunt + throw whoosh + a small squeaky 'wooah!' (+6 st) for the flying skeleton minion: funny, not scary."),
    "BossMinionLand": Sfx(808, "impact", [
        V([R(U + "IMPACTS/Metal/IMPACT_Metal_Soft_Plate_01_mono.wav"), R(LEGO + "Lego collide3.wav", gain=-4, max_s=0.45),
           R(U + "IMPACTS/Punch/IMPACT_Punch_03_mono.wav", gain=-5), R(U + "VOICES/Exclamations/EXCLAMATION_Male_B_Yah_mono.wav", semis=6, gain=-6, at=0.06)],
          max_s=0.7, fade_ms=150)],
        "When the minion lands and clings on the car roof corner.",
        "Thunk on the car's roof plate + brick rattle + a tiny 'yah!': the minion is now ON the car."),
    "BossMinionHit": Sfx(809, "action", [
        V([R(U + "IMPACTS/Metal/IMPACT_Metal_Hit_Sheet_Metal_RR1_mono.wav", max_s=0.4), R(U + "IMPACTS/Punch/IMPACT_Punch_03_mono.wav", gain=-3),
           R(LEGO_CLICKS[3], gain=-7)], max_s=0.45, fade_ms=120),
        V([R(U + "IMPACTS/Metal/IMPACT_Metal_Medium_Hollow_Single_mono.wav", max_s=0.4), R(U + "IMPACTS/Metal/IMPACT_Metal_Hit_Sheet_Metal_RR2_mono.wav", gain=-4),
           R(U + "IMPACTS/Punch/IMPACT_Punch_07_mono.wav", gain=-4), R(LEGO_CLICKS[0], gain=-8)], max_s=0.45, fade_ms=120)],
        "Each minion damage tick (the minion whacks the car; 1 heart). BossVehicleHit plays too if the arena's hit path fires it.",
        "Metal whack on the roof + punch body + brick click: a sharp, short 'bonk' that says the minion is hurting the car.",
        target=-13.5, max_gr_db=6.0),
    "BossWallGrow": Sfx(810, "action", [
        V([R(U + "FRICTION/Bricks/FRICTION_Brick_Slide_03_mono.wav", lp=4000, gain=-5, max_s=0.65),
           Roll(LEGO_CLICKS, 10, 0.0, 0.55, curve=1.0, gain=-8, gain_end=-3, seed=3), S("sub_rumble", gain=-9, args={"dur": 0.7}),
           R(BRICK_BRICK.format(1), gain=-2, at=0.58)], fade_ms=120)],
        "When the side wall starts rising along the road edge (0.6 s rise).",
        "Brick grind + ten Lego clicks stacking up + a final brick knock at 0.58 s when the wall tops out."),
    "BossWallSmash": Sfx(811, "impact_big", [
        V([R(U + "DEMOLISH/DEMOLISH_Short_04_mono.wav"), R(BRICK_BRICK.format(3), gain=-2), R(LEGO + "Lego collide2.wav", gain=-3, at=0.04),
           R(LEGO + "Lego collide4.wav", gain=-6, at=0.15), S("boss_thud", gain=-4)], max_s=1.2, fade_ms=300),
        V([R(U + "DEMOLISH/DEMOLISH_Short_01_mono.wav"), R(BRICK_BRICK.format(7), gain=-2), R(LEGO + "Lego collide.wav", gain=-3, at=0.04),
           R(LEGO + "Lego collide3.wav", gain=-6, at=0.15), S("boss_thud", gain=-4, p=0.92)], max_s=1.2, fade_ms=300)],
        "When the car smashes the wall (wall bursts into bricks, minion scraped off). The success moment of the minion beat.",
        "Demolish crash + brick knock + a shower of Lego bricks: satisfying and toy-like."),
    # ---------------------------------------------------------------- Phase 1: Power-up / rockets (GDD §4.4) --
    "BossPowerUpSpawn": Sfx(812, "tell", [
        V([S("sparkle"), R(U + "8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Appear_Fading_mono.wav", gain=-8, max_s=0.8),
           R(C + "Powerup/SFX_Powerup_Crystal_1.wav", gain=-7, max_s=0.8)], fade_ms=200)],
        "When the rocket crate appears ahead ('Grab the rockets!').",
        "Bright rising C-major sparkle + crystal shimmer: positive, inviting, clearly not a hazard.",
        target=-15.0),
    "BossPowerUpPickup": Sfx(813, "action", [
        V([S("power_up"), R(C + "Powerup/SFX_Powerup_Bright_1.wav", gain=-5, max_s=1.0),
           R(U + "MECHANICS/MECHANICS_Metal_Mechanism_13_mono.wav", gain=-5, at=0.26, max_s=0.4)], max_s=1.0, fade_ms=250)],
        "When the car picks the crate up (before the volley launches).",
        "Fast rising arpeggio + metal lock-and-load clunk at 0.28 s: 'rockets armed'.", target=-13.0),
    "BossRocketLaunch": Sfx(814, "action", [
        V([R(U + "WEAPONS/Rockets/ROCKET_Launch_Deep_Short_mono.wav"), R(U + "WEAPONS/Rockets/ROCKET_Launch_Bright_mono.wav", gain=-7, hp=1500, max_s=0.6),
           S("blast_crack", gain=-9, seed=i), S("sub_thump", gain=-7)], semis=s, max_s=0.9, fade_ms=200) for i, s in ((0, 0.0), (1, 1.5), (2, -1.5))],
        "Per rocket leaving the car roof (stagger 0.15 s; 3 pitch variants so a volley does not phase).",
        "Deep rocket launch + bright exhaust hiss + a small pop at t=0 for the instant of launch."),
    "BossRocketHit": Sfx(815, "impact", [
        V([R(U + "EXPLOSIONS/Quick/EXPLOSION_Quick_02_mono.wav", max_s=0.9), R(U + "EXPLOSIONS/Arcade/EXPLOSION_Arcade_12_mono.wav", gain=-3),
           R(LEGO + "Lego collide.wav", gain=-9, at=0.06, max_s=0.5), S("boss_thud", gain=-4)], max_s=1.0, fade_ms=300),
        V([R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Kickback_Debris_mono.wav", max_s=0.9), R(U + "EXPLOSIONS/Arcade/EXPLOSION_Arcade_07_mono.wav", gain=-3),
           R(LEGO + "Lego collide3.wav", gain=-9, at=0.06, max_s=0.5), S("boss_thud", gain=-4, p=0.92)], max_s=1.0, fade_ms=300),
        V([R(U + "EXPLOSIONS/Quick/EXPLOSION_Quick_Debris_05_mono.wav", max_s=0.9), R(U + "EXPLOSIONS/Arcade/EXPLOSION_Arcade_12_mono.wav", gain=-4, semis=-2),
           R(LEGO + "Lego collide4.wav", gain=-9, at=0.06, max_s=0.5), S("boss_thud", gain=-4, p=1.08)], max_s=1.0, fade_ms=300)],
        "Per rocket hitting the boss's chest (with hit-stop and flinch). Also the right sound for the finale's chained "
        "ExplodeSmall bursts (short, chain-safe).",
        "Punchy explosion with an arcade transient on frame 0, brick debris rattle and a boss-mass thud; 3 RR variants."),
    "BossHurt": Sfx(816, "voice", [
        V([R(MC + "MONSTER_Pain_01_mono.wav", semis=-3), R(U + "BREAKS_SNAPS/BREAK_Deep_Crack_01_mono.wav", gain=-7), S("sub_rumble", gain=-9, args={"dur": 0.6})],
          max_s=1.2, fade_ms=250),
        V([R(MC + "MONSTER_Grunt_Deep_Hurt_mono.wav", semis=-3), R(U + "BREAKS_SNAPS/CRACK_Deep_Medium_04_mono.wav", gain=-7),
           S("sub_rumble", gain=-9, seed=1, args={"dur": 0.6})], max_s=1.2, fade_ms=250)],
        "Boss flinch after a volley lands (once per volley, after the rocket hits), and on big laser damage if wanted.",
        "Pained monster groan (-3 st) + a stone crack in the Titan's bricks: 'it hurts' and 'it is breaking'."),
    # ---------------------------------------------------------------- Transform cutscene (GDD §5) -----------
    "BossTakeOff": Sfx(817, "action", [
        V([R(U + "WING_FLAPS/WING_FLAP_Large_01_RR1_mono.wav"), R(U + "WING_FLAPS/WING_FLAP_Large_02_RR1_mono.wav", gain=-3, at=0.45),
           S("jet_whine", gain=-4, at=0.15), R(U + "THRUSTERS_FLAMES/THRUSTER_Afterburner_Power_Distorted_loop_mono.wav", gain=-3, at=0.35, fade_in_ms=200),
           R(U + "WEAPONS/Rockets/ROCKET_Launch_Deep_Slow_mono.wav", gain=-5, at=0.3)], max_s=2.0, fade_ms=500)],
        "Transform cutscene 2.0 s: the Titan leaps, wings unfold (flap at 0 and 0.45 s), jets ignite (from ~0.35 s).",
        "Big wing flaps + rising turbine whine + afterburner roar: the Titan becomes a flying boss.", target=-12.5),
    "BossTransform": Sfx(818, "action", [
        V([R(U + "EXPLOSIONS/Quick/EXPLOSION_Quick_Debris_01_mono.wav", max_s=0.9, gain=-1), R(LEGO + "Lego collide2.wav", gain=-1, at=0.03),
           R(LEGO + "Lego collide4.wav", gain=-4, at=0.2), R(U + "CHARGE_UPS_DOWNS/CHARGE_Complex_Wet_12_Semi_Up_2000ms_mono.wav", gain=-9, at=0.45),
           Roll(LEGO_CLICKS, 22, 0.6, 2.2, curve=0.7, gain=-9, gain_end=-2, jitter=1.5, seed=7),
           R(LEGO + "Lego connect4.wav", at=2.3), R(U + "IMPACTS/Metal/IMPACT_Metal_Soft_Plate_01_mono.wav", gain=-3, at=2.3),
           S("rebuild_chime", gain=-6, at=2.3)], max_s=3.1, fade_ms=500)],
        "Transform cutscene 3.2 s: the car bursts into bricks (t=0), rebuilds into the ninja jet (clicks 0.6-2.2 s, faster and "
        "louder), snaps together at 2.3 s (= cutscene 5.5 s, just before lift-off).",
        "Brick burst + Lego shower, a riser and an accelerating Lego-click rebuild ending in a snap + chime: toy-like assembly.",
        target=-12.5),
    # ---------------------------------------------------------------- Phase 2: Devil Wind / Laser (GDD §6) ----
    "BossDevilWind": Sfx(819, "action", [
        V([R(U + f"WIND/WIND_Storm_Blowing_Howling_0{w}_loop_mono.wav", start=a, end=a + 1.8, fade_in_ms=120, fade_out_ms=500, trim=False),
           R(U + "WHOOSHES/Classic/WHOOSH_Wide_Deep_Slow_mono.wav", gain=-3), R(MC + "MONSTER_Breath_06_mono.wav", semis=-4, gain=-7)], fade_ms=300)
        for w, a in ((4, 1.0), (2, 2.0))],
        "When the Titan blows a gust barrage (after the 0.8 s violet wind-up); once per barrage row is fine (~1.8 s).",
        "Howling storm gust + deep whoosh + the Titan's huge breath (-4 st): wind that comes from the boss.", target=-13.0),
    "BossTired": Sfx(820, "action", [
        V([R(MC + "MONSTER_Exhausted_05_mono.wav", semis=-3, max_s=2.0), R(U + "CARTOON/CARTOON_Slide_Down_01_mono.wav", gain=-9, at=0.25)],
          max_s=2.2, fade_ms=500)],
        "When the Titan slumps after a barrage (Zzz, 'It's tired - FIRE!').",
        "Exhausted monster pant (-3 st) + a falling slide whistle: comic 'out of breath' cue that invites the attack.",
        target=-14.0),
    "BossLaserLoop": Sfx(821, "loop", [
        Loop([V([S("laser_pew", p=p, seed=i), R(U + "WEAPONS/SciFi/Blasters_Simple/BLASTER_Very_Short_mono.wav", gain=-10)])
              for i, p in enumerate((1.0, 0.94, 1.03, 0.97))], period_s=0.125, length_s=1.0, bed=S("laser_hum", gain=-9))],
        "SfxManager.StartLoop while the plane auto-fires (8 bolts/s = config laser.fireInterval 0.125 s); StopLoop on wake. "
        "Exactly 1.000 s = 8 bolts; start it on the first bolt so the pews line up with the visuals.",
        "Retro 'pew' bolts (4 pitch steps, cycled) + a sampled blaster transient over an 8 Hz-pulsed energy hum; seamless.",
        loop=True),
    "BossLaserHit": Sfx(822, "frequent", [
        V([R(U + f"IMPACTS/Energy/IMPACT_Energy_Solid_{n:02d}_mono.wav", semis=s, max_s=0.25), S("zap", gain=-5, seed=i)], max_s=0.3, fade_ms=80)
        for i, (n, s) in enumerate(((1, 0.0), (4, 0.0), (1, 2.5)))],
        "When a bolt strikes the Core (on target). Can fire up to 8/s; SfxManager's 35 ms throttle is fine.",
        "Short energy impact + crushed zap: crunchy 'damage' confirmation, 0.3 s max so 8/s never smears.", target=-17.0),
    "BossWake": Sfx(823, "voice", [
        V([S("whoom"), R(MC + "MONSTER_Growl_Long_02_mono.wav", semis=-3, max_s=1.2, gain=-2),
           R(U + "FORCE_FIELDS/FORCE_FIELD_Scifi_Pulse_01_mono.wav", gain=-7), S("sub_rumble", gain=-7, args={"dur": 1.0})], max_s=1.4, fade_ms=350)],
        "When the Titan wakes at a notch (70/30/5 %): shockwave, laser stops. Pair with BossRoar if the roar is wanted too.",
        "Downward shockwave sweep + angry growl (-3 st) + energy pulse: the wake reads as a blast, not just a voice."),
    # ---------------------------------------------------------------- Phase 2: Boss Clap (GDD §6.3) ----------
    "BossClapInhale": Sfx(824, "action", [
        V([S("inhale"), R(U + "WHOOSHES/Mixed/WHOOSH_Breath_mono.wav", reverse=True, gain=-5, at=0.30)], max_s=0.9, fade_ms=10)],
        "At the start of the clap inhale (0.8 s, white palms); the file is 0.80 s and stops dead where the blow (push) starts.",
        "Rising band-passed air swell + reversed breath: a huge inhale that cuts off right before the blow.", target=-14.0),
    "BossClapWarning": Sfx(825, "tell", [V([S("dive_klaxon")], fade_ms=40)],
        "When the 'DIVE!' warning starts (vertical control returns; 1.8 s normal / 2.2 s large). One shot, 0.95 s.",
        "Two submarine 'ah-OO-gah' dive klaxon calls: literally 'DIVE!', the most urgent cue in the mode.", target=-13.0),
    "BossSpikeSlam": Sfx(826, "impact_big", [
        V([R(HAMMER, max_s=1.2), R(U + "DEMOLISH/DEMOLISH_Metal_02_mono.wav", gain=-1), R(U + "HAZARDS_TRAPS/TRAP_Close_01_mono.wav", gain=-3),
           R(U + "IMPACTS/Metal/IMPACT_Metal_Hit_Large_Metal_01_mono.wav", gain=-5), S("sub_rumble", gain=-6, args={"dur": 1.0})], max_s=1.3, fade_ms=400)],
        "When the spiked walls smash together (0.22 s slam).",
        "Hammer boom + metal demolish + trap snap: the heaviest metallic hit (2-heart hazard)."),
    # ---------------------------------------------------------------- Finale / knockout (GDD §7) -------------
    "BossExplode": Sfx(827, "impact_big", [
        V([R(U + "EXPLOSIONS/Long/EXPLOSION_Long_Bright_Debris_Tail_mono.wav", max_s=2.6),
           R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Burst_with_Falling_Debris_mono.wav", gain=-4, at=0.12, max_s=2.4),
           R(LEGO + "Lego collide2.wav", gain=-6, at=0.35), R(LEGO + "Lego collide4.wav", gain=-6, at=0.7), R(LEGO + "Lego collide.wav", gain=-8, at=1.05),
           S("sub_rumble", gain=-6, args={"dur": 2.4}), S("power_boom", gain=-4)], max_s=3.0, fade_ms=900),
        V([R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Smash_Debris_mono.wav", max_s=2.2), R(U + "EXPLOSIONS/Quick/EXPLOSION_Quick_01_mono.wav", gain=-3),
           R(LEGO + "Lego collide3.wav", gain=-6, at=0.3), R(LEGO + "Lego collide2.wav", gain=-7, at=0.65), R(LEGO + "Lego collide4.wav", gain=-8, at=1.0),
           S("sub_rumble", gain=-7, seed=1, args={"dur": 2.4}), S("power_boom", gain=-4, p=0.9, seed=1)], max_s=3.0, fade_ms=900)],
        "Finale ExplodeBig (the final burst: parts fly apart into a brick shower), and the knockout vehicle burst if wanted. "
        "Use BossRocketHit for the chained ExplodeSmall bursts.",
        "Long bright explosion with falling debris and a 1 s Lego brick shower: the Titan comes apart into bricks."),
    "BossVehicleHit": Sfx(828, "impact", [
        V([R(U + f"IMPACTS/Generic/IMPACT_Generic_{g:02d}_mono.wav", max_s=0.4), R(LEGO + f"Lego collide{c}.wav", gain=-5, max_s=0.35),
           S("bwomp", gain=-7, p=p)], max_s=0.6, fade_ms=150) for g, c, p in ((5, "3", 1.0), (7, "", 0.92), (1, "4", 1.07))],
        "Every hazard hit on the car/jet (brick, rock, minion tick, wind, spike), with the i-frame blink.",
        "Plastic-and-metal crunch + brick rattle + a falling 'bwomp': clearly 'you got hit', distinct from boss hits."),
    "BossKnockout": Sfx(829, "impact", [
        V([R(U + "EXPLOSIONS/Medium/EXPLOSION_Medium_Smash_Debris_mono.wav", max_s=1.4), R(LEGO + "Lego collide2.wav", gain=-2, at=0.05),
           R(LEGO + "Lego collide4.wav", gain=-4, at=0.3), S("sad_horn", gain=-3, at=0.35)], max_s=2.4, fade_ms=500)],
        "Hearts reach 0: the vehicle tumbles and bursts (with hit-stop / slow-mo); then the Defeat stinger and BossLaugh.",
        "Crash into a pile of bricks + a cartoon 'wah wah wah waaah': a soft, kid-friendly fail."),
    "BossBannerSlam": Sfx(830, "impact", [
        V([S("taiko"), R(STONE_DEEP, gain=-3), R(UGS + "Scares/Large Cymbal SCARE.wav", gain=-10, max_s=1.0, from_peak_ms=5),
           R(LEGO + "Lego connect4.wav", gain=-6)], max_s=1.1, fade_ms=350)],
        "When a banner slams in (BOSS name, PHASE 1/2, WAKE, VICTORY!, KNOCKED OUT), on the frame it lands.",
        "Taiko drum + stone thud + cymbal shimmer + brick snap: a dramatic ninja 'BOOM' under the banner."),
    "BossLaserDeflect": Sfx(831, "soft", [
        V([S("tink", p=p, seed=i), R(U + "IMPACTS/Bullets/IMPACT_Bullet_Metal_01_mono.wav", gain=-8, max_s=0.2)], max_s=0.25, fade_ms=60)
        for i, p in enumerate((1.0, 1.12, 0.9))],
        "When a bolt misses and glances off the armour ('tink', no damage). Up to 8/s; keep it quiet.",
        "Small inharmonic metal tink: audible feedback that the shot missed, never louder than a hit."),
    "BossCountdown": Sfx(832, "tell", [V([S("countdown_beep"), R(LEGO + "Lego connect4.wav", gain=-10)], fade_ms=40)],
        "On each Ready digit 3, 2, 1 (one shot per digit, 0.3 s); BossRoar acts as the 'GO'.",
        "A5 arcade beep over a low brick-drum knock: crisp, the same pitch for each digit.", target=-15.0),
    "BossLaugh": Sfx(833, "voice", [
        V([R(U + "VOICES/Laugh/VOICE_Male_Hahaha_Psycho_04_mono.wav", semis=-6), R(MC + "MONSTER_Growl_Deep_03_Long_mono.wav", semis=-2, gain=-11, max_s=1.8),
           S("sub_rumble", gain=-11, args={"dur": 1.6})], max_s=2.4, fade_ms=500)],
        "Knockout: the Titan laughs (after the crash, ~1 s into the 3 s knockout).",
        "A villain 'ha-ha-ha' pitched down 6 st with a growl under it: big and goofy-menacing, kid-safe.", target=-12.0),
    "BossCrackGlow": Sfx(834, "frequent", [
        V([R(U + f"BREAKS_SNAPS/{n}_mono.wav", semis=s, max_s=0.5), S("glow_sizzle", gain=-4, seed=i)], fade_ms=60)
        for i, (n, s) in enumerate((("CRACK_Deep_Medium_04", 0.0), ("BREAK_Crack_03", -1.0), ("CRACK_Deep_Medium_04", -2.0)))],
        "When a row's crack line starts glowing (0.45 s before its bricks grow). One per row.",
        "Stone crack + a swelling, trembling hot hum: warns 'something is coming out of the ground here'."),
}

ORDER = sorted(SFX, key=lambda k: SFX[k].enum)
assert [SFX[k].enum for k in ORDER] == list(range(800, 835)), [SFX[k].enum for k in ORDER]


@dataclass
class Bgm:
    enum: int
    rel: str
    notes: str


BGM = {
    "BossBattleRoad": Bgm(51, UGMC + "Combat/Boss Battle 2 Loop.wav",
                          "Phase 1 road (dusk). ~150 BPM, steady 8th-note drive for the whole loop (LRA 4.4, centroid 305 Hz), "
                          "83.2 s = 52 bars, perfectly continuous wrap. Not used by any other mode (fingerprinted against every "
                          "Assets/Audio/Bgm loop; Boss Battle 1 is BilliardRogue's Boss)."),
    "BossBattleSky": Bgm(52, UGMC + "Combat/Boss Battle 5 Loop.wav",
                         "Phase 2 sky (storm night). Same series and key centre as the road loop (smooth transform crossfade), "
                         "darker (centroid 190 vs 305 Hz, 83 % of its energy under 200 Hz) and busier (4.6 vs 3.8 onsets/s); 80.0 s "
                         "= 40 bars with a built-in swell back into the top; perfectly continuous wrap. Unused elsewhere."),
}
