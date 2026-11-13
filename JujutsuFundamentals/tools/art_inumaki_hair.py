"""Inumaki's hair (her call 2026-11-12: "close to a bowl cut"): the same rounded cap as Uraume's bowl cut
(tools/art_uraume_hair.py), without the red streak and with a more tousled fringe. White fill, tinted his pale grey
in game.

  python tools/art_inumaki_hair.py HEADS_DIR"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_uraume_hair import hair, HAIRS, DIRS  # noqa: E402

heads = Path(sys.argv[1])
for d in DIRS:
    hair(heads, d, streak_on=False, nick=1.7, nicks=7).save(HAIRS / f"JJK_InumakiHair_{d}.png")
print("ok")
