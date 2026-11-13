"""Workshop preview for the merged mod (About/Preview.png, 1280x720), 2026-11-12. Made only from the mod's own art:
the six psycast path backgrounds side by side as the backdrop, the title in the font of the old Gojo preview, and a row
of picture ability icons along the bottom (icons that are only letters are left out).
Usage: python tools/art_preview.py   (run from anywhere; writes About/Preview.png)"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

M = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
T = lambda *p: os.path.join(M, "Textures", *p)
W, H = 1280, 720

bg = Image.new("RGBA", (W, H), (0, 0, 0, 255))
paths = ["JujutsuFundamentals", "GojoPath", "SukunaPath", "YutaPath", "CursedSpeech", "NewShadowStyle"]
sw = W // len(paths) + 1
for i, name in enumerate(paths):
    im = Image.open(T("UI", "Backgrounds", name + ".png")).convert("RGBA")
    scale = H / im.height
    im = im.resize((int(im.width * scale), H), Image.LANCZOS)
    x0 = (im.width - sw) // 2
    strip = im.crop((x0, 0, x0 + sw, H))
    strip = ImageEnhance.Brightness(strip).enhance(0.55)
    bg.alpha_composite(strip, (i * (W // len(paths)), 0))
# darken top and bottom so the text and icons read
shade = Image.new("RGBA", (W, H), (0, 0, 0, 0)); sd = ImageDraw.Draw(shade)
for y in range(H):
    a = int(200 * max((1 - y / 330) ** 1.4 if y < 330 else 0, ((y - 470) / 250) ** 1.3 if y > 470 else 0))
    sd.line([(0, y), (W, y)], fill=(4, 4, 10, min(230, a)))
bg.alpha_composite(shade)


title = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Black.ttf", 96)
sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 40)
small = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 26)
txt = Image.new("RGBA", (W, H), (0, 0, 0, 0))
for off, fill in [((4, 4), (0, 0, 0, 200)), ((0, 0), (245, 242, 236, 255))]:
    d = ImageDraw.Draw(txt)
    d.text((60 + off[0], 40 + off[1]), "JUJUTSU KAISEN", font=title, fill=fill)
    d.text((66 + off[0], 160 + off[1]), "Psycasts, Characters, Items, Quests", font=sub, fill=fill)
    d.text((66 + off[0], 214 + off[1]), "for Vanilla Psycasts Expanded", font=small, fill=fill if off == (0, 0) else (0, 0, 0, 200))
bg.alpha_composite(txt)

icons = [("Gojo", "Infinity"), ("Gojo", "Purple"), ("Sukuna", "Dismantle"), ("Sukuna", "MalevolentShrine"),
         ("Yuta/Copy", "Copy"), ("Yuta/Rika", "ManifestRika"), ("Yuta/Speech", "Stop"), ("Sukuna", "PiercingBlood"),
         ("Jujutsu", "Supernova"), ("Yuta/Copy", "JacobsLadder"), ("Angel", "Flight"), ("Jujutsu", "BlackFlash")]
size, gap = 82, 12
total = len(icons) * size + (len(icons) - 1) * gap
x, y = (W - total) // 2, H - size - 46
for folder, name in icons:
    im = Image.open(T("Abilities", *folder.split("/"), name + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)
    glow = Image.new("RGBA", (size + 24, size + 24), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([8, 8, size + 16, size + 16], 10, fill=(0, 0, 0, 200))
    bg.alpha_composite(glow.filter(ImageFilter.GaussianBlur(6)), (x - 12, y - 12))
    bg.alpha_composite(im, (x, y))
    x += size + gap

out = os.path.join(M, "About", "Preview.png")
bg.convert("RGB").save(out)
print(out)
