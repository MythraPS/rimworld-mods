"""Inumaki's high collar (her call 2026-11-12: his lower face covered, the Cursed Speech crest hidden under it): his
navy uniform collar zipped up over his mouth, below his eyes. Drawn on the vanilla Male_Average_Normal head (Inumaki
gets that head), as a head layer above the face. Colours baked in (drawn untinted).

  python tools/art_inumaki_collar.py HEADS_DIR [PREVIEW.png]"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parent.parent
OUT = MOD / "Textures/Things/Jujutsu/Inumaki"
S = 4
NAVY, NAVY_LIGHT, INK = (31, 33, 47, 255), (58, 62, 84, 255), (16, 16, 20, 255)
TOP = {"south": 74.0, "east": 74.5, "north": 80.0}     # under his eyes (rows 67-73); lower at the back of the neck


def collar(heads, d):
    head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA").getchannel("A")
    head = head.resize((128 * S, 128 * S), Image.LANCZOS).point(lambda v: 255 if v > 100 else 0)
    wide = head.filter(ImageFilter.MaxFilter(2 * 2 * S + 1))          # a little proud of his face, like cloth
    band = Image.new("L", head.size, 0)
    ImageDraw.Draw(band).rectangle([(0, TOP[d] * S), (128 * S, 128 * S)], fill=255)
    region = ImageChops.multiply(wide, band)
    outline = region.filter(ImageFilter.MaxFilter(2 * int(1.5 * S) + 1))
    img = Image.new("RGBA", head.size, (0, 0, 0, 0))
    img.paste(INK, mask=outline)
    img.paste(NAVY, mask=region)
    dr = ImageDraw.Draw(img)
    bb = region.getbbox()
    if bb and d != "north":
        cx = (bb[0] + bb[2]) / 2 if d == "south" else bb[2] - 6 * S
        dr.line([(cx, bb[1] + 1.5 * S), (cx, bb[3])], fill=NAVY_LIGHT, width=int(0.9 * S))      # the zip
        dr.line([(bb[0] + 2 * S, bb[1] + 1.2 * S), (bb[2] - 2 * S, bb[1] + 1.2 * S)], fill=NAVY_LIGHT, width=int(0.7 * S))   # the collar's top fold
    return img.resize((128, 128), Image.LANCZOS)


def main():
    heads = Path(sys.argv[1])
    OUT.mkdir(parents=True, exist_ok=True)
    out = []
    for d in ("south", "east", "north"):
        im = collar(heads, d)
        im.save(OUT / f"Collar_{d}.png")
        out.append((d, im))
    if len(sys.argv) > 2:
        prev = Image.new("RGBA", (128 * 3, 128), (90, 90, 100, 255))
        for i, (d, im) in enumerate(out):
            prev.alpha_composite(Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA"), (128 * i, 0))
            prev.alpha_composite(Image.open(MOD / f"Textures/Things/Pawn/Humanlike/Hairs/JJK_InumakiHair_{d}.png").convert("RGBA"), (128 * i, 0))
            prev.alpha_composite(im, (128 * i, 0))
        prev.resize((128 * 9, 384), Image.NEAREST).save(sys.argv[2])
    print("ok")


if __name__ == "__main__":
    main()
