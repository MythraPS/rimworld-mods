"""Cracking-space flecks for Uro's Thin Ice-Breaker and Skyfall (her call 2026-11-11: "the space around the target
is cracking"). Canon reference: the manga draws Thin Ice-Breaker as the air shattering like a pane of ice/glass,
jagged fractures branching off chaotically from the point of impact, a few shards broken loose (no rings: her call 2026-11-11). Three variants, pale blue-white with
a faint glow, on transparent backgrounds.

  python3 tools/yuta/art_sky_crack.py      (writes Textures/Things/Yuta/Sky/SkyCrack{A,B,C}.png + a preview)
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parent.parent.parent
OUT = MOD / "Textures/Things/Yuta/Sky"
S = 256
C = S / 2
LINE = (235, 245, 255)
GLOW = (150, 200, 255)


def branch(rng, lines, x, y, a, length, width, depth):
    """One fracture: a jagged walk that keeps changing direction, splitting off smaller fractures at random
    (her call 2026-11-11: chaotic cracks, not a spider web)."""
    pts = [(x, y)]
    heading = a
    travelled = 0.0
    while travelled < length:
        # straight runs with sharp kinks, around a heading that only drifts a little (glass, not lightning)
        heading += rng.uniform(-0.12, 0.12)
        a = heading + rng.uniform(-0.45, 0.45)
        seg = rng.uniform(10, 24)
        x += seg * math.cos(a); y += seg * math.sin(a)
        travelled += seg
        if math.hypot(x - C, y - C) > C * 0.95: break
        pts.append((x, y))
        if depth > 0 and rng.random() < 0.22:
            branch(rng, lines, x, y, heading + rng.choice((-1, 1)) * rng.uniform(0.5, 1.2),
                   (length - travelled) * rng.uniform(0.35, 0.8), max(1, width - 1), depth - 1)
    if len(pts) > 1: lines.append((pts, width))


def crack(seed, mains, _unused=None):
    rng = random.Random(seed)
    lines = []   # (points, width)
    # main fractures leave the impact point at uneven angles and lengths
    angles = sorted(rng.uniform(0, 2 * math.pi) for _ in range(mains))
    for a0 in angles:
        branch(rng, lines, C + rng.uniform(-4, 4), C + rng.uniform(-4, 4), a0, rng.uniform(0.45, 0.95) * C, 3, 3)
    # a few short fractures starting away from the centre, unconnected
    for _ in range(rng.randint(2, 4)):
        r = rng.uniform(0.3, 0.7) * C; t = rng.uniform(0, 2 * math.pi)
        branch(rng, lines, C + r * math.cos(t), C + r * math.sin(t), rng.uniform(0, 2 * math.pi), rng.uniform(15, 40), 2, 1)
    # broken-off shards: small irregular faces where fractures run close
    faces = []
    ends = [pt for pts, w in lines for pt in pts[1:]]
    for _ in range(rng.randint(3, 6)):
        if len(ends) < 3: break
        p0 = rng.choice(ends)
        near = sorted(ends, key=lambda q: (q[0] - p0[0]) ** 2 + (q[1] - p0[1]) ** 2)[1:6]
        if len(near) >= 2: faces.append([p0] + rng.sample(near, 2))

    def draw(width_scale, colour, alpha):
        img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        for pts, w in lines:
            d.line(pts, fill=colour + (alpha,), width=max(1, int(w * width_scale)))
        return img

    shards = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shards)
    for f in faces:
        sd.polygon(f, fill=GLOW + (rng.randint(35, 70),))
    glow = Image.alpha_composite(shards, draw(4.0, GLOW, 150).filter(ImageFilter.GaussianBlur(5)))
    core = draw(1.0, LINE, 255)
    # the impact point itself: a small bright burst
    burst = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(burst).ellipse((C - 10, C - 10, C + 10, C + 10), fill=LINE + (220,))
    burst = burst.filter(ImageFilter.GaussianBlur(4))
    out = Image.alpha_composite(glow, core)
    out = Image.alpha_composite(out, burst)
    # fade towards the edge so the fleck never ends in a hard square
    mask = Image.new("L", (S, S), 0)
    md = ImageDraw.Draw(mask)
    for r in range(int(C), 0, -2):
        v = int(255 * min(1.0, (C - r) / (C * 0.18)))
        md.ellipse((C - r, C - r, C + r, C + r), fill=v)
    a = out.getchannel("A")
    out.putalpha(Image.composite(a, Image.new("L", (S, S), 0), mask))
    return out


VARIANTS = {"SkyCrackA": (11, 6, None), "SkyCrackB": (23, 5, None), "SkyCrackC": (37, 7, None)}

OUT.mkdir(parents=True, exist_ok=True)
imgs = []
for name, (seed, mains, _) in VARIANTS.items():
    im = crack(seed, mains)
    im.save(OUT / f"{name}.png")
    imgs.append(im)
    print("wrote", OUT / f"{name}.png")

if len(sys.argv) > 1:
    # preview on a RimWorld-ish ground colour
    prev = Image.new("RGBA", (S * 3, S), (96, 82, 62, 255))
    for i, im in enumerate(imgs):
        prev.alpha_composite(im, (i * S, 0))
    prev.save(sys.argv[1])
    print("preview", sys.argv[1])
