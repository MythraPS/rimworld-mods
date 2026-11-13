"""Hair and face marks for the core mod's characters (her ask 2026-11-10), in the Sukuna mod's
style (tools/art_hair3.py: separate locks, white fill tinted by hair colour in game, dark outline).
Kusakabe and Miwa use vanilla hair (Rockstar, Long); nothing vanilla fits these two:
  Todo  shoulder-length hair tied back in a spiky ponytail; scar from the left hairline to the cheek
  Ryu   a tall pompadour
Head geometry is vanilla Male_Average_Normal (see art_hair2.py).

  python tools/art_characters.py HEADS_DIR [PREVIEW_DIR]
HEADS_DIR holds Male_Average_Normal_{south,east,north}.png (vanilla, extracted; not shipped).
"""
import math, random, sys
from pathlib import Path
from PIL import Image, ImageDraw

SUKUNA_TOOLS = Path(__file__).resolve().parent / "sukuna"   # Sukuna's hair helpers, merged in 2026-11-10
sys.path.insert(0, str(SUKUNA_TOOLS))
from art_hair2 import K, N, DIRS, head_mask, above, inter, qbez, draw4, S  # noqa: E402
from art_hair3 import Lock, jitter, render  # noqa: E402

MOD = Path(__file__).resolve().parent.parent
HAIRS = MOD / "Textures/Things/Pawn/Humanlike/Hairs"
TATS = MOD / "Textures/Things/Pawn/Humanlike/Tattoos"
SCAR = (120, 52, 48, 235)


def todo(heads, d, seed=41):
    """Slicked back over the top, gathered at the back into a short spiky ponytail."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, above(lambda x: 47 + 2.5 * math.cos((x - 64) / 23 * math.pi / 2) ** 2 if 41 <= x <= 87 else 62))
        # ponytail spikes showing above the crown, behind the head
        t = [(58, 42, 8, 46, 24, -0.2), (62, 41, 8, 56, 19, -0.1), (66, 41, 8, 70, 18, 0.1), (70, 42, 8, 81, 23, 0.2),
             (60, 44, 7, 50, 30, -0.3), (68, 44, 7, 78, 29, 0.3)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 46 if x >= 62 else 46 + (62 - x) * 1.0))
        # the ponytail sticks out behind (to the left), spiky
        t = [(46, 48, 9, 24, 36, -0.2), (46, 51, 9, 22, 47, 0.0), (47, 54, 8, 26, 58, 0.2), (49, 46, 8, 31, 30, -0.3),
             (48, 57, 7, 33, 66, 0.3)]
    else:
        cap = inter(hm, above(lambda x: 70))
        t = [(64, 50, 9, 50, 30, -0.2), (64, 50, 9, 64, 26, 0.0), (64, 50, 9, 78, 30, 0.2), (64, 52, 8, 46, 42, -0.3),
             (64, 52, 8, 82, 42, 0.3), (64, 55, 8, 56, 70, 0.1), (64, 55, 8, 72, 70, -0.1)]
    return render(cap, jitter([Lock(*x) for x in t], rnd, 0.6))


def blob(pts):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon([(x * K, y * K) for x, y in pts], fill=255)
    return m


def ryu(heads, d, seed=53):
    """Short at the sides; a tall pompadour: one smooth rounded mass swept up (and forward, seen from
    the side) over the forehead, with a few short locks for the strands."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, above(lambda x: 50 if 45 <= x <= 83 else 60))
        mass = blob(qbez((44, 50), (40, 14), (64, 12), 16) + qbez((64, 12), (88, 14), (84, 50), 16))
        t = [(56, 46, 9, 54, 22, -0.2), (64, 46, 9, 64, 18, 0.0), (72, 46, 9, 74, 22, 0.2)]
    elif d == "east":
        cap = inter(hm, above(lambda x: 48 if x >= 58 else 48 + (58 - x) * 0.5))
        # up from the crown, then rolling forward over the forehead (to the right)
        mass = blob(qbez((46, 50), (44, 16), (76, 14), 16) + qbez((76, 14), (100, 14), (98, 30), 12)
                    + qbez((98, 30), (96, 42), (80, 46), 12))
        t = [(56, 44, 8, 82, 22, 0.25), (62, 46, 8, 92, 28, 0.2)]
    else:
        cap = inter(hm, above(lambda x: 64))
        mass = blob(qbez((46, 50), (42, 18), (64, 16), 16) + qbez((64, 16), (86, 18), (82, 50), 16))
        t = [(58, 46, 9, 56, 24, -0.15), (70, 46, 9, 72, 24, 0.15)]
    from art_hair2 import union
    return render(union(cap, mass), jitter([Lock(*x) for x in t], rnd, 0.4))


def todo_scar(d):
    """Down the left side of his face (his left: the right of the picture from the front), hairline to cheek."""
    def f(dr):
        if d == "north":
            return
        if d == "south":
            pts = qbez((77.5, 49), (79.5, 66), (77.5, 84), 14)
        elif d == "west":
            pts = qbez((52, 49), (49.5, 66), (52, 83), 14)   # facing west, his left side is the one we see
        else:
            pts = []   # facing east we see his right side
        if pts:
            dr.line(S(pts), fill=SCAR, width=int(1.6 * K), joint="curve")
            x0 = pts[0][0]
            for y in (56, 63, 70, 77):
                x = x0 + (2 if d == "south" else -2.5) * math.sin((y - 49) / 35 * math.pi)
                dr.line(S([(x - 1.4, y - 0.6), (x + 1.4, y + 0.6)]), fill=SCAR, width=int(0.7 * K))
    return draw4(f)


def toji_scar(d):
    """A short scar across the right corner of his mouth (his right: the left of the picture from the front, and
    the side we see facing east). Facing west the other cheek shows, so that texture is empty."""
    def f(dr):
        # vanilla eyes are rows 67-73 (measured 2026-11-11); the mouth line is about row 83
        if d == "south":
            pts = [(59.5, 78), (58, 83), (56.5, 88)]
        elif d == "east":
            pts = [(86, 78), (84.5, 83), (83, 88)]
        else:
            return
        dr.line(S(pts), fill=SCAR, width=int(1.4 * K), joint="curve")
    return draw4(f)


def choso(heads, d, seed=67):
    """Black hair: a close cap with a few strands over the forehead, and two short spiky ponytails tied high on
    either side of the head, sticking up and out (canon: "two spiky ponytails")."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        # parted in the middle: high at the part, sweeping down to the temples
        cap = inter(hm, above(lambda x: 46 + abs(x - 64) * 0.42 if 42 <= x <= 86 else 62))
        t = [(48, 44, 8, 30, 24, -0.25), (49, 42, 8, 36, 18, -0.1), (50, 45, 7, 28, 34, -0.35),   # left tail
             (80, 44, 8, 98, 24, 0.25), (79, 42, 8, 92, 18, 0.1), (78, 45, 7, 100, 34, 0.35),     # right tail
             (60, 46, 5, 54, 58, 0.15), (68, 46, 5, 74, 58, -0.15)]                               # strands either side of the part
    elif d == "east":
        cap = inter(hm, above(lambda x: 47 + max(0, x - 70) * 0.5 if x >= 60 else 47 + (60 - x) * 0.9))
        t = [(52, 42, 8, 34, 24, -0.25), (53, 40, 8, 40, 18, -0.1), (51, 44, 7, 30, 34, -0.35),   # the near tail
             (60, 38, 6, 62, 22, 0.05), (82, 46, 6, 86, 56, 0.1)]                                 # far tail's tips, a brow strand
    else:
        cap = inter(hm, above(lambda x: 72))
        t = [(48, 44, 8, 30, 24, -0.25), (49, 42, 8, 36, 18, -0.1), (50, 45, 7, 28, 34, -0.35),
             (80, 44, 8, 98, 24, 0.25), (79, 42, 8, 92, 18, 0.1), (78, 45, 7, 100, 34, 0.35)]
    return render(cap, jitter([Lock(*x) for x in t], rnd, 0.6))


def main():
    heads = Path(sys.argv[1])
    HAIRS.mkdir(parents=True, exist_ok=True)
    (TATS / "JJK_TodoScar").mkdir(parents=True, exist_ok=True)
    for d in DIRS:
        todo(heads, d).save(HAIRS / f"JJK_TodoHair_{d}.png")
        ryu(heads, d).save(HAIRS / f"JJK_RyuHair_{d}.png")
        choso(heads, d).save(HAIRS / f"Choso_Hair_{d}.png")
        todo_scar(d).save(TATS / "JJK_TodoScar" / f"JJK_TodoScar_{d}.png")
    todo_scar("west").save(TATS / "JJK_TodoScar" / "JJK_TodoScar_west.png")
    (TATS / "JJK_TojiScar").mkdir(parents=True, exist_ok=True)
    for d in DIRS + ("west",):
        toji_scar(d).save(TATS / "JJK_TojiScar" / f"JJK_TojiScar_{d}.png")
    print("ok")


if __name__ == "__main__":
    main()
