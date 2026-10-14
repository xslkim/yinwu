#!/usr/bin/env bash
# e2e 命中率定量验证：钉死标靶 → 读游戏打印的靶心屏幕坐标 →
# 模拟带手抖（σ=15px）的 UDP 瞄准流打 24 发 → 命中率应 ≥ 90%
# 用法: bash game/tests/e2e_hit.sh
set -u

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GAME="$ROOT/game"
PY="D:/tvgun/.venv/Scripts/python.exe"

GODOT=""
for d in "$ROOT/tools/godot" "$ROOT/tools/godot2"; do
  for f in Godot_v4.7.2-stable_win64_console.exe Godot_v4.7.2-stable_win64.exe; do
    exe="$d/$f"
    if [ -f "$exe" ]; then GODOT="$exe"; break 2; fi
  done
done
[ -z "$GODOT" ] && { echo "[e2e] 找不到 Godot"; exit 1; }

LOG=/tmp/arcgun_e2e.log
"$GODOT" --headless --path "$GAME" --quit-after 2400 -- --udp --freeze-target > "$LOG" 2>&1 &
GPID=$!

# 等游戏打印靶心屏幕坐标
POS=""
for i in $(seq 1 50); do
  POS=$(grep -oP '靶心屏幕坐标=\(\K[0-9.]+,[0-9.]+' "$LOG" 2>/dev/null || true)
  [ -n "$POS" ] && break
  sleep 0.2
done
[ -z "$POS" ] && { echo "[e2e] 没等到靶心坐标"; kill $GPID 2>/dev/null; exit 1; }
TX="${POS%,*}"; TY="${POS#*,}"
echo "[e2e] 靶心屏幕坐标: $TX,$TY"

"$PY" - "$TX" "$TY" <<'EOF'
import random, socket, sys, time
tx, ty = float(sys.argv[1]), float(sys.argv[2])
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
dst = ("127.0.0.1", 9000)
random.seed(42)
shots = 0
for n in range(24):  # 24 发，每 6 发换一次弹
    if n % 6 == 0 and n > 0:
        s.sendto(b"RELOAD", dst); time.sleep(0.2)
    # 瞄 0.3s（σ=15px 手抖，模拟手机陀螺仪噪声），再扣扳机
    for _ in range(30):
        s.sendto(f"{tx+random.gauss(0,15):.1f},{ty+random.gauss(0,15):.1f}".encode(), dst)
        time.sleep(0.01)
    s.sendto(b"SHOT", dst)
    shots += 1
    time.sleep(0.05)
print(f"[e2e] 已打 {shots} 发")
EOF

wait $GPID
STATS=$(grep "\[Stats\]" "$LOG" | tail -1)
echo "[e2e] $STATS"
ACC=$(echo "$STATS" | grep -oP 'accuracy=\K[0-9.]+' || echo 0)
awk "BEGIN{exit !($ACC >= 0.90)}" && echo "[e2e] 命中率达标(>=90%) ✔" || { echo "[e2e] 命中率不足: $ACC"; exit 1; }
