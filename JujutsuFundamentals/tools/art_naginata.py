"""Maki's naginata (2026-11-12, her OK to base it on the game's spear): the vanilla spear's shaft and outline kept
as they are (decoded from Assets.zip with ~/.local/share/rimworld-tools/waylith_texture.py), its leaf point replaced
by a naginata's long single-edged blade with a gentle upward curve, and a small round guard. Same black outline and
flat light fill as the spear.
Usage: python tools/art_naginata.py SPEAR_PNG OUT_PNG"""
import sys
from PIL import Image, ImageDraw, ImageFilter

spear = Image.open(sys.argv[1]).convert("RGBA")
S = 4                       # draw at 4x, then shrink
W = 128
big = spear.resize((W * S, W * S), Image.NEAREST)
px = big.load()
CUT = 86 * S                # everything right of here was the spear point
for y in range(big.height):
    for x in range(CUT, big.width):
        px[x, y] = (0, 0, 0, 0)
cy = 63 * S                 # the shaft's centre line

def q(pts):
    return [(x * S, y * S) for x, y in pts]

parts = Image.new("RGBA", big.size, (0, 0, 0, 0))
d = ImageDraw.Draw(parts)
x0 = 91                      # blade starts after the guard
# blade: back (top) nearly straight then lifting, edge (bottom) bellied, meeting at an upturned point
blade = [(x0, 60.5), (x0 + 10, 60), (x0 + 20, 59), (x0 + 27, 57), (x0 + 32, 54.5),
         (x0 + 32, 54.5), (x0 + 30, 59), (x0 + 25, 64), (x0 + 17, 67), (x0 + 8, 67.5), (x0, 66.5)]
d.polygon(q(blade), fill=(236, 236, 236, 255))
d.line(q([(x0 + 1, 62.2), (x0 + 12, 61.8), (x0 + 22, 60.5), (x0 + 29, 57)]), fill=(175, 175, 175, 255), width=S)
# collar (habaki) and round guard (tsuba)
d.rectangle(q([(85, 61), (88, 65)]), fill=(120, 120, 120, 255))
d.ellipse(q([(88, 58.5), (91.5, 67.5)]), fill=(120, 120, 120, 255))
# black outline like the spear's: grow the parts' shape and put black underneath
a = parts.split()[3].point(lambda v: 255 if v > 0 else 0)
grown = a.filter(ImageFilter.MaxFilter(2 * 2 * S + 1))
outline = Image.new("RGBA", big.size, (25, 25, 25, 255))
outline.putalpha(grown)
big.alpha_composite(outline)
big.alpha_composite(parts)
big.resize((W, W), Image.LANCZOS).save(sys.argv[2])
print(sys.argv[2])
