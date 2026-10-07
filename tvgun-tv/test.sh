#!/usr/bin/env bash
# core 层桌面 JVM 单测：javac 编译 + java 跑 main 断言（复刻 tvgun android 测试模式）。
# 机器无关：自动探测 JDK；可用 JAVAC=<javac 路径> 覆盖。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/out/test"
PKG_SRC="$ROOT/src/com/tvgun/tv/core"

w() { cygpath -w "$1"; }

pick_first() { for c in "$@"; do [ -x "$c" ] && { echo "$c"; return 0; }; done; return 1; }

JAVAC="${JAVAC:-}"
if [ -z "$JAVAC" ]; then
  JAVAC="$(pick_first \
    "/c/Program Files/Android/Android Studio/jbr/bin/javac.exe" \
    "/c/Program Files/Android/jdk/jdk-8.0.302.8-hotspot/jdk8u302-b08/bin/javac.exe" \
    "$(command -v javac 2>/dev/null || true)")"
fi
[ -n "$JAVAC" ] || { echo "no javac found; set JAVAC=..."; exit 1; }
JBIN="$(dirname "$JAVAC")"
JAVA="$(pick_first "$JBIN/java.exe" "$JBIN/java" "$(command -v java 2>/dev/null || true)")"
[ -n "$JAVA" ] || { echo "no java found next to javac"; exit 1; }

# javac 参数：JDK9+ 用 --release 8，JDK8 用 -source/-target
if "$JAVAC" --release 8 -version >/dev/null 2>&1; then
  JAVAC_REL=(--release 8)
else
  JAVAC_REL=(-source 1.8 -target 1.8)
fi

echo "javac: $JAVAC"
echo "java:  $JAVA"

rm -rf "$OUT"; mkdir -p "$OUT/classes"
SRCS=()
while IFS= read -r f; do SRCS+=("$(w "$f")"); done < <(find "$PKG_SRC" "$ROOT/test" -name '*.java')
"$JAVAC" "${JAVAC_REL[@]}" -encoding UTF-8 -d "$(w "$OUT/classes")" "${SRCS[@]}"

"$JAVA" -cp "$(w "$OUT/classes")" CoreTest
