"""Yuji's school uniform (her note 2026-11-06: "he has a red scarf and his coat is closed").
Built on vanilla's Parka (closed, zip down the front, raised collar): navy coat, the collar and
the hood lying on his back red. Colours are baked in (the apparel is drawn untinted).

The Parka pictures are vanilla's own; they are not shipped in the repo. Extract
Parka_<Body>_<dir>.png from RimWorldLinux_Data/resources.assets with UnityPy first.

  python tools/art_uniform.py PARTS_DIR TEXTURES_DIR [PREVIEW.png]
"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")
NAVY = (30, 34, 58)
RED = (196, 40, 48)
NAME = "Sukuna_YujiUniform"


def holes(alpha):
    """Transparent pixels shut inside the garment (the neck opening), not open to the outside."""
    a = alpha.point(lambda v: 255 if v > 60 else 0)
    outside = a.copy()
    ImageDraw.floodfill(outside, (0, 0), 128)
    return outside.point(lambda v: 255 if v == 0 else 0)


def scarf_mask(img, d):
    alpha = img.getchannel("A")
    x0, y0, x1, y1 = alpha.getbbox()
    w = x1 - x0
    m = Image.new("L", img.size, 0)
    dr = ImageDraw.Draw(m)
    if d == "south":
        # a thick band around the neck opening, plus the collar's top edge
        m = holes(alpha).filter(ImageFilter.MaxFilter(11))
        ImageDraw.Draw(m).rectangle([x0, y0, x1, y0 + 7], fill=255)
    elif d == "east":
        # the collar from the side: the top of the coat, deeper toward the front (right)
        dr.polygon([(x0, y0), (x1, y0), (x1, y0 + 20), (x0 + w * 0.55, y0 + 16), (x0, y0 + 10)], fill=255)
    else:
        # the hood lying on his back: a rounded flap below the collar
        cx = (x0 + x1) / 2
        dr.rectangle([x0, y0, x1, y0 + 6], fill=255)
        dr.ellipse([cx - w * 0.3, y0 - 6, cx + w * 0.3, y0 + 22], fill=255)
    return ImageChops.multiply(m, alpha.point(lambda v: 255 if v > 0 else 0))


def colour(img, d):
    """Vanilla's grey shading times the cloth colour; black outlines stay black."""
    img = img.convert("RGBA")
    grey = img.convert("L")
    mask = scarf_mask(img, d)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    po, pg, pm, pa = out.load(), grey.load(), mask.load(), img.getchannel("A").load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            a = pa[x, y]
            if not a:
                continue
            v = pg[x, y] / 255
            col = RED if pm[x, y] > 127 else NAVY
            # keep the outline: dark source pixels stay dark
            k = v if v > 0.3 else v * 0.5
            lift = 1.25 if col is RED else 1.35
            po[x, y] = tuple(min(255, int(c * k * lift)) for c in col) + (a,)
    return out


SLIM = 0.9   # her note 2026-11-07: Yuji a bit slimmer; vanilla's Parka sits ~6 px wider than the body
INK = (14, 12, 16, 255)
RED_DARK = (120, 22, 30, 255)
RED_LIGHT = (214, 52, 60, 255)


def slim(img):
    w, h = img.size
    nw = int(round(w * SLIM))
    small = img.resize((nw, h), Image.LANCZOS)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(small, ((w - nw) // 2, 0))
    return out


def hood(img, d):
    """The red hood of the hoodie he wears under the coat (her note 2026-11-07: he should have a hood),
    lying down: on his back from behind, a bump at the back of the neck from the side, and its rim
    showing behind his neck from the front."""
    x0, y0, x1, y1 = img.getchannel("A").getbbox()
    w = x1 - x0
    cx = (x0 + x1) / 2
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    if d == "north":
        pts = [(cx - w * 0.30, y0 + 1), (cx + w * 0.30, y0 + 1), (cx + w * 0.27, y0 + 14), (cx + w * 0.12, y0 + 26),
               (cx, y0 + 29), (cx - w * 0.12, y0 + 26), (cx - w * 0.27, y0 + 14)]
        dr.polygon(pts, fill=RED, outline=INK)
        dr.line(pts + [pts[0]], fill=INK, width=2, joint="curve")
        dr.arc([cx - w * 0.24, y0 - 6, cx + w * 0.24, y0 + 12], 20, 160, fill=RED_DARK, width=3)    # opening
        dr.line([(cx, y0 + 13), (cx, y0 + 26)], fill=RED_DARK, width=1)                            # seam
        img = Image.alpha_composite(img, layer)
    elif d == "east":
        # lying against the back of his neck: find where the coat's top edge is, behind the collar
        al = img.getchannel("A")
        top = lambda x: next((y for y in range(img.size[1]) if al.getpixel((x, y)) > 60), y1)
        bx = x0 + 3
        by = top(bx + 6)
        pts = [(bx, by + 2), (bx + 5, by - 3), (bx + 13, by - 2), (bx + 16, by + 5), (bx + 12, by + 14), (bx + 4, by + 16), (bx, by + 10)]
        dr.polygon(pts, fill=RED)
        dr.line(pts + [pts[0]], fill=INK, width=2, joint="curve")
        dr.arc([bx + 3, by - 1, bx + 15, by + 11], 200, 340, fill=RED_DARK, width=2)
        img = Image.alpha_composite(img, layer)
    # front: the red band round the neck already reads as the hood's edge
    return img


def main():
    parts, tex = Path(sys.argv[1]), Path(sys.argv[2])
    prev = Path(sys.argv[3]) if len(sys.argv) > 3 else None
    worn = tex / "Things/Pawn/Humanlike/Apparel" / NAME
    worn.mkdir(parents=True, exist_ok=True)
    made = {}
    for b in BODIES:
        for d in DIRS:
            img = hood(slim(colour(Image.open(parts / f"Parka_{b}_{d}.png"), d)), d)
            img.save(worn / f"{NAME}_{b}_{d}.png")
            made[b, d] = img
    # the item on the ground: the Male front view
    icon_dir = tex / "Things/Item/Apparel" / NAME
    icon_dir.mkdir(parents=True, exist_ok=True)
    ic = made["Male", "south"]
    ic.crop(ic.getchannel("A").getbbox()).resize((64, 64), Image.LANCZOS).save(icon_dir / f"{NAME}.png")
    if prev:
        sheet = Image.new("RGBA", (3 * 256, len(BODIES) * 256), (70, 70, 70, 255))
        for j, b in enumerate(BODIES):
            for i, d in enumerate(DIRS):
                body = Image.open(parts / f"Naked_{b}_{d}.png").convert("RGBA")
                skin = Image.merge("RGBA", [*[c.point(lambda v, k=k: v * k // 255) for c, k in zip(body.split()[:3], (245, 217, 189))], body.getchannel("A")])
                sheet.alpha_composite(Image.alpha_composite(skin, made[b, d]).resize((256, 256), Image.NEAREST), (i * 256, j * 256))
        sheet.save(prev)


if __name__ == "__main__":
    main()
