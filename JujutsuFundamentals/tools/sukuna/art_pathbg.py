"""Shrine path background (her ask 2026-11-10): the Malevolent Shrine drawing from the ability icon
(GojoPsycasts/tools/art_icons.py shrine(): two-tier roof, fanged mouth between the pillars, a heap of
skulls), drawn large and clearly over the path's dark red, framed like the other paths. Same picture
in the alt background. Old images: ~/rimworld-backups/*-2026-11-10-before-shrinebg.png

  python tools/art_pathbg.py
"""
from pathlib import Path
from PIL import Image, ImageDraw

MOD = Path(__file__).resolve().parent.parent.parent   # tools/sukuna -> the mod
W, H = 950, 1515
TOP, BOT, EDGE = (58, 19, 22), (10, 6, 7), (140, 58, 48)
INK = (236, 226, 208, 125)   # the icon's cream, see-through so the nodes stay readable
HOLE = (20, 10, 14, 255)


def base():
    img = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(TOP, BOT)) + (255,))
    d.rectangle((24, 24, W - 25, H - 25), outline=EDGE + (255,), width=6)
    return img


def shrine():
    """The icon's shapes, in its own 128-unit space, scaled up around the middle of the page."""
    L = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    k = 7.0; ox, oy = W / 2 - 64 * k, H / 2 - 64 * k
    P = lambda pts: [(ox + x * k, oy + y * k) for x, y in pts]
    w = INK
    for cx, cy, r in ((40, 100, 9), (58, 103, 9), (76, 103, 9), (93, 100, 9), (49, 90, 8), (67, 92, 8), (84, 90, 8)):
        d.ellipse(P([(cx - r, cy - r), (cx + r, cy + r * 0.9)]), fill=w)
        d.ellipse(P([(cx - r * 0.55, cy - r * 0.2), (cx - r * 0.1, cy + r * 0.3)]), fill=HOLE)
        d.ellipse(P([(cx + r * 0.1, cy - r * 0.2), (cx + r * 0.55, cy + r * 0.3)]), fill=HOLE)
    d.rectangle(P([(36, 56), (92, 84)]), fill=w)
    d.rectangle(P([(44, 60), (84, 82)]), fill=HOLE)
    for x in range(45, 84, 6):
        d.polygon(P([(x, 60), (x + 5, 60), (x + 2.5, 67)]), fill=w)
        d.polygon(P([(x, 82), (x + 5, 82), (x + 2.5, 75)]), fill=w)
    d.polygon(P([(18, 50), (30, 56), (98, 56), (110, 50), (96, 44), (32, 44)]), fill=w)
    d.rectangle(P([(52, 34), (76, 44)]), fill=w)
    d.polygon(P([(36, 30), (46, 36), (82, 36), (92, 30), (80, 24), (48, 24)]), fill=w)
    d.rectangle(P([(61, 16), (67, 24)]), fill=w)
    return L


for path in ("Textures/UI/Backgrounds/SukunaPath.png", "Textures/UI/AlternativeBackgrounds/PathBG_Sukuna.png"):
    img = base()
    img.alpha_composite(shrine())
    img.save(MOD / path)
print("ok")
