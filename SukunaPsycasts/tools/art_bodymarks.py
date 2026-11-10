"""Sukuna's body markings (her note 2026-11-06: more tattoos): stripes under the chest, slanted pairs
down the sides of the stomach, and two bands across the back. One picture per body type, placed
from that body's own outline and clipped to it, so nothing spills off a thin or a fat body.
Drawn as a hediff render node (Sukuna_CurseMarks), not a tattoo: vanilla body tattoos use one
picture for every body type.

Needs vanilla's Naked_<Body>_<dir>.png (extract from RimWorldLinux_Data/resources.assets).

  python tools/art_bodymarks.py PARTS_DIR TEXTURES_DIR [PREVIEW.png]
"""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

K = 4
INK = (24, 18, 20, 255)
BODIES = ("Male", "Female", "Thin", "Fat", "Hulk")
NAME = "Sukuna_CurseMarks"


def qbez(p0, p1, p2, n=16):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]


def marks(body):
    a = body.getchannel("A")
    x0, y0, x1, y1 = a.getbbox()
    w, h = x1 - x0, y1 - y0
    W, H = body.size
    img = Image.new("RGBA", (W * K, H * K), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    lw = max(7, int(w / 52 * 3.0 * K))
    P = lambda pts: [(x * K, y * K) for x, y in pts]
    return img, dr, lw, P, (x0, y0, x1, y1, w, h)


def south(body):
    img, dr, lw, P, (x0, y0, x1, y1, w, h) = marks(body)
    cx = (x0 + x1) / 2
    for s in (-1, 1):
        # under each side of the chest: a curved stripe from the middle out toward the arm
        a, b = (cx + s * 0.05 * w, y0 + 0.42 * h), (cx + s * 0.38 * w, y0 + 0.37 * h)
        dr.line(P(qbez(a, (cx + s * 0.22 * w, y0 + 0.47 * h), b)), fill=INK, width=lw, joint="curve")
        # two slanted stripes down each side of the stomach
        for k in (0.0, 0.11):
            dr.line(P([(cx + s * 0.40 * w, y0 + (0.55 + k) * h), (cx + s * 0.20 * w, y0 + (0.61 + k) * h)]), fill=INK, width=lw)
    return img, body


def north(body):
    img, dr, lw, P, (x0, y0, x1, y1, w, h) = marks(body)
    cx = (x0 + x1) / 2
    for s in (-1, 1):
        for k in (0.0, 0.10):   # two bands across the back, broken at the spine
            dr.line(P(qbez((cx + s * 0.05 * w, y0 + (0.44 + k) * h), (cx + s * 0.22 * w, y0 + (0.47 + k) * h),
                           (cx + s * 0.42 * w, y0 + (0.42 + k) * h))), fill=INK, width=lw, joint="curve")
    return img, body


def east(body):
    img, dr, lw, P, (x0, y0, x1, y1, w, h) = marks(body)
    # facing east: the chest stripe at the front edge, the stomach pairs along the side
    dr.line(P(qbez((x1 - 0.02 * w, y0 + 0.42 * h), (x1 - 0.20 * w, y0 + 0.46 * h), (x1 - 0.42 * w, y0 + 0.39 * h))), fill=INK, width=lw, joint="curve")
    for k in (0.0, 0.11):
        dr.line(P([(x1 - 0.50 * w, y0 + (0.56 + k) * h), (x1 - 0.20 * w, y0 + (0.61 + k) * h)]), fill=INK, width=lw)
    return img, body


def finish(img, body):
    big_alpha = body.getchannel("A").resize(img.size, Image.NEAREST).point(lambda v: 255 if v > 200 else 0)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), big_alpha))
    return img.resize(body.size, Image.LANCZOS)


def main():
    parts, tex = Path(sys.argv[1]), Path(sys.argv[2])
    prev = Path(sys.argv[3]) if len(sys.argv) > 3 else None
    out = tex / "Things/Pawn/Humanlike" / NAME
    out.mkdir(parents=True, exist_ok=True)
    made = {}
    for b in BODIES:
        for d, fn in (("south", south), ("east", east), ("north", north)):
            body = Image.open(parts / f"Naked_{b}_{d}.png").convert("RGBA")
            img = finish(*fn(body))
            img.save(out / f"{NAME}_{b}_{d}.png")
            made[b, d] = (body, img)
    if prev:
        skin = (236, 200, 172)
        sheet = Image.new("RGBA", (3 * 200, len(BODIES) * 200), (70, 70, 70, 255))
        for j, b in enumerate(BODIES):
            for i, d in enumerate(("south", "east", "north")):
                body, img = made[b, d]
                r, g, bl, a = body.split()
                tinted = Image.merge("RGBA", (*[Image.eval(c, lambda v, k=k: v * k // 255) for c, k in zip((r, g, bl), skin)], a))
                sheet.alpha_composite(Image.alpha_composite(tinted, img).resize((200, 200), Image.NEAREST), (i * 200, j * 200))
        sheet.save(prev)


if __name__ == "__main__":
    main()
