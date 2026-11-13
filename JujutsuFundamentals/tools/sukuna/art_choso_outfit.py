"""Choso's clothes (2026-11-11): light, baggy, long-sleeved, with dark purple sashes crossed in an X over the body,
one round the neck and one round the waist (canon, from a fan wiki). The 1.7 game files can't be read yet, so the
shape is the parka outline from Modulo Yuji's hoodie (all five bodies), refilled plain light with its own outline,
and the sashes drawn on in purple. The apparel's colour multiplies it (Choso: white).

  python tools/sukuna/art_choso_outfit.py [PREVIEW.png]"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parents[2]
SRC = MOD / "Textures/Things/Pawn/Humanlike/Apparel/Sukuna_ModuloHoodie"
NAME = "Choso_Outfit"
OUT = MOD / "Textures/Things/Pawn/Humanlike/Apparel" / NAME
BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")
LIGHT, LINE = (236, 236, 236, 255), (48, 44, 50, 255)
SASH, SASH_EDGE = (64, 28, 84, 255), (34, 14, 46, 255)    # dark purple (her call 2026-11-11: white and dark purple)


def base(src):
    a = src.getchannel("A").point(lambda v: 255 if v > 40 else 0)
    inner = a.filter(ImageFilter.MinFilter(5))
    img = Image.new("RGBA", src.size, (0, 0, 0, 0))
    img.paste(LINE, mask=a)
    img.paste(LIGHT, mask=inner)
    # soft shading toward the sides, like vanilla folds
    shade = inner.filter(ImageFilter.GaussianBlur(6)).point(lambda v: 255 - v)
    dark = Image.new("RGBA", src.size, (205, 205, 205, 255))
    img.paste(dark, mask=ImageChops.multiply(shade, inner).point(lambda v: min(255, v * 2)))
    return img, a, inner


def band(dr, p0, p1, w):
    dr.line([p0, p1], fill=SASH_EDGE, width=w + 2)
    dr.line([p0, p1], fill=SASH, width=w)


def sashes(img, inner, d):
    x0, y0, x1, y1 = inner.getbbox()
    w, h = x1 - x0, y1 - y0
    over = Image.new("RGBA", img.size, (0, 0, 0, 0)); dr = ImageDraw.Draw(over)
    sw = max(3, int(w * 0.09))
    if d in ("south", "north"):
        top, low = y0 + h * 0.12, y0 + h * 0.62
        band(dr, (x0 + w * 0.12, top), (x1 - w * 0.12, low), sw)          # the X
        band(dr, (x1 - w * 0.12, top), (x0 + w * 0.12, low), sw)
        band(dr, (x0, y0 + h * 0.66), (x1, y0 + h * 0.66), sw)             # waist
        band(dr, (x0 + w * 0.3, y0 + h * 0.04), (x1 - w * 0.3, y0 + h * 0.04), sw)   # neck
    else:
        band(dr, (x0 + w * 0.2, y0 + h * 0.1), (x1 - w * 0.15, y0 + h * 0.6), sw)
        band(dr, (x0, y0 + h * 0.66), (x1, y0 + h * 0.66), sw)
        band(dr, (x0 + w * 0.25, y0 + h * 0.04), (x1 - w * 0.25, y0 + h * 0.04), sw)
    over.putalpha(ImageChops.multiply(over.getchannel("A"), inner))
    img.alpha_composite(over)
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    made = {}
    for b in BODIES:
        for d in DIRS:
            img, a, inner = base(Image.open(SRC / f"Sukuna_ModuloHoodie_{b}_{d}.png").convert("RGBA"))
            img = sashes(img, inner, d)
            img.save(OUT / f"{NAME}_{b}_{d}.png")
            made[b, d] = img
    icon_dir = MOD / "Textures/Things/Item/Apparel" / NAME
    icon_dir.mkdir(parents=True, exist_ok=True)
    ic = made["Male", "south"]
    ic.crop(ic.getchannel("A").getbbox()).resize((64, 64), Image.LANCZOS).save(icon_dir / f"{NAME}.png")
    if len(sys.argv) > 1:
        cream = (219, 212, 194)
        sheet = Image.new("RGBA", (3 * 200, len(BODIES) * 200), (110, 110, 110, 255))
        for j, b in enumerate(BODIES):
            for i, d in enumerate(DIRS):
                h = made[b, d]
                h = Image.merge("RGBA", [*[c.point(lambda v, k=k: v * k // 255) for c, k in zip(h.split()[:3], cream)], h.getchannel("A")])
                sheet.alpha_composite(h.resize((200, 200), Image.NEAREST), (i * 200, j * 200))
        sheet.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
