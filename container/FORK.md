# FORK.md — ParrotDroid 对 Winlator 的 fork 收口策略

> 目的：把对上游 Winlator 的侵入面压到最小，保证上游可同步、改动可审计。
> 所有对上游既有文件的修改必须带 `// PARROTDROID: <说明>`（Java/Gradle）或
> `<!-- PARROTDROID: <说明> -->`（XML）标记注释。

## 基线

| 项 | 值 |
|---|---|
| 上游 | https://github.com/brunodev85/winlator（v11.2） |
| 主仓基线 commit | `b6b2259`（"Update README.md"） |
| Android 工程 submodule | `winlator-app` @ `a030f552`（`winlator-src/app/`） |
| 其它 submodule（未改） | `vortek` / `gladio` |
| 许可证 | LGPL-2.1（fork 修改保持同许可公开，见 docs 09 第 4 节） |

## 包名策略

- Java package **保持 `com.winlator` 不动**（200+ 文件零改动）。
- `applicationId` 在 `app/build.gradle` 改为 **`com.parrotdroid`**。
- 我们的代码全部放新包 **`com.parrotdroid.*`**（`app/src/main/java/com/parrotdroid/`）。
- 注意：Manifest 里 FileProvider authorities 仍是字面量 `com.winlator.FileProvider`，
  与 `core/FileUtils.java:413` 的硬编码一致，不随 applicationId 变化，**不要"顺手"改它**。

## 修改的上游文件清单（全部有 PARROTDROID 标记）

| 文件（winlator-src/app/ 下） | 改动 |
|---|---|
| `app/build.gradle` | ① `applicationId 'com.winlator'` → `'com.parrotdroid'`；② 新增 `testImplementation 'junit:junit:4.13.2'`（C1.4 单测） |
| `app/src/main/AndroidManifest.xml` | ① 新增 leanback/touchscreen uses-feature 声明；② 新增 `GameWallActivity`（LAUNCHER + LEANBACK_LAUNCHER，横屏）与 `GameLaunchActivity`；③ 新增 `GunCalibrationActivity`（C1.4 光枪两点校准页）；④ `MainActivity` 摘掉 LAUNCHER intent-filter（Activity 本身保留，游戏墙"设置"按钮仍打开它） |
| `app/src/main/res/values/strings.xml` | `app_name` 改为 "ParrotDroid" |

除此之外**零修改**：xserver/xenvironment/renderer/winhandler/container 等引擎代码原样保留。

## 新增文件清单

代码（winlator-app submodule 内）：

- `app/src/main/java/com/parrotdroid/GameInfo.java` — 游戏展示数据模型
- `app/src/main/java/com/parrotdroid/GameLibrary.java` — 读 assets/games/index.json
- `app/src/main/java/com/parrotdroid/ui/GameWallActivity.java` — 电视游戏墙（新 Launcher 入口）
- `app/src/main/java/com/parrotdroid/ui/GameLaunchActivity.java` — 游戏启动占位页（C1.4 已接光枪输入；C2 接容器）
- `app/src/main/java/com/parrotdroid/ui/GunCalibrationActivity.java` — 光枪两点校准页（C1.4）
- `app/src/main/java/com/parrotdroid/ui/BorderOverlayView.java` — 白边框叠加（移植自 tvgun-tv）
- `app/src/main/java/com/parrotdroid/ui/CrosshairView.java` — 青色准星（C1.4 起由输入层驱动）
- `app/src/main/java/com/parrotdroid/input/` — tvgun 光枪协议接收与输入集成（C1.4，详见下节）
- `app/src/test/java/com/parrotdroid/input/` — 协议/校准/映射/回环单测（JUnit4，`gradlew testDebugUnitTest`）
- `app/src/main/res/layout/parrotdroid_game_wall.xml`、`parrotdroid_game_item.xml`
- `app/src/main/res/drawable/parrotdroid_game_item_bg.xml`、`parrotdroid_game_item_bg_focused.xml`、`parrotdroid_icon_placeholder.xml`
- `app/src/main/res/values/parrotdroid_strings.xml`、`parrotdroid_ids.xml`
- `app/src/main/assets/games/`（27 个适配 JSON + index.json）、`app/src/main/assets/icons/`（23 个图标）
  —— 由 sync_games.sh 生成，**不要手改**，改了会被下次同步覆盖

工具（主仓 winlator-src 根目录）：

- `sync_games.sh` — 游戏数据同步脚本（games JSON 拷贝 + Metadata/图标匹配 + index.json 生成）

## git 管理

- `winlator-src` 是浅克隆，本地 commit、不推送。改动发生在 submodule 内部时：
  **先在 `winlator-src/app/`（winlator-app submodule）里 commit，再回主仓 commit 记录 submodule 指针**。
  主仓根目录的新文件（sync_games.sh）直接随主仓 commit。
- commit message 用中文，前缀标明模块（如 `app: ...`）。

## C1.4 光枪输入集成（com.parrotdroid.input）

协议层源码拷贝自 `D:\yinwu\tvgun-tv\src\com\tvgun\tv\core\*.java`（文件头均有
`// PARROTDROID: ported from tvgun-tv core, keep in sync` 标记，改协议时两边同步）：

- `Protocol` / `UdpAimReceiver` / `MiniHttpServer` / `DiscoveryResponder` / `AimSlot`
  / `AffineCalibration` / `CoordinateMapper` / `InputSink` — 纯 Java 零依赖拷贝，仅改 package。
- `ParrotInputServer` — tvgun-tv `TvBridgeServer` 的纯 Java 移植（UDP aim + HTTP
  shot/coin/start/reload + 发现应答，默认端口 8000，start 幂等）。
- `LightGunInputManager` — Android 集成层：网络线程→主线程派发、aim 120Hz
  latest-wins 按帧节流、0.6s 超时判离线、校准持久化（`files/calibration.json`）、
  socket 绑定在工作线程（端口被占走 `Listener.onError`，不崩溃）。
- `GunInputBridge` — 进程级单例，GameLaunchActivity / GunCalibrationActivity /（C2 容器页）共享。

接入：`GameLaunchActivity` onResume start / onPause stop（进校准页时保持），
aim→`CrosshairView.setAim`，shot→准星闪光+计数，coin/start/reload→底部 1s 提示，
左上角 10Hz 状态行；"校准"按钮进 `GunCalibrationActivity`（两点仿射，结果持久化）。

C2 对接预留：`LightGunInputManager.Listener` 的坐标已是容器画面 View 像素（未钳制，
框外=屏外换弹语义），C2 在真容器页把 Listener 事件转发
`XServer.injectPointerMove/injectPointerButtonPress` 即可（docs 09 §3）。

## C1.4 闭环联调说明（无真机，ReplayClient 驱动模拟器）

目标：在模拟器里打开任意游戏的 GameLaunchActivity，用宿主机脚本推 aim/shot，
看到准星移动、射击计数与底部提示，验证协议接收→映射→UI 全链路。

1. 装包进模拟器：`adb install -r winlator-src/app/app/build/outputs/apk/debug/app-debug.apk`，
   启动 ParrotDroid（游戏墙）→ 任选一款游戏进入占位页。页面左上角状态行应显示
   "光枪 未连接"。
2. 打通端口（命令以 EMULATOR.md 为准，此处为已实测写法）：TCP 用
   `adb forward tcp:18000 tcp:8000`；adb forward 不支持 UDP，UDP 走模拟器控制台
   `redir add udp:18000:8000` 和 `redir add udp:18001:8001`（发现用），
   封装脚本 `container/emu-console.ps1`，**redir 是运行时状态，每次启动模拟器需重放**。
3. 发现自检：`dotnet run --project D:\yinwu\tvgun-bridge\src\TvgunBridge.ReplayClient -- --host 127.0.0.1 --port 18000 --discover`
   （发现走 18001/UDP → guest 8001）。
4. 推轨迹：
   `dotnet run --project ...\TvgunBridge.ReplayClient -- --host 127.0.0.1 --port 18000 --pattern circle --duration 10 --rate 120 --shots 3`
   预期：准星画圆 10 秒，3 次射击均有 `{"hit":true,...}` 应答（ReplayClient 退出码 0），
   页面右下计数 +3、准星处闪光；状态行变"已连接"且 aim 坐标跳动。
   扩展端点用 curl 验证底部 1 秒提示：`curl -X POST http://127.0.0.1:18000/coin`
   （/start、/reload 同理）。
5. 校准链路：占位页"校准"按钮 → 依次对两个靶点 `--shot-at` 各发一枪
   （`--pattern grid --shots 1 --shot-at 288,162` 与 `--shot-at 1632,918`，
   对应规范坐标 15%/85%），页面 Toast"校准完成并已保存"，
   `files/calibration.json` 落盘；BACK 返回后 aim 按校准映射。
6. 停流 0.6s 后状态行应回落"未连接"（AimSlot 超时判离线）。

注意：ReplayClient 的 `--discover` 依赖 UDP 双向（guest 8001 的 redir 必须已配），
跳过第 3 步不影响 aim/shot 主链路（手机真机才强依赖发现）。

以上闭环已自动化为 `container/e2e-emulator.sh`（模拟器起停 + 装包 + 像素/logcat 断言，
C1 验收连跑 3 遍全绿，记录见 PROGRESS.md）。两个实测坑：GameLaunchActivity 未
exported，shell 不能 am start 直入，走游戏墙 `input tap 560 480`；状态行 10Hz 刷新
导致 `uiautomator dump` 永不得 idle，文本断言改走 logcat（GameLaunchActivity 以
`ParrotGun` tag 打 shot 计数与连接状态）+ 状态行颜色像素判定。

## 上游同步策略

1. 同步前先 `git log --oneline` 确认本 fork 的 commit 都在最顶上（rebase 工作流）。
2. 上游更新时，对 submodule 各自 `git fetch upstream` + rebase 我们的 commit；
   PARROTDROID 标记的 3 处上游文件是仅有的预期冲突点，逐个手工合。
3. 我们的新文件不与上游同名同路径（包/资源全部带 parrotdroid 前缀），不会冲突。
4. 季度评估一次是否跟进上游大版本（任务文档 10 第 6 节风险评估为"低"）。
5. assets 里 143MB 的 .tzst 组件包随上游更新时整体替换，无需逐文件合并。

## C2（下一棒）对接说明

- 真容器页预计基于 `XServerDisplayActivity` 改造：CrosshairView/BorderOverlayView 直接
  addView 到 `R.id.FLXServerDisplay` 根布局（docs 09 §3.3）。
- 输入复用 `GunInputBridge.get(context)` 拿同一个 LightGunInputManager，实现
  `Listener` 转发 `XServer.injectPointerMove/injectPointerButtonPress`；坐标已是
  容器画面 View 像素（未钳制，框外=屏外换弹语义）。
- 校准数据已在 `files/calibration.json`，由 manager 自动加载应用，容器页无需处理。
