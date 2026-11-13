"""Cursed tool item pictures (2026-11-10), flat shapes with a dark outline like Kamutoke's.
Drawn from the canon designs as remembered; she checks them against the manga.
  python tools/art_cursedtools.py [PREVIEW_PNG]
"""
import math, sys
from pathlib import Path
from PIL import Image, ImageDraw

MOD = Path(__file__).resolve().parent.parent
OUT = MOD / "Textures/Things/Jujutsu/Tools"
K = 4                       # supersampling
N = 128
OL = (24, 22, 26, 255)      # outline
STEEL, STEEL_D = (200, 206, 214, 255), (140, 148, 160, 255)
WRAP = (36, 34, 40, 255)
GOLD = (176, 140, 70, 255)


def canvas():
    im = Image.new("RGBA", (N * K, N * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def S(pts): return [(x * K, y * K) for x, y in pts]


def rot(pts, ang, cx=64, cy=64):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def poly(d, pts, fill, ang=0.0, w=2.2):
    d.polygon(S(rot(pts, ang)), fill=fill, outline=OL, width=int(w * K))


def rect(d, x0, y0, x1, y1, fill, ang=0.0):
    poly(d, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill, ang)


def done(im): return im.resize((N, N), Image.LANCZOS)


D45 = -math.pi / 4   # diagonal, point up-right


def inverted_spear():
    im, d = canvas()
    a = D45
    poly(d, [(64, 6), (70, 22), (70, 70), (58, 70), (58, 22)], STEEL, a)          # blade
    poly(d, [(58, 64), (47, 60), (44, 44), (49, 44), (51, 55), (58, 57)], STEEL_D, a)  # side prong, from the blade's base
    rect(d, 50, 70, 78, 76, GOLD, a)                                                # guard
    rect(d, 59, 76, 69, 108, WRAP, a)                                               # grip
    cx, cy = rot([(64, 116)], a)[0]
    d.ellipse(S([(cx - 7, cy - 7), (cx + 7, cy + 7)]), outline=OL, width=int(3.2 * K))   # pommel ring
    return done(im)


def playful_cloud():
    im, d = canvas()
    red = (168, 48, 44, 255)
    segs = [((20, 30), (60, 20)), ((66, 26), (100, 54)), ((96, 62), (70, 104))]
    for (x0, y0), (x1, y1) in segs:
        ang = math.atan2(y1 - y0, x1 - x0)
        L = math.hypot(x1 - x0, y1 - y0)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        pts = [(cx - L / 2, cy - 5), (cx + L / 2, cy - 5), (cx + L / 2, cy + 5), (cx - L / 2, cy + 5)]
        poly(d, pts, red, 0) if False else d.polygon(S(rot(pts, ang, cx, cy)), fill=red, outline=OL, width=int(2.2 * K))
        for t in (0.12, 0.88):   # metal caps
            px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            cap = [(px - 3, py - 5.5), (px + 3, py - 5.5), (px + 3, py + 5.5), (px - 3, py + 5.5)]
            d.polygon(S(rot(cap, ang, px, py)), fill=GOLD, outline=OL, width=int(1.6 * K))
    for (ax, ay), (bx, by) in [((60, 20), (66, 26)), ((100, 54), (96, 62))]:   # short chain links
        d.line(S([(ax, ay), (bx, by)]), fill=STEEL_D, width=int(3 * K))
    return done(im)


def split_soul_katana():
    im, d = canvas()
    a = D45
    poly(d, [(62, 4), (68, 14), (68, 82), (60, 82), (60, 14)], STEEL, a)
    poly(d, [(60, 14), (62, 4), (64, 14), (64, 82), (60, 82)], STEEL_D, a)
    d.ellipse(S(rot([(54, 82)], a) + rot([(74, 88)], a)), fill=GOLD, outline=OL, width=int(2 * K)) if False else None
    rect(d, 54, 82, 74, 88, GOLD, a)
    rect(d, 60, 88, 68, 120, WRAP, a)
    for y in range(92, 118, 6):
        poly(d, [(60, y), (68, y + 3), (60, y + 3)], (90, 86, 96, 255), a, 1.2)
    return done(im)


def ring(d, x, y, ang, a=7.5, b=4.2, col=STEEL):
    pts = [(x + a * math.cos(t) * math.cos(ang) - b * math.sin(t) * math.sin(ang),
            y + a * math.cos(t) * math.sin(ang) + b * math.sin(t) * math.cos(ang)) for t in [i / 24 * 2 * math.pi for i in range(24)]]
    d.line(S(pts + pts[:1]), fill=OL, width=int(4.2 * K), joint="curve")
    d.line(S(pts + pts[:1]), fill=col, width=int(2.0 * K), joint="curve")


def chain():
    """A coil of linked rings, spaced along a spiral so each ring overlaps the next."""
    im, d = canvas()
    cx, cy, t, step = 64, 64, 1.2, 9.5
    pts = []
    while True:
        r = 10 + 5.2 * t
        if r > 54: break
        pts.append((cx + r * math.cos(t), cy + r * math.sin(t), t + math.pi / 2))
        t += step / r
    for i, (x, y, ang) in enumerate(pts):
        ring(d, x, y, ang, 7.5, 4.2 if i % 2 == 0 else 1.8, STEEL if i % 2 == 0 else STEEL_D)
    return done(im)


def dragon_bone():
    im, d = canvas()
    a = D45
    poly(d, [(64, 2), (76, 18), (76, 78), (52, 78), (52, 24)], STEEL, a)     # broad blade
    rect(d, 70, 30, 80, 70, (90, 94, 102, 255), a)                             # jet housing on the spine
    for y in (36, 48, 60):
        rect(d, 80, y, 86, y + 6, (60, 62, 70, 255), a)                       # nozzles
    rect(d, 48, 78, 80, 84, (70, 72, 80, 255), a)
    rect(d, 59, 84, 69, 118, WRAP, a)
    return done(im)


def nanami_cleaver():
    im, d = canvas()
    a = D45
    cloth = (222, 214, 196, 255)
    poly(d, [(56, 8), (74, 8), (76, 78), (54, 78)], cloth, a)                 # wrapped blade
    for y in range(16, 76, 10):                                               # the dotted cloth pattern
        for x in (60, 70):
            px, py = rot([(x + (5 if (y // 10) % 2 else 0) - 2, y)], a)[0]
            d.ellipse(S([(px - 2, py - 2), (px + 2, py + 2)]), fill=(46, 44, 52, 255))
    rect(d, 60, 78, 70, 116, WRAP, a)
    return done(im)


def prison_realm():
    im, d = canvas()
    s = 30
    top = [(64, 64 - s), (64 + s, 64 - s / 2), (64, 64), (64 - s, 64 - s / 2)]
    left = [(64 - s, 64 - s / 2), (64, 64), (64, 64 + s), (64 - s, 64 + s / 2)]
    right = [(64, 64), (64 + s, 64 - s / 2), (64 + s, 64 + s / 2), (64, 64 + s)]
    for pts, c in ((top, (120, 112, 132, 255)), (left, (84, 78, 96, 255)), (right, (64, 58, 76, 255))):
        d.polygon(S(pts), fill=c, outline=OL, width=int(2.4 * K))
    for pts in (top, left, right):   # an eye on each face
        cx = sum(p[0] for p in pts) / 4; cy = sum(p[1] for p in pts) / 4
        d.ellipse(S([(cx - 7, cy - 3.6), (cx + 7, cy + 3.6)]), fill=(236, 230, 214, 255), outline=OL, width=int(1.4 * K))
        d.ellipse(S([(cx - 2.6, cy - 2.6), (cx + 2.6, cy + 2.6)]), fill=OL)
    return done(im)


def black_rope():
    """A coiled black rope with a frayed end."""
    im, d = canvas()
    rope, hi = (34, 32, 38, 255), (78, 74, 84, 255)
    for i, r in enumerate((40, 32, 24, 16)):
        d.ellipse(S([(64 - r, 64 - r * 0.7), (64 + r, 64 + r * 0.7)]), outline=OL, width=int(9 * K))
        d.ellipse(S([(64 - r, 64 - r * 0.7), (64 + r, 64 + r * 0.7)]), outline=rope, width=int(6 * K))
        d.arc(S([(64 - r, 64 - r * 0.7), (64 + r, 64 + r * 0.7)]), 200, 290, fill=hi, width=int(1.6 * K))
    d.line(S([(100, 74), (116, 100)]), fill=OL, width=int(9 * K))
    d.line(S([(100, 74), (116, 100)]), fill=rope, width=int(6 * K))
    for dx in (-3, 0, 3):
        d.line(S([(116, 100), (118 + dx, 110)]), fill=rope, width=int(1.6 * K))
    return done(im)


def yuta_ring():
    """Placeholder: a plain silver band (to redo from references)."""
    im, d = canvas()
    d.ellipse(S([(30, 40), (98, 92)]), outline=OL, width=int(14 * K))
    d.ellipse(S([(30, 40), (98, 92)]), outline=STEEL, width=int(10 * K))
    d.arc(S([(33, 43), (95, 89)]), 200, 260, fill=(250, 250, 255, 255), width=int(3 * K))
    return done(im)


def gauntlets():
    """Placeholder: a dark armoured gauntlet (to redo from references)."""
    im, d = canvas()
    dark, plate = (58, 60, 68, 255), (96, 100, 112, 255)
    poly(d, [(42, 112), (86, 112), (90, 62), (38, 62)], dark)               # forearm guard
    for y in (70, 84, 98):
        poly(d, [(40, y), (88, y), (88, y + 8), (40, y + 8)], plate, 0, 1.6)
    poly(d, [(38, 62), (90, 62), (92, 36), (36, 36)], plate)                 # back of the hand
    for i, x in enumerate((40, 52, 64, 76)):
        poly(d, [(x, 36), (x + 10, 36), (x + 9, 14 + (i % 2) * 4), (x + 1, 14 + (i % 2) * 4)], dark, 0, 1.8)
    return done(im)


ART = {"InvertedSpear": inverted_spear, "PlayfulCloud": playful_cloud, "SplitSoulKatana": split_soul_katana,
       "ChainThousandMiles": chain, "DragonBone": dragon_bone, "NanamiCleaver": nanami_cleaver, "PrisonRealm": prison_realm,
       "BlackRope": black_rope, "YutaRing": yuta_ring, "Gauntlets": gauntlets}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tiles = []
    for name, f in ART.items():
        im = f(); im.save(OUT / f"{name}.png"); tiles.append(im)
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (N * 2 * len(tiles), N * 2), (92, 88, 80, 255))
        for i, t in enumerate(tiles):
            sheet.alpha_composite(t.resize((N * 2, N * 2), Image.NEAREST), (i * N * 2, 0))
        sheet.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
