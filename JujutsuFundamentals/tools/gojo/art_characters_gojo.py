"""Gojo's hair and blindfold, in the same style and on the same measured head
geometry as the Sukuna mod's art_characters.py (whose helpers are loaded from it).

  python tools/art_characters_gojo.py SUKUNA_TOOLS_DIR HEADS_DIR OUT_TEXTURES_DIR [PREVIEW.png]

Head geometry (vanilla Male_Average_Normal): south/north head x 41-86, y 39-88,
eyes x 51-59 and 68-76, y 67-72; east head x 39-88, eye x 74-81, y 67-72.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageChops

src = Path(sys.argv[1]) / "art_characters.py"
code = src.read_text().split("# ---------------------------------------------------------------- Yuji")[0]
exec(code)   # K, N, DIRS, head_mask, region_below, poly, union, inter, minus, spike, finish


# Hair pushed up by the blindfold: white, untidy spikes. Second pass (2026-10-28,
# her note "too long and sharp"): every spike is half as tall above its base and
# the tips are rounded off.
LENGTH = 0.55         # share of the first version's spike height kept
ROUND_PX = 1          # tip rounding radius, in 128-space pixels


def spike(bx0, bx1, by, tx, ty, by1=None):
    mid = (bx0 + bx1) / 2
    return poly([(bx0, by), (mid + (tx - mid) * LENGTH, by - (by - ty) * LENGTH), (bx1, by if by1 is None else by1)])


def soften(m):
    from PIL import ImageFilter
    return m.filter(ImageFilter.GaussianBlur(ROUND_PX * K)).point(lambda v: 255 if v >= 128 else 0)


def shorten(lines):
    """Strand strokes follow the shorter spikes: heights above y=45 are halved."""
    return [([(x, 45 - (45 - y) * LENGTH if y < 45 else y) for x, y in pts], w, g) for pts, w, g in lines]


def gojo(heads, d):
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, region_below(lambda x: 58))
        spikes = union(
            spike(40, 50, 52, 30, 30), spike(45, 55, 46, 38, 18), spike(51, 61, 42, 48, 9),
            spike(58, 67, 40, 60, 5), spike(64, 73, 40, 70, 6), spike(70, 80, 42, 81, 11),
            spike(76, 86, 47, 92, 20), spike(81, 89, 53, 97, 33),
            spike(44, 54, 49, 42, 24), spike(72, 82, 46, 86, 27))
        lines = [([(40, 26), (49, 44)], 0.8, 205), ([(50, 13), (55, 40)], 0.8, 205),
                 ([(64, 9), (65, 38)], 0.8, 205), ([(77, 14), (73, 40)], 0.8, 205), ([(88, 26), (80, 45)], 0.8, 205)]
    elif d == "east":
        cap = inter(hm, region_below(lambda x: 58 if x >= 62 else 58 + (62 - x) * 0.3))
        spikes = union(
            spike(38, 48, 56, 20, 40), spike(40, 50, 50, 24, 27), spike(45, 55, 45, 33, 15),
            spike(52, 62, 42, 46, 7), spike(60, 70, 41, 60, 5), spike(67, 77, 42, 74, 10),
            spike(74, 83, 46, 87, 19), spike(79, 88, 52, 94, 31))
        lines = [([(29, 30), (45, 47)], 0.8, 205), ([(39, 18), (52, 42)], 0.8, 205),
                 ([(52, 10), (59, 39)], 0.8, 205), ([(68, 10), (68, 40)], 0.8, 205)]
    else:
        # The back (her note 2026-10-28: "more uniform"): eight spikes evenly spaced,
        # all the same height, edges leaning out a little.
        cap = inter(hm, region_below(lambda x: 66))
        # Same outline as before, made symmetric: evenly spaced bases, heights
        # rising smoothly to the middle and mirrored left to right.
        bx = [40 + i * 5.9 for i in range(8)]
        base = [53, 47, 42, 40, 40, 42, 47, 53]
        tip_y = [32, 19, 10, 5, 5, 10, 19, 32]
        lean = [-21, -14, -8, -3, 3, 8, 14, 21]
        spikes = union(*[spike(x, x + 9.5, base[i], x + 4.75 + lean[i], tip_y[i]) for i, x in enumerate(bx)])
        lines = [([(bx[i] + 9.5 + lean[i] * 0.5, max(tip_y[i], tip_y[i + 1]) + 14), (bx[i] + 9.5, 44)], 0.8, 205) for i in (1, 3, 5)]
    return finish(union(cap, soften(spikes)), [], shorten(lines))


def blindfold(heads, d):
    """A black band over the eyes, wrapping the head, knotted at the back."""
    hm = head_mask(heads, d, grow=1)
    band = Image.new("L", (N, N), 0)
    dr = ImageDraw.Draw(band)
    if d == "south":
        dr.rectangle((0, 63 * K, N, 74 * K), fill=255)
    elif d == "east":
        dr.polygon([(30 * K, 62 * K), (N, 63 * K), (N, 74 * K), (30 * K, 73 * K)], fill=255)
    else:
        dr.rectangle((0, 63 * K, N, 74 * K), fill=255)
    band = inter(band, hm)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    outline = band.filter(__import__("PIL.ImageFilter", fromlist=["x"]).MaxFilter(2 * K + 1))
    img.paste((8, 8, 10, 255), mask=outline)
    img.paste((26, 26, 32, 255), mask=band)
    fold = Image.new("L", (N, N), 0)
    ImageDraw.Draw(fold).line([(0, 67 * K), (N, 67 * K)], fill=255, width=K)
    img.paste((44, 44, 54, 255), mask=inter(fold, band.filter(__import__("PIL.ImageFilter", fromlist=["x"]).MinFilter(2 * K + 1))))   # a fold in the cloth
    if d == "north":   # the knot and its tails
        knot = poly([(60, 64), (68, 64), (69, 74), (59, 74)])
        tails = union(poly([(60, 73), (64, 73), (58, 92), (54, 91)]), poly([(64, 73), (68, 73), (73, 92), (69, 93)]))
        k = union(knot, tails)
        img.paste((8, 8, 10, 255), mask=k.filter(__import__("PIL.ImageFilter", fromlist=["x"]).MaxFilter(2 * K + 1)))
        img.paste((26, 26, 32, 255), mask=k)
    if d == "east":   # the tails behind the head
        tails = union(poly([(36, 66), (40, 64), (30, 86), (26, 84)]), poly([(38, 70), (42, 70), (37, 90), (33, 89)]))
        img.paste((8, 8, 10, 255), mask=tails.filter(__import__("PIL.ImageFilter", fromlist=["x"]).MaxFilter(2 * K + 1)))
        img.paste((26, 26, 32, 255), mask=tails)
    return img.resize((128, 128), Image.LANCZOS)


def eyes(heads, d):
    """Six Eyes, for when the blindfold is off (her call 2026-10-31): the vanilla
    head's own iris pixels (dark pixels on rows 69-72, under the lash line) painted
    bright blue, with a pale glint. Nothing on the north view."""
    img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    if d == "north":
        return img
    head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA")
    boxes = [(48, 80)] if d == "south" else [(77, 84)]   # x 74 is a lash tip, x 84+ the head outline
    blobs = []
    for x0, x1 in boxes:
        for y in range(69, 73):
            for x in range(x0, x1):
                r, g, b, a = head.getpixel((x, y))
                if a > 0 and r + g + b < 200:
                    blobs.append((x, y))
    for x, y in blobs:
        img.putpixel((x, y), (70, 175, 255, 255) if y < 72 else (40, 110, 220, 255))
    # a glint: the top-left iris pixel of each eye
    seen = set()
    for x, y in sorted(blobs, key=lambda q: (q[1], q[0])):
        side = x < 64
        if y == 69 and side not in seen:
            seen.add(side); img.putpixel((x, y), (225, 245, 255, 255))
    return img


def main():
    heads, out = Path(sys.argv[2]), Path(sys.argv[3])
    hairs = out / "Things/Pawn/Humanlike/Hairs"
    tats = out / "Things/Pawn/Humanlike/Tattoos/Gojo_Blindfold"
    hairs.mkdir(parents=True, exist_ok=True); tats.mkdir(parents=True, exist_ok=True)
    made = {}
    for d in DIRS:
        made[("hair", d)] = gojo(heads, d); made[("fold", d)] = blindfold(heads, d)
        # the hair itself is drawn by the Sukuna mod's tools/art_hair3.py since 2026-11-07
        made[("fold", d)].save(tats / f"Gojo_Blindfold_{d}.png")
        eyedir = out / "Things/Pawn/Humanlike/Tattoos/Gojo_Eyes"; eyedir.mkdir(parents=True, exist_ok=True)
        eyes(heads, d).save(eyedir / f"Gojo_Eyes_{d}.png")
    if len(sys.argv) > 4:
        def tint(img, rgb):
            r, g, b, a = img.split()
            col = Image.new("RGB", img.size, rgb)
            return Image.merge("RGBA", (*ImageChops.multiply(Image.merge("RGB", (r, g, b)), col).split(), a))
        sheet = Image.new("RGBA", (128 * 3, 128), (120, 120, 120, 255))
        for c, d in enumerate(DIRS):
            head = tint(Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA"), (236, 206, 180))
            head = Image.alpha_composite(head, made[("fold", d)])
            head = Image.alpha_composite(head, tint(made[("hair", d)], (245, 247, 255)))
            sheet.alpha_composite(head, (c * 128, 0))
        sheet.resize((128 * 9, 128 * 3), Image.NEAREST).save(sys.argv[4])


main()
