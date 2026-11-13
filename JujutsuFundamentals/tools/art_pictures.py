"""Picture icons and path backgrounds (her picks 2026-11-10).

Icons, in the Sukuna mod's picture style (rounded tile, cream flat symbol):
  HollowWickerBasket   a circular lattice
  SimpleDomain         a circle on the ground, a figure in the middle with the fists pressed together at one side
  DomainAmplification  a fist with a circle of energy around it
Backgrounds (950x1515, VPE's size; framed like the Gojo mod's):
  Cursed Energy Fundamentals  the kanji for Jujutsu Kaisen, 呪術廻戦
  New Shadow Style            a katana in its sheath (undrawn)
The motif is drawn clearly (her note 2026-11-10: the first, faint version read as no background),
and the alt backgrounds carry it too.

  python tools/art_pictures.py
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

MOD = Path(__file__).resolve().parent.parent
T = MOD / "Textures"
S, K = 128, 4                  # icon size, supersampling
CREAM = (240, 232, 214)
FUND = (63, 77, 107)
SHADOW = (40, 90, 160)
KANJI_FONT = "/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc"


def tile(bg):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((4, 4, S - 5, S - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    return img


def big():
    return Image.new("RGBA", (S * K, S * K), (0, 0, 0, 0))


def finish(bg, layer):
    img = tile(bg)
    img.alpha_composite(layer.resize((S, S), Image.LANCZOS))
    return img


def P(pts):
    return [(x * K, y * K) for x, y in pts]


# ---------------------------------------------------------------- icons

def lattice():
    """A round basket: a ring, filled with a woven lattice of crossing diagonal strips."""
    L = big(); d = ImageDraw.Draw(L)
    cx, cy, r = 64, 64, 40
    strips = Image.new("L", L.size, 0); ds = ImageDraw.Draw(strips)
    for i in range(-4, 5):
        o = i * 19
        for sgn in (1, -1):
            x0, y0 = cx + o - 80 * sgn, cy - 80
            x1, y1 = cx + o + 80 * sgn, cy + 80
            ds.line(P([(x0, y0), (x1, y1)]), fill=255, width=6 * K)
    mask = Image.new("L", L.size, 0)
    ImageDraw.Draw(mask).ellipse(P([(cx - r, cy - r), (cx + r, cy + r)]), fill=255)
    strips = ImageChops.multiply(strips, mask)
    fill = Image.new("RGBA", L.size, CREAM + (255,)); fill.putalpha(strips)
    L.alpha_composite(fill)
    d = ImageDraw.Draw(L)
    d.ellipse(P([(cx - r, cy - r), (cx + r, cy + r)]), outline=CREAM + (255,), width=7 * K)
    return L


def simple_domain():
    """A circle on the ground (seen at an angle) and a figure standing in it, fists pressed together at the left hip."""
    L = big(); d = ImageDraw.Draw(L)
    # ground circle
    d.ellipse(P([(16, 84), (112, 114)]), outline=CREAM + (255,), width=4 * K)
    # figure
    cx = 64
    d.ellipse(P([(cx - 8, 20), (cx + 8, 36)]), fill=CREAM + (255,))                      # head
    d.polygon(P([(cx - 11, 40), (cx + 11, 40), (cx + 9, 70), (cx - 9, 70)]), fill=CREAM + (255,))   # body
    d.polygon(P([(cx - 9, 68), (cx - 1, 68), (cx - 6, 99), (cx - 13, 99)]), fill=CREAM + (255,))    # left leg
    d.polygon(P([(cx + 1, 68), (cx + 9, 68), (cx + 13, 99), (cx + 6, 99)]), fill=CREAM + (255,))    # right leg
    # both arms run down to the left hip, where the two fists press together; outlined in the tile
    # colour so they read over the body
    O = SHADOW + (255,)
    for a in ([(cx + 9, 44), (cx - 2, 57), (cx - 14, 64)], [(cx - 9, 44), (cx - 17, 55), (cx - 20, 63)]):
        d.line(P(a), fill=O, width=10 * K, joint="curve")
        d.line(P(a), fill=CREAM + (255,), width=6 * K, joint="curve")
    for x in (cx - 27, cx - 18):
        d.ellipse(P([(x - 1, 57), (x + 11, 71)]), fill=O)
        d.ellipse(P([(x + 1, 59), (x + 9, 69)]), fill=CREAM + (255,))
    return L


def amplification():
    """A fist (knuckles forward) inside a glowing ring of energy."""
    L = big()
    ring = big(); dr = ImageDraw.Draw(ring)
    dr.ellipse(P([(14, 14), (114, 114)]), outline=(150, 200, 255, 255), width=6 * K)
    L.alpha_composite(ring.filter(ImageFilter.GaussianBlur(5 * K)))
    L.alpha_composite(ring)
    d = ImageDraw.Draw(L)
    # fist seen from the front: four knuckles on top, fingers folded, thumb across
    d.rounded_rectangle(P([(38, 44), (90, 90)]), radius=10 * K, fill=CREAM + (255,))
    for i in range(4):
        x = 40 + i * 12.5
        d.ellipse(P([(x - 1, 38), (x + 12, 52)]), fill=CREAM + (255,))
    for i in range(1, 4):                                                                 # finger creases
        x = 40 + i * 12.5
        d.line(P([(x, 50), (x, 64)]), fill=FUND + (255,), width=2 * K)
    d.rounded_rectangle(P([(34, 62), (76, 74)]), radius=6 * K, fill=CREAM + (255,), outline=FUND + (255,), width=2 * K)   # thumb
    d.rectangle(P([(46, 90), (82, 104)]), fill=CREAM + (255,))                            # wrist
    return L


# ---------------------------------------------------------------- backgrounds

W, H = 950, 1515


def base(top, bot, edge):
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
    d.rectangle((24, 24, W - 25, H - 25), outline=edge + (255,), width=6)
    return img


def kanji_motif(col):
    """呪術廻戦, one column down the middle, faint, so the nodes stay readable over it."""
    L = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    f = ImageFont.truetype(KANJI_FONT, 300, index=0)
    chars = "呪術廻戦"
    step = 330; y0 = (H - step * len(chars)) / 2
    for i, ch in enumerate(chars):
        box = d.textbbox((0, 0), ch, font=f)
        d.text(((W - (box[2] - box[0])) / 2 - box[0], y0 + i * step + (step - (box[3] - box[1])) / 2 - box[1]), ch, font=f, fill=col)
    return L


def katana_motif(col, hi):
    """A katana in its sheath, standing upright: handle with its diamond wrap, guard, then the long curved scabbard."""
    L = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    cx = W / 2
    curve = lambda t: 38 * math.sin(math.pi * t)     # the scabbard's gentle curve

    def band(y0, y1, w0, w1, steps=60):
        left, right = [], []
        for i in range(steps + 1):
            t = i / steps; y = y0 + (y1 - y0) * t; w = w0 + (w1 - w0) * t
            x = cx + curve((y - 180) / (H - 360))
            left.append((x - w / 2, y)); right.append((x + w / 2, y))
        return left + right[::-1]

    top, guard, tip = 180, 520, H - 180
    d.polygon(band(top, guard - 30, 92, 98), fill=col)                    # handle
    d.ellipse((cx - 54 + curve(0), top - 22, cx + 54 + curve(0), top + 22), fill=col)   # pommel cap
    for i in range(7):                                                     # handle wrap diamonds
        y = top + 30 + i * 42
        x = cx + curve((y - 180) / (H - 360))
        d.polygon([(x, y), (x + 32, y + 20), (x, y + 40), (x - 32, y + 20)], fill=hi)
    xg = cx + curve((guard - 180) / (H - 360))
    d.ellipse((xg - 130, guard - 34, xg + 130, guard + 10), fill=col)       # guard (tsuba)
    d.ellipse((xg - 130, guard - 34, xg + 130, guard + 10), outline=hi, width=5)
    d.polygon(band(guard + 10, tip, 106, 84), fill=col)                     # scabbard
    xt = cx + curve(1)
    d.ellipse((xt - 42, tip - 30, xt + 42, tip + 30), fill=col)           # rounded scabbard end
    yk = guard + 130; xk = cx + curve((yk - 180) / (H - 360))
    d.rectangle((xk - 58, yk - 14, xk + 58, yk + 14), fill=hi)             # cord knob (kurikata)
    return L


def main():
    (T / "Abilities/Jujutsu").mkdir(parents=True, exist_ok=True)
    finish(SHADOW, lattice()).save(T / "Abilities/Jujutsu/HollowWickerBasket.png")
    finish(SHADOW, simple_domain()).save(T / "Abilities/Jujutsu/SimpleDomain.png")
    finish(FUND, amplification()).save(T / "Abilities/Jujutsu/DomainAmplification.png")

    for folder in ("Backgrounds", "AlternativeBackgrounds"):
        (T / "UI" / folder).mkdir(parents=True, exist_ok=True)
    fund_edge = (110, 90, 160)
    for path in ("UI/Backgrounds/JujutsuFundamentals.png", "UI/AlternativeBackgrounds/PathBG_JujutsuFundamentals.png"):
        img = base((30, 22, 44), (8, 6, 12), fund_edge)
        img.alpha_composite(kanji_motif((170, 140, 230, 120)))
        img.save(T / path)
    nss_edge = (70, 110, 170)
    for path in ("UI/Backgrounds/NewShadowStyle.png", "UI/AlternativeBackgrounds/PathBG_NewShadowStyle.png"):
        img = base((16, 26, 40), (5, 8, 12), nss_edge)
        img.alpha_composite(katana_motif((130, 165, 215, 130), (185, 210, 240, 150)))
        img.save(T / path)
    print("ok")


main()


# ---------------------------------------------------------------- Hazy Moon's blade (her ask 2026-11-10)

def hazy_moon_blade():
    """A light blue katana, laid flat like vanilla weapon textures (handle left, point right; the game
    turns it to aim). Wrapped handle, small oval guard, a long gently curved blade with a pale edge."""
    S2, k = 128, 4
    L = Image.new("RGBA", (S2 * k, S2 * k), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    Pk = lambda pts: [(x * k, y * k) for x, y in pts]
    OUT = (24, 40, 64, 255); BLADE = (150, 205, 255, 255); EDGE = (225, 242, 255, 255)
    HANDLE = (70, 120, 185, 255); WRAP = (165, 210, 250, 255); GUARD = (110, 165, 230, 255)
    curve = lambda x: 64 - 5 * math.sin(math.pi * (x - 40) / 84)   # the blade bows upward
    top = [(x, curve(x) - 3.2) for x in range(40, 121, 4)]
    bot = [(x, curve(x) + 3.2) for x in range(120, 39, -4)]
    blade = top + [(124, curve(122) - 1.5)] + bot
    d.polygon(Pk(blade), fill=OUT)                                  # outline pass
    d.polygon(Pk([(x, y - 0.9 if y < curve(x) else y + 0.9) for x, y in blade]), outline=OUT, width=4 * k)
    d.polygon(Pk(blade), fill=BLADE)
    d.line(Pk([(x, curve(x) - 2.2) for x in range(42, 121, 4)]), fill=EDGE, width=int(1.2 * k))   # cutting edge
    d.rounded_rectangle(Pk([(8, 60), (36, 68)]), radius=3 * k, fill=HANDLE, outline=OUT, width=2 * k)   # handle
    for x in range(11, 34, 5):
        d.polygon(Pk([(x, 64), (x + 2.5, 61), (x + 5, 64), (x + 2.5, 67)]), fill=WRAP)
    d.ellipse(Pk([(35, 56), (41, 72)]), fill=GUARD, outline=OUT, width=2 * k)   # guard
    img = L.resize((S2, S2), Image.LANCZOS)
    (T / "Things/Jujutsu").mkdir(parents=True, exist_ok=True)
    img.save(T / "Things/Jujutsu/HazyMoonBlade.png")


hazy_moon_blade()
