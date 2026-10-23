"""Path constants for the Boss Battle actor models (package M, part 1). Imported by Blender scripts AND the venv
orchestrator, so it only uses the standard library.

Never defaults to the old Billiard Rogue `Starter/` project or `/Users/simonbut` paths: everything is derived from
this file's location inside the repo (`<repo>/Tools/Blender/bossbattle/actors/bb_paths.py`).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TOOLS_BLENDER = os.path.join(REPO, "Tools", "Blender")
ENEMIES_KIT = os.path.join(TOOLS_BLENDER, "enemies")          # ekit.py, bl_look.py, game_look.py
UNITY = os.path.join(REPO, "ninjago-unity")
UNITY_ASSETS = os.path.join(UNITY, "Assets")

BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
VENV_PYTHON = os.path.join(REPO, "Tools", ".venv", "bin", "python")

# shared Billiard Rogue palette (32 x 16, right half = emissive) - read only, never modified
PALETTE_DIR = os.path.join(UNITY_ASSETS, "Textures", "BilliardRogue", "Palette")
PALETTE_JSON = os.path.join(PALETTE_DIR, "palette.json")
PALETTE_PNG = os.path.join(PALETTE_DIR, "Palette_Main.png")
PALETTE_EMISSION_PNG = os.path.join(PALETTE_DIR, "Palette_Emission.png")

# outputs (TDD 8.1 / 8.2, ownership TDD 10: package M owns Models/Ninjago/BossBattle/**)
MODELS_OUT = os.path.join(UNITY_ASSETS, "Models", "Ninjago", "BossBattle", "Actors")
PORTRAIT_OUT = os.path.join(UNITY_ASSETS, "Sprites", "BossBattle", "Hud", "BossPortrait.png")
MANIFEST = os.path.join(HERE, "bb_actors_manifest.json")

# review renders (never game assets): the session scratchpad when the caller sets BB_PREVIEW_DIR, else /tmp
PREVIEW_DIR = os.environ.get("BB_PREVIEW_DIR") or os.path.join("/tmp", "bossbattle_previews", "actors")


def fbx_path(model):
    return os.path.join(MODELS_OUT, model + ".fbx")
