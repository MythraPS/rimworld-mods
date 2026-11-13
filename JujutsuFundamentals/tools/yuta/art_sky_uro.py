"""Takako Uro's look (her calls 2026-11-11): long pink hair (fan sources agree on long pink hair) and black
sclera with pink irises. Hair in the mod's hair style (tools/sukuna/art_hair3.py: separate locks, white fill
tinted by hair colour in game). Head geometry: vanilla female head silhouettes, taken from Vanilla Races
Expanded - Phytokin's BarkSkinFemale_Average_Normal_* (same outline as vanilla), passed as HEADS_DIR with the
files renamed Male_Average_Normal_{south,east,north}.png (the helper's file name).
Eyes: Gojo's Six Eyes pictures (Biotech's eye-gene shapes), iris recoloured pink, ringed in black.

  python3 tools/yuta/art_sky_uro.py HEADS_DIR [PREVIEW_DIR]
"""
import math, random, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

TOOLS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(TOOLS / "sukuna"))
from art_hair2 import K, N, DIRS, head_mask, above, inter  # noqa: E402
from art_hair3 import Lock, jitter, render  # noqa: E402

MOD = TOOLS.parent
HAIRS = MOD / "Textures/Things/Yuta/Sky/Hair"
EYES = MOD / "Textures/Things/Yuta/Sky/UroEyes"
SIX = MOD / "Textures/Things/Pawn/Humanlike/HeadAttachments/Gojo_SixEyes"
PINK_DARK, PINK_LIGHT = (200, 60, 120), (255, 160, 205)


def uro(heads, d, seed=89):
    """Long, straight, past the shoulders, with a fringe that stops above the eyes."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        # fringe: down to row 60 in the middle (eyes are at 67-73), lower at the temples
        cap = inter(hm, above(lambda x: 58 + abs(x - 64) * 0.12 if 44 <= x <= 84 else 74))
        t = [(43, 52, 9, 38, 112, -0.05), (45, 57, 7, 41, 116, 0.05), (41, 62, 7, 36, 104, -0.1),   # left side, framing the face
             (85, 52, 9, 90, 112, 0.05), (83, 57, 7, 87, 116, -0.05), (87, 62, 7, 92, 104, 0.1),    # right side
             (56, 46, 7, 54, 62, 0.1), (64, 45, 7, 64, 63, 0.0), (72, 46, 7, 74, 62, -0.1)]          # fringe locks
    elif d == "east":
        cap = inter(hm, above(lambda x: 60 if x >= 68 else min(84, 60 + (68 - x) * 1.2)))
        t = [(50, 48, 11, 36, 116, 0.05), (46, 54, 10, 32, 112, 0.0), (56, 46, 10, 44, 118, 0.08),  # down the back
             (44, 62, 8, 34, 104, -0.05), (74, 46, 7, 82, 62, 0.15)]                               # back locks, fringe
    else:
        cap = inter(hm, above(lambda x: 96))
        t = [(52, 50, 12, 46, 118, -0.05), (64, 50, 12, 64, 120, 0.0), (76, 50, 12, 82, 118, 0.05),
             (44, 56, 9, 40, 110, -0.1), (84, 56, 9, 88, 110, 0.1), (58, 52, 10, 56, 119, 0.02), (70, 52, 10, 72, 119, -0.02)]
    return render(cap, jitter([Lock(*x) for x in t], rnd, 0.5))


def eyes(src):
    """Iris pink (dark to light as the blue was), and a black ring one pixel out: black sclera."""
    im = Image.open(src).convert("RGBA")
    W, H = im.size
    out = im.copy()
    blues = [im.getpixel((x, y))[2] for y in range(H) for x in range(W)
             if im.getpixel((x, y))[3] > 0 and im.getpixel((x, y))[2] > im.getpixel((x, y))[0] + 30]
    lo, hi = min(blues), max(blues)
    iris = set()
    for y in range(H):
        for x in range(W):
            r, g, b, a = im.getpixel((x, y))
            if a and b > r + 30:
                t = (b - lo) / max(1, hi - lo)
                out.putpixel((x, y), tuple(round(PINK_DARK[i] + (PINK_LIGHT[i] - PINK_DARK[i]) * t) for i in range(3)) + (a,))
                iris.add((x, y))
    # the iris's outer pixel ring turns black: the dark around it reads as black sclera
    for (x, y) in iris:
        if any((x + dx, y + dy) not in iris for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            out.putpixel((x, y), (8, 6, 8, 255))
    a = out.getchannel("A").filter(ImageFilter.MaxFilter(3))
    ring = Image.new("RGBA", (W, H), (8, 6, 8, 255))
    ring.putalpha(a)
    ring.alpha_composite(out)
    return ring


def main():
    heads = Path(sys.argv[1])
    HAIRS.mkdir(parents=True, exist_ok=True)
    for d in DIRS:
        uro(heads, d).save(HAIRS / f"JJK_UroHair_{d}.png")
    (EYES / "Male").mkdir(parents=True, exist_ok=True); (EYES / "Female").mkdir(parents=True, exist_ok=True)
    eyes(SIX / "Male/Gojo_SixEyes_Male.png").save(EYES / "Male/JJK_UroEyes_Male.png")
    eyes(SIX / "Female/Gojo_SixEyes_Female_east.png").save(EYES / "Female/JJK_UroEyes_Female_east.png")
    if len(sys.argv) > 2:   # preview: hair tinted pink over the head, x3
        prev = Path(sys.argv[2]); prev.mkdir(parents=True, exist_ok=True)
        row = Image.new("RGBA", (128 * 3, 128), (60, 60, 64, 255))
        for i, d in enumerate(DIRS):
            head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA")
            hair = Image.open(HAIRS / f"JJK_UroHair_{d}.png").convert("RGBA").resize((128, 128), Image.LANCZOS)
            tint = Image.new("RGBA", hair.size, (237, 140, 184, 255))
            hair = Image.composite(Image.blend(hair, tint, 0.0), hair, hair.getchannel("A"))
            r, g, b, a = hair.split()
            hair = Image.merge("RGBA", (r.point(lambda v: v * 237 // 255), g.point(lambda v: v * 140 // 255), b.point(lambda v: v * 184 // 255), a))
            tile = Image.new("RGBA", (128, 128), (60, 60, 64, 255)); tile.alpha_composite(head); tile.alpha_composite(hair)
            row.paste(tile, (128 * i, 0))
        row.resize((128 * 9, 128 * 3), Image.NEAREST).save(prev / "uro_preview.png")
    print("ok")


if __name__ == "__main__":
    main()
