"""Black Flash burst (her call 2026-11-07): a black, jagged star with a red rim and red cracks
running out of it, the colours of the anime's black lightning. Drawn at 4x and shrunk.
Shown as a growing static fleck where the hit lands, on top of the black sparks.

  python tools/art_blackflash.py OUT.png [PREVIEW.png]
"""
import math
import random
import sys
from PIL import Image, ImageDraw, ImageFilter

S, K = 256, 4
W = S * K
RED = (214, 24, 32, 255)
DARK = (8, 4, 6, 255)


def star(cx, cy, r_in, r_out, n, rnd):
    pts = []
    for i in range(n * 2):
        a = math.pi * i / n + rnd.uniform(-0.12, 0.12)
        r = (r_out if i % 2 == 0 else r_in) * rnd.uniform(0.75, 1.15)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def bolt(dr, x, y, ang, length, width, rnd, col):
    pts = [(x, y)]
    steps = 7
    for i in range(steps):
        ang += rnd.uniform(-0.55, 0.55)
        seg = length / steps
        x, y = x + seg * math.cos(ang), y + seg * math.sin(ang)
        pts.append((x, y))
    for i in range(len(pts) - 1):
        w = max(2, int(width * (1 - i / len(pts))))
        dr.line([pts[i], pts[i + 1]], fill=col, width=w)


def main():
    rnd = random.Random(7)
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    c = W / 2
    # red glow halo
    halo = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hd.polygon(star(c, c, W * 0.2, W * 0.36, 9, rnd), fill=(200, 20, 30, 170))
    halo = halo.filter(ImageFilter.GaussianBlur(W * 0.03))
    img.alpha_composite(halo)
    dr = ImageDraw.Draw(img)
    # red cracks reaching out
    for i in range(11):
        a = 2 * math.pi * i / 11 + rnd.uniform(-0.2, 0.2)
        bolt(dr, c + W * 0.12 * math.cos(a), c + W * 0.12 * math.sin(a), a, W * rnd.uniform(0.25, 0.37), W * 0.022, rnd, RED)
    # red rim then the black core
    dr.polygon(star(c, c, W * 0.15, W * 0.29, 9, rnd), fill=RED)
    dr.polygon(star(c, c, W * 0.12, W * 0.24, 9, rnd), fill=DARK)
    # black cracks inside the red ones, thinner
    for i in range(7):
        a = 2 * math.pi * i / 7 + rnd.uniform(-0.3, 0.3)
        bolt(dr, c, c, a, W * rnd.uniform(0.18, 0.3), W * 0.012, rnd, DARK)
    out = img.resize((S, S), Image.LANCZOS)
    out.save(sys.argv[1])
    if len(sys.argv) > 2:
        bg = Image.new("RGBA", (S * 2, S), (120, 110, 95, 255))
        bg.alpha_composite(out, (0, 0))
        bg2 = Image.new("RGBA", (S, S), (40, 40, 40, 255)); bg2.alpha_composite(out)
        bg.paste(bg2, (S, 0))
        bg.save(sys.argv[2])


if __name__ == "__main__":
    main()
