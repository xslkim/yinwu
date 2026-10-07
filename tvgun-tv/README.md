# tvgun-tv —— tvgun 手机光枪的 Android 电视端接收 App

tvgun 手机光枪系统（D:\tvgun，手机摄像头识别屏幕白边框 + 陀螺仪融合解算指向坐标）的
**Android 电视/盒子端接收 App**：在电视端实现手机 App 预期的整套服务端协议，
让"手机 → 局域网 → 电视"链路零配置跑通。本阶段（M4 前半）不依赖游戏容器，
全部闭环可测；容器集成点已留好（见文末）。

任务来源：`docs/teknoparrot-android/08-tvgun接入任务文档.md` 第 4 章（第二阶段）。
PC 版参考实现：`tvgun-bridge/`（Core 层语义已逐一对齐）。

## 架构

```
手机 App（零改动）                     Android 电视（本工程）
┌───────────────┐  UDP 8000 "x,y"@120Hz  ┌──────────────────────────────────┐
│ 摄像头+陀螺追踪 │ ─────────────────────▶ │ UdpAimReceiver → AimSlot(0.6s超时)│
│ 点屏=扳机       │  POST /shot {x,y}      │ MiniHttpServer（/shot /coin       │
│ 自动发现        │ ─────────────────────▶ │   /start /reload /health）        │
└───────────────┘  UDP 8001 TVGUN_DISCOVER│ DiscoveryResponder（TVGUN_HERE）  │
                   ◀───────────────────── │        │                         │
                                           │ CoordinateMapper（校准+映射）   │
                                           │        ▼                         │
                                           │ InputSink ─┬─ SelfTestSink 打鸭子│
                                           │            └─ ContainerSink 桩   │
                                           └──────────────────────────────────┘
```

- **`com.tvgun.tv.core`：纯 Java、零 Android 依赖**，桌面 JVM 直接单测/端到端测。
  协议服务、坐标引擎、自检游戏逻辑、sink 接口全部在此层。
- **`com.tvgun.tv`：Android 壳**。MainActivity（状态面板 + 入口）、
  SelfTestActivity（打鸭子自检）、CalibrationActivity（两点校准）、
  BorderOverlayView（可复用白边框，容器 Activity 以后直接叠加）、
  Bridge（服务单例 + 输入出口切换）。

## 协议（与手机端对偶，手机端零改动）

| 通道 | 方向 | 内容 |
|---|---|---|
| 8000/UDP | 手机→TV | 120Hz 瞄准流，ASCII `"x,y"`（1920×1080 规范坐标，1 位小数） |
| 8000/TCP | 手机→TV | `POST /shot`，JSON `{"x":f,"y":f}`（只有按下）；TV 回 `{"hit":true,"score":0}` |
| 8001/UDP | 双向 | 手机广播 `TVGUN_DISCOVER`，TV 回 `TVGUN_HERE <port>`（=服务端口+1） |

扩展端点（手机端暂未调用，为容器/手机 UI 预留）：`POST /coin` `/start` `/reload`
（回 `{"ok":true}`）、`GET /health`（回 `{"ok":true,"aimAge":<ms|null>}`）。

坐标链：规范坐标 → AffineCalibration（两点校准，可选）→ 目标 view 像素矩形线性映射
（**不钳制**，框外坐标用于屏外换弹）。校准靶点 (15%,15%) / (85%,85%)，
跨度 <100 规范单位拒绝解算，结果持久化在 SharedPreferences。

白边框几何与 PC 版一致：`BORDER_THICK=24px`，四角 L 形角标长度 3× 边厚
（`core/FrameGeometry.java`，12 矩形组）。

## 构建 / 测试（免 gradle 手工链，机器无关）

```bash
bash test.sh    # core 层桌面 JVM 单测：javac 编译 + java 跑 main 断言（81 项）
bash e2e.sh     # 真端到端：FakeTvMain(headless 假电视) + PC 版 ReplayClient 走真实协议
bash build.sh   # 产出 tvgun-tv.apk（minSdk 26，targetSdk=本机最新 platform）
```

工具链自动探测（JDK8 javac + aapt2 + d8(需 JRE11+) + apksigner + ANDROID_SDK），
可用环境变量覆盖：`JAVAC=... JAVA11=... ANDROID_SDK=...`。完全复刻
`D:\tvgun\android\build.sh` 的模式。零第三方依赖：HTTP 服务为 ServerSocket
手写最小 HTTP/1.1（Connection: close，8 线程池）。

`build.sh install` 可一键 adb 安装并启动（本机当前无设备连接，未实测）。

## 与手机端 / 容器的关系

- **手机端零改动**：本 App 就是手机 App 视角里的"PC run_tv.py"的电视版。
  手机自动发现 → 连上 → 瞄准流进状态面板 → 自检模式开枪命中，全链路即验证完毕。
- **容器（第二阶段后半）**：游戏容器（Winlator 类）就绪后，Activity 叠加
  `BorderOverlayView` 画边框，并把 `Bridge.attach()` 的输入出口从
  `ContainerSink` 桩换成真实注入实现（injectPointer 绝对坐标喂容器内 X Server，
  对接要点已写在 `core/ContainerSink.java` 的 TODO 注释中，参考文档 07 第 3 章）。
  协议服务、校准、坐标映射全部原样复用，无需改动。
- 电视显示延迟更大，手机端 `predictMs` 建议从 90ms 调到 ~130ms 档（手机端已有旋钮）。

## 目录

```
tvgun-tv/
├── build.sh / test.sh / e2e.sh     # 三件套，全绿才算完成
├── AndroidManifest.xml             # 双 LAUNCHER category、横屏、网络权限
├── src/com/tvgun/tv/
│   ├── MainActivity.java           # 状态面板（10Hz）+ 自检/校准/端口设置
│   ├── SelfTestActivity.java       # 打鸭子：边框+弹跳靶+青色准星+计分
│   ├── CalibrationActivity.java    # 两点校准（15%/85% 靶点，持久化）
│   ├── BorderOverlayView.java      # 可复用 24px 白边框 + L 角标
│   ├── Bridge.java                 # 服务单例 + sink 切换
│   └── core/                       # 纯 Java 层（零 Android 依赖）
│       ├── AimSlot.java            # 带锁最新覆盖、0.6s 超时、时钟可注入
│       ├── Protocol.java           # "x,y" 解析/格式化、手写最小 JSON
│       ├── UdpAimReceiver.java     # DatagramSocket 循环，畸形包静默丢弃
│       ├── DiscoveryResponder.java # TVGUN_DISCOVER → TVGUN_HERE <port>
│       ├── MiniHttpServer.java     # ServerSocket 手写 HTTP/1.1 + 路由
│       ├── AffineCalibration.java  # 两点仿射 Solve/Transform/退化拒绝
│       ├── CoordinateMapper.java   # 规范→view 像素线性映射（不钳制）
│       ├── FrameGeometry.java      # 边框 12 矩形（与 PC 版一致）
│       ├── BouncingTarget.java     # 弹跳靶物理（纯逻辑）
│       ├── InputSink.java          # onAim/onShot/onCoin/onStart/onReload
│       ├── SelfTestSink.java       # 打鸭子逻辑 + 计分
│       ├── ContainerSink.java      # 容器注入桩（含详细 TODO）
│       ├── TvBridgeServer.java     # 组合门面，Start/Stop，端口可配
│       └── FakeTvMain.java         # headless 假电视（e2e 用）
└── test/CoreTest.java              # 81 项桌面 JVM 单测
```
