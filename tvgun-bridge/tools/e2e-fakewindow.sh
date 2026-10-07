#!/usr/bin/env bash
# e2e-fakewindow.sh — 假窗口兜底闭环：真实游戏不可用时的等效验证（也供 CI 使用）。
#
# 用 PowerShell WinForms 起一个标题恰为 "TeknoBudgie - Ghost Squad Evo" 的 1920x1080
# 窗口冒充 GSEVO，验证桥接器全链路：SERVER_LISTENING → WINDOW_FOUND → BORDERLESS_APPLIED
# → OVERLAY_SHOWN → ReplayClient 推送（INJECTION_ENABLED）→ POST /exit（EXIT_KEY_SENT，
# 假窗口不死、桥接器不得自动退出）→ 手动关窗 → GAME_LOST → SHUTDOWN reason=game_exited。
#
# 用法：bash tools/e2e-fakewindow.sh [--run <标签>]
# 退出码：0 = 全部断言通过；1 = 有断言失败。

set -u

RUN_LABEL="fake1"
while [ $# -gt 0 ]; do
    case "$1" in
        --run) RUN_LABEL="${2:?--run 需要标签}"; shift 2 ;;
        *) echo "未知参数：$1" >&2; exit 2 ;;
    esac
done

BRIDGE_EXE="D:\\yinwu\\tvgun-bridge\\src\\TvgunBridge.App\\bin\\Release\\net9.0-windows\\TvgunBridge.App.exe"
REPLAY_PROJECT="D:/yinwu/tvgun-bridge/src/TvgunBridge.ReplayClient"
GAME_ID="ghost-squad-evolution"
BRIDGE_LOG="$APPDATA/TvgunBridge/bridge.log"
OUT_DIR="D:/yinwu/tvgun-bridge/out/e2e/$RUN_LABEL"
mkdir -p "$OUT_DIR"

FAKE_TITLE="TeknoBudgie - Ghost Squad Evo"
FAKE_PS1="$OUT_DIR/fakewindow.ps1"
FAKE_PID_FILE="$OUT_DIR/fakewindow.pid"
FAKE_PID=""

KILL_LIST=(TvgunBridge.App.exe TeknoParrotUi.exe TeknoBudgie.exe BudgieLoader.exe vsg.exe)

declare -a STEP_NAMES STEP_RESULTS STEP_ELAPSED
FAILURES=0

record_step() {
    STEP_NAMES+=("$1"); STEP_RESULTS+=("$2"); STEP_ELAPSED+=("$3")
    printf '[%-4s] %-40s (%ss)\n' "$2" "$1" "$3"
    [ "$2" = "FAIL" ] && FAILURES=$((FAILURES + 1))
    return 0
}

step_start() { STEP_T0=$SECONDS; }
step_end() {
    local elapsed=$((SECONDS - STEP_T0))
    if [ "$2" -eq 0 ]; then record_step "$1" PASS "$elapsed"; else record_step "$1" FAIL "$elapsed"; fi
}

kill_fake_window() {
    if [ -n "$FAKE_PID" ]; then
        taskkill //F //PID "$FAKE_PID" >/dev/null 2>&1
        FAKE_PID=""
    fi
    rm -f "$FAKE_PID_FILE"
}

cleanup_processes() {
    kill_fake_window
    for img in "${KILL_LIST[@]}"; do
        taskkill //F //IM "$img" >/dev/null 2>&1
    done
}

on_exit() {
    cleanup_processes
    return 0
}
trap on_exit EXIT

wait_for_log() {
    local pattern="$1" timeout="$2" t0=$SECONDS
    while [ $((SECONDS - t0)) -lt "$timeout" ]; do
        if [ -f "$BRIDGE_LOG" ] && grep -q "$pattern" "$BRIDGE_LOG"; then
            return 0
        fi
        sleep 1
    done
    return 1
}

bridge_running() {
    tasklist //FI "IMAGENAME eq TvgunBridge.App.exe" 2>/dev/null | grep -qi "TvgunBridge.App.exe"
}

dump_diagnostics() {
    echo "----- bridge.log 尾部 -----"
    [ -f "$BRIDGE_LOG" ] && tail -30 "$BRIDGE_LOG" || echo "(bridge.log 不存在)"
    echo "----- 相关进程 -----"
    tasklist 2>/dev/null | grep -i -E "tvgunbridge|powershell|budgie|vsg|teknoparrot" || echo "(无)"
    echo "--------------------------"
}

# ---------- 步骤 1：前置清理 ----------
echo "=== e2e-fakewindow ($RUN_LABEL) 假窗口: \"$FAKE_TITLE\" ==="
step_start
cleanup_processes
sleep 2
if [ -f "$BRIDGE_LOG" ]; then
    cp "$BRIDGE_LOG" "$OUT_DIR/bridge-prev.log" 2>/dev/null
    : > "$BRIDGE_LOG"
fi
RC=0
bridge_running && RC=1
step_end "1.前置清理" "$RC"

# ---------- 步骤 2：启动桥接器 ----------
step_start
cmd //c start "" "$BRIDGE_EXE" --game "$GAME_ID" >/dev/null 2>&1
RC=0
wait_for_log "SERVER_LISTENING port=8000" 30 || RC=1
step_end "2.桥接器启动 SERVER_LISTENING(30s)" "$RC"
[ "$RC" -ne 0 ] && dump_diagnostics

# ---------- 步骤 3：起假窗口并等待接管 ----------
cat > "$FAKE_PS1" <<'EOF'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = 'TeknoBudgie - Ghost Squad Evo'
$form.Width = 1920
$form.Height = 1080
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point(0, 0)
$form.BackColor = [System.Drawing.Color]::Black
$label = New-Object System.Windows.Forms.Label
$label.Text = 'FAKE GSEVO WINDOW - e2e'
$label.ForeColor = [System.Drawing.Color]::Lime
$label.Font = New-Object System.Drawing.Font('Consolas', 36)
$label.AutoSize = $true
$form.Controls.Add($label)
$PID | Out-File -FilePath $args[0] -Encoding ascii
$form.Show()
[System.Windows.Forms.Application]::Run($form)
EOF

step_start
RC=0
cmd //c start "" powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$FAKE_PS1")" "$(cygpath -w "$FAKE_PID_FILE")" >/dev/null 2>&1
# 等 PID 文件出现
t0=$SECONDS
while [ $((SECONDS - t0)) -lt 20 ] && [ ! -s "$FAKE_PID_FILE" ]; do sleep 1; done
if [ -s "$FAKE_PID_FILE" ]; then
    FAKE_PID=$(tr -d '[:space:]' < "$FAKE_PID_FILE")
else
    RC=1
fi
step_end "3.假窗口启动(WinForms 1920x1080)" "$RC"

step_start
RC=0
wait_for_log "WINDOW_FOUND" 30 || RC=1
step_end "3b.WINDOW_FOUND(30s)" "$RC"

step_start
RC=0
wait_for_log "BORDERLESS_APPLIED" 30 || RC=1
wait_for_log "OVERLAY_SHOWN" 10 || RC=1
step_end "3c.BORDERLESS_APPLIED+OVERLAY_SHOWN" "$RC"

# ---------- 步骤 4：输入闭环 ----------
step_start
RC=0
grep -q "INJECTION_ENABLED" "$BRIDGE_LOG" 2>/dev/null || RC=1
if [ "$RC" -eq 0 ]; then
    dotnet run --project "$REPLAY_PROJECT" -c Release -- \
        --host 127.0.0.1 --port 8000 --pattern circle --duration 5 --rate 60 \
        --shots 3 --shot-at 960,540 > "$OUT_DIR/replay.log" 2>&1
    RC=$?
    tail -8 "$OUT_DIR/replay.log" | sed 's/^/  replay| /'
fi
step_end "4.输入闭环 ReplayClient 3枪(exit=0)" "$RC"

# ---------- 步骤 5：/exit —— 假窗口不死，桥接器不得自动退出 ----------
step_start
RC=0
curl -s -X POST "http://127.0.0.1:8000/exit" -d "{}" --max-time 10 >/dev/null 2>&1
wait_for_log "EXIT_KEY_SENT" 30 || RC=1
step_end "5./exit → EXIT_KEY_SENT(30s)" "$RC"

step_start
RC=0
sleep 15
bridge_running || RC=1
grep -q "SHUTDOWN" "$BRIDGE_LOG" 2>/dev/null && RC=1
step_end "5b.假窗口存活期间桥接器不退出(15s)" "$RC"

# ---------- 步骤 6：关假窗口 → GAME_LOST → 自动退出 ----------
step_start
RC=0
kill_fake_window
wait_for_log "GAME_LOST" 30 || RC=1
step_end "6.关窗 → GAME_LOST(30s)" "$RC"

step_start
RC=0
wait_for_log "SHUTDOWN reason=game_exited" 30 || RC=1
step_end "6b.SHUTDOWN reason=game_exited(30s)" "$RC"

step_start
sleep 2
RC=0
bridge_running && RC=1
step_end "6c.桥接器进程已退出" "$RC"

# ---------- 汇总 ----------
cp "$BRIDGE_LOG" "$OUT_DIR/bridge.log" 2>/dev/null
echo
echo "========== 汇总 ($RUN_LABEL) =========="
for i in "${!STEP_NAMES[@]}"; do
    printf '[%-4s] %-40s (%ss)\n' "${STEP_RESULTS[$i]}" "${STEP_NAMES[$i]}" "${STEP_ELAPSED[$i]}"
done
if [ "$FAILURES" -eq 0 ]; then
    echo "RESULT: PASS"
    exit 0
fi
echo "RESULT: FAIL ($FAILURES 项失败)"
dump_diagnostics
exit 1
