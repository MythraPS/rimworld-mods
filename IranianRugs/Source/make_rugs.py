# Iranian (Lechak-Toranj: medallion and corners) rug textures for Vanilla Furniture Expanded - Art's
# rugs, in greys only: RimWorld multiplies the texture by the colour of the cloth or leather the rug
# is made from, so each rug keeps the colour the original would have had. Same canvas size and the
# same rug outline as the original textures, so they fill the same tiles.
import math, sys
from PIL import Image, ImageDraw, ImageFilter

SS = 4                                    # drawn at 4x, scaled down: crisp edges at game size
def g(v, a=255): return (int(v * 255),) * 3 + (a,)
def T(v): return 0.30 + 0.70 * v          # lift every tone: the originals are mostly near-white

def lobed(cx, cy, rx, ry, lobes, depth, steps=360):
    """A diamond-ish medallion with a scalloped edge."""
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        # a lozenge (L1 norm) softened toward an ellipse, with lobes along the edge
        c, s = math.cos(t), math.sin(t)
        r = 1 / ((abs(c) ** 1.35 + abs(s) ** 1.35) ** (1 / 1.35))
        r *= 1 + depth * math.cos(lobes * t)
        pts.append((cx + rx * r * c, cy + ry * r * s))
    return pts

def rosette(d, cx, cy, r, light, dark):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=g(dark))
    for k in range(8):
        a = k * math.pi / 4
        px, py = cx + math.cos(a) * r * 0.62, cy + math.sin(a) * r * 0.62
        rr = r * 0.32
        d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=g(light))
    d.ellipse([cx - r * 0.3, cy - r * 0.3, cx + r * 0.3, cy + r * 0.3], fill=g(light))

def rug(size, bbox, simple=False):
    W, H = size[0] * SS, size[1] * SS
    x0, y0, x1, y1 = [v * SS for v in bbox]
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    w, h = x1 - x0, y1 - y0
    m = min(w, h)
    fr = max(3 * SS, int(0.045 * h))                    # fringe on the short ends
    bx0, by0, bx1, by1 = x0, y0 + fr, x1, y1 - fr       # the woven body
    # fringe: knotted tassels
    step = max(3 * SS, int(m / 28))
    for x in range(int(bx0 + step / 2), int(bx1), step):
        for (ya, yb) in [(y0, by0), (by1, y1)]:
            d.line([(x, ya + SS), (x, yb)], fill=g(T(0.93)), width=max(SS, step // 3))
            d.line([(x, ya), (x, ya + 1.5 * SS)], fill=g(T(0.55)), width=max(SS, step // 3))
    # body and its outline
    ol = max(2 * SS, int(m * 0.012))
    d.rectangle([bx0, by0, bx1, by1], fill=g(0.18))
    ix0, iy0, ix1, iy1 = bx0 + ol, by0 + ol, bx1 - ol, by1 - ol
    bw = int(min(ix1 - ix0, iy1 - iy0) * (0.16 if not simple else 0.14))
    # outer guard, main border, inner guard
    gd = max(SS, int(bw * 0.14))
    d.rectangle([ix0, iy0, ix1, iy1], fill=g(T(0.90)))
    d.rectangle([ix0 + gd, iy0 + gd, ix1 - gd, iy1 - gd], fill=g(T(0.30)))
    d.rectangle([ix0 + gd + SS, iy0 + gd + SS, ix1 - gd - SS, iy1 - gd - SS], fill=g(T(0.80)))
    bi0, bj0, bi1, bj1 = ix0 + bw, iy0 + bw, ix1 - bw, iy1 - bw         # field edge
    # rosettes all round the main border
    rr = (bw - 2 * gd) * 0.34
    mid = lambda a, b: (a + b) / 2
    for (ax, ay, bx_, by_) in [(ix0 + bw / 2, iy0 + bw / 2, ix1 - bw / 2, iy0 + bw / 2), (ix0 + bw / 2, iy1 - bw / 2, ix1 - bw / 2, iy1 - bw / 2),
                               (ix0 + bw / 2, iy0 + bw / 2, ix0 + bw / 2, iy1 - bw / 2), (ix1 - bw / 2, iy0 + bw / 2, ix1 - bw / 2, iy1 - bw / 2)]:
        length = math.hypot(bx_ - ax, by_ - ay)
        n = max(2, int(length / (rr * 3.2)))
        for k in range(n + 1):
            t = k / n
            cx, cy = ax + (bx_ - ax) * t, ay + (by_ - ay) * t
            rosette(d, cx, cy, rr, T(0.92), T(0.42))
            if k < n and not simple:   # a small diamond between rosettes
                hx, hy = ax + (bx_ - ax) * (t + 0.5 / n), ay + (by_ - ay) * (t + 0.5 / n)
                q = rr * 0.45
                d.polygon([(hx, hy - q), (hx + q, hy), (hx, hy + q), (hx - q, hy)], fill=g(T(0.55)))
    # inner guard and the field
    d.rectangle([bi0 - gd, bj0 - gd, bi1 + gd, bj1 + gd], fill=g(T(0.30)))
    d.rectangle([bi0 - gd + SS, bj0 - gd + SS, bi1 + gd - SS, bj1 + gd - SS], fill=g(T(0.90)))
    d.rectangle([bi0, bj0, bi1, bj1], fill=g(T(0.62)))
    fw, fh = bi1 - bi0, bj1 - bj0
    # an all-over scatter of small blossoms in the field
    sp = max(8 * SS, int(min(fw, fh) / (6 if not simple else 4)))
    for j, y in enumerate(range(int(bj0 + sp / 2), int(bj1), sp)):
        for x in range(int(bi0 + sp / 2 + (sp / 2 if j % 2 else 0)), int(bi1), sp):
            q = sp * 0.12
            d.polygon([(x, y - q), (x + q, y), (x, y + q), (x - q, y)], fill=g(T(0.50)))
            d.ellipse([x - q * 0.4, y - q * 0.4, x + q * 0.4, y + q * 0.4], fill=g(T(0.80)))
    # corner spandrels: quarter medallions, drawn whole on their own layer and trimmed to the field
    cr = min(fw, fh) * 0.30
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
    for (cx, cy) in [(bi0, bj0), (bi1, bj0), (bi0, bj1), (bi1, bj1)]:
        ld.polygon(lobed(cx, cy, cr, cr, 12, 0.07), fill=g(T(0.88)))
        ld.polygon(lobed(cx, cy, cr * 0.62, cr * 0.62, 12, 0.06), fill=g(T(0.50)))
    clip = Image.new("L", (W, H), 0); ImageDraw.Draw(clip).rectangle([bi0, bj0, bi1, bj1], fill=255)
    layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (W, H), 0), clip))
    im.alpha_composite(layer); d = ImageDraw.Draw(im)
    # the central medallion with pendants on the long axis
    cx, cy = (bi0 + bi1) / 2, (bj0 + bj1) / 2
    long_v = fh >= fw
    mrx, mry = (fw * 0.34, fh * 0.30) if long_v else (fw * 0.30, fh * 0.34)
    if simple: mrx, mry = mrx * 1.1, mry * 1.1
    for (s, tone) in [(1.0, 0.25), (0.97, 0.90), (0.78, 0.40), (0.62, 0.84), (0.40, 0.48)]:
        d.polygon(lobed(cx, cy, mrx * s, mry * s, 16, 0.05), fill=g(T(tone)))
    rosette(d, cx, cy, min(mrx, mry) * 0.22, T(0.95), T(0.30))
    if not simple:
        for sgn in (-1, 1):                               # pendants
            if long_v: px, py, q = cx, cy + sgn * mry * 1.12, min(mrx, mry) * 0.22
            else: px, py, q = cx + sgn * mrx * 1.12, cy, min(mrx, mry) * 0.22
            d.polygon(lobed(px, py, q, q * 1.2 if long_v else q, 8, 0.08), fill=g(T(0.86)))
            d.ellipse([px - q * 0.4, py - q * 0.4, px + q * 0.4, py + q * 0.4], fill=g(T(0.42)))
    out = im.resize(size, Image.LANCZOS)
    return out

if __name__ == "__main__":
    out = sys.argv[1]
    specs = {"CarpetSmall": ((256, 256), (67, 54, 189, 202), True),
             "CarpetMedium": ((256, 256), (2, 2, 254, 254), False),
             "CarpetLarge": ((384, 384), (66, 0, 319, 384), False)}
    for name, (size, bb, simple) in specs.items():
        rug(size, bb, simple).save(f"{out}/{name}.png")
        print("made", name)
