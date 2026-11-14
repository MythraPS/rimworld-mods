#!/usr/bin/env bash
# Automated in-game tests (dev only; not part of a release). Builds the mod and the JJKTestRunner mod, starts
# RimWorld on an isolated data folder (her saves, mod list and settings are never touched) with -quicktest -jjktest,
# waits for the runner to write its results and quit, then prints them.
#   tools/run_tests.sh            build both, run
#   tools/run_tests.sh --nobuild  run only
set -uo pipefail
MOD="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$HOME/.local/share/Steam/steamapps/common/RimWorld"
RUNNER="$GAME/Mods/JJKTestRunner"
DATA="${JJK_TEST_DATA:-/tmp/claude-1000/-home-mythraps/b9a1f675-8722-4c82-8f76-4d2950491e43/scratchpad/rwtest}"
# whichever game is installed: 1.6 (Unity, RimWorldLinux) or 1.7 (Waylith, RimWorld)
VER="1.7"; EXE="./RimWorld"
if grep -q '^1\.6' "$GAME/Version.txt" 2>/dev/null; then VER="1.6"; EXE="./RimWorldLinux"; fi
echo "game $VER"
if [ "${1:-}" != "--nobuild" ]; then
  bash "$MOD/tools/build.sh" "$VER" || exit 1
  bash "$RUNNER/build.sh" "$VER" 2>&1 | grep -v "CS1701\|warning CS" || exit 1
fi
[ -f "$DATA/Config/ModsConfig.xml" ] || { echo "no test data folder at $DATA (see JJKTestRunner/README)"; exit 1; }
rm -f "$DATA/jjktest-results.txt" "$DATA/jjktest-progress.txt"
# always muted (her ask 2026-11-14): every volume slider to 0, master and UI included
sed -i -E 's#<volume(Master|Game|Music|Ambient|UI)>[^<]*</volume#<volume\1>0</volume#' "$DATA/Config/Prefs.xml"
cd "$GAME"
timeout "${JJK_TEST_TIMEOUT:-1500}" $EXE -savedatafolder="$DATA" -quicktest -jjktest > "$DATA/stdout.log" 2>&1
echo "game exited: $?"
cat "$DATA/jjktest-progress.txt" 2>/dev/null | tail -5
echo "----"
cat "$DATA/jjktest-results.txt" 2>/dev/null || echo "no results file (see $DATA/stdout.log and the Logs folder)"
