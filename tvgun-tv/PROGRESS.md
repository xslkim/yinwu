# PROGRESS —— tvgun-tv

## M4-前半（电视端 App 本体，不依赖游戏容器）—— 已完成

| 项 | 状态 | 说明 |
|---|---|---|
| core 层 AimSlot（0.6s 超时/时钟注入） | ✅ | `src/com/tvgun/tv/core/AimSlot.java` |
| core 层 Protocol（UDP "x,y"、/shot JSON、手写最小 JSON） | ✅ | `core/Protocol.java` |
| core 层 UdpAimReceiver（畸形包静默丢弃） | ✅ | `core/UdpAimReceiver.java` |
| core 层 DiscoveryResponder（port+1，TVGUN_HERE 应答） | ✅ | `core/DiscoveryResponder.java` |
| core 层 MiniHttpServer（手写 HTTP/1.1；/shot /coin /start /reload /health；404/400） | ✅ | `core/MiniHttpServer.java` |
| core 层 AffineCalibration（Solve/Transform/恒等/跨度<100 拒绝） | ✅ | `core/AffineCalibration.java`，语义对齐 PC 版 |
| core 层 CoordinateMapper（线性映射不钳制，支持屏外换弹） | ✅ | `core/CoordinateMapper.java` |
| core 层 BouncingTarget（弹跳/命中/换位，纯逻辑） | ✅ | `core/BouncingTarget.java` |
| core 层 InputSink / SelfTestSink / ContainerSink 桩 | ✅ | ContainerSink 含容器对接详细 TODO |
| core 层 TvBridgeServer 门面（Start/Stop、端口可配） | ✅ | `core/TvBridgeServer.java` |
| MainActivity 状态面板（10Hz：服务/端口/IP/aim 坐标/信号年龄/连接状态） | ✅ | 大字体、D-pad 可导航 |
| SelfTestActivity 自检打鸭子（边框+弹跳靶+青色准星+计分） | ✅ | aim 超时 0.6s 隐藏准星 |
| CalibrationActivity 两点校准（15%/85% 靶点，SharedPreferences 持久化） | ✅ | 走 RawShotListener 取原始规范坐标 |
| BorderOverlayView 可复用白边框（24px + L 角标 3×，与 PC 版一致） | ✅ | `core/FrameGeometry.java` 供复用 |
| AndroidManifest（4 权限、双 LAUNCHER category、横屏、exported） | ✅ | 常亮用 FLAG_KEEP_SCREEN_ON 代码实现 |
| test.sh：core 桌面 JVM 单测 81 项全绿 | ✅ | 2026-10-07 验证 |
| e2e.sh：FakeTvMain + ReplayClient 真端到端闭环 | ✅ | 发现应答 OK、5 发 shot 全合法响应、SHOT 日志 5 条命中 1 次（静态靶确定性） |
| build.sh：产出 tvgun-tv.apk（minSdk 26 / targetSdk 35） | ✅ | apksigner verify 通过；未安装设备（本机无连接） |

## 待容器集成清单（M4-后半）

1. **ContainerSink → 真实注入**：把 `core/ContainerSink.java` 桩替换为容器输入分发
   对接实现——onAim/onShot 的 view 像素绝对坐标 → Winlator 式 injectPointer 喂
   容器内 X Server；onShot 合成 down+~30ms up 的 click；屏外开枪/onReload → 右键
   或框外坐标；onCoin/onStart → 容器键盘事件（5/1）。对接要点见该文件 TODO 注释
   与文档 `docs/teknoparrot-android/07-输入注入与屏幕边框方案.md` 第 3 章。
2. **容器 Activity 叠加边框**：直接 `addView(new BorderOverlayView(...))` 盖在游戏
   SurfaceView 上；目标矩形 RectProvider 换为容器画面 view。
3. **双枪扩展**：协议层当前单 aim 槽；容器双枪需按来源 IP 分槽（PC 版 M3 同步演进）。
4. **延迟标定**：电视显示延迟 20-50ms，手机端 predictMs 建议 90→130ms 档；
   标定向导待第一阶段 PC 方法验收后复用。
5. **真机验证**：APK 未在真实电视/盒子上安装运行过（本机无设备）；D-pad 导航、
   字体大小、边框可视性需真机过一遍。
6. **组播发现**：已声明 CHANGE_WIFI_MULTICAST_STATE 权限；手机广播发包在部分
   盒子需 acquire MulticastLock 才能收到，真机若发现失败在 Bridge.start 处加锁。
