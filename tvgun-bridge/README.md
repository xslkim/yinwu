# tvgun-bridge

手机光枪（tvgun）→ Windows TeknoParrot 街机光枪游戏的本地桥接器。手机 App 通过局域网把瞄准坐标与扳机事件发给 PC 上的桥接器，桥接器完成校准映射后，用 SendInput 把准星与开枪注入到 TeknoParrot 运行的街机游戏窗口，并叠加白色边框辅助校准。

## 架构

| 项目 | 说明 |
| --- | --- |
| `src/TvgunBridge.Core` | 协议服务、校准映射、注入管线、窗口管理、游戏适配配置（无 UI 依赖） |
| `src/TvgunBridge.App` | WPF 控制面板：桥接开关、游戏选择、校准向导、白边叠加窗、自检打鸭子、全局热键 |
| `src/TvgunBridge.ReplayClient` | 控制台回放客户端：模拟手机发协议，用于无手机闭环验证与压力回放 |
| `tests/TvgunBridge.Tests` | xunit 单元测试 + E2E 回环测试（90 用例，全部 127.0.0.1 随机端口，无需真机/管理员） |

目标框架 net9.0-windows，零第三方 NuGet 依赖。

## 构建与测试

```bash
cd tvgun-bridge
dotnet build        # 0 警告 0 错误
dotnet test         # 90/90 通过
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

1. **urlacl（一次性）**：Windows 上 HttpListener 绑定 `+` 需要管理员或预先执行（App 绑定失败时会在主窗口显示该命令）：
   ```
   netsh http add urlacl url=http://+:8000/ user=<当前用户名>
   ```
   或以管理员身份运行 App。
2. **启动顺序**：先启动桥接器 App（`dotnet run --project src/TvgunBridge.App`），在下拉框选择游戏（会自动写 teknoparrot.ini：窗口化、隐藏光标、RawInput，写前备份 .bak），再通过 TeknoParrot 启动游戏。
3. **校准**：进入游戏画面后点"校准向导"，按提示对两个靶点各开一枪完成两点仿射校准（结果存 `%APPDATA%/TvgunBridge/calibration.json`）。
4. **开玩**：确认注入开关打开；热键 F5 投币 / F6 开始 / F7 注入开关 / F8 显隐窗口（可在 `%APPDATA%/TvgunBridge/hotkeys.json` 改键）。
5. **自检**：无游戏时可开"自检"窗口打弹跳靶验证 aim/shot 链路。

## 游戏适配

`src/TvgunBridge.Core/games/*.json` 内置 5 款：Ghost Squad Evolution、Point Blank X、Haunted Museum、Big Buck Hunter Pro、Aliens Extermination。每份含进程名、窗口标题正则、换弹策略（rightClick / offscreenShot）与 teknoparrot.ini 路径。

详细进度与真机联调清单见 [PROGRESS.md](PROGRESS.md)。
