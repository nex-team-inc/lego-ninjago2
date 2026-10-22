# Boss Battle — Game Design Document (v2, after design review)

Mode `GameModeType.BossBattle = 15`, menu name **Boss Battle** (kept apart from the chapter mode "Boss Rush"). Boss: the
**Brick Titan**. Android (Nex Playground), camera body tracking, 1–2 players sharing one vehicle. Session: 4–5 min for a clean
win. Technical design: `TDD.md`. Research: `research/*.md`.

## 1. Pillars
1. **The boss causes every threat.** No random pop-ups: each obstacle comes from a visible boss action with a readable wind-up
   (pose + colour tell on the acting hand/mouth + attack icon over its head).
2. **Lean is the only control.** Chest lean left/right steers; chest rise/drop climbs/dives (plane only) — same rule as Chest Chase.
3. **Always a next beat.** Attack → short breather → reward. The boss bar always moves when the player does the right thing.
4. **Readable at a glance (HD-2D diorama).** Big silhouettes, warm dusk then cold night, hazards are the brightest things on screen.
5. **Fair for kids.** Every timing is checked against the steering model (SmoothDamp 0.35 s, 7 m/s cap) plus ~0.6 s choice
   reaction, ~0.4 s body onset, ~0.1 s tracking latency. No overlapping attack may create an unavoidable hit.

## 2. Controls and indicators
| Input | Car (Phase 1) | Plane (Phase 2) |
|---|---|---|
| Lean left/right | 3 lanes, x −2.2…+2.2 (walled side reach 2.45) | 3 columns |
| Chest rise/drop | — | 3 rows, y 1.6…4.8 (centre 3.2) |
| 2 players | both chests blend into one steer (SharedSteer 50/50) | same |
- **Who is controlling:** P1/P2 triangles over each player's head in the corner camera preview (PiP); the player steering the
  most is highlighted (filtered: a player is promoted after leading by ≥ 0.2 share for ≥ 0.6 s; neutral when nobody steers).
  Screen-space P1/P2 tags over the vehicle show the same state. 1P: P1 highlighted all the time.

## 3. Structure, view stack and pacing
```
depth 1  BossBattleStartView        Continue (if save) / New battle / Difficulty ; Back = leave mode
depth 2  BossSetupView (calibration: PreviewsManager + SetupStateManager) ; Back = return to start view
depth 2  BossIntroView   → cutscene ~5 s (first run; "Ready" 3-2-1 on continue)
depth 2  BossRoadView    → PHASE 1 ROAD (~100 s)
depth 2  BossTransformView → cutscene ~6.5 s
depth 2  BossSkyView     → PHASE 2 SKY (~95 s)
depth 2  BossFinaleView  → cutscene ~6 s        | BossKnockoutView → ~3 s (hearts 0)
depth 2  BossBattleResultView  Continue from checkpoint / Retry / Menu ; Back = Menu (→ start view)
depth 3  BossPauseView (Back in any phase/cutscene view; Resume / Quit) ; tracking-lost re-calibration (setup pushed)
```
Every step is a view, including the no-UI cutscene steps. Retry/Continue replace the result view with a new setup view.
The world, camera rig and HUD exist from the first gameplay step and are hidden during start/setup/result.

Flow curve: intro spectacle → phase 1 teaches one attack at a time (steer check, slam, crate, rock, minion) then mixes → peak
at the 5th crate → transform release → phase 2 teaches rise/duck, then the laser verb → three wake-ups ramp tension → finale.

## 4. Phase 1 — Road (car)
Road scrolls toward the car (car fixed at z = 0, lanes x −2/0/+2). The Titan (~11 m) walks backwards at **z ≈ 30 m** facing
the car, always at the same distance ("moving with the car"), swaying. Dusk canyon, sun low and slightly to the side
(sun euler (32, 215, 0)), god rays through ruined arches, embers. A blob shadow under the boss keeps it grounded.

**Ready beat** (3 s, every phase start incl. continue): 3-2-1, roar, hearts refill animation.
**Beat 1 – steer check:** "Lean to steer!" until the car has reached x ≤ −1.6 and x ≥ +1.6 once (max 8 s).

### 4.1 Rock Drop
1. Wind-up 0.8 s (arm above head, **orange** glow in the hand, rock icon over the head), rock hurled up into the sky (whistle).
2. After `airSeconds` 1.0 s a **reticle** appears on the road around the car **and a "!" warning above the car** (screen-space);
   both follow the car's x (smoothing 0.25 s) for `followSeconds` 1.6 s.
3. **Lock**: reticle turns magenta-red (HDR 2.5) and blinks at 6 Hz, the "!" turns red and shakes; it stops following.
   A dark rock shadow grows inside the ring. `lockSeconds` 1.2 s (**first rock 1.6 s**), then 0.35 s fall.
4. Ring drawn at the rock radius 0.55 m; hit when |carX − ringX| < 0.55 + 0.7 (car half width) = 1.25 → "ring touches car =
   hit". 1 heart, crater dust, shake.
5. Pairs/triples: the next rock locks ≥ 1.5 s after the previous impact; never two locked reticles at once.

### 4.2 Minion Throw (walls)
1. Wind-up 0.9 s (**first 1.2 s**; **green** hand glow, minion icon), a **minion** (skeleton warrior) arcs onto the car and
   clings **on top of the car at the left or right roof corner**, flailing.
2. A **brick wall grows** from the floor along that road edge (0.6 s), big arrow + "Lean LEFT to smash!"; at grace/2 the
   prompt escalates (leaning-body pictogram, PiP indicator pulse).
3. Damage over time: grace then 1 heart per tick. **First minion slower**: arc 1.6 s (vs 1.0), grace 4 s (vs 2.5), tick 3.5 s
   (vs 2.5), max 1 tick (vs 2). After the max ticks the minion jumps off by itself, the wall sinks (result "failed") — the
   battle never stalls.
4. On a walled side the car's reach grows 2.2 → 2.45 so its body meets the wall. **Smash** when the car is at x ≥ 2.0 on that
   side moving outward (> 0.5 m/s) after the wall finished rising: wall bursts into bricks, minion scraped off and flung away.
   2P: if either player's own lean reaches ≥ 0.82 toward that side, the car is driven into the wall for 0.25 s and smashes.
5. "Both" variant: one minion per side; 3.5 s grace each, the second side's grace starts when the first clears or 3 s after landing.

### 4.3 Ground Slam (bricks from the floor)
1. Wind-up 1.0 s (both fists raised, **red** glow, slam icon), **slam** at its feet: shockwave ring, rumble, shake.
2. A glowing **crack line** runs toward the car; per row, cracks glow 0.45 s, then **bricks grow up** from the floor (0.3 s)
   finishing at z = 21 m (1.5 s before reaching the car). Rows are 2-lane barriers (masks 110/101/011).
3. `count` rows per slam with `rowSpacingSeconds`: first slam 1.9 s (fixed masks 101 then 110/011), mid slams 1.7 s, the late
   "faster" slam 1.5 s. Under 1.8 s spacing, no 011↔110 back to back (max lane jump 1). Seeded random.
4. Hit → 1 heart + bump.

### 4.4 Power-up → Rockets
- A glowing **rocket crate** spawns in a free lane ahead between attacks ("Grab the rockets!"); **crate #1 spawns in the lane
  next to the car**. Lead 0.3 s after the previous beat.
- Pickup fires a **volley of homing rockets** from the car roof (stagger 0.15 s, flight 1.0 s) that always hit the boss's chest:
  hit-stop 0.04 s, boss flinch, "×1" / "×2 DMG" popups, white delayed damage trail on the bar, +1 heart (up to max).
- Volley table (designer list): pickup 1 → 1 rocket ×1; pickup 2 → 2 rockets ×2 (double damage); pickups 3–5 → 3 rockets ×2.
  Phase-1 HP = the table's sum, so the **5th pickup always empties the bar** → Phase 2. Bar after each pickup:
  100 → 96 → 78 → 52 → 26 → 0 %. Below 30 % the Titan is **enraged** (red glow, wind-ups ×0.85 — never telegraph, lock,
  warning or spacing times). *Reading of "5th rocket": 5th power-up pickup (matches "pick up" and the 5 HUD icons); the
  alternative "5th rocket fired" would end phase 1 during the 3rd volley.*
- A missed crate returns right after the next attack.

### 4.5 Phase-1 beat script (default; designer list, `loopFrom` 0-based = 9)
| # | Beat | Expected s |
|---|---|---|
| 0 | Ready | 3 |
| 1 | Steer check | 4 |
| 2 | Ground Slam ×2 (spacing 1.9) | 7 |
| 3 | Power-up (adjacent lane) | 4 |
| 4 | Rock Drop ×1 (slow lock) | 6 |
| 5 | Minion Left (first, slow) | 8 |
| 6 | Power-up | 4 |
| 7 | Ground Slam ×3 (1.7) | 8 |
| 8 | Rock Drop ×2 | 9 |
| 9 | Power-up | 4 |
| 10 | Minion Right | 6 |
| 11 | Ground Slam ×3 (1.7) + Rock ×1 overlapped — rock impact placed midway between two row arrivals, ≥ 0.8 s from each | 8 |
| 12 | Power-up | 4 |
| 13 | Rock Drop ×3 | 11 |
| 14 | Minion Both | 8 |
| 15 | Ground Slam ×4 (1.5) | 9 |
| 16 | Power-up → 5th → Phase 2 | 4 |
Breather 1.2 s after every beat (≈ 20 s total) → ≈ 125 s with ready/steer; ≈ 100 s of play. Script ends without 5 pickups →
loop from index 9.

## 5. Transform cutscene (~6.5 s, letterboxed, HUD bars hidden, PiP kept)
| t | Shot |
|---|---|
| 0–0.8 | 5th volley lands, slow-mo 0.35×, flash, bar empties |
| 0.8–2.0 | cut to boss close shot: stagger, roar (BossAppear stinger) |
| 2.0–3.2 | Titan leaps, wings unfold, jets ignite; camera cranes **up** (translation only) |
| 3.2–4.4 | look blends dusk → night (2 s), lightning; cut to car: it bursts into bricks |
| 4.4–5.6 | bricks rebuild into the **ninja jet** |
| 5.6–6.5 | jet lifts off after the boss; "PHASE 2" banner; bar refills purple with notches 70/30/5; control returns for the last 1.5 s |
Camera moves are translation dollies/cranes plus cuts; rotation ≤ 10°/s (no pixel crawl). Seen once → OK/Back skips next time.

## 6. Phase 2 — Sky (plane)
Dark storm arena: black-violet sky, moon shafts through clouds, floating ruins, lightning. Plane grid x −2.2…+2.2, y 1.6…4.8,
plane half extents (0.6, 0.25). The Titan hovers far ahead (z ≈ 30 m).
**Ready beat**, then **beat 0 – rise and duck**: "Stand tall to climb, crouch to dive!" until the plane has reached y ≥ 4.2 and
y ≤ 2.2 once (max 8 s).

### 6.1 Devil Wind
Wind-up 0.8 s (cheeks puff, **violet** mouth glow, wind icon); the Titan **blows devil wind**: swirling gust columns
(cyan-white core HDR 1.6, magenta rim, dark outline — never the background hue) fill grid cells (sky masks, ≥ 1 free cell).
1.0 s before arrival, red frames mark blocked cells and a green frame the free cell at the plane's depth. Spacing 1.9 s,
minimum 1.6 s; first barrage single-axis masks only; under 1.8 s no climb↔dive back to back. Hit → 1 heart + bump.

### 6.2 Tired → Laser
After a barrage the Titan is **tired** (slumps, Zzz, "It's tired — FIRE!"). The plane **auto-fires laser bolts** (8/s,
looping laser sound). A **target ring** moves on an **∞ path** at the plane's depth: centre (0, 3.2), half size (1.8, 1.0),
period 8 s (x = 1.8 sin ωt, y = 3.2 + 1.0 sin 2ωt). The Titan's glowing **Core** (weak point) is placed on the camera ray
through the ring, so on screen the boss flies the same ∞ (wide loops in the sky). Bolt hits when the plane is within the ring
(hit radius 0.95, grows to ×1.4 between 6 s and 12 s of a window as assist): ring turns green, "ON TARGET", bolts strike the
Core (damage). Misses whiz past / glance off armour with a "tink" (no damage). First window prompt: "Fly into the ring!".
Bolt damage per window: 0.7, 0.9, 0.6, 0.25 (HP 100) → ≈ 9 s at 60 % on target; finish ≈ 4 s.
At the next notch (**70 %, 30 %, 5 %**) the Titan **wakes** (roar, shockwave, laser stops, +1 heart). A window that lasts
18 s without the notch ends with a wake anyway; then Wind ×3 → Laser repeats (no clap).

### 6.3 Boss Clap
1. Wind-up: inhale 0.8 s (white palms), the Titan **blows the plane to the top** (push 1.2 s, vertical steering overridden;
   sideways free); spiked walls rise left and right (x = ±3.6), spikes pointing at the plane, covering the top band.
2. Vertical control returns exactly when the warning starts: "DIVE!" + down arrows, safe bottom band glows green.
   Warning 1.8 s (normal) / 2.2 s (large).
3. Walls **smash together** (0.22 s). Hit when plane top (y + 0.25) is inside the band → 2 hearts.
4. Bands: normal 45 % of the grid height (safe below y ≈ 3.1), **second clap larger** 70 % (safe below y ≈ 2.3, bottom row);
   later claps reuse the large size.

### 6.4 Phase-2 beat script (default)
Ready → Rise & duck → Wind ×4 → Laser→70 % → Clap (normal) → Wind ×4 → Laser→30 % → Clap (large) → Wind ×5 →
Laser→5 % → Clap (large) + Wind ×3 (final frenzy) → Laser→0 % "FINISH IT!" → Finale. Tired/wake transitions 1.2 s each.
≈ 95 s.

## 7. Finale (~6 s)
Slow-mo; light cracks burst from the Core (god-ray beams), chained explosions across the parts, white flash, parts fly apart
into a brick shower; the storm clears into **dawn** (look blend). "VICTORY!" banner, victory stinger → result view.
**Knockout** (~3 s): hit-stop then 0.4× slow-mo, vehicle tumbles and bursts, the Titan laughs, "KNOCKED OUT".

## 8. Damage, hearts, difficulty
| | Normal | Easy |
|---|---|---|
| Hearts | 5 | 8 |
| Attack timing scale | 1.0 | 1.25 (slower) |
| Laser hit radius scale | 1.0 | 1.3 |
Hazard damage: brick 1, rock 1, minion tick 1, wind 1, spike wall 2. 1.0 s invulnerability after a hit (car blinks), hit-stop
0.06 s. +1 heart per volley and per wake. Hearts refill at each phase start and on continue. After 2 knockouts in the same phase
the result view focuses "Continue on Easy". Screen shake follows the existing screen-shake preference.

## 9. Checkpoints and save (continue after restart)
Save (own key, versioned): phase, next beat index, pickups, wakes passed, claps done, minions thrown, difficulty, elapsed, stats.
Written only at calm moments: phase start (after the ready beat), the breather after each volley, during each wake roar, during
each clap recovery, on quit and on app pause (last checkpoint snapshot, never mid-attack). "New battle" clears it. Win clears
it. Continue resumes at the saved beat (teaching overrides apply only if their beat was not passed), hearts full, with the
ready beat; a sky continue plays a 2 s rejoin shot. Bests persist (fastest win, fewest hits, wins, furthest phase).
Settings: difficulty (PlayerPreference), seen-cutscene flags.

## 10. HUD (pixel font two-layer: bold colour over dark outline underlay; sizes 16·n)
- Top centre: "BRICK TITAN" (48) + boss bar 960×48 (orange phase 1, purple phase 2 with 70/30/5 notches, white damage trail).
- Prompts: strip at screen y 0.86–0.92 under the bar (96), one at a time with arrows; banners 128 centre (cutscenes only).
- Hearts at (190, −24) from top-left (clear of the Back button), icons 16 px ×3.
- Top right: 5 rocket-crate icons (phase 1) / ON TARGET indicator (phase 2).
- Screen-space world labels: P1/P2 tags over the vehicle (48), "!" over the car for rocks (64), laser target ring, wind cell
  frames, attack icon over the boss.
- Bottom right: PiP 384×216 with over-head indicators; fades to 40 % while a right-side minion/wall is active.
- Letterbox 2×96 px during cutscenes (bars/hearts/prompts hidden, PiP kept).

## 11. Audio
BGM: phase 1 boss loop, phase 2 boss loop (darker); stingers BossAppear, Victory, Defeat. SFX per cue (GDD §4–7), laser as a
loop, music ducks under roars and stingers. Peaks ≤ −3 dBFS.

## 12. Visual style (HD-2D diorama)
- 640×360 world target (exact 3× to 1080p; 480p would be 2.25× and shimmer), point upscale; 270p on the low tier only if the
  perf gate fails.
- ToonLit 4 bands + rim; normal + cavity relief on bricks, rocks, walls and far canyon walls (not on the scrolling asphalt,
  which stays flat 3-band with features ≥ 0.5 m to avoid crawl); cavity/AO baked into the boss and vehicle palettes.
- Tilt-shift band: road centre 0.55, half-width 0.30 (car, reticles, cracks, boss all inside), sky centre 0.52, half-width
  0.25 following the plane; heavy blur (max 2.5) on sky and near foreground.
- Heavy bloom on emissives; only hazards and tells exceed HDR 2.0; ambience ≤ 1.3. Vignette 0.3.
- Grades: dusk (warm gold/orange, deep shadows), night (cool blue-violet, magenta accents), dawn (finale).
- Hard shadows from one sun (only big boss parts cast); blob shadow for the far boss.
- GI: trilight ambient gradient per phase; pooled point lights (no shadows) on rocket hits, explosions, laser impacts so lit
  particles react; god rays (dusk sun through arches, moon shafts at night).
- Particles: 12 fps pixel flipbooks — lit (dust, debris, embers) and glow (sparks, explosions, wind streaks, lasers).
- Light fog (dusk 0.006, night 0.008); a backdrop silhouette hides the scenery wrap.

## 13. Languages
en, fr-CA, ja, zh-Hans, zh-Hant — every boss string translated; pixel font CJK atlas rebuilt after seeding.

## 14. Analytics (every UI action and gameplay result)
minigame_start (per attempt, is_continue) / minigame_end (win, lose, abandon), screen tracking for every view (boss-start,
boss-setup, boss-intro, boss-road, boss-transform, boss-sky, boss-finale, boss-knockout, boss-pause, boss-result),
ui_button_click for every button incl. back, skip, difficulty, secret_code; settings_changed; boss_phase_start/end,
boss_attack_start/result, boss_minion_cleared, boss_powerup, boss_wake, boss_laser_window, boss_clap, boss_vehicle_hit,
boss_cutscene {name, seconds, skipped}, boss_checkpoint_save/continue, boss_tracking_lost, boss_pause/resume (+ TrackPause/
TrackResume), boss_defeat.

## 15. Performance targets (Mali-G52, Nex Playground)
60 fps: GPU ≤ 14.5 ms, batches ≤ 110 incl. shadow casters, SetPass ≤ 60, ≤ 60k tris, ≤ 300 particles, 0 B GC in steady frames,
UGUI ≤ 0.3 ms, saves < 4 ms at calm moments. GameScene UI cameras: no HDR intermediate while the mode runs.
