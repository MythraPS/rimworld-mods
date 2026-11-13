"""Yuta's Copy part: placeholder icons and path backgrounds (2026-11-11), in the house letter-tile style
(tools/art_icons.py) and kanji-background style (tools/art_pictures.py). Purple = Yuta's cursed energy.
Letter tiles: Copy "Co", Paste "Pa", Reverse Cursed Technique: Output "RO", Authentic Mutual Love "AL".
Kanji tiles (KanjiForAll setting): Copy 模倣, Authentic Mutual Love 真贋相愛 (canon names).
Backgrounds (950x1515), her pick 2026-11-11: a hand wearing Yuta's ring (silver band, blue stone; reference
~/.local/share/rimworld-refs/yuta-images/yuta_ring.jpg: his left hand, back of the hand towards us, ring on the
ring finger). The Copy icon uses the same hand.

  python3 tools/yuta/art_copy.py
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

MOD = Path(__file__).resolve().parents[2]
T = MOD / "Textures"
OUT = T / "Abilities/Yuta/Copy"
SCRIPT = "/usr/share/fonts/gsfonts/Z003-MediumItalic.otf"
KANJI_FONT = "/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc"
S = 128
WHITE = (240, 236, 228)
YUTA = (92, 52, 140)
W, H = 950, 1515


def tile(bg, letter=None):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((4, 4, S - 5, S - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    if letter:
        font = ImageFont.truetype(SCRIPT, 76 if len(letter) == 1 else 54)
        d = ImageDraw.Draw(img)
        box = d.textbbox((0, 0), letter, font=font)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], (S - (box[3] - box[1])) / 2 - box[1] - 2), letter, font=font, fill=WHITE)
    return img


def kanji_tile(bg, text):
    img = tile(bg)
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(KANJI_FONT, 44, index=0)
    lines = [text[:2], text[2:]] if len(text) > 2 else [text]
    lh = 44 * 1.05; y0 = S / 2 - lh * len(lines) / 2
    for i, line in enumerate(lines):
        box = d.textbbox((0, 0), line, font=f)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], y0 + i * lh + (lh - (box[3] - box[1])) / 2 - box[1]), line, font=f, fill=WHITE)
    return img


def base(top, bot, edge):
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
    d.rectangle((24, 24, W - 25, H - 25), outline=edge + (255,), width=6)
    return img


def kanji_motif(chars, col):
    L = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    f = ImageFont.truetype(KANJI_FONT, 360, index=0)
    step = 420; y0 = (H - step * len(chars)) / 2
    for i, ch in enumerate(chars):
        box = d.textbbox((0, 0), ch, font=f)
        d.text(((W - (box[2] - box[0])) / 2 - box[0], y0 + i * step + (step - (box[3] - box[1])) / 2 - box[1]), ch, font=f, fill=col)
    return L


import math


def capsule(d, a, b, w, fill):
    d.line([a, b], fill=fill, width=int(w))
    for x, y in (a, b):
        d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=fill)


# the hand on a 950x1515 frame: left hand, back of the hand towards us, so the thumb is on the right
FINGERS = [((330, 800), (292, 520), 68), ((408, 780), (388, 380), 76), ((492, 772), (497, 322), 80), ((576, 784), (612, 392), 76)]
THUMB = [((626, 1090), (722, 962), 94), ((722, 962), (800, 852), 78)]   # two joints
RING_FINGER = 1


def hand_layer(scale, col, line, silver, stone, dark, ring=1.0):
    """The hand in one colour with faint knuckle lines, then the ring at full strength on top."""
    Wd, Hd = int(W * scale), int(H * scale)
    L = Image.new("RGBA", (Wd, Hd), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    S_ = lambda p: (p[0] * scale, p[1] * scale)
    palm = [(322, 790), (400, 770), (490, 764), (580, 776), (648, 800), (670, 930), (660, 1060), (625, 1180),
            (590, 1330), (392, 1330), (345, 1180), (312, 1040), (300, 900)]
    d.polygon([S_(p) for p in palm], fill=col)
    for a, b, w in FINGERS:
        capsule(d, S_(a), S_(b), w * scale, col)
    for a, b, w in THUMB:
        capsule(d, S_(a), S_(b), w * scale, col)
    for a, b, w in FINGERS:                           # knuckles: a short line across each finger, a third of the way up
        t = 0.42
        cx = a[0] + (b[0] - a[0]) * t; cy = a[1] + (b[1] - a[1]) * t
        ang = math.atan2(b[1] - a[1], b[0] - a[0]) + math.pi / 2
        dx, dy = math.cos(ang) * w * 0.28, math.sin(ang) * w * 0.28
        d.line([S_((cx - dx, cy - dy)), S_((cx + dx, cy + dy))], fill=line, width=max(1, int(5 * scale)))
    # the ring: a band across the ring finger near its base, a small blue stone in the middle
    a, b, w = FINGERS[RING_FINGER]
    t = 0.13
    cx = a[0] + (b[0] - a[0]) * t; cy = a[1] + (b[1] - a[1]) * t
    ux, uy = (b[0] - a[0]), (b[1] - a[1]); n = math.hypot(ux, uy); ux, uy = ux / n, uy / n
    px, py = -uy, ux
    half_w, half_h = w * 0.58 * max(1.0, ring * 0.8), 20 * ring
    band = [(cx - px * half_w - ux * half_h, cy - py * half_w - uy * half_h), (cx + px * half_w - ux * half_h, cy + py * half_w - uy * half_h),
            (cx + px * half_w + ux * half_h, cy + py * half_w + uy * half_h), (cx - px * half_w + ux * half_h, cy - py * half_w + uy * half_h)]
    d.polygon([S_(p) for p in band], fill=silver, outline=dark, width=max(1, int(4 * scale)))
    r = 16 * ring
    d.ellipse((S_((cx - r, cy - r))[0], S_((cx - r, cy - r))[1], S_((cx + r, cy + r))[0], S_((cx + r, cy + r))[1]),
              fill=stone, outline=dark, width=max(1, int(3 * scale)))
    return L


(OUT / "Kanji").mkdir(parents=True, exist_ok=True)
for name, letter in {"Paste": "Pa", "RctOutput": "RO", "Domain": "AL"}.items():
    tile(YUTA, letter).save(OUT / f"{name}.png")
# the Copy icon: the hand, cropped to its middle, on the purple tile
K = 4
# the whole hand on the tile
CROP = (40, 210, 1100, 1270)          # the whole hand; the ring drawn twice as big so it reads
sc = K * S / (CROP[2] - CROP[0])
big = hand_layer(sc, (240, 232, 214, 255), (92, 52, 140, 255), (225, 228, 236, 255), (60, 120, 230, 255), (40, 40, 52, 255), ring=2.0)
box = tuple(int(v * sc) for v in CROP)
mask = Image.new("L", (S, S), 0); ImageDraw.Draw(mask).rounded_rectangle((10, 10, S - 11, S - 11), radius=12, fill=255)
layer = big.crop(box).resize((S, S), Image.LANCZOS)
layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (S, S), 0), mask))
icon = tile(YUTA)
icon.alpha_composite(layer)
icon.save(OUT / "Copy.png")
for name, text in {"Copy": "模倣", "Domain": "真贋相愛"}.items():
    kanji_tile(YUTA, text).save(OUT / "Kanji" / f"{name}.png")
for path in ("UI/Backgrounds/YutaPath.png", "UI/AlternativeBackgrounds/PathBG_Yuta.png"):
    (T / path).parent.mkdir(parents=True, exist_ok=True)
    img = base((34, 20, 48), (9, 5, 14), (140, 90, 200))
    img.alpha_composite(hand_layer(1.0, (175, 140, 230, 130), (60, 40, 90, 150), (215, 220, 232, 235), (70, 130, 235, 255), (40, 40, 55, 235)))
    img.save(T / path)
print("ok")
