"""Yuta's shirt with his dark trousers (her note 2026-11-12: "Yuta wears dark pants"). RimWorld never draws trousers on
a pawn, and the white shirt covers him to the feet, so his legs looked white. This is vanilla's button-down shirt
(ShirtButton) white down to where vanilla's jacket ends, and dark below it: the jacket hides the join, and what shows
underneath reads as his dark trousers. Colours are baked in; the apparel is drawn untinted.

The vanilla pictures aren't shipped: decode ShirtButton_* and Jacket_* from Assets.zip into
~/.local/share/rimworld-tools/parts with ~/.local/share/rimworld-tools/waylith_texture.py first.

  python tools/yuta/art_yuta_shirt.py [PREVIEW.png]"""
import sys
from pathlib import Path
from PIL import Image

MOD = Path(__file__).resolve().parents[2]
PARTS = Path.home() / ".local/share/rimworld-tools/parts"
OUT = MOD / "Textures/Things/Pawn/Humanlike/Apparel/JJK_YutaShirt"
BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")
WHITE = (236, 234, 228)
DARK = (44, 42, 46)        # his trousers


def make(body, d):
    shirt = Image.open(PARTS / f"ShirtButton_{body}_{d}.png").convert("RGBA")
    jacket = Image.open(PARTS / f"Jacket_{body}_{d}.png").convert("RGBA").getchannel("A")
    jb = jacket.getbbox()
    hem_all = jb[3] - 2 if jb else 96
    out = Image.new("RGBA", shirt.size, (0, 0, 0, 0))
    sp, op, jp = shirt.load(), out.load(), jacket.load()
    # the jacket's bottom edge per column; across its open front (where only the collar is) carry the edge over
    # from the panels either side
    bottoms = {}
    for x in range(shirt.width):
        col = [y for y in range(shirt.height) if jp[x, y] > 40]
        if col and max(col) >= hem_all - 14:
            bottoms[x] = max(col)
    xs = sorted(bottoms)

    def hem_at(x):
        if not xs:
            return hem_all
        if x in bottoms:
            return bottoms[x] - 2                     # just above the edge, so the jacket hides the join
        left = [k for k in xs if k < x]; right = [k for k in xs if k > x]
        if left and right:
            a_, b_ = left[-1], right[0]
            t = (x - a_) / (b_ - a_)
            return bottoms[a_] * (1 - t) + bottoms[b_] * t - 2
        return hem_all
    for x in range(shirt.width):
        hem = hem_at(x)
        for y in range(shirt.height):
            r, g, b, a = sp[x, y]
            if a == 0:
                continue
            c = WHITE if y <= hem else DARK
            k = r / 255.0
            op[x, y] = (int(c[0] * k), int(c[1] * k), int(c[2] * k), a)
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    made = {}
    for body in BODIES:
        for d in DIRS:
            made[(body, d)] = make(body, d)
            made[(body, d)].save(OUT / f"JJK_YutaShirt_{body}_{d}.png")
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (128 * 3, 128), (110, 110, 120, 255))
        for i, d in enumerate(DIRS):
            sheet.alpha_composite(made[("Thin", d)], (128 * i, 0))
            sheet.alpha_composite(Image.open(PARTS / f"Jacket_Thin_{d}.png").convert("RGBA"), (128 * i, 0))
        sheet.resize((128 * 3 * 3, 384), Image.NEAREST).save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
