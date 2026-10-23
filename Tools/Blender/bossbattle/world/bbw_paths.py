"""Path constants for the Boss Battle world props + arena (package M, part 2). Imported by the Blender scripts AND
the venv orchestrator, so it only uses the standard library.

Never defaults to the old Billiard Rogue `Starter/` project or `/Users/simonbut` paths: everything is derived from
this file's location inside the repo (`<repo>/Tools/Blender/bossbattle/world/bbw_paths.py`).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TOOLS_BLENDER = os.path.join(REPO, "Tools", "Blender")
ENEMIES_KIT = os.path.join(TOOLS_BLENDER, "enemies")          # ekit.py (Geo, primitives, Model, export_fbx), game_look.py
ENV_KIT = os.path.join(TOOLS_BLENDER, "environment")          # env_lib.py (hash01, Rng), render_common.py
UNITY = os.path.join(REPO, "ninjago-unity")
UNITY_ASSETS = os.path.join(UNITY, "Assets")

BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
VENV_PYTHON = os.path.join(REPO, "Tools", ".venv", "bin", "python")

# shared Billiard Rogue palette (32 x 16, right half = emissive) - read only, never modified
PALETTE_DIR = os.path.join(UNITY_ASSETS, "Textures", "BilliardRogue", "Palette")
PALETTE_JSON = os.path.join(PALETTE_DIR, "palette.json")
PALETTE_PNG = os.path.join(PALETTE_DIR, "Palette_Main.png")
PALETTE_EMISSION_PNG = os.path.join(PALETTE_DIR, "Palette_Emission.png")

# outputs (TDD 8.2, ownership TDD 10: package M owns Models/Ninjago/BossBattle/**)
MODELS_OUT = os.path.join(UNITY_ASSETS, "Models", "Ninjago", "BossBattle", "World")
ACTORS_OUT = os.path.join(UNITY_ASSETS, "Models", "Ninjago", "BossBattle", "Actors")   # part 1 (read only here)
MANIFEST = os.path.join(HERE, "bbw_world_manifest.json")
LAYOUT = os.path.join(HERE, "bbw_layout.json")

# review renders (never game assets): the session scratchpad when the caller sets BBW_PREVIEW_DIR, else the
# gitignored Tools/Staging mirror
PREVIEW_DIR = os.environ.get("BBW_PREVIEW_DIR") or os.path.join(REPO, "Tools", "Staging", "Previews", "BossBattleWorld")


def fbx_path(model):
    return os.path.join(MODELS_OUT, model + ".fbx")
