"""Art for the Sukuna questline (2026-11-01): Benevolent Shrine and Piercing Blood: Scatter icons (in the
style of the other Shrine icons: rounded square, cream symbol), the blood shot, and the Death Painting jar.

  python tools/art_arc.py TEXTURES_DIR"""
import math, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

T = Path(sys.argv[1])
K = 4
CREAM = (238, 230, 208, 255)


def frame_from(icon_path, fill):
    """The rounded frame of an existing icon, with its inside flooded in `fill`."""
    im = Image.open(icon_path).convert("RGBA")
    px = im.load()
    bg = px[64, 30] if "Dismantle" in str(icon_path) else px[20, 64]
    out = im.copy(); po = out.load()
    for y in range(128):
        for x in range(128):
            r, g, b, a = px[x, y]
            if a == 0: continue
            dark = r + g + b < 140
            if not dark: po[x, y] = fill[:3] + (a,)
    return out, bg


def benevolent():
    """Malevolent Shrine's roofline on blue, mouth closed, no skulls: steps where the heap was."""
    frame, _ = frame_from(T / "Abilities/Sukuna/Dismantle.png", (52, 92, 150, 255))
    N = 128 * K
    sym = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(sym)
    ink = (24, 20, 26, 255)
    s = lambda pts: [(x * K, y * K) for x, y in pts]
    shapes = [
        ("rect", (60, 14, 68, 24)),                                            # finial
        ("poly", [(44, 24), (84, 24), (78, 32), (50, 32)]),                     # top roof
        ("rect", (52, 32, 76, 40)),
        ("poly", [(22, 40), (106, 40), (94, 50), (34, 50)]),                    # main roof, swept ends
        ("rect", (38, 50, 90, 80)),                                             # body
        ("rect", (36, 82, 92, 89)),                                             # steps
        ("rect", (28, 91, 100, 98)),
        ("rect", (20, 100, 108, 107)),
    ]
    for kind, v in shapes:                     # outline pass, then fill pass
        if kind == "rect": d.rectangle([(v[0] - 2) * K, (v[1] - 2) * K, (v[2] + 2) * K, (v[3] + 2) * K], fill=ink)
        else:
            cx = sum(x for x, _ in v) / len(v); cy = sum(y for _, y in v) / len(v)
            d.polygon(s([(x + (2 if x > cx else -2), y + (2 if y > cy else -2)) for x, y in v]), fill=ink)
    for kind, v in shapes:
        if kind == "rect": d.rectangle([c * K for c in v], fill=CREAM)
        else: d.polygon(s(v), fill=CREAM)
    d.rectangle([56 * K, 58 * K, 72 * K, 80 * K], fill=ink)                  # the doorway, closed
    d.rectangle([58 * K, 60 * K, 70 * K, 80 * K], fill=(214, 186, 120, 255))
    d.line([64 * K, 60 * K, 64 * K, 80 * K], fill=ink, width=K)
    glow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((24 * K, 8 * K, 104 * K, 88 * K), fill=(255, 244, 200, 80))
    glow = glow.filter(ImageFilter.GaussianBlur(8 * K))
    inner = Image.new("L", (128, 128), 0); pm = inner.load(); pf = frame.load()
    for y in range(128):
        for x in range(128):
            if pf[x, y][:3] == (52, 92, 150): pm[x, y] = 255
    frame.paste(Image.alpha_composite(frame, glow.resize((128, 128), Image.LANCZOS)), (0, 0), inner)
    frame.alpha_composite(sym.resize((128, 128), Image.LANCZOS))
    frame.save(T / "Abilities/Sukuna/BenevolentShrine.png")


def piercing():
    frame, _ = frame_from(T / "Abilities/Sukuna/Dismantle.png", (122, 20, 30, 255))
    N = 128 * K
    sym = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(sym)
    cx = cy = 64 * K
    for i in range(8):
        a0 = i * math.pi / 4
        pts = []
        for t in [j / 20 for j in range(21)]:
            r = (10 + 36 * t) * K
            a = a0 + 0.55 * math.sin(t * math.pi * 0.9)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        d.line(pts, fill=CREAM, width=int(3.2 * K), joint="curve")
        ex, ey = pts[-1]; px_, py_ = pts[-3]
        ang = math.atan2(ey - py_, ex - px_)
        tip = (ex + 7 * K * math.cos(ang), ey + 7 * K * math.sin(ang))
        l = (ex + 4 * K * math.cos(ang + 2.2), ey + 4 * K * math.sin(ang + 2.2))
        rr = (ex + 4 * K * math.cos(ang - 2.2), ey + 4 * K * math.sin(ang - 2.2))
        d.polygon([tip, l, rr], fill=CREAM)
    d.ellipse((cx - 9 * K, cy - 9 * K, cx + 9 * K, cy + 9 * K), fill=CREAM)
    frame.alpha_composite(sym.resize((128, 128), Image.LANCZOS))
    frame.save(T / "Abilities/Sukuna/PiercingBloodScatter.png")


def bullet():
    N = 64 * K
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    # a drop pointing north with a fading tail
    for i in range(14):
        t = i / 13
        y = (14 + 40 * t) * K; r = (5.5 * (1 - t) + 1) * K
        d.ellipse((32 * K - r, y - r, 32 * K + r, y + r), fill=(200, 20, 35, int(255 * (1 - t) ** 1.3)))
    d.polygon([(32 * K, 6 * K), (26 * K, 18 * K), (38 * K, 18 * K)], fill=(220, 30, 45, 255))
    d.ellipse((29 * K, 13 * K, 33 * K, 17 * K), fill=(255, 150, 150, 230))
    glow = im.filter(ImageFilter.GaussianBlur(3 * K))
    out = Image.alpha_composite(glow, im).resize((64, 64), Image.LANCZOS)
    out.save(T / "Things/Sukuna/BloodBullet.png")


def painting():
    N = 128 * K
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    s = lambda *v: tuple(x * K for x in v)
    ink = (18, 14, 16, 255)
    # jar body: black glass, outlined
    d.rounded_rectangle(s(34, 36, 94, 114), radius=16 * K, fill=ink)
    d.rounded_rectangle(s(38, 40, 90, 110), radius=13 * K, fill=(42, 34, 44, 255))
    # the womb inside: a curled dark-red shape with a pale eye
    d.ellipse(s(46, 54, 84, 98), fill=(96, 18, 28, 255))
    d.ellipse(s(52, 60, 78, 92), fill=(130, 30, 40, 255))
    d.ellipse(s(56, 64, 72, 80), fill=(96, 18, 28, 255))
    d.ellipse(s(61, 69, 67, 75), fill=(230, 210, 190, 255))
    d.ellipse(s(63, 71, 66, 74), fill=ink)
    # glass shine
    d.rounded_rectangle(s(42, 46, 48, 100), radius=3 * K, fill=(255, 255, 255, 45))
    # neck, lid and paper seal
    d.rectangle(s(44, 26, 84, 38), fill=ink)
    d.rectangle(s(47, 28, 81, 36), fill=(110, 78, 50, 255))
    d.rectangle(s(58, 22, 70, 52), fill=ink)
    d.rectangle(s(60, 24, 68, 50), fill=(232, 220, 190, 255))
    d.line(s(62, 30, 66, 30), fill=(150, 30, 30, 255), width=K); d.line(s(62, 36, 66, 40), fill=(150, 30, 30, 255), width=K)
    im.resize((128, 128), Image.LANCZOS).save(T / "Things/Sukuna/DeathPainting.png")


benevolent(); piercing(); bullet(); painting()
