"""Sukuna's second pair of shoulders and his abdominal mouth, in RimWorld's body style (near-white fill that darkens
softly toward a ~3px black outline; the arms are tinted by skin colour in game, the mouth is not).
Fitted to the vanilla Naked_<Body>_<dir> silhouettes (extracted with UnityPy; not shipped).

Second pass (her note 2026-11-02: the stick arms looked ugly): each arm is a tapered limb bent at the
elbow with a mitten hand. South/north: both pairs come out of the sides (shoulder and lower ribs) and
hang bent; east: forearms reaching forward. Always drawn BEHIND the body so only what sticks out shows.

  python tools/art_fourarms.py BODIES_DIR OUT_TEXTURES_DIR [PREVIEW.png]"""
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

K = 4
N = 128 * K
BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")


def edges(alpha, y):
    xs = [x for x in range(128) if alpha.getpixel((x, int(min(127, max(0, y))))) > 100]
    return (xs[0], xs[-1]) if xs else (64, 64)


def S(pts):
    return [(x * K, y * K) for x, y in pts]


def qbez(p0, p1, p2, n=24):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]


def limb(mask, shoulder, elbow, wrist, w0, w1, hand):
    """Tapered upper arm and forearm, round joints, mitten hand past the wrist."""
    d = ImageDraw.Draw(mask)
    segs = [(shoulder, elbow, w0, (w0 + w1) / 2), (elbow, wrist, (w0 + w1) / 2, w1)]
    for a, b, wa, wb in segs:
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        d.polygon(S([(a[0] + nx * wa / 2, a[1] + ny * wa / 2), (b[0] + nx * wb / 2, b[1] + ny * wb / 2),
                     (b[0] - nx * wb / 2, b[1] - ny * wb / 2), (a[0] - nx * wa / 2, a[1] - ny * wa / 2)]), fill=255)
        r = wa / 2
        d.ellipse(S([(a[0] - r, a[1] - r), (a[0] + r, a[1] + r)]), fill=255)
    # mitten hand: an ellipse a little past the wrist, along the forearm
    dx, dy = wrist[0] - elbow[0], wrist[1] - elbow[1]
    L = math.hypot(dx, dy) or 1
    c = (wrist[0] + dx / L * hand * 0.55, wrist[1] + dy / L * hand * 0.55)
    ang = math.degrees(math.atan2(dy, dx))
    hm = Image.new("L", (N, N), 0)
    ImageDraw.Draw(hm).ellipse(S([(c[0] - hand, c[1] - hand * 0.78), (c[0] + hand, c[1] + hand * 0.78)]), fill=255)
    hm = hm.rotate(-ang, center=(c[0] * K, c[1] * K), resample=Image.BICUBIC)
    mask.paste(255, mask=hm.point(lambda v: 255 if v > 128 else 0))


def finish(mask, lo=200):
    edge = mask.filter(ImageFilter.GaussianBlur(4 * K))
    shade = edge.point(lambda v: int(lo + (247 - lo) * min(1, v / 190)))
    outer = mask.filter(ImageFilter.MaxFilter(int(2.6 * K) * 2 + 1))
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    img.paste((0, 0, 0, 255), mask=outer)
    img.paste(Image.merge("RGB", [shade] * 3), mask=mask)
    return img.resize((128, 128), Image.LANCZOS)


def arms(bodies, kind, d):
    """No arms (RimWorld draws none; her call 2026-11-02): only a second pair of shoulders, rounded
    bulges just below the first, sticking out past the sides. Drawn behind the body, so only the
    bulge shows. Side-on they are hidden by the body, so east is empty."""
    alpha = Image.open(bodies / f"Naked_{kind}_{d}.png").convert("RGBA").getchannel("A")
    x0, top, x1, bot = alpha.getbbox()
    h = bot - top
    s = (h / 65.0) ** 0.6
    mask = Image.new("L", (N, N), 0)
    if d in ("south", "north"):
        # a smooth bulge out of each side, below the first shoulders, blending into the body at both ends
        y0b, y1b, A = top + 0.22 * h, top + 0.60 * h, 4.8 * s
        dr = ImageDraw.Draw(mask)
        for side in (-1, 1):
            outer, inner = [], []
            for i in range(41):
                t = i / 40
                y = y0b + (y1b - y0b) * t
                l, r = edges(alpha, y)
                e = r if side > 0 else l
                bulge = A * math.sin(math.pi * t) ** 0.8
                outer.append((e + side * (bulge - 1.5), y))
                inner.append((e - side * 6, y))
            dr.polygon(S(outer + inner[::-1]), fill=255)
    return finish(mask)


def mouth(bodies, kind, d):
    """A wide grinning mouth across the belly (south), a short one on the side (east), none from behind."""
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    if d == "north":
        return img.resize((128, 128))
    alpha = Image.open(bodies / f"Naked_{kind}_{d}.png").convert("RGBA").getchannel("A")
    x0, top, x1, bot = alpha.getbbox()
    h = bot - top
    y = top + 0.60 * h
    l, r = edges(alpha, y)
    dr = ImageDraw.Draw(img)
    ink, teeth, lip = (28, 14, 16, 255), (245, 242, 232, 255), (120, 40, 50, 255)
    if d == "south":
        cx, hw = (l + r) / 2, min(12.0, (r - l) * 0.34)
        hh = hw * 0.62
    else:
        cx, hw = r - (r - l) * 0.20, min(6.0, (r - l) * 0.22)
        hh = hw * 0.8
    top_lip = qbez((cx - hw, y - hh * 0.2), (cx, y - hh * 0.55), (cx + hw, y - hh * 0.2))
    bot_lip = qbez((cx + hw, y - hh * 0.2), (cx, y + hh * 1.5), (cx - hw, y - hh * 0.2))
    dr.polygon(S(top_lip + bot_lip), fill=ink)
    # two rows of teeth
    up = qbez((cx - hw * 0.8, y - hh * 0.15), (cx, y - hh * 0.35), (cx + hw * 0.8, y - hh * 0.15))
    dr.polygon(S(up + [(cx + hw * 0.7, y + hh * 0.15), (cx, y + hh * 0.05), (cx - hw * 0.7, y + hh * 0.15)]), fill=teeth)
    lo = qbez((cx - hw * 0.6, y + hh * 0.55), (cx, y + hh * 1.0), (cx + hw * 0.6, y + hh * 0.55))
    dr.polygon(S(lo + [(cx + hw * 0.5, y + hh * 0.75), (cx - hw * 0.5, y + hh * 0.75)]), fill=teeth)
    n = 6 if d == "south" else 3
    for i in range(1, n):
        x = cx - hw * 0.8 + 1.6 * hw * 0.8 * i / n
        dr.line(S([(x, y - hh * 0.4), (x, y + hh * 0.2)]), fill=ink, width=int(0.7 * K))
    dr.line(S(top_lip + bot_lip + top_lip[:1]), fill=lip, width=int(1.2 * K))
    return img.resize((128, 128), Image.LANCZOS)


def main():
    bodies = Path(sys.argv[1])
    out = Path(sys.argv[2]) / "Things/Pawn/Humanlike/Sukuna_FourArms"
    outm = Path(sys.argv[2]) / "Things/Pawn/Humanlike/Sukuna_BellyMouth"
    out.mkdir(parents=True, exist_ok=True)
    outm.mkdir(parents=True, exist_ok=True)
    made = {}
    for kind in BODIES:
        for d in DIRS:
            made[(kind, d)] = (arms(bodies, kind, d), mouth(bodies, kind, d))
            made[(kind, d)][0].save(out / f"Sukuna_FourArms_{kind}_{d}.png")
            made[(kind, d)][1].save(outm / f"Sukuna_BellyMouth_{kind}_{d}.png")
    if len(sys.argv) > 3:
        skin = (236, 196, 160)

        def tint(im):
            r, g, b, a = im.split()
            return Image.merge("RGBA", (*ImageChops.multiply(Image.merge("RGB", (r, g, b)), Image.new("RGB", im.size, skin)).split(), a))
        sheet = Image.new("RGBA", (128 * 3, 128 * len(BODIES)), (120, 120, 120, 255))
        for r, kind in enumerate(BODIES):
            for c, d in enumerate(DIRS):
                body = tint(Image.open(bodies / f"Naked_{kind}_{d}.png").convert("RGBA"))
                arm, mo = tint(made[(kind, d)][0]), made[(kind, d)][1]
                tile = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
                for layer in (arm, body, mo): tile = Image.alpha_composite(tile, layer)
                sheet.alpha_composite(tile, (c * 128, r * 128))
        sheet.resize((128 * 3 * 2, 128 * len(BODIES) * 2), Image.NEAREST).save(sys.argv[3])


main()
