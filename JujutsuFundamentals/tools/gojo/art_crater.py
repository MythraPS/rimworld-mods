"""Blue's crater, drawn procedurally: a dark scooped-out middle, a lighter raised rim,
cracks and thrown dirt running outward. Semi-transparent so the map's own ground
shows through; BlueCrater scales it to the pull's radius.

  python tools/art_crater.py MOD_DIR
"""
import math, random, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

S = 512
rnd = random.Random(7)
c = S / 2
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# thrown dirt: streaks outward past the rim
ej = Image.new("RGBA", (S, S), (0, 0, 0, 0)); de = ImageDraw.Draw(ej)
for _ in range(70):
    a = rnd.uniform(0, math.tau); r0 = S * 0.30; r1 = S * rnd.uniform(0.38, 0.49)
    w = rnd.randint(3, 9)
    de.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * r1, c + math.sin(a) * r1)], fill=(70, 52, 36, 110), width=w)
img.alpha_composite(ej.filter(ImageFilter.GaussianBlur(3)))

# the rim: a lighter ring of piled-up earth
rim = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dr = ImageDraw.Draw(rim)
dr.ellipse((c - S * 0.36, c - S * 0.36, c + S * 0.36, c + S * 0.36), fill=(118, 94, 68, 170))
img.alpha_composite(rim.filter(ImageFilter.GaussianBlur(10)))

# the bowl: darker toward the middle
bowl = Image.new("RGBA", (S, S), (0, 0, 0, 0)); db = ImageDraw.Draw(bowl)
for k in range(40):
    t = k / 39; r = S * 0.30 * (1 - t)
    v = int(62 - 30 * t)
    db.ellipse((c - r, c - r, c + r, c + r), fill=(v + 8, v, v - 8 if v > 8 else 0, int(170 + 60 * t)))
img.alpha_composite(bowl.filter(ImageFilter.GaussianBlur(6)))

# cracks from the middle out through the rim
cr = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dc = ImageDraw.Draw(cr)
for _ in range(11):
    a = rnd.uniform(0, math.tau); x, y = c + math.cos(a) * S * 0.05, c + math.sin(a) * S * 0.05
    for _ in range(9):
        a += rnd.gauss(0, 0.35); L = rnd.uniform(S * 0.03, S * 0.05)
        nx, ny = x + math.cos(a) * L, y + math.sin(a) * L
        dc.line([(x, y), (nx, ny)], fill=(18, 12, 8, 220), width=3)
        x, y = nx, ny
img.alpha_composite(cr.filter(ImageFilter.GaussianBlur(0.7)))

# rubble specks
ds = ImageDraw.Draw(img)
for _ in range(260):
    a = rnd.uniform(0, math.tau); r = abs(rnd.gauss(S * 0.33, S * 0.07))
    if r > S * 0.48: continue
    x, y = c + math.cos(a) * r, c + math.sin(a) * r; s = rnd.uniform(1.5, 4.5); g = rnd.randint(70, 130)
    ds.ellipse((x - s, y - s, x + s, y + s), fill=(g, int(g * 0.85), int(g * 0.7), 230))

# soft outer edge
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).ellipse((S * 0.03, S * 0.03, S * 0.97, S * 0.97), fill=255)
a = img.getchannel("A"); from PIL import ImageChops
img.putalpha(ImageChops.multiply(a, mask.filter(ImageFilter.GaussianBlur(14))))
out = Path(sys.argv[1]) / "Textures/Things/Gojo/Crater.png"
img.save(out); print(out)
