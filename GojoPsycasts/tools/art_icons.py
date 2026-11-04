"""Ability icons, second pass (her picks 2026-10-29), for both mods.

  python tools/art_icons.py GOJO_MOD_DIR SUKUNA_MOD_DIR [PREVIEW.png]

Picture icons (replace the old ones):
  Gojo   Infinity      the infinity sign, now white
  Gojo   InfiniteVoid  the void disc with a spiral galaxy in it, no letter
  Sukuna Dismantle     one white slash
  Sukuna Cleave        white slashes crossing
  Sukuna Shrine        Malevolent Shrine, simplified: tiered roof, a fanged mouth
                       between the pillars, a pile of skulls under it
  Sukuna FireArrow     an arrow in flames
Kanji icons (for the "kanji" icon setting), every ability, in Abilities/<Mod>/Kanji/.
Tiles keep the old icons' background colours and the Sukuna mod's rounded-tile style.
"""
import math, random, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

GOJO, SUKUNA = Path(sys.argv[1]), Path(sys.argv[2])
src = (GOJO / "tools/art_gojo.py").read_text()
sys.argv = [sys.argv[0], str(GOJO)] + sys.argv[3:]
exec(src.rsplit("\nmain()", 1)[0])          # helpers only: tile, orb, void_disc, ...
KANJI_FONT = ("/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc", 0)   # index 0 = JP
S = 128
WHITE = (240, 236, 228)


def put(mod, name, img, kanji=False):
    folder = "Gojo" if mod == GOJO else "Sukuna"
    p = mod / "Textures/Abilities" / folder / ("Kanji" if kanji else "") / f"{name}.png"
    p.parent.mkdir(parents=True, exist_ok=True)
    img.save(p)
    return img


def bg_of(mod, name):
    folder = "Gojo" if mod == GOJO else "Sukuna"
    return Image.open(mod / "Textures/Abilities" / folder / f"{name}.png").convert("RGB").getpixel((20, 64))


def glow(layer, r=4):
    out = layer.filter(ImageFilter.GaussianBlur(r))
    out.alpha_composite(layer)
    return out


# ------------------------------------------------------------------ pictures

def infinity_white():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for blur, a in ((6, 150), (0, 255)):
        l = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(l)
        d.ellipse((18, 44, 66, 84), outline=(250, 250, 255, a), width=7)
        d.ellipse((62, 44, 110, 84), outline=(250, 250, 255, a), width=7)
        img.alpha_composite(l.filter(ImageFilter.GaussianBlur(blur)) if blur else l)
    return img


def galaxy(size, arms=2, seed=4):
    """A spiral galaxy: a bright core and dotted logarithmic arms."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img); c = size / 2; rnd = random.Random(seed)
    for arm in range(arms):
        for i in range(260):
            t = i / 260
            a = arm * math.tau / arms + t * 3.2 * math.pi
            r = size * 0.04 + t * size * 0.44
            x = c + math.cos(a) * r + rnd.gauss(0, size * 0.025 * (0.4 + t))
            y = c + math.sin(a) * r * 0.62 + rnd.gauss(0, size * 0.02 * (0.4 + t))
            s = max(0.6, (1 - t) * size * 0.022)
            v = int(255 - 70 * t)
            d.ellipse((x - s, y - s, x + s, y + s), fill=(int(v * 0.75), int(v * 0.85), 255, int(255 - 100 * t)))
    core = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(core).ellipse((c - size * 0.1, c - size * 0.07, c + size * 0.1, c + size * 0.07), fill=(255, 250, 235, 255))
    img.alpha_composite(core.filter(ImageFilter.GaussianBlur(size * 0.04)))
    return glow(img.rotate(-25, resample=Image.BICUBIC), 2)


def infinite_void(bg):
    base = tile(bg)
    disc = void_disc().resize((96, 96), Image.LANCZOS)
    base.alpha_composite(disc, (16, 16))
    # a dark patch behind the galaxy so it reads against the white paint
    hole = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(hole).ellipse((30, 38, 98, 90), fill=(4, 4, 14, 235))
    base.alpha_composite(hole.filter(ImageFilter.GaussianBlur(6)))
    base.alpha_composite(galaxy(76), (26, 26))
    return base


def slash_shape(d, p0, p1, width, bulge):
    """A crescent cut: thin at both ends, widest in the middle, bowed to one side."""
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy); nx, ny = -dy / L, dx / L
    pts_a, pts_b = [], []
    for i in range(25):
        t = i / 24
        w = math.sin(math.pi * t) * width
        bow = math.sin(math.pi * t) * bulge
        px, py = x0 + dx * t + nx * bow, y0 + dy * t + ny * bow
        pts_a.append((px + nx * w / 2, py + ny * w / 2)); pts_b.append((px - nx * w / 2, py - ny * w / 2))
    d.polygon(pts_a + pts_b[::-1], fill=(255, 255, 255, 255))


def slashes(lines):
    l = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(l)
    for p0, p1, w, b in lines:
        slash_shape(d, p0, p1, w, b)
    return glow(l, 3)


def shrine():
    """Malevolent Shrine, simplified: a two-tier temple roof with upturned eaves,
    a fanged mouth between its pillars, standing on a heap of skulls."""
    K = 4; N = S * K
    l = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(l)
    W = (236, 226, 208, 255); D = (20, 10, 14, 255)
    P = lambda pts: [(x * K, y * K) for x, y in pts]
    # skull heap
    for cx, cy, r in ((40, 100, 9), (58, 103, 9), (76, 103, 9), (93, 100, 9), (49, 90, 8), (67, 92, 8), (84, 90, 8)):
        d.ellipse(P([(cx - r, cy - r), (cx + r, cy + r * 0.9)]), fill=W)
        d.ellipse(P([(cx - r * 0.55, cy - r * 0.2), (cx - r * 0.1, cy + r * 0.3)]), fill=D)
        d.ellipse(P([(cx + r * 0.1, cy - r * 0.2), (cx + r * 0.55, cy + r * 0.3)]), fill=D)
    # body: pillars and the mouth
    d.rectangle(P([(36, 56), (92, 84)]), fill=W)
    d.rectangle(P([(44, 60), (84, 82)]), fill=D)
    for x in range(45, 84, 6):
        d.polygon(P([(x, 60), (x + 5, 60), (x + 2.5, 67)]), fill=W)      # upper teeth
        d.polygon(P([(x, 82), (x + 5, 82), (x + 2.5, 75)]), fill=W)      # lower teeth
    # lower roof, upturned at the ends
    d.polygon(P([(18, 50), (30, 56), (98, 56), (110, 50), (96, 44), (32, 44)]), fill=W)
    # upper roof
    d.rectangle(P([(52, 34), (76, 44)]), fill=W)
    d.polygon(P([(36, 30), (46, 36), (82, 36), (92, 30), (80, 24), (48, 24)]), fill=W)
    d.rectangle(P([(61, 16), (67, 24)]), fill=W)                          # finial
    img = l.resize((S, S), Image.LANCZOS)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 160), mask=img.getchannel("A").filter(ImageFilter.MaxFilter(5)))
    shadow.alpha_composite(img)
    return shadow


def fire_arrow():
    K = 4; N = S * K; rnd = random.Random(9)
    fl = Image.new("RGBA", (N, N), (0, 0, 0, 0)); df = ImageDraw.Draw(fl)
    ar = Image.new("RGBA", (N, N), (0, 0, 0, 0)); da = ImageDraw.Draw(ar)
    a = math.radians(-45)                       # pointing up and right
    ux, uy = math.cos(a), math.sin(a); nx, ny = -uy, ux
    tail, tip = (26 * K, 102 * K), (100 * K, 28 * K)
    # flames trailing behind along the shaft, licking backward
    for i in range(160):
        t = rnd.uniform(0.1, 0.98)
        x = tail[0] + (tip[0] - tail[0]) * t; y = tail[1] + (tip[1] - tail[1]) * t
        back = rnd.uniform(4, 30) * K * (1.2 - t * 0.5); side = rnd.gauss(0, 7 * K)
        cx, cy = x - ux * back + nx * side, y - uy * back + ny * side
        r = rnd.uniform(4, 10) * K * (1.2 - t * 0.5)
        col = rnd.choice(((255, 120, 20), (255, 170, 40), (255, 80, 10), (255, 220, 120)))
        df.ellipse((cx - r, cy - r, cx + r, cy + r), fill=col + (235,))
    flames = fl.filter(ImageFilter.GaussianBlur(3 * K))
    flames.alpha_composite(fl.filter(ImageFilter.GaussianBlur(K)))
    # the arrow: shaft, head, fletching
    da.line([tail, tip], fill=(250, 240, 220, 255), width=4 * K)
    hx, hy = tip
    da.polygon([(hx + ux * 12 * K, hy + uy * 12 * K), (hx + nx * 7 * K, hy + ny * 7 * K), (hx - nx * 7 * K, hy - ny * 7 * K)], fill=(255, 250, 235, 255))
    tx, ty = tail
    for s in (1, -1):
        da.polygon([(tx, ty), (tx + ux * 14 * K, ty + uy * 14 * K), (tx + ux * 6 * K + nx * s * 8 * K, ty + uy * 6 * K + ny * s * 8 * K), (tx - ux * 4 * K + nx * s * 8 * K, ty - uy * 4 * K + ny * s * 8 * K)], fill=(250, 240, 220, 255))
    flames.alpha_composite(ar)
    return flames.resize((S, S), Image.LANCZOS)


# ------------------------------------------------------------------ kanji

def kanji_tile(bg, text):
    img = tile(bg)
    d = ImageDraw.Draw(img); n = len(text)
    if n == 1: sz, lines = 80, [text]
    elif n == 2: sz, lines = 50, [text]
    elif n == 4: sz, lines = 44, [text[:2], text[2:]]
    else: sz, lines = 33, [text[:3], text[3:]]
    f = ImageFont.truetype(KANJI_FONT[0], sz, index=KANJI_FONT[1])
    lh = sz * 1.05; y0 = S / 2 - lh * len(lines) / 2
    for i, line in enumerate(lines):
        box = d.textbbox((0, 0), line, font=f)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], y0 + i * lh + (lh - (box[3] - box[1])) / 2 - box[1]), line, font=f, fill=WHITE)
    return img


GOJO_KANJI = {"Infinity": "無限", "Blue": "蒼", "Red": "赫", "Purple": "茈", "InfiniteVoid": "無量空処",
              "BlackFlash": "黒閃", "ReverseCursedTechnique": "反転術式", "BlueFist": "拳", "Speed": "速",
              "Teleport": "瞬", "Reinforcement": "呪力強化"}
SUKUNA_KANJI = {"Dismantle": "解", "Cleave": "捌", "MalevolentShrine": "伏魔御廚子", "PseudoInfinity": "呪力強化",
                "FireArrow": "竈", "BlackFlash": "黒閃", "ReverseCursedTechnique": "反転術式", "Thorns": "棘",
                "WorldCuttingSlash": "断"}


# Tile colours of the first icons (Gojo: art_gojo.py; Sukuna: sampled from its
# original textures), fixed here so re-running never samples a redrawn icon.
GOJO_BG = {"Infinity": (22, 28, 52), "Blue": (18, 30, 70), "Red": (70, 16, 18), "Purple": (44, 18, 70),
           "InfiniteVoid": (10, 10, 24), "BlackFlash": (22, 22, 26), "ReverseCursedTechnique": (46, 112, 80),
           "BlueFist": (30, 60, 120), "Speed": (40, 90, 160), "Teleport": (80, 50, 120),
           "Reinforcement": (63, 77, 107)}   # same tile as Sukuna's, copied 2026-10-31
SUKUNA_BG = {"Dismantle": (168, 68, 60), "Cleave": (140, 47, 42), "MalevolentShrine": (74, 35, 64),
             "PseudoInfinity": (63, 77, 107), "FireArrow": (181, 98, 42), "BlackFlash": (31, 31, 36),
             "ReverseCursedTechnique": (47, 107, 79), "Thorns": (109, 58, 82), "WorldCuttingSlash": (122, 31, 31)}


def main():
    made = []
    gb, sb = GOJO_BG, SUKUNA_BG
    made += [put(GOJO, "Infinity", tile(gb["Infinity"], infinity_white())),
             put(GOJO, "InfiniteVoid", infinite_void(gb["InfiniteVoid"])),
             put(SUKUNA, "Dismantle", tile(sb["Dismantle"], slashes([((30, 100), (98, 28), 12, 8)]))),
             put(SUKUNA, "Cleave", tile(sb["Cleave"], slashes([((28, 98), (100, 30), 10, 6), ((28, 30), (100, 98), 10, -6),
                                                               ((40, 106), (106, 44), 6, 4), ((22, 44), (88, 106), 6, -4)]))),
             put(SUKUNA, "MalevolentShrine", tile(sb["MalevolentShrine"], shrine())),
             put(SUKUNA, "FireArrow", tile(sb["FireArrow"], fire_arrow()))]
    for n, k in GOJO_KANJI.items(): made.append(put(GOJO, n, kanji_tile(gb[n], k), kanji=True))
    for n, k in SUKUNA_KANJI.items(): made.append(put(SUKUNA, n, kanji_tile(sb[n], k), kanji=True))
    if len(sys.argv) > 2:
        cols = 6; sheet = Image.new("RGBA", (cols * 136, ((len(made) + cols - 1) // cols) * 136), (60, 60, 64, 255))
        for i, im in enumerate(made): sheet.alpha_composite(im, ((i % cols) * 136 + 4, (i // cols) * 136 + 4))
        sheet.save(sys.argv[2])
    print(len(made), "icons")


main()
