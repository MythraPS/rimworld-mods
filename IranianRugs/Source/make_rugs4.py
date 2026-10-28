# Rugs that follow the ideoligion's style (Ideology), version 4. Builds on make_rugs3.py: same Rug
# class, masks, borders and designs, plus designs taken from neighbouring weaving traditions where
# Iran has no counterpart for a style:
#   Islamic   - mihrab prayer rugs, Ushak star medallions (Anatolia), saf (a row of arches)
#   Hindu     - Mughal flowering plants in a lattice (Agra, Lahore), boteh, vase
#   Buddhist  - Tibetan and Khotan lotus roundels inside a key-fret frame
#   Christian - Armenian and Caucasian cross medallions (Kazak, Karabagh)
#   Morbid    - Tibetan tantric skull rugs, dark Baluch
#   Totemic   - Turkmen guls, Kazakh and Kyrgyz ram's-horn felts, Qashqai
#   Spikecore - Eagle Kazak sunbursts and serrated kilims
#   Rustic    - Gabbeh and kilim
#   Animalist - animal friezes (Pazyryk, Caucasian), Qashqai
#   Techist   - right-angled fret lattices, garden-panel grids
#   Horaxian  - eye medallions on a dark field with curling vines
#   python3 make_rugs4.py <Textures/IranianRugs/Styles>
import math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_rugs3 as r3
from make_rugs3 import SS, NAVY, IVORY, GOLD, MADDER, TEAL, DARK, RUST, stepped, star8, rosette, lobed, octagon, animal, tree

BONE, BLACK, CRIMSON, SAFFRON = (226, 216, 190), (24, 20, 24), (150, 24, 34), (222, 150, 40)
GREEN, STEEL, CYAN, PURPLE, EMERALD = (58, 104, 62), (70, 84, 100), (90, 200, 215), (84, 44, 110), (30, 110, 84)
PINK, SKY = (204, 110, 130), (120, 160, 200)

r3.SCHEMES.update({
    "islamic":   dict(field=.9,  fdeep=.62, border=EMERALD, guard=IVORY, motif=GOLD,  accent=NAVY,    line=DARK),
    "hindu":     dict(field=.92, fdeep=.66, border=CRIMSON, guard=SAFFRON, motif=.5,  accent=PINK,    line=DARK),
    "buddhist":  dict(field=.9,  fdeep=.6,  border=SAFFRON, guard=NAVY,  motif=.5,    accent=CRIMSON, line=DARK),
    "christian": dict(field=.92, fdeep=.66, border=NAVY,    guard=IVORY, motif=.5,    accent=GOLD,    line=DARK),
    "morbid":    dict(field=.42, fdeep=.7,  border=BLACK,   guard=BONE,  motif=BONE,  accent=CRIMSON, line=BLACK),
    "totemic":   dict(field=.9,  fdeep=.6,  border=RUST,    guard=IVORY, motif=.45,   accent=TEAL,    line=DARK),
    "spikecore": dict(field=.88, fdeep=.55, border=BLACK,   guard=.97,   motif=CRIMSON, accent=.35,   line=BLACK),
    "rustic":    dict(field=.9,  fdeep=.64, border=.45,     guard=IVORY, motif=GREEN, accent=RUST,    line=DARK),
    "animalist": dict(field=.92, fdeep=.66, border=GREEN,   guard=IVORY, motif=.45,   accent=RUST,    line=DARK),
    "techist":   dict(field=.9,  fdeep=.6,  border=STEEL,   guard=.97,   motif=CYAN,  accent=NAVY,    line=BLACK),
    "horaxian":  dict(field=.4,  fdeep=.62, border=BLACK,   guard=PURPLE, motif=BONE, accent=CRIMSON, line=BLACK),
})

def box(f): return f[0], f[1], f[2], f[3], f[2] - f[0], f[3] - f[1]

def grid(f, cell):
    x0, y0, x1, y1, w, h = box(f)
    nx, ny = max(1, round(w / cell)), max(1, round(h / cell))
    return [(x0 + (i + .5) * w / nx, y0 + (j + .5) * h / ny, w / nx, h / ny, i, j) for i in range(nx) for j in range(ny)]

def cross(R, cx, cy, rx, ry, col, arm=.34):
    R.paint("rectangle", [cx - rx * arm, cy - ry, cx + rx * arm, cy + ry], col)
    R.paint("rectangle", [cx - rx, cy - ry * arm, cx + rx, cy + ry * arm], col)


# ---- new designs -----------------------------------------------------------------------------
def ushak(R, f, P):
    """Ushak star (Anatolia): eight-pointed star medallions in staggered rows on a lattice."""
    x0, y0, x1, y1, w, h = box(f)
    r3.herati(R, f, P, min(w, h) / 4)
    for cx, cy, cw, ch, i, j in grid(f, min(w, h) / 1.6):
        r = min(cw, ch) * .42
        R.paint("polygon", star8(cx, cy, r * 1.08), P["line"])
        R.paint("polygon", star8(cx, cy, r), P["border"])
        R.paint("polygon", octagon(cx, cy, r * .55, r * .55), P["guard"])
        R.paint("polygon", star8(cx, cy, r * .42), P["accent"])
        rosette(R, cx, cy, r * .16, P["motif"], P["guard"], P["border"])

def saf(R, f, P):
    """Saf: a row of mihrab arches side by side, a hanging lamp in each."""
    x0, y0, x1, y1, w, h = box(f)
    n = 2 if w < h * .9 else 3
    R.paint("rectangle", f, P["border"])
    for i in range(n):
        a0, a1 = x0 + w * i / n + w * .03, x0 + w * (i + 1) / n - w * .03
        cx, aw = (a0 + a1) / 2, a1 - a0
        col = P["field"] if i % 2 == 0 else P["fdeep"]
        R.paint("pieslice", [a0, y0 + h * .05, a1, y0 + h * .05 + aw], 180, 360, col)
        R.paint("rectangle", [a0, y0 + h * .05 + aw / 2 - 1, a1, y1 - h * .04], col)
        R.paint("line", [(cx, y0 + h * .08), (cx, y0 + h * .3)], P["line"], width=max(SS, int(aw * .03)))
        R.paint("polygon", [(cx, y0 + h * .28), (cx + aw * .12, y0 + h * .36), (cx, y0 + h * .44), (cx - aw * .12, y0 + h * .36)], P["motif"])
        rosette(R, cx, y1 - h * .2, aw * .14, P["accent"], P["guard"], P["line"])

def mughal(R, f, P):
    """Mughal flowering plants: a blossoming stem in every cell of a lattice."""
    x0, y0, x1, y1, w, h = box(f)
    for cx, cy, cw, ch, i, j in grid(f, min(w, h) / 3.2):
        R.paint("polygon", [(cx, cy - ch * .5), (cx + cw * .5, cy), (cx, cy + ch * .5), (cx - cw * .5, cy)], P["fdeep"])
        R.paint("polygon", [(cx, cy - ch * .44), (cx + cw * .44, cy), (cx, cy + ch * .44), (cx - cw * .44, cy)], P["field"])
        lw = max(SS, int(cw * .035))
        R.paint("line", [(cx, cy + ch * .32), (cx, cy - ch * .12)], GREEN, width=lw)
        for s in (-1, 1):
            R.paint("line", [(cx, cy + ch * .12), (cx + s * cw * .2, cy - ch * .02)], GREEN, width=lw)
            R.paint("ellipse", [cx + s * cw * .2 - cw * .07, cy - ch * .09, cx + s * cw * .2 + cw * .07, cy + ch * .05], P["accent"])
            R.paint("polygon", [(cx, cy + ch * .25), (cx + s * cw * .16, cy + ch * .2), (cx, cy + ch * .16)], GREEN)
        rosette(R, cx, cy - ch * .2, min(cw, ch) * .11, P["accent"] if (i + j) % 2 else P["motif"], P["guard"], P["border"])

def lotus(R, f, P):
    """Tibetan and Khotan: lotus roundels inside a key-fret frame, small clouds in the corners."""
    x0, y0, x1, y1, w, h = box(f)
    t = min(w, h) * .05
    n = max(6, int((w + h) / (t * 3)))
    for k in range(int(w / (t * 2))):                        # key fret along top and bottom
        for yy in (y0 + t * .4, y1 - t * 1.4):
            R.paint("rectangle", [x0 + k * t * 2, yy, x0 + k * t * 2 + t * 1.3, yy + t], P["accent"])
            R.paint("rectangle", [x0 + k * t * 2 + t * .3, yy + t * .3, x0 + k * t * 2 + t, yy + t * .7], P["field"])
    k = 1 if h < w * 1.25 else 3
    for i in range(k):
        cx, cy = (x0 + x1) / 2, y0 + h * (i + .5) / k
        r = min(w, h / k) * (.36 if i == k // 2 else .26)
        R.paint("ellipse", [cx - r * 1.05, cy - r * 1.05, cx + r * 1.05, cy + r * 1.05], P["line"])
        R.paint("ellipse", [cx - r, cy - r, cx + r, cy + r], P["border"])
        for ring, (m, col, rr) in enumerate(((12, P["accent"], .8), (8, P["guard"], .55))):
            for q in range(m):
                a = 2 * math.pi * (q + .5 * ring) / m
                px, py, pr = cx + math.cos(a) * r * rr * .75, cy + math.sin(a) * r * rr * .75, r * rr * .3
                R.paint("ellipse", [px - pr, py - pr, px + pr, py + pr], col)
        R.paint("ellipse", [cx - r * .25, cy - r * .25, cx + r * .25, cy + r * .25], P["motif"])
    for qx, qy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):   # cloud knots
        sx, sy = (1 if qx == x0 else -1), (1 if qy == y0 else -1)
        for d in (0, 1, 2):
            px, py, pr = qx + sx * w * (.1 + .05 * d), qy + sy * h * (.11 - .015 * d), min(w, h) * .035
            R.paint("ellipse", [px - pr, py - pr, px + pr, py + pr], P["fdeep"])

def armenian(R, f, P):
    """Armenian and Caucasian cross medallions (Kazak, Karabagh), small crosses and stars between."""
    x0, y0, x1, y1, w, h = box(f)
    k = 1 if h < w * 1.25 else 3
    cx = (x0 + x1) / 2
    for i in range(k):
        cy, seg = y0 + h * (i + .5) / k, h / k
        rx, ry = w * .36, seg * .4
        R.paint("polygon", stepped(cx, cy, rx * 1.08, ry * 1.08, 3), P["line"])
        R.paint("polygon", stepped(cx, cy, rx, ry, 3), P["border"])
        R.paint("polygon", stepped(cx, cy, rx * .74, ry * .74, 2), P["guard"])
        cross(R, cx, cy, rx * .5, ry * .55, P["accent"])
        cross(R, cx, cy, rx * .34, ry * .38, P["border"], arm=.22)
        R.paint("rectangle", [cx - rx * .07, cy - ry * .07, cx + rx * .07, cy + ry * .07], P["motif"])
    rnd = random.Random(21)
    s = min(w, h) * .05
    for n in range(22):
        px, py = rnd.uniform(x0 + s, x1 - s), rnd.uniform(y0 + s, y1 - s)
        if abs(px - cx) < w * .4 and min(abs(py - (y0 + h * (i + .5) / k)) for i in range(k)) < h / k * .44: continue
        if n % 2: cross(R, px, py, s * .6, s * .6, P["fdeep"] if n % 4 == 1 else P["accent"], arm=.3)
        else: R.paint("polygon", star8(px, py, s * .5), P["motif"])

def skull(R, cx, cy, s, bone, dark):
    R.paint("ellipse", [cx - s * .5, cy - s * .6, cx + s * .5, cy + s * .25], bone)
    R.paint("rectangle", [cx - s * .3, cy + s * .05, cx + s * .3, cy + s * .5], bone)
    for d in (-1, 1):
        R.paint("ellipse", [cx + d * s * .22 - s * .14, cy - s * .22, cx + d * s * .22 + s * .14, cy + s * .08], dark)
    R.paint("polygon", [(cx, cy + s * .08), (cx + s * .07, cy + s * .22), (cx - s * .07, cy + s * .22)], dark)
    for t in (-.15, 0, .15):
        R.paint("rectangle", [cx + t * s - s * .02, cy + s * .3, cx + t * s + s * .02, cy + s * .5], dark)

def skulls(R, f, P):
    """Tibetan tantric rugs: skulls in a diamond lattice on a dark field."""
    x0, y0, x1, y1, w, h = box(f)
    for cx, cy, cw, ch, i, j in grid(f, min(w, h) / 2.6):
        R.paint("polygon", [(cx, cy - ch * .5), (cx + cw * .5, cy), (cx, cy + ch * .5), (cx - cw * .5, cy)], P["accent"])
        R.paint("polygon", [(cx, cy - ch * .43), (cx + cw * .43, cy), (cx, cy + ch * .43), (cx - cw * .43, cy)], P["field"])
        skull(R, cx, cy, min(cw, ch) * .46, P["motif"], P["line"])

def horns(R, f, P):
    """Kazakh and Kyrgyz ram's horn (koshkar muiz): crosses whose arms curl into horns."""
    x0, y0, x1, y1, w, h = box(f)
    for cx, cy, cw, ch, i, j in grid(f, min(w, h) / 2.1):
        r = min(cw, ch) * .4
        col = P["border"] if (i + j) % 2 == 0 else P["accent"]
        R.paint("polygon", stepped(cx, cy, r * 1.12, r * 1.12, 3), P["fdeep"])
        cross(R, cx, cy, r * .7, r * .7, col, arm=.16)
        lw = max(SS, int(r * .11))
        for k in range(4):
            a = k * math.pi / 2
            ex, ey = cx + math.cos(a) * r * .7, cy + math.sin(a) * r * .7
            for sgn in (-1, 1):
                hx, hy = ex + math.cos(a + sgn * math.pi / 2) * r * .2, ey + math.sin(a + sgn * math.pi / 2) * r * .2
                q = r * .2
                R.paint("arc", [hx - q, hy - q, hx + q, hy + q], 0, 300, col, width=lw)
        R.paint("polygon", stepped(cx, cy, r * .2, r * .2, 2), P["motif"])

def sunburst(R, f, P):
    """Eagle Kazak: a medallion with long radiating spikes, serrated bars above and below."""
    x0, y0, x1, y1, w, h = box(f)
    k = 1 if h < w * 1.25 else 2
    cx = (x0 + x1) / 2
    for i in range(k):
        cy, seg = y0 + h * (i + .5) / k, h / k
        r = min(w, seg) * .46
        for scale, col, n in ((1.0, P["line"], 16), (.92, P["motif"], 16), (.66, P["accent"], 12), (.42, P["guard"], 8)):
            pts = []
            for q in range(n * 2):
                a = math.pi * q / n
                rr = r * scale * (1 if q % 2 == 0 else .45)
                pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a) * min(1.25, seg / w if seg > w else 1)))
            R.paint("polygon", pts, col)
        R.paint("polygon", stepped(cx, cy, r * .16, r * .16, 2), P["line"])
    n = max(6, int(w / (min(w, h) * .08)))
    for yy, d in ((y0, 1), (y1, -1)):
        for q in range(n):
            u0, u1 = x0 + w * q / n, x0 + w * (q + 1) / n
            R.paint("polygon", [(u0, yy), (u1, yy), ((u0 + u1) / 2, yy + d * h * .07)], P["line"] if q % 2 else P["motif"])

def frieze(R, f, P):
    """Animal friezes (Pazyryk, Caucasian): rows of walking animals between plain stripes, trees at the ends."""
    x0, y0, x1, y1, w, h = box(f)
    rows = max(3, round(h / (w * .3)))
    rh = h / rows
    for j in range(rows):
        cy = y0 + (j + .5) * rh
        R.paint("rectangle", [x0, cy + rh * .44, x1, cy + rh * .5], P["accent"])
        if j % 3 == 2:
            for i in range(3):
                tree(R, x0 + w * (i + .5) / 3, cy + rh * .4, rh * .8, P, P["line"])
            continue
        n = max(2, int(w / (rh * 1.1)))
        s = min(rh * .5, w / n * .55)
        for i in range(n):
            animal(R, x0 + w * (i + .5) / n, cy, s, (P["motif"], P["accent"], P["line"])[(i + j) % 3], flip=bool(j % 2))

def circuit(R, f, P):
    """Fret lattice: right-angled lines running between square nodes."""
    x0, y0, x1, y1, w, h = box(f)
    cells = grid(f, min(w, h) / 4)
    lw = max(SS, int(min(w, h) * .014))
    rnd = random.Random(33)
    for cx, cy, cw, ch, i, j in cells:
        R.paint("rectangle", [cx - cw * .5, cy - ch * .5, cx + cw * .5, cy + ch * .5], P["fdeep"] if (i + j) % 2 else P["field"])
    for cx, cy, cw, ch, i, j in cells:
        d = rnd.choice(((1, 0, 0, 1), (-1, 0, 0, 1), (1, 0, 0, -1), (0, 1, 1, 0)))
        mx, my = cx + d[0] * cw * .5, cy + d[1] * ch * .5
        ex, ey = mx + d[2] * cw * .5, my + d[3] * ch * .5
        for a, b in (((cx, cy), (mx, my)), ((mx, my), (ex, ey))):
            if x0 <= b[0] <= x1 and y0 <= b[1] <= y1: R.paint("line", [a, b], P["accent"], width=lw)
    for cx, cy, cw, ch, i, j in cells:
        q = min(cw, ch) * (.16 if (i * 3 + j) % 4 == 0 else .09)
        R.paint("rectangle", [cx - q * 1.3, cy - q * 1.3, cx + q * 1.3, cy + q * 1.3], P["line"])
        R.paint("rectangle", [cx - q, cy - q, cx + q, cy + q], P["motif"] if (i * 3 + j) % 4 == 0 else P["border"])

def eye(R, cx, cy, rx, ry, P):
    R.paint("polygon", [(cx - rx, cy), (cx - rx * .4, cy - ry), (cx + rx * .4, cy - ry), (cx + rx, cy), (cx + rx * .4, cy + ry), (cx - rx * .4, cy + ry)], P["line"])
    R.paint("polygon", [(cx - rx * .88, cy), (cx - rx * .35, cy - ry * .82), (cx + rx * .35, cy - ry * .82), (cx + rx * .88, cy), (cx + rx * .35, cy + ry * .82), (cx - rx * .35, cy + ry * .82)], P["motif"])
    r = ry * .72
    R.paint("ellipse", [cx - r, cy - r, cx + r, cy + r], P["accent"])
    R.paint("ellipse", [cx - r * .45, cy - r * .45, cx + r * .45, cy + r * .45], P["line"])

def eyes(R, f, P):
    """A great eye medallion on a dark field of curling vines, smaller eyes strewn round it."""
    x0, y0, x1, y1, w, h = box(f)
    rnd = random.Random(44)
    lw = max(SS, int(min(w, h) * .014))
    for _ in range(18):
        r = min(w, h) * rnd.uniform(.1, .2)
        px, py = rnd.uniform(x0 + r, x1 - r), rnd.uniform(y0 + r, y1 - r)
        a0 = rnd.uniform(0, 360)
        R.paint("arc", [px - r, py - r, px + r, py + r], a0, a0 + rnd.uniform(120, 250), P["guard"], width=lw)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    R.paint("polygon", lobed(cx, cy, w * .4, min(h * .26, w * .34), 10, .08), P["guard"])
    R.paint("polygon", lobed(cx, cy, w * .36, min(h * .23, w * .3), 10, .08), P["fdeep"])
    eye(R, cx, cy, w * .28, min(h * .12, w * .16), P)
    s = min(w, h) * .09
    for n in range(14):
        px, py = rnd.uniform(x0 + s, x1 - s), rnd.uniform(y0 + s * .6, y1 - s * .6)
        if abs(px - cx) < w * .42 and abs(py - cy) < min(h * .28, w * .36): continue
        eye(R, px, py, s, s * .5, P)

r3.DESIGNS.update({"ushak": ushak, "saf": saf, "mughal": mughal, "lotus": lotus, "armenian": armenian, "skulls": skulls,
                   "horns": horns, "sunburst": sunburst, "frieze": frieze, "circuit": circuit, "eyes": eyes})
r3.ABRASH.update({"skulls": .5, "eyes": .6, "circuit": .3})

# style -> scheme, and per size a list of (design, border style); the scheme may be overridden per rug
STYLES = {
    "Islamic":   ("islamic",   {"Small": [("ushak", "rosette"), ("medallion", "rosette"), ("herati", "diamonds")],
                                "Medium": [("ushak", "rosette"), ("medallion", "rosette"), ("saf", "thin")],
                                "Large": [("prayer", "rosette"), ("ushak", "diamonds"), ("saf", "thin"), ("medallion", "rosette")]}),
    "Hindu":     ("hindu",     {"Small": [("mughal", "rosette"), ("boteh", "sawtooth")],
                                "Medium": [("mughal", "rosette"), ("boteh", "rosette"), ("vase", "rosette")],
                                "Large": [("mughal", "rosette"), ("vase", "rosette"), ("boteh", "sawtooth")]}),
    "Buddhist":  ("buddhist",  {"Small": [("lotus", "thin"), ("lotus", "diamonds")],
                                "Medium": [("lotus", "thin"), ("lotus", "rosette"), ("garden", "thin")],
                                "Large": [("lotus", "thin"), ("lotus", "rosette"), ("afshan", "thin")]}),
    "Christian": ("christian", {"Small": [("armenian", "diamonds"), ("armenian", "sawtooth")],
                                "Medium": [("armenian", "diamonds"), ("armenian", "rosette"), ("heriz", "diamonds")],
                                "Large": [("armenian", "diamonds"), ("armenian", "sawtooth"), ("heriz", "diamonds")]}),
    "Morbid":    ("morbid",    {"Small": [("skulls", "sawtooth"), ("baluch", "sawtooth")],
                                "Medium": [("skulls", "sawtooth"), ("baluch", "diamonds"), ("skulls", "thin")],
                                "Large": [("skulls", "sawtooth"), ("baluch", "sawtooth"), ("skulls", "diamonds")]}),
    "Totemic":   ("totemic",   {"Small": [("horns", "diamonds"), ("turkmen", "diamonds")],
                                "Medium": [("horns", "diamonds"), ("turkmen", "sawtooth"), ("qashqai", "sawtooth")],
                                "Large": [("horns", "diamonds"), ("turkmen", "diamonds"), ("qashqai", "sawtooth")]}),
    "Spikecore": ("spikecore", {"Small": [("sunburst", "sawtooth"), ("kilim", "sawtooth")],
                                "Medium": [("sunburst", "sawtooth"), ("kilim", "sawtooth"), ("sunburst", "thin")],
                                "Large": [("sunburst", "sawtooth"), ("kilim", "sawtooth"), ("heriz", "sawtooth")]}),
    "Rustic":    ("rustic",    {"Small": [("gabbeh", "thin"), ("kilim", "thin")],
                                "Medium": [("gabbeh", "thin"), ("kilim", "thin"), ("garden", "thin")],
                                "Large": [("gabbeh", "thin"), ("kilim", "thin"), ("gabbeh", "diamonds")]}),
    "Animalist": ("animalist", {"Small": [("frieze", "thin"), ("qashqai", "diamonds")],
                                "Medium": [("frieze", "diamonds"), ("qashqai", "sawtooth"), ("gabbeh", "thin")],
                                "Large": [("frieze", "diamonds"), ("qashqai", "diamonds"), ("frieze", "thin")]}),
    "Techist":   ("techist",   {"Small": [("circuit", "thin"), ("circuit", "diamonds")],
                                "Medium": [("circuit", "thin"), ("garden", "thin"), ("circuit", "diamonds")],
                                "Large": [("circuit", "thin"), ("circuit", "diamonds"), ("garden", "thin")]}),
    "Horaxian":  ("horaxian",  {"Small": [("eyes", "sawtooth"), ("eyes", "thin")],
                                "Medium": [("eyes", "sawtooth"), ("eyes", "diamonds"), ("baluch", "sawtooth")],
                                "Large": [("eyes", "sawtooth"), ("eyes", "diamonds"), ("afshan", "sawtooth")]}),
}
SIZES = {"Small": ((256, 256), (67, 54, 189, 202)), "Medium": ((256, 256), (2, 2, 254, 254)), "Large": ((384, 384), (66, 3, 319, 381))}

if __name__ == "__main__":
    out = sys.argv[1]
    total = 0
    for style, (scheme, per_size) in STYLES.items():
        for size_name, combos in per_size.items():
            d = os.path.join(out, style, size_name)
            os.makedirs(d, exist_ok=True)
            for f in os.listdir(d): os.remove(os.path.join(d, f))
            size, bbox = SIZES[size_name]
            for k, (design, border) in enumerate(combos):
                r3.make(size, bbox, design, scheme, border, os.path.join(d, f"{style}{size_name}_{chr(65 + k)}.png"), seed=k * 5 + len(style))
                total += 1
    print(total, "textures for", len(STYLES), "styles")
