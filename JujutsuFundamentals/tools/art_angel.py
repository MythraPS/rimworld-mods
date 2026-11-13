"""Hana Kurusu, the Angel (her asks 2026-11-12; canon look from anibase / Pinkvilla / Sportskeeda: a blonde bob with
blunt bangs and the ends curled inward on both sides, a yellow halo floating above her head, bluish-white feathered
wings growing from her upper back).

  python tools/art_angel.py [PREVIEW.png]
Writes:
  Things/Pawn/Humanlike/Hairs/JJK_HanaHair_{south,east,north}.png   (128, white fill tinted by her hair colour)
  Things/Jujutsu/Angel/Halo_{south,east,north}.png                    (128, on her head's frame, drawn above it)
  Things/Jujutsu/Angel/Wings_{south,east,north}.png                   (256, on her body's frame at twice the size)
Drawn on the vanilla Female_Average_Normal head (Looks gives her that head) and the thin body; vanilla parts decoded
into ~/.local/share/rimworld-tools/heads (not shipped)."""
import math, sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parent.parent
T = MOD / "Textures"
PARTS = Path.home() / ".local/share/rimworld-tools/heads"
DIRS = ("south", "east", "north")
S = 4
INK = (28, 26, 30, 255)
HAIR, HAIR_SHADE = (250, 250, 250, 255), (205, 205, 210, 255)
HALO, HALO_EDGE, HALO_GLOW = (255, 214, 72, 255), (176, 124, 20, 255), (255, 226, 120, 90)
FEATHER, FEATHER_SHADE, FEATHER_LINE = (232, 240, 252, 255), (184, 204, 232, 255), (64, 76, 104, 255)


def head_alpha(d):
    a = Image.open(PARTS / f"Female_Average_Normal_{d}.png").convert("RGBA").getchannel("A")
    return a.resize((128 * S, 128 * S), Image.LANCZOS).point(lambda v: 255 if v > 100 else 0)


def P(pts):
    return [(x * S, y * S) for x, y in pts]


def finish(region, strands, extra=None):
    """Outline + fill + strand lines, shrunk to 128."""
    region = region.filter(ImageFilter.GaussianBlur(1.2)).point(lambda v: 255 if v > 127 else 0)
    outline = region.filter(ImageFilter.MaxFilter(2 * 2 * S + 1))
    img = Image.new("RGBA", region.size, (0, 0, 0, 0))
    img.paste(INK, mask=outline)
    img.paste(HAIR, mask=region)
    d = ImageDraw.Draw(img)
    for a, b in strands:
        d.line(P([a, b]), fill=HAIR_SHADE, width=int(0.9 * S))
    if extra:
        extra(d)
    return img.resize((128, 128), Image.LANCZOS)


def top_band(y):
    m = Image.new("L", (128 * S, 128 * S), 0)
    ImageDraw.Draw(m).rectangle(P([(0, 0), (128, y)]), fill=255)
    return m


def curl_only(m):
    """The parts of the side mask below the head (the curl), which the head mask doesn't reach."""
    return ImageChops.multiply(m, Image.eval(top_band(76), lambda v: 255 - v))


def hana_hair(d):
    """A chin-length bob: blunt bangs, the sides falling to the chin and curling inward at the ends."""
    head = head_alpha(d)
    cap = head.filter(ImageFilter.MaxFilter(2 * 3 * S + 1))
    m = Image.new("L", cap.size, 0); md = ImageDraw.Draw(m)
    face = Image.new("L", cap.size, 0); fd = ImageDraw.Draw(face)
    if d == "south":
        for sx in (-1, 1):                                                      # side panels to the chin
            x_out, x_in = 64 + sx * 26.5, 64 + sx * 17.5
            md.polygon(P([(64 + sx * 18, 50), (x_out, 54), (x_out + sx * 0.5, 80), (x_in, 80), (x_in, 60)]), fill=255)
            # the curl: a round end turning in under the cheek
            cx = 64 + sx * 20.5
            md.ellipse(P([(cx - 7.5, 75), (cx + 7.5, 89)]), fill=255)
        fd.rectangle(P([(64 - 17.5, 62), (64 + 17.5, 128)]), fill=255)         # her face stays clear
        cap = ImageChops.subtract(ImageChops.lighter(ImageChops.multiply(cap, top_band(62)), m), face)
        strands = [((x, 46), (x, 60)) for x in (52, 58, 64, 70, 76)] + [((64 + s * 22, 60), (64 + s * 22, 78)) for s in (-1, 1)]
        def curls(dr):
            for sx in (-1, 1):
                cx = 64 + sx * 20.5
                # the curl's spiral, turning in toward her chin
                dr.arc(P([(cx - 5, 78), (cx + 5, 87)]), 90 if sx < 0 else -90, 300 if sx < 0 else 120, fill=INK, width=int(1.1 * S))
                dr.arc(P([(cx - 2.2, 80.5), (cx + 2.2, 84.5)]), 0, 270, fill=HAIR_SHADE, width=int(0.9 * S))
        return finish(cap, strands, curls)
    if d == "east":
        md.polygon(P([(40, 50), (36, 60), (38, 80), (70, 80), (70, 60), (58, 50)]), fill=255)   # back and side, to the chin
        md.ellipse(P([(58, 74), (74, 89)]), fill=255)                                          # the curl, turning toward her face
        fd.polygon(P([(71, 62), (128, 62), (128, 128), (71, 128)]), fill=255)
        side_m = ImageChops.multiply(head.filter(ImageFilter.MaxFilter(2 * 3 * S + 1)), m)
        cap = ImageChops.subtract(ImageChops.lighter(ImageChops.multiply(cap, top_band(62)), ImageChops.lighter(side_m, curl_only(m))), face)
        strands = [((x, 46), (x + 3, 60)) for x in (56, 64, 72)] + [((48, 58), (50, 78)), ((58, 60), (60, 78))]
        def curls(dr):
            dr.arc(P([(61, 78), (71, 87)]), -90, 120, fill=INK, width=int(1.1 * S))
            dr.arc(P([(64, 80.5), (68.5, 84.5)]), 0, 270, fill=HAIR_SHADE, width=int(0.9 * S))
        return finish(cap, strands, curls)
    # north: the back of the bob, both ends curled in
    md.rectangle(P([(0, 0), (128, 80)]), fill=255)
    for sx in (-1, 1):
        cx = 64 + sx * 20
        md.ellipse(P([(cx - 8, 74), (cx + 8, 89)]), fill=255)
    md.rectangle(P([(64 - 22, 70), (64 + 22, 82)]), fill=255)
    wide = head.filter(ImageFilter.MaxFilter(2 * 4 * S + 1))
    side = Image.new("L", cap.size, 0); ImageDraw.Draw(side).rectangle(P([(64 - 26, 50), (64 + 26, 82)]), fill=255)
    cap = ImageChops.lighter(ImageChops.multiply(wide, m), ImageChops.multiply(side, m))
    strands = [((x, 48), (x, 76)) for x in (50, 57, 64, 71, 78)]
    def curls(dr):
        for sx in (-1, 1):
            cx = 64 + sx * 20
            dr.arc(P([(cx - 5, 78), (cx + 5, 87)]), 0, 200, fill=INK, width=int(1.1 * S))
    return finish(cap, strands, curls)


def halo(d):
    """A yellow ring floating above her head, seen a little from above; on the head's 128 frame."""
    im = Image.new("RGBA", (128 * S, 128 * S), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    cx = 64 + (3 if d == "east" else 0)
    box = [(cx - 17, 21), (cx + 17, 30)]
    glow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse(P([(box[0][0] - 3, box[0][1] - 2), (box[1][0] + 3, box[1][1] + 2)]), outline=HALO_GLOW, width=int(4 * S))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(2 * S)))
    dr.ellipse(P(box), outline=HALO_EDGE, width=int(3.4 * S))
    dr.ellipse(P([(box[0][0] + 0.6, box[0][1] + 0.6), (box[1][0] - 0.6, box[1][1] - 0.6)]), outline=HALO, width=int(2.2 * S))
    return im.resize((128, 128), Image.LANCZOS)


def bez(p0, p1, p2, p3, n=14):
    out = []
    for k in range(n + 1):
        t = k / n
        a, b, c_, d_ = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        out.append((a * p0[0] + b * p1[0] + c_ * p2[0] + d_ * p3[0], a * p0[1] + b * p1[1] + c_ * p2[1] + d_ * p3[1]))
    return out


# one wing in its own units (her call 2026-11-12: smaller, less pointy, feathery): root at (0, 0), spreading out to +x
# and up (-y). Built from rounded feathers: long flight feathers fanning out from the wrist, a row of shorter ones under
# the arm, and small coverts over their bases, every feather a capsule with a round end and its own outline.
WRIST = (40, -30)


def feather(dr, T_, base, ang, length, width, k):
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    tip = (base[0] + ux * length, base[1] + uy * length)
    pts = []
    for t in [i / 10 for i in range(11)]:                      # one side, widening then narrowing a little
        w = width * (0.55 + 0.45 * math.sin(t * math.pi * 0.85))
        pts.append((base[0] + ux * length * t + px * w, base[1] + uy * length * t + py * w))
    for a in [i / 8 * math.pi for i in range(9)]:               # the round end
        pts.append((tip[0] + px * width * 0.55 * math.cos(a) + ux * width * 0.55 * math.sin(a),
                    tip[1] + py * width * 0.55 * math.cos(a) + uy * width * 0.55 * math.sin(a)))
    for t in [i / 10 for i in range(10, -1, -1)]:
        w = width * (0.55 + 0.45 * math.sin(t * math.pi * 0.85))
        pts.append((base[0] + ux * length * t - px * w, base[1] + uy * length * t - py * w))
    poly = [T_(*q) for q in pts]
    dr.polygon(poly, fill=FEATHER, outline=FEATHER_LINE, width=max(2, int(1.3 * k)))
    a, b = T_(*base), T_(base[0] + ux * length * 0.85, base[1] + uy * length * 0.85)
    dr.line([a, b], fill=FEATHER_SHADE, width=max(2, int(1.0 * k)))     # the shaft


def wing(dr, root, sx, scale, k=2):
    rx, ry = root
    T_ = lambda x, y: ((rx + sx * x * scale) * k, (ry + y * scale) * k)
    A = lambda deg: math.radians(deg) if sx > 0 else math.radians(180 - deg)
    # in local units the wing points to +x; T_ mirrors x for the left wing, so angles stay in local terms
    T2 = lambda x, y: T_(x, y)
    def fe(base, deg, length, width):
        feather(dr, T2, base, math.radians(deg), length, width, k * scale)
    # flight feathers fanning from the wrist, drawn bottom first so the upper ones lie over them
    for n, (deg, L) in enumerate([(70, 34), (55, 40), (40, 44), (25, 46), (10, 44), (-5, 38), (-20, 30)]):
        t = n / 6
        base = (WRIST[0] * (0.55 + 0.45 * t), WRIST[1] * (0.55 + 0.45 * t))
        fe(base, deg, L, 6.5)
    # the shorter feathers under the arm
    for n in range(5):
        t = n / 4
        base = (WRIST[0] * t * 0.75, WRIST[1] * t * 0.75)
        fe(base, 92 - 14 * t, 30 - 6 * t, 6.5)
    # coverts: small rounded feathers over the bases, in two rows
    for row, (off, L, W) in enumerate([(6, 15, 5.5), (0, 11, 5.0)]):
        for n in range(6):
            t = (n + 0.5) / 6
            base = (WRIST[0] * t - off * 0.3, WRIST[1] * t - off)
            fe(base, 75 - 30 * t, L, W)
    # the arm itself: a rounded ridge along the top from the root to the wrist
    ridge = [(WRIST[0] * t, WRIST[1] * t - 6 * math.sin(t * math.pi)) for t in [i / 10 for i in range(11)]]
    dr.line([T_(*q) for q in ridge], fill=FEATHER, width=int(9 * scale * k))
    dr.line([T_(x, y - 4.2) for x, y in ridge], fill=FEATHER_LINE, width=max(2, int(1.3 * k)))


def wings(d):
    """On a 256 frame centred on her body (the body's own 128 frame sits 1:1 in its middle)."""
    N = 256
    im = Image.new("RGBA", (N * 2, N * 2), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    c = N / 2
    if d in ("south", "north"):
        for sx in (-1, 1):
            wing(dr, (c + sx * 6, c - 2), sx, 0.74)   # smaller (her calls 2026-11-12)
    else:  # east: both wings on her back (the picture's left), the far one higher and behind
        wing(dr, (c - 4, c - 5), -1, 0.66)
        near = Image.new("RGBA", im.size, (0, 0, 0, 0))
        wing(ImageDraw.Draw(near), (c - 7, c), -1, 0.74)
        im.alpha_composite(near)
    # a dark outline all round, like the pawns'
    edge = im.getchannel("A").point(lambda v: 255 if v > 60 else 0).filter(ImageFilter.MaxFilter(7))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.paste(FEATHER_LINE, mask=edge)
    out.alpha_composite(im)
    return out.resize((N, N), Image.LANCZOS)


def main():
    hairs = T / "Things/Pawn/Humanlike/Hairs"
    angel = T / "Things/Jujutsu/Angel"
    angel.mkdir(parents=True, exist_ok=True)
    out = {}
    for d in DIRS:
        out[d] = (hana_hair(d), halo(d), wings(d))
        out[d][0].save(hairs / f"JJK_HanaHair_{d}.png")
        out[d][1].save(angel / f"Halo_{d}.png")
        out[d][2].save(angel / f"Wings_{d}.png")
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (256 * 3, 256), (80, 80, 92, 255))
        for i, d in enumerate(DIRS):
            hair, ha, wi = out[d]
            cell = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
            if d != "north":
                cell.alpha_composite(wi)
            body = Image.open(PARTS / "Naked_Female_south.png").convert("RGBA") if (PARTS / "Naked_Female_south.png").exists() else None
            if body is not None:
                bname = {"south": "Naked_Female_south", "east": "Naked_Female_east", "north": "Naked_Female_north"}[d]
                cell.alpha_composite(Image.open(PARTS / f"{bname}.png").convert("RGBA"), (64, 64))
            if d == "north":
                cell.alpha_composite(wi)
            head = Image.open(PARTS / f"Female_Average_Normal_{d}.png").convert("RGBA")
            cell.alpha_composite(head, (64, 64 - 26))
            cell.alpha_composite(hair, (64, 64 - 26))
            cell.alpha_composite(ha, (64, 64 - 26))
            sheet.alpha_composite(cell, (256 * i, 0))
        sheet.resize((256 * 3 * 2, 512), Image.NEAREST).save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
