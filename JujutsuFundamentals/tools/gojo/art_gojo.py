"""Textures for the Satoru Gojo mod, drawn procedurally.

  python tools/art_gojo.py MOD_DIR [SUKUNA_MOD_DIR]

Writes under MOD_DIR/Textures:
  UI/Backgrounds/GojoPath.png, UI/AlternativeBackgrounds/PathBG_Gojo.png  (950x1515)
      a blue ring and a red ring overlapping into an infinity sign. Ring centres and
      radius match TreeLayout.cs (RingX 0.19, RingR 0.21 of the width, centred), so
      the nodes sit on the rings.
  Abilities/Gojo/*.png      128px tiles in the Sukuna mod's style
  Things/Gojo/*.png         orbs, black spark, red beam, barrier, the void
If SUKUNA_MOD_DIR is given, its Black Flash spark is replaced with the new one too.
Hair and blindfold are drawn by art_characters_gojo.py.
"""
import math, random, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

MOD = Path(sys.argv[1])
T = MOD / "Textures"
SCRIPT = "/usr/share/fonts/gsfonts/Z003-MediumItalic.otf"
SYMBOL = "/usr/share/fonts/noto/NotoSans-Regular.ttf"


def save(img, rel):
    p = T / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    img.save(p)


def glow_ring(size, cx, cy, r, rgb, width):
    """A soft glowing ring: wide faint halo, narrower body, bright core."""
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    for w, a, blur in ((width * 6, 60, width * 3), (width * 2.2, 140, width), (width * 0.7, 255, 1)):
        layer = Image.new("RGBA", size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).ellipse((cx - r, cy - r, cx + r, cy + r), outline=rgb + (a,), width=int(w))
        out.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))
    core = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(core).ellipse((cx - r, cy - r, cx + r, cy + r),
                                 outline=tuple(min(255, c + 110) for c in rgb) + (230,), width=max(2, int(width * 0.3)))
    out.alpha_composite(core)
    return out


def background(alt):
    W, H = 950, 1515
    img = Image.new("RGBA", (W, H))
    top, bot = (16, 18, 34), (6, 6, 12)
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
    if not alt:   # a few stars
        rnd = random.Random(7)
        for _ in range(260):
            x, y, s = rnd.randrange(W), rnd.randrange(H), rnd.choice((1, 1, 1, 2))
            v = rnd.randrange(90, 200)
            d.ellipse((x, y, x + s, y + s), fill=(v, v, min(255, v + 30), 255))
    cx, cy = W / 2, H / 2   # VPE draws this texture over exactly the rect it hands DoPathAbilities
    rx, rr = 0.19 * W, 0.21 * W
    # where the rings overlap, a faint purple
    lens = Image.new("L", (W, H), 0)
    a = Image.new("L", (W, H), 0); ImageDraw.Draw(a).ellipse((cx - rx - rr, cy - rr, cx - rx + rr, cy + rr), fill=255)
    b = Image.new("L", (W, H), 0); ImageDraw.Draw(b).ellipse((cx + rx - rr, cy - rr, cx + rx + rr, cy + rr), fill=255)
    lens = ImageChops.multiply(a, b).filter(ImageFilter.GaussianBlur(18))
    purple = Image.new("RGBA", (W, H), (120, 60, 200, 0)); purple.putalpha(lens.point(lambda v: v * 0.35))
    img.alpha_composite(purple)
    img.alpha_composite(glow_ring((W, H), cx - rx, cy, rr, (60, 140, 255), 9))
    img.alpha_composite(glow_ring((W, H), cx + rx, cy, rr, (255, 60, 60), 9))
    edge = (70, 110, 200) if not alt else (90, 90, 120)
    ImageDraw.Draw(img).rectangle((24, 24, W - 25, H - 25), outline=edge + (255,), width=6)
    return img


def orb(size, rgb, core=(255, 255, 255)):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c = size / 2
    for r, a, blur in ((c * 0.95, 70, size * 0.12), (c * 0.6, 170, size * 0.06), (c * 0.35, 255, size * 0.03)):
        l = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(l).ellipse((c - r, c - r, c + r, c + r), fill=rgb + (a,))
        img.alpha_composite(l.filter(ImageFilter.GaussianBlur(blur)))
    l = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    r = c * 0.16
    ImageDraw.Draw(l).ellipse((c - r, c - r, c + r, c + r), fill=core + (255,))
    img.alpha_composite(l.filter(ImageFilter.GaussianBlur(size * 0.02)))
    return img


def jag(rnd, x0, y0, ang, length, steps):
    pts = [(x0, y0)]
    for i in range(1, steps + 1):
        t = length * i / steps
        a = ang + rnd.uniform(-0.3, 0.3)
        pts.append((x0 + math.cos(a) * t + rnd.uniform(-4, 4), y0 + math.sin(a) * t + rnd.uniform(-4, 4)))
    return pts


def black_spark(seed=3):
    """Black lightning out of the impact, edged in red, like the anime's Black Flash."""
    S = 256
    rnd = random.Random(seed)
    bolts = [jag(rnd, S / 2, S / 2, k * math.tau / 6 + rnd.uniform(-0.3, 0.3), rnd.uniform(80, 120), 6) for k in range(6)]
    red = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dr = ImageDraw.Draw(red)
    blk = Image.new("RGBA", (S, S), (0, 0, 0, 0)); db = ImageDraw.Draw(blk)
    for b in bolts:
        dr.line(b, fill=(220, 20, 30, 255), width=14, joint="curve")
        db.line(b, fill=(8, 6, 8, 255), width=6, joint="curve")
        for i in range(2, len(b) - 1, 2):   # forks
            x, y = b[i]
            f = jag(rnd, x, y, math.atan2(y - S / 2, x - S / 2) + rnd.choice((-0.8, 0.8)), rnd.uniform(18, 34), 3)
            dr.line(f, fill=(220, 20, 30, 230), width=9, joint="curve")
            db.line(f, fill=(8, 6, 8, 255), width=3, joint="curve")
    img = red.filter(ImageFilter.GaussianBlur(3))
    img.alpha_composite(red.filter(ImageFilter.GaussianBlur(1)))
    img.alpha_composite(blk)
    hub = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(hub).ellipse((S / 2 - 16, S / 2 - 16, S / 2 + 16, S / 2 + 16), fill=(5, 4, 6, 255), outline=(200, 20, 30, 255), width=4)
    img.alpha_composite(hub)
    return img.resize((128, 128), Image.LANCZOS)


def red_beam():
    S = 128
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for w, a, blur, col in ((70, 90, 14, (255, 40, 40)), (34, 200, 5, (255, 70, 60)), (10, 255, 1, (255, 220, 210))):
        l = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(l).rectangle((S / 2 - w / 2, -10, S / 2 + w / 2, S + 10), fill=col + (a,))
        img.alpha_composite(l.filter(ImageFilter.GaussianBlur(blur)))
    return img   # vertical: flecks rotate it to the beam's heading


def barrier():
    S = 64
    img = Image.new("RGBA", (S, S), (10, 10, 20, 235))
    d = ImageDraw.Draw(img)
    for i in range(0, S, 8):
        d.line([(i, 0), (i, S)], fill=(40, 40, 70, 255), width=1)
        d.line([(0, i), (S, i)], fill=(40, 40, 70, 255), width=1)
    d.rectangle((0, 0, S - 1, S - 1), outline=(200, 210, 255, 255), width=2)
    return img


def void_disc():
    """Open space with white paint thrown across nothing; a disc fading at its rim."""
    S = 1024
    rnd = random.Random(11)
    img = Image.new("RGBA", (S, S), (3, 3, 10, 255))
    neb = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dn = ImageDraw.Draw(neb)
    for _ in range(14):
        x, y, r = rnd.randrange(S), rnd.randrange(S), rnd.randrange(80, 260)
        col = rnd.choice(((40, 30, 110), (20, 50, 120), (70, 20, 90)))
        dn.ellipse((x - r, y - r, x + r, y + r), fill=col + (60,))
    img.alpha_composite(neb.filter(ImageFilter.GaussianBlur(60)))
    d = ImageDraw.Draw(img)
    for _ in range(1400):
        x, y = rnd.randrange(S), rnd.randrange(S)
        s = rnd.choice((1, 1, 1, 2, 2, 3))
        v = rnd.randrange(140, 256)
        d.ellipse((x, y, x + s, y + s), fill=(v, v, 255, 255))
    paint = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dp = ImageDraw.Draw(paint)
    for _ in range(22):   # splatters: a ragged pool, then droplets and drips thrown one way
        x, y = rnd.randrange(80, S - 80), rnd.randrange(80, S - 80)
        r = rnd.randrange(16, 40)
        throw = rnd.uniform(0, math.tau)
        for _ in range(rnd.randrange(14, 26)):          # the pool: overlapping lumps
            a = rnd.uniform(0, math.tau); dist = rnd.uniform(0, r * 0.8); s = rnd.uniform(r * 0.3, r * 0.75)
            px, py = x + math.cos(a) * dist, y + math.sin(a) * dist
            dp.ellipse((px - s, py - s, px + s, py + s), fill=(250, 250, 250, 255))
        for _ in range(rnd.randrange(18, 40)):          # droplets, mostly in the throw direction
            a = throw + rnd.gauss(0, 0.7); dist = r + abs(rnd.gauss(0, r * 1.8)); s = max(1.2, rnd.uniform(1, r * 0.22) * (1.4 - dist / (r * 5)))
            px, py = x + math.cos(a) * dist, y + math.sin(a) * dist
            dp.ellipse((px - s, py - s, px + s, py + s), fill=(250, 250, 250, 255))
        for _ in range(rnd.randrange(2, 5)):            # tapering streaks
            a = throw + rnd.gauss(0, 0.5); L = rnd.uniform(r * 1.5, r * 4)
            for k in range(12):
                t = k / 11; s = (1 - t) * r * 0.28 + 0.8
                px, py = x + math.cos(a) * L * t, y + math.sin(a) * L * t
                dp.ellipse((px - s, py - s, px + s, py + s), fill=(250, 250, 250, 255))
    img.alpha_composite(paint.filter(ImageFilter.GaussianBlur(0.8)))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((8, 8, S - 8, S - 8), fill=235)
    img.putalpha(mask.filter(ImageFilter.GaussianBlur(10)))
    return img


def tile(bg, emblem=None, letter=None, fg=(240, 236, 228)):
    """The Sukuna mod's icon style: rounded tile, dark rim, a script letter or an emblem."""
    S = 128
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((4, 4, S - 5, S - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    if emblem is not None:
        img.alpha_composite(emblem)
    if letter:
        font = ImageFont.truetype(SCRIPT if len(letter) == 1 else SCRIPT, 76 if len(letter) == 1 else 54)
        d = ImageDraw.Draw(img)
        box = d.textbbox((0, 0), letter, font=font)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], (S - (box[3] - box[1])) / 2 - box[1] - 2), letter, font=font, fill=fg)
    return img


def infinity_emblem():
    S = 128
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for blur, a in ((6, 160), (0, 255)):
        l = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(l)
        d.ellipse((18, 44, 66, 84), outline=(110, 170, 255, a), width=7)
        d.ellipse((62, 44, 110, 84), outline=(255, 110, 110, a), width=7)
        img.alpha_composite(l.filter(ImageFilter.GaussianBlur(blur)) if blur else l)
    return img


def main():
    save(background(False), "UI/Backgrounds/GojoPath.png")
    save(background(True), "UI/AlternativeBackgrounds/PathBG_Gojo.png")

    save(orb(128, (60, 130, 255)), "Things/Gojo/BlueOrb.png")
    save(orb(128, (255, 50, 50)), "Things/Gojo/RedOrb.png")
    save(orb(128, (150, 60, 255), (240, 220, 255)), "Things/Gojo/PurpleOrb.png")
    spark = black_spark()
    save(spark, "Things/Gojo/BlackSpark.png")
    save(red_beam(), "Things/Gojo/RedBeam.png")
    save(barrier(), "Things/Gojo/Barrier.png")
    save(void_disc(), "Things/Gojo/InfiniteVoid.png")

    def small_orb(rgb, scale=0.75):
        o = orb(int(128 * scale), rgb); e = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        e.alpha_composite(o, ((128 - o.size[0]) // 2, (128 - o.size[1]) // 2)); return e

    icons = {
        "Infinity": tile((22, 28, 52), infinity_emblem()),
        "Blue": tile((18, 30, 70), small_orb((60, 130, 255))),
        "Red": tile((70, 16, 18), small_orb((255, 50, 50))),
        "Purple": tile((44, 18, 70), small_orb((150, 60, 255), 0.85)),
        "BlueFist": tile((30, 60, 120), letter="F"),
        "BlackFlash": tile((22, 22, 26), black_spark(5).resize((128, 128))),
        "Speed": tile((40, 90, 160), letter="A"),
        "ReverseCursedTechnique": tile((46, 112, 80), letter="R"),
        "Teleport": tile((80, 50, 120), letter="T"),
        "InfiniteVoid": tile((10, 10, 24), void_disc().resize((100, 100)).crop((0, 0, 100, 100)).resize((112, 112)), letter="V"),
    }
    for name, img in icons.items():
        if name == "InfiniteVoid":   # centre the void disc
            base = tile((10, 10, 24)); disc = void_disc().resize((96, 96))
            base.alpha_composite(disc, (16, 16)); img = tile((10, 10, 24), base, "V")
        save(img, f"Abilities/Gojo/{name}.png")

    if len(sys.argv) > 2:
        save_to = Path(sys.argv[2]) / "Textures/Things/Sukuna/BlackSpark.png"
        spark.save(save_to)
        print("replaced", save_to)
    print("done")


main()
