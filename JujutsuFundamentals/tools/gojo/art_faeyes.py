"""Gojo's eyes for Facial Animation (her note 2026-11-07: "a tiny tiny bit smaller" than its Wide
eyes, which replaced the bored-looking Normal ones): FA's own Wide eye pictures, shrunk to SCALE
around each view's eye centre. Masks and highlights get the same centre so they stay aligned.
The def (Gojo_EyeWideSmall, probability 0 so nobody else gets it) is in Defs/Character.xml.

  python tools/art_faeyes.py FA_WIDE_EYES_DIR OUT_DIR
  (FA_WIDE_EYES_DIR = <workshop>/1635901197/1.6/Textures/Things/Pawn/Humanlike/Eyes/Wide)
"""
import sys
from pathlib import Path
from PIL import Image

SCALE = 0.93


def centre(path):
    a = Image.open(path).convert("RGBA").getchannel("A")
    x0, y0, x1, y1 = a.getbbox()
    return (x0 + x1) / 2, (y0 + y1) / 2


def main():
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    for g in ("Male", "Female"):
        d = src / g
        cs = centre(d / "normal_south.png")
        ce = centre(d / "normal_east.png")
        (out / g).mkdir(parents=True, exist_ok=True)
        for f in sorted(d.glob("*.png")):
            view = "south" if "south" in f.name else "east" if "east" in f.name else "west"
            im = Image.open(f)
            mode = im.mode
            im = im.convert("RGBA")
            w, h = im.size
            cx, cy = cs if view == "south" else ce if view == "east" else (w - ce[0], ce[1])
            small = im.resize((round(w * SCALE), round(h * SCALE)), Image.LANCZOS)
            # the background is whatever fills the corner (clear for pictures, solid green for masks)
            canvas = Image.new("RGBA", (w, h), im.getpixel((0, 0)))
            canvas.paste(small, (round(cx - cx * SCALE), round(cy - cy * SCALE)))
            (canvas if mode == "RGBA" else canvas.convert(mode)).save(out / g / f.name)


if __name__ == "__main__":
    main()
