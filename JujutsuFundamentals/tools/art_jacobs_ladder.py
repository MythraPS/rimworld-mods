"""Jacob's Ladder (her design 2026-11-11). Canon: Hana conjures a four-winged trumpet of light, a magic circle appears
in the sky and a pillar of light falls on the target.
Outputs: Textures/Abilities/Yuta/Copy/JacobsLadder.png (icon, the mod's dark-square style via art_rika.icon),
         Textures/Things/Angel/JacobsLadderBeam.png (the pillar fleck, soft gold-white, fading at both ends).
  python3 tools/art_jacobs_ladder.py [PREVIEW.png]"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "yuta"))
import art_rika as R   # noqa: E402

MOD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GOLD, WHITE = (255, 226, 140), (255, 252, 236)


def beam():
    W, H = 128, 512
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for x in range(W):
        dx = abs(x - W / 2) / (W / 2)
        a_x = max(0.0, 1 - dx ** 1.6)
        for y in range(H):
            t = y / H
            a_y = min(1.0, t / 0.12) * min(1.0, (1 - t) / 0.12)
            a = a_x * a_y
            col = tuple(int(GOLD[i] + (WHITE[i] - GOLD[i]) * (1 - dx) ** 3) for i in range(3))
            img.putpixel((x, y), col + (int(255 * a),))
    # rungs of light: the ladder
    d = ImageDraw.Draw(img)
    for k in range(9):
        y = int(H * (0.12 + k * 0.095))
        d.line([(W * 0.28, y), (W * 0.72, y)], fill=WHITE + (150,), width=3)
    return img.filter(ImageFilter.GaussianBlur(1.2))


def icon():
    c = R.Canvas(128)
    im, d = c.layer()
    # the circle in the sky
    R.ell(d, c, 0.5, 0.2, 0.3, 0.09, GOLD + (255,), width=int(0.018 * c.S), outline=GOLD + (255,))
    im2, d2 = c.layer()
    R.ell(d2, c, 0.5, 0.2, 0.26, 0.07, (0, 0, 0, 0))
    # the pillar falling from it, with rungs
    im3, d3 = c.layer()
    d3.polygon([c.P(0.38, 0.22), c.P(0.62, 0.22), c.P(0.68, 0.92), c.P(0.32, 0.92)], fill=WHITE + (255,))
    for k in range(6):
        y = 0.32 + k * 0.1
        d3.line([c.P(0.37, y), c.P(0.63, y)], fill=GOLD + (255,), width=int(0.02 * c.S))
    # four wings round the trumpet-mouth at the top
    for sx in (-1, 1):
        for dy, L in ((0.0, 0.2), (0.07, 0.15)):
            pts = [c.P(0.5 + sx * 0.06, 0.2 + dy), c.P(0.5 + sx * (0.06 + L), 0.12 + dy), c.P(0.5 + sx * (0.06 + L * 0.8), 0.2 + dy)]
            d3.polygon(pts, fill=WHITE + (255,))
    return c.flatten(None)


def wings_icon():
    """The Angel's Flight: a pair of feathered wings, bluish white (canon: her wings)."""
    c = R.Canvas(128)
    im, d = c.layer()
    WING = (232, 240, 255)
    for sx in (-1, 1):
        for k in range(5):
            L = 0.36 - k * 0.05
            a = -0.35 + k * 0.22
            x0, y0 = 0.5 + sx * 0.04, 0.5
            tip = (x0 + sx * L * math.cos(a), y0 + L * math.sin(a))
            d.polygon([c.P(x0, y0 - 0.03), c.P(*tip), c.P(x0, y0 + 0.04)], fill=WING + (255,))
    R.ell(d, c, 0.5, 0.5, 0.05, 0.05, GOLD + (255,))
    return c.flatten((120, 140, 180))


def main():
    b = beam()
    b.save(os.path.join(MOD, "Textures/Things/Angel/JacobsLadderBeam.png"))
    ic = R.icon(icon(), 0.95)
    out = os.path.join(MOD, "Textures/Abilities/Yuta/Copy/JacobsLadder.png")
    ic.save(out)
    os.makedirs(os.path.join(MOD, "Textures/Abilities/Angel"), exist_ok=True)
    R.icon(wings_icon(), 0.95).save(os.path.join(MOD, "Textures/Abilities/Angel/Flight.png"))
    if len(sys.argv) > 1:
        prev = Image.new("RGBA", (128 + 128 + 20, 512), (70, 70, 70, 255))
        prev.alpha_composite(ic.resize((128, 128)), (0, 0))
        prev.alpha_composite(b, (148, 0))
        prev.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
