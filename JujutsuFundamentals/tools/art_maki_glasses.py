"""Maki's glasses (her call 2026-11-11: non-awakened Maki wears glasses). Canon: the cursed-spirit-seeing glasses she
needs before her Heavenly Restriction is complete; in her second year (the main story) they are square, under-rim
glasses (frame along the bottom of the lenses only), clear lenses. Drawn on the same measured head as Gojo's
sunglasses (vanilla Male_Average_Normal: eyes x 51-59 and 68-76, y 67-72 facing south; x 74-81 facing east).
Nothing shows from behind but the arms over the ears.

  python tools/art_maki_glasses.py [PREVIEW.png]
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

MOD = Path(__file__).resolve().parents[1]
HEADS = MOD / "tools/gojo/pawnparts"
K = 8
N = 128 * K
FRAME = (18, 18, 22, 255)
LENS = (205, 225, 240, 60)        # clear glass, a faint tint
TOP = (18, 18, 22, 150)            # under-rim: only a faint line where the lens edge catches the light
GLINT = (235, 245, 255, 170)


def box(dr, x0, y0, x1, y1, fill):
    dr.rectangle((x0 * K, y0 * K, x1 * K, y1 * K), fill=fill)


def lens(dr, x0, x1, y0=65.8, y1=72.8):
    box(dr, x0, y0, x1, y1, LENS)
    box(dr, x0, y0, x1, y0 + 0.7, TOP)                       # faint top edge
    box(dr, x0 - 0.4, y1 - 2.2, x0 + 0.8, y1, FRAME)         # the frame curls up a little at the sides
    box(dr, x1 - 0.8, y1 - 2.2, x1 + 0.4, y1, FRAME)
    box(dr, x0 - 0.4, y1 - 0.5, x1 + 0.4, y1 + 1.5, FRAME)   # the under-rim
    box(dr, x0 + 1.0, y0 + 1.0, x0 + 2.2, y0 + 2.0, GLINT)


def line(dr, pts, w, fill=FRAME):
    dr.line([(x * K, y * K) for x, y in pts], fill=fill, width=int(w * K))


def draw(d):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    if d == "south":
        line(dr, [(41, 67.5), (50, 67.2)], 1.4)          # arms to the ears
        line(dr, [(78, 67.2), (87, 67.5)], 1.4)
        line(dr, [(59.6, 67.6), (68.4, 67.6)], 1.2)      # the bridge
        lens(dr, 50.2, 59.6)
        lens(dr, 68.4, 77.8)
    elif d == "east":
        line(dr, [(56, 67.2), (75, 67.4)], 1.4)          # the arm back to the ear
        lens(dr, 75.5, 81.5)
    else:
        line(dr, [(40.5, 65.5), (40.5, 69.5)], 1.6)      # just the arms over the ears
        line(dr, [(87.5, 65.5), (87.5, 69.5)], 1.6)
    return img.resize((128, 128), Image.LANCZOS)


# Maki's own head and body (her look fixes 2026-11-12): Looks sets her head to Female_AverageNormal, and the art is
# clipped to that head's (and the female body's) outline, a little inside it, so nothing pokes out past her face.
# Vanilla textures decoded from Assets.zip into ~/.local/share/rimworld-tools/heads (not shipped).
PARTS = Path.home() / ".local/share/rimworld-tools/heads"


def clip(img, part, shrink):
    from PIL import ImageChops, ImageFilter
    m = Image.open(PARTS / f"{part}.png").getchannel("A").point(lambda v: 255 if v > 100 else 0)
    if m.size != img.size:
        m = m.resize(img.size, Image.NEAREST)
    if shrink:
        m = m.filter(ImageFilter.MinFilter(2 * shrink * (img.width // 128) + 1))
    img.putalpha(ImageChops.multiply(img.getchannel("A"), m))
    return img


def main():
    out = MOD / "Textures/Things/Pawn/Humanlike/Apparel/JJK_MakiGlasses"
    out.mkdir(parents=True, exist_ok=True)
    made = {}
    for d in ("south", "east", "north"):
        made[d] = clip(draw(d), f"Female_Average_Normal_{d}", 2)
        made[d].save(out / f"JJK_MakiGlasses_{d}.png")
    item = MOD / "Textures/Things/Item/Apparel/JJK_MakiGlasses"
    item.mkdir(parents=True, exist_ok=True)
    icon = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    icon.alpha_composite(made["south"].crop((38, 60, 90, 76)).resize((116, 36), Image.LANCZOS), (6, 46))
    icon.save(item / "JJK_MakiGlasses.png")
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (128 * 2, 128), (120, 120, 120, 255))
        head = Image.open(HEADS / "Male_Average_Normal_south.png").convert("RGBA")
        r, g, b, a = head.split()
        skin = Image.merge("RGBA", [x.point(lambda v, k=k: v * k // 255) for x, k in zip((r, g, b), (240, 212, 186))] + [a])
        sheet.alpha_composite(Image.alpha_composite(skin, made["south"]), (0, 0))
        sheet.alpha_composite(icon, (128, 0))
        sheet.resize((128 * 2 * 4, 128 * 4), Image.NEAREST).save(sys.argv[1])


main()
