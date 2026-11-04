#!/usr/bin/env bash
# Build GojoPsycasts.dll against the locally installed assemblies.
# No NuGet, no publicizer — offline and reproducible on this machine.
set -euo pipefail
MOD="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$HOME/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed"
WS="$HOME/.local/share/Steam/steamapps/workshop/content/294100"
# Use the Harmony MOD's own dll (brrainz.harmony, 2009463077). Other mods ship
# their own bundled copies, some of them Harmony 1.x with a different namespace.
HARMONY="$WS/2009463077/1.6/Assemblies/0Harmony.dll"

REFS=(
  "$GAME/Assembly-CSharp.dll"
  "$GAME/netstandard.dll"
  "$GAME/UnityEngine.dll"
  "$GAME/UnityEngine.CoreModule.dll"
  "$GAME/UnityEngine.IMGUIModule.dll"
  "$GAME/UnityEngine.TextRenderingModule.dll"
  "$WS/2023507013/1.6/Assemblies/VEF.dll"
  "$WS/2842502659/1.6/Assemblies/VanillaPsycastsExpanded.dll"
  "$HARMONY"
)
ARGS=()
for r in "${REFS[@]}"; do
  [ -f "$r" ] || { echo "MISSING REFERENCE: $r" >&2; exit 1; }
  ARGS+=("-r:$r")
done

OUT="$MOD/1.6/Assemblies/GojoPsycasts.dll"
mkdir -p "$(dirname "$OUT")"
mapfile -t SRC < <(find "$MOD/1.6/Source" -name '*.cs' | sort)
[ ${#SRC[@]} -gt 0 ] || { echo "no source files" >&2; exit 1; }

echo "building ${#SRC[@]} file(s) -> $(basename "$OUT")"
mcs -target:library -langversion:latest -sdk:4.8 -warn:2 \
    -out:"$OUT" "${ARGS[@]}" "${SRC[@]}"
echo "OK  $(stat -c %s "$OUT") bytes"
