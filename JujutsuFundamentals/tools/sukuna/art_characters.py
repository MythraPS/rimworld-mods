"""Hair and face markings for Yuji Itadori and Ryomen Sukuna, drawn in RimWorld's style
(white/grey fill, tinted by hair colour in game; ~3px black outline at 128px).

Geometry is measured from vanilla Male_Average_Normal heads (extracted with UnityPy):
  south/north: head x 41-86, y 39-88; eyes at x 51-59 and 68-76, y 67-72
  east:        head x 39-88, y 40-87; eye  at x 74-81, y 67-72
Drawn at 4x and downsampled.

  python tools/art_characters.py HEADS_DIR OUT_TEXTURES_DIR [PREVIEW.png]
HEADS_DIR holds Male_Average_Normal_{south,east,north}.png (vanilla, for fitting only;
they are not shipped)."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

K = 4                      # supersample
N = 128 * K
DIRS = ("south", "east", "north")


def head_mask(heads, d, grow=2):
    a = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA").getchannel("A")
    a = a.resize((N, N), Image.BICUBIC).point(lambda v: 255 if v > 100 else 0)
    if grow:
        a = a.filter(ImageFilter.MaxFilter(2 * grow * K + 1))
    return a


def region_below(fn):
    """Mask of pixels with y <= fn(x) (x, y in 128-space)."""
    m = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(m)
    pts = [(x * K, fn(x) * K) for x in [i / 2 for i in range(0, 257)]]
    d.polygon([(0, 0)] + pts + [(N, 0)], fill=255)
    return m


def poly(pts, scale=K):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon([(x * scale, y * scale) for x, y in pts], fill=255)
    return m


def union(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = ImageChops.lighter(out, m)
    return out


def inter(a, b):
    return ImageChops.multiply(a, b)


def minus(a, b):
    return ImageChops.subtract(a, b)


def spike(bx0, bx1, by, tx, ty, by1=None):
    """A spike: base from (bx0,by) to (bx1,by1 or by), tip at (tx,ty)."""
    return poly([(bx0, by), (tx, ty), (bx1, by if by1 is None else by1)])


def vgrad(top, bottom, hi=252, lo=200):
    g = Image.new("L", (1, N))
    for y in range(N):
        t = min(1, max(0, (y / K - top) / max(1, bottom - top)))
        g.putpixel((0, y), int(hi + (lo - hi) * t))
    return g.resize((N, N))


def finish(fill_mask, shade_masks, lines, outline_px=3):
    """fill_mask: whole hair. shade_masks: [(mask, grey)] painted over the gradient.
    lines: [(points, width_px, grey)] strand/separation strokes (128-space)."""
    ys = [y for y in range(N) if fill_mask.crop((0, y, N, y + 1)).getbbox()]
    top, bot = (ys[0] / K, ys[-1] / K) if ys else (0, 128)
    base = Image.merge("RGB", [vgrad(top, bot)] * 3)
    for m, grey in shade_masks:
        base.paste((grey, grey, grey), mask=m)
    dr = ImageDraw.Draw(base)
    for pts, w, grey in lines:
        dr.line([(x * K, y * K) for x, y in pts], fill=(grey, grey, grey), width=int(w * K), joint="curve")
    outer = fill_mask.filter(ImageFilter.MaxFilter(2 * outline_px * K // 2 * 2 + 1))
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    img.paste((0, 0, 0, 255), mask=outer)
    inner = fill_mask.filter(ImageFilter.MinFilter(3))
    img.paste(base, mask=inner)
    return img.resize((128, 128), Image.LANCZOS)


# ---------------------------------------------------------------- Yuji
def yuji(heads, d):
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, region_below(lambda x: 52 if 47 <= x <= 80 else 47))
        under = minus(inter(hm, region_below(lambda x: 58)), cap)
        under = inter(under, union(poly([(30, 0), (47, 0), (47, 70), (30, 70)]),
                                   poly([(80, 0), (98, 0), (98, 70), (80, 70)])))
        spikes = union(
            spike(44, 54, 44, 41, 29), spike(50, 60, 40, 51, 25), spike(57, 67, 39, 61, 22),
            spike(63, 73, 39, 68, 23), spike(70, 80, 41, 78, 26), spike(76, 84, 45, 86, 31),
            # fringe hanging over the forehead
            spike(52, 60, 50, 56, 57), spike(59, 67, 50, 63, 58), spike(66, 74, 50, 70, 56))
        lines = [([(51, 30), (54, 40)], 0.8, 175), ([(62, 25), (63, 38)], 0.8, 175),
                 ([(70, 27), (68, 39)], 0.8, 175), ([(78, 31), (75, 42)], 0.8, 175)]
    elif d == "east":
        cap = inter(hm, region_below(lambda x: 51 if x >= 62 else 51 + (62 - x) * 0.35))
        under = minus(inter(hm, region_below(lambda x: 58 if x >= 62 else 64 + (62 - x) * 0.25)), cap)
        under = inter(under, poly([(30, 0), (74, 0), (74, 80), (30, 80)]))
        spikes = union(
            spike(42, 52, 46, 33, 33), spike(48, 58, 42, 42, 25), spike(55, 65, 40, 53, 21),
            spike(62, 72, 40, 66, 22), spike(69, 79, 42, 78, 27), spike(76, 84, 46, 89, 34),
            spike(78, 86, 49, 87, 57))
        lines = [([(44, 29), (51, 41)], 0.8, 175), ([(55, 24), (59, 38)], 0.8, 175),
                 ([(67, 25), (66, 38)], 0.8, 175)]
    else:  # north
        cap = inter(hm, region_below(lambda x: 58))
        under = minus(inter(hm, region_below(lambda x: 70)), cap)
        spikes = union(
            spike(42, 52, 46, 39, 30), spike(49, 59, 41, 49, 25), spike(56, 66, 39, 59, 22),
            spike(62, 72, 39, 68, 22), spike(69, 79, 41, 78, 25), spike(75, 85, 46, 88, 30))
        lines = [([(50, 28), (53, 40)], 0.8, 175), ([(61, 25), (62, 38)], 0.8, 175),
                 ([(70, 26), (69, 39)], 0.8, 175), ([(64, 46), (64, 57)], 0.8, 190)]
    hair = union(cap, spikes, under)
    sep = []   # the undercut edge is its own shade, outlined lightly
    return finish(hair, [(under, 128)], lines + sep)


# ---------------------------------------------------------------- Sukuna
# Same body, hair swept up and back off the forehead, longer and sharper.
def shorten(pts, f, base=40):
    """Pull points above the hairline (y < base) toward it: f=1 keeps them, f=0.5 halves the length."""
    return [(x, base + (y - base) * f) if y < base else (x, y) for x, y in pts]


def sukuna(heads, d, f=1.0):
    """f: spike length. 1 = the original; the King now wears 0.65 (her call 2026-11-01: shorter),
    a vessel not yet in the four-armed form 0.35 (slightly spiky, in the pawn's own hair colour)."""
    def spike(bx0, bx1, by, tx, ty, by1=None):
        mx, my = (bx0 + bx1) / 2, (by + (by if by1 is None else by1)) / 2   # pull the tip toward its own base
        tx, ty = mx + (tx - mx) * f, my + (ty - my) * f
        return globals()["spike"](bx0, bx1, by, tx, ty, by1)
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, region_below(lambda x: 47 if 48 <= x <= 79 else 44))
        under = inter(minus(inter(hm, region_below(lambda x: 57)), cap),
                      union(poly([(30, 0), (47, 0), (47, 70), (30, 70)]),
                            poly([(80, 0), (98, 0), (98, 70), (80, 70)])))
        spikes = union(
            spike(42, 52, 45, 33, 26), spike(48, 58, 41, 44, 17), spike(55, 64, 39, 56, 12),
            spike(63, 72, 39, 71, 12), spike(69, 79, 41, 83, 17), spike(75, 85, 45, 94, 26))
        lines = [([(45, 22), (51, 38)], 0.8, 170), ([(57, 16), (59, 36)], 0.8, 170),
                 ([(69, 16), (67, 36)], 0.8, 170), ([(81, 22), (76, 38)], 0.8, 170)]
    elif d == "east":
        cap = inter(hm, region_below(lambda x: 47 if x >= 64 else 47 + (64 - x) * 0.4))
        under = inter(minus(inter(hm, region_below(lambda x: 58 if x >= 64 else 64 + (64 - x) * 0.25)), cap),
                      poly([(30, 0), (76, 0), (76, 80), (30, 80)]))
        spikes = union(
            spike(40, 50, 52, 22, 38), spike(44, 54, 46, 27, 27), spike(50, 60, 42, 36, 17),
            spike(58, 68, 40, 50, 11), spike(66, 76, 40, 64, 10), spike(73, 82, 43, 77, 16))
        lines = [([(31, 31), (47, 44)], 0.8, 170), ([(41, 20), (53, 39)], 0.8, 170),
                 ([(54, 14), (60, 37)], 0.8, 170), ([(66, 14), (70, 38)], 0.8, 170)]
    else:
        cap = inter(hm, region_below(lambda x: 58))
        under = minus(inter(hm, region_below(lambda x: 70)), cap)
        spikes = union(
            spike(41, 51, 47, 31, 27), spike(47, 57, 42, 43, 17), spike(55, 64, 39, 56, 12),
            spike(63, 72, 39, 71, 12), spike(70, 80, 42, 84, 17), spike(76, 86, 47, 96, 27))
        lines = [([(46, 22), (51, 38)], 0.8, 170), ([(58, 16), (60, 36)], 0.8, 170),
                 ([(69, 16), (67, 36)], 0.8, 170), ([(81, 22), (76, 38)], 0.8, 170)]
    lines = [(shorten(pts, f), w, g) for pts, w, g in lines]
    return finish(union(cap, spikes, under), [(under, 120)], lines)


# ---------------------------------------------------------------- markings
# The second pair of eyes sits under the first, with two bands across each cheek
# and a mark over the nose bridge. North: nothing (the face is turned away).
def markings(d):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    ink = (20, 12, 12, 255)

    def ln(pts, w):
        dr.line([(x * K, y * K) for x, y in pts], fill=ink, width=int(w * K), joint="curve")

    def eye(cx, cy, w, h):
        # an almond the size of the real eye above it, pointed at both corners
        pts = [(cx - w, cy)]
        for i in range(1, 12):
            t = i / 12
            pts.append((cx - w + 2 * w * t, cy - h * (1 - (2 * t - 1) ** 2)))
        pts.append((cx + w, cy))
        for i in range(11, 0, -1):
            t = i / 12
            pts.append((cx - w + 2 * w * t, cy + h * 0.8 * (1 - (2 * t - 1) ** 2)))
        dr.polygon([(x * K, y * K) for x, y in pts], fill=ink)

    if d == "south":
        # real eyes: x 51-59 / 68-76, y 67-72. The second pair sits right under them.
        for cx in (55, 72):
            eye(cx, 78.5, 3.6, 1.7)
        # two bands across each cheek, inside the jaw line (face is x 48-79 here)
        ln([(46, 76), (50, 76.5)], 1.3); ln([(47, 81), (50.5, 81.5)], 1.3)
        ln([(77, 76.5), (81, 76)], 1.3); ln([(76.5, 81.5), (80, 81)], 1.3)
    elif d == "east":
        eye(77.5, 78.5, 3.2, 1.7)
        ln([(63, 76), (69, 76.5)], 1.3); ln([(64, 81), (70, 81.5)], 1.3)
    return img.resize((128, 128), Image.LANCZOS)


def main():
    heads, out = Path(sys.argv[1]), Path(sys.argv[2])
    hairs = out / "Things/Pawn/Humanlike/Hairs"
    tats = out / "Things/Pawn/Humanlike/Tattoos/Sukuna_Markings"
    hairs.mkdir(parents=True, exist_ok=True)
    tats.mkdir(parents=True, exist_ok=True)
    made = {}
    for d in DIRS:
        made[("yuji", d)] = yuji(heads, d)
        made[("sukuna", d)] = sukuna(heads, d, 0.65)
        made[("vessel", d)] = sukuna(heads, d, 0.35)
        made[("vessel", d)].save(hairs / f"Sukuna_VesselHair_{d}.png")
        made[("marks", d)] = markings(d)
        made[("yuji", d)].save(hairs / f"Sukuna_YujiHair_{d}.png")
        made[("sukuna", d)].save(hairs / f"Sukuna_KingHair_{d}.png")
        made[("marks", d)].save(tats / f"Sukuna_Markings_{d}.png")

    if len(sys.argv) > 3:   # preview: tinted onto vanilla heads, 3x
        def tint(img, rgb):
            r, g, b, a = img.split()
            px = Image.merge("RGB", (r, g, b))
            col = Image.new("RGB", img.size, rgb)
            return Image.merge("RGBA", (*ImageChops.multiply(px, col).split(), a))
        skin = (236, 196, 160)
        rows = [("yuji", (232, 140, 160), False), ("sukuna", (232, 140, 160), True), ("vessel", (90, 60, 40), True)]
        sheet = Image.new("RGBA", (128 * 3, 128 * 3), (120, 120, 120, 255))
        for r, (who, col, marked) in enumerate(rows):
            for c, d in enumerate(DIRS):
                head = tint(Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA"), skin)
                if marked:
                    head = Image.alpha_composite(head, made[("marks", d)])
                head = Image.alpha_composite(head, tint(made[(who, d)], col))
                sheet.alpha_composite(head, (c * 128, r * 128))
        sheet.resize((128 * 9, 128 * 9), Image.NEAREST).save(sys.argv[3])


main()
