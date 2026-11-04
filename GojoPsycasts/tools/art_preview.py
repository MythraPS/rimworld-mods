"""Workshop preview (About/Preview.png, 1280x720), 2026-11-04: a starfield,
a glowing infinity sign with the ability icons on it, the title, and 無下限 (Limitless)."""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont
M = os.path.join(os.path.dirname(__file__), "..")
T = lambda *p: os.path.join(M, "Textures", *p)
W, H = 1280, 720
# backdrop: the domain texture, cropped to fill, darkened
import random
random.seed(7)
bg = Image.new("RGBA", (W, H), (0, 0, 0, 255)); px = bg.load()
for y in range(H):            # deep blue-black, a little lighter around the sign
    for x in range(W):
        r = math.hypot((x - 800) / W, (y - 455) / H)
        k = max(0.0, 1 - r * 1.6)
        px[x, y] = (int(4 + 18 * k), int(6 + 20 * k), int(18 + 46 * k), 255)
sd = ImageDraw.Draw(bg)
for _ in range(900):          # stars
    x, y = random.randrange(W), random.randrange(H); b = random.random() ** 3
    c = int(120 + 135 * b); rad = 0.6 + 1.6 * b
    sd.ellipse([x - rad, y - rad, x + rad, y + rad], fill=(c, c, min(255, c + 20), 255))
shade = Image.new("RGBA", (W, H), (0, 0, 0, 0)); sd = ImageDraw.Draw(shade)
for y in range(H): sd.line([(0, y), (W, y)], fill=(4, 6, 20, int(150 * (1 - y / H) ** 1.5)))
bg.alpha_composite(shade)

# infinity sign (lemniscate of Gerono-ish Bernoulli), centred low
cx, cy, a = 800, 455, 330
pts = []
for i in range(721):
    t = i / 720 * 2 * math.pi
    d = 1 + math.sin(t) ** 2
    pts.append((cx + a * math.cos(t) / d, cy + a * math.sin(t) * math.cos(t) / d * 1.25))
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
for j in range(len(pts) - 1):
    x = pts[j][0]; f = (x - (cx - a)) / (2 * a)            # left blue -> right red
    col = (int(70 + 185 * f), int(150 - 90 * f), int(255 - 175 * f))
    gd.line([pts[j], pts[j + 1]], fill=col + (255,), width=16)
blur = glow.filter(ImageFilter.GaussianBlur(14))
bg.alpha_composite(blur); bg.alpha_composite(blur)
thin = Image.new("RGBA", (W, H), (0, 0, 0, 0)); td = ImageDraw.Draw(thin)
for j in range(len(pts) - 1): td.line([pts[j], pts[j + 1]], fill=(255, 255, 255, 200), width=3)
bg.alpha_composite(thin)

def at(t):
    d = 1 + math.sin(t) ** 2
    return (cx + a * math.cos(t) / d, cy + a * math.sin(t) * math.cos(t) / d * 1.25)
icons = [("Infinity", (cx, cy), 120),
         ("Blue", at(math.pi), 104), ("BlueFist", at(math.pi * 0.78), 84), ("BlackFlash", at(math.pi * 1.22), 84),
         ("Red", at(0), 104), ("ReverseCursedTechnique", at(math.pi * 0.22), 84), ("Teleport", at(-math.pi * 0.22), 84),
         ("Purple", (cx, cy - 165), 96), ("InfiniteVoid", (cx, cy + 165), 96)]
LETTERS = {"BlueFist", "Teleport", "ReverseCursedTechnique"}   # their pictures are only a letter
for name, (x, y), s in icons:
    path = T("Abilities", "Gojo", "Kanji", name + ".png") if name in LETTERS else T("Abilities", "Gojo", name + ".png")
    ic = Image.open(path).convert("RGBA").resize((s, s), Image.LANCZOS)
    m = Image.new("L", (s, s), 0); ImageDraw.Draw(m).ellipse([0, 0, s - 1, s - 1], fill=255)
    halo = Image.new("RGBA", (s + 40, s + 40), (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse([8, 8, s + 31, s + 31], fill=(200, 230, 255, 150))
    halo = halo.filter(ImageFilter.GaussianBlur(9))
    bg.alpha_composite(halo, (int(x - s / 2 - 20), int(y - s / 2 - 20)))
    disc = Image.new("RGBA", (s, s), (0, 0, 0, 0)); disc.paste(ic, (0, 0), m)
    bg.alpha_composite(disc, (int(x - s / 2), int(y - s / 2)))
    ImageDraw.Draw(bg).ellipse([x - s / 2, y - s / 2, x + s / 2, y + s / 2], outline=(255, 255, 255, 220), width=3)

# Gojo himself, built from the game's own layers (vanilla parts extracted to tools/pawnparts:
# Thin body, Average head, basic shirt + jacket in his black, his white hair and blindfold).
# pawnparts is not in the repo: those are the game's own textures, extract them from resources.assets.)
PP = os.path.join(os.path.dirname(__file__), "pawnparts")
def tinted(path, rgb):
    im = Image.open(path).convert("RGBA"); r, g, b, a = im.split()
    mul = lambda ch, c: ch.point(lambda v: v * c // 255)
    return Image.merge("RGBA", (mul(r, rgb[0]), mul(g, rgb[1]), mul(b, rgb[2]), a))
skin, black, white = (236, 206, 180), (46, 46, 60), (245, 247, 255)
pawn = Image.new("RGBA", (128, 170), (0, 0, 0, 0))
BODY_Y, HEAD_Y = 40, 40 - 29          # the head sits about 0.34 cells above the body
for path, col, y in ((os.path.join(PP, "Naked_Thin_south.png"), skin, BODY_Y),
                     (os.path.join(PP, "ShirtBasic_Thin_south.png"), black, BODY_Y),
                     (os.path.join(PP, "Jacket_Thin_south.png"), black, BODY_Y),
                     (os.path.join(PP, "Male_Average_Normal_south.png"), skin, HEAD_Y),
                     (T("Things", "Pawn", "Humanlike", "Apparel", "Gojo_Blindfold", "Gojo_Blindfold_south.png"), (255, 255, 255), HEAD_Y),
                     (T("Things", "Pawn", "Humanlike", "Hairs", "Gojo_Hair_south.png"), white, HEAD_Y)):
    pawn.alpha_composite(tinted(path, col), (0, y))
pawn = pawn.crop(pawn.getbbox())
k = 430 / pawn.height
pawn = pawn.resize((int(pawn.width * k), 430), Image.LANCZOS)
px0, py0 = 230 - pawn.width // 2, 690 - pawn.height
aura = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(aura).ellipse([px0 - 70, py0 - 30, px0 + pawn.width + 70, py0 + pawn.height + 20], fill=(120, 190, 255, 70))
bg.alpha_composite(aura.filter(ImageFilter.GaussianBlur(40)))
shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(shadow).ellipse([px0 + 10, 672, px0 + pawn.width - 10, 706], fill=(0, 0, 0, 150))
bg.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6)))
bg.alpha_composite(pawn, (px0, py0))

d = ImageDraw.Draw(bg)
kanji = ImageFont.truetype("/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc", 150, index=0)
k = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ImageDraw.Draw(k).text((W - 40, 20), "無下限", font=kanji, fill=(255, 255, 255, 38), anchor="ra")
bg.alpha_composite(k)
title = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Black.ttf", 92)
sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 32)
for off, fill in (((4, 5), (0, 0, 0, 200)), ((0, 0), (255, 255, 255, 255))):
    d.text((60 + off[0], 40 + off[1]), "SATORU GOJO", font=title, fill=fill)
    d.text((64 + off[0], 152 + off[1]), "The Limitless path for Vanilla Psycasts Expanded", font=sub, fill=fill if off == (0, 0) else (0, 0, 0, 200))
bg.convert("RGB").save(os.path.join(M, "About", "Preview.png"), optimize=True)
