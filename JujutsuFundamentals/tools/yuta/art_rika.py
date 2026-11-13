#!/usr/bin/env python3
"""Rika's art (2026-11-11), drawn from the references in ~/.local/share/rimworld-refs/yuta-images:
  Cursed Spirit Rika (rika_curse_*): lavender-grey giant, ribbed torso, long arms with long-nailed hands,
    a head of finger-like tentacles with black spots around a huge fanged mouth (pink gums, purple tongue),
    lower body tapering into a tail (no smoke: her call 2026-11-11).
  Shikigami Rika (rika_shikigami_*, yuta_rika_pair): pale grey-white, muscular, an elongated ribbed bulb of a
    head with a small red-lipped mouth at the bottom, white flame-like spikes around it, long arms with dark
    nails, lower body a glossy black tail with black thorny wisps.
Outputs (mod Textures/):
  Things/Yuta/Rika/Curse_{south,east,north}.png, Things/Yuta/Rika/Shikigami_{south,east,north}.png  (384 px)
  Things/Yuta/Rika/ArmFleck.png, Things/Yuta/Rika/MawFleck.png, Things/Yuta/Rika/GrappleFleck.png  (256 px)
  Abilities/Yuta/Rika/{ManifestRika,RikaArm,RikaBite,RikaGrapple}.png  (128 px, the mod's dark-square icon style)
Run: python3 tools/yuta/art_rika.py
"""
import math, os
from PIL import Image, ImageDraw, ImageFilter

MOD = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THINGS = os.path.join(MOD, "Textures/Things/Yuta/Rika")
ICONS = os.path.join(MOD, "Textures/Abilities/Yuta/Rika")
os.makedirs(THINGS, exist_ok=True); os.makedirs(ICONS, exist_ok=True)

SS = 4  # supersampling

CURSE = dict(skin=(186, 180, 206), shade=(150, 144, 176), line=(52, 46, 66), spot=(28, 26, 34),
             gum=(204, 84, 116), mouth=(72, 18, 36), tongue=(112, 60, 152), fang=(244, 236, 214),
             smoke=(38, 30, 52), nail=(206, 196, 222))
SHIKI = dict(skin=(220, 221, 226), shade=(168, 170, 182), line=(48, 50, 60), hair=(240, 243, 247),
             hairshade=(176, 190, 204), tail=(24, 22, 30), tailhi=(78, 74, 92), nail=(62, 62, 84), lip=(192, 40, 52))


class Canvas:
    def __init__(self, size):
        self.size = size
        self.S = size * SS
        self.layers = []

    def layer(self):
        im = Image.new("RGBA", (self.S, self.S), (0, 0, 0, 0))
        self.layers.append(im)
        return im, ImageDraw.Draw(im)

    def P(self, x, y):
        return (x * self.S, y * self.S)

    def flatten(self, outline, width=0.012, out=None):
        """Each layer gets its own outline (dilated alpha in the outline colour) under it."""
        base = Image.new("RGBA", (self.S, self.S), (0, 0, 0, 0))
        k = max(3, int(width * self.S) | 1)
        for im in self.layers:
            if outline is not None:
                a = im.split()[3].point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MaxFilter(k))
                ol = Image.new("RGBA", (self.S, self.S), outline + (255,))
                ol.putalpha(a)
                base = Image.alpha_composite(base, ol)
            base = Image.alpha_composite(base, im)
        return base.resize((self.size, self.size), Image.LANCZOS)


def limb(d, c, p1, p2, r1, r2, fill):
    (x1, y1), (x2, y2) = p1, p2
    ang = math.atan2(y2 - y1, x2 - x1)
    nx, ny = -math.sin(ang), math.cos(ang)
    pts = [(x1 + nx * r1, y1 + ny * r1), (x2 + nx * r2, y2 + ny * r2), (x2 - nx * r2, y2 - ny * r2), (x1 - nx * r1, y1 - ny * r1)]
    d.polygon([c.P(*p) for p in pts], fill=fill)
    for (x, y), r in ((p1, r1), (p2, r2)):
        d.ellipse([c.P(x - r, y - r), c.P(x + r, y + r)], fill=fill)


def chain(d, c, pts, radii, fill):
    for i in range(len(pts) - 1):
        limb(d, c, pts[i], pts[i + 1], radii[i], radii[i + 1], fill)


def ell(d, c, cx, cy, rx, ry, fill, width=0, outline=None):
    d.ellipse([c.P(cx - rx, cy - ry), c.P(cx + rx, cy + ry)], fill=fill, outline=outline, width=int(width * c.S) if width else 0)


def line(d, c, pts, col, w):
    d.line([c.P(*p) for p in pts], fill=col, width=max(1, int(w * c.S)), joint="curve")


def bez(p0, p1, p2, n=12):
    out = []
    for i in range(n + 1):
        t = i / n
        out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    return out


def claw(d, c, x, y, a, size, col):
    """A hooked claw from a fingertip along angle a."""
    nx, ny = -math.sin(a), math.cos(a)
    w, L = size * 0.055, size * 0.32
    tip = (x + math.cos(a + 0.35) * L, y + math.sin(a + 0.35) * L)      # hooks a little
    d.polygon([c.P(x + nx * w, y + ny * w), c.P(x - nx * w, y - ny * w), c.P(*tip)], fill=col)


def hand(d, c, wrist, ang, size, col, nail, spread=0.9, fingers=4, length=1.0, claws=None):
    """Long-fingered hand: palm toward ang (radians, 0 = +x, pi/2 = down). claws: a colour for claws on every finger."""
    px, py = wrist[0] + math.cos(ang) * size * 0.5, wrist[1] + math.sin(ang) * size * 0.5
    limb(d, c, wrist, (px, py), size * 0.32, size * 0.42, col)
    for i in range(fingers):
        a = ang + (i - (fingers - 1) / 2) * spread / max(1, fingers - 1)
        bx, by = px + math.cos(a) * size * 0.3, py + math.sin(a) * size * 0.3
        mx, my = bx + math.cos(a + 0.15) * size * 0.55 * length, by + math.sin(a + 0.15) * size * 0.55 * length
        tx, ty = mx + math.cos(a + 0.35) * size * 0.45 * length, my + math.sin(a + 0.35) * size * 0.45 * length
        chain(d, c, [(bx, by), (mx, my), (tx, ty)], [size * 0.11, size * 0.09, size * 0.065], col)
        ell(d, c, tx, ty, size * 0.06, size * 0.06, nail)
        if claws: claw(d, c, tx, ty, a + 0.35, size, claws)
    # thumb
    a = ang - spread * 0.9
    bx, by = px + math.cos(a) * size * 0.2, py + math.sin(a) * size * 0.2
    tx, ty = bx + math.cos(a - 0.4) * size * 0.5, by + math.sin(a - 0.4) * size * 0.5
    limb(d, c, (bx, by), (tx, ty), size * 0.11, size * 0.07, col)
    ell(d, c, tx, ty, size * 0.06, size * 0.06, nail)
    if claws: claw(d, c, tx, ty, a - 0.4, size, claws)


# ============================================================ Cursed Spirit Rika

def curse_smoke(c, cx, top, w):
    im, d = c.layer()
    col = CURSE["smoke"]
    for i in range(14):
        x = cx + (i / 13 - 0.5) * w * 1.4
        y = top + 0.05 + (i % 3) * 0.03
        ell(d, c, x, y + 0.08, 0.07 + (i % 4) * 0.012, 0.09, col + (255,))
        # wisps curling up at the edge
        if i % 2 == 0:
            line(d, c, bez((x, y + 0.1), (x + (0.06 if i < 7 else -0.06), y), (x + (0.03 if i < 7 else -0.03), y - 0.08)), col, 0.025)
    # lighter crests
    for i in range(7):
        x = cx + (i / 6 - 0.5) * w * 1.2
        line(d, c, bez((x - 0.04, top + 0.12), (x, top + 0.04), (x + 0.04, top + 0.1)), (70, 60, 92), 0.01)


def curse_tentacles(c, cx, cy, n, spread, length, back=False):
    im, d = c.layer()
    col = CURSE["skin"]
    for i in range(n):
        a = -math.pi / 2 + (i - (n - 1) / 2) * spread / (n - 1)
        L = length * (0.85 + 0.25 * ((i * 37) % 5) / 4)
        bend = 0.35 if a > -math.pi / 2 else -0.35
        p0 = (cx + math.cos(a) * 0.04, cy + math.sin(a) * 0.04)
        p1 = (cx + math.cos(a) * L * 0.6, cy + math.sin(a) * L * 0.6)
        p2 = (cx + math.cos(a + bend) * L, cy + math.sin(a + bend) * L)
        pts = bez(p0, p1, p2, 8)
        radii = [0.045 - 0.018 * k / 8 for k in range(9)]
        chain(d, c, pts, radii, col)
        # knuckle creases and black spots (the reference's mottling)
        for k in (3, 6):
            x, y = pts[k]
            ell(d, c, x + 0.01, y, 0.016, 0.012, CURSE["spot"])
        x, y = pts[-1]
        ell(d, c, x, y, 0.026, 0.026, CURSE["nail"])


def curse_mouth(c, cx, cy, rx, ry, rot=0.0):
    im, d = c.layer()
    ell(d, c, cx, cy, rx * 1.2, ry * 1.12, CURSE["skin"])                 # lips/flesh rim
    ell(d, c, cx, cy, rx, ry, CURSE["gum"])
    ell(d, c, cx, cy + ry * 0.05, rx * 0.72, ry * 0.8, CURSE["mouth"])
    ell(d, c, cx, cy + ry * 0.35, rx * 0.45, ry * 0.42, CURSE["tongue"])
    # fangs along the top and bottom rims, pointing into the mouth (the reference: two rows, none at the corners)
    for row, (a0, a1) in ((-1, (-2.75, -0.39)), (1, (0.39, 2.75))):
        n = 7
        for i in range(n):
            a = a0 + (a1 - a0) * i / (n - 1)
            bx, by = cx + math.cos(a) * rx * 0.9, cy + math.sin(a) * ry * 0.9
            L = ry * (0.55 if i % 2 else 0.38)
            tx, ty = bx + (cx - bx) * 0.15, by - row * L
            w = rx * (0.11 if i % 2 else 0.08)
            d.polygon([c.P(bx - w, by), c.P(bx + w, by), c.P(tx, ty)], fill=CURSE["fang"])


def curse_torso(c, cx, top, w, h, ribs=True, back=False):
    im, d = c.layer()
    pts = [(cx - w * 0.55, top), (cx + w * 0.55, top), (cx + w * 0.38, top + h * 0.6), (cx + w * 0.2, top + h),
           (cx - w * 0.2, top + h), (cx - w * 0.38, top + h * 0.6)]
    d.polygon([c.P(*p) for p in pts], fill=CURSE["skin"])
    ell(d, c, cx - w * 0.42, top + 0.02, w * 0.2, w * 0.16, CURSE["skin"])
    ell(d, c, cx + w * 0.42, top + 0.02, w * 0.2, w * 0.16, CURSE["skin"])
    if ribs:
        for k in range(6):
            y = top + h * (0.25 + k * 0.11)
            ww = w * (0.38 - k * 0.03)
            line(d, c, bez((cx - ww, y), (cx, y + 0.03), (cx + ww, y)), CURSE["shade"], 0.009)
    if back:
        for k in range(7):
            y = top + h * (0.1 + k * 0.12)
            ell(d, c, cx, y, 0.012, 0.016, CURSE["shade"])
    # spots
    for (dx, dy) in ((-0.3, 0.75), (0.28, 0.85), (0.1, 0.95)):
        ell(d, c, cx + w * dx, top + h * dy, 0.018, 0.012, CURSE["spot"])


def curse_arm(c, shoulder, elbow, wrist, ang, col=None, bulge=1.0, spread=1.0):
    """A muscular arm (her call 2026-11-11: more dynamic): curved upper arm and forearm through the elbow, a bulging
    biceps and forearm, a knobbly elbow, muscle lines, and a clawed hand flaring at the end."""
    im, d = c.layer()
    col = col or CURSE["skin"]
    sx, sy = shoulder; ex, ey = elbow; wx, wy = wrist
    # the upper arm bows outward, the forearm bows the other way: an S through the elbow instead of two rods
    def bow(a, b, k):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        nx, ny = -(b[1] - a[1]), b[0] - a[0]
        return (mx + nx * k, my + ny * k)
    upper = bez(shoulder, bow(shoulder, elbow, 0.22), elbow, 8)
    fore = bez(elbow, bow(elbow, wrist, -0.18), wrist, 8)
    r_up = [0.066 * bulge, 0.072 * bulge, 0.074 * bulge, 0.068 * bulge, 0.06, 0.054, 0.05, 0.047, 0.046]
    r_fo = [0.046, 0.05, 0.054 * bulge, 0.055 * bulge, 0.052, 0.047, 0.042, 0.038, 0.035]
    chain(d, c, upper, r_up, col)
    chain(d, c, fore, r_fo, col)
    ell(d, c, ex, ey, 0.05, 0.045, col)                                   # the elbow knob
    # muscle lines along the biceps and forearm
    line(d, c, upper[2:7], CURSE["shade"], 0.008)
    line(d, c, fore[2:6], CURSE["shade"], 0.007)
    ell(d, c, ex + 0.01, ey + 0.012, 0.013, 0.01, CURSE["spot"])
    hand(d, c, wrist, ang, 0.13, col, CURSE["nail"], spread=1.05 * spread, length=1.2, claws=(40, 34, 50))   # claws: her call 2026-11-11


def curse_head(c, cx, cy, rx, ry, mouth=True, turn=0.0):
    """Her head as in the JJK 0 reference (her call 2026-11-11): a tall ridged dome, and at its bottom a triangular
    mouth, wide under the dome and narrowing to the chin, lined with fangs. turn > 0 shifts the face toward +x."""
    im, d = c.layer()
    ell(d, c, cx, cy, rx, ry, CURSE["skin"])
    # vertical ridges over the dome, curving with it
    for k in range(-3, 4):
        x0 = cx + turn * rx * 0.5 + k * rx * 0.24
        if abs(x0 - cx) > rx * 0.92: continue
        line(d, c, bez((x0, cy - ry * 0.92), (x0 + (x0 - cx) * 0.25, cy - ry * 0.2), (x0 + (x0 - cx) * 0.05, cy + ry * 0.35)), CURSE["shade"], 0.008)
    if not mouth:
        return
    mx = cx + turn * rx * 0.55
    top, bot = cy + ry * 0.2, cy + ry * 0.97
    hw = rx * (0.97 if turn == 0 else 0.62)   # as wide as her face (her call 2026-11-11)
    # shaped after her reference picture (2026-11-12): wide under the dome, the sides drawing in to a rounded chin,
    # thick pink gums along the top and down the sides, long thin fangs hanging from the top and shorter ones rising
    # from the bottom, dark inside, no tongue
    def half(t):                       # half-width down the mouth, t 0 = top, 1 = chin
        return 1.0 - 0.42 * t ** 1.4
    def outline(scale=1.0, inset=0.0):
        pts = []
        for k in range(15):
            t = k / 14
            pts.append((mx - hw * (half(t) * scale - inset), top + (bot - top) * t * scale))
        for k in range(1, 12):         # the rounded chin
            a = math.pi * k / 12
            r = hw * (half(1.0) * scale - inset)
            pts.append((mx - r * math.cos(a), top + (bot - top) * scale + r * 0.55 * math.sin(a)))
        for k in range(14, -1, -1):
            t = k / 14
            pts.append((mx + hw * (half(t) * scale - inset), top + (bot - top) * t * scale))
        return pts
    S_ = 0.86                          # the shape's height before the chin's curve
    d.polygon([c.P(*q) for q in outline(S_)], fill=CURSE["gum"])
    inner = [(x, y + (bot - top) * 0.25 if y <= top + 1e-9 else y) for x, y in outline(S_, 0.21)]
    gum_top = top + (bot - top) * 0.25
    inner = [(x, max(y, gum_top)) for x, y in inner]
    d.polygon([c.P(*q) for q in inner], fill=CURSE["mouth"])
    # top fangs: long and thin, the middle ones longest
    n = 8
    span = hw * (half(0.0) - 0.21) * 0.95
    for k in range(n):
        x = mx - span + 2 * span * (k + 0.5) / n
        mid = 1.0 - abs((k + 0.5) / n - 0.5) * 1.3
        L = (bot - top) * (0.30 + 0.22 * mid)
        w = span / n * 0.62
        d.polygon([c.P(x - w, gum_top - 0.004), c.P(x + w, gum_top - 0.004), c.P(x, gum_top + L)], fill=CURSE["fang"])
    # bottom fangs: shorter, rising from the chin's gum
    chin = top + (bot - top) * S_ + hw * half(1.0) * 0.55 * 0.62
    m = 6
    bspan = hw * (half(1.0) * S_ - 0.21) * 0.9
    for k in range(m):
        x = mx - bspan + 2 * bspan * (k + 0.5) / m
        L = (bot - top) * 0.22 * (1.0 - abs((k + 0.5) / m - 0.5) * 0.8)
        w = bspan / m * 0.6
        yb = chin - (abs(x - mx) / max(bspan, 1e-6)) ** 2 * hw * 0.2
        d.polygon([c.P(x - w, yb), c.P(x + w, yb), c.P(x, yb - L)], fill=CURSE["fang"])

def curse_tail(c, cx, top, w, lean=0.0):
    """Below the waist, no legs: dark tendrils, thick ribbons that sweep out to the sides and trail off (her call
    2026-11-11, from the reference: "that dark tendril thing"; it replaced the smoke)."""
    im, d = c.layer()
    col, hi = (34, 28, 44), (70, 60, 88)
    # (start dx, mid, end, width): two broad ribbons out to each side and one trailing down
    for sx, mid, end, wd in ((-0.04, (-0.22, 0.12), (-0.44, 0.17), 0.075), (0.04, (0.22, 0.12), (0.44, 0.15), 0.075),
                              (0.0, (0.06 + lean, 0.13), (0.1 + lean, 0.23), 0.06)):
        pts = bez((cx + sx, top), (cx + mid[0], top + mid[1]), (cx + end[0], top + end[1]), 14)
        radii = [wd * (1 - 0.8 * k / 14) for k in range(15)]
        chain(d, c, pts, radii, col)
        line(d, c, pts[2:-3], hi, 0.008)
    ell(d, c, cx, top + 0.01, w * 1.1, 0.05, col)


def curse_neck(c, cx, top, bottom, w0, w1):
    im, d = c.layer()
    d.polygon([c.P(cx - w0, top), c.P(cx + w0, top), c.P(cx + w1, bottom), c.P(cx - w1, bottom)], fill=CURSE["skin"])
    line(d, c, [(cx - w0 * 0.5, top + 0.01), (cx - w1 * 0.55, bottom)], CURSE["shade"], 0.008)
    line(d, c, [(cx + w0 * 0.5, top + 0.01), (cx + w1 * 0.55, bottom)], CURSE["shade"], 0.008)


def draw_curse(face):
    """Neck, ridged dome head and triangular mouth after the JJK 0 reference (her call 2026-11-11; the head used to
    be only tentacles round a round mouth, with no neck)."""
    c = Canvas(384)
    cx = 0.5
    if face == "south":
        curse_tentacles(c, cx, 0.2, 13, math.pi * 1.5, 0.22)       # her hair-like tendrils, behind the head
        curse_arm(c, (0.33, 0.45), (0.17, 0.4), (0.15, 0.58), math.pi * 0.7, bulge=1.1, spread=1.15)   # raised, clawing out
        curse_arm(c, (0.67, 0.45), (0.83, 0.56), (0.8, 0.75), math.pi * 0.45)                         # low, reaching down
        curse_tail(c, cx, 0.74, 0.08)
        curse_torso(c, cx, 0.42, 0.38, 0.4)
        curse_neck(c, cx, 0.3, 0.45, 0.03, 0.05)   # slimmer (her call 2026-11-11)
        curse_head(c, cx, 0.19, 0.08, 0.15)
    elif face == "north":
        curse_arm(c, (0.67, 0.45), (0.83, 0.4), (0.85, 0.58), math.pi * 0.3, bulge=1.1, spread=1.15)
        curse_arm(c, (0.33, 0.45), (0.17, 0.56), (0.2, 0.75), math.pi * 0.55)
        curse_tail(c, cx, 0.74, 0.08)
        curse_torso(c, cx, 0.42, 0.38, 0.4, ribs=False, back=True)
        curse_neck(c, cx, 0.3, 0.45, 0.03, 0.05)   # slimmer (her call 2026-11-11)
        curse_head(c, cx, 0.19, 0.08, 0.15, mouth=False)            # the back of the dome
        curse_tentacles(c, cx, 0.2, 13, math.pi * 1.5, 0.22)       # tendrils fall over it from behind
    else:  # east: leaning toward +x, reaching forward
        curse_tentacles(c, 0.5, 0.2, 9, math.pi * 1.1, 0.22)
        curse_arm(c, (0.42, 0.45), (0.28, 0.56), (0.24, 0.74), math.pi * 0.55, col=CURSE["shade"])   # far arm, hanging back
        curse_tail(c, 0.46, 0.8, 0.06, lean=-0.08)
        im, d = c.layer()
        pts = [(0.36, 0.42), (0.6, 0.4), (0.6, 0.62), (0.52, 0.82), (0.4, 0.82), (0.34, 0.62)]
        d.polygon([c.P(*p) for p in pts], fill=CURSE["skin"])
        for k in range(6):
            y = 0.5 + k * 0.05
            line(d, c, [(0.5, y), (0.6 - k * 0.012, y + 0.01)], CURSE["shade"], 0.008)
        ell(d, c, 0.42, 0.64, 0.016, 0.012, CURSE["spot"])
        curse_neck(c, 0.53, 0.3, 0.44, 0.03, 0.045)
        curse_head(c, 0.57, 0.19, 0.075, 0.15, turn=0.6)
        curse_arm(c, (0.52, 0.45), (0.64, 0.6), (0.77, 0.6), math.pi * 0.15, bulge=1.1, spread=1.15)   # near arm, lunging forward
    return c.flatten(CURSE["line"], 0.01)


# ============================================================ Shikigami Rika

def shiki_tail(c, cx, top, side=1, east=False):
    im, d = c.layer()
    col = SHIKI["tail"]
    pts = bez((cx, top), (cx + 0.05 * side, top + 0.22), (cx + 0.24 * side, top + 0.26), 12)
    radii = [0.09 - 0.07 * k / 12 for k in range(13)]
    chain(d, c, pts, radii, col)
    # glossy highlight
    line(d, c, bez((cx - 0.03 * side, top + 0.02), (cx + 0.0, top + 0.17), (cx + 0.12 * side, top + 0.22), 10), SHIKI["tailhi"], 0.008)
    # thorny black wisp off the tip
    x, y = pts[-1]
    tips = [(x + 0.05 * side, y - 0.08), (x + 0.1 * side, y - 0.16), (x + 0.08 * side, y - 0.26)]
    line(d, c, [(x, y)] + tips, col, 0.012)
    for (tx, ty) in tips:
        line(d, c, [(tx, ty), (tx + 0.03 * side, ty - 0.02)], col, 0.006)
        line(d, c, [(tx, ty), (tx - 0.02 * side, ty - 0.03)], col, 0.006)


def shiki_flames(c, cx, cy, n, R, spread=2 * math.pi, start=-math.pi / 2):
    im, d = c.layer()
    for i in range(n):
        a = start + (i / (n - 1) - 0.5) * spread if spread < 2 * math.pi else i / n * 2 * math.pi
        L = R * (0.8 + 0.4 * ((i * 53) % 7) / 6)
        wave = 0.14 if i % 2 else -0.14
        p0 = (cx + math.cos(a) * R * 0.35, cy + math.sin(a) * R * 0.35)
        p1 = (cx + math.cos(a + wave) * L * 0.7, cy + math.sin(a + wave) * L * 0.7)
        p2 = (cx + math.cos(a - wave * 0.6) * L, cy + math.sin(a - wave * 0.6) * L)
        pts = bez(p0, p1, p2, 8)
        radii = [0.022 * (1 - k / 9) + 0.003 for k in range(9)]
        chain(d, c, pts, radii, SHIKI["hair"] if i % 3 else SHIKI["hairshade"])


def shiki_head(c, cx, cy, rx, ry, tilt=0.0, face=True):
    im, d = c.layer()
    # elongated bulb, ribbed lengthwise
    pts = []
    for k in range(40):
        t = k / 40 * 2 * math.pi
        x, y = rx * math.sin(t), -ry * math.cos(t)
        x *= 1.0 - 0.25 * max(0.0, math.cos(t + math.pi))   # narrower at the bottom
        pts.append((cx + x * math.cos(tilt) - y * math.sin(tilt), cy + x * math.sin(tilt) + y * math.cos(tilt)))
    d.polygon([c.P(*p) for p in pts], fill=SHIKI["skin"])
    for k in (-0.55, -0.2, 0.2, 0.55):
        top = (cx + k * rx * 0.9, cy - ry * 0.82)
        mid = (cx + k * rx * 1.08, cy)
        bot = (cx + k * rx * 0.6, cy + ry * 0.8)
        line(d, c, bez(top, mid, bot, 10), SHIKI["shade"], 0.006)
    if face:
        mx, my = cx + math.sin(-tilt) * ry * 0.95, cy + ry * 0.95
        # a grin (her calls 2026-11-12: open with the teeth showing, then closed into a grin): clenched teeth between
        # red lips, the corners turned up
        w, h = rx * 0.66, ry * 0.32   # taller (her call 2026-11-12; was ry * 0.2)
        xs = [k / 12 * 2 - 1 for k in range(13)]
        up = lambda u: my - h * 0.45 - h * 0.35 * u * u
        lo = lambda u: my + h * 0.6 - h * 1.4 * u * u
        shape = [(mx + u * w, up(u)) for u in xs] + [(mx + u * w, lo(u)) for u in reversed(xs)]
        d.polygon([c.P(*q) for q in shape], fill=(246, 242, 232), outline=SHIKI["lip"], width=max(2, int(c.size * 0.012)))
        line(d, c, [(mx + u * w * 0.96, (up(u) + lo(u)) / 2) for u in xs], (120, 110, 110), 0.004)   # where the teeth meet
        for k in range(1, 8):                                                                       # gaps between teeth
            u = k / 8 * 2 - 1
            line(d, c, [(mx + u * w, up(u) + h * 0.1), (mx + u * w, lo(u) - h * 0.1)], (150, 140, 140), 0.003)


def shiki_arm(c, shoulder, elbow, wrist, ang, col=None):
    im, d = c.layer()
    col = col or SHIKI["skin"]
    chain(d, c, [shoulder, elbow, wrist], [0.055, 0.04, 0.03], col)
    line(d, c, [shoulder, elbow], SHIKI["shade"], 0.006)   # muscle line
    hand(d, c, wrist, ang, 0.11, col, SHIKI["nail"], spread=0.9, length=1.05)


def shiki_torso(c, cx, top, w, h, back=False):
    im, d = c.layer()
    pts = [(cx - w * 0.55, top), (cx + w * 0.55, top), (cx + w * 0.3, top + h * 0.7), (cx + w * 0.22, top + h),
           (cx - w * 0.22, top + h), (cx - w * 0.3, top + h * 0.7)]
    d.polygon([c.P(*p) for p in pts], fill=SHIKI["skin"])
    ell(d, c, cx - w * 0.44, top + 0.02, w * 0.2, w * 0.15, SHIKI["skin"])
    ell(d, c, cx + w * 0.44, top + 0.02, w * 0.2, w * 0.15, SHIKI["skin"])
    if back:
        line(d, c, [(cx, top + 0.02), (cx, top + h * 0.9)], SHIKI["shade"], 0.006)
        for s in (-1, 1):
            line(d, c, bez((cx + s * w * 0.1, top + h * 0.1), (cx + s * w * 0.4, top + h * 0.2), (cx + s * w * 0.25, top + h * 0.5)), SHIKI["shade"], 0.006)
    else:
        line(d, c, bez((cx - w * 0.4, top + h * 0.15), (cx, top + h * 0.28), (cx + w * 0.4, top + h * 0.15)), SHIKI["shade"], 0.006)  # pecs
        line(d, c, [(cx, top + h * 0.28), (cx, top + h * 0.95)], SHIKI["shade"], 0.006)
        for k in range(3):
            y = top + h * (0.45 + k * 0.15)
            line(d, c, [(cx - w * 0.16, y), (cx + w * 0.16, y)], SHIKI["shade"], 0.005)
    # the stitch-like scars from the reference
    for (dx, dy) in ((-0.3, 0.3), (0.25, 0.55)):
        x, y = cx + w * dx, top + h * dy
        line(d, c, [(x - 0.03, y), (x + 0.03, y + 0.01)], SHIKI["shade"], 0.004)


def draw_shiki(face):
    c = Canvas(384)
    cx = 0.5
    if face == "south":
        shiki_tail(c, cx, 0.66, 1)
        shiki_arm(c, (0.34, 0.42), (0.2, 0.56), (0.17, 0.73), math.pi * 0.6)
        shiki_arm(c, (0.66, 0.42), (0.8, 0.56), (0.83, 0.73), math.pi * 0.4)
        shiki_torso(c, cx, 0.38, 0.34, 0.32)
        shiki_flames(c, cx, 0.24, 30, 0.23)
        shiki_head(c, cx, 0.25, 0.072, 0.11)
    elif face == "north":
        shiki_tail(c, cx, 0.66, -1)
        shiki_arm(c, (0.34, 0.42), (0.2, 0.56), (0.17, 0.73), math.pi * 0.6)
        shiki_arm(c, (0.66, 0.42), (0.8, 0.56), (0.83, 0.73), math.pi * 0.4)
        shiki_torso(c, cx, 0.38, 0.34, 0.32, back=True)
        shiki_head(c, cx, 0.26, 0.072, 0.11, face=False)
        shiki_flames(c, cx, 0.25, 30, 0.23)
    else:
        shiki_tail(c, 0.44, 0.66, -1)
        shiki_arm(c, (0.42, 0.42), (0.32, 0.57), (0.34, 0.74), math.pi * 0.5, col=SHIKI["shade"])
        im, d = c.layer()
        pts = [(0.36, 0.38), (0.58, 0.37), (0.56, 0.6), (0.5, 0.72), (0.4, 0.72), (0.36, 0.58)]
        d.polygon([c.P(*p) for p in pts], fill=SHIKI["skin"])
        line(d, c, bez((0.56, 0.42), (0.6, 0.5), (0.54, 0.6)), SHIKI["shade"], 0.006)
        shiki_flames(c, 0.54, 0.25, 22, 0.22, spread=math.pi * 1.5, start=-math.pi * 0.85)
        shiki_head(c, 0.58, 0.27, 0.06, 0.1, tilt=-0.5)
        shiki_arm(c, (0.5, 0.42), (0.66, 0.52), (0.76, 0.66), math.pi * 0.3)
    return c.flatten(SHIKI["line"], 0.01)


# ============================================================ flecks

def arm_fleck():
    """Rika's arm rising out of the ground and slamming down (top-down view: a huge hand, fingers spread)."""
    c = Canvas(256)
    im, d = c.layer()
    ell(d, c, 0.5, 0.82, 0.26, 0.09, CURSE["smoke"] + (220,))
    im, d = c.layer()
    limb(d, c, (0.5, 0.86), (0.5, 0.6), 0.08, 0.07, CURSE["skin"])
    hand(d, c, (0.5, 0.6), -math.pi / 2, 0.26, CURSE["skin"], CURSE["nail"], spread=1.3, length=0.95)
    return c.flatten(CURSE["line"], 0.014)


def maw_fleck():
    c = Canvas(256)
    curse_tentacles(c, 0.5, 0.5, 10, math.pi * 1.6, 0.3)
    curse_mouth(c, 0.5, 0.52, 0.22, 0.26)
    return c.flatten(CURSE["line"], 0.014)


def grapple_fleck():
    """Two huge hands closing from both sides (drawn over the held pawn)."""
    c = Canvas(256)
    im, d = c.layer()
    hand(d, c, (0.12, 0.55), -0.15, 0.26, CURSE["skin"], CURSE["nail"], spread=1.1, length=0.9)
    im, d = c.layer()
    hand(d, c, (0.88, 0.55), math.pi + 0.15, 0.26, CURSE["skin"], CURSE["nail"], spread=1.1, length=0.9)
    return c.flatten(CURSE["line"], 0.014)


# ============================================================ icons

BG = (30, 20, 38)


def icon(img, scale=0.92, dy=0.0):
    out = Image.new("RGBA", (128, 128), BG + (255,))
    s = int(128 * scale)
    im = img.resize((s, s), Image.LANCZOS)
    out.alpha_composite(im, ((128 - s) // 2, int((128 - s) // 2 + dy * 128)))
    return out


def manifest_icon():
    c = Canvas(256)
    shiki_flames(c, 0.5, 0.5, 30, 0.44)
    shiki_head(c, 0.5, 0.5, 0.14, 0.22)
    return c.flatten(SHIKI["line"], 0.012)


def main():
    for face in ("south", "east", "north"):
        draw_curse(face).save(os.path.join(THINGS, f"Curse_{face}.png"))
        draw_shiki(face).save(os.path.join(THINGS, f"Shikigami_{face}.png"))
    arm, maw, grab = arm_fleck(), maw_fleck(), grapple_fleck()
    arm.save(os.path.join(THINGS, "ArmFleck.png"))
    maw.save(os.path.join(THINGS, "MawFleck.png"))
    grab.save(os.path.join(THINGS, "GrappleFleck.png"))
    icon(manifest_icon()).save(os.path.join(ICONS, "ManifestRika.png"))
    icon(arm, 1.0, 0.04).save(os.path.join(ICONS, "RikaArm.png"))
    icon(maw, 1.0).save(os.path.join(ICONS, "RikaBite.png"))
    icon(grab, 1.05).save(os.path.join(ICONS, "RikaGrapple.png"))
    print("wrote", THINGS, ICONS)


if __name__ == "__main__":
    main()
