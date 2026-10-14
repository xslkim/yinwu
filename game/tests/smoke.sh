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
"$GODOT" --headless --path "$GAME" --import || { echo "[smoke] import 失败"; exit 1; }

echo "[smoke] 2/3 单元测试 ..."
"$GODOT" --headless --path "$GAME" --script res://tests/run_tests.gd || { echo "[smoke] 测试失败"; exit 1; }

echo "[smoke] 3/3 headless 启动游戏（300 帧后自动退出）..."
"$GODOT" --headless --path "$GAME" --quit-after 300 || { echo "[smoke] 游戏启动崩溃"; exit 1; }

echo "[smoke] 全部通过 ✔"
