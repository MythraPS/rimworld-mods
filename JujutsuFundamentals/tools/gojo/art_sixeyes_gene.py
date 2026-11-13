"""The Six Eyes gene icon (2026-11-12): the kanji 六眼 in the kanji-icon font, white with the Six Eyes' light
blue glow, on a clear background like Biotech's gene icons. Usage: python tools/gojo/art_sixeyes_gene.py MOD_DIR"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

MOD = Path(sys.argv[1])
S = 128
FONT = ("/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc", 0)
BLUE = (150, 212, 248, 255)   # the Six Eyes' light blue (Facial Animation tint)

f = ImageFont.truetype(FONT[0], 54, index=FONT[1])
text = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(text)
box = d.textbbox((0, 0), "六眼", font=f)
xy = ((S - (box[2] - box[0])) / 2 - box[0], (S - (box[3] - box[1])) / 2 - box[1])
d.text(xy, "六眼", font=f, fill=(240, 236, 228, 255))
glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(glow).text(xy, "六眼", font=f, fill=BLUE)
glow = glow.filter(ImageFilter.GaussianBlur(5))
out = Image.alpha_composite(glow, glow)
out = Image.alpha_composite(out, text)
p = MOD / "Textures/UI/Icons/Genes/Gojo_SixEyes.png"
out.save(p)
print(p)
