"""Workshop preview: a spread of the designs, each in a different material colour, the way CutoutComplex
shows it in game (mask red = texture x stuff colour, mask black = texture as drawn)."""
import glob, os, sys
from PIL import Image, ImageChops
T = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures", "IranianRugs")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "About", "Preview.png")
STUFF = [(205, 192, 168), (160, 45, 45), (95, 70, 160), (150, 190, 215), (140, 95, 60), (90, 130, 90), (200, 170, 90)]
W, H, BG = 1280, 720, (88, 104, 72)

def tinted(path, col):
    tex = Image.open(path).convert("RGBA"); m = Image.open(path[:-4] + "_m.png").convert("RGBA")
    mul = ImageChops.multiply(tex.convert("RGB"), Image.new("RGB", tex.size, col))
    red = m.getchannel("R").point(lambda v: 255 if v > 127 else 0)
    rgb = Image.composite(mul, tex.convert("RGB"), red); rgb.putalpha(tex.getchannel("A"))
    return rgb.crop(rgb.getbbox())

img = Image.new("RGB", (W, H), BG)
# a spread of the designs (all of them would be too small to see at this size)
PICK = {"Large": "AHIJLMN", "Medium": "BDFGKL", "Small": "BEGI"}
rows = [("Large", 178), ("Medium", 170), ("Small", 140)]
y, k = 70, 0
for size, cell in rows:
    files = [f"{T}/{size}/Iranian{size}_{c}.png" for c in PICK[size]]
    gap = (W - len(files) * cell) / (len(files) + 1)
    for i, f in enumerate(files):
        r = tinted(f, STUFF[k % len(STUFF)]); k += 1
        r.thumbnail((cell - 10, cell - 10), Image.LANCZOS)
        x = int(gap + i * (cell + gap) + (cell - r.width) / 2)
        img.paste(r, (x, int(y + (cell - r.height) / 2)), r)
    y += cell + 25
img.save(OUT, optimize=True); print(OUT, img.size, os.path.getsize(OUT))
