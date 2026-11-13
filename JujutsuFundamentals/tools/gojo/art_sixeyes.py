"""Light blue Six Eyes, drawn like Biotech's eye-colour genes: the vanilla red-eye
pictures (extracted from Biotech's asset bundle) with the red iris recoloured.
usage: art_sixeyes.py <dir with redeyes_male.png, redeyes_female_east.png> <mod Textures dir>"""
import sys
from pathlib import Path
from PIL import Image

DARK, LIGHT = (70, 165, 235), (185, 235, 255)

def recolour(src):
    im = Image.open(src).convert("RGBA")
    reds = [im.getpixel((x, y))[0] for y in range(im.height) for x in range(im.width)
            if im.getpixel((x, y))[3] > 0 and im.getpixel((x, y))[0] > im.getpixel((x, y))[1] + 30]
    lo, hi = min(reds), max(reds)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = im.getpixel((x, y))
            if a == 0 or r <= g + 30: continue          # outline and lashes stay as they are
            t = (r - lo) / max(1, hi - lo)
            im.putpixel((x, y), tuple(round(DARK[i] + (LIGHT[i] - DARK[i]) * t) for i in range(3)) + (a,))
    return im

src, out = Path(sys.argv[1]), Path(sys.argv[2]) / "Things/Pawn/Humanlike/HeadAttachments/Gojo_SixEyes"
(out / "Male").mkdir(parents=True, exist_ok=True); (out / "Female").mkdir(parents=True, exist_ok=True)
recolour(src / "redeyes_male.png").save(out / "Male/Gojo_SixEyes_Male.png")
recolour(src / "redeyes_female_east.png").save(out / "Female/Gojo_SixEyes_Female_east.png")
print("ok", out)
