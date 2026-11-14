"""Positive Energy's icon (her pick 2026-11-14): Reverse Cursed Technique's icon recoloured white-gold, picture and kanji
versions. The green field becomes gold and the dark border a deep amber; the cream letter/kanji stays.
Usage: python tools/art_positive_energy_icon.py"""
from PIL import Image
import colorsys

GREEN = (47, 107, 79)
def recolour(src, dst):
    im = Image.open(src).convert("RGBA")
    px = im.load()
    gh, gl, gs = colorsys.rgb_to_hls(*[c / 255 for c in GREEN])
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            if s > 0.2 and abs(h - gh) < 0.12:                      # the green field and its antialiased edges
                nr, ng, nb = colorsys.hls_to_rgb(0.12, min(1, l * 1.55), 0.72)   # gold, brighter
                px[x, y] = (round(nr * 255), round(ng * 255), round(nb * 255), a)
            elif l < 0.2:                                          # the dark border
                px[x, y] = (74, 52, 16, a)
    im.save(dst)

base = "Textures/Abilities/Jujutsu/"
recolour(base + "ReverseCursedTechnique.png", base + "PositiveEnergy.png")
recolour(base + "Kanji/ReverseCursedTechnique.png", base + "Kanji/PositiveEnergy.png")
print("ok")
