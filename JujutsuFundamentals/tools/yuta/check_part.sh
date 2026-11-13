#!/usr/bin/env bash
# Build-check one Yuta part on its own, while the other parts are being written in parallel.
#   tools/yuta/check_part.sh Speech|Sky|Rika|Copy [scratch dir]
# Copies the mod to <scratch>/<part>/mod, drops the OTHER parts' Source/Yuta/<X> and Common/Defs/Yuta/<X>
# folders (so their half-written files can't break this build), then builds 1.6 + 1.7 and validates there.
# Copy needs Rika's code (Rika.Manifested lives in YutaCore, so it doesn't): pass "Copy+Rika" to keep both.
set -euo pipefail
MOD="$(cd "$(dirname "$0")/../.." && pwd)"
PARTS="$1"; SCR="${2:-/tmp/claude-1000/-home-mythraps/b9a1f675-8722-4c82-8f76-4d2950491e43/scratchpad/yuta-check}"
DEST="$SCR/${PARTS//+/_}/mod"
rm -rf "$DEST"; mkdir -p "$DEST"
rsync -a --exclude '1.6/Assemblies' --exclude '1.7/Assemblies' --exclude 'Textures' "$MOD/" "$DEST/"
ln -s "$MOD/Textures" "$DEST/Textures"
for x in Speech Sky Rika Copy; do
  case "+$PARTS+" in *"+$x+"*) ;; *) rm -rf "$DEST/Source/Yuta/$x" "$DEST/Common/Defs/Yuta/$x" ;; esac
done
bash "$DEST/tools/build.sh" 2>&1 | grep -v "warning CS1701" | grep -v "^$"
~/.venvs/dnfile/bin/python "$DEST/tools/validate.py" 2>&1 | grep -v "^note" | tail -15
