"""Awakened Maki's burn scars (her ask 2026-11-11). Canon (CBR, 2026): Jogo's fire in Shibuya left striped burn scars
running up both sides of her arms and face. Her call 2026-11-11: covered in scars, all over. Face and body tattoos, 128 px, drawn at 4x and scaled down.
Placement is measured on the vanilla south textures in tools/gojo/pawnparts: thin body silhouette x 51-76, y 43-109
(arms at its edges); head eyes on rows 67-73 (measured earlier). East views have no measured base: the streaks sit
where the near arm and cheek are on vanilla side views (placeholder until checked in game)."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, random

MOD = Path(__file__).resolve().parent.parent
T = MOD / "Textures/Things/Pawn/Humanlike/Tattoos"
K = 4
SCAR = (156, 96, 88, 215)       # healed burn: darker, redder than her skin
EDGE = (120, 70, 66, 160)


def canvas():
    im = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def streaks(d, x0, x1, y0, y1, n, seed):
    """n ragged vertical streaks between x0..x1, from y1 (bottom) up to about y0."""
    r = random.Random(seed)
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n + r.uniform(-0.4, 0.4)
        top = y0 + r.uniform(0, (y1 - y0) * 0.25)
        w = r.uniform(0.7, 1.2)
        pts = []
        y = y1
        while y > top:
            pts.append((x + r.uniform(-0.35, 0.35), y))
            y -= 2.5
        pts.append((x, top))
        S = [(px * K, py * K) for px, py in pts]
        d.line(S, fill=EDGE, width=int((w + 0.5) * K), joint="curve")
        d.line(S, fill=SCAR, width=int(w * K), joint="curve")


# Maki's own head and body (her look fixes 2026-11-12): Looks sets her head to Female_AverageNormal, and the art is
# clipped to that head's (and the female body's) outline, a little inside it, so nothing pokes out past her face.
# Vanilla textures decoded from Assets.zip into ~/.local/share/rimworld-tools/heads (not shipped).
PARTS = Path.home() / ".local/share/rimworld-tools/heads"


def clip(img, part, shrink):
    from PIL import ImageChops, ImageFilter
    m = Image.open(PARTS / f"{part}.png").getchannel("A").point(lambda v: 255 if v > 100 else 0)
    if m.size != img.size:
        m = m.resize(img.size, Image.NEAREST)
    if shrink:
        m = m.filter(ImageFilter.MinFilter(2 * shrink * (img.width // 128) + 1))
    img.putalpha(ImageChops.multiply(img.getchannel("A"), m))
    return img


def save(im, name, facing):
    out = T / name
    out.mkdir(parents=True, exist_ok=True)
    im = im.resize((128, 128), Image.LANCZOS)
    part = f"Female_Average_Normal_{facing}" if "Face" in name else f"Naked_Female_{facing}"
    clip(im, part, 4 if "Face" in name else 2).save(out / f"{name}_{facing}.png")


def blotch(d, cx, cy, rx, ry, seed):
    """A healed burn patch: an irregular shiny blotch with a darker rim."""
    r = random.Random(seed)
    pts = []
    for i in range(12):
        a = i / 12 * 6.2832
        k = r.uniform(0.7, 1.15)
        pts.append(((cx + rx * k * math.cos(a)) * K, (cy + ry * k * math.sin(a)) * K))
    d.polygon(pts, fill=EDGE)
    inner = [((cx + (x / K - cx) * 0.8) * K, (cy + (y / K - cy) * 0.8) * K) for x, y in pts]
    d.polygon(inner, fill=SCAR)


def face():
    # her call 2026-11-11 ("covered in scars"): both sides of the face jaw to brow, the forehead and the chin too;
    # the eye rows (67-73) stay clear
    im, d = canvas()            # south
    streaks(d, 45, 52, 56, 88, 4, 1)
    streaks(d, 76, 83, 56, 88, 4, 2)
    streaks(d, 54, 74, 50, 64, 5, 11)          # forehead
    streaks(d, 56, 72, 76, 88, 4, 12)          # chin and jaw
    blotch(d, 49, 78, 3.5, 4, 13)              # right cheek (her right), the heavier side
    save(im, "JJK_MakiScarsFace", "south")
    im, d = canvas()            # east: the near cheek, temple and jaw
    streaks(d, 62, 76, 54, 88, 5, 3)
    blotch(d, 70, 80, 3, 3.5, 14)
    save(im, "JJK_MakiScarsFace", "east")
    im, _ = canvas()            # north: not shown (visibleNorth false), but the file must exist
    save(im, "JJK_MakiScarsFace", "north")


def body():
    """Whole body (her call 2026-11-11): arms, shoulders, chest, belly and legs, heavier on her right side (the
    viewer's left from the front). Silhouette measured on tools/gojo/pawnparts/Naked_Thin_south.png: x 50-76,
    y 43-109, shoulders at row 46."""
    im, d = canvas()            # south and north
    streaks(d, 51, 56, 48, 100, 4, 4)          # right arm (heavier)
    streaks(d, 71, 75, 50, 98, 3, 5)           # left arm
    streaks(d, 56, 70, 47, 84, 7, 21)          # chest and belly
    streaks(d, 53, 62, 86, 108, 4, 22)         # right leg
    streaks(d, 65, 73, 88, 106, 3, 23)         # left leg
    for i, (x, y, rx, ry) in enumerate(((56, 52, 3, 3.5), (60, 66, 4, 3), (68, 58, 3, 3), (57, 80, 3.5, 3),
                                        (66, 76, 3, 3.5), (55, 96, 3, 4), (69, 98, 2.5, 3))):
        blotch(d, x, y, rx, ry, 30 + i)
    save(im, "JJK_MakiScarsBody", "south")
    save(im, "JJK_MakiScarsBody", "north")
    im, d = canvas()            # east: the side view, arm down the middle, torso and leg
    streaks(d, 58, 68, 48, 104, 6, 6)
    for i, (x, y, rx, ry) in enumerate(((62, 58, 3, 3.5), (64, 76, 3.5, 3), (61, 94, 3, 3.5))):
        blotch(d, x, y, rx, ry, 40 + i)
    save(im, "JJK_MakiScarsBody", "east")


face()
body()
print("ok")
