"""Old scars for Modulo Yuji's face (Sukuna_ModuloScars), in their own colour (the tattoo
uses the Cutout shader, so skin tone doesn't tint them). Head geometry as in
art_characters.py: south face x 48-79, eyes x 51-59 / 68-76 at y 67-72; east eye x 74-81.

  python tools/art_scars.py OUT_TEXTURES_DIR"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

K = 4
N = 128 * K
EDGE = (120, 66, 64, 255)
FILL = (214, 140, 134, 255)


def scar(dr, pts, w):
    p = [(x * K, y * K) for x, y in pts]
    dr.line(p, fill=EDGE, width=int((w + 1.0) * K), joint="curve")
    dr.line(p, fill=FILL, width=int(w * K), joint="curve")


def scars(d):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    if d == "south":
        scar(dr, [(53, 74.5), (55.5, 81)], 1.6)            # Mahito's two, one under each eye
        scar(dr, [(74, 74.5), (71.5, 80.5)], 1.6)
        scar(dr, [(67, 61.5), (70.5, 66)], 1.4)            # later ones: across the left brow
        scar(dr, [(57, 83.5), (62, 85)], 1.3)              # chin
        scar(dr, [(48.5, 69), (49.5, 74)], 1.2)            # right temple
    elif d == "east":
        scar(dr, [(75, 74.5), (73, 80.5)], 1.6)
        scar(dr, [(74, 61.5), (77.5, 66)], 1.4)
        scar(dr, [(68, 83), (72.5, 84.5)], 1.3)
    return img.resize((128, 128), Image.LANCZOS)


out = Path(sys.argv[1]) / "Things/Pawn/Humanlike/Tattoos/Sukuna_ModuloScars"
out.mkdir(parents=True, exist_ok=True)
for d in ("south", "east", "north"):
    img = scars(d)
    sys.path.insert(0, str(Path(__file__).parent))
    from art_hair2 import yuji_marks          # Yuji keeps his under-eye marks as Modulo
    img.alpha_composite(yuji_marks(d))
    img.save(out / f"Sukuna_ModuloScars_{d}.png")
