"""Modulo Yuji's hoodie (her note 2026-11-07: adult Yuji wears a hoodie, not the uniform).
Built on vanilla's Parka like the uniform (tools/art_uniform.py): the parka's shape with its zip, the
hood lying on his back, two drawstrings and a front pocket. Drawn in greys, so it takes the apparel's
colour (Modulo Yuji gets charcoal; a styling station can change it).

  python tools/art_hoodie.py PARTS_DIR TEXTURES_DIR [PREVIEW.png]
"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")
NAME = "Sukuna_ModuloHoodie"
LINE = 40   # outline / seam grey


def grey(img):
    """Parka shading lifted to a light grey (the apparel colour multiplies it); outlines stay dark."""
    img = img.convert("RGBA")
    g = img.convert("L").point(lambda v: v if v < 60 else min(235, int(v * 1.25)))
    return Image.merge("RGBA", (g, g, g, img.getchannel("A")))


def details(img, d):
    a = img.getchannel("A")
    x0, y0, x1, y1 = a.getbbox()
    w, h, cx = x1 - x0, y1 - y0, (x0 + x1) / 2
    dr = ImageDraw.Draw(img)
    inside = a.point(lambda v: 255 if v > 0 else 0)
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    od = ImageDraw.Draw(over)
    if d == "south":
        # drawstrings hanging from the neck, and the pocket across the belly
        for s in (-1, 1):
            x = cx + s * w * 0.15
            od.line([(x, y0 + h * 0.22), (x, y0 + h * 0.42)], fill=(225, 225, 225, 255), width=2)
            od.ellipse([x - 2, y0 + h * 0.42 - 1, x + 2, y0 + h * 0.42 + 3], fill=(200, 200, 200, 255), outline=(LINE, LINE, LINE, 255))
        py0, py1 = y0 + h * 0.58, y0 + h * 0.82
        od.polygon([(cx - w * 0.26, py1), (cx - w * 0.2, py0), (cx + w * 0.2, py0), (cx + w * 0.26, py1)],
                   fill=(150, 150, 150, 255), outline=(LINE, LINE, LINE, 255))
    elif d == "east":
        # the hood bunched at the back of the neck, the pocket's edge at the front
        od.ellipse([x0 - w * 0.1, y0 - h * 0.08, x0 + w * 0.5, y0 + h * 0.22], fill=(170, 170, 170, 255), outline=(LINE, LINE, LINE, 255), width=2)
        od.line([(x1 - w * 0.35, y0 + h * 0.6), (x1 - 2, y0 + h * 0.6)], fill=(LINE, LINE, LINE, 255), width=2)
    else:
        # the hood lying on his back
        od.ellipse([cx - w * 0.3, y0 - 6, cx + w * 0.3, y0 + h * 0.36], fill=(170, 170, 170, 255), outline=(LINE, LINE, LINE, 255), width=2)
        od.arc([cx - w * 0.2, y0 - 2, cx + w * 0.2, y0 + h * 0.22], 20, 160, fill=(LINE, LINE, LINE, 255), width=2)
    keep = ImageChops.multiply(over.getchannel("A"), inside)
    over.putalpha(keep)
    img.alpha_composite(over)
    return img


def main():
    parts, tex = Path(sys.argv[1]), Path(sys.argv[2])
    prev = Path(sys.argv[3]) if len(sys.argv) > 3 else None
    worn = tex / "Things/Pawn/Humanlike/Apparel" / NAME
    worn.mkdir(parents=True, exist_ok=True)
    made = {}
    for b in BODIES:
        for d in DIRS:
            img = details(grey(Image.open(parts / f"Parka_{b}_{d}.png")), d)
            img.save(worn / f"{NAME}_{b}_{d}.png")
            made[b, d] = img
    icon_dir = tex / "Things/Item/Apparel" / NAME
    icon_dir.mkdir(parents=True, exist_ok=True)
    ic = made["Male", "south"]
    ic.crop(ic.getchannel("A").getbbox()).resize((64, 64), Image.LANCZOS).save(icon_dir / f"{NAME}.png")
    if prev:
        col = (64, 66, 72)
        sheet = Image.new("RGBA", (3 * 256, len(BODIES) * 256), (110, 110, 110, 255))
        for j, b in enumerate(BODIES):
            for i, d in enumerate(DIRS):
                body = Image.open(parts / f"Naked_{b}_{d}.png").convert("RGBA")
                skin = Image.merge("RGBA", [*[c.point(lambda v, k=k: v * k // 255) for c, k in zip(body.split()[:3], (245, 217, 189))], body.getchannel("A")])
                h = made[b, d]
                h = Image.merge("RGBA", [*[c.point(lambda v, k=k: v * k // 255) for c, k in zip(h.split()[:3], col)], h.getchannel("A")])
                sheet.alpha_composite(Image.alpha_composite(skin, h).resize((256, 256), Image.NEAREST), (i * 256, j * 256))
        sheet.save(prev)


if __name__ == "__main__":
    main()
