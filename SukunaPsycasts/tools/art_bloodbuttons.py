"""Icons for Yuji's Blood to focus / Focus to blood buttons (2026-11-06, her note: Blood to focus
looked the same as Piercing Blood: Scatter, and Focus to blood borrowed RCT's). Same rounded frame
and cream symbols as the other Shrine icons: a blood drop and a psyfocus ring with an arrow between.
  python tools/art_bloodbuttons.py TEXTURES_DIR"""
import math, sys
from pathlib import Path
from PIL import Image, ImageDraw

T = Path(sys.argv[1]); K = 4; N = 128 * K
CREAM, INK = (238, 230, 208, 255), (24, 20, 26, 255)


def frame_from(icon_path, fill):
    """The rounded frame of an existing icon, its inside flooded in `fill` (copied from art_arc.py,
    which can't be imported: it redraws its own textures when loaded)."""
    im = Image.open(icon_path).convert("RGBA")
    px = im.load(); out = im.copy(); po = out.load()
    for y in range(128):
        for x in range(128):
            r, g, b, a = px[x, y]
            if a and r + g + b >= 140: po[x, y] = fill[:3] + (a,)
    return out


def drop(d, cx, cy, r):
    pts = [(cx + r * math.cos(a), cy + r * math.sin(a)) for a in [math.pi * i / 24 for i in range(0, 25)]]
    pts += [(cx - r, cy), (cx, cy - 2.3 * r), (cx + r, cy)]
    d.polygon([(x * K, y * K) for x, y in [(cx, cy - 2.3 * r)] + pts[:25]], fill=CREAM)


def ring(d, cx, cy, r):
    d.ellipse([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], outline=CREAM, width=int(6 * K))
    d.ellipse([(cx - r * 0.35) * K, (cy - r * 0.35) * K, (cx + r * 0.35) * K, (cy + r * 0.35) * K], fill=CREAM)


def arrow(d, x0, x1, y):
    d.line([x0 * K, y * K, (x1 - 6) * K, y * K], fill=CREAM, width=int(5 * K))
    d.polygon([(x1 * K, y * K), ((x1 - 11) * K, (y - 8) * K), ((x1 - 11) * K, (y + 8) * K)], fill=CREAM)


def make(name, fill, first, second):
    frame = frame_from(T / "Abilities/Sukuna/Dismantle.png", fill)
    sym = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(sym)
    first(d, 36, 74); arrow(d, 52, 80, 64); second(d, 96, 64)
    frame.alpha_composite(sym.resize((128, 128), Image.LANCZOS))
    frame.save(T / f"Abilities/Sukuna/{name}.png")


make("BloodToFocus", (122, 20, 30, 255), lambda d, x, y: drop(d, x - 6, y, 13), lambda d, x, y: ring(d, x - 6, y, 15))
make("FocusToBlood", (88, 52, 140, 255), lambda d, x, y: ring(d, x - 6, y - 10, 15), lambda d, x, y: drop(d, x - 6, y + 10, 13))
