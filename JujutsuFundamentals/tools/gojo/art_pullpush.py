"""Pull and Push icons (2026-11-04): the Blue / Red picture shrunk in the middle,
four white arrows pointing in (pull) or out (push)."""
import math, os
from PIL import Image, ImageDraw
D = os.path.join(os.path.dirname(__file__), "..", "..", "Textures", "Abilities", "Gojo")
def make(src, out, inward):
    base = Image.open(os.path.join(D, src)).convert("RGBA")
    im = Image.new("RGBA", base.size, (0, 0, 0, 0))
    w = base.size[0]; small = base.resize((int(w * 0.55),) * 2, Image.LANCZOS)
    o = (w - small.size[0]) // 2; im.alpha_composite(small, (o, o))
    d = ImageDraw.Draw(im); c = w / 2
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        ux, uy = math.cos(a), math.sin(a)
        r0, r1 = w * 0.30, w * 0.47
        tip, tail = (r0, r1) if inward else (r1, r0)
        tx, ty = c + ux * tip, c + uy * tip
        d.line([(c + ux * tail, c + uy * tail), (tx, ty)], fill=(255, 255, 255, 255), width=7)
        s = 1 if inward else -1   # head points toward the tip
        bx, by = tx + ux * s * 14, ty + uy * s * 14
        px, py = -uy * 11, ux * 11
        d.polygon([(tx - ux * s * 4, ty - uy * s * 4), (bx + px, by + py), (bx - px, by - py)], fill=(255, 255, 255, 255))
    im.save(os.path.join(D, out))
make("Blue.png", "Pull.png", True)
make("Red.png", "Push.png", False)
