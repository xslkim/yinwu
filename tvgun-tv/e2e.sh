#!/usr/bin/env bash
# 真端到端闭环：桌面 JVM 起 FakeTvMain（完整 TvBridgeServer + 打鸭子逻辑），
# 用 PC 版 ReplayClient 走真实线协议（UDP aim / POST /shot / 发现应答）验证。
# 断言：发现应答成功、5 发 shot 全部收到合法响应、FakeTvMain 输出 5 条 SHOT 且首发射中。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/out/e2e"
REPLAY_PROJ="$ROOT/../tvgun-bridge/src/TvgunBridge.ReplayClient"

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
DOTNET="$(command -v dotnet 2>/dev/null || true)"
[ -n "$DOTNET" ] || { echo "no dotnet found (needed for ReplayClient)"; exit 1; }
[ -d "$REPLAY_PROJ" ] || { echo "ReplayClient not found at $REPLAY_PROJ"; exit 1; }

if "$JAVAC" --release 8 -version >/dev/null 2>&1; then
  JAVAC_REL=(--release 8)
else
  JAVAC_REL=(-source 1.8 -target 1.8)
fi

echo "==> [1/5] 编译 core"
rm -rf "$OUT"; mkdir -p "$OUT/classes"
SRCS=()
while IFS= read -r f; do SRCS+=("$(w "$f")"); done < <(find "$ROOT/src/com/tvgun/tv/core" -name '*.java')
"$JAVAC" "${JAVAC_REL[@]}" -encoding UTF-8 -d "$(w "$OUT/classes")" "${SRCS[@]}"

LOG="$OUT/faketv.log"

echo "==> [2/5] 后台启动 FakeTvMain"
"$JAVA" -cp "$(w "$OUT/classes")" com.tvgun.tv.core.FakeTvMain --port 0 --static-target >"$LOG" 2>&1 &
FAKE_PID=$!
cleanup() {
  kill "$FAKE_PID" 2>/dev/null || true
  wait "$FAKE_PID" 2>/dev/null || true
}
trap cleanup EXIT

PORT=""
for i in $(seq 1 100); do
  PORT="$(sed -n 's/.*LISTEN port=\([0-9][0-9]*\).*/\1/p' "$LOG" | head -1)"
  [ -n "$PORT" ] && break
  if ! kill -0 "$FAKE_PID" 2>/dev/null; then
    echo "FakeTvMain 提前退出，日志："; cat "$LOG"; exit 1
  fi
  sleep 0.1
done
[ -n "$PORT" ] || { echo "FakeTvMain 未输出 LISTEN 行，日志："; cat "$LOG"; exit 1; }
echo "    FakeTvMain listening on port $PORT (pid $FAKE_PID)"

run_replay() {
  local dll="$REPLAY_PROJ/bin/Debug/net9.0/TvgunBridge.ReplayClient.dll"
  if [ -f "$dll" ]; then
    "$DOTNET" exec "$(w "$dll")" "$@"
  else
    "$DOTNET" run --project "$(w "$REPLAY_PROJ")" -- "$@"
  fi
}

echo "==> [3/5] 自动发现"
run_replay --host 127.0.0.1 --port "$PORT" --discover | tee "$OUT/discover.log"
grep -q "TVGUN_HERE $PORT" "$OUT/discover.log" \
  || { echo "FAIL: 发现应答不含 TVGUN_HERE $PORT"; exit 1; }
echo "    发现应答 OK"

echo "==> [4/5] 回放 grid 模式 5 发射击"
run_replay --host 127.0.0.1 --port "$PORT" --pattern grid --duration 2 --shots 5 --shot-at 960,540 \
  | tee "$OUT/replay.log"
grep -q "shots hit-valid    : 5" "$OUT/replay.log" \
  || { echo "FAIL: 5 发 shot 未全部收到合法响应"; exit 1; }
echo "    5 发 shot 全部收到合法响应"

echo "==> [5/5] 校验 FakeTvMain 输出"
sleep 0.5
SHOT_COUNT="$(grep -c '^SHOT ' "$LOG" || true)"
HIT_COUNT="$(grep -c 'HIT' "$LOG" || true)"
[ "$SHOT_COUNT" -eq 5 ] || { echo "FAIL: 期望 5 条 SHOT 日志，实际 $SHOT_COUNT"; cat "$LOG"; exit 1; }
[ "$HIT_COUNT" -ge 1 ] || { echo "FAIL: 期望至少 1 次命中（静态靶心），实际 $HIT_COUNT"; cat "$LOG"; exit 1; }
echo "    SHOT 日志 $SHOT_COUNT 条，命中 $HIT_COUNT 次"

kill "$FAKE_PID" 2>/dev/null || true
wait "$FAKE_PID" 2>/dev/null || true
trap - EXIT
if grep -q '^BYE' "$LOG"; then
  echo "    FakeTvMain 干净退出（BYE）"
else
  echo "    警告: FakeTvMain 未输出 BYE（不影响结果）"
fi

echo
echo "E2E PASS"
