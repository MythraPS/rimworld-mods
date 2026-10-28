# Rugs for the ideoligion styles other mods add (v1.3). Same machinery as make_rugs4.py; each style gets its own colour
# scheme and designs chosen to suit it, plus one new design (celestial) for the Astronomy style.
#   python3 make_rugs5.py <Textures/IranianRugs/Styles>
import math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_rugs3 as r3, make_rugs4 as r4
from make_rugs3 import SS, NAVY, IVORY, GOLD, MADDER, TEAL, DARK, RUST, star8, rosette, lobed
from make_rugs4 import BONE, BLACK, CRIMSON, SAFFRON, GREEN, STEEL, CYAN, PURPLE, EMERALD, PINK, SKY, box

SILVER, PLUM, OLIVE, SLATE, ICE, AMBER, JADE = (200, 204, 212), (96, 40, 84), (110, 112, 50), (64, 72, 84), (196, 226, 240), (214, 140, 30), (40, 140, 110)
r3.SCHEMES.update({
    "hedonist":   dict(field=.9,  fdeep=.6,  border=CRIMSON, guard=GOLD,   motif=PINK,   accent=GOLD,    line=DARK),
    "corporate":  dict(field=.9,  fdeep=.65, border=SLATE,   guard=SILVER, motif=.5,     accent=NAVY,    line=BLACK),
    "authority":  dict(field=.85, fdeep=.55, border=BLACK,   guard=CRIMSON, motif=GOLD,  accent=BLACK,   line=BLACK),
    "eldritch":   dict(field=.4,  fdeep=.62, border=BLACK,   guard=JADE,   motif=(150, 220, 120), accent=PURPLE, line=BLACK),
    "esoteric":   dict(field=.55, fdeep=.8,  border=PLUM,    guard=GOLD,   motif=GOLD,   accent=.95,     line=DARK),
    "eschaton":   dict(field=.5,  fdeep=.75, border=BLACK,   guard=AMBER,  motif=CRIMSON, accent=SAFFRON, line=BLACK),
    "cult":       dict(field=.92, fdeep=.68, border=GOLD,    guard=CRIMSON, motif=.45,   accent=GOLD,    line=DARK),
    "pantheist":  dict(field=.9,  fdeep=.62, border=GREEN,   guard=IVORY,  motif=OLIVE,  accent=RUST,    line=DARK),
    "omnist":     dict(field=.92, fdeep=.66, border=TEAL,    guard=IVORY,  motif=SAFFRON, accent=MADDER, line=DARK),
    "agnostic":   dict(field=.9,  fdeep=.72, border=.42,     guard=.97,    motif=.6,     accent=.3,      line=.2),
    "secular":    dict(field=.92, fdeep=.7,  border=SKY,     guard=IVORY,  motif=.55,    accent=TEAL,    line=DARK),
    "shinto":     dict(field=.93, fdeep=.7,  border=CRIMSON, guard=IVORY,  motif=BLACK,  accent=CRIMSON, line=BLACK),
    "serketist":  dict(field=.6,  fdeep=.85, border=BLACK,   guard=AMBER,  motif=AMBER,  accent=BLACK,   line=BLACK),
    "viking":     dict(field=.9,  fdeep=.62, border=NAVY,    guard=IVORY,  motif=MADDER, accent=.4,      line=DARK),
    "sophian":    dict(field=.95, fdeep=.75, border=IVORY,   guard=GOLD,   motif=SKY,    accent=GOLD,    line=DARK),
    "astronomy":  dict(field=.35, fdeep=.55, border=NAVY,    guard=GOLD,   motif=GOLD,   accent=ICE,     line=BLACK),
})

def celestial(R, f, P):
    """Astronomy: a sun medallion with rays, crescent moons in the corners, stars strewn over a dark field."""
    x0, y0, x1, y1, w, h = box(f)
    rnd = random.Random(61)
    s = min(w, h)
    for _ in range(int(w * h / (s * s) * 40)):
        x, y = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        R.paint("polygon", star8(x, y, s * rnd.uniform(.012, .03)), P["accent"] if rnd.random() < .7 else P["motif"])
    k = 1 if h < w * 1.25 else 2
    cx = (x0 + x1) / 2
    for i in range(k):
        cy = y0 + h * (i + .5) / k; r = min(w, h / k) * .3
        pts = []
        for q in range(32):
            a = math.pi * q / 16; rr = r * (1 if q % 2 == 0 else .72)
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        R.paint("polygon", pts, P["motif"])
        R.paint("ellipse", [cx - r * .62, cy - r * .62, cx + r * .62, cy + r * .62], P["guard"])
        R.paint("ellipse", [cx - r * .5, cy - r * .5, cx + r * .5, cy + r * .5], P["fdeep"])
        rosette(R, cx, cy, r * .3, P["accent"], P["guard"], P["border"])
    for qx, qy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
        mx, my = qx + (s * .14 if qx == x0 else -s * .14), qy + (s * .14 if qy == y0 else -s * .14); m = s * .08
        R.paint("ellipse", [mx - m, my - m, mx + m, my + m], P["accent"])
        R.paint("ellipse", [mx - m * .55, my - m * 1.05, mx + m * 1.45, my + m * .95], P["field"])

r3.DESIGNS["celestial"] = celestial
r3.ABRASH["celestial"] = .4

STYLES = {  # folder: (scheme, Small, Medium, Large) - each a list of (design, border)
    "Hedonist":       ("hedonist",  [("medallion", "rosette"), ("afshan", "rosette")], [("medallion", "rosette"), ("vase", "rosette"), ("afshan", "rosette")], [("medallion", "rosette"), ("vase", "rosette"), ("afshan", "rosette")]),
    "Corporate":      ("corporate", [("garden", "thin"), ("circuit", "thin")], [("garden", "thin"), ("circuit", "thin"), ("herati", "thin")], [("garden", "thin"), ("circuit", "thin"), ("herati", "thin")]),
    "Authoritarian":  ("authority", [("heriz", "diamonds"), ("sunburst", "sawtooth")], [("heriz", "diamonds"), ("sunburst", "sawtooth"), ("armenian", "diamonds")], [("heriz", "diamonds"), ("sunburst", "sawtooth"), ("armenian", "sawtooth")]),
    "Eldritch":       ("eldritch",  [("eyes", "sawtooth"), ("afshan", "thin")], [("eyes", "sawtooth"), ("afshan", "sawtooth"), ("eyes", "thin")], [("eyes", "sawtooth"), ("afshan", "sawtooth"), ("eyes", "diamonds")]),
    "Esoteric":       ("esoteric",  [("ushak", "diamonds"), ("celestial", "thin")], [("ushak", "diamonds"), ("celestial", "thin"), ("lotus", "thin")], [("ushak", "diamonds"), ("celestial", "thin"), ("lotus", "thin")]),
    "Eschatological": ("eschaton",  [("sunburst", "sawtooth"), ("skulls", "sawtooth")], [("sunburst", "sawtooth"), ("skulls", "sawtooth"), ("kilim", "sawtooth")], [("sunburst", "sawtooth"), ("skulls", "sawtooth"), ("kilim", "sawtooth")]),
    "CultOfPersonality": ("cult",   [("medallion", "rosette"), ("sunburst", "rosette")], [("medallion", "rosette"), ("sunburst", "rosette"), ("heriz", "rosette")], [("medallion", "rosette"), ("sunburst", "rosette"), ("heriz", "rosette")]),
    "Pantheist":      ("pantheist", [("gabbeh", "thin"), ("garden", "diamonds")], [("gabbeh", "thin"), ("garden", "diamonds"), ("frieze", "thin")], [("gabbeh", "thin"), ("garden", "diamonds"), ("frieze", "thin")]),
    "Omnist":         ("omnist",    [("herati", "rosette"), ("mughal", "rosette")], [("herati", "rosette"), ("mughal", "rosette"), ("ushak", "rosette")], [("herati", "rosette"), ("mughal", "rosette"), ("ushak", "rosette")]),
    "Agnostic":       ("agnostic",  [("kilim", "thin"), ("herati", "thin")], [("kilim", "thin"), ("herati", "thin"), ("garden", "thin")], [("kilim", "thin"), ("herati", "thin"), ("garden", "thin")]),
    "SecularSpirituality": ("secular", [("lotus", "thin"), ("garden", "thin")], [("lotus", "thin"), ("garden", "thin"), ("herati", "thin")], [("lotus", "thin"), ("garden", "thin"), ("herati", "thin")]),
    "Shintaoism":     ("shinto",    [("lotus", "thin"), ("kilim", "thin")], [("lotus", "thin"), ("kilim", "thin"), ("garden", "thin")], [("lotus", "thin"), ("kilim", "thin"), ("garden", "thin")]),
    "Serketist":      ("serketist", [("baluch", "sawtooth"), ("horns", "sawtooth")], [("baluch", "sawtooth"), ("horns", "sawtooth"), ("kilim", "sawtooth")], [("baluch", "sawtooth"), ("horns", "sawtooth"), ("kilim", "sawtooth")]),
    "Viking":         ("viking",    [("horns", "diamonds"), ("kilim", "sawtooth")], [("horns", "diamonds"), ("kilim", "sawtooth"), ("frieze", "thin")], [("horns", "diamonds"), ("kilim", "sawtooth"), ("frieze", "thin")]),
    "Sophian":        ("sophian",   [("lotus", "rosette"), ("ushak", "rosette")], [("lotus", "rosette"), ("ushak", "rosette"), ("celestial", "rosette")], [("lotus", "rosette"), ("ushak", "rosette"), ("celestial", "rosette")]),
    "Astronomy":      ("astronomy", [("celestial", "thin"), ("celestial", "diamonds")], [("celestial", "thin"), ("celestial", "diamonds"), ("ushak", "thin")], [("celestial", "thin"), ("celestial", "diamonds"), ("ushak", "thin")]),
}

if __name__ == "__main__":
    out = sys.argv[1]; total = 0
    for style, (scheme, *sizes) in STYLES.items():
        for size_name, combos in zip(("Small", "Medium", "Large"), sizes):
            d = os.path.join(out, style, size_name); os.makedirs(d, exist_ok=True)
            for f in os.listdir(d): os.remove(os.path.join(d, f))
            size, bbox = r4.SIZES[size_name]
            for k, (design, border) in enumerate(combos):
                r3.make(size, bbox, design, scheme, border, os.path.join(d, f"{style}{size_name}_{chr(65 + k)}.png"), seed=k * 5 + len(style))
                total += 1
    print(total, "textures for", len(STYLES), "styles")
