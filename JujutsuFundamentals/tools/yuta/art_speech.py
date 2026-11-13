"""Cursed Speech art (2026-11-11).

Icons: each command's canon Japanese line (Inumaki's own words) on a tile, in the letter-tile style of
tools/art_icons.py:
  Stop 止まれ  Run Away 逃げろ  Don't Move 動くな  Blast Away 吹っ飛べ  Sleep 眠れ  Get Crushed 潰れろ
  Explode 爆ぜろ  Die 死ね
  The tile colour is a placeholder until she picks one.
Backgrounds (her pick 2026-11-11; 950x1515 like tools/art_pictures.py): his tongue with the snake eye and fangs,
and the snake eye at both corners of his mouth (the crest is on his cheeks and tongue).
Inumaki's crest: the snake eye (a ring with a dot) at both corners of his mouth. Placement uses the vanilla head
measurements from tools/art_characters.py (eyes rows 67-73, mouth line about row 83, Toji's mouth-corner scar
at x 58 from the front and x 84.5 from the side).

  python3 tools/yuta/art_speech.py
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

MOD = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(MOD / "tools/sukuna"))
from art_hair2 import K, draw4, S  # noqa: E402

T = MOD / "Textures"
KANJI_FONT = "/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc"
SZ = 128
WHITE = (240, 236, 228)
SPEECH = (92, 74, 112)   # placeholder tile colour
WORDS = {"Stop": "止まれ", "RunAway": "逃げろ", "DontMove": "動くな", "BlastAway": "吹っ飛べ", "Sleep": "眠れ",
         "GetCrushed": "潰れろ", "Explode": "爆ぜろ", "Die": "死ね"}


def tile(bg):
    img = Image.new("RGBA", (SZ, SZ), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((4, 4, SZ - 5, SZ - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    return img


def word_tile(text):
    img = tile(SPEECH)
    d = ImageDraw.Draw(img)
    size = {2: 50, 3: 38, 4: 29}[len(text)]
    f = ImageFont.truetype(KANJI_FONT, size, index=0)
    box = d.textbbox((0, 0), text, font=f)
    d.text(((SZ - (box[2] - box[0])) / 2 - box[0], (SZ - (box[3] - box[1])) / 2 - box[1]), text, font=f, fill=WHITE)
    return img


W, H = 950, 1515


def snake_eye(d, x, y, r, col, hole):
    """The Inumaki crest's snake eye: a thick ring with a dot in the middle."""
    d.ellipse((x - r, y - r, x + r, y + r), fill=col)
    q = r * 0.62
    d.ellipse((x - q, y - q, x + q, y + q), fill=hole)
    q = r * 0.28
    d.ellipse((x - q, y - q, x + q, y + q), fill=col)


def fangs(d, x, y, r, col):
    """A pair of fangs under the eye, curving down and in."""
    for side in (-1, 1):
        x0 = x + side * r * 0.55
        d.polygon([(x0 - r * 0.22, y), (x0 + r * 0.22, y), (x0 - side * r * 0.12, y + r * 0.95)], fill=col)


def background():
    """Inumaki's crest, as he shows it: the tongue stuck out, the snake eye and fangs on it, and the snake eye at both
    corners of the mouth (her pick 2026-11-11). Flat and faint like the other paths' backgrounds."""
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    top, bot, edge = (30, 22, 38), (8, 6, 10), (130, 100, 150)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
    d.rectangle((24, 24, W - 25, H - 25), outline=edge + (255,), width=6)

    k = 2
    L = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0)); dl = ImageDraw.Draw(L)
    col, hi, hole = (175, 140, 210, 125), (215, 190, 240, 165), (0, 0, 0, 0)
    dark = (14, 8, 18, 235)                  # inside the mouth
    P = lambda pts: [(x * k, y * k) for x, y in pts]
    cx, my = W / 2, 470                      # mouth centre
    # the open mouth: a wide lens, upper lip flatter, lower lip deeper
    import math
    up = [(cx + 300 * math.cos(math.pi * (1 - i / 40)), my - 70 * math.sin(math.pi * i / 40)) for i in range(41)]
    lo = [(cx + 300 * math.cos(math.pi * i / 40), my + 130 * math.sin(math.pi * i / 40)) for i in range(41)]
    dl.polygon(P(up + lo), fill=col)
    inner_up = [(cx + 245 * math.cos(math.pi * (1 - i / 40)), my - 30 * math.sin(math.pi * i / 40)) for i in range(41)]
    inner_lo = [(cx + 245 * math.cos(math.pi * i / 40), my + 78 * math.sin(math.pi * i / 40)) for i in range(41)]
    dl.polygon(P(inner_up + inner_lo), fill=dark)
    # the tongue, hanging out of the mouth and down, with a rounded tip
    # her call 2026-11-11: the tongue much smaller (s = its scale against the first version)
    s = 0.45
    ty0 = my + 38
    Y = lambda y: ty0 + (y - ty0) * s        # a height on the first version's tongue, scaled
    tw, ty1, fl = 125 * s, Y(1040), 30 * s
    side = [(cx - tw, ty0)] + [(cx - tw - fl * i / 30, ty0 + (ty1 - ty0) * i / 30) for i in range(31)]
    tip = [(cx + (tw + fl) * math.cos(math.pi * (1 - i / 30)), ty1 + 150 * s * math.sin(math.pi * i / 30)) for i in range(31)]
    other = [(cx + tw + fl * (30 - i) / 30, ty1 - (ty1 - ty0) * i / 30) for i in range(31)]
    dl.polygon(P(side + tip + other), fill=col)
    dl.line(P([(cx, Y(ty0 + 130)), (cx, Y(ty0 + 300))]), fill=dark, width=max(2, int(10 * s)) * k)   # the centre groove
    # the crest on the tongue
    snake_eye(dl, cx * k, Y(840) * k, 115 * s * k, hi, col)
    fangs(dl, cx * k, Y(965) * k, 120 * s * k, hi)
    # the snake eye at each corner of the mouth
    for x in (cx - 345, cx + 345):
        snake_eye(dl, x * k, my * k, 46 * k, hi, hole)
    img.alpha_composite(L.resize((W, H), Image.LANCZOS))
    return img


INK = (34, 26, 40, 255)


def crest(d):
    """A ring with a dot in it, just outside each corner of the mouth. Facing west the game mirrors east."""
    def f(dr):
        spots = {"south": [(56.5, 83), (71.5, 83)], "east": [(85.5, 83)]}.get(d, [])
        for x, y in spots:
            r = 2.6
            dr.ellipse(S([(x - r, y - r), (x + r, y + r)]), outline=INK, width=int(0.9 * K))
            q = 0.8
            dr.ellipse(S([(x - q, y - q), (x + q, y + q)]), fill=INK)
    return draw4(f)


def main():
    out = T / "Abilities/Yuta/Speech"
    out.mkdir(parents=True, exist_ok=True)
    for name, text in WORDS.items():
        word_tile(text).save(out / f"{name}.png")
    for path in ("UI/Backgrounds/CursedSpeech.png", "UI/AlternativeBackgrounds/PathBG_CursedSpeech.png"):
        (T / path).parent.mkdir(parents=True, exist_ok=True)
        background().save(T / path)
    tat = T / "Things/Pawn/Humanlike/Tattoos/JJK_InumakiCrest"
    tat.mkdir(parents=True, exist_ok=True)
    for d in ("south", "east", "north"):
        crest(d).save(tat / f"JJK_InumakiCrest_{d}.png")
    print("ok")


main()
