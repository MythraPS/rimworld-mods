"""Piercing Blood's shot (her note 2026-11-06): like a red laser but not glowing, an arrow-shaped
head, and the blood boiling as it travels. BloodBeam = the shot (points north, head at the top),
BloodBubble = one bubble of the boil, thrown along its path.
  python tools/art_bloodbeam.py TEXTURES_DIR"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

T = Path(sys.argv[1]) / "Things/Sukuna"
K = 4
DARK, MID, LIGHT = (92, 6, 14, 255), (150, 14, 24, 255), (196, 52, 58, 255)


def beam():
    W, H = 32, 256
    im = Image.new("RGBA", (W * K, H * K), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx = W / 2 * K
    head = 30
    # the shaft: widest just behind the head, thinning to a point at the tail
    shaft = [(cx - 3.2 * K, head * K), (cx + 3.2 * K, head * K), (cx + 0.6 * K, (H - 4) * K), (cx - 0.6 * K, (H - 4) * K)]
    d.polygon(shaft, fill=MID)
    d.polygon([(cx - 1.2 * K, head * K), (cx + 1.2 * K, head * K), (cx, (H - 40) * K)], fill=LIGHT)     # wet sheen, not a glow
    # the arrowhead
    tip = [(cx, 2 * K), (cx + 11 * K, (head + 6) * K), (cx + 3.2 * K, (head - 1) * K), (cx - 3.2 * K, (head - 1) * K), (cx - 11 * K, (head + 6) * K)]
    d.polygon(tip, fill=DARK)
    d.polygon([(cx, 8 * K), (cx + 6 * K, (head + 1) * K), (cx - 6 * K, (head + 1) * K)], fill=MID)
    # fade the tail out so it doesn't end in a hard cut
    a = im.getchannel("A"); px = a.load()
    for y in range(int(H * 0.6) * K, H * K):
        f = 1 - (y - H * 0.6 * K) / (H * 0.4 * K)
        for x in range(W * K):
            px[x, y] = int(px[x, y] * f)
    im.putalpha(a)
    im.resize((W, H), Image.LANCZOS).save(T / "BloodBeam.png")


def bubble():
    S = 32
    im = Image.new("RGBA", (S * K, S * K), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.ellipse([3 * K, 3 * K, 29 * K, 29 * K], fill=MID, outline=DARK, width=2 * K)
    d.ellipse([9 * K, 8 * K, 15 * K, 13 * K], fill=LIGHT)          # the shine on a bubble
    im.resize((S, S), Image.LANCZOS).save(T / "BloodBubble.png")


beam(); bubble()
