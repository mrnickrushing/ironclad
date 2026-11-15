#!/usr/bin/env python3
"""Converts the free kit models the game uses into Assets/Resources/Models/<id>.fbx (vertex-coloured, one unit on
their longest side, or one unit tall): python3 Tools/art/models.py [id...]   (fetch kits with kenney.py first:
watercraft-kit car-kit city-kit-industrial city-kit-roads pirate-kit survival-kit nature-kit)"""
import os, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
F = os.path.join(ROOT, "art-src", "free")
G = "/Models/GLB format/"
TRN, TD, PIR, SUR, NAT = "train-kit" + G, "tower-defense-kit" + G, "pirate-kit" + G, "survival-kit" + G, "nature-kit/Models/GLTF format/"
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
    "pole": ("city-kit-roads" + G + "electricity-pole-single.glb", "height"),
    "cliff": (NAT + "cliff_block_rock.glb", "size"), "fence": (NAT + "fence_simple.glb", "size"), "stump": (NAT + "stump_old.glb", "size"), "log_stack": (NAT + "log_stackLarge.glb", "size"),
}
# Raiders: mastjie's Cowboy (CC0), posed by Tools/blender/autorig.py and dressed in six outfits (a bandana over each
# face): standing, four strides, riding, on a wagon seat, at a handcar's bar; the sledge men stride with a hammer.
for i in range(6):
    for tag, pose in (("idle", "idle"), ("w0", "walk0"), ("w1", "walk1"), ("w2", "walk2"), ("w3", "walk3"), ("ride", "ride"), ("drive", "drive"), ("pump", "pump")):
        MODELS["man_%d_%s" % (i, tag)] = ("props/cowboy.glb", "height", None, "--autopose", pose, "--outfit", str(i), "--maxfaces", "1800")
for i in (1, 3):
    for f in range(4):
        MODELS["man_%d_c%d" % (i, f)] = ("props/cowboy.glb", "height", None, "--autopose", "carry%d" % f, "--outfit", str(i), "--maxfaces", "1800")
MODELS["carriage"] = ("props/carriage.glb", "size", None, "--swap", "0.9,0.05,0.03=0.2,0.085,0.04;0.83,0.89,0.87=0.035,0.028,0.022")   # bay horses, dark manes
MODELS["guard_aim"] = ("props/cowboy.glb", "height", None, "--autopose", "aim", "--outfit", "6", "--maxfaces", "1800")    # the rifle wagon's crew
MODELS["sledgehammer"] = (SUR + "tool-hammer.glb", "height")
# Single models from poly.pizza (CC0 or CC BY, see art-src/free/props/manifest.json), fetched by props.py.
P = "props/"
for mid, mode in (("horse_f", "size"), ("horse_e", "size"), ("west_cart", "size"), ("wagon_c", "size"), ("carriage", "size"), ("cart", "size"),
                  ("saloon", "size"), ("water_tower_c", "height"), ("windmill", "height"), ("dynamite", "height"), ("gatling", "size"), ("big_cannon", "size"), ("minecart", "size")):
    MODELS.setdefault(mid, (P + mid + ".glb", mode))
out = os.path.join(ROOT, "Assets", "Resources", "Models")
os.makedirs(out, exist_ok=True)
for mid in (sys.argv[1:] or list(MODELS)):
    src, mode = MODELS[mid][0], MODELS[mid][1]
    extra = ((["--pose", MODELS[mid][2]] if MODELS[mid][2] else []) + list(MODELS[mid][3:])) if len(MODELS[mid]) > 2 else []
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "model_import.py"), "--", os.path.join(F, src), os.path.join(out, mid + ".fbx"),
                        "--" + mode, "1"] + extra, capture_output=True, text=True)
    line = [l for l in r.stdout.splitlines() if "[free]" in l]
    print(mid, line[0].split("faces")[1] if line else "FAILED " + (r.stdout + r.stderr)[-300:])
