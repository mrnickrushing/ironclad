#!/usr/bin/env python3
"""Converts the free kit models the game uses into Assets/Resources/Models/<id>.fbx (vertex-coloured, one unit on
their longest side, or one unit tall): python3 Tools/art/models.py [id...]   (fetch kits with kenney.py first:
watercraft-kit car-kit city-kit-industrial mini-characters pirate-kit survival-kit nature-kit)"""
import os, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
F = os.path.join(ROOT, "art-src", "free")
G = "/Models/GLB format/"
TRN, TD, MINI, PIR, SUR, NAT = "train-kit" + G, "tower-defense-kit" + G, "mini-characters" + G, "pirate-kit" + G, "survival-kit" + G, "nature-kit/Models/GLTF format/"
MODELS = {  # id: (source, "size" | "height"[, pose, extra args...])
    "loco": (TRN + "train-locomotive-a.glb", "size"), "loco_b": (TRN + "train-locomotive-b.glb", "size"), "loco_c": (TRN + "train-locomotive-c.glb", "size"),
    "flatbed": (TRN + "train-carriage-flatbed.glb", "size"), "flatbed_wood": (TRN + "train-carriage-flatbed-wood.glb", "size"),
    "boxcar": (TRN + "train-carriage-box.glb", "size"), "coalcar": (TRN + "train-carriage-coal.glb", "size"), "tankcar": (TRN + "train-carriage-tank.glb", "size"),
    "woodcar": (TRN + "train-carriage-wood.glb", "size"), "lumbercar": (TRN + "train-carriage-lumber.glb", "size"), "dirtcar": (TRN + "train-carriage-dirt.glb", "size"),
    "rail": (TRN + "railroad-straight.glb", "size"), "handcar_base": (TRN + "train-carriage-flatbed.glb", "size"),
    "turret": (TD + "weapon-turret.glb", "size"), "cannon": (TD + "weapon-cannon.glb", "size"), "ballista": (TD + "weapon-ballista.glb", "size"), "catapult": (TD + "weapon-catapult.glb", "size"),
    "ship_cannon": (PIR + "cannon.glb", "size"), "barrel": (PIR + "barrel.glb", "height"), "crate": (PIR + "crate.glb", "size"), "chest": (PIR + "chest.glb", "size"),
    "tower": (PIR + "tower-watch.glb", "height"), "flag": (PIR + "flag-high-pennant.glb", "height"),
    "box": (SUR + "box.glb", "size"), "signpost": (SUR + "signpost.glb", "height"), "tent": (NAT + "tent_detailedOpen.glb", "size"), "campfire": (NAT + "campfire_stones.glb", "size"),
    "tree_a": (NAT + "tree_default.glb", "height"), "tree_oak": (NAT + "tree_oak.glb", "height"), "tree_pine": (NAT + "tree_pineRoundA.glb", "height"), "tree_pine_b": (NAT + "tree_pineTallA.glb", "height"),
    "cactus_a": (NAT + "cactus_tall.glb", "height"), "cactus_b": (NAT + "cactus_short.glb", "height"),
    "bush": (NAT + "plant_bushLarge.glb", "size"), "grass": (NAT + "grass_large.glb", "size"), "flowers": (NAT + "flower_yellowA.glb", "size"),
    "rock_a": (NAT + "stone_largeA.glb", "size"), "rock_b": (NAT + "stone_largeC.glb", "size"), "rock_c": (NAT + "stone_tallB.glb", "size"), "rock_d": (NAT + "stone_largeB.glb", "size"), "rock_e": (NAT + "stone_tallA.glb", "size"),
    "cliff": (NAT + "cliff_block_rock.glb", "size"), "fence": (NAT + "fence_simple.glb", "size"), "stump": (NAT + "stump_old.glb", "size"), "log_stack": (NAT + "log_stackLarge.glb", "size"),
}
# Raiders: kit people, each frozen in four strides and standing.
for i, k in enumerate(["character-male-a", "character-male-b", "character-male-c", "character-male-d", "character-male-e", "character-male-f"]):
    for tag, pose in (("w0", "walk:f0"), ("w1", "walk:f0.25"), ("w2", "walk:f0.5"), ("w3", "walk:f0.75"), ("idle", "idle:f0")):
        MODELS["man_%d_%s" % (i, tag)] = (MINI + k + ".glb", "height", pose)
# Single models from poly.pizza (CC0 or CC BY, see art-src/free/props/manifest.json), fetched by props.py.
P = "props/"
for mid, mode in (("horse_f", "size"), ("horse_e", "size"), ("west_cart", "size"), ("wagon_c", "size"), ("carriage", "size"), ("cart", "size"), ("cowboy", "height"),
                  ("saloon", "size"), ("water_tower_c", "height"), ("windmill", "height"), ("dynamite", "height"), ("gatling", "size"), ("big_cannon", "size"), ("minecart", "size")):
    MODELS[mid] = (P + mid + ".glb", mode)
out = os.path.join(ROOT, "Assets", "Resources", "Models")
os.makedirs(out, exist_ok=True)
for mid in (sys.argv[1:] or list(MODELS)):
    src, mode = MODELS[mid][0], MODELS[mid][1]
    extra = ((["--pose", MODELS[mid][2]] if MODELS[mid][2] else []) + list(MODELS[mid][3:])) if len(MODELS[mid]) > 2 else []
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "model_import.py"), "--", os.path.join(F, src), os.path.join(out, mid + ".fbx"),
                        "--" + mode, "1"] + extra, capture_output=True, text=True)
    line = [l for l in r.stdout.splitlines() if "[free]" in l]
    print(mid, line[0].split("faces")[1] if line else "FAILED " + (r.stdout + r.stderr)[-300:])
