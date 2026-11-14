#!/usr/bin/env python3
"""The loading screen's picture: the train crossing a frontier sunset, 16:9, in the icon's sticker look. The engine and
wagons are the free Kenney models rendered by Tools/blender/icon_render.py.
  python3 -I Tools/art/hero_art.py        ->  Assets/Resources/UI/Loading/hero.jpg
"""
import json, math, os, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Loading", "hero.jpg")
W, H = 3840, 2160
INK = (46, 27, 18, 255)
KIT = os.path.join(ROOT, "art-src", "free", "train-kit", "Models", "GLB format")


def render(names):
    tmp = tempfile.mkdtemp(); jobs = []
    for n in names:
        jobs.append(dict(name=n, files=[os.path.join(KIT, n + ".glb")], out=os.path.join(tmp, n + ".png"), yaw=90, pitch=8, size=1200))
    json.dump(jobs, open(os.path.join(tmp, "j.json"), "w"))
    subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "icon_render.py"), "--", os.path.join(tmp, "j.json")],
                   capture_output=True, text=True, check=True)
    out = []
    for n in names:
        im = Image.open(os.path.join(tmp, n + ".png")).convert("RGBA")
        im.putalpha(im.split()[3].point(lambda v: 255 if v >= 235 else 0))
        out.append(im.crop(im.getbbox()))
    return out


def outlined(im, grow, drop):
    a = im.split()[3]; fat = a.filter(ImageFilter.MaxFilter(grow * 2 + 1)); pad = grow + drop + 4
    out = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", im.size, INK[:3] + (110,)), (pad, pad + drop), fat)
    out.paste(Image.new("RGBA", im.size, INK), (pad, pad), fat)
    out.alpha_composite(im, (pad, pad))
    return out


im = Image.new("RGBA", (W, H), (0, 0, 0, 255)); d = ImageDraw.Draw(im)
top, low = (196, 70, 36), (255, 186, 78)
horizon = int(H * 0.74)
for y in range(horizon):
    k = y / horizon
    d.line([(0, y), (W, y)], fill=tuple(int(top[i] + (low[i] - top[i]) * k ** 1.25) for i in range(3)) + (255,))
cx, cy = W * 0.72, horizon
rays = Image.new("RGBA", (W, H), (0, 0, 0, 0)); rd = ImageDraw.Draw(rays)
for i in range(18):
    a0 = math.pi + i * math.pi / 18 + 0.01; a1 = a0 + math.pi / 36
    rd.polygon([(cx, cy), (cx + math.cos(a0) * W * 1.6, cy + math.sin(a0) * W * 1.6), (cx + math.cos(a1) * W * 1.6, cy + math.sin(a1) * W * 1.6)], fill=(255, 226, 150, 60))
im.alpha_composite(rays); d = ImageDraw.Draw(im)
r = int(H * 0.24)
d.ellipse([cx - r - 16, cy - r - 16, cx + r + 16, cy + r + 16], fill=INK)
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 236, 170, 255))
for x0, w, h, col in ((-0.04, 0.3, 0.2, (150, 62, 40)), (0.2, 0.14, 0.11, (166, 74, 44)), (0.34, 0.2, 0.15, (150, 62, 40)), (0.84, 0.22, 0.17, (150, 62, 40)), (0.6, 0.1, 0.07, (166, 74, 44))):
    x = x0 * W
    d.polygon([(x, horizon), (x + w * W * 0.1, horizon - h * H), (x + w * W * 0.9, horizon - h * H), (x + w * W, horizon)], fill=col + (255,))
d.rectangle([0, horizon - 10, W, horizon + 8], fill=INK)
d.rectangle([0, horizon + 8, W, H], fill=(118, 58, 34, 255))
d.rectangle([0, horizon + 8, W, horizon + int(H * 0.05)], fill=(160, 84, 46, 255))
# The track.
ty = int(H * 0.86)
for i in range(-1, 60):
    x = i * W * 0.02
    d.rectangle([x, ty - 6, x + W * 0.008, ty + 50], fill=(84, 48, 30, 255))
d.rectangle([0, ty - 8, W, ty + 14], fill=INK); d.rectangle([0, ty - 2, W, ty + 8], fill=(196, 204, 214, 255))
# The train: engine on the right, wagons trailing off to the left.
parts = render(["train-locomotive-b", "train-carriage-flatbed-wood", "train-carriage-box", "train-carriage-tank", "train-carriage-coal", "train-carriage-wood", "train-carriage-box", "train-carriage-flatbed-wood"])
x = int(W * 0.9)
for i, p in enumerate(parts):
    tall = H * (0.2 if i == 0 else 0.15)
    s = tall / p.height
    p = p.resize((int(p.width * s), int(p.height * s)), Image.LANCZOS)
    st = outlined(p, 12, 16)
    x -= st.width - 40
    im.alpha_composite(st, (x, ty + 44 - st.height))
d = ImageDraw.Draw(im)
sx = W * 0.868
for (dx, y, rr) in ((0, 0.6, 0.022), (-0.03, 0.565, 0.028), (-0.068, 0.535, 0.034), (-0.112, 0.515, 0.03), (-0.155, 0.505, 0.022)):
    d.ellipse([sx + W * dx - H * rr - 12, H * y - H * rr - 12, sx + W * dx + H * rr + 12, H * y + H * rr + 12], fill=INK)
for (dx, y, rr) in ((0, 0.6, 0.022), (-0.03, 0.565, 0.028), (-0.068, 0.535, 0.034), (-0.112, 0.515, 0.03), (-0.155, 0.505, 0.022)):
    d.ellipse([sx + W * dx - H * rr, H * y - H * rr, sx + W * dx + H * rr, H * y + H * rr], fill=(255, 248, 232, 255))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
im.convert("RGB").resize((1920, 1080), Image.LANCZOS).save(OUT, quality=90)
print("hero", OUT)
