#!/usr/bin/env bash
# e2e-emulator.sh — ParrotDroid 模拟器端到端闭环验收（C1 里程碑）
#
# 闭环：PC(ReplayClient) 推光枪 aim/shot → redir/adb-forward → 模拟器内 App → 准星跟随/射击计数。
# 断言：
#   1. 游戏墙截图产出（27 格 GameWallActivity）
#   2. AIM   准星青色质心 ≈ 规范坐标 (480,270) 映射像素 (600,270)，容差 ±40px
#   3. CONN  持续 aim 流下状态行变绿（0xFF8BC34A = "光枪 已连接"）
#   4. SHOT  3 次 shot 后 logcat 出现 "shot count=3"，且 ReplayClient 3 枪全部 hit-valid
#   5. DROP  停流 ≥0.6s 后 logcat 出现 "gun connected=false"（AimSlot 超时判离线）
#
# 用法（Git Bash）：
#   bash container/e2e-emulator.sh            # 全自动：起模拟器→验收→关模拟器
#   KEEP_EMU=1 bash container/e2e-emulator.sh # 验收完保留模拟器
#   SKIP_INSTALL=1 ...                        # 跳过装包（APK 未变时提速）
# 退出码：0 全部断言通过；非 0 失败（哪个断言挂看输出 [FAIL] 行）。
set -u
cd "$(dirname "$0")/.."   # 仓库根 D:\yinwu

# ---------- 环境 ----------
EMU="$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe"
ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"
AVD=parrotdroid-dev
SERIAL=localhost:5613
CONSOLE_PORT=5612
APK=container/winlator-src/app/app/build/outputs/apk/debug/app-debug.apk
DLL=tvgun-bridge/src/TvgunBridge.ReplayClient/bin/Debug/net9.0/TvgunBridge.ReplayClient.dll
PY=/d/tvgun/.venv/Scripts/python.exe
OUT=container/e2e-out
KEEP_EMU=${KEEP_EMU:-0}
SKIP_INSTALL=${SKIP_INSTALL:-0}

mkdir -p "$OUT"
PASS=0; FAIL=0
ok()   { echo "[PASS] $1"; PASS=$((PASS+1)); }
bad()  { echo "[FAIL] $1"; FAIL=$((FAIL+1)); }
die()  { echo "[ABORT] $1"; exit 2; }

# ---------- 1. 模拟器 ----------
adb_up() { "$ADB" -s "$SERIAL" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r' | grep -q '^1$'; }

STARTED_BY_US=0
if adb_up; then
  echo "[INFO] 模拟器已在运行，复用"
else
  echo "[INFO] 启动模拟器 $AVD (ports $CONSOLE_PORT,5613)..."
  rm -f ~/.android/avd/$AVD.avd/*.lock 2>/dev/null
  "$EMU" -avd "$AVD" -ports $CONSOLE_PORT,5613 -no-snapshot-save >"$OUT/emulator.log" 2>&1 &
  STARTED_BY_US=1
  for i in $(seq 1 30); do
    "$ADB" connect $SERIAL 2>/dev/null | grep -q connected && break
    sleep 2
  done
  "$ADB" -s $SERIAL wait-for-device || die "adb 连不上 $SERIAL"
  for i in $(seq 1 90); do adb_up && break; sleep 3; done
  adb_up || die "等待 boot_completed 超时（日志 $OUT/emulator.log）"
  echo "[INFO] 启动完成"
fi

cleanup() {
  if [ "$KEEP_EMU" != "1" ] && [ "$STARTED_BY_US" = "1" ]; then
    echo "[INFO] 关闭模拟器"
    # 坑：经 adb connect 的 localhost:5613 是 TCP 设备序列号，不支持 emu kill；
    # 必须用 emulator-<console端口> 序列号。
    local ks=$SERIAL
    "$ADB" devices | grep -q "^emulator-$CONSOLE_PORT" && ks=emulator-$CONSOLE_PORT
    "$ADB" -s $ks emu kill 2>/dev/null
  fi
}
trap cleanup EXIT

# ---------- 2. 端口通路 ----------
"$ADB" -s $SERIAL forward tcp:18000 tcp:8000 || die "adb forward 失败"
TOKEN=$(cat ~/.emulator_console_auth_token)
powershell -NoProfile -ExecutionPolicy Bypass -File container/emu-console.ps1 \
  -Port $CONSOLE_PORT -Token "$TOKEN" \
  -Commands "redir add udp:18000:8000;redir add udp:18001:8001;redir list;redir list" \
  | tee "$OUT/redir.log" | grep -q "udp:18000 => 8000" || die "UDP redir 配置失败"
echo "[INFO] 端口通路就绪（tcp:18000 forward + udp:18000/18001 redir）"

# ---------- 3. 装包 ----------
if [ "$SKIP_INSTALL" != "1" ]; then
  "$ADB" -s $SERIAL install -r "$APK" | tee "$OUT/install.log" | grep -q Success \
    || die "APK 安装失败（见 $OUT/install.log；若 NO_MATCHING_ABIS 查 EMULATOR.md §5）"
  echo "[INFO] APK 安装成功"
fi

# ---------- 4. 进游戏墙 → GameLaunchActivity ----------
"$ADB" -s $SERIAL logcat -c
"$ADB" -s $SERIAL shell am start -n com.parrotdroid/.ui.GameWallActivity >/dev/null
sleep 4
"$ADB" -s $SERIAL shell input tap 1536 516   # 关掉可能的"沉浸式提示"弹窗（点在空白处无害）
sleep 1
"$ADB" -s $SERIAL exec-out screencap -p > "$OUT/wall.png"
[ -s "$OUT/wall.png" ] && ok "游戏墙截图 $OUT/wall.png" || bad "游戏墙截图失败"
"$ADB" -s $SERIAL shell input tap 560 480    # 点第一个游戏（Action Deka）
sleep 3
FOCUS=$("$ADB" -s $SERIAL shell "dumpsys window | grep mCurrentFocus" | tr -d '\r')
echo "$FOCUS" | grep -q GameLaunchActivity || die "未进入 GameLaunchActivity: $FOCUS"
echo "[INFO] 已进入 GameLaunchActivity"
"$ADB" -s $SERIAL exec-out screencap -p > "$OUT/launch0.png"

# ---------- 5a. AIM：确定轨迹 + 定点 ----------
dotnet "$DLL" --host 127.0.0.1 --port 18000 --pattern grid --duration 5 --rate 60 >"$OUT/grid.log" 2>&1 \
  || bad "grid 轨迹推送失败（见 $OUT/grid.log）"
printf '0,aim,960,540\n800,aim,480,270\n1500,aim,480,270\n' > "$OUT/hold.csv"
dotnet "$DLL" --host 127.0.0.1 --port 18000 --script "$OUT/hold.csv" >"$OUT/hold.log" 2>&1 \
  || bad "定点 script 推送失败（见 $OUT/hold.log）"
"$ADB" -s $SERIAL exec-out screencap -p > "$OUT/launch1.png"

AIM_RES=$("$PY" - <<'EOF'
from PIL import Image
import numpy as np
img = np.array(Image.open(r'D:\yinwu\container\e2e-out\launch1.png').convert('RGB')).astype(int)
r, g, b = img[:,:,0], img[:,:,1], img[:,:,2]
# CrosshairView.COLOR = 0xFF00E5FF（青色圆环+十字）
mask = (r < 80) & (g > 150) & (b > 180) & (abs(g-229) < 90) & (abs(b-255) < 75)
ys, xs = np.nonzero(mask)
if len(xs) < 100:
    print(f"FAIL cyan pixels too few: {len(xs)}"); raise SystemExit
cx, cy = xs.mean(), ys.mean()
ex, ey = 600.0, 270.0   # 规范 (480,270)@(1920x1080) → 屏 (600,270)@(2400x1080)
verdict = "PASS" if abs(cx-ex) <= 40 and abs(cy-ey) <= 40 else "FAIL"
print(f"{verdict} centroid=({cx:.1f},{cy:.1f}) expected=({ex:.0f},{ey:.0f}) err=({cx-ex:.1f},{cy-ey:.1f}) n={len(xs)}")
EOF
)
echo "$AIM_RES" | grep -q '^PASS' && ok "AIM 准星质心: $AIM_RES" || bad "AIM 准星质心: $AIM_RES"

# ---------- 5b. CONN：持续流下状态行变绿 ----------
dotnet "$DLL" --host 127.0.0.1 --port 18000 --pattern circle --duration 20 --rate 60 >"$OUT/circle.log" 2>&1 &
BG=$!
sleep 2
"$ADB" -s $SERIAL exec-out screencap -p > "$OUT/streaming.png"
CONN_RES=$("$PY" - <<'EOF'
from PIL import Image
import numpy as np
img = np.array(Image.open(r'D:\yinwu\container\e2e-out\streaming.png').convert('RGB')).astype(int)
region = img[0:70, 0:800]   # 左上角状态行
r, g, b = region[:,:,0], region[:,:,1], region[:,:,2]
green = ((abs(r-139)<40) & (abs(g-195)<40) & (abs(b-74)<40)).sum()  # 0xFF8BC34A = 已连接
print(f"{'PASS' if green > 20 else 'FAIL'} status-green-px={green}")
EOF
)
echo "$CONN_RES" | grep -q '^PASS' && ok "CONN 已连接状态: $CONN_RES" || bad "CONN 已连接状态: $CONN_RES"

# ---------- 5c. SHOT：3 枪 ----------
dotnet "$DLL" --host 127.0.0.1 --port 18000 --pattern grid --duration 2 --rate 60 \
  --shots 3 --shot-at 960,540 | tee "$OUT/shots.log" | grep -Eq "shots hit-valid\s+: 3" \
  && ok "SHOT 3 枪全部 hit-valid" || bad "SHOT 应答异常（见 $OUT/shots.log）"
kill $BG 2>/dev/null; wait $BG 2>/dev/null
sleep 1
"$ADB" -s $SERIAL exec-out screencap -p > "$OUT/shots.png"
"$ADB" -s $SERIAL logcat -d -s ParrotGun:I > "$OUT/parrotgun.log"
grep -q "shot count=3" "$OUT/parrotgun.log" && ok "SHOT logcat 出现 shot count=3" \
  || bad "SHOT logcat 未见 shot count=3（见 $OUT/parrotgun.log）"
grep -q "gun connected=true" "$OUT/parrotgun.log" && ok "CONN logcat 出现 gun connected=true" \
  || bad "CONN logcat 未见 gun connected=true"

# ---------- 5d. DROP：停流 0.6s 判离线 ----------
sleep 1
"$ADB" -s $SERIAL logcat -d -s ParrotGun:I > "$OUT/parrotgun.log"
tail -1 "$OUT/parrotgun.log" | grep -q "gun connected=false" \
  && ok "DROP 停流后判离线（connected=false）" || bad "DROP 停流后未判离线"

# ---------- 汇总 ----------
echo "=============================================="
echo "E2E 结果: PASS=$PASS FAIL=$FAIL  证据目录: $OUT/"
[ "$FAIL" = "0" ]
