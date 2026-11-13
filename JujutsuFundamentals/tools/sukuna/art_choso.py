"""Choso's art (2026-11-11): the single-target Piercing Blood icon (Scatter's frame and colours, one straight
beam), and the dark mark across the bridge of his nose (face tattoo, vanilla Male_Average_Normal geometry).

  python tools/sukuna/art_choso.py"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

MOD = Path(__file__).resolve().parents[2]
T = MOD / "Textures"
K = 4
CREAM = (238, 230, 208, 255)


def frame_from(icon_path, fill):
    """Same as art_arc.py: the rounded frame of an existing icon, its inside flooded in `fill`."""
    im = Image.open(icon_path).convert("RGBA")
    px = im.load()
    out = im.copy(); po = out.load()
    for y in range(128):
        for x in range(128):
            r, g, b, a = px[x, y]
            if a and r + g + b >= 140:
                po[x, y] = fill[:3] + (a,)
    return out


def piercing_single():
    frame = frame_from(T / "Abilities/Sukuna/Dismantle.png", (122, 20, 30, 255))
    N = 128 * K
    sym = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(sym)
    sx, sy, ex, ey = 34, 94, 92, 36                      # one straight beam, lower left to upper right
    d.ellipse([(sx - 10) * K, (sy - 10) * K, (sx + 10) * K, (sy + 10) * K], fill=CREAM)   # the compressed blood
    d.line([(sx * K, sy * K), (ex * K, ey * K)], fill=CREAM, width=int(6 * K))
    ang = math.atan2(ey - sy, ex - sx)
    tip = ((ex + 10 * math.cos(ang)) * K, (ey + 10 * math.sin(ang)) * K)
    l = ((ex + 7 * math.cos(ang + 2.3)) * K, (ey + 7 * math.sin(ang + 2.3)) * K)
    r = ((ex + 7 * math.cos(ang - 2.3)) * K, (ey + 7 * math.sin(ang - 2.3)) * K)
    d.polygon([tip, l, r], fill=CREAM)
    frame.alpha_composite(sym.resize((128, 128), Image.LANCZOS))
    frame.save(T / "Abilities/Sukuna/PiercingBlood.png")


MARK = (40, 30, 34, 235)


def nose_mark(d):
    """A dark band straight across the bridge of the nose, cheek to cheek."""
    img = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0)); dr = ImageDraw.Draw(img)
    # just under the eyes (vanilla eyes are rows 67-73 on Male_Average_Normal, measured 2026-11-11)
    if d == "south":
        dr.rounded_rectangle([52 * K, 75 * K, 76 * K, 78.5 * K], radius=int(1.5 * K), fill=MARK)
    elif d in ("east", "west"):
        x0, x1 = (74, 89) if d == "east" else (39, 54)
        dr.rounded_rectangle([x0 * K, 75 * K, x1 * K, 78.5 * K], radius=int(1.5 * K), fill=MARK)
    return img.resize((128, 128), Image.LANCZOS)


piercing_single()
out = T / "Things/Pawn/Humanlike/Tattoos/Choso_NoseMark"
out.mkdir(parents=True, exist_ok=True)
for d in ("south", "east", "north", "west"):
    nose_mark(d).save(out / f"Choso_NoseMark_{d}.png")
print("ok")
