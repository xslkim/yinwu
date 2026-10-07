#!/usr/bin/env bash
# e2e-realgame.sh — 真实游戏闭环验收：一键启动 → 窗口接管 → 输入注入 → 退出
#
# 目标游戏：ghost-squad-evolution（GSEVO，Lindbergh，窗口标题 "TeknoBudgie - Ghost Squad Evo"）。
# 流程：桌面锁定检测 → 清理残留 → 起桥接器(--game) → 起 TeknoParrotUi → 等窗口接管
#       → ReplayClient 推送 3 枪 → POST /exit（ESC）→ 等 SHUTDOWN reason=game_exited
#       → 清理 → PASS/FAIL 汇总。
# 无论成败都会 taskkill 全部残留进程（EXIT trap），不遗留全屏窗口。
#
# 前置条件：桌面会话**未锁定**（锁屏时游戏图形初始化失败会自行退出、注入不到达，
# 本脚本会在步骤 0 直接 FAIL；锁屏/CI 环境请用 tools/e2e-fakewindow.sh 兜底）。
#
# 用法：bash tools/e2e-realgame.sh [--run <标签>]
#   --run <标签>   第 N 遍运行的标签，截图/日志以此区分（默认 run1）。
# 退出码：0 = 全部断言通过；1 = 有断言失败。

set -u

# ---------- 参数 ----------
RUN_LABEL="run1"
while [ $# -gt 0 ]; do
    case "$1" in
        --run) RUN_LABEL="${2:?--run 需要标签}"; shift 2 ;;
        *) echo "未知参数：$1" >&2; exit 2 ;;
    esac
done

# ---------- 路径与常量 ----------
BRIDGE_EXE="D:\\yinwu\\tvgun-bridge\\src\\TvgunBridge.App\\bin\\Release\\net9.0-windows\\TvgunBridge.App.exe"
REPLAY_PROJECT="D:/yinwu/tvgun-bridge/src/TvgunBridge.ReplayClient"
TP_DIR="/d/yinwu/1846"
TP_EXE="TeknoParrotUi.exe"
TP_PROFILE="GSEVO.xml"
GAME_ID="ghost-squad-evolution"
PYTHON="D:/tvgun/.venv/Scripts/python.exe"
BRIDGE_LOG="$APPDATA/TvgunBridge/bridge.log"
OUT_DIR="D:/yinwu/tvgun-bridge/out/e2e/$RUN_LABEL"
mkdir -p "$OUT_DIR"

# 残留进程：桥接器、TeknoParrot 宿主、Lindbergh 加载器、游戏本体
KILL_LIST=(TvgunBridge.App.exe TeknoParrotUi.exe TeknoBudgie.exe BudgieLoader.exe vsg.exe)

# ---------- 结果记录 ----------
declare -a STEP_NAMES STEP_RESULTS STEP_ELAPSED
FAILURES=0

record_step() { # name result(PASS/FAIL/SKIP) elapsed
    STEP_NAMES+=("$1"); STEP_RESULTS+=("$2"); STEP_ELAPSED+=("$3")
    printf '[%-4s] %-34s (%ss)\n' "$2" "$1" "$3"
    [ "$2" = "FAIL" ] && FAILURES=$((FAILURES + 1))
    return 0
}

step_start() { STEP_T0=$SECONDS; }

step_end() { # name ok(0/1)
    local elapsed=$((SECONDS - STEP_T0))
    if [ "$2" -eq 0 ]; then record_step "$1" PASS "$elapsed"; else record_step "$1" FAIL "$elapsed"; fi
}

# ---------- 工具 ----------
cleanup_processes() {
    for img in "${KILL_LIST[@]}"; do
        taskkill //F //IM "$img" >/dev/null 2>&1
    done
    return 0
}

on_exit() {
    cleanup_processes
    return 0
}
trap on_exit EXIT

# 轮询 bridge.log 出现指定模式；$1=模式 $2=超时秒 $3=步骤名。成功返回 0。
wait_for_log() {
    local pattern="$1" timeout="$2"
    local t0=$SECONDS
    while [ $((SECONDS - t0)) -lt "$timeout" ]; do
        if [ -f "$BRIDGE_LOG" ] && grep -q "$pattern" "$BRIDGE_LOG"; then
            return 0
        fi
        sleep 1
    done
    return 1
}

take_shot() { # 文件名：最多重试 3 次，错误留档
    local i
    for i in 1 2 3; do
        if "$PYTHON" -c "
from PIL import ImageGrab
ImageGrab.grab().save(r'$OUT_DIR/$1')
" 2>"$OUT_DIR/$1.err"; then
            rm -f "$OUT_DIR/$1.err"
            echo "  截图: $OUT_DIR/$1"
            return 0
        fi
        sleep 2
    done
    echo "  截图失败: $1（不阻断，错误见 $OUT_DIR/$1.err）"
    return 0
}

dump_diagnostics() {
    echo "----- bridge.log 尾部 -----"
    [ -f "$BRIDGE_LOG" ] && tail -30 "$BRIDGE_LOG" || echo "(bridge.log 不存在)"
    echo "----- 相关进程 -----"
    tasklist //FI "IMAGENAME eq TvgunBridge.App.exe" //FI "IMAGENAME eq TeknoParrotUi.exe" 2>/dev/null | grep -i -E "exe|=====" || true
    tasklist 2>/dev/null | grep -i -E "budgie|vsg|teknoparrot" || echo "(无游戏进程)"
    echo "--------------------------"
}

bridge_running() {
    tasklist //FI "IMAGENAME eq TvgunBridge.App.exe" 2>/dev/null | grep -qi "TvgunBridge.App.exe"
}

# ---------- 步骤 0：桌面会话未锁定检测 ----------
# 锁屏（LogonUI 运行）时：游戏图形初始化失败会自行退出（实测 WINDOW_FOUND 后约 25s
# 无 ESC 也 GAME_LOST），SendInput 注入也无法到达游戏，截图只能拍到锁屏——此时真实
# 游戏闭环不具判定效力，应改用 tools/e2e-fakewindow.sh 兜底。
echo "=== e2e-realgame ($RUN_LABEL) 目标: $GAME_ID ==="
step_start
RC=0
if tasklist //FI "IMAGENAME eq LogonUI.exe" 2>/dev/null | grep -qi "LogonUI.exe"; then
    RC=1
    echo "  检测到 LogonUI.exe：桌面已锁定，真实游戏闭环无效（游戏会自行退出、注入不生效）"
    echo "  请先解锁桌面再跑本脚本，或使用 tools/e2e-fakewindow.sh 兜底验证桥接链路"
fi
step_end "0.桌面会话未锁定(LogonUI检测)" "$RC"
if [ "$RC" -ne 0 ]; then
    echo "RESULT: FAIL (环境受限：桌面锁定)"
    exit 1
fi

# ---------- 步骤 1：前置清理 ----------
step_start
cleanup_processes
sleep 2
if [ -f "$BRIDGE_LOG" ]; then
    cp "$BRIDGE_LOG" "$OUT_DIR/bridge-prev.log"
    : > "$BRIDGE_LOG"
fi
cleanup_processes; RC=$?
if bridge_running; then RC=1; fi
step_end "1.前置清理(残留进程+log备份)" "$RC"

# ---------- 步骤 2：启动桥接器 ----------
step_start
cmd //c start "" "$BRIDGE_EXE" --game "$GAME_ID" >/dev/null 2>&1
RC=0
wait_for_log "SERVER_LISTENING port=8000" 30 || RC=1
step_end "2.桥接器启动 SERVER_LISTENING(30s)" "$RC"
if [ "$RC" -ne 0 ]; then dump_diagnostics; fi

# INI_WRITTEN 应在启动时已写入
step_start
RC=0
grep -q "INI_WRITTEN" "$BRIDGE_LOG" 2>/dev/null || RC=1
step_end "2b.游戏 ini 写入 INI_WRITTEN" "$RC"

# ---------- 步骤 3：启动游戏并等待窗口接管 ----------
step_start
RC=0
(cd "$TP_DIR" && cmd //c start "" "$TP_EXE" --profile="$TP_PROFILE") >/dev/null 2>&1
wait_for_log "WINDOW_FOUND" 120 || RC=1
step_end "3.游戏窗口发现 WINDOW_FOUND(120s)" "$RC"

step_start
RC=0
wait_for_log "BORDERLESS_APPLIED" 30 || RC=1
wait_for_log "OVERLAY_SHOWN" 10 || RC=1
step_end "3b.无边框+叠加边框接管(30s)" "$RC"
take_shot "1-game-started.png"

# ---------- 步骤 4：输入闭环（ReplayClient 推送 3 枪） ----------
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
take_shot "2-after-shots.png"

# ---------- 步骤 5：退出闭环 ----------
step_start
RC=0
curl -s -X POST "http://127.0.0.1:8000/exit" -d "{}" --max-time 10 >/dev/null 2>&1
wait_for_log "EXIT_KEY_SENT" 30 || RC=1
step_end "5./exit → EXIT_KEY_SENT(30s)" "$RC"
take_shot "3-before-shutdown.png"

step_start
EXIT_PATH="game_ate_esc"
if ! wait_for_log "SHUTDOWN reason=game_exited" 60; then
    # 游戏不吃 ESC：taskkill 游戏进程，桥接器应在窗口丢失 10s 后自动退出
    EXIT_PATH="taskkill_fallback"
    echo "  游戏未因 ESC 退出，taskkill 游戏进程走窗口丢失路径"
    for img in TeknoParrotUi.exe TeknoBudgie.exe BudgieLoader.exe vsg.exe; do
        taskkill //F //IM "$img" >/dev/null 2>&1
    done
    wait_for_log "SHUTDOWN reason=game_exited" 30 || EXIT_PATH="shutdown_missing"
fi
echo "  退出路径: $EXIT_PATH"
RC=0
[ "$EXIT_PATH" = "shutdown_missing" ] && RC=1
step_end "5b.桥接器自动退出 SHUTDOWN(game_exited)" "$RC"

# ---------- 步骤 6：清理断言 ----------
step_start
cleanup_processes
sleep 2
RC=0
bridge_running && RC=1
step_end "6.桥接器进程已退出" "$RC"

# ---------- 汇总 ----------
cp "$BRIDGE_LOG" "$OUT_DIR/bridge.log" 2>/dev/null
echo
echo "========== 汇总 ($RUN_LABEL) =========="
for i in "${!STEP_NAMES[@]}"; do
    printf '[%-4s] %-34s (%ss)\n' "${STEP_RESULTS[$i]}" "${STEP_NAMES[$i]}" "${STEP_ELAPSED[$i]}"
done
echo "退出路径: $EXIT_PATH"
if [ "$FAILURES" -eq 0 ]; then
    echo "RESULT: PASS"
    exit 0
fi
echo "RESULT: FAIL ($FAILURES 项失败)"
dump_diagnostics
exit 1
