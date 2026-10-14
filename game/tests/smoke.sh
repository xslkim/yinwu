#!/usr/bin/env bash
# ArcGun 冒烟测试：headless 导入 + 单元测试 + headless 启动 5 秒无崩溃
# 用法: bash game/tests/smoke.sh
set -u

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GAME="$ROOT/game"

# 自动定位 Godot 二进制（两个下载目录先到先得；console 版才能看到 stdout）
GODOT=""
for d in "$ROOT/tools/godot" "$ROOT/tools/godot2"; do
  for f in Godot_v4.7.2-stable_win64_console.exe Godot_v4.7.2-stable_win64.exe; do
    exe="$d/$f"
    if [ -f "$exe" ]; then GODOT="$exe"; break 2; fi
  done
done
if [ -z "$GODOT" ]; then
  echo "[smoke] 找不到 Godot 二进制（tools/godot 或 tools/godot2）" >&2
  exit 1
fi

echo "[smoke] Godot: $GODOT"

echo "[smoke] 1/3 headless --import ..."
"$GODOT" --headless --path "$GAME" --import 2>&1 | tee /tmp/arcgun_smoke.log | grep -E "SCRIPT ERROR|Parse Error" && { echo "[smoke] import 有脚本错误"; exit 1; }
[ ${PIPESTATUS[0]} -ne 0 ] && { echo "[smoke] import 失败"; exit 1; }

echo "[smoke] 2/3 单元测试 ..."
"$GODOT" --headless --path "$GAME" --script res://tests/run_tests.gd || { echo "[smoke] 测试失败"; exit 1; }

echo "[smoke] 3/3 headless 启动游戏（300 帧后自动退出）..."
"$GODOT" --headless --path "$GAME" --quit-after 300 2>&1 | tee /tmp/arcgun_smoke.log | grep -E "SCRIPT ERROR|Parse Error" && { echo "[smoke] 运行时有脚本错误"; exit 1; }
[ ${PIPESTATUS[0]} -ne 0 ] && { echo "[smoke] 游戏启动崩溃"; exit 1; }

# 可选：`bash game/tests/smoke.sh --full` 追加 e2e 命中率定量验证（约 20 秒）
if [ "${1:-}" = "--full" ]; then
  echo "[smoke] 附加: e2e 命中率 ..."
  bash "$GAME/tests/e2e_hit.sh" || { echo "[smoke] e2e 命中率不达标"; exit 1; }
fi

echo "[smoke] 全部通过 ✔"
