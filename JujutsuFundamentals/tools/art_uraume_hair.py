"""Uraume's hair (her ask 2026-11-11): a chin-length white bob with bangs and a red streak (her words: "that red
streak in his hair"). Same style as tools/art_characters.py (white fill tinted by hair colour in game, dark outline);
the streak is drawn already red, so with Uraume's near-white hair colour it stays red in game.

  python tools/art_uraume_hair.py HEADS_DIR [PREVIEW.png]
HEADS_DIR holds Male_Average_Normal_{south,east,north}.png (vanilla, extracted; not shipped)."""
import random, sys
from pathlib import Path
from PIL import Image, ImageChops

sys.path.insert(0, str(Path(__file__).resolve().parent / "sukuna"))
from art_hair2 import K, N, DIRS, head_mask, above, inter  # noqa: E402
from art_hair3 import Lock, jitter, render  # noqa: E402

MOD = Path(__file__).resolve().parent.parent
HAIRS = MOD / "Textures/Things/Pawn/Humanlike/Hairs"
RED = (200, 34, 40)


def uraume(heads, d, seed=97):
    """Returns (hair image, streak lock)."""
    rnd = random.Random(seed + DIRS.index(d))
    hm = head_mask(heads, d)
    if d == "south":
        cap = inter(hm, above(lambda x: 56))
        t = [(44, 46, 11, 40, 86, 0.05), (47, 50, 10, 43, 84, 0.0),          # left side, to the chin
             (84, 46, 11, 88, 86, -0.05), (81, 50, 10, 85, 84, 0.0),         # right side
             (52, 40, 9, 50, 60, 0.1), (60, 38, 9, 58, 62, 0.05), (68, 38, 9, 70, 62, -0.05), (76, 40, 9, 78, 60, -0.1)]   # bangs
        streak = Lock(72, 39, 8, 75, 64, -0.08)
    elif d == "east":
        cap = inter(hm, above(lambda x: 56 if x >= 60 else 56 + (60 - x) * 0.5))
        t = [(48, 44, 13, 44, 86, 0.05), (56, 44, 12, 54, 86, 0.0), (64, 42, 10, 64, 80, -0.05),   # back and side
             (74, 40, 9, 82, 60, -0.1), (80, 42, 8, 88, 58, -0.15)]                                 # bangs, forward
        streak = Lock(76, 40, 7, 86, 62, -0.12)
    else:
        cap = inter(hm, above(lambda x: 84))
        t = [(46, 44, 12, 42, 86, 0.05), (56, 42, 12, 54, 88, 0.0), (64, 42, 12, 64, 88, 0.0),
             (72, 42, 12, 74, 88, 0.0), (82, 44, 12, 86, 86, -0.05)]
        streak = Lock(52, 42, 8, 50, 86, 0.04)          # the same streak, seen from behind (his right side)
    locks = jitter([Lock(*x) for x in t], rnd, 0.5) + [streak]
    return render(cap, locks), streak


def tint(img, streak):
    """Turns the streak lock red, keeping its shading and outline."""
    m = streak.mask.resize((128, 128), Image.LANCZOS).point(lambda v: 255 if v > 100 else 0)
    r, g, b, a = img.split()
    lum = r
    red = Image.merge("RGB", [lum.point(lambda v, c=c: int(v / 255 * c)) for c in RED])
    light = lum.point(lambda v: 255 if v > 120 else 0)                 # only the fill, never the dark outline
    img.paste(red, mask=ImageChops.multiply(m, light))
    return img


def main():
    heads = Path(sys.argv[1])
    out = []
    for d in DIRS:
        img, streak = uraume(heads, d)
        img = tint(img, streak)
        img.save(HAIRS / f"JJK_UraumeHair_{d}.png")
        out.append(img)
    if len(sys.argv) > 2:
        prev = Image.new("RGBA", (128 * 3, 128), (90, 90, 90, 255))
        for i, (d, im) in enumerate(zip(DIRS, out)):
            head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA")
            prev.alpha_composite(head, (128 * i, 0))
            prev.alpha_composite(im, (128 * i, 0))
        prev.resize((128 * 3 * 3, 128 * 3), Image.NEAREST).save(sys.argv[2])
    print("ok")


if __name__ == "__main__":
    main()
