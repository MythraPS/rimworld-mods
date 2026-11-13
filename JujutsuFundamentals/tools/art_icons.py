"""Placeholder ability icons for the new techniques (2026-11-10), in the Sukuna/Gojo letter-tile style
(rounded tile, dark rim, script letters; tile() copied from GojoPsycasts/tools/art_gojo.py).
Letter tiles for Blast, Hazy Moon, Batto, Evening Moon. Kanji tiles only where the canon kanji was checked: Hollow Wicker Basket 彌虚葛籠 (JJK wiki),
Simple Domain 簡易領域 and Domain Amplification 領域展延 (ciatr.jp). Reinforcement, RCT and Black Flash
are Sukuna's icons, copied.

  python tools/art_icons.py
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

MOD = Path(__file__).resolve().parent.parent
OUT = MOD / "Textures/Abilities/Jujutsu"
SCRIPT = "/usr/share/fonts/gsfonts/Z003-MediumItalic.otf"
KANJI_FONT = "/usr/share/fonts/noto-cjk/NotoSerifCJK-Black.ttc"
S = 128
WHITE = (240, 236, 228)
FUND = (63, 77, 107)    # Reinforcement's tile, placeholder for the Fundamentals path
SHADOW = (40, 90, 160)  # placeholder for New Shadow Style

# HollowWickerBasket, SimpleDomain and DomainAmplification have pictures now (tools/art_pictures.py)
TODO = (120, 64, 44)    # placeholder for Todo's Boogie Woogie
RYU = (96, 96, 104)     # placeholder for Ryu
LETTERS = {"Blast": ("CB", FUND), "HazyMoon": ("HM", SHADOW), "Batto": ("Ba", SHADOW), "EveningMoon": ("EM", SHADOW),
           "BoogieWoogie": ("BW", TODO), "BoogieWoogieFlurry": ("RB", TODO), "BoogieWoogieDeflect": ("RF", TODO),
           "GraniteBlast": ("GB", RYU), "GraniteRapidFire": ("GR", RYU), "RyuDomain": ("GT", RYU)}
TILE = {"HollowWickerBasket": SHADOW, "SimpleDomain": SHADOW, "DomainAmplification": FUND}
KANJI = {"HollowWickerBasket": "彌虚葛籠", "SimpleDomain": "簡易領域", "DomainAmplification": "領域展延"}


def tile(bg, letter=None):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((4, 4, S - 5, S - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    if letter:
        font = ImageFont.truetype(SCRIPT, 76 if len(letter) == 1 else 54)
        d = ImageDraw.Draw(img)
        box = d.textbbox((0, 0), letter, font=font)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], (S - (box[3] - box[1])) / 2 - box[1] - 2), letter, font=font, fill=WHITE)
    return img


def kanji_tile(bg, text):
    img = tile(bg)
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(KANJI_FONT, 44, index=0)
    lines = [text[:2], text[2:]]
    lh = 44 * 1.05; y0 = S / 2 - lh * len(lines) / 2
    for i, line in enumerate(lines):
        box = d.textbbox((0, 0), line, font=f)
        d.text(((S - (box[2] - box[0])) / 2 - box[0], y0 + i * lh + (lh - (box[3] - box[1])) / 2 - box[1]), line, font=f, fill=WHITE)
    return img


(OUT / "Kanji").mkdir(parents=True, exist_ok=True)
for name, (letter, bg) in LETTERS.items():
    tile(bg, letter).save(OUT / f"{name}.png")
for name, text in KANJI.items():
    kanji_tile(TILE[name], text).save(OUT / "Kanji" / f"{name}.png")
print("ok")
