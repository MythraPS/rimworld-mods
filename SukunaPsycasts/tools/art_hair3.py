"""Third pass at the character hair (her call 2026-11-07, after comparing with the "Jujutsu Colony
Expansion" mod's hand-drawn hair): built from separate locks of different lengths with a little
randomness (seeded, so it redraws the same), layered back to front, each front lock edged with a
thin dark line so the locks read apart, and an inner stroke down each lock. White, so the game
tints it with the pawn's hair colour. Sizes match the passes she approved: Yuji short (2026-11-06),
Sukuna's not too long, Gojo's tips rounded and the back of his head even (2026-10-28).
Modulo Yuji's neat hair stays as art_hair2.py draws it.

  python tools/art_hair3.py HEADS_DIR SUKUNA_TEXTURES GOJO_TEXTURES [PREVIEW.png]
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

sys.path.insert(0, str(Path(__file__).parent))
from art_hair2 import K, N, DIRS, head_mask, above, union, inter, qbez  # noqa: E402


class Lock:
    def __init__(self, bx, by, w, tx, ty, bend):
        self.bx, self.by, self.w, self.tx, self.ty, self.bend = bx, by, w, tx, ty, bend
        dx, dy = tx - bx, ty - by
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        a = (bx + nx * w / 2, by + ny * w / 2)
        b = (bx - nx * w / 2, by - ny * w / 2)
        mx, my = bx + dx * 0.55 + nx * bend * L * 0.25, by + dy * 0.55 + ny * bend * L * 0.25
        self.mid = (mx, my)
        ca = (mx + nx * w * 0.34, my + ny * w * 0.34)
        cb = (mx - nx * w * 0.34, my - ny * w * 0.34)
        pts = qbez(a, ca, (tx, ty)) + qbez((tx, ty), cb, b)[1:]
        m = Image.new("L", (N, N), 0)
        ImageDraw.Draw(m).polygon([(x * K, y * K) for x, y in pts], fill=255)
        # round the tip a touch (her note on Gojo: not sharp)
        self.mask = m.filter(ImageFilter.GaussianBlur(0.8 * K)).point(lambda v: 255 if v >= 128 else 0)

    def spine(self, a=0.22, b=0.78):
        pts = qbez((self.bx, self.by), self.mid, (self.tx, self.ty), 20)
        return pts[int(a * 20):int(b * 20) + 1]


def short(bx, by, w, tx, ty, bend, f):
    return Lock(bx, by, w, bx + (tx - bx) * f, by + (ty - by) * f, bend)


def jitter(locks, rnd, amount=1.0):
    """Hand-drawn unevenness: each tip moves a little and each lock is a touch longer or shorter."""
    out = []
    for l in locks:
        k = 1 + rnd.uniform(-0.12, 0.12) * amount
        out.append(Lock(l.bx, l.by, l.w * (1 + rnd.uniform(-0.08, 0.08) * amount),
                        l.bx + (l.tx - l.bx) * k + rnd.uniform(-1.5, 1.5) * amount,
                        l.by + (l.ty - l.by) * k, l.bend + rnd.uniform(-0.12, 0.12) * amount))
    return out


def render(cap, locks, outline=2.6, lo=196):
    """Vanilla shading (darker toward the outline), then ink: a dark line running down into the hair
    from each gap between neighbouring locks, and a light strand down each lock."""
    full = union(cap, *[l.mask for l in locks])
    e = full.filter(ImageFilter.GaussianBlur(5 * K))
    shade = e.point(lambda v: int(lo + (252 - lo) * min(1, v / 200)))
    base = Image.merge("RGB", [shade] * 3)
    ink = Image.new("L", (N, N), 0)
    di = ImageDraw.Draw(ink)
    up = sorted([l for l in locks if l.ty < l.by], key=lambda l: l.bx)
    for k, (a, b) in enumerate(zip(up, up[1:])):
        if b.bx - a.bx > 14 or k % 2:   # every other gap only (her note 2026-11-07: fewer lines)
            continue
        vx, vy = (a.bx + b.bx) / 2, min(a.by, b.by) - 3
        cx = vx + (64 - vx) * 0.12
        pts = qbez((vx, vy), (cx, vy + 4), (cx + (64 - vx) * 0.08, vy + 9), 10)
        di.line([(x * K, y * K) for x, y in pts], fill=255, width=int(1.0 * K), joint="curve")
    inside = full.filter(ImageFilter.MinFilter(int(1.2 * K) | 1))
    base.paste((52, 50, 56), mask=inter(ink, inside))
    dr = ImageDraw.Draw(base)
    srnd = random.Random(len(locks))
    for l in locks:
        a = srnd.uniform(0.12, 0.3)
        dr.line([(x * K, y * K) for x, y in l.spine(a, a + srnd.uniform(0.3, 0.5))], fill=(168, 168, 172), width=int(0.6 * K), joint="curve")
    r = int(outline * K)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    img.paste((0, 0, 0, 255), mask=full.filter(ImageFilter.MaxFilter(2 * r + 1)))
    img.paste(base, mask=full)
    return img.resize((128, 128), Image.LANCZOS)


# ------------------------------------------------------------------ styles (outlines as approved)
YUJI_LENGTH, KING_LENGTH = 0.5, 0.47   # Yuji 0.6 -> 0.5 (her note 2026-11-07: a bit smaller)   # King 0.55 -> 0.47 (her note 2026-11-07: a bit smaller)
KING_TIP_IN = 0.85   # tips pulled toward the middle so the fan is narrower


def yuji(heads, d, L=YUJI_LENGTH, seed=11):
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        line = lambda x: 49 + 3.5 * math.cos((x - 64) / 23 * math.pi / 2) ** 2 if 41 <= x <= 87 else 46
        cap = inter(hm, above(lambda x: max(line(x), 58 if (x < 45 or x > 83) else 0)))
        t = [(46, 47, 9, 37, 33, -0.4), (53, 42, 10, 49, 25, -0.3), (60, 40, 10, 58, 21, -0.15), (67, 40, 10, 70, 21, 0.15),
             (74, 42, 10, 80, 25, 0.3), (81, 47, 9, 91, 33, 0.4), (56, 47, 9, 54, 57, 0.3), (64, 48, 9, 64, 58, 0.0),
             (72, 47, 9, 74, 57, -0.3), (44, 52, 6, 42, 60, 0.2), (84, 52, 6, 86, 60, -0.2)]
        locks = jitter([short(*x, L) for x in t], rnd)
        return render(cap, [Lock(l.bx, l.by, l.w, 64 + (l.tx - 64) * KING_TIP_IN, l.ty, l.bend) for l in locks])
    elif d == "east":
        cap = inter(hm, above(lambda x: 50 if x >= 66 else 50 + (66 - x) * 0.6))
        t = [(44, 50, 9, 33, 38, -0.3), (50, 44, 10, 41, 28, -0.25), (57, 41, 10, 53, 22, -0.1), (64, 40, 10, 66, 21, 0.1),
             (71, 41, 10, 77, 24, 0.2), (78, 44, 9, 88, 31, 0.3), (80, 48, 8, 88, 57, -0.2), (73, 48, 8, 77, 57, -0.1)]
    else:
        cap = inter(hm, above(lambda x: 66))
        t = [(46, 48, 9, 37, 33, -0.4), (53, 43, 10, 49, 26, -0.3), (60, 41, 10, 58, 22, -0.15), (67, 41, 10, 70, 22, 0.15),
             (74, 43, 10, 80, 26, 0.3), (81, 48, 9, 91, 33, 0.4), (52, 64, 8, 50, 71, 0.2), (60, 65, 8, 59, 72, 0),
             (68, 65, 8, 69, 72, 0), (76, 64, 8, 78, 71, -0.2)]
    return render(cap, jitter([short(*x, L) for x in t], rnd))


def sukuna(heads, d, f=KING_LENGTH, seed=23):
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        line = lambda x: 45 + 2.5 * math.cos((x - 64) / 23 * math.pi / 2) ** 2 if 41 <= x <= 87 else 44
        cap = inter(hm, above(lambda x: max(line(x), 57 if (x < 44 or x > 84) else 0)))
        t = [(46, 46, 9, 33, 28, -0.35), (52, 41, 10, 44, 18, -0.25), (59, 39, 10, 55, 12, -0.1), (66, 39, 10, 71, 12, 0.1),
             (73, 41, 10, 83, 18, 0.25), (80, 46, 9, 94, 28, 0.35), (63, 41, 8, 63, 16, 0.0)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 46 if x >= 68 else 46 + (68 - x) * 0.6))
        t = [(46, 48, 9, 30, 36, -0.2), (52, 43, 10, 36, 25, -0.2), (59, 40, 10, 47, 16, -0.15), (66, 39, 10, 60, 13, -0.1),
             (73, 40, 10, 73, 14, 0.05), (80, 43, 9, 84, 22, 0.1)]
    else:
        cap = inter(hm, above(lambda x: 64))
        t = [(46, 47, 9, 33, 29, -0.35), (52, 42, 10, 44, 19, -0.25), (59, 40, 10, 55, 13, -0.1), (66, 40, 10, 71, 13, 0.1),
             (73, 42, 10, 83, 19, 0.25), (80, 47, 9, 94, 29, 0.35)]
    locks = jitter([short(*x, f) for x in t], rnd)
    if d != "east":   # tips only, toward the middle of the face
        locks = [Lock(l.bx, l.by, l.w, 64 + (l.tx - 64) * KING_TIP_IN, l.ty, l.bend) for l in locks]
    return render(cap, locks)


GOJO_TOP = 28   # highest tip (y); her notes 2026-11-07: keep it short
GOJO_LEAN = 4.0  # combed-over sweep, pixels toward his left


def gojo(heads, d, seed=5):
    """Third redo (her note 2026-11-07): not a crown. Short hair combed up and a little to one side,
    messed up by fighting. Locks stand nearly upright from the skull and stay inside the head's
    outline; the earlier passes fanned each tip outward, which made the hair wider than the skull
    and a semicircle."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d, grow=0.0).filter(ImageFilter.MinFilter(int(2 * K) | 1))
    def row(xs, w, skull, top_mid, drop, cx):
        out = []
        half = (xs[-1] - xs[0]) / 2
        for x in xs:
            u = (x - cx) / half                       # -1 .. 1 across the head
            by = skull(x)
            ty = top_mid + drop * u * u               # a little lower at the sides, not a dome
            tx = x - u * 6.0 + GOJO_LEAN              # sides tip in to follow the skull, all swept one way
            out.append(Lock(x, by, w, tx, ty, 0.25))   # every lock curls the same way: combed
        return out
    if d == "south":
        cap = inter(hm, above(lambda x: 55))
        skull = lambda x: 44 + 10 * ((x - 64) / 23) ** 2
        t = row([48 + i * 32 / 5 for i in range(6)], 11, skull, GOJO_TOP, 10, 64)
        t += row([55, 66, 76], 10, lambda x: 53, 44, 3, 66)   # front locks over the hairline
        return render(cap, jitter(t, rnd, 0.8))
    if d == "east":
        cap = inter(hm, above(lambda x: 55 if x >= 62 else 55 + (62 - x) * 0.3))
        skull = lambda x: 44 + 10 * ((x - 62) / 22) ** 2
        t = row([45 + i * 36 / 5 for i in range(6)], 11, skull, GOJO_TOP, 10, 63)
        return render(cap, jitter(t, rnd, 0.8))
    cap = inter(hm, above(lambda x: 64))
    skull = lambda x: 44 + 10 * ((x - 64) / 23) ** 2
    t = row([48 + i * 32 / 5 for i in range(6)], 11, skull, GOJO_TOP, 10, 64)
    return render(cap, t)


MODULO_NARROW = 0.8    # her note 2026-11-07 (second time): still too wide; locks pulled in toward the middle
MODULO_INSET = 2.0     # pixels the cap sits inside the head outline


def squeeze(locks, k, cx=64.0):
    return [Lock(cx + (l.bx - cx) * k, l.by, l.w * (0.5 + 0.5 * k), cx + (l.tx - cx) * k, l.ty, l.bend) for l in locks]


def modulo(heads, d, seed=31):
    """Modulo Yuji, third pass (her note 2026-11-07: much neater). Short, smooth hair close to the skull
    with a little height on top, and a tidy fringe swept toward his left. No tufts, no randomness."""
    hm = head_mask(heads, d, grow=0.0)
    if d != "east": hm = hm.filter(ImageFilter.MinFilter(int(2 * MODULO_INSET * K) | 1))   # inside the skull's outline
    lift = ImageChops.offset(hm, 0, -int(2.5 * K))          # a little volume on top
    dome = union(hm, inter(lift, above(lambda x: 52)))
    if d == "south":
        cap = inter(dome, above(lambda x: 48 + (x - 41) * 0.1 if 41 <= x <= 87 else 57))
        fringe = [Lock(50, 45, 11, 56, 55, 0.2), Lock(59, 45, 11, 66, 54, 0.18), Lock(68, 46, 10, 75, 53, 0.15)]
    elif d == "east":
        cap = inter(dome, above(lambda x: 49 if x >= 64 else 49 + (64 - x) * 0.5))
        fringe = [Lock(72, 45, 10, 80, 54, 0.15)]
    else:
        cap = inter(dome, above(lambda x: 64))
        fringe = []
    return render(cap, fringe)


def main():
    heads, sk, gj = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    sh, gh = sk / "Things/Pawn/Humanlike/Hairs", gj / "Things/Pawn/Humanlike/Hairs"
    made = {}
    for d in DIRS:
        made["Sukuna_YujiHair", d] = yuji(heads, d)
        made["Sukuna_KingHair", d] = sukuna(heads, d, KING_LENGTH)
        made["Sukuna_VesselHair", d] = sukuna(heads, d, 0.4)
        made["Gojo_Hair", d] = gojo(heads, d)
        made["Sukuna_ModuloHair", d] = modulo(heads, d)
    if len(sys.argv) > 4 and sys.argv[4].endswith(".png"):
        prev = Path(sys.argv[4])
        names = ["Sukuna_YujiHair", "Sukuna_KingHair", "Sukuna_VesselHair", "Gojo_Hair", "Sukuna_ModuloHair"]
        col = {"Sukuna_YujiHair": (240, 150, 170), "Sukuna_KingHair": (240, 150, 170), "Sukuna_VesselHair": (240, 150, 170), "Gojo_Hair": (245, 247, 255), "Sukuna_ModuloHair": (240, 240, 245)}
        def tint(img, rgb):
            r, g, b, a = img.split()
            return Image.merge("RGBA", [x.point(lambda v, k=k: v * k // 255) for x, k in zip((r, g, b), rgb)] + [a])
        sheet = Image.new("RGBA", (128 * 6, 128 * len(names)), (110, 110, 110, 255))
        for j, n in enumerate(names):
            for i, d in enumerate(DIRS):
                head = tint(Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA"), (236, 206, 180))
                old = Image.open((gh if n.startswith("Gojo") else sh) / f"{n}_{d}.png").convert("RGBA")
                sheet.alpha_composite(Image.alpha_composite(head, tint(old, col[n])), (i * 128, j * 128))
                sheet.alpha_composite(Image.alpha_composite(head, tint(made[n, d], col[n])), ((i + 3) * 128, j * 128))
        sheet.resize((128 * 6 * 2, 128 * len(names) * 2), Image.NEAREST).save(prev)
        if "--write" not in sys.argv:
            return
    for (n, d), img in made.items():
        img.save((gh if n.startswith("Gojo") else sh) / f"{n}_{d}.png")


if __name__ == "__main__":
    main()
