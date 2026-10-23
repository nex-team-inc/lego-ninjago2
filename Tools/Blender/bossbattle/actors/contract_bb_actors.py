"""Part contract for the Boss Battle actor FBX files (TDD 8.1 / 8.2 + the package M brief). Shared by the Blender
build (bl_build_actor.py), the FBX round-trip check (bl_verify_fbx.py) and the orchestrator. Part names are a
contract with BossActor / MinionActor / VehicleRig code and the wrapper prefabs: renaming one breaks them silently.

HIERARCHY = {model: {part: parent or None}}, exactly one root per model.
"""
HIERARCHY = {
    # TDD 8.1: Body > Hips > {Torso > {Head > {Jaw, HornL, HornR, EyeL, EyeR}, ArmL > ForearmL > FistL,
    #          ArmR > ForearmR > FistR, WingL, WingR, JetL, JetR, Core, ChestPlate, PadL, PadR}, LegL, LegR}
    "BB_Boss": {
        "Body": None,
        "Hips": "Body",
        "Torso": "Hips",
        "Head": "Torso",
        "Jaw": "Head", "HornL": "Head", "HornR": "Head", "EyeL": "Head", "EyeR": "Head",
        "ArmL": "Torso", "ForearmL": "ArmL", "FistL": "ForearmL",
        "ArmR": "Torso", "ForearmR": "ArmR", "FistR": "ForearmR",
        "WingL": "Torso", "WingR": "Torso", "JetL": "Torso", "JetR": "Torso",
        "Core": "Torso", "ChestPlate": "Torso", "PadL": "Torso", "PadR": "Torso",
        "LegL": "Hips", "LegR": "Hips",
    },
    # TDD 8.2: Body, ArmL, ArmR, Weapon (the weapon rides on the right arm)
    "BB_Minion": {"Body": None, "ArmL": "Body", "ArmR": "Body", "Weapon": "ArmR"},
    "BB_NinjaCar": {"Body": None, "WheelFL": "Body", "WheelFR": "Body", "WheelRL": "Body", "WheelRR": "Body"},
    "BB_NinjaJet": {"Body": None, "WingL": "Body", "WingR": "Body", "Engine": "Body", "Cannon": "Body"},
}

BUDGETS = {"BB_Boss": 3500, "BB_Minion": 500, "BB_NinjaCar": 1400, "BB_NinjaJet": 1000}

# expected overall size (Unity metres, x = width, y = height, z = length) and tolerance, checked on the build
SIZE = {
    "BB_Boss": ((None, 11.0, None), 0.6),      # ~11 m tall (horn tips)
    "BB_Minion": ((None, 1.0, None), 0.15),    # ~1 m tall
    "BB_NinjaCar": ((1.6, 0.9, 2.4), 0.2),     # Chest Chase car footprint
    "BB_NinjaJet": ((1.8, None, 2.1), 0.15),   # skycraft footprint: 1.8 m span, 2.1 m long
}

FACING = {"BB_Boss": "-Z", "BB_Minion": "-Z", "BB_NinjaCar": "+Z", "BB_NinjaJet": "+Z"}
PIVOT = {"BB_Boss": "feet", "BB_Minion": "feet", "BB_NinjaCar": "bottom-centre", "BB_NinjaJet": "centre"}

# TDD 8.1: only these boss parts cast shadows (wrapper prefab: every other renderer ShadowCastingMode.Off)
SHADOW_CASTERS = {
    "BB_Boss": ["Hips", "Torso", "ArmL", "ArmR", "LegL", "LegR"],
    "BB_Minion": ["Body"],
    "BB_NinjaCar": ["Body"],
    "BB_NinjaJet": ["Body"],
}

MODELS = list(HIERARCHY)
