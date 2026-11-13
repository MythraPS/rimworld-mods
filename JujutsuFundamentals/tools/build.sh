#!/usr/bin/env bash
# Build the mod's code for both game versions from the one Source folder.
#   1.6: Mono's mcs against the 1.6 game (saved in ~/.local/share/rimworld-refs/1.6, since the game folder
#        now holds 1.7) and the 1.6 builds of VEF, VPE and Harmony.
#   1.7: Mono's Roslyn csc against the 1.7 game (its own .NET 10 libraries, UnityStubs and the Waylith engine)
#        and the 1.7 builds of VEF, VPE and Harmony. mcs can't read .NET 10's core library, csc can.
#   tools/build.sh        both
#   tools/build.sh 1.7    one
set -euo pipefail
MOD="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$HOME/.steam/steam/steamapps/common/RimWorld"
WS="$HOME/.local/share/Steam/steamapps/workshop/content/294100"
R16="$HOME/.local/share/rimworld-refs/1.6"
mapfile -t SRC < <(find "$MOD/Source" -name '*.cs' | sort)
[ ${#SRC[@]} -gt 0 ] || { echo "no source files" >&2; exit 1; }

need() { [ -f "$1" ] || { echo "MISSING REFERENCE: $1" >&2; exit 1; }; echo "-r:$1"; }

build16() {
  local OUT="$MOD/1.6/Assemblies/JujutsuFundamentals.dll" ARGS=()
  for r in Assembly-CSharp netstandard UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.TextRenderingModule \
           VEF VanillaPsycastsExpanded 0Harmony; do ARGS+=("$(need "$R16/$r.dll")"); done
  mkdir -p "$(dirname "$OUT")"
  echo "1.6: building ${#SRC[@]} file(s)"
  mcs -target:library -langversion:latest -sdk:4.8 -warn:2 -nowarn:618 -out:"$OUT" "${ARGS[@]}" "${SRC[@]}"
  echo "1.6: OK  $(stat -c %s "$OUT") bytes"
}

build17() {
  # .NET 10 reference assemblies (Microsoft.NETCore.App.Ref 10.0.14 from nuget.org, unpacked into
  # ~/.local/share/rimworld-refs/net10ref), so the dll references System.Runtime like other 1.7 mods.
  # Building against the game's own runtime dlls referenced System.Private.CoreLib, and the 1.7 game
  # then read our own methods' signatures wrong (BadImageFormatException, 2026-11-10).
  local OUT="$MOD/1.7/Assemblies/JujutsuFundamentals.dll" ARGS=() REF="$HOME/.local/share/rimworld-refs/net10ref/ref/net10.0"
  [ -d "$REF" ] || { echo "MISSING .NET 10 reference pack: $REF" >&2; exit 1; }
  for f in "$REF"/*.dll; do ARGS+=("-r:$f"); done
  # the 1.7 game's own dlls: saved in rimworld-refs/1.7 whenever 1.7 is installed, so 1.7 builds still work while 1.6 is
  local G17="$GAME"; [ -f "$GAME/UnityStubs.dll" ] || G17="$HOME/.local/share/rimworld-refs/1.7"
  [ -f "$G17/UnityStubs.dll" ] || { echo "1.7: no 1.7 game dlls (game is 1.6 and rimworld-refs/1.7 is empty): switch Steam to 1.7 once to save them" >&2; exit 1; }
  if [ "$G17" = "$GAME" ]; then mkdir -p "$HOME/.local/share/rimworld-refs/1.7"; cp -u "$GAME"/Assembly-CSharp.dll "$GAME"/UnityStubs.dll "$GAME"/Waylith*.dll "$HOME/.local/share/rimworld-refs/1.7/"; fi
  for f in "$G17"/Assembly-CSharp.dll "$G17"/UnityStubs.dll "$G17"/Waylith*.dll; do ARGS+=("-r:$f"); done
  ARGS+=("$(need "$WS/2023507013/1.7/Assemblies/VEF.dll")"
         "$(need "$WS/2842502659/1.7/Assemblies/VanillaPsycastsExpanded.dll")" "$(need "$WS/2009463077/1.7/Assemblies/0Harmony.dll")")
  mkdir -p "$(dirname "$OUT")"
  # build beside it and swap in only on success: a failed build must never cost the last good dll (it did, 2026-11-12)
  local TMP="$OUT.building"; rm -f "$TMP"
  echo "1.7: building ${#SRC[@]} file(s)"
  csc -target:library -langversion:latest -nostdlib+ -noconfig -nologo -nowarn:618,672 -out:"$TMP" "${ARGS[@]}" "${SRC[@]}" \
    | grep -v "^$" || true
  [ -f "$TMP" ] || { echo "1.7: build failed (the previous dll, if any, is untouched)" >&2; exit 1; }
  mv -f "$TMP" "$OUT"
  echo "1.7: OK  $(stat -c %s "$OUT") bytes"
}

case "${1:-both}" in
  1.6) build16 ;;
  1.7) build17 ;;
  *)   build16; build17 ;;
esac
