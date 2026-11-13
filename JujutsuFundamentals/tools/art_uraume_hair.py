"""Uraume's hair (her ask 2026-11-11; a bowl cut since 2026-11-12, her call): white, a smooth rounded cap cut level
all round (at the eyebrows in front, a little lower at the sides and back), with the red streak (her words: "that red
streak in his hair"). Dark outline like the other hair; the white fill is tinted by the hair colour in game, the
streak is drawn already red so it stays red under Uraume's near-white hair colour.

  python tools/art_uraume_hair.py HEADS_DIR [PREVIEW.png]
HEADS_DIR holds Male_Average_Normal_{south,east,north}.png (vanilla, extracted; not shipped; a copy is in
~/.local/share/rimworld-tools/heads)."""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parent.parent
HAIRS = MOD / "Textures/Things/Pawn/Humanlike/Hairs"
DIRS = ("south", "east", "north")
S = 4                                   # drawn at 4x, shrunk to 128
INK, FILL, SHADE = (28, 26, 30, 255), (250, 250, 250, 255), (205, 205, 210, 255)
RED = (200, 34, 40, 255)


def cut_y(d, x):
    """Where the hair ends (128 px coords)."""
    if d == "south":
        return 63 + 0.0045 * (x - 64) ** 2          # eyebrows in the middle, a little lower at the sides
    if d == "east":
        return 62 + max(0.0, (78 - x)) * 0.22       # eyebrow at the front (right), down to the ear at the back
    return 71                                       # from behind: level


def hair(heads, d, streak_on=True, nick=1.0, nicks=9):
    """nick / nicks: how deep and how many the notches in the fringe are (Inumaki's is more tousled)."""
    head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA").getchannel("A")
    head = head.resize((128 * S, 128 * S), Image.LANCZOS).point(lambda v: 255 if v > 100 else 0)
    cap = head.filter(ImageFilter.MaxFilter(2 * 3 * S + 1))            # sits a little proud of the skull
    below = Image.new("L", cap.size, 0); bd = ImageDraw.Draw(below)
    for X in range(cap.width):
        y = cut_y(d, X / S) * S
        bd.line([(X, 0), (X, y)], fill=255)
    cap = ImageChops.multiply(cap, below)
    # the blunt fringe, nicked a little where the strands part
    nd = ImageDraw.Draw(cap)
    bb0 = cap.getbbox()
    for i in range(1, nicks):
        X = bb0[0] + (bb0[2] - bb0[0]) * i / nicks
        y = cut_y(d, X / S) * S
        nd.polygon([(X - 1.1 * nick * S, y + 1), (X + 1.1 * nick * S, y + 1), (X, y - 1.6 * nick * S)], fill=0)
    cap = cap.filter(ImageFilter.GaussianBlur(1.5)).point(lambda v: 255 if v > 127 else 0)
    outline = cap.filter(ImageFilter.MaxFilter(2 * 2 * S + 1))
    img = Image.new("RGBA", cap.size, (0, 0, 0, 0))
    img.paste(INK, mask=outline)
    img.paste(FILL, mask=cap)
    dr = ImageDraw.Draw(img)
    bb = cap.getbbox()
    # a few strands, like the other hair textures, ending at the cut
    for i in range(1, 9):
        X = bb[0] + (bb[2] - bb[0]) * i / 9
        yb = cut_y(d, X / S) * S
        dr.line([(X, yb - 12 * S), (X, yb - 1.8 * S)], fill=SHADE, width=int(1.0 * S))
    if not streak_on:
        return img.resize((128, 128), Image.LANCZOS)
    # the red streak: one band from the crown to the cut, on his right side (the viewer's left from the front)
    sx = {"south": 52.0, "east": 74.0, "north": 52.0}[d] * S
    streak = Image.new("L", cap.size, 0)
    yb = cut_y(d, sx / S) * S
    ImageDraw.Draw(streak).polygon([(sx - 0.6 * S, bb[1]), (sx + 0.8 * S, bb[1]), (sx + 2.4 * S, yb - 4 * S), (sx + 0.4 * S, yb + 1 * S), (sx - 1.8 * S, yb - 4 * S)], fill=255)
    streak = ImageChops.multiply(streak, cap.filter(ImageFilter.MinFilter(2 * S + 1)))
    img.paste(RED, mask=streak)
    return img.resize((128, 128), Image.LANCZOS)


def main():
    heads = Path(sys.argv[1])
    out = []
    for d in DIRS:
        img = hair(heads, d)
        img.save(HAIRS / f"JJK_UraumeHair_{d}.png")
        out.append(img)
    if len(sys.argv) > 2:
        prev = Image.new("RGBA", (128 * 3, 128), (90, 90, 90, 255))
        for i, (d, im) in enumerate(zip(DIRS, out)):
            prev.alpha_composite(Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA"), (128 * i, 0))
            prev.alpha_composite(im, (128 * i, 0))
        prev.resize((128 * 3 * 3, 128 * 3), Image.NEAREST).save(sys.argv[2])
    print("ok")


if __name__ == "__main__":
    main()
