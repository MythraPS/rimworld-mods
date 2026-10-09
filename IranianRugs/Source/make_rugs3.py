# Iranian rug textures for Vanilla Furniture Expanded - Art's rugs, version 3.
#
# Each rug size gets several designs (RimWorld's Graphic_Random picks one per rug, and keeps it).
# Shader CutoutComplex: every texture has a mask (<name>_m.png). Red in the mask is tinted with the
# colour of the cloth or leather the rug is made from, black keeps the texture's own colour.
#
# v3: the material's colour runs through the patterns, not just the field. Every colour role in a
# scheme (border, guard stripes, motifs, accents, outlines) is either a FIXED colour (an RGB tuple,
# mask black) or a SHADE of the material (a float grey, mask red: 0.3 = dark material, 0.97 = pale).
# Schemes mix the two, so the material shows as dark borders, pale guards, tonal motifs and so on.
# Designs come from several Iranian weaving traditions: city medallion, Herati, prayer, Bakhtiari
# garden, boteh, vase, Heriz, Turkmen gul, Qashqai, Gabbeh, kilim, Baluch, afshan.
#   python3 make_rugs3.py <Textures/IranianRugs>
import math, os, random, sys
from PIL import Image, ImageDraw

SS = 4                                           # drawn at 4x, scaled down: crisp at game size
NAVY, IVORY, GOLD = (31, 42, 74), (232, 220, 192), (201, 162, 74)
MADDER, TEAL, DARK, RUST = (142, 42, 42), (46, 107, 107), (40, 26, 20), (160, 70, 40)

# field/fdeep are always material shades: the field ground and the tonal motifs drawn on it
SCHEMES = {
    "classic":   dict(field=.92, fdeep=.68, border=NAVY,   guard=IVORY, motif=.55,  accent=MADDER, line=DARK),
    "matborder": dict(field=.95, fdeep=.7,  border=.42,    guard=IVORY, motif=GOLD, accent=NAVY,   line=DARK),
    "matmotif":  dict(field=.9,  fdeep=.6,  border=NAVY,   guard=.97,   motif=.5,   accent=MADDER, line=DARK),
    "tonal":     dict(field=.85, fdeep=.55, border=.4,     guard=.97,   motif=.65,  accent=IVORY,  line=.22),
    "darkfield": dict(field=.5,  fdeep=.82, border=MADDER, guard=IVORY, motif=.95,  accent=GOLD,   line=DARK),
    "ivorymat":  dict(field=.9,  fdeep=.6,  border=IVORY,  guard=.45,   motif=.5,   accent=NAVY,   line=DARK),
    "tealmat":   dict(field=.93, fdeep=.66, border=TEAL,   guard=.96,   motif=.5,   accent=GOLD,   line=DARK),
    "madder":    dict(field=.92, fdeep=.68, border=MADDER, guard=IVORY, motif=.48,  accent=NAVY,   line=DARK),
    "deeptonal": dict(field=.62, fdeep=.92, border=.32,    guard=.85,   motif=.95,  accent=RUST,   line=.18),
}

class Rug:
    """Two layers drawn together: the colour texture and its mask."""
    def __init__(self, size):
        self.W, self.H = size[0] * SS, size[1] * SS
        self.img = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))
        self.msk = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))
        self.d, self.m = ImageDraw.Draw(self.img), ImageDraw.Draw(self.msk)

    def paint(self, kind, xy, *args, **kw):
        """Last positional arg is the colour: an RGB tuple (fixed) or a float (material shade)."""
        *angles, spec = args
        if kind in ("rectangle", "ellipse", "pieslice", "arc"):        # boxes drawn mirrored
            ax, ay, bx, by = xy
            xy = [min(ax, bx), min(ay, by), max(ax, bx), max(ay, by)]
        if isinstance(spec, float):
            g = int(max(0, min(1, spec)) * 255)
            col, mcol = (g, g, g, 255), (255, 0, 0, 255)
        else:
            col, mcol = tuple(spec) + (255,), (0, 0, 0, 255)
        getattr(self.d, kind)(xy, *angles, fill=col, **kw)
        getattr(self.m, kind)(xy, *angles, fill=mcol, **kw)

    def save(self, path):
        for im, p in ((self.img, path), (self.msk, path.replace(".png", "_m.png"))):
            im.resize((self.W // SS, self.H // SS), Image.LANCZOS).save(p)


# ---- shapes ----------------------------------------------------------------------------------
def lobed(cx, cy, rx, ry, lobes, depth, steps=240):
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        c, s = math.cos(t), math.sin(t)
        r = 1 / ((abs(c) ** 1.35 + abs(s) ** 1.35) ** (1 / 1.35))
        r *= 1 + depth * math.cos(lobes * t)
        pts.append((cx + rx * r * c, cy + ry * r * s))
    return pts

def stepped(cx, cy, rx, ry, n):
    """A diamond with staircase edges (the angular outline of village and tribal rugs)."""
    # staircase between successive diamond tips
    tips = [(0, -1), (1, 0), (0, 1), (-1, 0)]
    out = []
    for i in range(4):
        (ax, ay), (bx, by) = tips[i], tips[(i + 1) % 4]
        for k in range(n):
            x0, y0 = ax + (bx - ax) * k / n, ay + (by - ay) * k / n
            x1, y1 = ax + (bx - ax) * (k + 1) / n, ay + (by - ay) * (k + 1) / n
            out += [(x0, y0), (x1, y0) if i % 2 == 0 else (x0, y1)]
    return [(cx + x * rx, cy + y * ry) for x, y in out]

def octagon(cx, cy, rx, ry):
    return [(cx + rx * math.cos(math.pi / 8 + k * math.pi / 4), cy + ry * math.sin(math.pi / 8 + k * math.pi / 4)) for k in range(8)]

def star8(cx, cy, r):
    return [(cx + (r if k % 2 == 0 else r * 0.45) * math.cos(k * math.pi / 8), cy + (r if k % 2 == 0 else r * 0.45) * math.sin(k * math.pi / 8)) for k in range(16)]

def rosette(R, cx, cy, r, petal, core, dark):
    R.paint("ellipse", [cx - r, cy - r, cx + r, cy + r], dark)
    for k in range(8):
        a = k * math.pi / 4
        px, py, rr = cx + math.cos(a) * r * 0.6, cy + math.sin(a) * r * 0.6, r * 0.33
        R.paint("ellipse", [px - rr, py - rr, px + rr, py + rr], petal)
    R.paint("ellipse", [cx - r * 0.3, cy - r * 0.3, cx + r * 0.3, cy + r * 0.3], core)

def boteh(R, cx, cy, s, color, inner, flip=False):
    """A paisley: a teardrop with a curled tip."""
    pts = []
    for i in range(60):
        t = math.pi * 2 * i / 60
        x = math.cos(t) * s * 0.55
        y = math.sin(t) * s * 0.75
        k = 1 + 0.6 * max(0, -math.sin(t))                  # pointed top
        pts.append((cx + (x * (1 - 0.35 * max(0, -math.sin(t))) + (0.35 * s * max(0, -math.sin(t)) ** 2) * (-1 if flip else 1)), cy + y * k))
    R.paint("polygon", pts, color)
    R.paint("ellipse", [cx - s * 0.22, cy - s * 0.05, cx + s * 0.22, cy + s * 0.4], inner)

def tree(R, cx, base, h, P, trunk):
    """A village tree: a stack of triangles on a trunk."""
    R.paint("rectangle", [cx - h * 0.04, base - h * 0.3, cx + h * 0.04, base], trunk)
    for k in range(3):
        ty = base - h * (0.25 + 0.22 * k)
        wd = h * (0.32 - 0.07 * k)
        R.paint("polygon", [(cx - wd, ty), (cx + wd, ty), (cx, ty - h * 0.32)], P["fdeep"] if k % 2 else P["motif"])

def animal(R, x, y, s, col, flip=False):
    """A tiny goat or dog in the Qashqai manner: block body, stick legs, a head and a tail."""
    d = -1 if flip else 1
    R.paint("rectangle", [x - s * 0.5, y - s * 0.2, x + s * 0.5, y + s * 0.15], col)
    for lx in (-0.4, -0.15, 0.2, 0.42):
        R.paint("rectangle", [x + lx * s - s * 0.05, y + s * 0.15, x + lx * s + s * 0.05, y + s * 0.5], col)
    R.paint("rectangle", [x + d * s * 0.45, y - s * 0.5, x + d * s * 0.7, y - s * 0.15], col)
    R.paint("rectangle", [x - d * s * 0.6, y - s * 0.35, x - d * s * 0.5, y - s * 0.15], col)


# ---- body and borders ------------------------------------------------------------------------
def fringe_and_body(R, bbox, P, border="rosette"):
    """Fringed ends, the woven body and its borders. Returns the field box."""
    x0, y0, x1, y1 = [v * SS for v in bbox]
    w, h = x1 - x0, y1 - y0
    m = min(w, h)
    fr = max(3 * SS, int(0.045 * h))
    step = max(3 * SS, int(m / 28))
    for x in range(int(x0 + step / 2), int(x1), step):
        for (ya, yb) in [(y0, y0 + fr), (y1 - fr, y1)]:
            R.paint("line", [(x, ya + SS), (x, yb)], (236, 228, 208), width=max(SS, step // 3))
    by0, by1 = y0 + fr, y1 - fr
    ol = max(2 * SS, int(m * 0.012))
    R.paint("rectangle", [x0, by0, x1, by1], P["line"])
    ix0, iy0, ix1, iy1 = x0 + ol, by0 + ol, x1 - ol, by1 - ol
    thin = border == "thin"
    bw = int(min(ix1 - ix0, iy1 - iy0) * (0.07 if thin else 0.15))
    gd = max(SS, int(bw * (0.2 if thin else 0.14)))
    R.paint("rectangle", [ix0, iy0, ix1, iy1], P["guard"])
    R.paint("rectangle", [ix0 + gd, iy0 + gd, ix1 - gd, iy1 - gd], P["border"])
    mid = bw / 2
    cxs = lambda a, b, n: [a + (b - a) * (i + 0.5) / n for i in range(n)]
    nx = max(2, int((ix1 - ix0) / (bw * 1.25))); ny = max(2, int((iy1 - iy0) / (bw * 1.25)))
    spots = [(cx, cy) for cx in cxs(ix0 + bw, ix1 - bw, nx) for cy in (iy0 + mid, iy1 - mid)] + \
            [(cx, cy) for cy in cxs(iy0 + bw, iy1 - bw, ny) for cx in (ix0 + mid, ix1 - mid)]
    corners = [(ix0 + mid, iy0 + mid), (ix1 - mid, iy0 + mid), (ix0 + mid, iy1 - mid), (ix1 - mid, iy1 - mid)]
    rr = (bw - 2 * gd) * 0.32
    if border == "rosette":
        for cx, cy in spots: rosette(R, cx, cy, rr, P["motif"], P["guard"], P["accent"])
        for cx, cy in corners: rosette(R, cx, cy, rr * 1.15, P["guard"], P["accent"], P["motif"])
    elif border == "diamonds":
        for k, (cx, cy) in enumerate(spots + corners):
            q = (bw - 2 * gd) * 0.42
            R.paint("polygon", stepped(cx, cy, q, q, 2), P["motif"] if k % 2 else P["accent"])
            R.paint("rectangle", [cx - q * 0.2, cy - q * 0.2, cx + q * 0.2, cy + q * 0.2], P["guard"])
    elif border == "sawtooth":
        # reciprocal triangles along the inside of the border
        for (a, b, fixed, side) in ((ix0 + gd, ix1 - gd, iy0 + gd, "top"), (ix0 + gd, ix1 - gd, iy1 - gd, "bottom"),
                                    (iy0 + gd, iy1 - gd, ix0 + gd, "left"), (iy0 + gd, iy1 - gd, ix1 - gd, "right")):
            n = max(3, int((b - a) / (bw * 0.6)))
            t = (bw - 2 * gd) * 0.7
            for k in range(n):
                u0, u1 = a + (b - a) * k / n, a + (b - a) * (k + 1) / n
                um = (u0 + u1) / 2
                if side == "top":    pts = [(u0, fixed), (u1, fixed), (um, fixed + t)]
                elif side == "bottom": pts = [(u0, fixed), (u1, fixed), (um, fixed - t)]
                elif side == "left": pts = [(fixed, u0), (fixed, u1), (fixed + t, um)]
                else:                pts = [(fixed, u0), (fixed, u1), (fixed - t, um)]
                R.paint("polygon", pts, P["motif"] if k % 2 else P["accent"])
    elif thin:
        for k, (cx, cy) in enumerate(spots):
            q = (bw - 2 * gd) * 0.3
            R.paint("rectangle", [cx - q, cy - q, cx + q, cy + q], P["motif"] if k % 2 else P["accent"])
    fb = [ix0 + bw - gd, iy0 + bw - gd, ix1 - bw + gd, iy1 - bw + gd]
    R.paint("rectangle", fb, P["guard"])
    return [fb[0] + gd, fb[1] + gd, fb[2] - gd, fb[3] - gd]

def field(R, f, P, seed, strength=1.0):
    """The field in the material's colour, with abrash - the banding of hand-dyed wool."""
    x0, y0, x1, y1 = f
    base = P["field"]
    R.paint("rectangle", f, float(base))
    rnd = random.Random(seed)
    y = y0
    while y < y1:
        hgt = (y1 - y0) * rnd.uniform(0.04, 0.12)
        R.paint("rectangle", [x0, y, x1, min(y1, y + hgt)], float(base + strength * rnd.choice([-0.06, -0.03, 0.02, 0.05])))
        y += hgt


# ---- designs ---------------------------------------------------------------------------------
def herati(R, f, P, cell, dense=False):
    """An all-over lattice of small diamonds and flowers."""
    x0, y0, x1, y1 = f
    nx = max(2, int((x1 - x0) / cell)); ny = max(2, int((y1 - y0) / cell))
    cw, ch = (x1 - x0) / nx, (y1 - y0) / ny
    for i in range(nx):
        for j in range(ny):
            cx, cy = x0 + (i + 0.5) * cw, y0 + (j + 0.5) * ch
            R.paint("polygon", [(cx, cy - ch * 0.42), (cx + cw * 0.42, cy), (cx, cy + ch * 0.42), (cx - cw * 0.42, cy)], P["fdeep"])
            R.paint("polygon", [(cx, cy - ch * 0.3), (cx + cw * 0.3, cy), (cx, cy + ch * 0.3), (cx - cw * 0.3, cy)], P["field"])
            r = min(cw, ch) * (0.14 if dense else 0.11)
            R.paint("ellipse", [cx - r, cy - r, cx + r, cy + r], P["motif"] if (i + j) % 2 else P["accent"])
            if dense:
                for dx, dy in ((-0.5, -0.5), (0.5, -0.5), (-0.5, 0.5), (0.5, 0.5)):
                    q = r * 0.55
                    R.paint("ellipse", [cx + dx * cw - q, cy + dy * ch - q, cx + dx * cw + q, cy + dy * ch + q], P["border"])

def medallion(R, f, P, corners=True):
    """City medallion (Tabriz, Kashan): a lobed centre with pendants and quarter-medallion corners."""
    x0, y0, x1, y1 = f
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, h = x1 - x0, y1 - y0
    R.paint("polygon", lobed(cx, cy, w * 0.36, h * 0.34, 12, 0.07), P["line"])
    R.paint("polygon", lobed(cx, cy, w * 0.34, h * 0.32, 12, 0.07), P["border"])
    R.paint("polygon", lobed(cx, cy, w * 0.27, h * 0.25, 12, 0.07), P["fdeep"])
    R.paint("polygon", lobed(cx, cy, w * 0.21, h * 0.19, 8, 0.08), P["guard"])
    R.paint("polygon", lobed(cx, cy, w * 0.14, h * 0.13, 8, 0.1), P["accent"])
    rosette(R, cx, cy, min(w, h) * 0.07, P["motif"], P["guard"], P["border"])
    for s in (-1, 1):
        py = cy + s * h * 0.42
        R.paint("polygon", [(cx, py - h * 0.06), (cx + w * 0.07, py), (cx, py + h * 0.06), (cx - w * 0.07, py)], P["border"])
        R.paint("ellipse", [cx - w * 0.025, py - w * 0.025, cx + w * 0.025, py + w * 0.025], P["motif"])
    if corners:
        for (qx, qy, a0) in ((x0, y0, 0), (x1, y0, 90), (x1, y1, 180), (x0, y1, 270)):
            R.paint("pieslice", [qx - w * 0.22, qy - h * 0.2, qx + w * 0.22, qy + h * 0.2], a0, a0 + 90, P["border"])
            R.paint("pieslice", [qx - w * 0.15, qy - h * 0.135, qx + w * 0.15, qy + h * 0.135], a0, a0 + 90, P["fdeep"])
            R.paint("pieslice", [qx - w * 0.09, qy - h * 0.08, qx + w * 0.09, qy + h * 0.08], a0, a0 + 90, P["accent"])

def prayer(R, f, P):
    """A mihrab: an arch at the top with a tree of life under it."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    ay = y0 + h * 0.28
    R.paint("rectangle", [x0, y0, x1, ay], P["border"])
    R.paint("pieslice", [x0 + w * 0.08, y0 + h * 0.05, x1 - w * 0.08, ay + h * 0.25], 180, 360, P["field"])
    R.paint("rectangle", [x0 + w * 0.08, ay + h * 0.12 - 1, x1 - w * 0.08, y1], P["field"])
    for k in range(4):
        rosette(R, x0 + w * (0.06 + 0.88 * (k % 2)), y0 + h * (0.06 + 0.12 * (k // 2)), min(w, h) * 0.035, P["motif"], P["guard"], P["accent"])
    cx = (x0 + x1) / 2
    R.paint("rectangle", [cx - w * 0.015, ay + h * 0.15, cx + w * 0.015, y1 - h * 0.05], P["line"])
    for i in range(6):
        yy = ay + h * (0.2 + i * 0.09)
        span = w * (0.12 + 0.035 * i)
        for s in (-1, 1):
            R.paint("line", [(cx, yy + h * 0.03), (cx + s * span, yy)], P["fdeep"], width=max(SS, int(w * 0.014)))
            r = min(w, h) * 0.03
            R.paint("ellipse", [cx + s * span - r, yy - r, cx + s * span + r, yy + r], P["motif"] if i % 2 else P["accent"])
    R.paint("polygon", [(cx - w * 0.12, y1 - h * 0.02), (cx + w * 0.12, y1 - h * 0.02), (cx + w * 0.07, y1 - h * 0.1), (cx - w * 0.07, y1 - h * 0.1)], P["border"])

def garden(R, f, P):
    """Bakhtiari kheshti: a grid of garden panels, alternately pale and deep material, each with a flower."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    nx = 3 if w < h else 4
    ny = max(3, round(nx * h / w))
    cw, ch = w / nx, h / ny
    for i in range(nx):
        for j in range(ny):
            px, py = x0 + i * cw, y0 + j * ch
            box = [px + cw * 0.06, py + ch * 0.06, px + cw * 0.94, py + ch * 0.94]
            R.paint("rectangle", [px, py, px + cw, py + ch], P["guard"])
            R.paint("rectangle", box, P["fdeep"] if (i + j) % 2 else P["field"])
            if (i + j) % 3 == 0:
                tree(R, px + cw / 2, py + ch * 0.85, ch * 0.75, P, P["line"])
            else:
                rosette(R, px + cw / 2, py + ch / 2, min(cw, ch) * 0.24, P["motif"] if (i + j) % 2 else P["accent"], P["guard"], P["border"])

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
            col = (P["motif"], P["accent"], P["fdeep"])[(i + j) % 3]
            boteh(R, cx, cy, s * 0.55, col, P["guard"], flip=bool(j % 2))

def vase(R, f, P):
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    herati(R, f, P, min(w, h) / 3.2)
    cx = (x0 + x1) / 2
    for k, yy in enumerate((y0 + h * 0.3, y0 + h * 0.75)):
        bw_, bh_ = w * 0.13, h * 0.07
        R.paint("ellipse", [cx - bw_, yy - bh_ * 0.2, cx + bw_, yy + bh_ * 1.6], P["line"])
        R.paint("ellipse", [cx - bw_ * 0.9, yy - bh_ * 0.1, cx + bw_ * 0.9, yy + bh_ * 1.5], P["border"])
        R.paint("rectangle", [cx - bw_ * 0.28, yy - bh_ * 0.9, cx + bw_ * 0.28, yy], P["border"])
        R.paint("rectangle", [cx - bw_ * 0.5, yy - bh_ * 1.05, cx + bw_ * 0.5, yy - bh_ * 0.8], P["motif"])
        R.paint("rectangle", [cx - bw_ * 0.45, yy + bh_ * 1.45, cx + bw_ * 0.45, yy + bh_ * 1.75], P["motif"])
        R.paint("ellipse", [cx - bw_ * 0.35, yy + bh_ * 0.35, cx + bw_ * 0.35, yy + bh_ * 1.0], P["guard"])
        for a in (-0.7, -0.35, 0, 0.35, 0.7):
            ex, ey = cx + a * w * 0.38, yy - h * (0.16 - abs(a) * 0.08)
            R.paint("line", [(cx, yy - h * 0.07), (ex, ey)], P["line"], width=max(SS, int(w * 0.01)))
            rosette(R, ex, ey, min(w, h) * 0.045, P["motif"] if k else P["accent"], P["guard"], P["border"])

def heriz(R, f, P):
    """Heriz (Azerbaijan, NW Iran): a big angular medallion and angular corners, bold and geometric."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    n = 5
    R.paint("polygon", stepped(cx, cy, w * 0.42, h * 0.36, n), P["line"])
    R.paint("polygon", stepped(cx, cy, w * 0.39, h * 0.33, n), P["border"])
    R.paint("polygon", stepped(cx, cy, w * 0.3, h * 0.25, n - 1), P["fdeep"])
    R.paint("polygon", stepped(cx, cy, w * 0.19, h * 0.16, 3), P["guard"])
    R.paint("polygon", star8(cx, cy, min(w, h) * 0.11), P["accent"])
    R.paint("polygon", star8(cx, cy, min(w, h) * 0.05), P["motif"])
    for s in (-1, 1):                                        # angular pendants
        R.paint("polygon", stepped(cx, cy + s * h * 0.42, w * 0.08, h * 0.05, 2), P["border"])
    for (qx, qy, sx, sy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        pts = [(qx, qy)]
        for k in range(5):                                   # staircase corner spandrel
            pts += [(qx + sx * w * 0.24 * (1 - k / 5), qy + sy * h * 0.2 * k / 5), (qx + sx * w * 0.24 * (1 - (k + 1) / 5), qy + sy * h * 0.2 * k / 5)]
        pts += [(qx, qy + sy * h * 0.2)]
        R.paint("polygon", pts, P["border"])
        R.paint("polygon", star8(qx + sx * w * 0.06, qy + sy * h * 0.05, min(w, h) * 0.035), P["motif"])
    rnd = random.Random(3)
    for _ in range(10):                                      # scattered angular flowers on the field
        px, py = rnd.uniform(x0 + w * 0.05, x1 - w * 0.05), rnd.uniform(y0 + h * 0.05, y1 - h * 0.05)
        if abs(px - cx) / (w * 0.45) + abs(py - cy) / (h * 0.4) < 1.05: continue
        R.paint("polygon", stepped(px, py, min(w, h) * 0.035, min(w, h) * 0.035, 2), P["accent"])

def turkmen(R, f, P):
    """Turkmen (Golestan and Khorasan): rows of quartered octagonal guls, small cross guls between."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    cols = 2 if w < h * 0.8 else 3
    gw = w / cols
    rows = max(2, round(h / (gw * 0.75)))
    gh = h / rows
    for j in range(rows):
        for i in range(cols):
            cx, cy = x0 + (i + 0.5) * gw, y0 + (j + 0.5) * gh
            rx, ry = gw * 0.4, gh * 0.4
            R.paint("polygon", octagon(cx, cy, rx * 1.06, ry * 1.06), P["line"])
            R.paint("polygon", octagon(cx, cy, rx, ry), P["guard"])
            # quarters in two colours, diagonally opposite matching
            for q, (a0, a1) in enumerate(((180, 270), (270, 360), (0, 90), (90, 180))):
                R.paint("pieslice", [cx - rx * 0.9, cy - ry * 0.9, cx + rx * 0.9, cy + ry * 0.9], a0, a1, P["accent"] if q % 2 == j % 2 else P["fdeep"])
            R.paint("line", [(cx - rx * 0.9, cy), (cx + rx * 0.9, cy)], P["line"], width=max(SS, int(rx * 0.06)))
            R.paint("line", [(cx, cy - ry * 0.9), (cx, cy + ry * 0.9)], P["line"], width=max(SS, int(rx * 0.06)))
            R.paint("polygon", stepped(cx, cy, rx * 0.3, ry * 0.3, 2), P["motif"])
            if i < cols - 1 and j < rows - 1:                # minor gul between four major ones
                mx, my = cx + gw / 2, cy + gh / 2
                q = min(gw, gh) * 0.1
                R.paint("rectangle", [mx - q * 1.6, my - q * 0.4, mx + q * 1.6, my + q * 0.4], P["motif"])
                R.paint("rectangle", [mx - q * 0.4, my - q * 1.6, mx + q * 0.4, my + q * 1.6], P["motif"])

def qashqai(R, f, P):
    """Qashqai (Fars): a pole of linked stepped diamonds, with stars, birds and goats strewn round it."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    k = 3 if h > w * 1.2 else 1
    seg = h / k
    for i in range(k):
        cy = y0 + seg * (i + 0.5)
        R.paint("polygon", stepped(cx, cy, w * 0.3, seg * 0.42, 4), P["border"])
        R.paint("polygon", stepped(cx, cy, w * 0.21, seg * 0.3, 3), P["fdeep"])
        R.paint("polygon", stepped(cx, cy, w * 0.11, seg * 0.16, 2), P["accent"])
        R.paint("polygon", star8(cx, cy, min(w, seg) * 0.06), P["motif"])
        if i < k - 1:
            R.paint("rectangle", [cx - w * 0.02, cy + seg * 0.4, cx + w * 0.02, cy + seg * 0.6], P["line"])
    rnd = random.Random(11)
    s = min(w, h) * 0.07
    for n in range(26):
        px, py = rnd.uniform(x0 + s, x1 - s), rnd.uniform(y0 + s, y1 - s)
        if abs(px - cx) < w * 0.33: continue
        c = (P["motif"], P["accent"], P["fdeep"], P["line"])[n % 4]
        if n % 3 == 0: animal(R, px, py, s, c, flip=px > cx)
        elif n % 3 == 1: R.paint("polygon", star8(px, py, s * 0.45), c)
        else: R.paint("rectangle", [px - s * 0.2, py - s * 0.2, px + s * 0.2, py + s * 0.2], c)

def gabbeh(R, f, P):
    """Gabbeh (Lori and Qashqai): a thick, plain field of strong abrash, with a few simple trees."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    n = 1 if w < h * 0.6 else 2 if w < h * 1.1 else 3
    for i in range(n):
        tree(R, x0 + w * (i + 0.5) / n, y1 - h * 0.12, min(h * 0.6, w * 0.9 / n), P, P["line"])
    rnd = random.Random(5)
    for k in range(6):
        px, py = rnd.uniform(x0 + w * 0.1, x1 - w * 0.1), rnd.uniform(y0 + h * 0.08, y0 + h * 0.3)
        q = min(w, h) * 0.025
        R.paint("rectangle", [px - q, py - q, px + q, py + q], P["accent"] if k % 2 else P["motif"])

def kilim(R, f, P):
    """Kurdish and Lori kilim: flat-woven bands of zigzags, stepped diamonds and plain stripes."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    bands = max(5, round(h / (w * 0.22)))
    bh = h / bands
    for b in range(bands):
        by = y0 + b * bh
        kind = b % 4
        if kind == 0:
            R.paint("rectangle", [x0, by, x1, by + bh], P["fdeep"])
            n = max(3, int(w / bh))
            for i in range(n):
                cx = x0 + w * (i + 0.5) / n
                R.paint("polygon", stepped(cx, by + bh / 2, w / n * 0.4, bh * 0.4, 2), P["accent"] if i % 2 else P["motif"])
        elif kind == 1:
            n = max(4, int(w / (bh * 0.7)))
            for i in range(n):
                u0, u1 = x0 + w * i / n, x0 + w * (i + 1) / n
                R.paint("polygon", [(u0, by + bh), (u1, by + bh), ((u0 + u1) / 2, by)], P["border"])
        elif kind == 2:
            R.paint("rectangle", [x0, by + bh * 0.3, x1, by + bh * 0.7], P["accent"])
            R.paint("rectangle", [x0, by + bh * 0.42, x1, by + bh * 0.58], P["guard"])
        else:
            n = max(4, int(w / (bh * 0.7)))
            for i in range(n):
                u0, u1 = x0 + w * i / n, x0 + w * (i + 1) / n
                R.paint("polygon", [(u0, by), (u1, by), ((u0 + u1) / 2, by + bh)], P["motif"])

def baluch(R, f, P):
    """Baluch (Khorasan and Sistan): a deep field with a grid of small hooked diamonds."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    cell = min(w, h) / 3.5
    nx = max(2, int(w / cell)); ny = max(2, int(h / cell))
    cw, ch = w / nx, h / ny
    for i in range(nx):
        for j in range(ny):
            cx, cy = x0 + (i + 0.5) * cw, y0 + (j + 0.5) * ch
            R.paint("polygon", [(cx, cy - ch * 0.38), (cx + cw * 0.38, cy), (cx, cy + ch * 0.38), (cx - cw * 0.38, cy)], P["motif"])
            q = min(cw, ch) * 0.07
            for dx, dy in ((0, -0.38), (0.38, 0), (0, 0.38), (-0.38, 0)):     # hooks on the tips
                hx, hy = cx + dx * cw, cy + dy * ch
                R.paint("rectangle", [hx - q, hy - q, hx + q, hy + q], P["accent"])
            R.paint("polygon", [(cx, cy - ch * 0.2), (cx + cw * 0.2, cy), (cx, cy + ch * 0.2), (cx - cw * 0.2, cy)], P["field"])
            R.paint("rectangle", [cx - q, cy - q, cx + q, cy + q], P["accent"] if (i + j) % 2 else P["line"])

def afshan(R, f, P):
    """Afshan (the 'scattered' design): palmettes and blossoms strewn over a field of curling vines."""
    x0, y0, x1, y1 = f
    w, h = x1 - x0, y1 - y0
    rnd = random.Random(8)
    lw = max(SS, int(min(w, h) * 0.012))
    for _ in range(14):                                       # vines
        px, py = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        r = min(w, h) * rnd.uniform(0.12, 0.25)
        a0 = rnd.uniform(0, 360)
        R.paint("arc", [px - r, py - r, px + r, py + r], a0, a0 + rnd.uniform(90, 200), P["fdeep"], width=lw)
    s = min(w, h)
    nx, ny = max(2, round(w / (s * 0.3))), max(2, round(h / (s * 0.3)))
    for i in range(nx):
        for j in range(ny):
            px = x0 + (i + 0.5 + rnd.uniform(-0.15, 0.15)) * w / nx
            py = y0 + (j + 0.5 + rnd.uniform(-0.15, 0.15)) * h / ny
            r = s * 0.075
            if (i + j) % 2:
                R.paint("polygon", lobed(px, py, r, r, 6, 0.18), P["motif"])
                R.paint("ellipse", [px - r * 0.35, py - r * 0.35, px + r * 0.35, py + r * 0.35], P["accent"])
            else:
                rosette(R, px, py, r * 0.8, P["accent"], P["guard"], P["fdeep"])

DESIGNS = {"medallion": lambda R, f, P: (herati(R, f, P, min(f[2] - f[0], f[3] - f[1]) / 4), medallion(R, f, P)),
           "herati": lambda R, f, P: herati(R, f, P, min(f[2] - f[0], f[3] - f[1]) / 5, dense=True),
           "prayer": prayer, "garden": garden, "boteh": botehs, "vase": vase, "heriz": heriz,
           "turkmen": turkmen, "qashqai": qashqai, "gabbeh": gabbeh, "kilim": kilim, "baluch": baluch, "afshan": afshan}
ABRASH = {"gabbeh": 2.2, "kilim": 0.5}                         # how strongly the field bands

def make(size, bbox, design, scheme, border, path, seed):
    R = Rug(size)
    P = SCHEMES[scheme]
    f = fringe_and_body(R, bbox, P, border)
    field(R, f, P, seed, ABRASH.get(design, 1.0))
    DESIGNS[design](R, f, P)
    R.save(path)

if __name__ == "__main__":
    out = sys.argv[1]
    # (design, scheme, border style)
    specs = {"Small":  ((256, 256), (67, 54, 189, 202), [
                ("medallion", "matborder", "rosette"), ("boteh", "matmotif", "sawtooth"), ("garden", "classic", "diamonds"),
                ("herati", "tonal", "rosette"), ("turkmen", "darkfield", "diamonds"), ("gabbeh", "deeptonal", "thin"),
                ("kilim", "madder", "thin"), ("baluch", "deeptonal", "sawtooth"), ("heriz", "tealmat", "diamonds")]),
             "Medium": ((256, 256), (2, 2, 254, 254), [
                ("medallion", "classic", "rosette"), ("garden", "tonal", "diamonds"), ("herati", "ivorymat", "rosette"),
                ("boteh", "tealmat", "sawtooth"), ("vase", "matborder", "rosette"), ("medallion", "darkfield", "sawtooth"),
                ("heriz", "madder", "diamonds"), ("turkmen", "matmotif", "diamonds"), ("qashqai", "tonal", "sawtooth"),
                ("gabbeh", "matborder", "thin"), ("kilim", "tealmat", "thin"), ("afshan", "ivorymat", "rosette")]),
             "Large":  ((384, 384), (66, 3, 319, 381), [
                ("medallion", "matmotif", "rosette"), ("prayer", "madder", "sawtooth"), ("garden", "tealmat", "diamonds"),
                ("herati", "classic", "rosette"), ("boteh", "tonal", "sawtooth"), ("vase", "ivorymat", "rosette"),
                ("medallion", "deeptonal", "diamonds"), ("heriz", "matborder", "diamonds"), ("turkmen", "madder", "sawtooth"),
                ("qashqai", "darkfield", "diamonds"), ("gabbeh", "tonal", "thin"), ("kilim", "matmotif", "thin"),
                ("baluch", "darkfield", "sawtooth"), ("afshan", "tealmat", "rosette")])}
    for size_name, (size, bbox, combos) in specs.items():
        d = os.path.join(out, size_name)
        os.makedirs(d, exist_ok=True)
        for f in os.listdir(d): os.remove(os.path.join(d, f))
        for k, (design, scheme, border) in enumerate(combos):
            make(size, bbox, design, scheme, border, os.path.join(d, f"Iranian{size_name}_{chr(65 + k)}.png"), seed=k * 7 + len(size_name))
        print(size_name, len(combos), "designs")
