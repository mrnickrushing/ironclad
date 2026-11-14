#!/usr/bin/env python3
"""The app icon: the locomotive coming out of a frontier sunset, in the game's sticker look (dark outline, hard drop).
The engine is the free Kenney model rendered by Tools/blender/icon_render.py.
  python3 -I Tools/art/app_icon.py        ->  Assets/Icon/icon.png (1024, no transparency, as the App Store wants)
"""
import json, math, os, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Assets", "Icon", "icon.png")
N, SS = 1024, 2
W = N * SS
INK = (46, 27, 18, 255)


def engine():
    tmp = tempfile.mkdtemp()
    out = os.path.join(tmp, "loco.png")
    src = os.path.join(ROOT, "art-src", "free", "train-kit", "Models", "GLB format", "train-locomotive-b.glb")
    json.dump([dict(name="loco", files=[src], out=out, yaw=62, pitch=12, size=1500)], open(os.path.join(tmp, "j.json"), "w"))
    subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "icon_render.py"), "--", os.path.join(tmp, "j.json")],
                   capture_output=True, text=True, check=True)
    im = Image.open(out).convert("RGBA")
    # The render's soft shadow on the floor is half-clear: keep only the model itself.
    a = im.split()[3].point(lambda v: 255 if v >= 235 else 0)
    im.putalpha(a)
    return im.crop(im.getbbox())


def outlined(im, grow, drop):
    """The picture with a dark outline round it and a hard shadow below."""
    a = im.split()[3]
    fat = a.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    pad = grow + drop + 4
    out = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    ink = Image.new("RGBA", im.size, INK)
    shadow = Image.new("RGBA", im.size, INK[:3] + (110,))
    out.paste(shadow, (pad, pad + drop), fat)
    out.paste(ink, (pad, pad), fat)
    out.alpha_composite(im, (pad, pad))
    return out


im = Image.new("RGBA", (W, W), (0, 0, 0, 255))
d = ImageDraw.Draw(im)
# Sky: deep orange at the top to gold at the horizon.
top, low = (214, 84, 40), (255, 190, 74)
horizon = int(W * 0.66)
for y in range(horizon):
    k = y / horizon
    d.line([(0, y), (W, y)], fill=tuple(int(top[i] + (low[i] - top[i]) * k ** 1.3) for i in range(3)) + (255,))
# Rays from the sun.
rays = Image.new("RGBA", (W, W), (0, 0, 0, 0)); rd = ImageDraw.Draw(rays)
cx, cy = W * 0.5, horizon
for i in range(14):
    a0 = math.pi + i * math.pi / 14 + 0.02; a1 = a0 + math.pi / 28
    rd.polygon([(cx, cy), (cx + math.cos(a0) * W * 1.5, cy + math.sin(a0) * W * 1.5), (cx + math.cos(a1) * W * 1.5, cy + math.sin(a1) * W * 1.5)], fill=(255, 226, 150, 70))
im.alpha_composite(rays)
d = ImageDraw.Draw(im)
r = int(W * 0.27)
d.ellipse([cx - r - 14, cy - r - 14, cx + r + 14, cy + r + 14], fill=INK)
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 236, 170, 255))
# Mesas on the horizon, then the ground.
mesa = (150, 62, 40, 255)
for x0, w, h in ((-0.05, 0.32, 0.13), (0.2, 0.16, 0.07), (0.72, 0.36, 0.16), (0.6, 0.14, 0.08)):
    x = x0 * W
    d.polygon([(x, horizon), (x + w * W * 0.12, horizon - h * W), (x + w * W * 0.88, horizon - h * W), (x + w * W, horizon)], fill=mesa)
d.rectangle([0, horizon - 8, W, horizon + 6], fill=INK)
d.rectangle([0, horizon + 6, W, W], fill=(120, 60, 36, 255))
d.rectangle([0, horizon + 6, W, horizon + int(W * 0.05)], fill=(160, 84, 46, 255))
# The track, coming toward the viewer.
for y, w in ((0.86, 26), (0.93, 30)):
    d.rectangle([0, int(W * y), W, int(W * y) + w], fill=INK)
for i in range(-2, 16):
    x = i * W * 0.085
    d.polygon([(x, W * 0.84), (x + W * 0.035, W * 0.84), (x + W * 0.01, W * 0.975), (x - W * 0.025, W * 0.975)], fill=(84, 48, 30, 255))
for y in (0.868, 0.938):
    d.rectangle([0, int(W * y), W, int(W * y) + 14], fill=(196, 204, 214, 255))
# The engine.
loco = engine()
scale = W * 0.9 / loco.width
loco = loco.resize((int(loco.width * scale), int(loco.height * scale)), Image.LANCZOS)
st = outlined(loco, 16, 20)
im.alpha_composite(st, (int((W - st.width) / 2 + W * 0.01), int(W * 0.93 - st.height + 30)))
# Steam.
d = ImageDraw.Draw(im)
for (x, y, rr) in ((0.66, 0.15, 0.07), (0.55, 0.095, 0.058), (0.46, 0.06, 0.042)):
    d.ellipse([W * x - W * rr - 12, W * y - W * rr - 12, W * x + W * rr + 12, W * y + W * rr + 12], fill=INK)
for (x, y, rr) in ((0.66, 0.15, 0.07), (0.55, 0.095, 0.058), (0.46, 0.06, 0.042)):
    d.ellipse([W * x - W * rr, W * y - W * rr, W * x + W * rr, W * y + W * rr], fill=(255, 248, 232, 255))
im.convert("RGB").resize((N, N), Image.LANCZOS).save(OUT)
print("icon", OUT)
