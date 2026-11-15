"""Poses a character that came without a skeleton (mastjie's low-poly Cowboy, CC0), for model_import.py --autopose.

A small skeleton is laid into the figure from its own proportions (measured on the model standing in its A-pose, 1.75
tall, facing -Y), every vertex is bound to its nearest one or two bones (by distance to the bone's surface, so the
chest stays with the spine and not the arm), and the pose is applied straight to the vertices: the game draws people
as plain meshes, one per stride or pose.

Poses: idle, walk0..walk3 (a stride in four), ride (astride a horse, reins in hand), drive (on a wagon seat),
carry0..carry3 (striding with a sledgehammer on the shoulder), pump (both hands on a handcar's bar), aim (a rifle
shouldered).
"""
import math

from mathutils import Matrix, Quaternion, Vector

H = 1.75
# name: (parent, head, tail, radius, side). Joints measured on the model (see the module doc); s mirrors left/right.
def _bones():
    b = {"pelvis": (None, (0, -0.02, 0.92), (0, -0.02, 1.05), 0.17, 0),
         "spine": ("pelvis", (0, -0.02, 1.0), (0, -0.02, 1.42), 0.17, 0),
         "head": ("spine", (0, -0.03, 1.46), (0, -0.04, 1.75), 0.11, 0)}
    for s, n in ((1, "L"), (-1, "R")):
        b["uarm" + n] = ("spine", (s * 0.20, -0.01, 1.39), (s * 0.35, -0.05, 1.17), 0.06, s)
        b["farm" + n] = ("uarm" + n, (s * 0.35, -0.05, 1.17), (s * 0.46, -0.16, 1.09), 0.05, s)
        b["hand" + n] = ("farm" + n, (s * 0.46, -0.16, 1.09), (s * 0.55, -0.32, 0.96), 0.05, s)
        b["thigh" + n] = ("pelvis", (s * 0.10, -0.02, 0.92), (s * 0.158, -0.01, 0.50), 0.09, s)
        b["shin" + n] = ("thigh" + n, (s * 0.158, -0.01, 0.50), (s * 0.20, -0.01, 0.12), 0.07, s)
        b["foot" + n] = ("shin" + n, (s * 0.20, -0.01, 0.12), (s * 0.20, -0.17, 0.03), 0.06, s)
    return b


B = _bones()
ORDER = ["pelvis", "spine", "head"] + [p + n for n in "LR" for p in ("uarm", "farm", "hand", "thigh", "shin", "foot")]


def _seg(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def _allowed(name, p):
    s = B[name][4]
    if name.startswith(("uarm", "farm", "hand")):
        return p.x * s > 0.17 and 0.85 < p.z < 1.5
    if name.startswith(("thigh", "shin", "foot")):
        return p.z < 0.97 and p.x * s > -0.02
    if name == "head":
        return p.z > 1.40
    return True


def weights(p):
    """The one or two bones a point follows, with how much."""
    d = []
    for n in ORDER:
        if not _allowed(n, p):
            continue
        par, h, t, r, s = B[n]
        d.append((max(0.0, _seg(p, Vector(h), Vector(t)) - r), n))
    d.sort()
    if len(d) == 1 or d[1][0] - d[0][0] > 0.08:
        return [(d[0][1], 1.0)]
    a, b = math.exp(-d[0][0] / 0.025), math.exp(-d[1][0] / 0.025)
    return [(d[0][1], a / (a + b)), (d[1][1], b / (a + b))]


def _rot(x=0.0, y=0.0, z=0.0):
    """Degrees about the figure's axes: X across the body (+ swings a limb backward), Y front to back, Z up."""
    return (Quaternion((0, 0, 1), math.radians(z)) @ Quaternion((0, 1, 0), math.radians(y)) @ Quaternion((1, 0, 0), math.radians(x)))


def _arms_down(q, deg=32):
    # The A-pose arms hang out at about 45 degrees: bring them in towards the sides (each side its own way).
    q["uarmL"] = _rot(y=deg) @ q.get("uarmL", Quaternion())
    q["uarmR"] = _rot(y=-deg) @ q.get("uarmR", Quaternion())


def pose_angles(name):
    q = {}
    if name == "idle":
        _arms_down(q, 34)
        q["farmL"] = _rot(x=-12); q["farmR"] = _rot(x=-12)
    elif name.startswith("walk"):
        ph = int(name[4:]) / 4.0
        sw = math.sin(ph * 2 * math.pi)
        for n, s in (("L", 1), ("R", -1)):
            swing = 26 * sw * s          # + forward
            q["thigh" + n] = _rot(x=-swing)
            back = max(0.0, -swing)       # the trailing leg bends at the knee, the forward one a little
            q["shin" + n] = _rot(x=10 + back * 1.1)
            q["foot" + n] = _rot(x=-back * 0.3)
            q["uarm" + n] = _rot(x=swing * 0.8)
            q["farm" + n] = _rot(x=-18)
        _arms_down(q, 34)
        q["spine"] = _rot(x=-4)
    elif name == "ride":
        # Astride: thighs forward and out round the horse, shins hanging down, hands forward at the reins.
        for n, s in (("L", 1), ("R", -1)):
            q["thigh" + n] = _rot(y=-s * 30) @ _rot(x=-42)
            q["shin" + n] = _rot(x=48)
            q["foot" + n] = _rot(x=-10)
            q["uarm" + n] = _rot(x=-38) @ _rot(y=s * 30)
            q["farm" + n] = _rot(x=-40) @ _rot(z=s * 18)
        q["spine"] = _rot(x=-8)
    elif name == "drive":
        for n, s in (("L", 1), ("R", -1)):
            q["thigh" + n] = _rot(y=-s * 8) @ _rot(x=-85)
            q["shin" + n] = _rot(x=85)
            q["uarm" + n] = _rot(x=-30) @ _rot(y=s * 32)
            q["farm" + n] = _rot(x=-45) @ _rot(z=s * 20)
        q["spine"] = _rot(x=-6)
    elif name == "pump":
        # Standing at a handcar's bar, both hands on it in front of the chest.
        for n, s in (("L", 1), ("R", -1)):
            q["thigh" + n] = _rot(x=-12 if s > 0 else 8)
            q["shin" + n] = _rot(x=18)
            q["uarm" + n] = _rot(x=-55) @ _rot(y=s * 38)
            q["farm" + n] = _rot(x=-35) @ _rot(z=s * 35)
        q["spine"] = _rot(x=-14)
    elif name.startswith("carry"):
        # Striding with a sledgehammer over the right shoulder: that hand up at the shoulder, the other arm swinging.
        ph = int(name[5:] or 0) / 4.0
        sw = math.sin(ph * 2 * math.pi)
        for n, s in (("L", 1), ("R", -1)):
            swing = 24 * sw * s
            q["thigh" + n] = _rot(x=-swing)
            q["shin" + n] = _rot(x=10 + max(0.0, -swing) * 1.1)
            q["foot" + n] = _rot(x=-max(0.0, -swing) * 0.3)
        q["uarmL"] = _rot(x=-24 * sw * 0.8) @ _rot(y=34)
        q["farmL"] = _rot(x=-18)
        q["uarmR"] = _rot(x=-40) @ _rot(y=-40)
        q["farmR"] = _rot(x=-135)
        q["spine"] = _rot(x=-6)
    elif name == "aim":
        # Shouldering a rifle: both arms up level in front, the left reaching further along the barrel.
        _arms_down(q, 34)
        q["uarmR"] = _rot(x=-78) @ q["uarmR"]
        q["farmR"] = _rot(z=-35)
        q["uarmL"] = _rot(x=-85) @ _rot(z=-20) @ q["uarmL"]
        q["farmL"] = _rot(z=-25)
        q["head"] = _rot(z=-10)
    else:
        raise SystemExit("autorig: no pose " + name)
    return q


def matrices(q):
    m = {}
    for n in ORDER:
        par, h, t, r, s = B[n]
        # Each bone turns about its joint where the parent has already carried it, round the figure's own axes: an
        # elbow bends the forearm the same way whichever way the upper arm was swung.
        up = m[par] if par else Matrix.Identity(4)
        h = up @ Vector(h)
        m[n] = Matrix.Translation(h) @ q.get(n, Quaternion()).to_matrix().to_4x4() @ Matrix.Translation(-h) @ up
    return m


def pose(meshes, name):
    """Poses the meshes in place; returns the rest bounds (min, max) in world space, so the caller can size the figure
    by its standing height and keep its feet's rest level as the origin whatever the pose."""
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    k = H / (mx.z - mn.z)
    cx = (mn.x + mx.x) / 2
    mats = matrices(pose_angles(name))
    for o in meshes:
        mw = o.matrix_world.copy(); inv = mw.inverted()
        part = PARTS.setdefault(o.name, {})
        for v in o.data.vertices:
            w = mw @ v.co
            p = Vector(((w.x - cx) * k, w.y * k, (w.z - mn.z) * k))
            part[v.index] = region(p)
            if part[v.index] == "glove":
                # Closed fists: the fingers are drawn in towards the palm (they are a third of the model's faces,
                # and spread out they only come apart when the figure is slimmed).
                s = 1 if p.x > 0 else -1
                c = Vector((s * 0.49, -0.21, 1.04))
                p = c + (p - c) * 0.5
            out = Vector()
            for n, f in weights(p):
                out += (mats[n] @ p) * f
            v.co = inv @ Vector((out.x / k + cx, out.y / k, out.z / k + mn.z))
    return mn, mx



# ---- clothes ----
# The model paints itself from a 2x2 texture, so its colours come out as blends with stripes across the faces. Each
# part is painted one flat colour instead, chosen by where it is on the standing figure (and, inside the head and the
# torso, by whether the model drew it light or dark: face or hat, shirt or vest); an outfit gives every part its colour.
PARTS = {}


def region(p):
    ax = abs(p.x)
    if p.z > 1.44 and ax < 0.36:
        if p.z > 1.66 or ax > 0.13:
            return "hat"
        if 1.485 < p.z < 1.565 and p.y < -0.03:
            return "mask"
        return "head"
    if ax > 0.17 and 0.85 < p.z < 1.5:
        return "glove" if ax > 0.43 else "sleeve"
    if p.z >= 1.03:
        return "torso"
    if p.z >= 0.93:
        return "belt"
    if p.z < 0.15:
        return "boots"
    return "pants"


def _lin(h):
    return tuple(((int(h[i:i + 2], 16) / 255) / 12.92) if int(h[i:i + 2], 16) / 255 <= 0.04045 else (((int(h[i:i + 2], 16) / 255) + 0.055) / 1.055) ** 2.4 for i in (0, 2, 4))


# hat, skin, shirt, vest, pants, boots, mask (bandana), belt
OUTFITS = [      # bright enough to read from the camera's height: dark clothes went black against the ground
    ("8a5a34", "e8b08a", "eadcb8", "7a4a2a", "4f6a9a", "4a3324", "c8352a", "5a3a22"),
    ("3a3532", "d09670", "c23a2c", "4a4440", "a27a50", "3a2c22", "3a3532", "4a3324"),
    ("a89a88", "eebd9c", "5f86c4", "8f5e38", "5a5048", "4a3324", "e0cc9a", "5a3a22"),
    ("c49a5e", "a06a48", "86a04a", "5a4430", "8a8a90", "3a2c22", "a8302c", "4a3324"),
    ("4a403a", "e0a682", "b0a898", "86663e", "9a6a3e", "3a2c22", "3f5a8a", "5a3a22"),
    ("6a4a30", "c48a62", "963044", "b09868", "4a5a7c", "4a3324", "3a3532", "4a3324"),
    ("2c2a2a", "e0a682", "3c5a8c", "2a2624", "6c6c70", "2a1c14", "e0a682", "2b1d14"),     # 6: the train's own riflemen, no bandana
]


def recolour(meshes, outfit, unused=None):
    hat, skin, shirt, vest, pants, boots, mask, belt = (_lin(h) for h in OUTFITS[outfit % len(OUTFITS)])
    for o in meshes:
        part = PARTS.get(o.name)
        col = o.data.color_attributes.get("Col")
        if not part or col is None:
            continue
        loops = o.data.loops
        for li, d in enumerate(col.data):
            r = part.get(loops[li].vertex_index, "pants")
            light = sum(d.color[:3]) > 0.9
            c = {"hat": hat, "head": skin if light else hat, "mask": mask if light else hat, "torso": shirt if light else vest,
                 "sleeve": shirt, "glove": (0.13, 0.08, 0.05), "belt": belt, "pants": pants, "boots": boots}[r]
            d.color = c + (1,)
