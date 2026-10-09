# Iranian rug textures for Vanilla Furniture Expanded - Art's rugs, version 2.
#
# Each rug size gets several designs (RimWorld's Graphic_Random picks one per rug, and keeps it).
# Shader CutoutComplex: every texture has a mask (<name>_m.png). Red in the mask is tinted with the
# colour of the cloth or leather the rug is made from, black keeps the texture's own colour
# (RimWorld Wiki, Modding Tutorials/Textures). So the FIELD - the big middle of the rug - is drawn
# in light greys under red and comes out in the material's colour, with woven shading; the borders,
# medallions and motifs are drawn in fixed traditional colours (ivory, navy, gold, madder red,
# teal) under black and look the same whatever the rug is made of.
#   python3 make_rugs2.py <Textures/IranianRugs>
import math, os, random, sys
from PIL import Image, ImageDraw

SS = 4                                           # drawn at 4x, scaled down: crisp at game size
PAL = {
    "navy":   {"border": (31, 42, 74), "guard": (232, 220, 192), "motif": (201, 162, 74), "accent": (142, 42, 42), "line": (40, 26, 20)},
    "ivory":  {"border": (230, 218, 190), "guard": (31, 42, 74), "motif": (142, 42, 42), "accent": (46, 107, 107), "line": (52, 36, 26)},
    "madder": {"border": (128, 34, 34), "guard": (232, 220, 192), "motif": (31, 42, 74), "accent": (201, 162, 74), "line": (40, 22, 18)},
    "teal":   {"border": (38, 92, 96), "guard": (236, 224, 196), "motif": (160, 70, 40), "accent": (201, 162, 74), "line": (28, 34, 34)},
}

class Rug:
    """Two layers drawn together: the colour texture and its mask."""
    def __init__(self, size):
        self.W, self.H = size[0] * SS, size[1] * SS
        self.img = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))
        self.msk = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))
        self.d, self.m = ImageDraw.Draw(self.img), ImageDraw.Draw(self.msk)

    def fixed(self, kind, xy, *args, **kw):
        *angles, color = args
        getattr(self.d, kind)(xy, *angles, fill=color + (255,), **kw)
        getattr(self.m, kind)(xy, *angles, fill=(0, 0, 0, 255), **kw)

    def tint(self, kind, xy, *args, **kw):
        *angles, grey = args
        g = int(grey * 255)
        getattr(self.d, kind)(xy, *angles, fill=(g, g, g, 255), **kw)
        getattr(self.m, kind)(xy, *angles, fill=(255, 0, 0, 255), **kw)

    def save(self, path):
        for im, p in ((self.img, path), (self.msk, path.replace(".png", "_m.png"))):
            im.resize((self.W // SS, self.H // SS), Image.LANCZOS).save(p)


def lobed(cx, cy, rx, ry, lobes, depth, steps=240):
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        c, s = math.cos(t), math.sin(t)
        r = 1 / ((abs(c) ** 1.35 + abs(s) ** 1.35) ** (1 / 1.35))
        r *= 1 + depth * math.cos(lobes * t)
        pts.append((cx + rx * r * c, cy + ry * r * s))
    return pts

def rosette(R, cx, cy, r, petal, core, dark):
    R.fixed("ellipse", [cx - r, cy - r, cx + r, cy + r], dark)
    for k in range(8):
        a = k * math.pi / 4
        px, py, rr = cx + math.cos(a) * r * 0.6, cy + math.sin(a) * r * 0.6, r * 0.33
        R.fixed("ellipse", [px - rr, py - rr, px + rr, py + rr], petal)
    R.fixed("ellipse", [cx - r * 0.3, cy - r * 0.3, cx + r * 0.3, cy + r * 0.3], core)

def boteh(R, cx, cy, s, color, inner, flip=False):
    """A paisley: a teardrop with a curled tip."""
    pts = []
    for i in range(60):
        t = math.pi * 2 * i / 60
        x = math.cos(t) * s * 0.55
        y = math.sin(t) * s * 0.75 + (s * 0.35 if math.sin(t) < 0 else 0) * -math.sin(t) * 0.0
        k = 1 + 0.6 * max(0, -math.sin(t))                  # pointed top
        pts.append((cx + (x * (1 - 0.35 * max(0, -math.sin(t))) + (0.35 * s * max(0, -math.sin(t)) ** 2) * (-1 if flip else 1)), cy + y * k))
    R.fixed("polygon", pts, color)
    R.fixed("ellipse", [cx - s * 0.22, cy - s * 0.05, cx + s * 0.22, cy + s * 0.4], inner)

def fringe_and_body(R, bbox, P):
    """Fringed ends, the woven body and its borders. Returns the field box."""
    x0, y0, x1, y1 = [v * SS for v in bbox]
    w, h = x1 - x0, y1 - y0
    m = min(w, h)
    fr = max(3 * SS, int(0.045 * h))
    step = max(3 * SS, int(m / 28))
    for x in range(int(x0 + step / 2), int(x1), step):
        for (ya, yb) in [(y0, y0 + fr), (y1 - fr, y1)]:
            R.fixed("line", [(x, ya + SS), (x, yb)], (236, 228, 208), width=max(SS, step // 3))
    by0, by1 = y0 + fr, y1 - fr
    ol = max(2 * SS, int(m * 0.012))
    R.fixed("rectangle", [x0, by0, x1, by1], P["line"])
    ix0, iy0, ix1, iy1 = x0 + ol, by0 + ol, x1 - ol, by1 - ol
    bw = int(min(ix1 - ix0, iy1 - iy0) * 0.15)
    gd = max(SS, int(bw * 0.14))
    R.fixed("rectangle", [ix0, iy0, ix1, iy1], P["guard"])
    R.fixed("rectangle", [ix0 + gd, iy0 + gd, ix1 - gd, iy1 - gd], P["border"])
    # rosettes and vine dots round the main border
    rr = (bw - 2 * gd) * 0.32
    cxs = lambda a, b, n: [a + (b - a) * (i + 0.5) / n for i in range(n)]
    nx = max(2, int((ix1 - ix0) / (bw * 1.25))); ny = max(2, int((iy1 - iy0) / (bw * 1.25)))
    mid = bw / 2
    for cx in cxs(ix0 + bw, ix1 - bw, nx):
        for cy in (iy0 + mid, iy1 - mid):
            rosette(R, cx, cy, rr, P["motif"], P["guard"], P["accent"])
    for cy in cxs(iy0 + bw, iy1 - bw, ny):
        for cx in (ix0 + mid, ix1 - mid):
            rosette(R, cx, cy, rr, P["motif"], P["guard"], P["accent"])
    for cx, cy in [(ix0 + mid, iy0 + mid), (ix1 - mid, iy0 + mid), (ix0 + mid, iy1 - mid), (ix1 - mid, iy1 - mid)]:
        rosette(R, cx, cy, rr * 1.15, P["guard"], P["accent"], P["motif"])
    fb = [ix0 + bw - gd, iy0 + bw - gd, ix1 - bw + gd, iy1 - bw + gd]
    R.fixed("rectangle", fb, P["guard"])
    return [fb[0] + gd, fb[1] + gd, fb[2] - gd, fb[3] - gd]

def field(R, f, seed):
    """The tinted field: light greys with abrash - the gentle banding of hand-dyed wool."""
    x0, y0, x1, y1 = f
    R.tint("rectangle", f, 0.92)
    rnd = random.Random(seed)
    y = y0
    while y < y1:
        hgt = (y1 - y0) * rnd.uniform(0.04, 0.12)
        R.tint("rectangle", [x0, y, x1, min(y1, y + hgt)], rnd.choice([0.86, 0.9, 0.95, 0.98]))
        y += hgt

def herati(R, f, P, cell, dense=False):
    """An all-over lattice of small diamonds and flowers - in darker field tone plus fixed florets."""
    x0, y0, x1, y1 = f
    nx = max(2, int((x1 - x0) / cell)); ny = max(2, int((y1 - y0) / cell))
    cw, ch = (x1 - x0) / nx, (y1 - y0) / ny
    for i in range(nx):
        for j in range(ny):
            cx, cy = x0 + (i + 0.5) * cw, y0 + (j + 0.5) * ch
            R.tint("polygon", [(cx, cy - ch * 0.42), (cx + cw * 0.42, cy), (cx, cy + ch * 0.42), (cx - cw * 0.42, cy)], 0.7)
            R.tint("polygon", [(cx, cy - ch * 0.3), (cx + cw * 0.3, cy), (cx, cy + ch * 0.3), (cx - cw * 0.3, cy)], 0.94)
            r = min(cw, ch) * (0.14 if dense else 0.11)
            R.fixed("ellipse", [cx - r, cy - r, cx + r, cy + r], P["motif"] if (i + j) % 2 else P["accent"])
            if dense:
                for dx, dy in ((-0.5, -0.5), (0.5, -0.5), (-0.5, 0.5), (0.5, 0.5)):
                    q = r * 0.55
                    R.fixed("ellipse", [cx + dx * cw - q, cy + dy * ch - q, cx + dx * cw + q, cy + dy * ch + q], P["border"])

def medallion(R, f, P, corners=True):
    x0, y0, x1, y1 = f
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, h = x1 - x0, y1 - y0
    R.fixed("polygon", lobed(cx, cy, w * 0.36, h * 0.34, 12, 0.07), P["line"])
    R.fixed("polygon", lobed(cx, cy, w * 0.34, h * 0.32, 12, 0.07), P["border"])
    R.fixed("polygon", lobed(cx, cy, w * 0.24, h * 0.22, 8, 0.08), P["guard"])
    R.fixed("polygon", lobed(cx, cy, w * 0.15, h * 0.14, 8, 0.1), P["accent"])
    rosette(R, cx, cy, min(w, h) * 0.07, P["motif"], P["guard"], P["border"])
    # pendants
    for s in (-1, 1):
        py = cy + s * h * 0.42
        R.fixed("polygon", [(cx, py - h * 0.06), (cx + w * 0.07, py), (cx, py + h * 0.06), (cx - w * 0.07, py)], P["border"])
        R.fixed("ellipse", [cx - w * 0.025, py - w * 0.025, cx + w * 0.025, py + w * 0.025], P["motif"])
    if corners:
        # quarter-medallions in the corners, pointing into the field (pillow angles run clockwise from 3 o'clock)
        for (qx, qy, a0) in ((x0, y0, 0), (x1, y0, 90), (x1, y1, 180), (x0, y1, 270)):
            R.fixed("pieslice", [qx - w * 0.22, qy - h * 0.2, qx + w * 0.22, qy + h * 0.2], a0, a0 + 90, P["border"])
            R.fixed("pieslice", [qx - w * 0.14, qy - h * 0.13, qx + w * 0.14, qy + h * 0.13], a0, a0 + 90, P["guard"])
            R.fixed("pieslice", [qx - w * 0.07, qy - h * 0.065, qx + w * 0.07, qy + h * 0.065], a0, a0 + 90, P["accent"])

def prayer(R, f, P):
    """A mihrab: an arch at the top with a tree of life under it."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    ay = y0 + h * 0.28
    # spandrels (fixed colour) above the arch
    R.fixed("rectangle", [x0, y0, x1, ay], P["border"])
    R.tint("pieslice", [x0 + w * 0.08, y0 + h * 0.05, x1 - w * 0.08, ay + h * 0.25], 180, 360, 0.92)
    R.tint("rectangle", [x0 + w * 0.08, ay + h * 0.12 - 1, x1 - w * 0.08, y1], 0.92)
    for k in range(4):
        rosette(R, x0 + w * (0.06 + 0.88 * (k % 2)), y0 + h * (0.06 + 0.12 * (k // 2)), min(w, h) * 0.035, P["motif"], P["guard"], P["accent"])
    # tree of life
    cx = (x0 + x1) / 2
    R.fixed("rectangle", [cx - w * 0.015, ay + h * 0.15, cx + w * 0.015, y1 - h * 0.05], P["line"])
    for i in range(6):
        yy = ay + h * (0.2 + i * 0.09)
        span = w * (0.12 + 0.035 * i)
        for s in (-1, 1):
            R.fixed("line", [(cx, yy + h * 0.03), (cx + s * span, yy)], P["line"], width=max(SS, int(w * 0.012)))
            r = min(w, h) * 0.03
            R.fixed("ellipse", [cx + s * span - r, yy - r, cx + s * span + r, yy + r], P["motif"] if i % 2 else P["accent"])
    R.fixed("polygon", [(cx - w * 0.12, y1 - h * 0.02), (cx + w * 0.12, y1 - h * 0.02), (cx + w * 0.07, y1 - h * 0.1), (cx - w * 0.07, y1 - h * 0.1)], P["border"])

def garden(R, f, P):
    """Kheshti: a grid of panels, alternately field colour and fixed colour, each with a flower."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    nx = 3 if w < h else 4
    ny = max(3, round(nx * h / w))
    cw, ch = w / nx, h / ny
    for i in range(nx):
        for j in range(ny):
            px, py = x0 + i * cw, y0 + j * ch
            box = [px + cw * 0.06, py + ch * 0.06, px + cw * 0.94, py + ch * 0.94]
            R.fixed("rectangle", [px, py, px + cw, py + ch], P["guard"])
            if (i + j) % 2: R.fixed("rectangle", box, P["border"])
            else: R.tint("rectangle", box, 0.9)
            cx, cy = px + cw / 2, py + ch / 2
            rosette(R, cx, cy, min(cw, ch) * 0.24, P["motif"] if (i + j) % 2 else P["accent"], P["guard"], P["line"])

def botehs(R, f, P):
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    s = min(w, h) / 6
    rows = max(2, int(h / (s * 1.7))); cols = max(2, int(w / (s * 1.3)))
    for j in range(rows):
        for i in range(cols):
            cx = x0 + (i + 0.5 + (0.5 if j % 2 else 0)) * w / cols
            if cx > x1 - s * 0.4: continue
            cy = y0 + (j + 0.5) * h / rows
            boteh(R, cx, cy, s * 0.55, P["motif"] if (i + j) % 2 else P["accent"], P["guard"], flip=bool(j % 2))

def vase(R, f, P):
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    herati(R, f, P, min(w, h) / 3.2)
    cx = (x0 + x1) / 2
    for k, yy in enumerate((y0 + h * 0.3, y0 + h * 0.75)):
        # a vase: round body, narrow neck, flared lip, a foot
        bw_, bh_ = w * 0.13, h * 0.07
        R.fixed("ellipse", [cx - bw_, yy - bh_ * 0.2, cx + bw_, yy + bh_ * 1.6], P["line"])
        R.fixed("ellipse", [cx - bw_ * 0.9, yy - bh_ * 0.1, cx + bw_ * 0.9, yy + bh_ * 1.5], P["border"])
        R.fixed("rectangle", [cx - bw_ * 0.28, yy - bh_ * 0.9, cx + bw_ * 0.28, yy], P["border"])
        R.fixed("rectangle", [cx - bw_ * 0.5, yy - bh_ * 1.05, cx + bw_ * 0.5, yy - bh_ * 0.8], P["motif"])
        R.fixed("rectangle", [cx - bw_ * 0.45, yy + bh_ * 1.45, cx + bw_ * 0.45, yy + bh_ * 1.75], P["motif"])
        R.fixed("ellipse", [cx - bw_ * 0.35, yy + bh_ * 0.35, cx + bw_ * 0.35, yy + bh_ * 1.0], P["guard"])
        for a in (-0.7, -0.35, 0, 0.35, 0.7):
            ex, ey = cx + a * w * 0.38, yy - h * (0.16 - abs(a) * 0.08)
            R.fixed("line", [(cx, yy - h * 0.07), (ex, ey)], P["line"], width=max(SS, int(w * 0.01)))
            rosette(R, ex, ey, min(w, h) * 0.045, P["motif"] if k else P["accent"], P["guard"], P["border"])

DESIGNS = {"medallion": lambda R, f, P: (herati(R, f, P, min(f[2] - f[0], f[3] - f[1]) / 4), medallion(R, f, P)),
           "herati": lambda R, f, P: herati(R, f, P, min(f[2] - f[0], f[3] - f[1]) / 5, dense=True),
           "prayer": prayer, "garden": garden, "boteh": botehs, "vase": vase}

def make(size, bbox, design, pal, path, seed):
    R = Rug(size)
    f = fringe_and_body(R, bbox, PAL[pal])
    field(R, f, seed)
    DESIGNS[design](R, f, PAL[pal])
    R.save(path)

if __name__ == "__main__":
    out = sys.argv[1]
    specs = {"Small":  ((256, 256), (67, 54, 189, 202), [("medallion", "navy"), ("boteh", "madder"), ("garden", "ivory"), ("herati", "teal")]),
             "Medium": ((256, 256), (2, 2, 254, 254), [("medallion", "navy"), ("garden", "madder"), ("herati", "ivory"), ("boteh", "teal"), ("vase", "navy"), ("medallion", "madder")]),
             "Large":  ((384, 384), (66, 3, 319, 381), [("medallion", "navy"), ("prayer", "madder"), ("garden", "teal"), ("herati", "ivory"), ("boteh", "navy"), ("vase", "madder"), ("medallion", "teal")])}
    for size_name, (size, bbox, combos) in specs.items():
        d = os.path.join(out, size_name)
        os.makedirs(d, exist_ok=True)
        for f in os.listdir(d): os.remove(os.path.join(d, f))
        for k, (design, pal) in enumerate(combos):
            make(size, bbox, design, pal, os.path.join(d, f"Iranian{size_name}_{chr(65 + k)}.png"), seed=k * 7 + len(size_name))
        print(size_name, len(combos), "designs")
