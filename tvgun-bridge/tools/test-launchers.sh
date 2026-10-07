#!/usr/bin/env bash
# test-launchers.sh - dry-run make-launchers.sh and assert on the output.
# Asserts: 27 game bats + 1 hall bat, correct --game id / --profile xml per game,
# CRLF line endings, pure ASCII. Prints PASS when all checks succeed.

set -euo pipefail

ROOT="D:/yinwu"
TOOLS="$ROOT/tvgun-bridge/tools"
GAMES_DIR="$ROOT/tvgun-bridge/src/TvgunBridge.Core/games"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

bash "$TOOLS/make-launchers.sh" --dry-run --out "$OUT" > /dev/null

fail() { echo "FAIL: $*" >&2; exit 1; }

# Byte-level checks (this machine's grep has no working -P, so count bytes instead).
# CRLF-clean: number of CR bytes equals number of LF bytes, and file is non-empty.
assert_crlf() {
    local f="$1" lfs crs
    lfs=$(wc -l < "$f")
    crs=$(tr -cd '\r' < "$f" | wc -c)
    [ "$lfs" -gt 0 ] || fail "$f: empty"
    [ "$lfs" = "$crs" ] || fail "$f: LF lines=$lfs but CR bytes=$crs (not pure CRLF)"
}
# ASCII-clean: deleting all bytes 0x00-0x7F leaves nothing.
assert_ascii() {
    local f="$1"
    if tr -d '\000-\177' < "$f" | grep -q .; then fail "$f: non-ASCII bytes"; fi
}

# 1. file count: 27 game bats + hall.bat
n=$(find "$OUT" -maxdepth 1 -name '*.bat' | wc -l)
[ "$n" = "28" ] || fail "expected 28 bats (27 games + hall), got $n"
[ -f "$OUT/hall.bat" ] || fail "hall.bat missing"

# 2. per-game assertions
count=0
for f in "$GAMES_DIR"/*.json; do
    id=$(grep -m1 '"id":' "$f" | sed 's/.*"id": *"//; s/".*//')
    profile=$(grep -m1 '"teknoParrotProfile":' "$f" | sed 's/.*"teknoParrotProfile": *"//; s/".*//')
    bat="$OUT/$id.bat"
    [ -f "$bat" ] || fail "$bat missing"
    grep -q -- "--game $id" "$bat" || fail "$bat: missing '--game $id'"
    grep -q -- "--profile=$profile" "$bat" || fail "$bat: missing '--profile=$profile'"
    assert_crlf "$bat"
    assert_ascii "$bat"
    count=$((count + 1))
done
[ "$count" = "27" ] || fail "expected 27 game bats checked, got $count"

# 3. hall bat: CRLF + ASCII + has 27 menu entries + 27 :launch call sites
assert_crlf "$OUT/hall.bat"
assert_ascii "$OUT/hall.bat"
entries=$(grep -c '^call :launch' "$OUT/hall.bat" || true)
[ "$entries" = "27" ] || fail "hall.bat: expected 27 'call :launch' lines, got $entries"
labels=$(grep -c '^:g[0-9][0-9]' "$OUT/hall.bat" || true)
[ "$labels" = "27" ] || fail "hall.bat: expected 27 game labels, got $labels"

echo "PASS ($count game launchers + hall menu verified)"
