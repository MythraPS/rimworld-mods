"""Young Gojo's round sunglasses (her call 2026-11-07: young Gojo wears them instead of the
blindfold). Small, round, near-black lenses with a thin frame and a faint glint, on the same
measured head as the blindfold (vanilla Male_Average_Normal: eyes x 51-59 and 68-76, y 67-72
facing south; x 74-81 facing east). Nothing shows from behind but the arms over the ears.

  python tools/art_sunglasses.py OUT_TEXTURES_DIR [HEADS_DIR PREVIEW.png]
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

K = 8
N = 128 * K
FRAME = (14, 14, 18, 255)
LENS = (22, 20, 30, 245)
GLINT = (150, 170, 210, 200)


def ring(dr, cx, cy, r, w):
    dr.ellipse(((cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K), fill=FRAME)
    dr.ellipse(((cx - r + w) * K, (cy - r + w) * K, (cx + r - w) * K, (cy + r - w) * K), fill=LENS)
    g = r * 0.38
    dr.ellipse(((cx - r * 0.55 - g / 2) * K, (cy - r * 0.5 - g / 2) * K, (cx - r * 0.55 + g / 2) * K, (cy - r * 0.5 + g / 2) * K), fill=GLINT)


def line(dr, pts, w):
    dr.line([(x * K, y * K) for x, y in pts], fill=FRAME, width=int(w * K))


def draw(d):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    if d == "south":
        line(dr, [(41, 68.5), (49.5, 69)], 1.7)          # arms to the ears
        line(dr, [(78.5, 69), (87, 68.5)], 1.7)
        line(dr, [(59.5, 68.6), (63.5, 67.8), (67.5, 68.6)], 1.7)   # the bridge
        ring(dr, 55, 69.5, 5.6, 1.3)
        ring(dr, 72, 69.5, 5.6, 1.3)
    elif d == "east":
        line(dr, [(56, 68), (74, 68.6)], 1.7)              # the arm back to the ear
        ring(dr, 79, 69.5, 5.0, 1.3)
    else:
        line(dr, [(40.5, 66), (40.5, 70)], 1.8)            # just the arms over the ears
        line(dr, [(87.5, 66), (87.5, 70)], 1.8)
    return img.resize((128, 128), Image.LANCZOS)


def main():
    out = Path(sys.argv[1]) / "Things/Pawn/Humanlike/Apparel/Gojo_Sunglasses"
    out.mkdir(parents=True, exist_ok=True)
    made = {}
    for d in ("south", "east", "north"):
        made[d] = draw(d)
        made[d].save(out / f"Gojo_Sunglasses_{d}.png")
    # the item on the ground: the front view, cropped and enlarged
    item = Path(sys.argv[1]) / "Things/Item/Apparel/Gojo_Sunglasses"
    item.mkdir(parents=True, exist_ok=True)
    s = made["south"]; s.crop((36, 58, 92, 80)).resize((112, 44), Image.LANCZOS)
    icon = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    icon.alpha_composite(s.crop((36, 58, 92, 80)).resize((112, 44), Image.LANCZOS), (8, 42))
    icon.save(item / "Gojo_Sunglasses.png")
    if len(sys.argv) > 3:
        heads = Path(sys.argv[2])
        sheet = Image.new("RGBA", (128 * 3, 128), (120, 120, 120, 255))
        for c, d in enumerate(("south", "east", "north")):
            head = Image.open(heads / f"Male_Average_Normal_{d}.png").convert("RGBA")
            r, g, b, a = head.split()
            skin = Image.merge("RGBA", [x.point(lambda v, k=k: v * k // 255) for x, k in zip((r, g, b), (236, 206, 180))] + [a])
            hair = Image.open(Path(sys.argv[1]) / f"Things/Pawn/Humanlike/Hairs/Gojo_Hair_{d}.png").convert("RGBA")
            hr, hg, hb, ha = hair.split()
            hair = Image.merge("RGBA", [x.point(lambda v, k=k: v * k // 255) for x, k in zip((hr, hg, hb), (245, 247, 255))] + [ha])
            sheet.alpha_composite(Image.alpha_composite(Image.alpha_composite(skin, made[d]), hair), (c * 128, 0))
        sheet.resize((128 * 9, 128 * 3), Image.NEAREST).save(sys.argv[3])


main()
