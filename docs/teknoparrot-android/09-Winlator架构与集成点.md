# 09 - Winlator 源码级架构调研与 ParrotDroid 集成点

> 调研对象：[brunodev85/winlator](https://github.com/brunodev85/winlator)（Winlator 11.2，main 分支，调研时间 2025 年）
> 调研方式：GitHub API 拉取仓库树 + raw.githubusercontent.com 逐文件精读
> 目标：为 ParrotDroid（Android 电视/骁龙设备上的街机光枪游戏容器）定位三个二次开发集成点：**输入注入**、**游戏启动与内容管理**、**图形栈与显示**，并给出许可边界与裁剪建议。

---

## 目录

- [0. 仓库结构与版本事实](#0-仓库结构与版本事实)
- [1. 输入注入集成点（最重要）](#1-输入注入集成点最重要)
  - [1.1 显示与输入总体架构：纯 Java 实现的 X Server](#11-显示与输入总体架构纯-java-实现的-x-server)
  - [1.2 绝对坐标注入的确切入点：XServer.inject\* 系列方法](#12-绝对坐标注入的确切入点xserverinject-系列方法)
  - [1.3 坐标系换算在哪一层](#13-坐标系换算在哪一层)
  - [1.4 三种触摸模式与外接鼠标路径对比](#14-三种触摸模式与外接鼠标路径对比)
  - [1.5 第二条输入通路：WinHandler → winhandler.exe（Windows 层注入）](#15-第二条输入通路winhandler--winhandlerexewindows-层注入)
  - [1.6 UDP 光枪接收线程的接入方案（类/方法级建议）](#16-udp-光枪接收线程的接入方案类方法级建议)
- [2. 游戏启动与内容管理集成点](#2-游戏启动与内容管理集成点)
  - [2.1 Container 数据模型与目录结构](#21-container-数据模型与目录结构)
  - [2.2 启动一个 exe 的完整调用链](#22-启动一个-exe-的完整调用链)
  - [2.3 游戏打包/导入方案：做成"选游戏即玩"](#23-游戏打包导入方案做成选游戏即玩)
  - [2.4 Box64/Wine/Turnip/DXVK 的组件形态与替换方法](#24-box64wineturnipdxvk-的组件形态与替换方法)
  - [2.5 环境变量与启动参数注入点汇总](#25-环境变量与启动参数注入点汇总)
- [3. 图形栈与显示集成点](#3-图形栈与显示集成点)
  - [3.1 渲染链路全景](#31-渲染链路全景)
  - [3.2 分辨率、全屏与缩放模式](#32-分辨率全屏与缩放模式)
  - [3.3 白边框叠加层（Android View）与游戏画面对齐要点](#33-白边框叠加层android-view与游戏画面对齐要点)
  - [3.4 输入/显示延迟链路上的已知优化开关](#34-输入显示延迟链路上的已知优化开关)
- [4. 许可与边界](#4-许可与边界)
  - [4.1 许可证事实（不是 MIT）](#41-许可证事实不是-mit)
  - [4.2 内置组件许可一览](#42-内置组件许可一览)
  - [4.3 已知局限](#43-已知局限)
- [5. 裁剪建议与工作量分级](#5-裁剪建议与工作量分级)
- [附录 A：关键文件清单与 URL](#附录-a关键文件清单与-url)

---

## 0. 仓库结构与版本事实

主仓库 `brunodev85/winlator` 本身几乎不含 App 源码，是一个"壳仓库"，真正的代码在三个 git 子模块里：

```
[submodule "app"]     → https://github.com/brunodev85/winlator-app   （Android App 全部 Java/C++ 源码）
[submodule "vortek"]  → https://github.com/brunodev85/vortek         （通用 Vulkan 包装层客户端）
[submodule "gladio"]  → https://github.com/brunodev85/gladio         （OpenGL ES 后端）
```

来源：<https://raw.githubusercontent.com/brunodev85/winlator/main/.gitmodules>

主仓库还承载：

- `installable_components/`：可在线下载的组件包（box64/dxvk/vkd3d/wined3d/turnip 的 `.tzst` + `index.txt`），运行时可替换 APK 内置版本；
- `input_controls/`：官方触屏按键配置（`.icp`）；
- `wine_addons/`：wine-mono / wine-gecko 安装包；
- `android_alsa/`：自研 ALSA→Android 音频桥（C 源码 + 构建脚本）；
- `glibc_patches/`：让 glibc 的 SysV SHM 在 Android 上可用的补丁（X Server MIT-SHM 扩展依赖它）。

版本事实（`winlator-app/app/build.gradle`）：

- `applicationId 'com.winlator'`，`versionName "11.2"`
- `minSdkVersion 26`（Android 8.0）、`targetSdkVersion 28`、`compileSdk 35`
- `AndroidManifest.xml` 中 `XServerDisplayActivity` 为 `launchMode="singleTask"`、强制横屏、支持 PiP

包结构总览（`app/src/main/java/com/winlator/`，共约 200 个 Java 文件）：

| 包 | 职责 |
|---|---|
| `xserver/` | **纯 Java 实现的 X11 服务器**（协议解析、窗口/像素图/光标/输入设备管理、扩展） |
| `xconnector/` | Unix domain socket 的 epoll 多路复用连接器（X 客户端接入层） |
| `xenvironment/` | 容器运行环境：RootFS、各后台组件（X Server、音频、渲染器、访客程序启动器） |
| `renderer/` | Android 端 GLES 合成器：把 X 窗口内容画到 GLSurfaceView |
| `widget/` | 自定义 View：XServerView、TouchpadView、InputControlsView 等 |
| `winhandler/` | 与容器内 Windows 助手进程 winhandler.exe 的 UDP 协议（鼠标/键盘/手柄/进程管理） |
| `container/` | Container/Shortcut/Drive 数据模型与 ContainerManager |
| `inputcontrols/` | 触屏虚拟按键系统（profile、外接手柄映射） |
| `box64/` | Box64 预设（preset）管理 |
| `core/` | 工具类：WineUtils、EnvVars、TarCompressorUtils、GeneralComponents 等 |
| `alsaserver/` / `sysvshm/` | ALSA 音频服务器 / SysV 共享内存服务器（Java 侧） |

---

## 1. 输入注入集成点（最重要）

### 1.1 显示与输入总体架构：纯 Java 实现的 X Server

Winlator **不使用任何真实 Xorg/Xvfb**，而是用 Java 完整实现了 X11 协议服务器（`com.winlator.xserver` 包，约 90 个类）。Wine 的 `winex11.drv` 作为普通 X 客户端，通过 rootfs 内的 Unix socket 连进来：

- socket 路径常量：`xconnector/UnixSocketConfig.java:11` → `XSERVER_PATH = "/tmp/.X11-unix/X0"`
- 接入组件：`xenvironment/components/XServerComponent.java`，内部用 `XConnectorEpoll`（epoll 多路复用）接受客户端，由 `XClientConnectionHandler` / `XClientRequestHandler` 处理 X 协议字节流
- 支持的扩展见 `xserver/XServer.java:198-210` 的 `setupExtensions()`：BigReq、**MIT-SHM**、**DRI3**、**Present**、Sync、Composite、GLX、GenericEvent、**XInputExtension (XI2)**

输入流向（触摸/鼠标 → 游戏）：

```
Android MotionEvent
  → TouchpadView / InputControlsView / onExternalMouseEvent        （Android View 层）
  → XServer.injectPointerMove / injectPointerButtonPress / ...     （X 服务器层，见 1.2）
  → Pointer 状态更新 + 触发 OnPointerMotionListener
  → InputDeviceManager.onPointerMove/onPointerButtonPress          （xserver/InputDeviceManager.java:146-238）
  → 按 X11 语义找"指针下的窗口"(pointWindow)、处理 grab、
    做 FullscreenTransformation 逆变换，构造 MotionNotify/ButtonPress 事件
  → 经 XConnectorEpoll 写入 socket → Wine winex11.drv → Windows 消息/光标
```

关键源码：

- `xserver/InputDeviceManager.java:217-238`（`onPointerMove`：把 root 坐标换算成窗口局部坐标后发 `MotionNotify`）
- `xserver/extensions/XInputExtension.java`（XI2 RawMotion/RawButton 事件，供用 Raw Input 的游戏）

### 1.2 绝对坐标注入的确切入点：XServer.inject* 系列方法

`xserver/XServer.java:148-196` 提供了现成的、线程安全（内部持 `Lockable.WINDOW_MANAGER + Lockable.INPUT_DEVICE` 双锁）的注入 API，**这就是我们 UDP 坐标要调用的方法**：

```java
// xserver/XServer.java
public void injectPointerMove(int x, int y) {              // L148：绝对坐标（X 屏幕坐标系）
    try (XLock lock = lock(Lockable.WINDOW_MANAGER, Lockable.INPUT_DEVICE)) {
        pointer.setPosition(x, y);
    }
}

public void injectPointerMoveDelta(int dx, int dy) {       // L154：相对移动 + 同时发 XI2 RawMotion
    try (XLock lock = lock(Lockable.WINDOW_MANAGER, Lockable.INPUT_DEVICE)) {
        pointer.setPosition(pointer.getX() + dx, pointer.getY() + dy);
        if (cursorLocker == null) pointer.clampPosition();
        XInputExtension xInputExtension = getExtension(XInputExtension.class);
        if (xInputExtension != null) xInputExtension.sendRawMotion(dx, dy);
    }
}

public void injectPointerButtonPress(Pointer.Button buttonCode)    // L164
public void injectPointerButtonRelease(Pointer.Button buttonCode)  // L173
public void injectKeyPress(XKeycode xKeycode) / injectKeyRelease() // L182/L192
```

URL：<https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xserver/XServer.java>

`Pointer`（`xserver/Pointer.java`）内部用 `short x/y` 存坐标，`setPosition()` 触发所有 `OnPointerMotionListener`（两个已注册监听者：`InputDeviceManager` 负责发 X 事件给 Wine，`GLRenderer` 负责重绘光标纹理）。

按钮枚举 `Pointer.Button`：`BUTTON_LEFT/MIDDLE/RIGHT/SCROLL_UP/SCROLL_DOWN/...`，`code()` 即 X11 按钮号（左键=1）。

**结论：绝对瞄准坐标注入 = `xServer.injectPointerMove(x, y)`；扳机 = `injectPointerButtonPress/Release(Pointer.Button.BUTTON_LEFT)`。无需改动 X Server 内部任何代码。**

注意：`injectPointerButtonPress/Release` 会同时通过 `XInputExtension.sendRawButtonState()` 发 XI2 RawButton 事件（`XServer.java:168-169`），而 `injectPointerMove` 只更新绝对位置、不发 RawMotion——用 Raw Input 读相对量的游戏需要另行用 `injectPointerMoveDelta` 喂相对增量（或走 1.5 的 Windows 层通路）。

### 1.3 坐标系换算在哪一层

三层坐标：

1. **Android View 像素**（GLSurfaceView 全屏大小，如 1920×1080 电视即 1:1）
2. **X 屏幕坐标**（`ScreenInfo`，由容器 `screenSize` 决定，默认 `1280x720`，见 `container/Container.java:21`）
3. **X 窗口局部坐标**（由 `InputDeviceManager` 在分发事件时换算）

View→X 的换算在 **`widget/TouchpadView.java:78-88` 的 `updateXform()`**：

```java
private void updateXform(int outerWidth, int outerHeight, int innerWidth, int innerHeight) {
    ViewTransformation viewTransformation = new ViewTransformation();
    viewTransformation.update(outerWidth, outerHeight, innerWidth, innerHeight);
    float invAspect = 1.0f / viewTransformation.aspect;
    if (!xServer.getRenderer().isFullscreen()) {
        XForm.makeTranslation(xform, -viewTransformation.viewOffsetX, -viewTransformation.viewOffsetY);
        XForm.scale(xform, invAspect, invAspect);
    }
    else XForm.makeScale(xform, invAspect, invAspect);
}
```

`renderer/ViewTransformation.java:14-25` 是经典的 letterbox 适配：`aspect = min(outerW/innerW, outerH/innerH)`，居中加黑边（`viewOffsetX/Y`）。所有触摸点经 `XForm.transformPoint(xform, x, y)` 变成 X 屏幕坐标后再调 `injectPointerMove`。

**对我们的意义**：如果容器 `screenSize` 固定设为 `1920x1080`（与光枪规范坐标一致），且电视 View 本身也是 1920×1080，则换算恒等（`aspect=1, offset=0`），UDP 坐标可直接 `injectPointerMove(gunX, gunY)`，零换算误差。若游戏内部分辨率不是 1080p，用 Shortcut 的 `forceFullscreen=1`（见 3.2）让 Winlator 自己缩放并保持指针逆变换一致。

### 1.4 三种触摸模式与外接鼠标路径对比

| 模式 | 入口 | 调用 | 坐标性质 |
|---|---|---|---|
| 触控板模式（默认） | `TouchpadView.onTouchEvent` → `handleFingerMove`（`TouchpadView.java:221-267`） | `injectPointerMoveDelta(dx,dy)`（带灵敏度+加速度） | 相对 |
| 直触模式（move_cursor_to_touchpoint） | 同上，`moveCursorToTouchpoint=true` 分支（`TouchpadView.java:252-255`） | `injectPointerMove(finger.x, finger.y)` | **绝对** |
| 相对鼠标模式（游戏内鼠标视角） | `XServer.setRelativeMouseMovement(true)`，见 `XServerDisplayActivity.java:656-676` | `WinHandler.mouseEvent(MouseEventFlags.MOVE, dx, dy, 0)` 走 UDP→winhandler.exe→Windows `mouse_event()` | 相对（Windows 层） |
| 外接物理鼠标（未捕获） | `XServerDisplayActivity.dispatchGenericMotionEvent` → `TouchpadView.onExternalMouseEvent`（`TouchpadView.java:358-401`） | HOVER_MOVE → `injectPointerMove(绝对)`；按键 → `injectPointerButtonPress/Release`；滚轮 → SCROLL 按钮对 | 绝对 |
| 外接鼠标指针捕获（requestPointerCapture） | `TouchpadView.onCapturedPointer`（`TouchpadView.java:419-435`） | `injectPointerMoveDelta(dx,dy)` | 相对 |

外接鼠标按键还通过 `InputDeviceManager.onPointerButtonPress`（`InputDeviceManager.java:146-152`）在"相对鼠标模式"下改走 `WinHandler.mouseEvent` 通路。

### 1.5 第二条输入通路：WinHandler → winhandler.exe（Windows 层注入）

Winlator 在容器里预置了一个 Windows 助手程序 **`C:\windows\winhandler.exe`**（随游戏启动命令一起拉起，见 `XServerDisplayActivity.java:998`），Java 侧 `winhandler/WinHandler.java` 通过 **localhost UDP** 与它通信（Java 绑定 7947 接收，winhandler.exe 绑定 7946，`WinHandler.java:28-29`）。

协议见 `winhandler/RequestCodes.java`：`EXEC / KILL_PROCESS / MOUSE_EVENT / KEYBOARD_EVENT / GET_GAMEPAD / SET_GAMEPAD_STATE / BRING_TO_FRONT / CURSOR_POS_FEEDBACK / ...`

鼠标注入（`WinHandler.java:162-175`）：

```java
public void mouseEvent(int flags, int dx, int dy, int wheelDelta) {
    if (!initReceived) return;
    addAction(() -> {
        sendData.rewind();
        sendData.put(RequestCodes.MOUSE_EVENT);
        sendData.putInt(10);
        sendData.putInt(flags);
        sendData.putShort((short)dx);
        sendData.putShort((short)dy);
        sendData.putShort((short)wheelDelta);
        ...
    });
}
```

`winhandler/MouseEventFlags.java` 直接复用 Win32 `MOUSEEVENTF_*` 常量：`MOVE=0x0001, LEFTDOWN=0x0002, ..., VIRTUALDESK=0x4000, ABSOLUTE=0x8000`。**`ABSOLUTE` 常量已定义但当前 Java 源码中无人使用**——这是留给我们的现成后门：`winHandler.mouseEvent(MOVE|ABSOLUTE|VIRTUALDESK, x*65535/1919, y*65535/1079, 0)` 即可在 Windows 层做绝对定位（等价 TeknoParrot 里 `mouse_event` 的玩法），绕开 X 层，对某些只认 Windows 光标/Raw Input 的光枪游戏更可靠。

注意该通路要等 winhandler.exe 发来 `INIT` 才生效（`initReceived`，`WinHandler.java:315-321`），且 `CURSOR_POS_FEEDBACK`（`WinHandler.java:352-359`）会把 Windows 光标位置回写 X 指针，两条通路的指针状态会同步。

另外 `winhandler/GamepadHandler.java` 提供**虚拟手柄**通路（`SET_GAMEPAD_STATE`，支持 DInput/XInput 映射），若某款光枪游戏走 XInput 也可考虑把手枪映射成虚拟 X360 手柄。

### 1.6 UDP 光枪接收线程的接入方案（类/方法级建议）

推荐方案（改动最小、与现有架构同构）：

1. **新建 `com.winlator.lightgun.LightGunInputManager`**（我们自己的类，与 `WinHandler` 同风格：单线程 `DatagramSocket` + `ArrayDeque<Runnable>` 或直接在收包线程内调用注入 API——`inject*` 方法自带锁，线程安全）。
2. **在 `XServerDisplayActivity.onCreate()` 中实例化**：紧挨 `xServer = new XServer(this, screenInfo); xServer.setWinHandler(winHandler);`（`XServerDisplayActivity.java:236-237`）之后，例如 `lightGunManager = new LightGunInputManager(xServer, winHandler);`，在 `setupXEnvironment()` 完成（`winHandler.start()` 之后，`XServerDisplayActivity.java:570-572`）启动收包线程，`onDestroy()`（L343-348）里随 `winHandler.stop()` 一并停止。
3. **收包循环内**：
   - 瞄准包（120Hz）：`xServer.injectPointerMove(gunX, gunY)`——坐标已是 1920×1080 规范坐标，容器 screenSize 固定 1920x1080 时直通。
   - 扳机按下/抬起：`xServer.injectPointerButtonPress/Release(Pointer.Button.BUTTON_LEFT)`。
   - 备选/双发：同时 `winHandler.mouseEvent(MouseEventFlags.MOVE|MouseEventFlags.ABSOLUTE|MouseEventFlags.VIRTUALDESK, x65535, y65535, 0)` 覆盖 Raw Input 型游戏（按游戏适配 JSON 决定走哪条或双发）。
   - 120Hz 合帧：直接每包都 inject 即可——`Pointer.setPosition` 只是改状态+通知，`GLRenderer.onPointerMove` 触发 `requestRender()`（`GLRenderer.java:191-193`），`RENDERMODE_WHEN_DIRTY` 下不会超额渲染。
4. **必须关掉的干扰项**：
   - 确保 `xServer.isRelativeMouseMovement() == false`（默认 false；不要在 InputControls 对话框里打开）。
   - `TouchpadView` 在电视上无触摸源，保留无害；若怕误触可 `touchpadView.setEnabled(false)`。
   - `capturePointerOnExternalMouse`（`XServerDisplayActivity.java:301-305`）在外接鼠标时会 `requestPointerCapture()`，电视无人操作场景可保留默认。
   - 隐藏系统光标显示：`renderer.setCursorVisible(false)`（`setupUI()` 里 shortcut 场景已经是先 false 后由窗口映射置 true，见 L246-251；光枪游戏通常自带准星，可在适配 JSON 加 `hideCursor` 项强制 false）。
5. **白边框叠加层**：作为 `FrameLayout rootView (R.id.FLXServerDisplay)` 上最后一个 `addView` 的 Android View（与 `frameRating`、`magnifierView` 同层，见 `setupUI()` L615-619），天然位于游戏画面之上，无需进 GL 层。

**结论：接入点是 `XServerDisplayActivity`（生命周期托管）+ `XServer.injectPointerMove/injectPointerButtonPress`（注入 API），不需要改动 xserver 包内部；新增一个 LightGunInputManager 类 + 两处挂钩（onCreate 创建、onDestroy 停止）即可。**

---

## 2. 游戏启动与内容管理集成点

### 2.1 Container 数据模型与目录结构

`container/Container.java`：一个容器 = rootfs `home/` 下的一个目录 `xuser-<id>/`（`RootFS.USER = "xuser"`），内含：

```
/data/data/com.winlator/files/rootfs/          ← RootFS.find()（xenvironment/RootFS.java:26-31）
├── home/
│   ├── xuser -> xuser-1                        ← activateContainer() 建的活动符号链接（ContainerManager.java:67-72）
│   └── xuser-1/                                ← 容器根（Container.getRootDir()）
│       ├── .container                          ← JSON 配置（Container.saveData(), Container.java:267-296）
│       ├── .wine/                              ← Wine 前缀（drive_c/、system.reg、user.reg…）
│       └── .local/share/icons/…                ← 快捷方式图标
├── opt/wine/                                   ← 主 Wine 发行（rootfs.tzst 自带）
├── opt/installed-wine/                         ← 后装的其它 Wine 版本
├── tmp/.X11-unix/X0、tmp/.vortek/V0、…         ← 各组件 socket
└── .winlator/.rfs_version                      ← rootfs 版本（当前 LATEST_VERSION=23，RootFSInstaller.java:29）
```

`.container` JSON 字段（`Container.saveData()`）：`name, screenSize, envVars, cpuList, graphicsDriver, dxwrapper(+Config), audioDriver(+Config), wincomponents, drives, hudMode, startupSelection, box64Preset, desktopTheme, wineVersion, extraData`。关键默认值：

```java
// container/Container.java:20-29
DEFAULT_ENV_VARS   = "ZINK_DESCRIPTORS=lazy ZINK_DEBUG=compact MESA_SHADER_CACHE_DISABLE=false
                      MESA_SHADER_CACHE_MAX_SIZE=512MB mesa_glthread=true WINEESYNC=1 TU_DEBUG=noconform"
DEFAULT_SCREEN_SIZE = "1280x720"
DEFAULT_AUDIO_DRIVER = ALSA
DEFAULT_DXWRAPPER    = DXVK
```

**Shortcut（`container/Shortcut.java`）**：容器桌面目录（`.wine/drive_c/users/xuser/Desktop/`）下的 `.desktop` 文件，`[Desktop Entry]` 段是标准 desktop 条目，`[Extra Data]` 段存每游戏覆盖项：`screenSize, envVars, execArgs, graphicsDriver, dxwrapper, wincomponents, box64Preset, controlsProfile, forceFullscreen, dinputMapperType` 等（`XServerDisplayActivity.java:207-221` 逐一读取）。**Shortcut 就是我们"每款游戏一个适配 JSON"的天然落点**。

### 2.2 启动一个 exe 的完整调用链

```
ShortcutsFragment.onItemClick                                   （ShortcutsFragment.java:228-231）
  Intent(activity, XServerDisplayActivity.class)
    .putExtra("container_id", id) .putExtra("shortcut_path", desktop文件路径)
        │
XServerDisplayActivity.onCreate()                               （XServerDisplayActivity.java:141）
  ContainerManager.getContainerById → activateContainer(建 xuser 符号链接)
  读 container/shortcut 配置 → resolveScreenInfo()（"native" 时取设备分辨率取偶）
  inputControlsManager = new InputControlsManager(this)
  xServer = new XServer(this, screenInfo); xServer.setWinHandler(winHandler)
  注册 WindowManager.OnWindowModificationListener（首个可渲染窗口出现时关加载框）
  setupUI()：XServerView(GLSurfaceView+GLRenderer) / TouchpadView / InputControlsView 加入 FLXServerDisplay
  后台线程：
    setupWineSystemFiles()     ← 补丁、wincomponents、主题、开始菜单、dosdevices 符号链接
    extractGraphicsDriverFiles() ← 解压 turnip/vortek/zink/virgl/gladio，写 MESA/GALLIUM 环境变量
    changeWineAudioDriver()    ← 改 user.reg 的 Audio 驱动
    setupXEnvironment()        ← 见下
        │
setupXEnvironment()                                             （XServerDisplayActivity.java:495-580）
  envVars: WINEPREFIX / WINEDEBUG / MESA_NO_ERROR / WINEESYNC=1(缺省补) + container.envVars + shortcut.envVars
  guestExecutable = "wine explorer /desktop=nogui,1920x1080 C:\windows\winhandler.exe /dir Z:\... \"game.exe\" args"
                    ↑ getWineStartCommand() L955-999；桌面模式为 /desktop=shell
  XEnvironment 组件依次挂载：
    SysVSharedMemoryComponent  (tmp/.sysvshm/SM0，MIT-SHM 用)
    XServerComponent           (tmp/.X11-unix/X0)
    NetworkInfoUpdateComponent
    ALSAServerComponent 或 PulseAudioComponent
    VortekRendererComponent(Vulkan) / VirGLRendererComponent(GL)   ← 按 graphicsDriver 二元组
    GuestProgramLauncherComponent                                  ← 最后启动访客程序
  environment.startEnvironmentComponents(); winHandler.start();
        │
GuestProgramLauncherComponent.start()                           （GuestProgramLauncherComponent.java:34-116）
  extractBox64File()（按需解压 box64-<ver>.tzst）+ copyDefaultBox64RCFile()
  execGuestProgram()：
    env: HOME/USER/TMPDIR/DISPLAY=:0/PATH=<rootfs>/opt/wine/bin:.../LD_LIBRARY_PATH/BOX64_*
    command = "<rootfs>/usr/local/bin/box64 wine explorer /desktop=... winhandler.exe ..."
    ProcessHelper.exec(command, envVars, rootDir, terminationCallback)
        │
box64 → wine → winex11.drv 连 /tmp/.X11-unix/X0 → 创建游戏窗口
winhandler.exe 连 UDP 7947 发 INIT → WinHandler 通路就绪
X 窗口 MapNotify → Activity 监听器关加载框、显示光标 → 游戏画面经 3.1 链路上屏
```

进程退出时 `terminationCallback → exit()`（`XServerDisplayActivity.java:559,430-443`）。

### 2.3 游戏打包/导入方案：做成"选游戏即玩"

Winlator 的容器创建入口：`ContainerManager.createContainerAsync(JSONObject data, Callback)`（`ContainerManager.java:74-125`）——创建 `xuser-<id>` 目录并从 assets 解压 **`container_pattern.tzst`**（预制 Wine 前缀骨架），再从 `common_dlls.json` 拷贝 system32/syswow64 公共 DLL。整个过程纯本地、无 UI 依赖，可直接在代码里批量调用。

推荐产品化流程（去通用设置界面）：

1. **预制游戏包**：每款游戏 = `drive_c` 下的目录（如 `ParrotDroid/Games/<GameId>/`）+ 一份适配 JSON（映射到 Shortcut 的 `[Extra Data]` 字段 + 我们自己的键如 `inputMode=lightgunAbsolute`）。发布形态可与 `container_pattern.tzst` 相同的 **tar.zst**（`core/TarCompressorUtils.java` 支持 ZSTD 解压，`TarCompressorUtils.Type.ZSTD`）。
2. **首启初始化**：`MainActivity` 已自带 rootfs 安装流程（`RootFSInstaller.installIfNeeded()`，`MainActivity.java:82,102`；`xenvironment/RootFSInstaller.java:47-83`，从 assets 解 `rootfs.tzst` 并显示进度）。在其后追加我们自己的 `GameLibraryInstaller`：对每款游戏调 `createContainerAsync`（或直接复制预制容器目录再写 `.container`），把游戏目录解到容器 `drive_c`，写桌面 `.desktop` + `[Extra Data]`。
   - 体积优化：街机游戏普遍要求相近（DX9/11 + DXVK + Turnip），**可以全库共用一个容器**、每游戏一个 Shortcut（Shortcut 已支持 per-game 覆盖 graphicsDriver/dxwrapper/envVars/screenSize），只有个别不兼容游戏才开第二容器。
3. **启动即玩**：自己的游戏列表 Activity（替代 `ShortcutsFragment`/`ContainersFragment`）发 `XServerDisplayActivity` Intent（`container_id` + `shortcut_path`），用户全程看不到 Winlator 的任何设置 UI。`Shortcut` 的 `forceFullscreen=1` 保证低分辨率游戏拉伸上屏。
4. **无快捷方式直启**：也支持 Intent 带 `exec_path` 直接跑 exe（`XServerDisplayActivity.java:224,970-979`）。

### 2.4 Box64/Wine/Turnip/DXVK 的组件形态与替换方法

组件以 **zstd 压缩 tar（`.tzst`）** 打包在 APK assets 里，运行时按需解到 rootfs（`XServerDisplayActivity.extractGraphicsDriverFiles/extractDXWrapperFiles`，L726-890）：

| 组件 | APK assets 路径 | 解压目标 | 默认版本（`core/DefaultVersion.java`） |
|---|---|---|---|
| RootFS（含 Wine 主版本 `/opt/wine`、box64 启动器、winhandler.exe） | `assets/rootfs.tzst` | `files/rootfs/` | rfs_version=23 |
| 容器骨架 | `assets/container_pattern.tzst` | `home/xuser-<id>/` | — |
| Box64 | `assets/box64/box64-0.4.4.tzst` | rootfs `/usr/local/bin` | 0.4.4 |
| Turnip（Adreno Vulkan） | `assets/graphics_driver/turnip-26.2.0.tzst` | `usr/lib/libvulkan_freedreno.so` + ICD | 26.2.0 |
| Vortek（通用 Vulkan 包装） | `assets/graphics_driver/vortek-2.1.tzst` | `usr/lib/libvulkan_vortek.so` | 2.1 |
| Zink / VirGL / Gladio（GL 后端） | `assets/graphics_driver/{zink-22.2.5,virgl-23.1.9,gladio-1.1}.tzst` | rootfs `/usr/lib` | — |
| DXVK（D3D9/10/11→Vulkan） | `assets/dxwrapper/dxvk-{1.10.3,2.4.1}.tzst` | 容器 `drive_c/windows` | 2.4.1（Vortek 且 VK<1.3 时 1.10.3） |
| D8VK / D7VK（D3D8/DDraw→Vulkan） | `assets/dxwrapper/{d8vk-1.0,d7vk-1.11}.tzst` | 同上 | — |
| VKD3D（D3D12） | `assets/dxwrapper/vkd3d-2.14.1.tzst` | 同上 | 2.14.1 |
| CNC-DDraw | `assets/dxwrapper/cnc-ddraw-6.6/` | `ProgramData\cnc-ddraw` | 6.6 |
| WineD3D（多版本） | assets 内置主版本；其余在线下载 | 同上 | 跟随 Wine |
| wincomponents（direct3d/directsound/xaudio/vcrun…） | `assets/wincomponents/*.tzst` | 容器 `windows/` + 注册表覆盖 | `Container.DEFAULT_WINCOMPONENTS` |
| PulseAudio | `assets/pulseaudio.tzst` | `files/pulseaudio` | — |

**替换/升级某个组件的两种机制**：

1. **换 APK 内置包**：直接替换 assets 里的 `.tzst` 并 bump `DefaultVersion` 常量。解压缓存键是 `"<driver>-<version>"` 串（`XServerDisplayActivity.java:729-751`），版本号变了会自动重解。
2. **在线组件（推荐用于运维升级）**：主仓库 `installable_components/<type>/index.txt` 列出可下载版本，`core/GeneralComponents.java:23` 的 `INSTALLABLE_COMPONENTS_URL = "https://raw.githubusercontent.com/brunodev85/winlator/main/installable_components/%s"`。fork 后可把该 URL 指到自己的 CDN，dxvk/vkd3d/wined3d/box64/turnip 都支持运行时下载切换（`GeneralComponents.Type` 枚举 + `getInstallMode()`）。下载落盘 `files/components/<type>/`（`getComponentDir`），优先级高于 assets。

### 2.5 环境变量与启动参数注入点汇总

| 注入点 | 位置 | 能干什么 |
|---|---|---|
| Container 级 envVars | `Container.setEnvVars()`（容器 `.container` JSON） | 每容器固定变量（默认含 `WINEESYNC=1`、`mesa_glthread=true`） |
| Shortcut 级 envVars | `.desktop` `[Extra Data] envVars=...`（`XServerDisplayActivity.java:518`） | **每游戏覆盖（DXVK_HUD、MESA_EXTENSION_MAX_YEAR 等），我们的适配 JSON 落点** |
| Activity 硬编码 | `setupXEnvironment()` L497-519 | `WINEPREFIX / WINEDEBUG / MESA_NO_ERROR / vblank_mode=0` |
| 图形驱动配置 | `TurnipConfigDialog.setEnvVars`（`TU_DEBUG`、`MESA_VK_WSI_PRESENT_MODE`、`TU_OVERRIDE_HEAP_SIZE`）、`VirGLConfigDialog`、`VortekConfigDialog` | Turnip present mode（mailbox/fifo）、显存上限 |
| DX wrapper 配置 | `DXVKConfigDialog.setEnvVars`（`DXVK_ASYNC=1`、`DXVK_STATE_CACHE_PATH`、`DXVK_CONFIG_FILE`）、`VKD3DConfigDialog`、`WineD3DConfigDialog` | DXVK async、配置文件路径 |
| Box64 | `GuestProgramLauncherComponent.addBox64EnvVars()` L136-174 + `Box64PresetManager.getEnvVars()` | `BOX64_DYNAREC=1`、`BOX64_DYNACACHE`、预设（Compatibility/Performance/Stability）、`box64/default.box64rc` |
| exec 参数 | Shortcut `[Extra Data] execArgs`、`overrideEnvVars.EXTRA_EXEC_ARGS`（`XServerDisplayActivity.java:994-997`） | 给游戏 exe 传命令行（如 `-force-gfx-direct`） |
| API 覆盖入口 | `XServerDisplayActivity.getOverrideEnvVars()`（L1021-1024） | fork 代码里启动前直接塞变量 |

---

## 3. 图形栈与显示集成点

### 3.1 渲染链路全景

```
游戏 D3D9/11 ──DXVK(d3d9/d3d11.dll→Vulkan)──┐
游戏 D3D8    ──D8VK──────────────────────────┤
游戏 DDraw   ──D7VK / CNC-DDraw──────────────┤
游戏 D3D12   ──VKD3D─────────────────────────┤ Vulkan 调用
游戏 OpenGL  ──WineD3D / 原生 GL→Zink────────┘
        │
Vulkan 驱动二选一（container graphicsDriver[0]）：
  Turnip（Adreno 原生 Mesa Vulkan，libvulkan_freedreno.so）
  Vortek（通用包装层：guest libvulkan_vortek.so → unix socket /tmp/.vortek/V0
          → Java/native 侧 VortekRendererComponent → 宿主机 Vulkan 驱动，Mali 也能用）
        │
渲染结果写入与 X 窗口 Drawable 关联的 AHardwareBuffer（零拷贝）
  renderer/GPUImage.java：native createHardwareBuffer()/createImageKHR()（EGLImage→GLES 纹理）
  2D/软渲染窗口则走 MIT-SHM（SysVSharedMemoryComponent + glibc_patches 的 shmat 补丁）
        │
GLRenderer（renderer/GLRenderer.java）在 GLSurfaceView 线程上：
  onUpdateWindowContent → xServerView.requestRender()（RENDERMODE_WHEN_DIRTY）
  drawFrame()：按 Z-order 逐个 renderWindowDrawable() 画窗口纹理 + renderCursor() 画光标
        │
SurfaceFlinger 上屏
```

关键事实：

- `XServerView`（`widget/XServerView.java`）是普通 `GLSurfaceView`，`RENDERMODE_WHEN_DIRTY`，`setEGLConfigChooser(8,8,8,8,0,0)`，**`EGL14.eglSwapInterval(display, 0)`（`GLRenderer.java:97`）——关 vsync 等待，出帧即换**，把同步交给下层。
- 渲染被动触发：X 窗口内容更新（`WindowManager.OnWindowModificationListener.onUpdateWindowContent`）→ `requestRender()`（`GLRenderer.java:172-174`），没有自研帧调度器。
- X 扩展里有 **Present 扩展**（`xserver/extensions/PresentExtension.java`）与 DRI3，Wine 的 flip 模型呈现可直通，减少一次全屏拷贝。
- 帧率 HUD：`widget/FrameRating.java`（容器 `hudMode` 控制），`X11_WND_GPU_INFO=1` 时显示 GPU 信息（`XServerDisplayActivity.java:511`）。

### 3.2 分辨率、全屏与缩放模式

- **容器 screenSize** 决定 X 屏幕（`ScreenInfo`，`xserver/ScreenInfo.java`）与 Wine 虚拟桌面大小（`/desktop=nogui,<宽>x<高>`，`XServerDisplayActivity.java:514`）。可选固定值或 `"native"`（取设备分辨率并向下取偶，L1042-1048）。
- **View 适配**：`ViewTransformation.update()` letterbox 居中（3.3 详述）；导航菜单 `menu_item_toggle_fullscreen` → `GLRenderer.toggleFullscreen()` 改为拉伸全屏（`GLRenderer.java:124-127`）。
- **Force Fullscreen**（Shortcut `forceFullscreen=1`）：`GLRenderer.collectRenderableWindows()`（L300-320）对"接近全屏但未全屏"的单窗口做 `FullscreenTransformation`（`renderer/FullscreenTransformation.java`）——保持纵横比拉伸到 X 屏幕并居中；**指针坐标由 `InputDeviceManager` 用同一个 `FullscreenTransformation.transformPointerCoords()` 逆变换**（`InputDeviceManager.java:165-169` 等三处），所以低分辨率光枪游戏拉伸后枪口坐标依然对齐。这正是街机老游戏（800×600/1024×768）需要开关。
- 屏幕特效（CRT/FXAA/调色）：`renderer/EffectComposer` + `renderer/effects/*`，`contentdialog/ScreenEffectDialog` 配置——可裁（见 §5）。

### 3.3 白边框叠加层（Android View）与游戏画面对齐要点

我们的白边框定位叠加是 Android View，挂在 `R.id.FLXServerDisplay` 顶层即可（与 `FrameRating` 同法，`XServerDisplayActivity.java:615-619`）。注意：

1. **Letterbox 偏移**：GLRenderer 非 fullscreen 时，X 屏幕被居中缩放显示，偏移量 = `viewTransformation.viewOffsetX/Y`、缩放 = `aspect`（`ViewTransformation.java`）。白边框若要给光枪摄像头"贴住游戏画面四边"，必须与 GL 视口对齐：容器 screenSize 固定 1920x1080 且电视面板 1080p 时 `offset=0, aspect=1`，天然对齐；4K 电视上 View 为 3840×2160，`aspect=2`，边框坐标 ×2 即可（仍无偏移）。最稳妥做法是叠加层直接读 `xServerView.getRenderer().viewTransformation` 的四个字段做变换。
2. **沉浸式**：`AppUtils.hideSystemUI(this)` + `keepScreenOn`（`XServerDisplayActivity.java:144-145`）已处理；`drawerLayout.setOnApplyWindowInsetsListener` 把系统 inset 清零（L155），无刘海安全区问题。
3. **显示延迟**：叠加层由 SurfaceFlinger 与 GLSurfaceView 合成，与游戏画面同帧上屏（硬件叠加层或 GPU 合成），无额外帧延迟；不要用 `View.invalidate()` 高频自绘动画，边框静态即可。
4. **光标隐藏**：光枪游戏建议 `renderer.setCursorVisible(false)`（光标是 GLRenderer 手画的纹理，`GLRenderer.renderCursor()`），避免系统 X 光标与游戏准星双影。
5. PiP/画中画（`menu_item_pip_mode`）会改变 Surface 比例，电视形态可裁掉。

### 3.4 输入/显示延迟链路上的已知优化开关

| 开关 | 位置 | 作用 |
|---|---|---|
| `eglSwapInterval(0)` | `GLRenderer.java:97` | Android 合成端不等待 vsync |
| `RENDERMODE_WHEN_DIRTY` + 内容更新即 `requestRender()` | `XServerView.java:24`、`GLRenderer.java:172-174` | 按需渲染，输入事件（指针移动）直接触发重绘（`onPointerMove→requestRender`，L191-193） |
| `vblank_mode=0` | `XServerDisplayActivity.java:727` | Mesa GL 关垂直同步 |
| `MESA_VK_WSI_PRESENT_MODE` | `TurnipConfigDialog.setEnvVars`（presentMode 配置） | Vulkan 呈现模式（mailbox 可降排队延迟） |
| `MESA_VK_WSI_NATIVE_MEM_IMPORTED=1` | 同上（directRendering） | Turnip 直渲染零拷贝 |
| `mesa_glthread=true` | `Container.DEFAULT_ENV_VARS` | GL 命令线程化 |
| `ZINK_CONTEXT_THREADED=1` | `extractGraphicsDriverFiles` L768 | Zink 线程化 |
| `WINEESYNC=1` | 默认补（L519）+ `DEFAULT_ENV_VARS` | Wine 同步原语走 eventfd，降 CPU/延迟；**fsync/futex2 在 Android 内核不可用，Winlator 未集成** |
| `BOX64_DYNAREC=1`、`BOX64_DYNACACHE`、Preset | `GuestProgramLauncherComponent.addBox64EnvVars` | 动态重编译性能 |
| `DXVK_ASYNC=1` | DXVK 版本含 "async" 时（`DXVKConfigDialog`） | 异步编译着色器，减卡顿 |
| 指针 120Hz 注入 | 直接调 `injectPointerMove` | 无节流；X 事件直通 socket，事件即消息 |

延迟大头在 **Box64 翻译 + DXVK/Turnip 渲染**，输入通路（UDP→inject→X socket→Wine 消息）本身是亚毫秒级，无需优化。

---

## 4. 许可与边界

### 4.1 许可证事实（不是 MIT）

经 GitHub License API 逐一核实（2025 年）：

| 仓库 | 许可证 |
|---|---|
| `brunodev85/winlator`（壳仓库） | **LGPL-2.1** |
| `brunodev85/winlator-app`（App 源码） | **LGPL-2.1** |
| `brunodev85/vortek` | LGPL-2.1 |
| `brunodev85/gladio` | LGPL-2.1 |

（API 返回 `"key": "lgpl-2.1"`；LICENSE 文件正文为 GNU Lesser GPL v2.1。）

**fork 商用注意事项**：

- LGPL-2.1 允许商用分发，但**对 Winlator 源码本身的任何修改（包括 winlator-app 里的 Java 代码）必须以相同许可公开源码**（提供对应源码或书面获取承诺）。我们的 fork 仓库保持公开即可合规，无 copyleft 传染到"仅通过 IPC/Intent 与 Winlator 交互"的独立 App。
- 自有代码隔离建议：白边框叠加、UDP 光枪协议、游戏库管理若写成**独立模块/独立进程**（通过接口调用），可闭源；直接改在 `com.winlator.*` 包里的代码（如 LightGunInputManager 挂在 XServerDisplayActivity）属于修改作品，按 LGPL 公开即可——对我们无实质损失。
- 需在 About/文档中保留原版权与许可声明，标明"基于 Winlator (LGPL-2.1)"。

### 4.2 内置组件许可一览

| 组件 | 许可 | 备注 |
|---|---|---|
| Wine | LGPL-2.1+ | 动态链接使用无传染；改动需公开 |
| Box86/Box64 | MIT | 商用友好 |
| Mesa（Turnip/Zink/VirGL） | MIT | 商用友好 |
| DXVK / D8VK / D7VK | zlib | 商用友好 |
| VKD3D | LGPL-2.1 | 随 Wine 项目 |
| CNC-DDraw | 见上游（FunkyFr3sh/cnc-ddraw，含第三方 shader） | 商用前建议复核 |
| wine-mono / wine-gecko | LGPL/MPL 混合 | 可选组件，可不装 |
| glibc patches（Termux Pacman） | LGPL-2.1 | |
| Vortek / Gladio | LGPL-2.1 | brunodev85 自研 |

街机光枪游戏清单里 DX9/11 必需的是 Wine + Box64 + Mesa/Turnip + DXVK，全部为 MIT/zlib/LGPL 组合，无 GPL 强传染组件。

### 4.3 已知局限

1. **Mali GPU**：Turnip 只支持 Adreno。非 Adreno 设备走 **Vortek**——一个架在宿主 Vulkan 驱动上的用户态包装层（`vortek` 子模块 README："compatibility layer on top of the Vulkan host driver…format emulation, SPIR-V inspection, texture decoding"），Java 侧 `VortekRendererComponent` 经 `/tmp/.vortek/V0` 与 guest 的 `libvulkan_vortek.so` 通信，可配 `vkMaxVersion`（封顶 1.3）、`maxDeviceMemory`、Adrenotools 驱动。Vortek 即社区所称的 **"Universal"模式**（Winlator 10 起成为默认，`GraphicsDrivers.DEFAULT_VULKAN_DRIVER = VORTEK`，`getDefaultDriver()` 在检测到 Adreno 时才回 Turnip）。Mali 上性能与兼容性（尤其 DXVK 2.x 需要 VK 1.3）明显弱于 Adreno+Turnip——**产品选型应锁定骁龙/Adreno 设备**。
2. **Android 版本**：minSdk 26（Android 8.0）；SysV SHM 靠 glibc patch 模拟，非内核原生。
3. **仅 ARM64 主机**：x86/x64 全靠 Box64（32 位走 Wine WoW64 + Box86 系），无原生 x86 主机支持；性能约为原生的 50-80%，DX9 老游戏余量充足。
4. **无 fsync**（内核缺 futex2）；esync 默认开。
5. **Linux ELF 游戏**：Winlator 容器内就是 glibc Linux rootfs + Box64，理论上可直接 `box64 ./game.x86_64`（改 `guestExecutable` 即可，`GuestProgramLauncherComponent.setGuestExecutable`），但没有 Wine 的 DLL 层，游戏需自备全部依赖——可行，工作量在适配层。
6. winhandler.exe、Wine 构建（`/opt/wine`）、rootfs 镜像本身为二进制分发，源码需自行从上游构建（Wine 构建脚本不在仓库内）。

---

## 5. 裁剪建议与工作量分级

产品定位：**固定游戏列表 + 光枪输入 + 电视形态**。下表按"删/留/改"分类：

**可直接删除（约占 UI 代码 60%，纯减负）**

| 模块 | 路径 | 理由 |
|---|---|---|
| 触屏虚拟按键系统 | `inputcontrols/` 全部、`widget/InputControlsView.java`、`ControlsEditorActivity`、`ExternalControllerBindingsActivity`、`InputControlsFragment`、assets `inputcontrols/`、主仓库 `input_controls/*.icp` | 电视无光枪外输入设备；`XServerDisplayActivity` 中 `inputControlsView` 相关代码一并摘除 |
| 触屏手势 | `widget/TouchpadView.java`、`MagnifierView` | 无触屏；保留 `onExternalMouseEvent` 逻辑可并入调试通道 |
| 容器/设置 UI | `ContainersFragment`、`ContainerDetailFragment`、`ContainerFileManagerFragment`、`BaseFileManagerFragment`、`SettingsFragment`、`contentdialog/` 大部分（DXVK/Turnip/VKD3D/Vortek/WineD3D/AudioDriver/ScreenEffect/Box64EditPreset 等配置对话框） | 配置全部固化进游戏适配 JSON |
| 屏幕特效 | `renderer/effects/`（CRT/FXAA/Color）、`EffectComposer`、`ScreenEffectDialog` | 光枪画面要 1:1 无后处理（可留开关调试） |
| 通用小部件 | `widget/` 下 ColorPickerView、SimplePianoKeyboard、RangeScroller、SeekBar 等 | 仅被上述 UI 引用 |
| 在线组件下载 UI | `GeneralComponents` 的下载对话框部分（保留 extract 逻辑） | 组件固化进 APK |

**必须保留（核心引擎，基本不动）**

- `xserver/` 全部（X 协议服务器）
- `xconnector/`、`xenvironment/`（含 RootFSInstaller、各 Component）
- `renderer/GLRenderer`、`GPUImage`、`Texture`、`ViewTransformation`、`FullscreenTransformation`、`material/*`
- `winhandler/`（WinHandler/GamepadHandler/RequestCodes/MouseEventFlags）
- `container/`（Container/ContainerManager/Shortcut/Drive/GraphicsDrivers/DXWrappers）
- `core/`（WineUtils、WineInfo、TarCompressorUtils、EnvVars、ProcessHelper、GPUHelper、FileUtils 等）
- `box64/`（Box64PresetManager，配置固化后只留读取）
- `alsaserver/`、`sysvshm/`、`services/ForegroundService`
- assets 全部 `.tzst`（rootfs、container_pattern、组件包）

**需要改写（我们的集成点）**

| 模块 | 改动 | 工作量 |
|---|---|---|
| `XServerDisplayActivity` | 摘除 inputControls/magnifier/抽屉菜单；挂 LightGunInputManager 与白边框 View；生命周期托管 | 中（~300 行改动） |
| 新增 `lightgun/LightGunInputManager` | UDP 收包 → `XServer.injectPointerMove/ButtonPress`（+可选 WinHandler ABSOLUTE 双发） | 小（~200 行新增） |
| 新增游戏库 Activity + 安装器 | 替代 MainActivity 的 Fragment 体系；首启批量建容器/解游戏包 | 中（~600 行新增） |
| Shortcut 适配 JSON 生成器 | 把每游戏 JSON 转成 `.desktop [Extra Data]` | 小 |
| 组件固化 | 删 `GeneralComponents` 下载路径，版本钉死在 `DefaultVersion` | 小 |
| Linux ELF 游戏支持 | `guestExecutable` 直起 box64 + 依赖打包 | 中（每游戏适配） |

**工作量分级总估**（1 名熟悉 Android 的工程师）：

- S 级（1-2 周）：输入注入接入 + 叠加层对齐 + Shortcut 直启链路打通（用现成 Winlator UI 手工建容器验证游戏）。
- M 级（3-5 周）：UI 裁剪 + 自有游戏库界面 + 游戏包安装器 + 适配 JSON 体系。
- L 级（持续）：每游戏兼容性调优（Box64 preset / DXVK 版本 / forceFullscreen / 双发输入策略）、Vortek/Turnip 参数固化、Linux ELF 游戏适配。

---

## 附录 A：关键文件清单与 URL

源码根：<https://github.com/brunodev85/winlator-app/tree/main/app/src/main/java/com/winlator>

| 主题 | 文件 | 行号锚点 |
|---|---|---|
| 注入 API | [xserver/XServer.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xserver/XServer.java) | L148 injectPointerMove / L154 Delta / L164,173 Button / L182 Key |
| 指针状态 | [xserver/Pointer.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xserver/Pointer.java) | L71 setPosition / L81 setButton |
| X 事件分发 | [xserver/InputDeviceManager.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xserver/InputDeviceManager.java) | L146/181/217 三个 onPointer\* |
| XI2 Raw 事件 | [xserver/extensions/XInputExtension.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xserver/extensions/XInputExtension.java) | L240 sendRawButtonState / L253 sendRawMotion |
| 触摸→X 换算 | [widget/TouchpadView.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/widget/TouchpadView.java) | L78 updateXform / L358 onExternalMouseEvent / L419 onCapturedPointer |
| letterbox 数学 | [renderer/ViewTransformation.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/renderer/ViewTransformation.java) | L14 update |
| 全屏拉伸+指针逆变换 | [renderer/FullscreenTransformation.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/renderer/FullscreenTransformation.java) | L17 update / L26 transformPointerCoords |
| 主 Activity | [XServerDisplayActivity.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/XServerDisplayActivity.java) | L236 建 XServer / L495 setupXEnvironment / L955 getWineStartCommand / L798-806 事件分发 |
| GL 合成器 | [renderer/GLRenderer.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/renderer/GLRenderer.java) | L97 swapInterval(0) / L122 drawFrame / L238 renderWindows |
| AHardwareBuffer | [renderer/GPUImage.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/renderer/GPUImage.java) | L99-105 native 方法 |
| WinHandler 协议 | [winhandler/WinHandler.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/winhandler/WinHandler.java) | L162 mouseEvent / L28 端口 7946/7947 |
| Win32 鼠标标志 | [winhandler/MouseEventFlags.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/winhandler/MouseEventFlags.java) | ABSOLUTE=0x8000 |
| 请求码 | [winhandler/RequestCodes.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/winhandler/RequestCodes.java) | MOUSE_EVENT=7 等 |
| 容器模型 | [container/Container.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/container/Container.java) | L20-29 默认值 / saveData |
| 容器管理 | [container/ContainerManager.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/container/ContainerManager.java) | L98 createContainer / L67 activateContainer |
| 快捷方式 | [container/Shortcut.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/container/Shortcut.java) | [Extra Data] 段解析 |
| 启动器组件 | [xenvironment/components/GuestProgramLauncherComponent.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xenvironment/components/GuestProgramLauncherComponent.java) | L86 execGuestProgram / L136 addBox64EnvVars |
| X 接入组件 | [xenvironment/components/XServerComponent.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xenvironment/components/XServerComponent.java) | 全文 |
| Vortek 组件 | [xenvironment/components/VortekRendererComponent.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xenvironment/components/VortekRendererComponent.java) | Options L42-74 |
| 图形驱动枚举 | [container/GraphicsDrivers.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/container/GraphicsDrivers.java) | L14-15 默认 Vortek+Gladio / L81 getDefaultDriver |
| 默认版本 | [core/DefaultVersion.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/core/DefaultVersion.java) | BOX64 0.4.4 / Turnip 26.2.0 / DXVK 2.4.1 |
| 在线组件 | [core/GeneralComponents.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/core/GeneralComponents.java) | L23 INSTALLABLE_COMPONENTS_URL |
| RootFS 安装 | [xenvironment/RootFSInstaller.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xenvironment/RootFSInstaller.java) | L29 LATEST_VERSION=23 / L47 install |
| RootFS 布局 | [xenvironment/RootFS.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xenvironment/RootFS.java) | L14-18 路径常量 |
| socket 路径 | [xconnector/UnixSocketConfig.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/xconnector/UnixSocketConfig.java) | L8-13 |
| 启动 Intent | [ShortcutsFragment.java](https://github.com/brunodev85/winlator-app/blob/main/app/src/main/java/com/winlator/ShortcutsFragment.java) | L228-231 |
| 子模块定义 | [.gitmodules](https://raw.githubusercontent.com/brunodev85/winlator/main/.gitmodules) | winlator-app / vortek / gladio |
| Vortek 说明 | [vortek README](https://github.com/brunodev85/vortek) | Vulkan host driver 兼容层 |
| 许可证 | [winlator LICENSE](https://raw.githubusercontent.com/brunodev85/winlator/main/LICENSE) | LGPL-2.1 |
