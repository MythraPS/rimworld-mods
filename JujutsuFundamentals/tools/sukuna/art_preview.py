"""Workshop preview (About/Preview.png, 1280x720), made 2026-11-07 while she slept, in the style of the
Gojo mod's: a dark crimson backdrop with embers and 両面宿儺 faint behind, the Shrine techniques on an
arc, and Sukuna and Yuji standing, built from the game's own layers plus this mod's art.

Needs vanilla parts extracted from RimWorldLinux_Data/resources.assets (not in the repo):
Naked_Male_south.png in PARTS_DIR and Male_Average_Normal_south.png in HEADS_DIR.

  python tools/art_preview.py PARTS_DIR HEADS_DIR
"""
import math
import os
import random
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

M = os.path.join(os.path.dirname(__file__), "..", "..")
T = lambda *p: os.path.join(M, "Textures", *p)
PARTS, HEADS = sys.argv[1], sys.argv[2]
W, H = 1280, 720
random.seed(11)

# backdrop: crimson-black, brighter behind the arc
bg = Image.new("RGBA", (W, H))
px = bg.load()
for y in range(H):
    for x in range(W):
        r = math.hypot((x - 820) / W, (y - 470) / H)
        k = max(0.0, 1 - r * 1.5)
        px[x, y] = (int(14 + 70 * k), int(3 + 8 * k), int(6 + 12 * k), 255)
ember = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ed = ImageDraw.Draw(ember)
for _ in range(420):          # embers drifting up
    x, y = random.randrange(W), random.randrange(H)
    b = random.random() ** 3
    rad = 0.8 + 2.4 * b
    ed.ellipse([x - rad, y - rad, x + rad, y + rad], fill=(255, int(90 + 120 * b), 40, int(90 + 150 * b)))
bg.alpha_composite(ember.filter(ImageFilter.GaussianBlur(0.8)))
bg.alpha_composite(ember)

# the techniques on an arc, the domain in the middle
cx, cy, R = 905, 690, 330
LETTERS = {"Thorns", "ReverseCursedTechnique", "WorldCuttingSlash", "PseudoInfinity"}   # pictures that are only a letter
names = ["Dismantle", "Cleave", "Thorns", "ReverseCursedTechnique", "BlackFlash", "FireArrow",
         "WorldCuttingSlash", "AirStep", "PseudoInfinity"]
for i, name in enumerate(names):
    a = math.radians(192 + 156 * i / (len(names) - 1))
    x, y, s = cx + R * math.cos(a), cy + R * math.sin(a) * 1.05, 86
    path = T("Abilities", "Sukuna", "Kanji", name + ".png") if name in LETTERS else T("Abilities", "Sukuna", name + ".png")
    ic = Image.open(path).convert("RGBA").resize((s, s), Image.LANCZOS)
    m = Image.new("L", (s, s), 0)
    ImageDraw.Draw(m).ellipse([0, 0, s - 1, s - 1], fill=255)
    halo = Image.new("RGBA", (s + 40, s + 40), (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse([8, 8, s + 31, s + 31], fill=(255, 90, 80, 140))
    bg.alpha_composite(halo.filter(ImageFilter.GaussianBlur(9)), (int(x - s / 2 - 20), int(y - s / 2 - 20)))
    disc = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    disc.paste(ic, (0, 0), m)
    bg.alpha_composite(disc, (int(x - s / 2), int(y - s / 2)))
    ImageDraw.Draw(bg).ellipse([x - s / 2, y - s / 2, x + s / 2, y + s / 2], outline=(255, 235, 225, 220), width=3)
s = 150
ic = Image.open(T("Abilities", "Sukuna", "MalevolentShrine.png")).convert("RGBA").resize((s, s), Image.LANCZOS)
m = Image.new("L", (s, s), 0)
ImageDraw.Draw(m).ellipse([0, 0, s - 1, s - 1], fill=255)
halo = Image.new("RGBA", (s + 60, s + 60), (0, 0, 0, 0))
ImageDraw.Draw(halo).ellipse([10, 10, s + 49, s + 49], fill=(255, 60, 50, 170))
bg.alpha_composite(halo.filter(ImageFilter.GaussianBlur(14)), (cx - s // 2 - 30, 505 - s // 2 - 30))
disc = Image.new("RGBA", (s, s), (0, 0, 0, 0))
disc.paste(ic, (0, 0), m)
bg.alpha_composite(disc, (cx - s // 2, 505 - s // 2))
ImageDraw.Draw(bg).ellipse([cx - s / 2, 505 - s / 2, cx + s / 2, 505 + s / 2], outline=(255, 235, 225, 230), width=4)


def tinted(path, rgb):
    im = Image.open(path).convert("RGBA")
    r, g, b, a = im.split()
    mul = lambda ch, c: ch.point(lambda v: v * c // 255)
    return Image.merge("RGBA", (mul(r, rgb[0]), mul(g, rgb[1]), mul(b, rgb[2]), a))


def trousers(body, rgb, waist):
    """White trousers painted onto the body below the waist (the game's Pants texture is not extracted)."""
    a = body.getchannel("A")
    cut = Image.new("L", body.size, 0)
    ImageDraw.Draw(cut).rectangle([0, waist, body.width, body.height], fill=255)
    m = ImageChops.multiply(a, cut)
    grey = body.convert("L")
    out = Image.new("RGBA", body.size, (0, 0, 0, 0))
    shade = grey.point(lambda v: 255 if v > 60 else v)   # keep the black outline
    col = Image.merge("RGBA", [shade.point(lambda v, c=c: v * c // 255) for c in rgb] + [m])
    out.alpha_composite(col)
    return out


SKIN, PINK = (245, 217, 189), (238, 140, 158)
BODY_Y, HEAD_Y = 40, 11


def pawn(layers):
    p = Image.new("RGBA", (128, 170), (0, 0, 0, 0))
    for im, y in layers:
        p.alpha_composite(im, (0, y))
    p = p.crop(p.getbbox())
    k = 420 / p.height
    return p.resize((int(p.width * k), 420), Image.LANCZOS)


body = tinted(os.path.join(PARTS, "Naked_Male_south.png"), SKIN)
head = tinted(os.path.join(HEADS, "Male_Average_Normal_south.png"), SKIN)
HUM = T("Things", "Pawn", "Humanlike")
def no_grin(marks):
    """His face markings without the grin (her ask 2026-11-10, preview only - the game keeps it): the mouth
    sits at x 54.5-73.5, y 79.6-89.5 of the 128 px texture (art_hair2.sukuna_marks); the cheek stripes end
    at x 50.9 and start at 76.1, so clearing that box takes only the mouth."""
    marks = marks.copy()
    ImageDraw.Draw(marks).rectangle([53.8, 78.8, 74.2, 90.5], fill=(0, 0, 0, 0))
    return marks


sukuna = pawn([(body, BODY_Y),
               (Image.open(os.path.join(HUM, "Sukuna_CurseMarks", "Sukuna_CurseMarks_Male_south.png")).convert("RGBA"), BODY_Y),
               (head, HEAD_Y),
               (no_grin(Image.open(os.path.join(HUM, "Tattoos", "Sukuna_Markings", "Sukuna_Markings_south.png")).convert("RGBA")), HEAD_Y),
               (tinted(os.path.join(HUM, "Hairs", "Sukuna_VesselHair_south.png"), PINK), HEAD_Y)])
yuji = pawn([(body, BODY_Y),
             (Image.open(os.path.join(HUM, "Apparel", "Sukuna_YujiUniform", "Sukuna_YujiUniform_Male_south.png")).convert("RGBA"), BODY_Y),
             (head, HEAD_Y),
             (Image.open(os.path.join(HUM, "Tattoos", "Sukuna_YujiMarks", "Sukuna_YujiMarks_south.png")).convert("RGBA"), HEAD_Y),
             (tinted(os.path.join(HUM, "Hairs", "Sukuna_YujiHair_south.png"), PINK), HEAD_Y)])

for p, x0, glow in ((yuji, 290, (255, 160, 170, 60)), (sukuna, 90, (255, 40, 40, 90))):
    y0 = 700 - p.height
    aura = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(aura).ellipse([x0 - 60, y0 - 30, x0 + p.width + 60, y0 + p.height + 20], fill=glow)
    bg.alpha_composite(aura.filter(ImageFilter.GaussianBlur(40)))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse([x0 + 10, 682, x0 + p.width - 10, 714], fill=(0, 0, 0, 160))
    bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    bg.alpha_composite(p, (x0, y0))

kan = ImageFont.truetype("/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc", 150, index=0)
k = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(k).text((W - 40, 20), "両面宿儺", font=kan, fill=(255, 255, 255, 36), anchor="ra")
bg.alpha_composite(k)
title = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Black.ttf", 76)
sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 30)
d = ImageDraw.Draw(bg)
for off, fill in (((4, 5), (0, 0, 0, 200)), ((0, 0), (255, 255, 255, 255))):
    d.text((60 + off[0], 40 + off[1]), "RYOMEN SUKUNA", font=title, fill=fill)
    d.text((62 + off[0], 122 + off[1]), "& YUJI ITADORI", font=title, fill=fill)
    d.text((64 + off[0], 216 + off[1]), "The Shrine path and the fingers, for Vanilla Psycasts Expanded", font=sub,
           fill=fill if off == (0, 0) else (0, 0, 0, 200))
bg.convert("RGB").save(os.path.join(M, "About", "Preview.png"), optimize=True)
