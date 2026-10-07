# ParrotDroid 容器线开发进度

> 方案来源：D:\yinwu\docs\teknoparrot-android\00-总体落地方案.md 第 3 章。
> 本阶段目标：Winlator 本机可构建 + 集成点调研（文档 09）+ 容器任务文档（文档 10）。

## 任务状态

- [x] C0.1 Winlator 源码克隆到 container\winlator-src 并本机构建出 APK
  - 2026-10-07 完成。主仓 commit `b6b2259`（v11.2）；产物 `winlator-src\app\app\build\outputs\apk\debug\app-debug.apk`（150.2 MB，包名 com.winlator，minSdk 26，arm64-v8a）。详见 BUILD.md。
- [x] C0.2 架构调研：文档 09（输入注入/游戏启动/图形栈集成点，源码级）
- [x] C0.3 容器任务文档：文档 10（分阶段任务/验收/硬件清单）
- [x] C1.1 fork 收口：改动全部在 `com.parrotdroid.*` 新包 + 3 处带 PARROTDROID 标记的上游文件；策略见 FORK.md
- [x] C1.2 入口替换（轻裁剪）：`GameWallActivity` 接管 LAUNCHER + LEANBACK_LAUNCHER，原 MainActivity 保留并可从游戏墙"设置"进入
- [x] C1.3 电视游戏墙：27 款游戏横向网格（图标/名称/平台），D-pad 焦点放大+白描边；选中进 GameLaunchActivity 占位页（黑底+"容器未就绪"+白边框+青色准星）
- [x] C0.4 Android 模拟器环境搭建（android-34 x86_64 + WHPX + AVD parrotdroid-dev，启动/转发/UDP 全验证）
- [x] C1.4 集成 tvgun-tv core 包 + 光枪输入接入（协议接收→坐标映射→占位页准星/反馈，XServer 注入留 C2）
- [ ] C2（后续，需骁龙 8 系设备）真机跑通首款游戏

## 工作日志

- 2026-10-07 容器线启动。目标与定时续跑已建立。
- 2026-10-07 C0.1 完成：Winlator 源码（含 3 个 submodule）克隆至 container\winlator-src，`gradlew assembleDebug` 构建成功，产出 app-debug.apk（150.2 MB，com.winlator 11.2，minSdk 26，arm64-v8a）。关键坑：Gradle 工程在 submodule `app/` 下；需完整 JDK 17（用 VS 自带的 `C:\Program Files (x86)\Android\openjdk\jdk-17.0.12`）；gradle.properties 需加 `org.gradle.jvmargs=-Xmx8g` 否则压缩 assets 时 OOM；NDK r24/CMake 3.22.1 由 AGP 自动安装。复跑命令见 BUILD.md。
- 2026-10-07 C1.1/C1.2/C1.3 完成：winlator-src 内 fork 收口（app submodule commit `d75b358` + 主仓 `7d4efeb`，仅本地）。applicationId=com.parrotdroid、app_name=ParrotDroid、入口换成 GameWallActivity（LAUNCHER+LEANBACK_LAUNCHER，aapt2 badging 已核实）。游戏数据走 `winlator-src/sync_games.sh`（构建前手动跑）：27 款 games/*.json + Metadata 匹配 + 图标拷贝进 assets（23 命中、4 占位）。产物 app-debug.apk 160.1 MB，构建 0 错误，无新增警告。GameLaunchActivity 为准星/白边框预留了 getter 与 R.id（C1.4 对接说明见 FORK.md）。
  - 遗留：① 未装机运行（模拟器已就绪，见 C0.4，注意 APK 仅 arm64-v8a、镜像 x86_64，首装需验证 ARM 转译）；② after-dark/omatsuri 两款/robin-hood 图标缺源文件用占位图（1846\Icons 里只有 AfterDark2.png，后续可人工补图或改匹配规则）；③ Winlator 深层 UI 未裁（只换入口，C 后续里程碑再按 docs 09 §5 裁剪）。
- 2026-10-07 C0.4 完成：模拟器环境搭好并全链路实测。主 SDK 补装 `system-images;android-34;google_apis;x86_64`（Android 14）与 `cmdline-tools;latest`；AVD `parrotdroid-dev`（pixel_6，RAM 4096MB）。加速=WHPX 可用。关键坑：Hyper-V/winnat 端口排除段 5511-5610 盖住模拟器默认端口 5554-5585，导致误报 "too many emulator instances" 秒退——解法是固定 `-ports 5612,5613` 启动 + `adb connect localhost:5613`。无头启动 30-60 秒 boot_completed=1，getprop/截图验证通过（emulator-boot.png）。通路实测：host→guest TCP 用 `adb forward tcp:18000 tcp:8000` OK；**adb forward 不支持 UDP**，host→guest UDP 用模拟器控制台 `redir add udp:18000:8000`（封装脚本 emu-console.ps1）端到端收发 OK。完整复跑步骤与故障表见 EMULATOR.md。
  - 遗留：① `hw.initialOrientation=landscape` 未生效（开屏仍竖屏），横屏建议 App 侧强制；② arm64-only APK 装机实测待 C1.4 做；③ redir 为运行时状态，每次启动模拟器需重放。
- 2026-10-07 C1.4 完成：tvgun-tv core 8 个协议类源码拷贝进 `com.parrotdroid.input`（带 keep-in-sync 标记，零第三方依赖），新增 ParrotInputServer（TvBridgeServer 纯 Java 移植，start 幂等）、LightGunInputManager（主线程派发 + aim 按帧 latest-wins 节流 + 0.6s 离线判定 + 校准持久化 files/calibration.json + 工作线程绑端口防 NetworkOnMainThread）、GunInputBridge 单例。GameLaunchActivity 全量接入：onResume start/onPause stop（进校准页保持）、aim 驱动准星、shot 闪光+计数、coin/start/reload 底部 1s 提示、左上 10Hz 状态行、D-pad 可达"校准"按钮进 GunCalibrationActivity（两点仿射，失败提示重试，BACK 取消）。JUnit4 单测 8 类 23 例（协议解析/AimSlot 超时/仿射往返与退化/坐标映射四角/UDP·HTTP·发现·端到端回环）`testDebugUnitTest` 全绿，`assembleDebug` 0 错误。
  - 遗留：① 未装机联调（闭环步骤框架见 FORK.md"C1.4 闭环联调说明"，端口转发命令以 EMULATOR.md 为准：TCP `adb forward tcp:18000 tcp:8000`、UDP 控制台 `redir add udp:18000:8000`，redir 每次启动需重放）；② arm64-only APK 在 x86_64 镜像的装机验证未做；③ aim/shot 只到 UI 层准星，XServer.injectPointer 接线留 C2。
- 2026-10-07 **C1 里程碑验收通过**：模拟器端到端闭环（PC 推光枪坐标 → 模拟器内准星跟随）全绿。
  - 装机：arm64-only APK 直装成功（镜像带 libndk_translation，`ro.product.cpu.abilist=x86_64,arm64-v8a`），C0.4/C1.4 遗留①②清零。
  - 断言（脚本 `container/e2e-emulator.sh`，连跑 3 遍全 PASS，含 1 遍冷启动起停）：
    1. 游戏墙截图 `container/e2e-out/wall.png`（27 格）；
    2. AIM：grid 5s@60Hz 后定点 (480,270)，准星青色质心 (599.5,269.5) vs 期望 (600,270)，**误差 0.5px**（容差 ±40），`e2e-out/launch1.png`；
    3. CONN：持续流下状态行绿色像素 2400+（"光枪 已连接"），`e2e-out/streaming.png` + logcat `gun connected=true`；
    4. SHOT：3 枪全部 `{"hit":true}`（p50 延迟 11ms），logcat `shot count=3`，`e2e-out/shots.png` 右下"射击 3"；
    5. DROP：停流 0.6s 后 logcat `gun connected=false`（AimSlot 超时判离线）。
  - 验收期改动：GameLaunchActivity 增加 `ParrotGun` logcat 日志（shot 计数/连接状态）——因状态行 10Hz 刷新致 uiautomator dump 永不得 idle，UI 文本断言改走 logcat+像素色判。
  - 新坑记录：经 `adb connect localhost:5613` 的 TCP 序列号不支持 `emu kill`，须用 `emulator-5612`；GameLaunchActivity 未 exported，shell 无法 am start 直入，走游戏墙 input tap。
  - 遗留：uiautomator 文本断言不可用（上述原因）；校准链路（FORK.md 联调说明第 5 步）未纳入自动断言，手工步骤保留。
