"""Second pass at the character hair and face marks (her note 2026-11-02: the first hair looked
off-style, the hairline was ugly, the eyes looked shut). Hair now hugs the skull like vanilla
hair does, with curved tufts instead of flat triangles and no dark undercut band; shading is
vanilla's soft darkening toward the outline.

Head geometry (vanilla Male_Average_Normal, 128px):
  south/north: head x 41-87, y 39-89; eyes x 52-59 and 68-75, y 67-72
  east:        head x 39-88, y 40-87; eye  x 74-81, y 67-72

  python tools/art_hair2.py HEADS_DIR TEXTURES_DIR [PREVIEW_DIR]
"""
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

K = 4
KING_LENGTH = 0.55    # the true form's spikes (was 0.75; her note 2026-11-06: not too long)
YUJI_LENGTH = 0.6     # Yuji's locks, as a share of their first length (2026-11-06)
N = 128 * K
DIRS = ("south", "east", "north")


def head_mask(heads, d, grow=1.5):
    a = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA").getchannel("A")
    a = a.resize((N, N), Image.BICUBIC).point(lambda v: 255 if v > 100 else 0)
    if grow:
        a = a.filter(ImageFilter.MaxFilter(int(2 * grow * K) | 1))
    return a


def above(fn):
    """Pixels with y <= fn(x), 128-space."""
    m = Image.new("L", (N, N), 0)
    pts = [(x * K, fn(x) * K) for x in [i / 4 for i in range(0, 513)]]
    ImageDraw.Draw(m).polygon([(0, 0)] + pts + [(N, 0)], fill=255)
    return m


def union(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = ImageChops.lighter(out, m)
    return out


def minus(a, b):
    return ImageChops.subtract(a, b)


def inter(a, b):
    return ImageChops.multiply(a, b)


def qbez(p0, p1, p2, n=16):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in [i / n for i in range(n + 1)]]


def tuft(bx, by, w, tx, ty, bend=0.0, f=1.0):
    """A curved lock: base centred on (bx,by) and w wide, tip at (tx,ty). bend curls it sideways
    (positive = clockwise). f shortens it toward its base."""
    tx, ty = bx + (tx - bx) * f, by + (ty - by) * f
    dx, dy = tx - bx, ty - by
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L, dx / L
    a = (bx + nx * w / 2, by + ny * w / 2)
    b = (bx - nx * w / 2, by - ny * w / 2)
    mx, my = bx + dx * 0.55 + nx * bend * L * 0.25, by + dy * 0.55 + ny * bend * L * 0.25
    ca = (mx + nx * w * 0.32, my + ny * w * 0.32)
    cb = (mx - nx * w * 0.32, my - ny * w * 0.32)
    pts = qbez(a, ca, (tx, ty)) + qbez((tx, ty), cb, b)[1:]
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon([(x * K, y * K) for x, y in pts], fill=255)
    return m


def finish(fill, strands=(), outline=2.6, lo=196):
    """Vanilla look: near-white fill that darkens softly toward the outline, ~3px black edge."""
    edge = fill.filter(ImageFilter.GaussianBlur(5 * K))          # 255 deep inside, lower near edges
    shade = edge.point(lambda v: int(lo + (252 - lo) * min(1, v / 200)))
    base = Image.merge("RGB", [shade] * 3)
    dr = ImageDraw.Draw(base)
    for pts, w, g in strands:
        dr.line([(x * K, y * K) for x, y in pts], fill=(g, g, g), width=max(1, int(w * K)), joint="curve")
    r = int(outline * K)
    outer = fill.filter(ImageFilter.MaxFilter(2 * r + 1))
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    img.paste((0, 0, 0, 255), mask=outer)
    img.paste(base, mask=fill)
    return img.resize((128, 128), Image.LANCZOS)


# --------------------------------------------------------------------------- hair
def yuji(heads, d, L=1.0):
    """Short spiky pink hair, locks falling forward over the forehead. L shortens every lock toward
    its root (her note 2026-11-06: shorter; YUJI_LENGTH below)."""
    hm = head_mask(heads, d)
    if d == "south":
        line = lambda x: 49 + 3.5 * math.cos((x - 64) / 23 * math.pi / 2) ** 2 if 41 <= x <= 87 else 46
        cap = inter(hm, above(lambda x: max(line(x), 58 if (x < 45 or x > 83) else 0)))
        t = [tuft(46, 47, 9, 37, 33, -0.4, f=L), tuft(53, 42, 10, 49, 25, -0.3, f=L), tuft(60, 40, 10, 58, 21, -0.15, f=L),
             tuft(67, 40, 10, 70, 21, 0.15, f=L), tuft(74, 42, 10, 80, 25, 0.3, f=L), tuft(81, 47, 9, 91, 33, 0.4, f=L),
             tuft(56, 47, 9, 54, 57, 0.3, f=L), tuft(64, 48, 9, 64, 58, 0.0, f=L), tuft(72, 47, 9, 74, 57, -0.3, f=L),
             tuft(44, 52, 6, 42, 60, 0.2, f=L), tuft(84, 52, 6, 86, 60, -0.2, f=L)]
        strands = [([(52, 33), (55, 43)], 0.7, 170), ([(64, 28), (64, 40)], 0.7, 170), ([(76, 33), (73, 43)], 0.7, 170)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 50 if x >= 66 else 50 + (66 - x) * 0.6))
        t = [tuft(44, 50, 9, 33, 38, -0.3, f=L), tuft(50, 44, 10, 41, 28, -0.25, f=L), tuft(57, 41, 10, 53, 22, -0.1, f=L),
             tuft(64, 40, 10, 66, 21, 0.1, f=L), tuft(71, 41, 10, 77, 24, 0.2, f=L), tuft(78, 44, 9, 88, 31, 0.3, f=L),
             tuft(80, 48, 8, 88, 57, -0.2, f=L), tuft(73, 48, 8, 77, 57, -0.1, f=L)]
        strands = [([(48, 34), (53, 45)], 0.7, 170), ([(62, 28), (62, 41)], 0.7, 170)]
    else:
        cap = inter(hm, above(lambda x: 66))
        t = [tuft(46, 48, 9, 37, 33, -0.4, f=L), tuft(53, 43, 10, 49, 26, -0.3, f=L), tuft(60, 41, 10, 58, 22, -0.15, f=L),
             tuft(67, 41, 10, 70, 22, 0.15, f=L), tuft(74, 43, 10, 80, 26, 0.3, f=L), tuft(81, 48, 9, 91, 33, 0.4, f=L),
             tuft(52, 64, 8, 50, 71, 0.2, f=L), tuft(60, 65, 8, 59, 72, 0, f=L), tuft(68, 65, 8, 69, 72, 0, f=L), tuft(76, 64, 8, 78, 71, -0.2, f=L)]
        strands = [([(56, 46), (57, 60)], 0.7, 175), ([(71, 46), (70, 60)], 0.7, 175)]
    return finish(union(cap, *t), strands)


def modulo(heads, d):
    """Modulo Yuji (her note 2026-11-06: neat hair): short and combed flat, swept to his left, a soft
    side fringe instead of spikes. Same skull-hugging cap as the others, a little fuller on top."""
    hm = head_mask(heads, d, grow=2.4)
    if d == "south":
        # hairline sweeps from low on his right (viewer's left) up to high on his left
        line = lambda x: (52 - (x - 41) * 0.16 if 41 <= x <= 87 else 58)
        cap = inter(hm, above(lambda x: max(line(x), 60 if (x < 44 or x > 84) else 0)))
        t = [tuft(50, 48, 10, 42, 55, -0.3), tuft(58, 47, 9, 52, 53, -0.2), tuft(84, 47, 7, 87, 54, 0.2)]
        strands = [([(70, 37), (54, 47)], 0.6, 172), ([(76, 39), (62, 48)], 0.6, 172), ([(64, 34), (49, 45)], 0.6, 172)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 52 if x >= 64 else 52 + (64 - x) * 0.55))
        t = [tuft(76, 46, 9, 83, 53, 0.25), tuft(68, 44, 9, 76, 49, 0.2)]
        strands = [([(52, 36), (70, 44)], 0.6, 172), ([(46, 42), (64, 47)], 0.6, 172)]
    else:
        cap = inter(hm, above(lambda x: 68))
        t = [tuft(52, 64, 8, 51, 70, 0.1), tuft(64, 65, 9, 64, 71, 0), tuft(76, 64, 8, 77, 70, -0.1)]
        strands = [([(56, 44), (58, 62)], 0.6, 175), ([(70, 44), (69, 62)], 0.6, 175)]
    return finish(union(cap, *t), strands)


def sukuna(heads, d, f=1.0):
    """Swept up and back off the forehead. f shortens the locks: King 0.75, vessel 0.45."""
    hm = head_mask(heads, d)
    if d == "south":
        line = lambda x: 45 + 2.5 * math.cos((x - 64) / 23 * math.pi / 2) ** 2 if 41 <= x <= 87 else 44
        cap = inter(hm, above(lambda x: max(line(x), 57 if (x < 44 or x > 84) else 0)))
        t = [tuft(46, 46, 9, 33, 28, -0.35, f), tuft(52, 41, 10, 44, 18, -0.25, f), tuft(59, 39, 10, 55, 12, -0.1, f),
             tuft(66, 39, 10, 71, 12, 0.1, f), tuft(73, 41, 10, 83, 18, 0.25, f), tuft(80, 46, 9, 94, 28, 0.35, f),
             tuft(63, 41, 8, 63, 16, 0.0, f)]
        strands = [([(54, 44), (57, 49)], 0.6, 175), ([(64, 43), (64, 48)], 0.6, 175), ([(74, 44), (71, 49)], 0.6, 175)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 46 if x >= 68 else 46 + (68 - x) * 0.6))
        t = [tuft(46, 48, 9, 30, 36, -0.2, f), tuft(52, 43, 10, 36, 25, -0.2, f), tuft(59, 40, 10, 47, 16, -0.15, f),
             tuft(66, 39, 10, 60, 13, -0.1, f), tuft(73, 40, 10, 73, 14, 0.05, f), tuft(80, 43, 9, 84, 22, 0.1, f)]
        strands = [([(76, 44), (70, 47)], 0.6, 175)]
    else:
        cap = inter(hm, above(lambda x: 64))
        t = [tuft(46, 47, 9, 33, 29, -0.35, f), tuft(52, 42, 10, 44, 19, -0.25, f), tuft(59, 40, 10, 55, 13, -0.1, f),
             tuft(66, 40, 10, 71, 13, 0.1, f), tuft(73, 42, 10, 83, 19, 0.25, f), tuft(80, 47, 9, 94, 29, 0.35, f)]
        strands = [([(58, 46), (59, 60)], 0.7, 175), ([(70, 46), (69, 60)], 0.7, 175)]
    return finish(union(cap, *t), strands)


# --------------------------------------------------------------------------- face marks
INK = (24, 18, 20, 255)


def draw4(fn):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    fn(ImageDraw.Draw(img))
    return img.resize((128, 128), Image.LANCZOS)


def S(pts):
    return [(x * K, y * K) for x, y in pts]


SCAR = (86, 26, 32, 255)


def yuji_marks(d):
    """Yuji's own (redrawn 2026-11-06, her note: the old curved lines read as eyebags): a thin closed
    slit, scar-red, exactly where Sukuna's second eyes open (same centres as sukuna_marks), with the
    same small outer lash, so it reads as a sealed eye."""
    def f(dr):
        if d == "north":
            return
        eyes = [(55.0, -1), (72.0, 1)] if d == "south" else [(78.0, 1)]
        for cx, s in eyes:
            cx += 1.2 * s
            y = 76.1
            top = qbez((cx - 2.8, y), (cx, y - 0.7), (cx + 2.8, y), 10)
            bot = qbez((cx + 2.8, y), (cx, y + 0.7), (cx - 2.8, y), 10)
            dr.polygon(S(top + bot[1:]), fill=SCAR)
            dr.line(S([(cx - 2.8, y), (cx + 2.8, y)]), fill=SCAR, width=int(1.1 * K))
            dr.line(S([(cx + 0.3, y - 1.4), (cx - 0.3, y + 1.4)]), fill=SCAR, width=int(0.8 * K))   # stitch
            dr.line(S([(cx + 2.5 * s, y - 0.1), (cx + 4.0 * s, y - 1.1)]), fill=SCAR, width=int(0.8 * K))
    return draw4(f)


def face_extras(dr, d, skip_right=False):
    """More of Sukuna's markings (her note 2026-11-06: more tattoos): a short mark over each brow,
    sloping up and out, and two stripes across each cheek toward the ear. skip_right: leave his
    right side bare (the true form's mask covers it)."""
    if d == "north":
        return
    sides = [(55.5, -1), (71.5, 1)] if d == "south" else [(77.5, 1)]
    for cx, s in sides:
        if skip_right and d == "south" and s < 0:
            continue
        dr.line(S([(cx - 2.2 * s, 64.2), (cx + 3.0 * s, 62.4)]), fill=INK, width=int(1.1 * K))      # brow
        edge = 45.5 if s < 0 else 82.5
        if d != "south":
            edge = 87.0
        for y in (79.2, 81.6):                                                                  # cheek
            x0 = cx + 4.6 * s
            dr.line(S([(x0, y), (edge, y - 0.8)]), fill=INK, width=int(1.0 * K))


EYE_RED = (200, 22, 32, 255)
EYE_WHITE = (246, 240, 236, 255)


def almond(cx, cy, w, h, n=12):
    top = [(cx - w + 2 * w * i / n, cy - h * (1 - (2 * i / n - 1) ** 2)) for i in range(n + 1)]
    bot = [(cx - w + 2 * w * i / n, cy + h * 0.75 * (1 - (2 * i / n - 1) ** 2)) for i in range(n, -1, -1)]
    return top + bot[1:-1]


def eye2(dr, cx, s, cy=76.1):
    """His second eye under the first (her note 2026-11-07: make the mark look like an eye): an almond
    with a white, a red iris and a pupil, outlined in ink, with the outer lash."""
    pts = almond(cx, cy, 3.3, 1.9)
    dr.polygon(S(pts), fill=EYE_WHITE)
    dr.ellipse(S([(cx - 1.35, cy - 1.45), (cx + 1.35, cy + 1.25)]), fill=EYE_RED)
    dr.ellipse(S([(cx - 0.5, cy - 0.6), (cx + 0.5, cy + 0.4)]), fill=INK)
    dr.line(S(pts + [pts[0]]), fill=INK, width=int(0.75 * K), joint="curve")
    dr.line(S(pts[:13]), fill=INK, width=int(1.1 * K), joint="curve")   # heavier upper lid
    dr.line(S([(cx + 2.9 * s, cy - 1.0), (cx + 4.6 * s, cy - 2.2)]), fill=INK, width=int(0.9 * K))


def forehead(dr, d):
    """The mark on his forehead (anime reference, her ask 2026-11-07): a small diamond in the middle
    pointing down, with a stroke on each side that runs out and down from beside its top."""
    if d == "south":
        dr.polygon(S([(64, 52.6), (65.4, 55.8), (64, 59.8), (62.6, 55.8)]), fill=INK)
        for s in (-1, 1):
            dr.line(S([(64 + 2.7 * s, 53.2), (64 + 5.6 * s, 56.6), (64 + 5.3 * s, 60.2)]), fill=INK, width=int(1.1 * K), joint="curve")
    elif d == "east":
        dr.polygon(S([(84.4, 52.6), (85.5, 55.8), (84.6, 59.8), (83.4, 55.8)]), fill=INK)
        dr.line(S([(81.8, 53.2), (79.2, 56.6), (79.5, 60.2)]), fill=INK, width=int(1.1 * K), joint="curve")


BONE = (232, 226, 212, 255)
BONE_DARK = (178, 168, 150, 255)


def king_marks(heads, d):
    """The true form's face markings (her note 2026-11-06): his left side keeps its second eye and
    markings and the grin; his right side is left bare for the mask, which is drawn separately in
    his own skin colour (king_mask, a hediff render node), not as part of this tattoo."""
    if d == "north":
        return draw4(lambda dr: None)
    if d == "east":
        return sukuna_marks(d)
    def f(dr):
        face_extras(dr, d, skip_right=True)
        forehead(dr, d)
        eye2(dr, 72.0 + 1.2, 1)
        mouth = qbez((54.5, 79.6), (64, 89.5), (73.5, 79.6), 24)
        dr.polygon(S(mouth), fill=INK)
        dr.polygon(S(qbez((56.6, 80.6), (64, 85.6), (71.4, 80.6), 24)), fill=(248, 246, 240, 255))
        for x in (60.0, 64.0, 68.0):
            dr.line(S([(x, 81.2), (x, 82.9)]), fill=INK, width=int(0.7 * K))
    return draw4(f)


MASK_LIGHT = (246, 246, 246, 255)     # tinted by his skin in game: near-white = his skin tone
MASK_SHADE = (196, 196, 196, 255)


def king_mask(heads, d):
    """The mask over his right eyes, in grey so the game's skin shader colours it his own skin tone.
    His right side is the viewer's left from the front and the side seen facing west; east and north
    are empty."""
    if d == "south":
        mask = [(45.5, 63.5), (53.0, 60.8), (63.0, 61.2), (63.8, 69.0), (61.5, 75.0), (56.0, 78.2), (50.5, 76.0), (45.5, 71.0)]
    elif d == "west":
        mask = [(41.0, 63.5), (49.0, 60.8), (57.5, 61.5), (58.0, 69.0), (55.0, 75.5), (49.0, 78.2), (42.5, 74.0)]
    else:
        return draw4(lambda dr: None)
    m = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    md = ImageDraw.Draw(m)
    md.polygon(S(mask), fill=MASK_LIGHT)
    md.line(S(mask + [mask[0]]), fill=INK, width=int(1.1 * K), joint="curve")
    mx = sum(p[0] for p in mask) / len(mask)
    x0 = min(p[0] for p in mask); x1 = max(p[0] for p in mask)
    # the eyes carved on it (her ask 2026-11-07), where his two right eyes are under it; ringed irises
    # like the true form's eyes in the manga
    ex = 55.0 - 1.2 if d == "south" else 49.5
    for ey, w, h in ((69.4, 3.6, 2.1), (76.0, 3.2, 1.8)):
        pts = almond(ex, ey, w, h)
        md.line(S(pts + [pts[0]]), fill=INK, width=int(0.8 * K), joint="curve")
        md.ellipse(S([(ex - 1.3, ey - 1.3), (ex + 1.3, ey + 1.1)]), outline=INK, width=int(0.6 * K))
        md.ellipse(S([(ex - 0.4, ey - 0.5), (ex + 0.4, ey + 0.3)]), fill=INK)
    hm = head_mask(heads, "east" if d == "west" else d, grow=0)
    if d == "west":
        hm = hm.transpose(Image.FLIP_LEFT_RIGHT)
    m.putalpha(ImageChops.multiply(m.getchannel("A"), hm))
    return m.resize((128, 128), Image.LANCZOS)


def sukuna_marks(d):
    """Sukuna in control: a second pair of eyes open under the first and a wide grin."""
    def f(dr):
        if d == "north":
            return
        eyes = [(55.0, -1), (72.0, 1)] if d == "south" else [(78.0, 1)]
        face_extras(dr, d)
        forehead(dr, d)
        for cx, s in eyes:
            eye2(dr, cx + 1.2 * s, s)
        if d == "south":
            mouth = qbez((54.5, 79.6), (64, 89.5), (73.5, 79.6), 24)
            dr.polygon(S(mouth), fill=INK)
            dr.polygon(S(qbez((56.6, 80.6), (64, 85.6), (71.4, 80.6), 24)), fill=(248, 246, 240, 255))
            for x in (60.0, 64.0, 68.0):   # gaps between the teeth
                dr.line(S([(x, 81.2), (x, 82.9)]), fill=INK, width=int(0.7 * K))
        else:
            mouth = qbez((73.5, 80.0), (81.0, 88.5), (86.5, 79.0), 24)
            dr.polygon(S(mouth), fill=INK)
            dr.polygon(S(qbez((75.6, 80.9), (81.0, 85.2), (85.3, 80.0), 24)), fill=(248, 246, 240, 255))
            for x in (79.0, 82.5):
                dr.line(S([(x, 81.4), (x, 82.9)]), fill=INK, width=int(0.7 * K))
    return draw4(f)



def second_eyes(d):
    """Only his second pair of eyes (her note 2026-11-07: with Facial Animation he showed just two):
    drawn on a layer above Facial Animation's face, which covers the face tattoo. Same eyes as
    sukuna_marks, nothing else (FA draws its own mouth)."""
    def f(dr):
        if d == "north":
            return
        eyes = [(55.0, -1), (72.0, 1)] if d == "south" else [(78.0, 1)]
        forehead(dr, d)   # FA's face covers the tattoo, so the forehead mark rides on this layer too
        for cx, s in eyes:
            eye2(dr, cx + 1.2 * s, s)
    return draw4(f)


def main():
    heads, out = Path(sys.argv[1]), Path(sys.argv[2])
    prev = Path(sys.argv[3]) if len(sys.argv) > 3 else None
    hairs = out / "Things/Pawn/Humanlike/Hairs"
    tats = out / "Things/Pawn/Humanlike/Tattoos"
    for d in DIRS:
        # Yuji's, the King's and the vessel's hair are drawn by art_hair3.py since 2026-11-07
        # Modulo Yuji's hair too, since 2026-11-07
        (tats / "Sukuna_YujiMarks").mkdir(parents=True, exist_ok=True)
        yuji_marks(d).save(tats / f"Sukuna_YujiMarks/Sukuna_YujiMarks_{d}.png")
        sukuna_marks(d).save(tats / f"Sukuna_Markings/Sukuna_Markings_{d}.png")
        (out / "Things/Pawn/Humanlike/Sukuna_FAEyes").mkdir(parents=True, exist_ok=True)
        second_eyes(d).save(out / f"Things/Pawn/Humanlike/Sukuna_FAEyes/Sukuna_FAEyes_{d}.png")
    (tats / "Sukuna_KingMarkings").mkdir(parents=True, exist_ok=True)
    masks = out / "Things/Pawn/Humanlike/Sukuna_Mask"
    masks.mkdir(parents=True, exist_ok=True)
    for d in ("south", "east", "north"):
        king_marks(heads, d).save(tats / f"Sukuna_KingMarkings/Sukuna_KingMarkings_{d}.png")
    for d in ("south", "east", "west", "north"):
        king_mask(heads, d).save(masks / f"Sukuna_Mask_{d}.png")


if __name__ == "__main__":
    main()
