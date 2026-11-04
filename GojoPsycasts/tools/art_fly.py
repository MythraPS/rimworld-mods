"""Fly icon (Gojo) and Air Step icon (Sukuna mod), 2026-11-04: three white chevrons
pointing up on the mod's dark ability background (taken from the Infinity / Dismantle icon corners)."""
import os
from PIL import Image, ImageDraw
HERE = os.path.dirname(__file__)
def icon(out, bg, col):
    im = Image.new("RGBA", (128, 128), bg); d = ImageDraw.Draw(im)
    for k, y in enumerate((88, 62, 36)):
        a = 255 - k * 60
        d.line([(30, y + 18), (64, y - 10), (98, y + 18)], fill=col + (a,), width=11, joint="curve")
    im.save(out)
icon(os.path.join(HERE, "..", "Textures", "Abilities", "Gojo", "Fly.png"), (16, 24, 48, 255), (235, 245, 255))
sk = os.path.join(HERE, "..", "..", "SukunaPsycasts", "Textures", "Abilities", "Sukuna", "AirStep.png")
icon(sk, (40, 10, 12, 255), (255, 235, 230))
