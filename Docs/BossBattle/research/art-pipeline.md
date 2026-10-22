# Boss Battle: 3D/2D art production pipeline (research note)

Date 2026-10-22. Scope: how low-poly models, palette textures, pixel particles, icons and fonts are produced
offline (Blender 5.2 headless + Python) and brought into `ninjago-unity` (Unity 6000.3.9f1, URP 17.3, Android).
Everything marked **verified** was run or read on this machine (`/Users/oscar/...`). Read-only investigation: no
Unity asset, scene or setting was changed. One Blender build ran with output in the session scratchpad only.

Paths below are relative to the repo root `/Users/oscar/gitfolder/ninjago/ninjago-dragon-rising` unless absolute.
`U/` = `ninjago-unity/Assets/`.

---

## 0. TL;DR

| Topic | Fact / decision |
|---|---|
| Blender | **Installed**: `/Applications/Blender.app/Contents/MacOS/Blender` = 5.2.2 LTS (embedded Python 3.13.13, numpy 2.3.4). `/opt/homebrew/bin/blender` is the Homebrew cask wrapper to the same app. |
| Python deps | **Missing.** `/usr/bin/python3` (3.9.6) and `/opt/homebrew/bin/python3.13` have no numpy/Pillow/scipy/fontTools; there is no `Tools/.venv`. Create one (section 1). Blender's own Python has numpy only (no PIL/scipy). |
| Headless build | **Verified**: `bl_build_model.py --model Boss_CrystalGolem` ran in 3.95 s (992 tris, 6 parts) and produced the same geometry as the committed `U/Models/BilliardRogue/Bosses/Boss_CrystalGolem.fbx`. Only the FBX `Texture/Video FileName` nodes differ (absolute output path). |
| Old paths | The scripts were written for `/Users/simonbut/project/VibeProject3` with a `Starter/` Unity project. On this machine the project is `ninjago-unity/`. 17 default paths point at `Starter/...` (section 2.3). The Boss Battle scripts must use `ninjago-unity` paths. |
| Model colouring | One shared **32x16** palette `U/Textures/BilliardRogue/Palette/Palette_Main.png` (16 colour families x 16 shades, left half = albedo, right half = the same colours marked **emissive**). Every face's UVs point at the centre of one texel. Material `U/Materials/BilliardRogue/M_Palette.mat` uses shader `BilliardRogue/ToonLit` (4 cel bands, rim light, `_EmissionMap = Palette_Emission.png` x 1.6). |
| Unity import | No AssetPostprocessor. The explicit `Nex.BilliardRogue.Editor.ImportSettingsBuilder.Run()` covers only the `*/BilliardRogue` roots. Boss Battle needs its own roots (section 6.1). |
| Ninjago today | Every Ninjago mini-game builds its 3D from **Unity primitives** (`NinjagoEditorUtils.Block`) with flat-colour **URP/Lit** materials (`NinjagoAssetsBuilder.Lit`). There is no FBX, no toon shading and no pixel font. All UI uses the **Play Chickens SDF** font. |
| Recommendation | Build the Boss Battle actors and arena with the **ekit / env_lib Blender kits + palette + ToonLit** (the HD-2D look needs cel bands, which URP/Lit cannot give). Particles: new 8-frame strips from `make_particles.py` code. Font: BilliardPixel TMP assets (they already fall back to CJK). |

---

## 1. Environment on this machine (verified)

```
/Applications/Blender.app/Contents/MacOS/Blender --version      -> Blender 5.2.2 LTS (2026-09-15)
which blender                                                   -> /opt/homebrew/bin/blender (cask wrapper)
/opt/homebrew/bin/ffmpeg                                        -> present
unity command editor_status --project-path .../ninjago-unity    -> status ready, playMode stopped (Editor OPEN)
```
The Unity project uses **Gamma** colour space (`ProjectSettings.asset:50 m_ActiveColorSpace: 0`). Android runs
**OpenGLES3 only** (`m_APIs: 0b000000`). The default Android compression is ETC2 (`m_Formats: 02000000`).
`VertexChannelCompressionMask: 4054` (UV0 may be stored as fp16, which is still precise enough for texel-centre UVs).

**Python venv (one-off, not done yet).** `Tools/.venv/` and `Tools/Staging/` are already gitignored (`.gitignore`
lines 157 and 160).
```bash
cd /Users/oscar/gitfolder/ninjago/ninjago-dragon-rising
/opt/homebrew/bin/python3.13 -m venv Tools/.venv
Tools/.venv/bin/pip install numpy pillow scipy fonttools
Tools/.venv/bin/python -c 'import numpy, PIL, scipy, fontTools; print("ok")'
```
These are the imports used by `Tools/Textures/*.py`, `Tools/Fonts/build_pixel_font.py` (fontTools),
`Tools/Blender/*/build_*.py` (orchestrators: numpy + PIL), `compose_previews.py` / `review_sheet.py` (scipy).
Code that runs inside Blender (`bl_*.py`, `ekit.py`, `env_*.py`, `hp_*.py`, `models_*.py`) needs only `bpy`,
`bmesh` and `mathutils`. `game_look.py` imports scipy lazily, so Blender can import it.

The Editor is open and holds the project lock, so **batchmode is impossible**. Run editor builders with
`unity command eval '<C# returning string>' --project-path .../ninjago-unity` (example in the
`ImportSettingsBuilder` header comment). Do it only in the implementation phase.

---

## 2. Blender headless pipeline

### 2.1 Invocation contract (verified)
```bash
B=/Applications/Blender.app/Contents/MacOS/Blender
R=/Users/oscar/gitfolder/ninjago/ninjago-dragon-rising
P=$R/ninjago-unity/Assets/Textures/BilliardRogue/Palette
$B -b --factory-startup --python-exit-code 1 --python $R/Tools/Blender/enemies/bl_build_model.py -- \
   --model Boss_CrystalGolem --fbx $OUT/Boss_CrystalGolem.fbx --icon-raw $OUT/golem_raw.png --icon-size 128 \
   --palette-json $P/palette.json --palette-png $P/Palette_Main.png --stats $OUT/golem.json
# -> one line "ENEMY_STATS {json}", exit 0; 4.5 s wall (EEVEE icon included)
```
- `-b` runs in the background; `--factory-startup` ignores user prefs and add-ons. Without `--python-exit-code 1` a
  Python exception still exits 0. Arguments after `--` are parsed by `bl_common.parse_args`
  (`Tools/Blender/bl_common.py:21`).
- Orchestrators run with the venv Python and spawn one Blender process per model in a thread pool:
  `build_enemies.blender()` (`Tools/Blender/enemies/build_enemies.py:83`), `build_one()` (:89; it greps
  `ENEMY_STATS`). The environment kit uses `build_all.py` -> `build_env.py` (prints `ASSET_STATS` per piece).
- EEVEE works headless (Metal). Icons render with `look.setup_eevee(..., pixel=True)`, which gives hard alpha.
  `finalize_icon()` (`build_enemies.py:56`) then adds a 1 px outline in palette gray 0 (12,12,16) and fails if the
  model touches the icon border.

### 2.2 Files and APIs (what to reuse)

| File | Key symbols (line) | Role |
|---|---|---|
| `Tools/Blender/bl_common.py` | `parse_args` 21, `reset_scene` 44, `load_palette` 53, `palette_uv` 60, `paint_faces` 74, `palette_material` 86, `canonicalize` 156, `mesh_object` 170, `tri_count` 183, `export_fbx_if_changed` 244, `export_fbx` 256, `nested_bake_space_fix` 298, `export_fbx_nested_if_changed` 330, `setup_icon_render` 338 | Shared: palette UVs, deterministic meshes, FBX export flags, no-churn export |
| `Tools/Blender/enemies/ekit.py` | `C` 33, `vgrad` 41, `facing` 49, `T/R/S` 59-71, `frame` 73, `aim` 87, `Geo` 93, `lathe` 166, `sphere` 196, `dome` 201, `hexa` 212, `box` 221, `prism` 245, `slab` 276, `ngon` 281, `sweep` 286, `cone` 331, `cyl` 335, `crystal` 339, `ico` 345, `jitter` 378, `eye` 403, `eye_pair` 429, `Part` 463, `Model` 475 (`scale_all` 493, `scale_part` 515, `tilt_up` 526, `build` 543, `stats` 574), `export_fbx` 629 | **Character/prop kit** in Blender space; geometry is pure Python, so vertex/face order is deterministic |
| `Tools/Blender/enemies/models_enemies.py` | `ENEMY_BUDGET=600` 16, `top_lit` 24, `shine` 28, `on_surface` 45, `on_top` 53, `skeleton()` 139, `bone()` 478, `big_skull()` 500 | Nine enemies; the skeleton is the template for the minion |
| `Tools/Blender/enemies/models_bosses.py` | `BOSS_BUDGET=1500` 12, `rock()` 15, `rock_rule` 22, `king_slime` 28, `bone_lich` 91, `crystal_golem` 216, `REGISTRY` 293 | Segmented bosses (arms, head, core, emissive weak point) |
| `Tools/Blender/enemies/contract.py` | `REQUIRED_PARTS` | Exact part names per FBX (the build fails on a mismatch) |
| `Tools/Blender/enemies/bl_build_model.py` | `check_contract` 47 (parts, tri budget, baked rot/scale, nothing below the pivot), `check_footprint` 66, `main` 139 | Per-model Blender entry |
| `Tools/Blender/enemies/bl_look.py` | `BANDS4` 16, `toon_material` 34, `setup_eevee` 178, `sun` 200, `frame_ortho` 234 | EEVEE cel material for icons and reviews |
| `Tools/Blender/enemies/game_look.py` | `shade` 85, `bloom` 120, `grade` 178 | Review-only emulation of ToonLit and URP post (reads Billiard Rogue acts and volumes from `Starter/...`) |
| `Tools/Blender/environment/env_lib.py` | `u2b/b2u` 27/32, `xform` 36, `Rng` 57, `Part` 88 (`box` 126, `lathe` 150, `cyl` 192, `ico` 196, `poly_prism` 210, `extrude_y` 300, `paint` 325, `paint_fn` 355, `box_uv` 366, `to_object` 387), `EMISSIVE_KEYWORDS` 23 | **Environment kit, authored in Unity space** (x right, y up, z forward) |
| `Tools/Blender/environment/env_pieces.py` | `stone_fn` 81, `flame_fn` 130, `ruin_column` 435, `ruin_arch` 464, `brazier` 630, `cave_rock` 856, `crypt_pillar` 1244, `light_shaft` 1276, `PIECES` 1305, `TRI_BUDGET=800` 1368 | 56 pieces; the ruins, rocks and braziers are directly reusable for the dark arena |
| `Tools/Blender/environment/build_env.py` | `check_uvs` 34 (texel centre + correct emissive half) | Piece builder + UV contract |
| `Tools/Blender/heroprops/hp_mesh.py`, `heroprops_models.py` (`build_cue` 277 along Unity +Z, `build_crate` 386, `MODELS` 648), `verify_fbx.py` (`EXPECT`) | | Hero and props; FBX re-import verification of names, parents, identity transforms and UVs |

### 2.3 Hardcoded `Starter` paths (must be overridden)
`bl_common.py:26` (palette default), `enemies/build_enemies.py:38`, `enemies/game_look.py:26`,
`enemies/bl_preview.py:36`, `heroprops/build_heroprops.py:41` (also `/Users/simonbut` in its docstring),
`environment/{make_layouts.py:133, render_diorama.py:33,36, render_pieces.py:24, verify_env.py:20,
make_preview_surfaces.py:20}`, `Textures/pixelkit.py:23` (palette fallback), `Textures/paintkit.py:798`,
`Textures/make_arms.py:29`, `Textures/art_report.py:83`, `Fonts/build_pixel_font.py:479` (CJK source font),
`compile_check.py:22`, `sync_staging.sh` (rsyncs to `Starter/Assets`).
Orchestrators also write to the gitignored mirror `Tools/Staging/Assets/...`. `pixelkit.staging()` (`:27`)
honours `BR_STAGING_DIR`, which redirects 2D writes.
**For Boss Battle**, use one `bb_paths.py` that defines `REPO`, `UNITY_ASSETS = REPO/ninjago-unity/Assets`,
`PALETTE_DIR = UNITY_ASSETS/Textures/BilliardRogue/Palette` and `BLENDER`, and always pass `--palette-json` /
`--palette-png`. Write outputs **directly** into new `U/.../BossBattle/` folders. `export_fbx_if_changed` writes a
dot-prefixed temp file that Unity ignores, so this is safe while the Editor is open. Do not resurrect
`sync_staging.sh`: it would copy the whole staging mirror. Do not add a `Starter` symlink to the repo either; the
Makefile also references `./Starter`.

### 2.4 FBX export and Unity axes (bl_common.export_fbx :256, verified by the import metas)
Flags: `apply_scale_options="FBX_SCALE_ALL"`, `axis_forward="-Z"`, `axis_up="Y"`, `bake_space_transform=True`,
`mesh_smooth_type="FACE"`, `use_tspace=False`, `add_leaf_bones=False`, `bake_anim=False`, `path_mode="STRIP"`,
`use_metadata=False`.
- 1 Blender m = 1 Unity unit, root scale 1, rotation 0. Blender (x, y, z) maps to Unity (-x, z, -y).
- **ekit / hp_mesh models are authored in Blender space**: front = Blender -Y (Unity **+Z**), up = +Z, the
  character's right hand = Blender -X (Unity +X). The pivot is the bottom centre at the origin.
- **env_lib pieces are authored in Unity space** through `u2b()`. Do not mix the two conventions in one script.
- Hierarchies 2 or more levels deep must export through `ekit.export_fbx` / `bc.export_fbx_nested_if_changed`.
  Blender 5.2's exporter otherwise gives grandchildren a bogus 90 deg local rotation (`bl_common.py:298`).
  `bl_build_model.py:156` already uses `ekit.export_fbx`.
- **FBX determinism (verified):** the binary always differs per export (timestamps and UIDs).
  `export_fbx_if_changed` compares the parsed nodes and replaces the file only when its content changed. The
  comparison includes the `Texture/FileName` string, which becomes `<output dir>/Palette_Main.png` even with
  `path_mode=STRIP`. Building the same model into a **different folder** therefore counts as "changed" once. A
  rebuild into the same folder gives `fbx_changed=False`.

### 2.5 The ekit pattern to copy (from `models_bosses.crystal_golem`, :216)
```python
m = k.Model("Boss_CrystalGolem", BOSS_BUDGET, footprint=(2, 2))
body = m.part("Body", (0, 0, 0))                                  # root part: pivot at the feet
torso = rock(rock_rule, 3, subdiv=1, amount=0.07).copy(T(0, 0.12, 0.98) @ S(0.6, 0.5, 0.55))
body.add(torso.paint(rock_rule))                                   # colour rule per face (normal / centre)
for name, side, seed in (("ArmR", -1, 21), ("ArmL", 1, 31)):
    sh = Vector((side * 0.5, 0.08, 1.28))                          # shoulder = the animation pivot
    arm = m.part(name, sh, parent="Body")                          # geometry stays in model space
    arm.add(rock(rock_rule, seed).copy(T(side * 0.6, 0.0, 0.9) @ S(0.15, 0.16, 0.26)).paint(rock_rule))
core = m.part("CoreCrystal", (0.0, -0.5, 1.0), parent="Body")
core.add(k.lathe([(0.0, -0.2), (0.15, -0.05), (0.15, 0.07), (0.0, 0.28)], 6,
                 lambda c, n: C("cyan", 13 if n.z > 0.3 else 10, True)), T(0.0, -0.5, 1.0))   # True = emissive
return m          # registered in REGISTRY; part names listed in contract.REQUIRED_PARTS
```
`Model.build()` (:543) subtracts each pivot, paints the UVs, triangulates warped quads (`_triangulate_nonplanar`,
:448), canonicalizes, and parents with location only. Rotation and scale are always baked into the mesh, so Unity
children have identity rotation/scale and are tweenable about their pivots. The stats JSON gives `pivot_unity`
per part. Colour spec: `C(family, shade 0..15, emissive)`. Rules: `vgrad` (by height), `facing`/`top_lit` (by
normal), `shine` (baked highlight), or a lambda `(centre, normal) -> C(...)`.

---

## 3. Palette and texture conventions

- `Tools/Textures/make_palette.py` (`FAMILIES` :31, append-only) writes `Palette_Main.png` (32x16 RGBA),
  `Palette_Emission.png` (black left half, colour right half), `Palette_CatP2.png` and `palette.json`
  (`{"size":[32,16],"shades":16,"emissiveOffset":16,"families":{name:{row,hex[16]}}}`). The committed copies are in
  `U/Textures/BilliardRogue/Palette/`.
- Rows (verified from palette.json): red 0, orange 1, yellow 2, lime 3, green 4, teal 5, cyan 6, sky 7, blue 8,
  indigo 9, purple 10, magenta 11, pink 12, brown 13, skin 14, gray 15. Shade 0 is dark and 15 is light. The
  ramps are hue-shifted (cool shadows, warm highlights). gray 0 = (12,12,16), gray 15 = white.
- UV = texel centre: `u = (shade + 0.5 [+16 if emissive]) / 32`, `v = 1 - (row + 0.5) / 16` (`bl_common.palette_uv`
  :60). `build_env.check_uvs` enforces texel centres and the correct half.
- **Emission is palette-driven, not name-driven.** A face glows when it is mapped into the right half
  (`build_enemies.EMISSIVE_RULE` :112). env_lib also sets `emissive_name` from `EMISSIVE_KEYWORDS`
  (Emissive/Gem/Orb/Flame/Crystal/Fuse/Eyes) as the default emissive flag of a `Part`.
- Brightness rules from the Billiard Rogue polish pass (`Docs/BilliardRogue/progress/polish-enemies.md`):
  keep large lit surfaces at **shade <= 12-13** (bloom starts around 1.0 in gamma space). Use emissive eye whites
  at `gray 4` (`ekit.SCLERA` :399) and full white only for tiny glints. The `x2.2` emission noted in the
  ekit/game_look comments is stale: `MaterialsBuilder.PaletteEmission` is now **1.6** (`MaterialsBuilder.cs:31`).
- Recolour variants (rage phase, hurt): add a palette PNG with the **same layout** and a material variant that
  uses it (the `M_Palette_CatP2` pattern, `MaterialsBuilder.cs:56`). Avoid MaterialPropertyBlocks on many
  renderers because they break SRP batching. The hit flash on a few boss renderers is fine:
  `EnemyStatusVisuals` (`U/Scripts/BilliardRogue/Presentation/EnemyStatusVisuals.cs:13`) drives `_FlashAmount` /
  `_StatusTint` through MPBs.
- Tiling detail textures (`_Surface` parts): 64 px PNG sets `<Name>_{Albedo,Normal,Cavity}.png` in
  `U/Textures/BilliardRogue/Surfaces/` plus `surfaces.json` (`tiling` 0.5). They come from
  `Tools/Textures/make_surfaces.py`. Normal maps follow the OpenGL convention (+G up); cavity 0.5 is neutral. Use
  them on `*_Surface` parts with box UVs (1 UV = 1 m) and `M_Surface_<Name>` materials.

---

## 4. Shaders and materials in the project (reuse; do not modify for Boss Battle)

| Shader (`U/Shaders/BilliardRogue/`) | Use |
|---|---|
| `BilliardRogue/ToonLit` (`ToonLit.shader` properties :5-36) | Opaque cel. `_BaseMap`, `_BaseColor`, `_Bands` (3-5), `_BandSoftness`, `_ShadowTint`, `_AmbientStrength`, `_RimColor` (A = strength), `_RimPower`, `_EMISSION` + `_EmissionMap/_EmissionColor/_EmissionStrength`, `_NORMALMAP`, `_CAVITYMAP`, `_WORLD_UV`, `_FlashColor/_FlashAmount`, `_StatusTint`, `_Cull`. Property names are a contract (`Editor/Tests/RenderingContractTests.cs:26`). |
| `BilliardRogue/ToonLitTransparent` | `M_Palette_Ghost` (`_Alpha`) |
| `BilliardRogue/LitParticle` | Alpha-clipped, lit pixel billboards (`_Cutoff`, `_LightInfluence`, `_Bands`, `_EmissionStrength`) |
| `BilliardRogue/GlowParticle` | Additive HDR (`_Intensity`, `_SOFT_DISC`) |
| `BilliardRogue/LightShaft` | God-ray volumes (`Env_LightShaft` mesh, unit UVs) |
| `BilliardRogue/TiltShiftBlur` | Post blur |

`MaterialsBuilder.BuildPalette` (`U/Scripts/BilliardRogue/Editor/MaterialsBuilder.cs:85`): `_Bands 4`,
`_BandSoftness 0.02`, `_ShadowTint (0.34,0.32,0.5)`, `_AmbientStrength 0.35`, `_RimColor (1,0.95,0.85,0.55)`,
`_RimPower 3`, `_EMISSION` on, `_EmissionMap = Palette_Emission.png`, `_EmissionColor = white x 1.6`.
The HD-2D frame settings (640x360 RT, tilt-shift, bloom 0.9/0.8, vignette 0.25, low-tier GPUs Mali-G52/G51/G31)
are in `HD2DVisualConfig` (`U/Scripts/BilliardRogue/Configs/HD2DVisualConfig.cs:26-68`). The rendering note
covers those; this note covers only content.

---

## 5. 2D art: particles, icons, UI, fonts

### 5.1 Pixel particle strips (`Tools/Textures/make_particles.py`)
- **Format (actual, supersedes asset-toolchain.md section 8.3):** one PNG per effect, a horizontal strip of
  **8 frames** (16x16 or 32x32 each). Values are white/grey (W 255, L 214, M 170, D 128; contour K 56), alpha
  strictly 0/255, every frame keeps a **1 px empty gutter** (the build fails otherwise), fixed seeds.
  `particles.json` holds `frames`, `frameSize`, `columns 8`, `rows 1`, `loop`, `fps 12`,
  `startSizeMetres = px/28`.
- `SHEETS` (:545): Spark, Dust, Leaf, Ember, Snow, Mote, Smoke, Star (16 px); Ring, Bolt, Flash (32 px); IceShard,
  Bubble, Heart, Debris (16 px). Generators: `spark` 117, `puff` 147 (shared by dust/smoke), `ember` 232,
  `ring` 331, `bolt` 354, `debris` 492, `flash` 503; helpers `Frame` (paint/under/erase), `dissolve` 72,
  `shaded_poly` 86. Committed: `U/Sprites/BilliardRogue/Particles/*.png` (Spark 128x16, Ring 256x32).
- Painted override: `Tools/Textures/paint/<Folder>/<Name>.paint` replaces the procedural sprite
  (`paintkit.paint_path/render`). See `Tools/Textures/paint/README.md` for the pixel-art quality rules (no
  Pillow-band shading, no noise dither, hand-shaped clumps, hard alpha).
- Unity side: `VfxMaterialLibrary` (`U/Scripts/BilliardRogue/Presentation/Vfx/Editor/VfxMaterialLibrary.cs`,
  `SheetRoot` hardcoded to `Assets/Sprites/BilliardRogue/Particles`) makes one material per shading x sheet so
  systems batch. `VfxParticleFactory.ConfigureLook` (:170, sheet setup :184-197) sets
  `textureSheetAnimation`: Grid, `numTilesX = columns`, `numTilesY = rows`, WholeSheet, `timeMode FPS`,
  `fps = 12`, `startFrame` range. Emissive sprites render as sheet x vertex colour x (1 + boost). The particle
  colour tints the white sheet; HDR > 1 feeds bloom. This gives the "billboard lit pixel particles at 12-15 fps".
- Ninjago VFX today (`U/Scripts/Ninjago/Editor/NinjagoVfxBuilder.cs`) use soft URP Particles/Unlit circles and lit
  cube "bricks", not pixel strips. They are registered in `VfxManager.prefab` through
  `NinjagoAssetsBuilder.EditPrefab<VfxManager>(..., "effectSpecs", ...)` + `Register(dict, effect, prefab, 2, 4)`.
  `VfxManager.VisualEffect` values in use: 0-42 (Billiard Rogue) and 100-211 (Ninjago). Give Boss Battle an
  explicit block, e.g. **300-349**.

### 5.2 Icons and UI
- `make_icons.py`: 16/32 px cel-shaded pixel icons (ball types, status, telegraph). `make_ui.py`: 9-slice frames,
  bars (`Bar_Fill_Boss`), overlays, logo, `ui_slices.json`. UI pixel art is drawn 1x and shown at 3x
  (`ImportSettingsBuilder.UiPixelsPerUnit = 100/3`). `build_art2d.py` runs fonts -> surfaces -> particles ->
  icons -> UI -> extras -> arms -> painted, keeps an ownership manifest, and `--check` verifies determinism.
- 3D-rendered icons: `bl_build_model.py --icon-raw ... --icon-size 48` + `finalize_icon` (1 px gray-0 outline).
- Ninjago icons are generated in the Editor as white anti-aliased shapes (`NinjagoAssetsBuilder.BuildSprite`
  :57/:85; `IconBoss` in `ChapterAssetsBuilder.cs:91`) and imported as Sprites with default (bilinear) settings.

### 5.3 Fonts (which font is used where)
- **Ninjago views today**: `NinjagoEditorUtils.FontPath = "Assets/Fonts/BrainParty/Play Chickens SDF.asset"`
  (`NinjagoEditorUtils.cs:24`). Every `Label()` (:166) / `PlainText()` (:175) uses it. The outline material is
  `Materials/Ninjago/Play Chickens Outline.mat` (`NinjagoAssetsBuilder.OutlineTextMaterial` :166). Its fallback is
  `Fonts/Localization/GlowSans-ja/GlowSansJ-Normal-Medium SDF.asset`. It is SDF and smooth, not pixel.
- **Pixel fonts available** (`U/Fonts/BilliardRogue/`): `BilliardPixel_TMP`, `BilliardPixelBold_TMP`,
  `BilliardPixelOutline_TMP`, `BilliardPixelBoldOutline_TMP` (static 512x512 atlases, RASTER_HINTED 1-bit,
  16 px sampling, padding 1, 217 glyphs). All four fall back to `BilliardPixel_CJK_TMP` (static 1024x1024 built from
  `GlowSansJ-Normal-Bold.otf`, currently 952 glyphs). Built by `Tools/Fonts/build_pixel_font.py` (TTFs from
  `glyphs_regular.txt` / `glyphs_bold.txt`; 16 px em; tabular digits; outline fonts dilated 1 px with identical
  metrics) and `Nex.BilliardRogue.Editor.FontAssetsBuilder.Run()` (`U/Scripts/BilliardRogue/Editor/FontAssetsBuilder.cs`).
- Rules: use font sizes of **16 x n** (48 = 3x), no faux bold/italic. For high-contrast numbers (boss HP), put an
  Outline-asset text in `#0a0c18` under the main text. **The CJK atlas is static**: after seeding new Boss Battle
  strings in ja / zh-Hans / zh-Hant, re-run `FontAssetsBuilder.Run()`. It collects every character in the
  `LocalizationTable` string tables, and missing characters render as boxes. Locales present: en, fr-CA, ja,
  zh-Hans, zh-Hant.
- To use the pixel font in Boss Battle views, add a font parameter (or a `PixelFontPath` constant) to the Ninjago
  `Label/PlainText` helpers instead of changing `FontPath`, which would restyle every other Ninjago game.

---

## 6. Unity import, materials and prefabs (how FBX gets in)

### 6.1 Import settings (`U/Scripts/BilliardRogue/Editor/ImportSettingsBuilder.cs`)
- An explicit builder, not a postprocessor ("no global AssetPostprocessor, so nothing outside our folders is ever
  reimported"). `Roots` (:35) = `Assets/{Models,Textures,Sprites,Audio/Sfx,Audio/Bgm,Fonts}/BilliardRogue`.
  `Run()` (:58) refreshes the AssetDatabase, applies settings per file, and calls `SaveAndReimport` only when the
  importer fingerprint changed (idempotent).
- `ConfigureModel` (:123): `globalScale 1`, `useFileScale`, **`bakeAxisConversion=false`** (true flips the model
  to face -Z), `preserveHierarchy`, `sortHierarchyByName`, `meshCompression Off`, `isReadable false`,
  `importNormals Import` (keeps the flat facets), `importTangents None`, **`materialImportMode None`**,
  `animationType None` (only `*_Anim.fbx` -> Generic). Verified in
  `U/Models/BilliardRogue/Bosses/Boss_BoneLich.fbx.meta` (`materialImportMode: 0`, `bakeAxisConversion: 0`,
  `preserveHierarchy: 1`, `useFileScale: 1`, `importAnimation: 0`, `animationType: 0`).
- `ConfigureTexture` (:165): point filter, no mips, `_Normal` -> NormalMap, `_Cavity/_Height/_Mask` -> R8; the
  palette and `/Vfx/` clamp. `ConfigureParticle` (:194): Default type, sRGB, alpha is transparency, clamp, point,
  uncompressed. `ApplyCompression` (:253): <= 256 px stays uncompressed (Android `Automatic`, i.e. RGBA32), larger
  -> ASTC 4x4.
- **For Boss Battle:** make `ConfigureModel/ConfigureTexture/ConfigureParticle/ConfigureSprite` `internal` (all
  editor code compiles into Assembly-CSharp-Editor; the only asmdefs are Billiard Rogue Simulation/InputCore).
  Then add `BossBattleImportSettings.Run()` with roots `Assets/Models/BossBattle`,
  `Assets/Textures/BossBattle`, `Assets/Sprites/BossBattle`. Do **not** add a global AssetPostprocessor or bump
  versions: Billiard Rogue research measured a full-project reimport when that happens.

### 6.2 Material assignment and prefab wrapping (Billiard Rogue pattern)
`WorldPrefabModels` (`U/Scripts/BilliardRogue/Editor/WorldPrefabModels.cs`):
`InstantiateModel(relativePath, parent, name, placeholderPrimitive, placeholderScale, layer, out isPlaceholder)`
(:145) nests the FBX as a prefab instance inside a container, so re-exports flow through; a missing model becomes
a primitive placeholder. `ApplyMaterial(root, material, renderers)` (:188) assigns `M_Palette` to every slot.
`CastShadowsFrom(renderers, castPart)` (:210) keeps one shadow caster (the largest part).
`FindPart(root, name)` (:232) does a recursive lookup. `Palette(player2)` (:78) loads `M_Palette`.
Gameplay must reference the wrapper prefab, never the raw FBX: with `materialImportMode=None` the raw FBX shows
`Default-Material`.

### 6.3 Animation
Billiard Rogue animates rigid parts in code (DOTween, used in 22 Billiard Rogue scripts). Ninjago rigs
(`BossRig`, `BruteRig`, `VehicleRig`) animate transforms procedurally without DOTween (0 Ninjago scripts import
it). Both work with ekit parts because every part has identity rotation/scale and its pivot at the joint. Use
`*_Anim.fbx` only for authored clips; that is not needed here.

---

## 7. How Ninjago builders make models today (primitives)

- `NinjagoEditorUtils.Block(PrimitiveType, name, parent, localPos, localScale, material, euler?)` (:223) creates a
  primitive, removes its collider and sets the transform and shared material. `Empty(name, parent, pos)` (:238)
  creates a pivot node. `SavePrefab(root, path)` saves the prefab (`PrefabsRoot = Assets/Prefabs/Ninjago`).
- `NinjagoAssetsBuilder.Lit(name, color, smoothness = 0.15, emission?)` (:122) creates or loads
  `Assets/Materials/Ninjago/<name>.mat` with **URP/Lit**; emission enables `_EMISSION`.
  `Particle(name, transparent)` (:143) creates URP Particles/Unlit (soft circle) or Simple Lit (bricks).
  `MaterialAsset` (:155) never overwrites an existing material. This explains the modified `Build *.mat` files in
  git status: the builders re-save materials.
- Chapter boss: `ChapterActorsBuilder.BuildEnemy(path, boss)` (:182) and `BuildEnemyBody(parent, boss)` (:257)
  build a blocky brick body facing **-Z**: legs, torso, head, a 2x2 grid of **studs** on the head (:271, cylinder
  0.22 diameter, height 0.12), yellow emissive eyes, `ArmPivot`/`HandPoint`/`ChestPoint` empties. The boss adds
  horns, spiked shoulders and a cape (:284-292); the root is scaled by 3 (:233). The runner track reuses it:
  `ChapterWorldBuilder.BuildRunner` places `BossRunner` (z 22, figure scale 2.2, "Chapter Boss Body" material) and
  `BossFlyer` (z 28; a deck plus two wing cubes, purple emissive) (:261-270). The rock is 2 rotated cubes
  (`BuildRock` :302).
- Chase world: `NinjagoWorldBuilder.BuildRoad` (:239; asphalt 6.4 m wide, curbs at x +/-3.3, dashes, block trees,
  `ScrollingStrip` wrap), `BuildSky` (:280; sphere clouds), `BuildCar` (:308; faces **+Z**, wheels, 1.55x2.4 m
  chassis), `BuildSkycraft` (:338; faces +Z), `BuildObstacleBlock` (:221; a unit cube scaled per cell with
  "mortar" bands and a cap, **no studs**).
- **Brick conventions**: `KidsWorldBuilder.Brick` (`KidsWorldBuilder.cs:132`) is a 2x4 toy brick: block
  0.8x0.4x0.4, 8 studs at pitch 0.2, diameter 0.12 (0.6 x pitch, LEGO-correct), height 0.06 (0.3 x pitch, taller
  than LEGO's ~0.21 for readability). `OnePunchWorldBuilder` (:193-208): a monster with one big stud on the head,
  face on +Z. `ChapterActorsBuilder`: 4 studs on the head "so the bricks are easy to count". Conclusion: Ninjago
  actors are chunky bricks with studs on top, saturated flat colours and emissive eyes. Facing is inconsistent
  (vehicles and OnePunch +Z, chapter enemy -Z), so state the facing in each new prefab.

---

## 8. Scale and texel density for the chase camera

The car camera pose is (0, 3, -6.5) with pitch 8.8 deg and vertical FOV 60 (`ChapterWorldBuilder` /
`NinjagoWorldBuilder.BuildRunnerWorld`). On the 640x360 world RT:
**px per metre = 360 / (2 d tan 30 deg) = 312 / d** (d = distance from the camera).

| Object | Distance d | px/m | Consequence |
|---|---|---|---|
| Car / minions on the car | 6.5 m | 48 | a 16 px particle = 0.33 m; minion 1.2 m tall = 58 px |
| Road power-up / rock warning | 10-20 m | 31-16 | crate 1.2 m = 20-37 px |
| Boss (phase 1) | ~28 m (BossRunner z 22) | 11 | **boss 10-12 m tall = 110-130 px (1/3 of the screen)**; a stud 0.6 m = 7 px |
| Boss (phase 2, flying) | 15-25 m | 21-12 | wing span 14 m = 170-290 px |

Particle `startSize` (m) = frame px / (px/m at the effect's distance). Billiard Rogue's 28 texels/m (top-down
arena) does not apply here. Thin details must be >= 2 px at their distance: >= 0.18 m on the boss, >= 0.04 m on
the car.

---

## 9. Recommended Boss Battle art plan

### 9.1 New scripts (all deterministic, one command)
| File | Runs in | Content |
|---|---|---|
| `Tools/Blender/bossbattle/bb_paths.py` | both | `REPO`, `UNITY_ASSETS`, `PALETTE_DIR`, `BLENDER`, output roots (no `Starter`) |
| `Tools/Blender/bossbattle/bb_kit.py` | Blender | `from ekit import *` plus Ninjago helpers: `brick(nx, nz, h, color, studs=True, segs=6)` (studs pitch p, diameter 0.6p, height 0.25p, 6-segment = 16 tris each), `stud_grid()`, `spike(r, h, sides=4)`, `plate()`, `helix_ribbon()` (gust), `panel_lines()` |
| `Tools/Blender/bossbattle/models_bb_actors.py` | Blender | `REGISTRY` of the actor builders below |
| `Tools/Blender/bossbattle/models_bb_arena.py` | Blender | arena pieces with `env_lib.Part` (Unity space) + `PIECES` |
| `Tools/Blender/bossbattle/contract_bb.py` | both | `REQUIRED_PARTS` + `BUDGETS` per model |
| `Tools/Blender/bossbattle/bb_build_model.py` | Blender | copy of `bl_build_model.py`: same contract checks, footprint check replaced by a bounds check, `ekit.export_fbx`, raw icon, review views |
| `Tools/Blender/bossbattle/build_bossbattle.py` | venv | parallel orchestrator (pattern of `build_enemies.py`), `bb_models_manifest.json` (parts, pivots, bounds_unity, tris, emissive faces), icons via `finalize_icon`, review sheet in the scratchpad |
| `Tools/Textures/make_particles_bossbattle.py` | venv | `import make_particles as mp`; reuse `mp.Frame/puff/spark/ember/ring/flash/debris/bolt`; new generators; writes strips + `particles.json` |
| `Tools/Textures/make_surfaces_bossbattle.py` (optional) | venv | `LavaBrick`, `DarkStone`, `Asphalt` 64 px sets (pattern of `make_surfaces.py`) |
| `U/Scripts/Ninjago/BossBattle/Editor/BossBattleArtBuilder.cs` | Unity | import settings for the BossBattle roots, `M_BB_*` materials, wrapper prefabs (`WorldPrefabModels.InstantiateModel` + `ApplyMaterial` + `CastShadowsFrom`), VFX prefabs + `VfxManager` registration; called from `NinjagoBuildAll.Run()` |

Outputs: `U/Models/BossBattle/{Actors,Arena}/BB_*.fbx`, `U/Sprites/BossBattle/{Particles,Icons}/*.png`,
`U/Textures/BossBattle/Surfaces/*.png`, `U/Materials/BossBattle/M_BB_*.mat`, `U/Prefabs/Ninjago/BossBattle/**`.
Previews go to the scratchpad (never the repo).

### 9.2 Actor models (ekit, Blender space, front = Unity +Z, pivot at the feet unless noted)
| FBX | Parts (hierarchy) | Tris | Size | Look / notes |
|---|---|---|---|---|
| `BB_Boss` | `Body` (pelvis/legs, root) > `Torso` > `Head` > `Jaw`, `HornL`, `HornR`, `Eyes` (emissive); `Torso` > `ArmL` (shoulder) > `ForearmL` (elbow) > `FistL` (same for R); `Torso` > `WingL`, `WingR` (shoulder-blade pivot, folded in phase 1, spread in phase 2) and `JetL`, `JetR` (emissive exhaust); `Torso` > `ChestPlate`, `PadL`, `PadR` (armour that breaks off at 70/30/5 %); `Torso` > `Core` (emissive weak point, hit target for rockets/lasers) | <= 3000 | 10-12 m tall, wing span ~14 m | Dark villain: indigo/purple/gray 2-6 body, red/magenta emissive eyes, horns and core; brick armour with **studs** on the shoulder pads and fists; gold trim (yellow 9-11). Big fists for slam, throw and clap. 4-level nesting -> **must** use `ekit.export_fbx`. Every armour piece is its own part so the explosion can scatter parts. |
| `BB_Minion` | `Body` > `Head` (+`Eyes` emissive), `ArmL`, `ArmR` > `Weapon`, `Tail` (serpentine variant) | <= 450 | 1.2 m | Start from `models_enemies.skeleton()` (:139): bone gray 11-14, red scarf, dark sockets with red emissive pupils; `tilt_up("Head", 15)` toward the camera. Pivot at the feet; it clings to the car side. |
| `BB_Rock` | `Rock` (+ `Cracks_Emissive` optional) | <= 160 | 1.4 m | `models_bosses.rock(rock_rule, seed, subdiv=1)` (80 tris) + orange emissive crack faces; pivot at the **centre** (it tumbles). 2-3 seeds as variants. |
| `BB_PowerCrate` | `Crate`, `Studs`, `Emblem_Emissive` | <= 300 | 1.2 m cube | Ninjago brick crate: brown/gold box, 2x2 studs on top, glowing rocket emblem (cyan or yellow emissive) on the four sides; pivot at the bottom; spins and bobs in code. |
| `BB_Rocket` | `Body`, `Fins`, `Exhaust_Emissive` | <= 150 | 0.9 m along +Z | lathe body (6 segments) + cone nose + 4 slab fins; red/white with an orange emissive exhaust; pivot at the **centre** (homing rotation). Same orientation as `heroprops.build_cue` (Unity +Z). |
| `BB_SpikeWall` | `Wall`, `Spikes` (child; slides out) | <= 700 | 6 x 4 x 0.8 m | Stacked bricks with studs on top (dark stone gray 3-5 and purple), spikes (4-sided cones, bone/steel gray 11-13) on the inner face (+X); pivot at the bottom centre of the outer face. The right wall is the same mesh rotated 180 deg; the "bigger" second clap scales the root. |
| `BB_WindGust` | `Gust` | <= 200 | 3 m long | `sweep` along a helix, flattened, 6 segments, purple/magenta **emissive** palette faces (opaque, so no overdraw on Mali); plus WindStreak particles. Alternative: unit-UV mesh + the LightShaft-style shader (`env_pieces.light_shaft` :1276 pattern). |
| `BB_LaserBolt` | `Bolt` | <= 48 | 1.5 x 0.15 m | lathe octahedron stretched along +Z, cyan or red emissive; pivot at the centre; with Flash/Spark particles at muzzle and impact. |
| `BB_WarnMarker` | n/a | n/a | n/a | Not a model: a ground decal sprite (Ninjago `Ring` sprite or a pixel ring strip) that follows the car, then locks. |

Contract and budget entries go into `contract_bb.py`. The build fails on names, budget, unbaked transforms or
geometry below the pivot (rocks/rockets/bolts: allow a centred pivot via a per-model flag).

### 9.3 Arena pieces (env_lib, Unity space, `TRI_BUDGET` 800 each)
`BB_Cliff_A/B` (jittered `ico` stacks, `stone_fn` in gray/indigo 2-6, purple 5 moss, optional emissive magenta
cracks), `BB_Ruin_Column` / `BB_Ruin_Arch` (call `env_pieces.ruin_column/ruin_arch` and repaint darker),
`BB_BrickFloor_Surface` (a `_Surface` part with box UVs, `M_Surface_LavaBrick`), `BB_LavaCrack` (palette part
`LavaCrack_Emissive`, orange/red 9-13), `BB_Brazier` (reuse `brazier` :630), `BB_StormCloud` (flat-bottomed ico
clumps in indigo 2-4 for phase 2), `BB_FloatingIsle` (rock + ruins for the sky arena), `BB_RoadSegment_Surface`
(optional upgrade of the primitive road). Statics should be combined per group with
`Nex.BilliardRogue.Editor.StaticMeshCombiner.Combine` (`StaticMeshCombiner.cs:40`) to cut draw calls. God rays:
reuse `Env_LightShaft.fbx` + `M_LightShaft`.

### 9.4 Particle strips (`U/Sprites/BossBattle/Particles/`, 8 frames, 12 fps, white/grey, alpha 0/255, 1 px gutter)
| Sheet | Size | Loop | Source | Use (tint) |
|---|---|---|---|---|
| `Spark` | 16 | no | `mp.spark` | rocket and laser hits, wall scrape (yellow/white HDR) |
| `Dust` | 16 | no | `mp.dust` (`puff`) | rock landing, brick pop-up, car wall bump (warm grey) |
| `Ember` | 16 | yes | `mp.ember` | lava floor, boss aura, rocket trail sparks (orange HDR) |
| `Smoke` | 16 | no | `mp.smoke` (`puff`) | rocket trail, boss parts falling off (dark grey) |
| `WindStreak` | 32x16 frame (non-square) or 32 | yes | **new**: 2-3 horizontal dashes that slide and break into dots | devil wind and the clap gust (pale violet) |
| `Explosion` | 32 | no | **new**: `flash` core -> 3-4 lobe fireball (puff with W/L/M/D bands) -> dissolve into smoke clumps | rocket impact, final boss explosion (orange/red HDR) |
| `Ring` | 32 | no | `mp.ring` | slam shockwave, phase transition (magenta) |
| `Flash` | 32 | no | `mp.flash` | hit core, laser muzzle (glow material) |
| `Debris` | 16 | yes | `mp.debris` | bricks and armour chunks (tint per material) |
| `Bolt` | 32 | yes | `mp.bolt` | boss "tired" crackle, laser charge (cyan) |

`VfxMaterialLibrary` reads only `Assets/Sprites/BilliardRogue/Particles`. Either give it a constructor parameter for
the sheet root, or write the Boss Battle sheets into the Billiard Rogue folder with a `BB_` prefix. The first is
cleaner.

---

## 10. Concrete checklist (Boss Battle art)

1. [ ] Create `Tools/.venv` (section 1) and confirm `import numpy, PIL, scipy, fontTools`.
2. [ ] Add `Tools/Blender/bossbattle/bb_paths.py`; never default to `Starter/`.
3. [ ] `bb_kit.py` (brick + stud helpers on top of ekit); verify stud tri cost (6-segment studs, no bottom cap).
4. [ ] `models_bb_actors.py` + `contract_bb.py`: Boss, Minion, Rock, PowerCrate, Rocket, SpikeWall, WindGust,
   LaserBolt with the parts and budgets of section 9.2.
5. [ ] `bb_build_model.py` + `build_bossbattle.py`: `--python-exit-code 1`, `--factory-startup`,
   `ekit.export_fbx` (nested fix), raw icon + `finalize_icon`, manifest with `pivot_unity` / `bounds_unity`.
6. [ ] Look at every icon and review PNG (Read tool) before committing; rebuild twice -> `fbx_changed=False`.
7. [ ] `models_bb_arena.py` (env_lib, Unity space): cliffs, ruins, lava/brick floor, storm clouds, islands.
8. [ ] `make_particles_bossbattle.py`: new `WindStreak`, `Explosion`; reuse the others; gutter and alpha
   assertions as in `make_particles.main` (:629).
9. [ ] Optional surfaces: `LavaBrick`, `DarkStone` (64 px, OpenGL normals, cavity 0.5 neutral).
10. [ ] Unity: make the `ImportSettingsBuilder.Configure*` methods internal; add `BossBattleImportSettings` roots;
   run it through `unity command eval` (implementation phase only).
11. [ ] Materials: `M_BB_Palette` (ToonLit, `_Bands` 3-5 tuned for the dark arena, emission 1.6),
   `M_BB_Palette_Rage` (recoloured palette PNG, same layout), surface materials, particle materials.
12. [ ] Wrapper prefabs (`InstantiateModel` + `ApplyMaterial` + `CastShadowsFrom` from `Body`/`Torso`); look up
   parts by name (`FindPart`); never reference the raw FBX.
13. [ ] VFX prefabs with pixel strips registered as `VfxManager.VisualEffect` 300-349 (explicit values).
14. [ ] Pixel font for Boss Battle views via a font parameter on the Ninjago `Label/PlainText`; re-run
   `FontAssetsBuilder.Run()` after the localization seeding (CJK atlas is static).
15. [ ] Profile on a low-tier device: boss <= 3000 tris, the whole frame < ~100k tris and < ~150 draw calls, one
   shadow caster per actor.

---

## 11. Gotchas

- Blender exits 0 on Python errors unless `--python-exit-code 1` is passed. The first EEVEE render per process is
  slower (shader compile); it is not a hang.
- Load images with absolute paths (`bpy.data.images.load(os.path.abspath(p), check_existing=True)`); relative
  paths render magenta. Call `bpy.context.view_layer.update()` before reading `matrix_world`.
- `bmesh.ops.create_*` face order varies per run. ekit/hp_mesh build their vertices explicitly, and
  `bc.mesh_object` canonicalizes.
- `bakeAxisConversion` must stay false. Blender default FBX settings give scale 100, rotation 270 and one clip per
  action x object.
- The palette texture must stay point-filtered, uncompressed and clamped (ETC2/ASTC change about 100 % of the
  palette texels). UVs must sit exactly on texel centres; the PNG row 0 is the top.
- Two authoring spaces: ekit = Blender (front -Y), env_lib = Unity (`u2b`). Ninjago primitive actors face
  -Z (chapter) or +Z (vehicles).
- Part names are a contract (prefab overrides and `FindPart`); renaming one breaks references silently. FBX
  fileIDs are derived from node names.
- The `asset-toolchain.md` research (16x16 palette, one 128x128 particle atlas, AssetPostprocessor, M_Palette
  emission 2.2) is **superseded** by the implementation: 32x16 palette with an emissive half, per-effect 8x1
  strips, explicit `ImportSettingsBuilder`, emission 1.6.
- `ImportSettingsBuilder` and `VfxMaterialLibrary` hardcode Billiard Rogue folders. The first import of a new file
  uses Unity defaults (bilinear, compressed) until the settings builder runs.
- `export_fbx_if_changed` compares the embedded texture path, so the first build into a new folder always reports
  `changed`.
- Mali GPUs: large alpha-blended meshes (gust, wind) cost a lot of overdraw. Prefer opaque emissive palette meshes
  and alpha-clipped LitParticle billboards.

## 12. Risks / unverified

- No Boss Battle model was built yet; budgets in section 9.2 are estimates scaled from the Billiard Rogue bosses
  (992-1148 tris at 2 m).
- Whether Boss Battle reuses the Billiard Rogue HD-2D world renderer (640x360 RT) is the rendering note's call.
  The texel densities in section 8 assume it does.
- `GlowSansJ-Normal-Bold` (Japanese) may lack some simplified/traditional Chinese glyphs for zh-Hans/zh-Hant
  (not checked; fontTools is not installed).
- ASTC support on the target device is unverified, but pixel art <= 256 px is uncompressed anyway.
- `game_look.py`, `bl_preview.py` and the environment previews read Billiard Rogue configs from `Starter/...`.
  They need path fixes before they work here (review tooling only, not needed for the FBX output).
