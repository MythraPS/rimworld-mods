# Workshop preview: the 22 everyday carpets as they appear in the photos, in a plain grid (1280x720).
#   python3 Source/make_preview_photo.py
import json, os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from make_rugs_photo import cut_out
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
picks = json.load(open(os.path.join(root, "Source/carpet_sources.json")))
W, H, cols, rows, gap = 1280, 720, 11, 2, 8
cw, ch = (W - gap * (cols + 1)) // cols, (H - gap * (rows + 1)) // rows
sheet = Image.new("RGB", (W, H), (34, 30, 28))
for i, p in enumerate(picks):
    im = Image.open(p["file"]).convert("RGB"); im.thumbnail((1200, 1200))
    rug, _ = cut_out(im)
    if rug.width > rug.height: rug = rug.rotate(90, expand=True)
    rug.thumbnail((cw, ch), Image.LANCZOS)
    x = gap + (i % cols) * (cw + gap) + (cw - rug.width) // 2
    y = gap + (i // cols) * (ch + gap) + (ch - rug.height) // 2
    sheet.paste(rug, (x, y))
sheet.save(os.path.join(root, "About/Preview.png"))
print("About/Preview.png", sheet.size)
