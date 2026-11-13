"""The Malice Cloak (her design 2026-11-11): a cloak of human leather filled with cursed, void-like energy, so full
of hate it draws the eye. Drawn from the mod's existing torso outline (Modulo Yuji's hoodie, all five bodies),
widened and hung long to the shins like a cloak; stitched leather panels in near-black, and a faint void glow
along the edges. No emblems.

  python3 tools/art_malice_cloak.py [PREVIEW.png]"""
import random, sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parents[1]
SRC = MOD / "Textures/Things/Pawn/Humanlike/Apparel/Sukuna_ModuloHoodie"
NAME = "JJK_MaliceCloak"
OUT = MOD / "Textures/Things/Pawn/Humanlike/Apparel" / NAME
ITEM = MOD / "Textures/Things/Jujutsu/Tools"
BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
DIRS = ("south", "east", "north")
LEATHER, PANEL, SEAM, LINE = (34, 24, 38), (48, 34, 46), (20, 14, 24), (10, 8, 14)
GLOW = (150, 70, 230)


def silhouette(src, d):
    a = src.getchannel("A").point(lambda v: 255 if v > 40 else 0)
    x0, y0, x1, y1 = a.getbbox()
    w, h = x1 - x0, y1 - y0
    m = a.filter(ImageFilter.MaxFilter(5))                      # a little wider than the shirt under it
    dr = ImageDraw.Draw(m)
    bottom = min(124, y1 + int(h * 0.22))
    if d == "east":                                              # side: hangs behind, flaring back
        dr.polygon([(x0 + w * 0.05, y0 + h * 0.35), (x1 - w * 0.1, y0 + h * 0.35), (x1 - w * 0.05, bottom),
                    (x0 - w * 0.18, bottom)], fill=255)
    else:                                                        # front/back: falls wide to the shins
        dr.polygon([(x0, y0 + h * 0.3), (x1, y0 + h * 0.3), (x1 + w * 0.12, bottom), (x0 - w * 0.12, bottom)], fill=255)
    if d == "south":                                             # open at the front: a dark parting down the middle
        cx = (x0 + x1) / 2
        dr.polygon([(cx - 1, y0 + h * 0.18), (cx + 1, y0 + h * 0.18), (cx + w * 0.08, bottom), (cx - w * 0.08, bottom)], fill=0)
    return m.filter(ImageFilter.GaussianBlur(0.6)).point(lambda v: 255 if v > 127 else 0)


def leather(m, seed):
    rnd = random.Random(seed)
    img = Image.new("RGBA", m.size, (0, 0, 0, 0))
    base = Image.new("RGBA", m.size, LEATHER + (255,))
    img.paste(base, mask=m)
    x0, y0, x1, y1 = m.getbbox()
    panels = Image.new("RGBA", m.size, (0, 0, 0, 0)); pd = ImageDraw.Draw(panels)
    for _ in range(9):                                           # stitched-together leather patches
        px, py = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        pw, ph = rnd.uniform(8, 18), rnd.uniform(8, 20)
        pts = [(px + rnd.uniform(-2, 2), py), (px + pw, py + rnd.uniform(-2, 2)), (px + pw + rnd.uniform(-2, 2), py + ph),
               (px, py + ph + rnd.uniform(-2, 2))]
        pd.polygon(pts, fill=PANEL + (255,))
        for i in range(4):                                       # seam stitches along one edge
            t = (i + 0.5) / 4
            sx, sy = pts[0][0] + (pts[1][0] - pts[0][0]) * t, pts[0][1] + (pts[1][1] - pts[0][1]) * t
            pd.line([(sx, sy - 1.5), (sx, sy + 1.5)], fill=SEAM + (255,), width=1)
    panels.putalpha(ImageChops.multiply(panels.getchannel("A"), m))
    img.alpha_composite(panels)
    # void glow just inside the edge
    edge = ImageChops.subtract(m, m.filter(ImageFilter.MinFilter(5)))
    glow = Image.new("RGBA", m.size, GLOW + (0,))
    glow.putalpha(edge.filter(ImageFilter.GaussianBlur(1.5)).point(lambda v: int(v * 0.7)))
    glow.putalpha(ImageChops.multiply(glow.getchannel("A"), m))
    img.alpha_composite(glow)
    # dark outline
    out = Image.new("RGBA", m.size, (0, 0, 0, 0))
    ring = ImageChops.subtract(m.filter(ImageFilter.MaxFilter(3)), m)
    out.paste(LINE + (255,), mask=ring)
    out.alpha_composite(img)
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True); ITEM.mkdir(parents=True, exist_ok=True)
    made = {}
    for b in BODIES:
        for d in DIRS:
            src = Image.open(SRC / f"Sukuna_ModuloHoodie_{b}_{d}.png").convert("RGBA")
            img = leather(silhouette(src, d), hash((b, d)) & 0xffff)
            img.save(OUT / f"{NAME}_{b}_{d}.png")
            made[b, d] = img
    # the item: the cloak lying in a heap, from the south view, squashed
    ic = made["Male", "south"]
    ic = ic.crop(ic.getchannel("A").getbbox())
    heap = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    ic = ic.resize((100, 74), Image.LANCZOS)
    heap.alpha_composite(ic, (14, 30))
    halo = heap.getchannel("A").filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(5)).point(lambda v: int(v * 0.5))
    under = Image.new("RGBA", (128, 128), GLOW + (0,)); under.putalpha(halo)
    under.alpha_composite(heap)
    under.save(ITEM / "MaliceCloak.png")
    # the shade a thrown cloak rises as: the cloak hanging empty in the air, hem fading out, a void glow round it
    shade_dir = MOD / "Textures/Things/Jujutsu/MaliceShade"
    shade_dir.mkdir(parents=True, exist_ok=True)
    shades = {}
    for d in DIRS:
        cl = made["Male", d]
        cl = cl.crop(cl.getchannel("A").getbbox())
        sh = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        cl = cl.resize((int(cl.width * 1.25), int(cl.height * 1.15)), Image.LANCZOS)
        sh.alpha_composite(cl, ((128 - cl.width) // 2, max(0, 104 - cl.height)))
        fade = Image.linear_gradient("L").resize((128, 128)).point(lambda v: 255 - max(0, (v - 150) * 255 // 105))
        sh.putalpha(ImageChops.multiply(sh.getchannel("A"), fade))
        g = sh.getchannel("A").filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(6)).point(lambda v: int(v * 0.55))
        under = Image.new("RGBA", (128, 128), GLOW + (0,)); under.putalpha(g)
        under.alpha_composite(sh)
        under.save(shade_dir / f"MaliceShade_{d}.png")
        shades[d] = under
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (4 * 200, len(BODIES) * 200), (110, 100, 90, 255))
        for j, b in enumerate(BODIES):
            for i, d in enumerate(DIRS):
                sheet.alpha_composite(made[b, d].resize((200, 200), Image.NEAREST), (i * 200, j * 200))
        sheet.alpha_composite(under.resize((200, 200), Image.LANCZOS), (600, 0))
        for i, d in enumerate(DIRS):
            sheet.alpha_composite(shades[d].resize((200, 200), Image.LANCZOS), (600, 200 * (i + 1)))
        sheet.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
