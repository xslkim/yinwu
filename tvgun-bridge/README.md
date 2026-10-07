# tvgun-bridge

手机光枪（tvgun）→ Windows TeknoParrot 街机光枪游戏的本地桥接器。手机 App 通过局域网把瞄准坐标与扳机事件发给 PC 上的桥接器，桥接器完成校准映射后，用 SendInput 把准星与开枪注入到 TeknoParrot 运行的街机游戏窗口，并叠加白色边框辅助校准。

## 架构

| 项目 | 说明 |
| --- | --- |
| `src/TvgunBridge.Core` | 协议服务、校准映射、注入管线、窗口管理、游戏适配配置（无 UI 依赖） |
| `src/TvgunBridge.App` | WPF 控制面板 + 一键启动 CLI：桥接开关、游戏选择、校准向导、白边叠加窗、自检打鸭子、全局热键 |
| `src/TvgunBridge.ReplayClient` | 控制台回放客户端：模拟手机发协议，用于无手机闭环验证与压力回放 |
| `tests/TvgunBridge.Tests` | xunit 单元测试 + E2E 回环测试（196 用例，全部 127.0.0.1 随机端口，无需真机/管理员） |

目标框架 net9.0-windows，零第三方 NuGet 依赖。

## 构建与测试

```bash
cd tvgun-bridge
dotnet build        # 0 警告 0 错误
dotnet test         # 196/196 通过
```

ReplayClient 冒烟（对空端口验证发现超时，退出码 2）：

```bash
dotnet run --project src/TvgunBridge.ReplayClient -- --discover --port 49211
```

## 手机端对接协议

- **瞄准流**：手机持续向 PC 的 UDP **8000** 发送 ASCII `"x,y"`（一位小数，1920×1080 规范坐标，60Hz 左右），最新值覆盖，0.6s 无包自动停移。
- **开枪**：HTTP `POST http://<PC>:8000/shot`，桥接器先移准星再点击，响应 `{"hit":true,"score":0}`。扩展端点：`POST /coin`（投币）、`/start`（开始）、`/reload`（换弹）、`GET /health`。
- **服务发现**：手机向 UDP **8001** 广播 `TVGUN_DISCOVER`，桥接器应答 `TVGUN_HERE <服务端口>`。
- 以上行为与 ReplayClient 的 `PhoneSender` 完全一致，可用 ReplayClient 代替手机联调。

## 真机使用步骤

**日常玩：双击游戏目录里的 `启动游戏.bat`**（或根目录 `游戏大厅.bat` 菜单选游戏）。bat 会自动：自检管理员环境（缺 urlacl 则提权跑一次 setup-admin-once.bat）→ 杀残留 TeknoParrotUi → 起桥接器（`--game <id>` 自动模式：写 teknoparrot.ini、起协议服务、开注入、窗口出现后自动无边框铺满+白边叠加）→ TeknoParrotUi `--profile=` 直启游戏。游戏退出 10 秒后桥接器自动退出，全程无需开控制面板。

手动/排障模式（等价于旧流程）：

1. **urlacl（一次性）**：Windows 上 HttpListener 绑定 `+` 需要管理员或预先执行（App 绑定失败时会在主窗口显示该命令）：
   ```
   netsh http add urlacl url=http://+:8000/ user=<当前用户名>
   ```
   或以管理员身份运行 App。
2. **启动顺序**：先启动桥接器 App（`dotnet run --project src/TvgunBridge.App`），在下拉框选择游戏（会自动写 teknoparrot.ini：窗口化、隐藏光标、RawInput，写前备份 .bak），再通过 TeknoParrot 启动游戏。
3. **校准**：进入游戏画面后点"校准向导"，按提示对两个靶点各开一枪完成两点仿射校准（结果存 `%APPDATA%/TvgunBridge/calibration.json`）。
4. **开玩**：确认注入开关打开；热键 F5 投币 / F6 开始 / F7 注入开关 / F8 显隐窗口（可在 `%APPDATA%/TvgunBridge/hotkeys.json` 改键）。
5. **自检**：无游戏时可开"自检"窗口打弹跳靶验证 aim/shot 链路。

注意：一键启动要求桌面会话**未锁定**——锁屏下游戏图形初始化失败会自行退出、注入也无法到达游戏（e2e-realgame.sh 步骤 0 会拦截该环境）。

## 一键启动（CLI 自动模式）

```bash
TvgunBridge.App.exe --game <adapterId> [--show-ui] [--no-inject] [--exit-after-game]
```

- 无参数 = 原图形界面模式，行为不变。
- `--game <id>` 加载 `games/<id>.json`（随 exe 输出目录），自动：写 teknoparrot.ini（游戏目录缺失只警告不崩）→ 启动协议服务 → 打开注入 → 窗口出现后自动无边框铺满主屏并显示白边叠加框。默认**不显示控制面板**（不进 Alt-Tab，只有叠加窗）；`--show-ui` 可照旧打开面板。
- `--no-inject` 关闭注入（自动模式默认开）。
- `--exit-after-game` 游戏窗口连续消失 10 秒后优雅退出（还原窗口、停服务；自动模式默认开；游戏窗口从未出现则不自动退出，留给 bat/用户把关）。
- 退出码：0 正常；1 端口绑定失败；2 参数错误或游戏适配文件不存在。
- 日志：`%APPDATA%/TvgunBridge/bridge.log`（追加、带时间戳，GUI 模式同写）。关键行：`SERVER_LISTENING port=8000`、`INI_WRITTEN path=...` / `INI_SKIPPED reason=...`、`WINDOW_FOUND title="..." hwnd=0x...`、`BORDERLESS_APPLIED`、`OVERLAY_SHOWN`、`INJECTION_ENABLED`、`EXIT_KEY_SENT`、`GAME_LOST`、`SETUP_MISSING ...`、`SHUTDOWN reason=...`。

launcher bat 配合方式（管理员一次性环境准备仍由 `setup-admin-once.bat` 负责，桥接器只自检记 `SETUP_MISSING` 警告，不提权）：

```bat
rem 游戏 exe 用 Release 输出（games/*.json 已随输出复制）
start "" "D:\yinwu\tvgun-bridge\src\TvgunBridge.App\bin\Release\net9.0-windows\TvgunBridge.App.exe" --game ghost-squad-evolution
rem 再拉 TeknoParrot（profile 名取自适配器 JSON 的 teknoParrotProfile 字段）
"D:\yinwu\1846\TeknoParrotUi.exe" --profile=GSEVO.xml
```

桥接器会先等游戏窗口出现再接管，bat 中两条命令的先后顺序不敏感（先启桥接器更稳）。

闭环验收脚本（需本机 Git Bash + 已构建 Release）：

```bash
bash tools/e2e-realgame.sh --run run1    # 真实游戏（GSEVO）全链路；锁屏环境会被步骤 0 拦截
bash tools/e2e-fakewindow.sh --run fake1 # 假窗口兜底（锁屏/CI 可用）：WinForms 冒充游戏窗口验证全链路
```

两脚本全自动（清理→起桥接器→起窗口→ReplayClient 推送→/exit→自动退出断言），失败自动 dump bridge.log，截图与日志落在 `out/e2e/<run>/`（不入库）。

## 游戏适配

`src/TvgunBridge.Core/games/*.json` 内置 27 款（Ghost Squad Evolution、Point Blank X、Haunted Museum、Big Buck Hunter Pro/Home、Aliens Extermination 等）。每份含进程名、窗口标题正则、换弹策略（rightClick / offscreenShot）、teknoparrot.ini 路径与 TeknoParrot profile 名。

详细进度与真机联调清单见 [PROGRESS.md](PROGRESS.md)。
