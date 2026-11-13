"""Red burst ring (2026-11-04): a bright red rim fading to a faint red inside,
transparent outside. Grows outward as a static fleck."""
import math, os
from PIL import Image
N = 256
im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); px = im.load(); c = (N - 1) / 2
for y in range(N):
    for x in range(N):
        r = math.hypot(x - c, y - c) / c          # 0 centre .. 1 edge
        if r > 1: continue
        rim = math.exp(-((r - 0.86) / 0.07) ** 2)  # bright band near the edge
        fill = 0.28 * r ** 2                       # faint, stronger toward the rim
        a = min(1.0, rim + fill) * (1 - max(0, (r - 0.95) / 0.05))
        white = rim ** 3 * 0.6                     # hot core of the band
        px[x, y] = (255, int(40 + 180 * white), int(40 + 160 * white), int(255 * a))
out = os.path.join(os.path.dirname(__file__), "..", "..", "Textures", "Things", "Gojo", "RedBurst.png")
im.save(out)
