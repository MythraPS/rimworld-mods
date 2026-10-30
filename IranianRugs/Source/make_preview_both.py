# Workshop preview for 1.4.0 (1280x720): top two rows, the 22 photo carpets (as make_preview_photo.py); bottom two rows,
# 22 of the 35 drawn designs (11 grand, then 11 large, one size per row), each tinted by a different material the way the game shows them
# (as make_preview.py).
#   ~/.venvs/rugs/bin/python Source/make_preview_both.py
import glob, os
from PIL import Image, ImageChops
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
T = os.path.join(root, "Textures", "IranianRugs")
STUFF = [(205, 192, 168), (160, 45, 45), (95, 70, 160), (150, 190, 215), (140, 95, 60), (90, 130, 90), (200, 170, 90)]
W, H, cols, gap = 1280, 720, 11, 8
cw, ch = (W - gap * (cols + 1)) // cols, (H - gap * 5) // 4

def tex(path, col=None):
    t = Image.open(path).convert("RGBA")
    if col:
        m = Image.open(path[:-4] + "_m.png").convert("RGBA")
        mul = ImageChops.multiply(t.convert("RGB"), Image.new("RGB", t.size, col))
        red = m.getchannel("R").point(lambda v: 255 if v > 127 else 0)
        rgb = Image.composite(mul, t.convert("RGB"), red); rgb.putalpha(t.getchannel("A")); t = rgb
    t = t.crop(t.getbbox())
    return t.rotate(90, expand=True) if t.width > t.height else t

def plain(d): return sorted(f for f in glob.glob(f"{T}/{d}/*.png") if not f.endswith("_m.png"))
photos = plain("Large/Photo")
drawn = [(f, STUFF[i % len(STUFF)]) for i, f in enumerate(plain("Large/Drawn")[:11] + plain("Medium/Drawn")[:11])]
assert len(photos) == 22 and len(drawn) == 22, (len(photos), len(drawn))
sheet = Image.new("RGB", (W, H), (34, 30, 28))
for i, (f, col) in enumerate([(p, None) for p in photos] + drawn):
    r = tex(f, col); r.thumbnail((cw, ch), Image.LANCZOS)
    x = gap + (i % cols) * (cw + gap) + (cw - r.width) // 2
    y = gap + (i // cols) * (ch + gap) + (ch - r.height) // 2
    sheet.paste(r, (x, y), r)
out = os.path.join(root, "About/Preview.png")
sheet.save(out, optimize=True); print(out, sheet.size, os.path.getsize(out))
