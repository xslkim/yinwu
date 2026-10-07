# tvgun-bridge 开发进度

> 任务来源：D:\yinwu\docs\teknoparrot-android\08-tvgun接入任务文档.md 第 5 章。
> 本文件由开发 Agent 实时更新；续跑时先读本文件确定下一个未完成任务。

## 里程碑状态

- [x] M0 桥接器骨架 + 协议服务 + 回放闭环（T0.1-T0.4）
  - [x] T0.1 解决方案骨架（Core / App / ReplayClient / Tests）
  - [x] T0.2 Core 协议服务（UDP aim / HTTP /shot / 发现应答）——Core 部分完成
  - [x] T0.3 Core 校准映射 + 注入管线 + 回放闭环测试——完成（E2E 闭环测试落地）
  - [x] T0.4 ReplayClient 接入验证（回放脚本 → 桥接器）
  - [x] T0.4 打鸭子自检模式——App 层已实现（SelfTestWindow），待真机联调
- [ ] M1 白边框叠加 + SendInput 全链路（T1.1-T1.5）——SendInput 底层（Core.Injection）已就绪
  - [x] T1.1 置顶透明点击穿透叠加窗（24px 白边+四角 L 标）——App 层已实现（OverlayBorderWindow），待真机联调
  - [x] T1.2 游戏窗口发现与跟随 + 无边框铺满——App 层已实现（BridgeRuntime 组合 WindowTracker/BorderlessForcer/Overlay），待真机联调
  - [x] T1.3 SendInput 注入器——Core 已完成（见前次日志）
  - [x] T1.4 坐标引擎 + 两点校准——Core 已完成；校准向导 UI（CalibrationWindow）App 层已实现，待真机联调
  - [ ] T1.5 GSEVO 全链路里程碑验收——待真机联调
- [ ] M2 多游戏适配 + 手感参数化 + 热键 + 稳定性（T2.1-T2.5）——27 款游戏 GameAdapter JSON 与 TeknoParrotIniWriter 已就绪
  - [x] T2.1/T2.2 游戏选择下拉 + ReloadStrategy 接线（rightClick→右键换弹、offscreenShot→屏外带钳制开枪）+ teknoparrot.ini 写入——App 层已实现，待真机联调
  - [x] T2.3 本地热键兜底（WH_KEYBOARD_LL：F5 投币/F6 开始/F7 注入开关/F8 显隐窗口，投币/开始键与热键可在 hotkeys.json 配置；HTTP /coin /start /reload → KeyTap 链路）——App 层已实现，待真机联调
  - [ ] T2.4 手感调优——待真机联调
  - [x] T2.5 异常处理稳定性（拔线/关游戏无残留）——实现完成（AimSlot 超时停移、窗口 Lost 隐藏叠加、退出 UndoAll），实机验证列入联调清单
- [ ] M3 虚拟 HID（产品化，本周期不做，仅接口预留）
- [ ] M4 Android 电视端（另立项）

## 工作日志

- 2026-10-07 解决方案骨架创建完成：TvgunBridge.sln（Core / App(WPF) / ReplayClient / Tests(xunit)），net9.0-windows，首次 build 通过。
- 2026-10-07 **Core 层完成（T0.2/T0.3 的 Core 部分）**：
  - `Protocol`：AimPoint/ShotEvent、AimSlot（最新覆盖 + 0.6s 超时，时钟可注入）、UdpAimListener（"x,y" ASCII 解析，畸形包静默丢弃）、ShotHttpServer（POST /shot 回 `{"hit":true,"score":0}`；扩展端点 /coin /start /reload /health）、DiscoveryResponder（TVGUN_DISCOVER → TVGUN_HERE <port>）、BridgeServer 组合 façade（默认 8000，发现口 8001）。
  - `Coordinates`：AffineCalibration（两点仿射解算，跨度 <100 规范单位拒绝，JSON 存 %APPDATA%/TvgunBridge/calibration.json，Identity 默认）、CoordinateMapper（规范 1920x1080 → 客户区像素，边框外缘基准，公式同 run_tv.py:68-73）、VirtualDeskNormalizer（↔ SendInput 0-65535，+0.5 取整，metrics 可注入）。
  - `Injection`：IInputInjector、SendInputInjector（MOUSEEVENTF_ABSOLUTE|VIRTUALDESK，<1px 且 <8ms 节流，Click 经 SemaphoreSlim 串行化 down→hold→up）、RecordingInjector（并发安全事件记录，闭环测试关键件）、InputPipeline（125Hz 轮询 AimSlot→MoveAbsolute；shot→先移后点；aim 超时自动停移；Enabled 总开关；Tick() 公开供确定性测试）。
  - `Windowing`：GameWindowFinder（进程名 + 标题正则评分匹配，EnumWindows 全链）、BorderlessForcer（去 WS_CAPTION|WS_THICKFRAME|WS_*BOX，SetWindowPos 铺满，Undo 恢复）、WindowTracker（500ms 轮询，Found/Lost/BoundsChanged，句柄失效自动重找，Poll() 公开）。
  - `Config`：BridgeConfig（%APPDATA%/TvgunBridge/config.json，缺省完备）、GameAdapter + ReloadStrategy、TeknoParrotIniWriter（保留既有内容、写前 .bak 备份、不存在则创建；测试只用临时目录，未碰真实游戏 ini）、5 款游戏 games/*.json（Ghost Squad Evolution / Point Blank X / Haunted Museum / Big Buck Hunter Pro / Aliens Extermination；统一写 `[General] Windowed=1, HideCursor=1, Input API=RawInput`；ExecutableName/窗口名取证自 1846/GameProfiles、UserProfiles 与 HookedWindows.txt，记入各 JSON 的 notes 字段）。
  - 工程：Core.csproj 开启 TreatWarningsAsErrors + GenerateDocumentationFile，公共 API 全 XML 文档（英文）；零第三方 NuGet；games/*.json 以 Content CopyIfNewer 随引用项目输出。
  - **测试结果：72/72 通过（连跑 3 次稳定），build 0 警告 0 错误。** 网络测试全部用 127.0.0.1 随机高端口回环，无管理员/真实游戏/手机依赖。

- 2026-10-07 **ReplayClient + E2E 闭环完成（T0.3/T0.4）**：
  - `ReplayClient`（net9.0 console，零第三方依赖）：`PhoneSender`（public static 协议发送件，与真机一致的 UDP "x,y" 一位小数、POST /shot JSON、TVGUN_DISCOVER 发现；测试项目直接复用）、`ReplayScript`（CSV `tMs,type,x,y` 脚本回放）、`Patterns`（grid 5×5×0.8s / circle 半径400两圈 / jitter 中心±2px高斯(种子固定) / edges 四角四边中点；jitter Box-Muller ±3σ 截断）、`Program`（CLI：--host/--port/--script/--pattern/--duration/--rate/--shots/--shot-at/--discover；结束打印 aim 发送数、shot 数、命中数、延迟 p50/p95；退出码 0=全部 shot 合法 / 1=失败 / 2=发现超时）。发现接收循环容忍 Windows ICMP port-unreachable 导致的 WSAECONNRESET。
  - `E2E 测试`（tests 新增 4 个 E2E_*.cs，+18 用例，全部 127.0.0.1 随机高端口回环）：E2E_ProtocolLoopbackTests（UDP aim→AimSlot、POST /shot→事件+精确响应体 `{"hit":true,"score":0}`、发现应答 `TVGUN_HERE <port>`）、E2E_FullChainTests（Identity 校准 + RectD(0,0,1920,1080)：grid 10 点逐点坐标断言、shot→Move+Click(HoldMs=30) 顺序、Click 展开 down→up 边界顺序、aim 超时 150ms 后停止注入、Enabled=false 零注入；pipeline 用 Tick() 确定性驱动，网络等待用 SpinWait 超时断言）、E2E_CalibratedChainTests（a=0.9,b=50,c=1.1,d=-30 + RectD(100,50,1600,900)，Theory 4 点 + shot 验证校准后像素坐标）、E2E_ReplayClientTests（Process 启动测试输出目录里的 ReplayClient dll：--discover 命中/超时退出码 2、--script 闭环回放断言服务端收到 aim/shot 与统计输出、shot 无应答退出码 1、--pattern circle 60Hz×1s 计数验证）。
  - 测试基建：`E2EHelpers.StartBridgeServer` 对"探测端口→绑定"竞争做换端口重试（并行测试下必备）；aim 样本计数用 duration×rate 直接计算避免浮点边界。
  - **测试结果：90/90 通过（72 旧 + 18 新），连跑 3 次稳定，ReplayClient/Tests 构建 0 警告 0 错误。** 注意：全解决方案 `dotnet build` 目前被 src/TvgunBridge.App/Input/HotkeySettings.cs（缺 using System.IO，另一工作流的 M2 进行中代码）阻断，与本次改动无关，未动 App。

- 2026-10-07 **App 层（WPF 外壳）完成（T0.4 自检 / T1.1 / T1.2 / T1.4 UI / T2.1-T2.3 接线）**：
  - `OverlayBorderWindow`（T1.1）：WindowStyle=None + AllowsTransparency + Topmost + ShowInTaskbar=false，OnSourceInitialized 追加 `WS_EX_TRANSPARENT|WS_EX_LAYERED|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW`，不抢焦点不进 Alt-Tab；`FrameGeometry`（纯逻辑：4 边 24px 实心条 + 四角 3×24 L 角标，沿用 tvgun BORDER_THICK=24/CORNER_LEN=3×）+ `FrameRenderer` 画白色 Rectangle，中间完全透明；`FollowRect(RectD 屏幕像素)` 按 VisualTreeHelper.GetDpi 换算 DIP 定位，跟随 WindowTracker Found/BoundsChanged，Lost/停桥时隐藏。
  - `BridgeRuntime`：App 侧总装。组合 BridgeServer + InputPipeline + WindowTracker + BorderlessForcer + Overlay；`StartBridge()` 捕获 HttpListenerException（端口占用/无 urlacl，主窗口显示 `netsh http add urlacl url=http://+:8000/ user=...` 解决办法）与 SocketException，不崩溃；shot 不挂 pipeline.Attach 而自管（offscreenShot 策略需钳制）：映射→（策略钳制到客户区外 8px 带）→MoveAbsolute→Click(TriggerButton)；/coin /start /reload → KeyTap(CoinKey/StartKey) 或右键/屏外开枪换弹；注入总开关默认关（防误触），门控鼠标与按键注入。
  - `MainWindow`：深色控制面板，10Hz 刷新协议服务/Aim 坐标+信号年龄/游戏窗口标题+客户区/注入状态/最近动作；游戏下拉（games/*.json 5 款，启动恢复 config.activeGameId）→ SelectGame 应用 ProcessNames/WindowTitleRegex、ClickHoldMs 覆盖、TeknoParrotIniWriter 写 ini（游戏目录不存在则跳过不建目录）；按钮：开始/停止桥接、注入开关、校准向导、自检、无边框化切换、退出；关闭即退出（无托盘）。
  - `CalibrationWindow`（T1.4 UI）：全屏半透明黑，(15%,15%)→(85%,85%) 白圆环+十字靶点，订阅 ShotFeed 记 raw 规范坐标+靶点屏幕像素，两点齐调 TrySolveFromScreenPoints，成功 cal.Save()+显示系数，失败显示原因可重来，ESC 取消/R 重来。
  - `SelfTestWindow`（T0.4）：全屏黑 + 同款 24px 白边框 + `BouncingTarget`（纯逻辑：匀速弹跳、半径命中判定、随机换位）+ 青色准星（aim 经 CoordinateMapper→本窗口 DIP，60Hz）；命中得分+1 并换位；ESC 退出。
  - 热键（T2.3）：`GlobalHotkeyHook`（WH_KEYBOARD_LL，回调委托 rooted 防 GC）+ `KeyNameParser`（纯逻辑："5"/"F5"/"ESC"→VK→Core VirtualKey）+ `HotkeySettings`（%APPDATA%/TvgunBridge/hotkeys.json：CoinKey=5/StartKey=1/F5-F8 可配）。
  - 工程：app.manifest（PerMonitorV2 + dpiAware）经 ApplicationManifest 引用；App.csproj 开 TreatWarningsAsErrors；零第三方 NuGet；纯逻辑（FrameGeometry/BouncingTarget/ReloadPlanner/KeyNameParser）抽为无 UI 依赖类。
  - **全解决方案 build 0 警告 0 错误；测试 90/90 通过（未动 Core/Tests/ReplayClient）。** 未做真机启动验证（按要求）。

### 遗留事项（下一棒）

- ~~修复 App/Input/HotkeySettings.cs 编译错误~~（已在 App 层工作中修复，全解决方案 build 恢复 0 警告 0 错误；最终集成验证已复核 `using System.IO;` 存在）。
- 真机联调清单（App 层全部待真机验证）：叠加窗跟随/点击穿透、无边框铺满、校准向导、自检打鸭子、热键 F5-F8、HTTP /coin /start /reload → 按键链路、ReloadStrategy 双策略。
- Windowing 三块与 SendInputInjector 真机验证（单测不覆盖真实桌面；Windows 上 HttpListener 绑 "+" 需管理员或 `netsh http add urlacl url=http://+:8000/`，App 已在绑定失败时于主窗口显示该命令，不崩溃）。
- ~~Big Buck Hunter Pro 的 teknoParrotIniPath 指向 "Big Bug Hunter Pro" 目录（拼写存疑），首次实机联调时核对。~~ 已核实：两个目录都真实存在，非拼写错误（见 2026-10-07 适配补全日志）。
- ReplayClient 真机对照：与真机同时打同一游戏，对比 aim 流与 shot 时序（需真机环境）。
- ~~未 git commit，由后续统一提交。~~ 已提交并推送 main（见下方最终集成验证日志）。

- 2026-10-07 **最终集成验证 + 提交**：
  - 全解决方案 `dotnet build --no-incremental`：**0 警告 0 错误**（Core / App / ReplayClient / Tests 四项目全过；并行开发未留下集成缝隙，无需修复）。
  - `dotnet test`：**90/90 通过，连跑 2 次稳定**（单元 + E2E 共 90 用例）。
  - 输出一致性核对：App / Core / Tests 三个输出目录均含 games/*.json 5 个游戏适配文件。
  - CLI 冒烟（闭环真人模拟，未启动 WPF App）：`dotnet test --filter E2E_FullChainTests|E2E_ReplayClientTests` 10/10 通过（127.0.0.1 回环全链路 + ReplayClient 子进程）；`dotnet run --project src/TvgunBridge.ReplayClient -- --discover --port 49211` 对空端口 2s 超时退出码 2，符合预期。
  - git：.gitignore 白名单放行 tvgun-bridge（排除 bin/obj），随本次提交推送 origin/main。

- 2026-10-07 **游戏适配补全：5 款 → 27 款（M2 多游戏适配取证落地）**：
  - 数据源：`1846/UserProfiles/*.xml`（27 个，含 GamePath/ExecutableName/EmulatorType/GunGame）为主，`1846/GameProfiles/*.xml` 与 `1846/HookedWindows.txt`（窗口标题名单）为辅，逐款核对游戏目录内 exe/ELF 与 teknoparrot.ini 实际位置。
  - games/*.json 由 5 款扩至 27 款（新增 22 款；更新 4 款的 teknoParrotIniPath 指向 exe 所在真实目录：ghost-squad-evolution→vsg_l、big-buck-hunter-pro→Big Bug Hunter Pro\Big Bug Hunter Pro、aliens-extermination→DATA、haunted-museum→Haunted Museum 子目录；point-blank-x 原有路径已正确未动）。全部统一写 `[General] Windowed=1, HideCursor=1, Input API=RawInput`。
  - 目录名核对结论：D:\yinwu 下 "Big Buck Hunter Pro Home" 与 "Big Bug Hunter Pro" **两个目录都真实存在**，分别对应 UserProfiles/BBHHome.xml 与 BBHPro.xml 的 GamePath——既有 big-buck-hunter-pro.json 的 "Big Bug Hunter Pro" 不是拼写错误，仅补全为 exe 所在子目录；新增 big-buck-hunter-pro-home.json 对应 Home 版（该条历史遗留核对项关闭）。
  - 换弹策略：光枪游戏默认 offscreenShot；Big Buck Hunter Pro / Pro Home（泵动）rightClick；Aliens Extermination（全自动）none+clickHoldMs=30。
  - 特殊案例：vampire-night 走 Play! 模拟器（EmulatorType=Play，PS2 基板），窗口标题取自 HookedWindows.txt 的 `Play! - [ VPNGAME/TC3LOAD/TC4LOAD/CBRLOAD ]`，notes 标注"输入链路不同，需真机验证"；after-dark 的 EmulationProfile 复用 WartranTroopers，已在 notes 区分（并区别于 AfterDark2 的 Night Hunter 版）。
  - 测试：ConfigTests 由"5 款"改为遍历全部 games/*.json 断言反序列化 + 必填字段；新增 TeknoParrotIniPathParentDirectoryExists（每款断言 ini 父目录真实存在，D:\yinwu 不存在时直接返回跳过，保证 CI 可移植）。**build 0 警告 0 错误，dotnet test 139/139 通过**（90 + 22 新增反序列化用例 + 27 ini 目录用例）。

- 2026-10-08 **一键启动 Core 增量：POST /exit 端点 + GameAdapter 双字段（teknoParrotProfile / gameDirectory）**：
  - `ShotHttpServer` 新增 `POST /exit` 扩展端点：触发 `ExitReceived` 事件并回 `{"ok":true}`（语义：手机端"退出游戏"按钮 → ESC 注入，映射逻辑归 App 层）；`BridgeServer` façade 同步暴露 `ExitReceived`。
  - `GameAdapter` 新增 `teknoParrotProfile`（TeknoParrotUi `--profile=` 用的 profile 文件名，含 .xml）与 `gameDirectory`（teknoparrot.ini 所在目录，即 teknoParrotIniPath 的目录部分）；System.Text.Json 反序列化向后兼容（旧 JSON 缺字段=null，有专门测试）。
  - 27 款 games/*.json 全部填充两字段：teknoParrotProfile 逐一对应 `1846/UserProfiles/*.xml` 实际文件名（个别与 notes 写法不同以文件为准，如 after-dark→AfterDark.xml、gashaaaan-refill→Gashaaaan2.xml、primeval-hunt→Primevil.xml、vampire-night→vnight.xml）；gameDirectory 取 teknoParrotIniPath 的目录部分。27 款的 profile 在 UserProfiles 与 GameProfiles **双侧均存在**（无需留 null，skip 名单为空）。
  - 测试：新增 ShotHttpServerTests.ExitPost 回环（200+事件+响应体）、E2E ExitPost 走 BridgeServer façade、ConfigTests 扩展每款断言两字段非空 + GameDirectoryMatchesIniPathParent（纯字符串，CI 可移植）+ TeknoParrotProfileExistsInUserOrGameProfiles（D:\yinwu 不存在则跳过的可移植模式）。**build 0 警告 0 错误，dotnet test 196/196 通过，连跑 2 次稳定**（139 + 27×2 字段断言用例 + /exit×2 + 向后兼容×1）。

### 待人工核对（适配参数无法从本地取证确定）

- akuma：HookedWindows.txt 无对应窗口标题条目，windowTitleRegex 暂按目录名 `(?i)akuma` 推测，需实机确认。
- big-buck-hunter-pro-home：HookedWindows.txt 仅有 Pro 版条目，Home 版窗口标题暂用 "Big Buck Hunter Pro"（可能与 Pro 版相同），需实机确认。
- vampire-night：Play! 模拟器输入链路（非 TeknoParrot RawInput 直注），换弹/开枪链路需真机验证；processNames 中 "Play" 为推测的模拟器进程名。
- go-go-strike / family-guy-bowling（保龄类）与 medaru-no-gunman（FarCry 基板）的换弹方式暂按光枪默认 offscreenShot，实机若不同再调。

## 最终状态

### 自动化已完成（开发机可验证，已全绿）

- Core 协议服务：UDP aim "x,y"（8000）、HTTP POST /shot + /coin /start /reload /exit /health、UDP 发现应答（8001）。
- 校准映射（两点仿射）、坐标引擎、SendInput 注入管线（125Hz、超时停移、Enabled 总开关）。
- 游戏窗口发现/跟随、无边框化与 Undo、白边框叠加窗（纯逻辑全测）。
- 校准向导、自检打鸭子、热键（F5-F8 可配）、ReloadStrategy 双策略、TeknoParrotIniWriter（.bak 备份、测试仅碰临时目录）。
- ReplayClient（CSV 回放 / grid / circle / jitter / edges 模式 + 发现）与 90 项自动化测试（含 4 组 E2E 127.0.0.1 回环闭环）。

### 需真机联调（需真实桌面 / TeknoParrot / 手机枪）

- T1.5 GSEVO 全链路里程碑验收：叠加窗跟随/点击穿透、无边框铺满、aim→准星→开枪→得分全链。
- 校准向导实际两点校准手感与精度确认（T2.4 手感调优一并进行）。
- 自检打鸭子、热键 F5-F8、HTTP /coin /start /reload → KeyTap 链路、ReloadStrategy 双策略实机效果。
- T2.5 异常稳定性实机验证：拔线停移、关游戏叠加隐藏、退出 UndoAll 无残留。
- HttpListener 绑 "+" 需管理员或 `netsh http add urlacl url=http://+:8000/ user=<当前用户>`（App 绑定失败时会在主窗口显示该命令）。
- ~~Big Buck Hunter Pro 的 teknoParrotIniPath 目录拼写核对（"Big Bug Hunter Pro" 存疑）。~~ 已核实非拼写错误，Home/Pro 两款均已适配。
- ReplayClient 与真机同打一局对照 aim 流与 shot 时序。
- TeknoParrot 启动顺序：先启桥接器 App 选游戏 → 再经 TeknoParrot 启动游戏（写 ini 已自动化，首次核对备份 .bak）。
