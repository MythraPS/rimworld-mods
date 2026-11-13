"""Hollow Wicker Basket (her ask 2026-11-11: "needs a visual effect, look up what it looks like in the story").
Canon (Game Rant; ch. 171 Reggie, ch. 249/266 Sukuna): "a spherical cage that looks like a woven basket appears with
the user in the middle", held up by an interlocked-finger hand sign; it cracks under hits. No colour is given in any
text source; her call: Sukuna's red and white, everyone else white and their own cursed-energy colour.

Drawn as the woven dome seen from above, like the base of a wicker basket: radial stakes running out from the crown,
weavers going round in rings over one stake and under the next (the gaps between strands are see-through), and a thick
rolled rim. Two textures, both white so the game tints them:
  Weave.png  the strands (drawn white)
  Accent.png the shading edge of every strand where it passes over another, plus the rim's lower edge (drawn in the
             user's cursed-energy colour)

  python3 tools/art_wicker_basket.py [PREVIEW.png]"""
import math, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

MOD = Path(__file__).resolve().parent.parent
OUT = MOD / "Textures/Things/Jujutsu/WickerBasket"
K = 4
S = 256 * K
C = S / 2
R = S * 0.46              # outer radius (rim centre line)
STAKES = 16               # radial stakes
RINGS = 5                 # weaver rings between the crown and the rim


def polar(a, r):
    return (C + math.cos(a) * r, C + math.sin(a) * r)


def arc_pts(r, a0, a1, n=12):
    return [polar(a0 + (a1 - a0) * i / n, r) for i in range(n + 1)]


def draw():
    weave = Image.new("L", (S, S), 0)
    accent = Image.new("L", (S, S), 0)
    dw, da = ImageDraw.Draw(weave), ImageDraw.Draw(accent)
    stake_w = int(6 * K)
    weaver_w = int(9 * K)
    crown = R * 0.12
    ring_r = [crown + (R * 0.9 - crown) * (i + 0.5) / RINGS for i in range(RINGS)]
    step = 2 * math.pi / STAKES

    # 1. the stakes, from the crown out to the rim
    for k in range(STAKES):
        a = k * step
        dw.line([polar(a, crown * 0.6), polar(a, R)], fill=255, width=stake_w)
    # 2. the weavers: each ring passes over a stake and under the next, alternating ring to ring. Where a weaver
    #    goes under, the stake is drawn on top again (cut a gap in the weaver around the stake).
    for i, r in enumerate(ring_r):
        dw.line(arc_pts(r, 0, 2 * math.pi, 96), fill=255, width=weaver_w, joint="curve")
    for i, r in enumerate(ring_r):
        for k in range(STAKES):
            a = k * step
            under = (k + i) % 2 == 0          # the weaver goes under this stake
            if under:
                # the stake crosses over: clear a notch in the weaver either side of the stake, redraw the stake
                half = (stake_w * 0.9) / r
                for side in (-1, 1):
                    dw.line(arc_pts(r, a + side * half, a + side * half * 1.35, 3), fill=0, width=weaver_w + 2 * K)
                dw.line([polar(a, r - weaver_w * 0.75), polar(a, r + weaver_w * 0.75)], fill=255, width=stake_w)
                # shading edge on the stake where it rides over
                da.line([polar(a + stake_w * 0.45 / r, r - weaver_w * 0.6), polar(a + stake_w * 0.45 / r, r + weaver_w * 0.6)],
                        fill=255, width=int(2.5 * K))
            else:
                # the weaver rides over the stake: shading on the weaver's outer edge across the stake
                da.line(arc_pts(r + weaver_w * 0.42, a - step * 0.35, a + step * 0.35, 6), fill=255, width=int(2.5 * K))
    # 3. the crown knot and the rolled rim
    dw.ellipse([C - crown, C - crown, C + crown, C + crown], fill=255)
    da.ellipse([C - crown * 0.55, C - crown * 0.55, C + crown * 0.55, C + crown * 0.55], fill=255)
    rim_w = int(16 * K)
    dw.ellipse([C - R, C - R, C + R, C + R], outline=255, width=rim_w)
    # the rim is a twisted roll: slanted bands round it
    for k in range(STAKES * 3):
        a = k * 2 * math.pi / (STAKES * 3)
        p0 = polar(a, R - rim_w * 0.45)
        p1 = polar(a + 0.045, R + rim_w * 0.05)
        da.line([p0, p1], fill=255, width=int(3 * K))
    da.ellipse([C - R - rim_w * 0.1, C - R - rim_w * 0.1, C + R + rim_w * 0.1, C + R + rim_w * 0.1], outline=255, width=int(4 * K))

    def finish(mask, glow):
        m = mask.filter(ImageFilter.GaussianBlur(0.6 * K))
        img = Image.new("RGBA", (S, S), (255, 255, 255, 0))
        img.putalpha(m)
        if glow:
            g = mask.filter(ImageFilter.GaussianBlur(5 * K)).point(lambda v: int(v * 0.5))
            gl = Image.new("RGBA", (S, S), (255, 255, 255, 0)); gl.putalpha(g)
            img = Image.alpha_composite(gl, img)
        return img.resize((256, 256), Image.LANCZOS)
    # the dome: strands fade a little toward the crown so the pawn inside reads through
    fade = Image.new("L", (S, S), 0)
    fd = ImageDraw.Draw(fade)
    for i in range(60, 0, -1):
        rr = R * 1.1 * i / 60
        fd.ellipse([C - rr, C - rr, C + rr, C + rr], fill=int(255 * (0.55 + 0.45 * i / 60)))
    weave = Image.composite(weave, Image.new("L", (S, S), 0), fade.point(lambda v: v))
    weave = weave.point(lambda v: v)
    w_img = finish(weave, glow=False)
    a_img = finish(accent, glow=True)
    return w_img, a_img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    w, a = draw()
    w.save(OUT / "Weave.png")
    a.save(OUT / "Accent.png")
    if len(sys.argv) > 1:
        prev = Image.new("RGBA", (256 * 3, 256), (96, 82, 62, 255))
        for i, col in enumerate([(235, 50, 50), (80, 140, 255), (160, 80, 240)]):
            tile = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
            wt = Image.new("RGBA", (256, 256), (255, 255, 255, 0)); wt.putalpha(w.getchannel("A").point(lambda v: int(v * 0.85)))
            at = Image.new("RGBA", (256, 256), col + (0,)); at.putalpha(a.getchannel("A"))
            tile = Image.alpha_composite(tile, wt); tile = Image.alpha_composite(tile, at)
            prev.alpha_composite(tile, (256 * i, 0))
        prev.save(sys.argv[1])
    print("ok")


if __name__ == "__main__":
    main()
