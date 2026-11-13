"""World map icon for Sukuna's lair and faction: Malevolent Shrine's silhouette (two curved roofs
over an open hall on steps). White, so the map can tint it.

  python tools/art_worldicon.py TEXTURES_DIR"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

K = 4
N = 128 * K


def S(pts):
    return [(x * K, y * K) for x, y in pts]


def roof(dr, cx, top, w, h, tip):
    """A curved temple roof: ridge at top, eaves sweeping down and flicking up at the ends."""
    pts = [(cx - w * 0.18, top), (cx + w * 0.18, top)]
    steps = 16
    right = []
    for i in range(steps + 1):
        t = i / steps
        x = cx + w * 0.18 + (w / 2 - w * 0.18) * t
        y = top + h * (t ** 0.6)
        right.append((x, y))
    right.append((cx + w / 2 + tip, top + h - tip * 1.2))
    under = [(cx + w / 2 - 2, top + h + 3), (cx - w / 2 + 2, top + h + 3)]
    left = [(2 * cx - x, y) for x, y in reversed(right)]
    dr.polygon(S(pts[:1] + [pts[1]] + right + under + left), fill=255)


def icon():
    m = Image.new("L", (N, N), 0)
    dr = ImageDraw.Draw(m)
    cx = 64
    roof(dr, cx, 14, 62, 16, 6)                     # upper roof
    dr.rectangle(S([(cx - 16, 32), (cx + 16, 40)]), fill=255)
    roof(dr, cx, 40, 92, 20, 8)                     # lower roof
    for x in (cx - 30, cx - 12, cx + 8, cx + 26):   # pillars of the open hall
        dr.rectangle(S([(x, 63), (x + 4, 94)]), fill=255)
    dr.rectangle(S([(cx - 40, 94), (cx + 40, 100)]), fill=255)   # steps
    dr.rectangle(S([(cx - 48, 100), (cx + 48, 106)]), fill=255)
    dr.rectangle(S([(cx - 56, 106), (cx + 56, 112)]), fill=255)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    edge = m.filter(ImageFilter.MaxFilter(4 * K + 1))
    img.paste((0, 0, 0, 200), mask=edge)
    img.paste((255, 255, 255, 255), mask=m)
    return img.resize((128, 128), Image.LANCZOS)


out = Path(sys.argv[1]) / "World/Sukuna"
out.mkdir(parents=True, exist_ok=True)
icon().save(out / "ShrineIcon.png")
