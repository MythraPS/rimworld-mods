"""Dhruv's shikigami as Yuta copies them (her call 2026-11-11): small bat-like creatures with a head like Cursed
Spirit Rika's (her crown of spotted tentacles round a fanged mouth, from art_rika.py), on spread membrane wings
in dark cursed-smoke purple.

  python tools/yuta/art_dhruv.py [PREVIEW.png]"""
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import art_rika as R  # noqa: E402

OUT = os.path.join(R.MOD, "Textures/Things/Yuta/Dhruv")
os.makedirs(OUT, exist_ok=True)

WING = (58, 42, 82)
WING_HI = (96, 74, 128)
BONE = (150, 130, 182)
BODY = (44, 34, 60)


def wing(c, root, tip_dir, span, side, fold=1.0):
    """A bat wing: three finger bones fanning out from the wrist, membrane between them with a scalloped edge."""
    im, d = c.layer()
    rx, ry = root
    wrist = (rx + side * span * 0.42 * fold, ry - span * 0.16)
    tips = []
    for k, (ang, L) in enumerate(((-0.55, 0.62), (-0.05, 0.6), (0.45, 0.48))):
        a = tip_dir + side * ang
        tips.append((wrist[0] + side * math.cos(a) * span * L * fold, wrist[1] + math.sin(a) * span * L))
    # membrane: root -> wrist -> tips -> back to the body, with scallops between the tips
    poly = [root, wrist, tips[0]]
    for a, b in zip(tips, tips[1:] + [(rx + side * span * 0.06, ry + span * 0.22)]):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        # scallop dips toward the wrist
        poly += [(mx + (wrist[0] - mx) * 0.22, my + (wrist[1] - my) * 0.22), b]
    d.polygon([c.P(*p) for p in poly], fill=WING + (255,))
    # bones
    R.line(d, c, [root, wrist], BONE, 0.016)
    for t in tips:
        R.line(d, c, [wrist, t], BONE, 0.01)
    R.ell(d, c, wrist[0], wrist[1], 0.016, 0.016, BONE)
    # a lighter fold line in each panel
    for t in tips[:2]:
        R.line(d, c, [((wrist[0] + t[0]) / 2, (wrist[1] + t[1]) / 2), ((root[0] + t[0]) / 2, (root[1] + t[1]) / 2 + 0.02)], WING_HI, 0.006)


def body(c, cx, cy, w, h):
    im, d = c.layer()
    R.ell(d, c, cx, cy, w, h, BODY + (255,))
    # little hind claws
    for s in (-1, 1):
        R.line(d, c, [(cx + s * w * 0.4, cy + h * 0.8), (cx + s * w * 0.55, cy + h * 1.25)], BONE, 0.009)


def head_img(face):
    """Rika's head (her ridged dome with the triangular fanged mouth, tendrils behind), drawn full size with art_rika's
    own functions so a redraw there carries over, then shrunk onto the bat."""
    c = R.Canvas(128)
    if face == "east":
        R.curse_tentacles(c, 0.46, 0.5, 9, math.pi * 1.1, 0.3)
        R.curse_head(c, 0.55, 0.52, 0.15, 0.22, turn=0.6)
    elif face == "south":
        R.curse_tentacles(c, 0.5, 0.5, 11, math.pi * 1.5, 0.3)
        R.curse_head(c, 0.5, 0.5, 0.17, 0.23)
    else:
        R.curse_head(c, 0.5, 0.5, 0.17, 0.23, mouth=False)
        R.curse_tentacles(c, 0.5, 0.5, 11, math.pi * 1.5, 0.3)
    return c.flatten(R.CURSE["line"], width=0.016)


def paste(base, img, cx, cy, scale):
    w = int(base.size[0] * scale)
    small = img.resize((w, w), Image.LANCZOS)
    base.alpha_composite(small, (int(cx * base.size[0] - w / 2), int(cy * base.size[1] - w / 2)))


def draw(face):
    c = R.Canvas(128)
    if face in ("south", "north"):
        wing(c, (0.47, 0.6), -0.3, 0.4, -1)
        wing(c, (0.53, 0.6), -0.3, 0.4, 1)
        body(c, 0.5, 0.63, 0.06, 0.085)
        img = c.flatten(R.CURSE["line"], width=0.014)
        paste(img, head_img(face), 0.5, 0.5, 0.5)
    else:  # east: the far wing raised behind, the near wing forward
        wing(c, (0.44, 0.58), -1.0, 0.36, -1, fold=0.6)
        body(c, 0.47, 0.63, 0.06, 0.08)
        wing(c, (0.48, 0.6), -0.6, 0.38, 1, fold=0.75)
        img = c.flatten(R.CURSE["line"], width=0.014)
        paste(img, head_img(face), 0.55, 0.5, 0.46)
    return img


def main():
    out = {}
    for face in ("south", "east", "north"):
        img = draw(face)
        img.save(os.path.join(OUT, f"Bat_{face}.png"))
        out[face] = img
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (128 * 3 * 3, 128 * 3), (92, 80, 64, 255))
        for i, face in enumerate(("south", "east", "north")):
            big = out[face].resize((384, 384), Image.NEAREST)
            sheet.alpha_composite(big, (i * 384, 0))
        sheet.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
