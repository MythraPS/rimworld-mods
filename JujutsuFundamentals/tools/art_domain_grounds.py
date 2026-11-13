"""Domain grounds (her calls 2026-11-11): each domain's landscape drawn over its inside, like Infinite Void's space
and white paint. Top-down, round, 1024 px, soft irregular edge. Drawn by GojoPsycasts.DomainOverlay at
radius * 2 + 1 cells across, so at Yuta's base radius (23.9) one cell is about 21 px.
  MutualLove      Yuta's Authentic Mutual Love (canon ch. 249): dark grey and purple ground, ruined crosses, scattered
                  katanas, giant ropes tied in mizuhiki knots round the edge.
  Benevolent      Yuji's domain (canon ch. 264-265, an empty version of his hometown): a street from above.
  GraniteTsunami  Ryu's (no canon interior; her pick): scorched ground, blue blast craters (Granite Blast blue).
  Skyfall         Uro's (no canon interior; her pick): the ground turned to open sky, cracked.

  python3 tools/art_domain_grounds.py [PREVIEW.png]
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

MOD = Path(__file__).resolve().parent.parent
OUT = MOD / "Textures/Things/Domains"
S = 1024
C = S / 2


# ------------------------------------------------------------------ shared

def noise(seed, scale, lo, hi):
    """Soft value noise as an L image: random blobs blurred together."""
    rng = random.Random(seed)
    small = Image.new("L", (scale, scale))
    small.putdata([rng.randint(lo, hi) for _ in range(scale * scale)])
    return small.resize((S, S), Image.BICUBIC).filter(ImageFilter.GaussianBlur(S / scale / 2))


def tinted(base, col, mask):
    layer = Image.new("RGBA", (S, S), col + (255,))
    out = base.copy()
    out.paste(layer, (0, 0), mask)
    return out


def edge(img, seed):
    """Round, with a soft edge that wobbles a little."""
    rng = random.Random(seed)
    mask = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(mask)
    n = 72
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        r = C * (0.93 + rng.uniform(-0.025, 0.025))
        pts.append((C + r * math.cos(a), C + r * math.sin(a)))
    d.polygon(pts, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(14))
    a = img.getchannel("A")
    img.putalpha(ImageChops.multiply(a, mask))
    return img


def rot(pts, cx, cy, a):
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]


def rect(cx, cy, w, h, a):
    return rot([(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)], cx, cy, a)


def inside(x, y, r=0.82):
    return math.hypot(x - C, y - C) < C * r


# ------------------------------------------------------------------ Yuta: Authentic Mutual Love

PURPLE = (158, 77, 242)


def cross(d, rng, cx, cy, size, a, stone, dark):
    """A stone cross seen from above, fallen flat, with pieces broken off and a crack or two."""
    arm, w = size, size * 0.24
    shadow = (16, 14, 20, 140)
    # shadow first, nudged down-right
    for poly in (rect(cx + 6, cy + 8, w, arm * 1.6, a), rect(cx + 6, cy + 8 - 0, arm, w, a)):
        d.polygon(poly, fill=shadow)
    long_ = rect(cx, cy + 0, w, arm * 1.6, a)
    bar = rot([(-arm / 2, -arm * 0.32 - w / 2), (arm / 2, -arm * 0.32 - w / 2), (arm / 2, -arm * 0.32 + w / 2), (-arm / 2, -arm * 0.32 + w / 2)], cx, cy, a)
    d.polygon(long_, fill=stone, outline=dark)
    d.polygon(bar, fill=stone, outline=dark)
    # broken off: bite pieces out of an end with the ground colour
    for _ in range(rng.randint(1, 3)):
        t = rng.choice((-0.8, 0.8, 0.0))
        px, py = rot([(rng.uniform(-arm / 2, arm / 2) if t == 0.0 else rng.uniform(-w / 2, w / 2), t * arm)], cx, cy, a)[0]
        r = w * rng.uniform(0.5, 0.9)
        d.polygon([(px + r * math.cos(k * 1.3 + rng.random()), py + r * math.sin(k * 1.3 + rng.random())) for k in range(5)], fill=(40, 38, 46, 255))
    # cracks
    for _ in range(2):
        x0, y0 = rot([(rng.uniform(-w / 3, w / 3), rng.uniform(-arm * 0.6, arm * 0.6))], cx, cy, a)[0]
        pts = [(x0, y0)]
        for _ in range(4):
            x0 += rng.uniform(-8, 8); y0 += rng.uniform(-8, 8); pts.append((x0, y0))
        d.line(pts, fill=dark, width=2)
    # rubble round it
    for _ in range(rng.randint(4, 8)):
        rx, ry = cx + rng.uniform(-arm, arm), cy + rng.uniform(-arm, arm)
        r = rng.uniform(3, 8)
        d.ellipse((rx - r, ry - r, rx + r, ry + r), fill=stone, outline=dark)


def katana(d, cx, cy, a, L):
    blade = rect(cx, cy, 5, L, a)
    d.polygon(rot([(1, L * 0.1 + 3), (1, L * 0.1 + 3)], cx, cy, a) + [], fill=None) if False else None
    sx, sy = rot([(3, 3)], 0, 0, a)[0]
    d.polygon([(x + sx, y + sy) for x, y in blade], fill=(10, 10, 14, 110))
    d.polygon(blade, fill=(196, 198, 210, 255), outline=(70, 70, 84, 255))
    # edge line
    e0, e1 = rot([(1.5, -L / 2), (1.5, L / 2)], cx, cy, a)
    d.line([e0, e1], fill=(240, 240, 250, 255), width=1)
    # guard and handle at one end
    gx, gy = rot([(0, L / 2)], cx, cy, a)[0]
    d.polygon(rect(gx, gy, 13, 4, a), fill=(60, 50, 40, 255))
    hx, hy = rot([(0, L / 2 + L * 0.17)], cx, cy, a)[0]
    d.polygon(rect(hx, hy, 6, L * 0.32, a), fill=(36, 30, 44, 255), outline=(90, 70, 120, 255))


def rope_ring(img, rng):
    """Thick twisted ropes all round the edge, with mizuhiki love knots (awaji musubi) tied in at four points."""
    d = ImageDraw.Draw(img)
    R = C * 0.86
    light, mid, dark = (232, 226, 240), (176, 150, 210), (70, 50, 96)
    for strand, off in ((dark, 0), (mid, 0)):
        pass
    n = 360
    pts = [(C + R * math.cos(2 * math.pi * i / n), C + R * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
    d.line(pts, fill=dark + (255,), width=30)
    d.line(pts, fill=mid + (255,), width=24)
    # the twist: short slanted bands along the rope
    for i in range(0, 720):
        a = 2 * math.pi * i / 720
        if i % 2: continue
        x0, y0 = C + (R - 10) * math.cos(a), C + (R - 10) * math.sin(a)
        x1, y1 = C + (R + 10) * math.cos(a + 0.012), C + (R + 10) * math.sin(a + 0.012)
        d.line([(x0, y0), (x1, y1)], fill=light + (255,), width=3)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        kx, ky = C + R * math.cos(a), C + R * math.sin(a)
        knot(d, kx, ky, 70, a + math.pi / 2, light, mid, dark)


def knot(d, cx, cy, s, a, light, mid, dark):
    """Awaji musubi seen flat: three interlaced loops (two side lobes and one top lobe) with the two ends trailing."""
    loops = [(-0.42, 0.05, 0.36, 0.28), (0.42, 0.05, 0.36, 0.28), (0.0, -0.32, 0.3, 0.3)]
    for lx, ly, rx, ry in loops:
        x, y = rot([(lx * s, ly * s)], cx, cy, a)[0]
        bbox = (x - rx * s, y - ry * s, x + rx * s, y + ry * s)
        d.ellipse(bbox, outline=dark + (255,), width=16)
        d.ellipse(bbox, outline=mid + (255,), width=10)
        d.ellipse(bbox, outline=light + (255,), width=3)
    for side in (-1, 1):
        p = rot([(side * 0.2 * s, 0.25 * s), (side * 0.45 * s, 0.8 * s), (side * 0.38 * s, 1.15 * s)], cx, cy, a)
        d.line(p, fill=dark + (255,), width=16)
        d.line(p, fill=mid + (255,), width=10)


def mutual_love():
    rng = random.Random(249)
    base = Image.new("RGBA", (S, S), (46, 44, 52, 255))
    base = tinted(base, (30, 28, 36), noise(1, 24, 0, 160))
    base = tinted(base, (74, 62, 96), noise(2, 10, 0, 70))            # a purple haze over the grey
    base = tinted(base, PURPLE, noise(3, 6, 0, 22))
    d = ImageDraw.Draw(base)
    # cracks in the ground
    for _ in range(40):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        pts = [(x, y)]
        for _ in range(6):
            x += rng.uniform(-18, 18); y += rng.uniform(-18, 18); pts.append((x, y))
        d.line(pts, fill=(28, 26, 32, 255), width=2)
    # ruined crosses
    placed = []
    while len(placed) < 9:
        x, y = rng.uniform(C * 0.3, S - C * 0.3), rng.uniform(C * 0.3, S - C * 0.3)
        if not inside(x, y, 0.7) or any(math.hypot(x - px, y - py) < 190 for px, py in placed): continue
        placed.append((x, y))
        cross(d, rng, x, y, rng.uniform(90, 135), rng.uniform(0, math.pi), (124, 120, 132, 255), (44, 40, 52, 255))
    # no drawn katanas and no rope ring (her calls 2026-11-11): the real katanas are the ones he can pick up
    return edge(base, 249)


# ------------------------------------------------------------------ Yuji: an empty hometown street

def benevolent():
    rng = random.Random(264)
    base = Image.new("RGBA", (S, S), (150, 146, 138, 255))         # pavement between buildings
    d = ImageDraw.Draw(base)
    road_w, walk_w = 150, 40
    rx, ry = C + 30, C - 40                                        # the crossroads, a little off centre
    roof_cols = [(92, 104, 120), (130, 96, 80), (100, 98, 96), (70, 86, 96), (140, 130, 112), (110, 78, 70)]
    # blocks of houses in the four corners: roofs from above with ridge lines
    def roofs(x0, y0, x1, y1):
        y = y0 + 12
        while y < y1 - 40:
            x = x0 + 12
            h = rng.randint(70, 120)
            while x < x1 - 40:
                w = rng.randint(80, 140)
                w = min(w, x1 - 12 - x)
                hh = min(h, y1 - 12 - y)
                if w < 40 or hh < 40: break
                col = rng.choice(roof_cols)
                d.rectangle((x + 5, y + 6, x + w + 5, y + hh + 6), fill=(60, 58, 54, 255))     # shadow
                d.rectangle((x, y, x + w, y + hh), fill=col + (255,), outline=(50, 50, 52, 255), width=2)
                if w > hh: d.line([(x + 6, y + hh / 2), (x + w - 6, y + hh / 2)], fill=tuple(max(0, c - 30) for c in col) + (255,), width=3)
                else: d.line([(x + w / 2, y + 6), (x + w / 2, y + hh - 6)], fill=tuple(max(0, c - 30) for c in col) + (255,), width=3)
                x += w + rng.randint(10, 24)
            y += h + rng.randint(12, 24)
    half = road_w / 2 + walk_w
    roofs(0, 0, rx - half, ry - half); roofs(rx + half, 0, S, ry - half)
    roofs(0, ry + half, rx - half, S); roofs(rx + half, ry + half, S, S)
    # sidewalks with kerbs
    for (x0, y0, x1, y1) in ((0, ry - half, S, ry + half), (rx - half, 0, rx + half, S)):
        d.rectangle((x0, y0, x1, y1), fill=(176, 172, 164, 255))
    # tiles on the sidewalks
    for t in range(0, S, 26):
        d.line([(t, ry - half), (t, ry - road_w / 2)], fill=(150, 146, 140, 255), width=1)
        d.line([(t, ry + road_w / 2), (t, ry + half)], fill=(150, 146, 140, 255), width=1)
        d.line([(rx - half, t), (rx - road_w / 2, t)], fill=(150, 146, 140, 255), width=1)
        d.line([(rx + road_w / 2, t), (rx + half, t)], fill=(150, 146, 140, 255), width=1)
    # asphalt
    asph = (66, 66, 70, 255)
    d.rectangle((0, ry - road_w / 2, S, ry + road_w / 2), fill=asph)
    d.rectangle((rx - road_w / 2, 0, rx + road_w / 2, S), fill=asph)
    base = tinted(base, (52, 52, 56), ImageChops.multiply(noise(5, 40, 0, 140), road_mask(rx, ry, road_w)))
    d = ImageDraw.Draw(base)
    # kerb lines
    kerb = (205, 202, 196, 255)
    for o in (-road_w / 2, road_w / 2):
        d.line([(0, ry + o), (rx - road_w / 2, ry + o)], fill=kerb, width=4); d.line([(rx + road_w / 2, ry + o), (S, ry + o)], fill=kerb, width=4)
        d.line([(rx + o, 0), (rx + o, ry - road_w / 2)], fill=kerb, width=4); d.line([(rx + o, ry + road_w / 2), (rx + o, S)], fill=kerb, width=4)
    # dashed centre lines, stop lines, zebra crossings
    white = (232, 232, 226, 255)
    for t in range(0, S, 60):
        if abs(t + 15 - rx) > road_w / 2 + 60: d.line([(t, ry), (t + 32, ry)], fill=white, width=5)
        if abs(t + 15 - ry) > road_w / 2 + 60: d.line([(rx, t), (rx, t + 32)], fill=white, width=5)
    for side in (-1, 1):
        for k in range(7):
            o = -road_w / 2 + 12 + k * (road_w - 24) / 6
            zx = rx + side * (road_w / 2 + 30)
            d.rectangle((zx - 18, ry + o - 6, zx + 18, ry + o + 6), fill=white)
            zy = ry + side * (road_w / 2 + 30)
            d.rectangle((rx + o - 6, zy - 18, rx + o + 6, zy + 18), fill=white)
    # a few street details: manholes, a vending machine, a utility pole shadow
    for _ in range(3):
        mx, my = rng.choice(((rng.uniform(0, S), ry + rng.uniform(-50, 50)), (rx + rng.uniform(-50, 50), rng.uniform(0, S))))
        d.ellipse((mx - 14, my - 14, mx + 14, my + 14), fill=(48, 48, 50, 255), outline=(90, 90, 92, 255), width=2)
    for (px, py) in ((rx - half + 12, ry - half - 30), (rx + half - 12, ry + half + 40), (rx - half + 14, ry + half + 160)):
        d.rectangle((px - 7, py - 7, px + 7, py + 7), fill=(80, 70, 60, 255))
        d.line([(px, py), (px + 90, py + 60)], fill=(60, 58, 54, 160), width=6)
    return edge(base, 264)


def road_mask(rx, ry, w):
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    d.rectangle((0, ry - w / 2, S, ry + w / 2), fill=255)
    d.rectangle((rx - w / 2, 0, rx + w / 2, S), fill=255)
    return m


# ------------------------------------------------------------------ Ryu: scorched ground, blue craters

def granite():
    rng = random.Random(179)
    base = Image.new("RGBA", (S, S), (44, 38, 34, 255))
    base = tinted(base, (22, 20, 20), noise(7, 20, 0, 200))           # char
    base = tinted(base, (72, 56, 42), noise(8, 12, 0, 90))            # burnt earth showing through
    base = tinted(base, (110, 106, 100), noise(9, 48, 0, 50))         # ash
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    d = ImageDraw.Draw(base)
    craters = []
    while len(craters) < 12:
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(34, 80)
        if not inside(x, y, 0.8) or any(math.hypot(x - a, y - b) < r + rr + 20 for a, b, rr in craters): continue
        craters.append((x, y, r))
    for x, y, r in craters:
        # scorch ring, radial cracks, dark pit, blue glow at the rim and in the cracks
        d.ellipse((x - r * 1.6, y - r * 1.6, x + r * 1.6, y + r * 1.6), fill=(24, 22, 22, 140))
        for _ in range(rng.randint(5, 9)):
            a = rng.uniform(0, 2 * math.pi)
            L = r * rng.uniform(1.3, 2.2)
            pts = [(x + r * 0.8 * math.cos(a), y + r * 0.8 * math.sin(a))]
            px, py = pts[0]
            for k in range(5):
                aa = a + rng.uniform(-0.35, 0.35)
                px += L / 5 * math.cos(aa); py += L / 5 * math.sin(aa); pts.append((px, py))
            d.line(pts, fill=(14, 12, 12, 255), width=3)
            gd.line(pts[:3], fill=(30, 97, 255, 200), width=5)
        pit = [(x + r * (0.85 + rng.uniform(-0.12, 0.12)) * math.cos(t), y + r * (0.85 + rng.uniform(-0.12, 0.12)) * math.sin(t))
               for t in [2 * math.pi * i / 14 for i in range(14)]]
        d.polygon(pit, fill=(26, 24, 26, 255), outline=(12, 10, 10, 255))
        d.ellipse((x - r * 0.5, y - r * 0.45, x + r * 0.5, y + r * 0.5), fill=(12, 12, 14, 255))
        start = rng.uniform(0, 360)
        for k in range(rng.randint(2, 4)):                         # the glow lingers on parts of the rim only
            a0 = start + k * rng.uniform(70, 120)
            gd.arc((x - r, y - r, x + r, y + r), a0, a0 + rng.uniform(40, 110), fill=(30, 97, 255, 230), width=7)
        # embers in the pit
        for _ in range(rng.randint(2, 5)):
            ex, ey = x + rng.uniform(-r * 0.45, r * 0.45), y + rng.uniform(-r * 0.45, r * 0.45)
            er = rng.uniform(3, 7)
            gd.ellipse((ex - er, ey - er, ex + er, ey + er), fill=(140, 190, 255, 170))
    glow_soft = glow.filter(ImageFilter.GaussianBlur(8))
    base = Image.alpha_composite(base, glow_soft)
    base = Image.alpha_composite(base, glow.filter(ImageFilter.GaussianBlur(1)))
    return edge(base, 179)


# ------------------------------------------------------------------ Uro: sky, cracked

def fracture(rng, lines, x, y, heading, length, width, depth):
    pts = [(x, y)]
    travelled = 0.0
    while travelled < length:
        heading += rng.uniform(-0.12, 0.12)
        a = heading + rng.uniform(-0.45, 0.45)
        seg = rng.uniform(18, 40)
        x += seg * math.cos(a); y += seg * math.sin(a)
        travelled += seg
        pts.append((x, y))
        if depth > 0 and rng.random() < 0.22:
            fracture(rng, lines, x, y, heading + rng.choice((-1, 1)) * rng.uniform(0.5, 1.2),
                     (length - travelled) * rng.uniform(0.35, 0.8), max(1, width - 1), depth - 1)
    lines.append((pts, width))


def skyfall():
    rng = random.Random(180)
    # blue, a little lighter towards the middle
    base = Image.new("RGBA", (S, S), (70, 128, 206, 255))
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse((C * 0.2, C * 0.2, S - C * 0.2, S - C * 0.2), fill=255)
    base = tinted(base, (128, 178, 236), m.filter(ImageFilter.GaussianBlur(160)))
    # clouds: clumps of soft white puffs with a grey-blue underside
    clouds = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cd = ImageDraw.Draw(clouds)
    for _ in range(11):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        for _ in range(rng.randint(8, 14)):
            px, py = x + rng.gauss(0, 55), y + rng.gauss(0, 30)
            r = rng.uniform(26, 58)
            cd.ellipse((px - r + 6, py - r + 10, px + r + 6, py + r + 10), fill=(150, 170, 200, 150))
            cd.ellipse((px - r, py - r, px + r, py + r), fill=(250, 252, 255, 210))
    base = Image.alpha_composite(base, clouds.filter(ImageFilter.GaussianBlur(9)))
    # the sky cracking: a few big fractures across it
    lines = []
    for _ in range(4):
        x, y = rng.uniform(C * 0.5, S - C * 0.5), rng.uniform(C * 0.5, S - C * 0.5)
        for _ in range(rng.randint(3, 5)):
            fracture(rng, lines, x, y, rng.uniform(0, 2 * math.pi), rng.uniform(120, 300), 5, 3)
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for pts, w in lines: gd.line(pts, fill=(170, 215, 255, 200), width=w * 4)
    core = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    kd = ImageDraw.Draw(core)
    for pts, w in lines:
        kd.line([(x + 2, y + 2) for x, y in pts], fill=(30, 50, 90, 200), width=w)    # the gap's dark side
        kd.line(pts, fill=(245, 250, 255, 255), width=max(1, w - 2))
    base = Image.alpha_composite(base, glow.filter(ImageFilter.GaussianBlur(6)))
    base = Image.alpha_composite(base, core)
    return edge(base, 180)


ALL = {"MutualLove": mutual_love, "Benevolent": benevolent, "GraniteTsunami": granite, "Skyfall": skyfall}

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    imgs = []
    for name, fn in ALL.items():
        im = fn()
        im.save(OUT / f"{name}.png")
        imgs.append(im)
        print("wrote", OUT / f"{name}.png")
    if len(sys.argv) > 1:
        t = 512
        prev = Image.new("RGBA", (t * 4, t), (96, 82, 62, 255))
        for i, im in enumerate(imgs):
            prev.alpha_composite(im.resize((t, t), Image.LANCZOS), (i * t, 0))
        prev.save(sys.argv[1])
        print("preview", sys.argv[1])
