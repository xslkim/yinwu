# 06 · TeknoParrot 光枪输入机制与 DemulShooter 深度调研

> 调研目标：把自研手机光枪系统（手机摄像头识别屏幕白边框 + 陀螺仪融合，解算出枪口指向屏幕的
> 1920×1080 规范坐标，经局域网 120Hz UDP 上报 PC，另有扳机事件）作为光枪输入，接入 PC 上由
> TeknoParrot 运行的街机光枪游戏（幽灵特警进化 Ghost Squad Evolution、Point Blank X、
> 吸血鬼之夜 Vampire Night、Big Buck Hunter Pro、Haunted Museum、Aliens Extermination 等）。
> 本文拆解 TeknoParrot 原生光枪链路、DemulShooter 的注入原理、Sinden 光枪的完整配合方式，
> 最后对三条接入路线给出对比与推荐。
>
> 调研日期：2026-10。除特别说明外，源码引用均给出 GitHub 路径，可直接核对。

---

## 目录

- [1. 结论速览](#1-结论速览)
- [2. TeknoParrot 输入架构总览](#2-teknoparrot-输入架构总览)
- [3. TeknoParrot 原生"鼠标当光枪"机制](#3-teknoparrot-原生鼠标当光枪机制)
  - [3.1 GameProfile 决定光枪输入路径的关键字段](#31-gameprofile-决定光枪输入路径的关键字段)
  - [3.2 RawInput 路径：绝对坐标如何映射到游戏枪坐标](#32-rawinput-路径绝对坐标如何映射到游戏枪坐标)
  - [3.3 DirectInput 路径](#33-directinput-路径)
  - [3.4 扳机 / 换弹 / 开始 / 投币的映射](#34-扳机--换弹--开始--投币的映射)
  - [3.5 "屏幕外开枪换弹"（Offscreen Reload）](#35-屏幕外开枪换弹offscreen-reload)
- [4. DemulShooter 深度调研](#4-demulshooter-深度调研)
  - [4.1 总体架构：进程外读枪 + 进程内注入](#41-总体架构进程外读枪--进程内注入)
  - [4.2 三类注入手法的源码证据](#42-三类注入手法的源码证据)
  - [4.3 支持的光枪硬件及数据读取方式](#43-支持的光枪硬件及数据读取方式)
  - [4.4 网络 / 远程输入接口：结论是没有](#44-网络--远程输入接口结论是没有)
  - [4.5 对本项目游戏清单的覆盖情况](#45-对本项目游戏清单的覆盖情况)
  - [4.6 输出力反馈（Recoil / 灯光）](#46-输出力反馈recoil--灯光)
  - [4.7 DemulShooter vs TeknoParrot 原生光枪](#47-demulshooter-vs-teknoparrot-原生光枪)
- [5. Sinden 光枪与 TeknoParrot 的完整链路](#5-sinden-光枪与-teknoparrot-的完整链路)
- [6. 双人双枪的实现方式](#6-双人双枪的实现方式)
- [7. 三条接入路线对比与推荐](#7-三条接入路线对比与推荐)
- [8. 可操作配置示例](#8-可操作配置示例)
- [附：参考来源汇总](#附参考来源汇总)

---

## 1. 结论速览

1. **TeknoParrot 原生光枪链路完全走 RawInput**：TPUI 把光枪/鼠标的绝对坐标归一化为 0.0–1.0 的
   "窗口内因子"，再按 profile 的 `xAxisMin/Max`、`InvertedMouseAxis` 换算成游戏原生枪坐标，
   写入名为 `TeknoParrot_JvsState` 的 64 字节共享内存；注入到游戏进程的 OpenParrot DLL 读这块
   内存，伪装成 JVS I/O 板/鼠标 API 喂给游戏。
2. **DemulShooter 没有"从网络接收光枪坐标"的入向接口**。它自己的 IPC（`-ipcinputs`）方向相反——
   是把 DemulShooter 算好的枪数据**发布**到命名内存映射文件给别的本地程序读；Unity 游戏的 TCP
   （33610 端口）方向也相反——BepInEx 插件在游戏内当服务器，DemulShooter 作为客户端**推**数据进去。
   它只从本机 RawInput 设备（绝对模式 HID 光枪 / 鼠标 / 摇杆）取输入。
3. 因此三条路线里，**路线 (b)——桥接成"绝对坐标模式"的虚拟 HID 设备——兼容性最好**：它同时是
   TeknoParrot RawInput 光枪和 DemulShooter 眼中的标准光枪设备（与 AimTrak/Sinden 同类），
   双枪、校枪、后坐力输出全都能用，且不会和系统鼠标打架。
4. 路线 (a)（SendInput 绝对鼠标 + TP 原生光枪）**零驱动、开发量最小，适合先做 POC**：
   TP 的 RawInput 监听内置了一个名为 `Windows Mouse Cursor` 的伪设备，直接跟踪系统光标位置当枪用；
   但单光标意味着原生方案下基本只能单枪，且会抢占用户鼠标。
5. 路线 (c)（扩展 DemulShooter 输入层）适合**需要后坐力/灯光输出或 TP 原生搞不定的游戏**
   （如 Haunted Museum 的相对输入、Ghost Squad Evolution 的 JVS 原始轴），做法是给 DemulShooter
   源码增加一个"UDP 网络设备"（仿照 `RawInputController` 写一个新的设备源），工作量可控，
   但要用我们自己维护的 fork，或用虚拟 HID + 原版 DemulShooter 的组合避免 fork。

---

## 2. TeknoParrot 输入架构总览

TeknoParrot 由两部分组成，输入数据在两者之间的流向是理解一切的前提：

```
[输入设备] --RawInput/DI/XI--> [TeknoParrotUI (TPUI, WPF 前端, C#)]
                                      |  计算成游戏原生坐标
                                      v
                    共享内存 "TeknoParrot_JvsState" (64 字节)
                                      |
                                      v
[游戏进程] <-- 游戏原生输入 API/JVS -- [OpenParrot (注入游戏的 DLL, C++)]
```

- **TPUI 侧**：`TeknoParrotUi.Common/InputListening/` 下的三个监听器
  （[InputListenerRawInput.cs](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/InputListening/InputListenerRawInput.cs)、
  InputListenerDirectInput.cs、InputListenerXInput.cs）负责读设备；
  处理结果写入静态类 [`InputCode`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/InputCode.cs)
  的数字按键状态和 `AnalogBytes[32]` 模拟量数组。
- **共享内存**：[`JvsHelper.cs:16`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Jvs/JvsHelper.cs)
  `StateSection = MemoryMappedFile.CreateOrOpen("TeknoParrot_JvsState", 64)`。
  TPUI 把按键/模拟量序列化进这 64 字节。
- **游戏侧**：OpenParrot 注入 DLL 在游戏进程里打开同一块内存，见
  [`OpenParrot/src/Functions/Ring_amLib/amJvs.cpp:247-258`](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Ring_amLib/amJvs.cpp)：
  `CreateFileMapping(..., 64, L"TeknoParrot_JvsState")` + `MapViewOfFile`，并把内部指针
  `ffbOffset`（P1 数字键）、`ffbOffset2/3`（P1 X/Y 轴）、`ffbOffset4/5`（P2 X/Y 轴）……
  绑定到各槽位；随后由每个游戏自己的 hook 代码（如
  [`TypeX2/HauntedMuseumInputMisc.cpp`](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Games/TypeX2/HauntedMuseumInputMisc.cpp)、
  JVS 仿真、DirectInput wrapper 等）把这些值以游戏期望的方式"回放"进去——
  有的游戏是直接写内存（Haunted Museum 里 `*(DWORD*)(imageBase + 0x32797C) = 0x02; // Gun Board Connected`），
  有的是 hook 串口 API 伪装 JVS I/O 板应答（`amJvs.cpp` 里的 `Hook_GetCommModemStatus`）。
- 部分游戏不用共享内存而用命名管道：`TeknoParrotUi.Common/Pipes/` 下每个游戏一个 Pipe 类
  （如 [`AliensExterminationPipe.cs`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Pipes/AliensExterminationPipe.cs)），
  基类 `ControlSender.TransmitThread()` 每 15ms 把按键压成位图发给游戏内的管道服务器。

**要点**：无论鼠标、光枪还是手柄，最终都归一成"游戏原生枪坐标 + 按键位图"。
我们的手机光枪要接入，只需在它认可的任一设备入口提供正确形态的输入。

---

## 3. TeknoParrot 原生"鼠标当光枪"机制

### 3.1 GameProfile 决定光枪输入路径的关键字段

每个游戏的 profile（`TeknoParrotUi.Common/GameProfiles/*.xml`）里与光枪相关的字段，定义见
[`GameProfile.cs`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfile.cs)：

| 字段 | 类型 | 作用 |
| --- | --- | --- |
| `GunGame` | bool | 标记这是光枪游戏。DirectInput 模式下启用"摇杆轴→枪坐标"特殊换算；UI 里开放 P1–P4 LightGun 绑定项 |
| `ConfigValues["Input API"]` | DirectInput / XInput / RawInput | **光枪必须选 RawInput**（官方 wiki 原文："To use the native Lightgun support, you must choose RawInput"） |
| `xAxisMin/Max`、`yAxisMin/Max` | short，默认 0/255 | 游戏原生枪坐标的量程。屏幕归一化因子线性映射到 `[min,max]` 区间 |
| `InvertedMouseAxis` | bool | 反转枪坐标轴序与方向（很多街机基板 X/Y 轴定义与屏幕相反，如 Haunted Museum、Big Buck Hunter Pro 都是 `true`） |
| `Use Relative Input` | bool（ConfigValue） | 切到相对移动模式：鼠标/枪的相对位移 × 灵敏度系数推动准星，配 `Player N Relative Sensitivity`（1–50）。用于原生输入是相对量（或绝对定位有毛病）的游戏，如 Haunted Museum、Point Blank X 的 profile 都带此项 |
| `Use16BitAnalog` / `HighResolutionAxis` | bool | 启用 16 位枪轴（0–65535），解决 8 位（256 级）精度不足导致的"像素级跳变"。注意 `HighResolutionAxis` 是代码里按游戏白名单硬编码的（GameProfile.cs 中仅少数 dll 生效） |
| `EmulationProfile` / `EmulatorType` | 枚举 | 决定注入哪套游戏 hook（OpenParrot / ElfLdr2 / Play! / RPCS3…） |

实例：[`HauntedMuseum.xml`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfiles/HauntedMuseum.xml)
关键行——`<EmulationProfile>HauntedMuseum</EmulationProfile>`、`<InvertedMouseAxis>true</InvertedMouseAxis>`、
`<GunGame>true</GunGame>`、四个轴量程 0–255、Input API 下拉默认 RawInput，另带
"Custom Crosshairs / Use Relative Input / 双人灵敏度"字段。
[`BBHPro.xml`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfiles/BBHPro.xml)
则是 `<EmulationProfile>RawThrillsGUN</EmulationProfile>` + `<EmulatorType>ElfLdr2</EmulatorType>`
（这是个 Linux 游戏，靠 ElfLdr2 加载），同样 `GunGame=true`、`InvertedMouseAxis=true`。

### 3.2 RawInput 路径：绝对坐标如何映射到游戏枪坐标

核心函数是 InputListenerRawInput.cs 的
[`HandleRawInputGun(JoystickButtons, int inputX, int inputY, bool moveAbsolute, bool virtualDesktop)`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/InputListening/InputListenerRawInput.cs#L1637)。

**设备侧三种来源**（WM_INPUT 处理，同文件约 L925–1005）：

1. **绝对模式设备**（`RawMouseFlags.MoveAbsolute`）：AimTrak、Sinden、 GunCon 等光枪在系统里就是
   "绝对坐标鼠标"，RawInput 报文 `LastX/LastY` 是 0–0xFFFF 的归一化绝对坐标
   （带 `MOUSE_VIRTUAL_DESKTOP` 标志时相对于虚拟桌面，否则相对于主显示器）。
2. **伪设备 "Windows Mouse Cursor"**：当设备报相对移动（普通鼠标）时，TP 会改用
   `Cursor.Position`（系统光标像素坐标）——这就是把普通鼠标/系统光标直接当光枪的入口。
   **对我们路线 (a) 至关重要**：SendInput 移动系统光标，TP 选这个伪设备即可。
3. **相对模式设备**：普通鼠标的 `LastX/LastY` 是相对位移，TP 内部维护 `_lastPosX/Y`
   累加并夹在窗口范围内，配合灵敏度（`_dpiScaleX/Y` 与 profile 灵敏度）使用——即
   "Use Relative Input" 模式。

**坐标换算**（HandleRawInputGun 主体）：

- 先算"窗口内因子" `factorX/factorY ∈ [0,1]`，0,0 = 游戏画面左上角，1,1 = 右下角：
  - **窗口化**（含无边框）：绝对单位先换算回屏幕像素
    （`inputX / 0xFFFF * SystemParameters.PrimaryScreenWidth`），
    再 `(inputX - _windowLocationX) / _windowWidth`，越界钳位到 0/1；
  - **全屏**：绝对设备直接 `factorX = inputX / 0xFFFF`；
    相对来源（系统光标）用 `inputX / GetSystemMetrics(SM_CXSCREEN)`。
    注释里特别说明用 `GetSystemMetrics`（物理像素）而不是 `SystemParameters`（DIP），
    因为 200% DPI 缩放下后者会减半导致"双重回绕 bug"——**所以 Windows 显示缩放必须设 100%**，
    Sinden wiki 也把这条列为 TeknoParrot 配枪的首要注意事项。
- **多显示器**：RawInput 绝对坐标默认相对主显示器；设备报 `MOUSE_VIRTUAL_DESKTOP` 时，
  代码里仅对个别平台（`_isTeknoSS32`）用 `SM_XVIRTUALSCREEN/SM_CXVIRTUALSCREEN` 做了
  虚拟桌面原点修正。工程上结论：**把游戏显示器设为主显示器**，可以避免绝大多数偏移问题。
- 再按 profile 量程换算：`x = minX + factorX * (maxX - minX)`，
  `InvertedMouseAxis` 或 16 位模式下按代码注释中的字节序规则写入 `InputCode.AnalogBytes`
  （16 位大端；P1 占字节 0–3，P2 占 4–7，P3/P4 依次类推，Gunslinger/Luigi 有特例）。
- 最终 `InputCode.AnalogBytes` 经 JvsHelper 落入 `TeknoParrot_JvsState` 共享内存，
  由游戏侧 OpenParrot 按各自的转换（有的游戏内部再除 255 归一化，如 D3D9Misc.cpp 里
  `vPos1P.x = (*ffbOffset2) / 255.0f * resWidth`）变成准星位置。

**精度提示**：8 位模式全屏只有 256 级 X 分辨率（1080p 下约 7.5px/级），明显可感知；
支持 `HighResolutionAxis` 的游戏务必开 16 位。对我们 120Hz/亚像素的手机光枪，
8 位量化是原生路线的主要精度瓶颈。

### 3.3 DirectInput 路径

InputListenerDirectInput.cs（约 4480 行）中，`GunGame=true` 时 DI 设备的模拟轴走另一套换算：
X 轴 `analogPos = _minX + analogPos / _DivideX` 后按 `_invertedMouseAxis` 取反
（同文件 L3472 起的 `case AnalogType.AnalogJoystick` / L3741 `AnalogJoystickY`）。
DI 模式下"鼠标"就是 DI 枚举出的 SystemMouse，其轴是相对量，TP 同样走内部位置累加。
DI 路径的主要问题是**无法区分多只鼠标类设备**（所有鼠标合并为系统光标），
所以双枪必须用 RawInput。XInput 路径同理不适合光枪（手柄摇杆是相对量且无光标概念）。

### 3.4 扳机 / 换弹 / 开始 / 投币的映射

RawInput 模式下，鼠标/光枪的 5 个按键（左/右/中/X1/X2）在
InputListenerRawInput.cs L932–959 逐一映射到用户在"CONTROLLER SETUP"里绑定的功能：
对枪类游戏惯例是——

| 输入 | 惯例绑定 | 说明 |
| --- | --- | --- |
| 鼠标左键 / 枪扳机（屏内） | Player N Trigger（Button1） | 开火 |
| 鼠标右键 / 枪扳机（屏外）或枪侧键 | Player N Reload | 换弹/装填；Sinden 类枪"屏外扣扳机"上报为右键 |
| 鼠标中键 | Action / 手雷 / 换武器 | 如 GSEVO 的换武器（DemulShooter wiki 控制表同样：左=扳机、中=Action、右=换弹） |
| 键盘/手柄键 | Start（Button2）、Coin、Service、Test、音量 | TP 用键盘 F 键/数字键映射即可；投币也可开 FreePlay 规避 |

绑定时 TP 会区分"屏内/屏外"：Sinden wiki 强调映射屏内功能（扳机）时必须**开着白边框、
枪口指向屏幕**，映射屏外功能时指向边框外——因为枪在两种状态下上报的是不同的鼠标键。

以 Aliens Extermination 为例，游戏侧
[`AliensExterminationPipe.cs`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Pipes/AliensExterminationPipe.cs)
把 `Button1`→Trigger、`Button2`→Start、`Button3`→Special、`Button4`→Flame(喷火器)、
`Coin`→投币压成 Control 位图，15ms 一帧发给游戏。

### 3.5 "屏幕外开枪换弹"（Offscreen Reload）

三种实现层次：

1. **硬件层**（最常用）：Sinden/AimTrak 固件在"摄像头看不到边框（屏外）时扣扳机"上报为
   鼠标右键而非左键，用户把右键绑 Reload 即完成。与模拟器无关，最可靠。
2. **TP profile 层**：个别游戏 profile 提供 `OffScreenReloadHack`（如
   [`HOTD4ELF2.xml`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfiles/HOTD4ELF2.xml)，
   Sinden wiki 的 HOTD4 一节也写明 "Off-screen reload hack = Enabled"）。该选项由游戏侧
   OpenParrot hook 实现：检测"扳机按下时枪坐标在画面外"→ 向游戏写换弹状态而非开火。
   我们的手机光枪坐标系自带"是否在屏内"判定，可在桥接层直接复刻此逻辑：
   屏外扳机 → 发右键（Reload），屏内扳机 → 发左键（Trigger）。
3. **DemulShooter 层**：部分游戏（Operation G.H.O.S.T. 等）在 DemulShooter_GUI 里可配置
   RELOAD/ACTION 共用或分离按钮，见
   [DemulShooter wiki · Configuration](https://github.com/argonlefou/DemulShooter/wiki/Configuration)。

---

## 4. DemulShooter 深度调研

[DemulShooter](https://github.com/argonlefou/DemulShooter)（作者 argonlefou）是这个领域的
"参照物"：README 自述"interfering with (mostly) emulators to allow users to play railshooter games
with up to 4 lightguns"。它对 TeknoParrot 游戏的价值在于**双枪/四枪、校枪、后坐力输出**，
以及替那些原生输入有毛病的游戏直接重写输入链路。

### 4.1 总体架构：进程外读枪 + 进程内注入

```
[光枪设备] --RawInput(绝对/相对)--> [DemulShooter.exe (常驻, 需管理员)]
        - WM_INPUT 消息窗口注册 Joystick/Mouse/Gamepad 三类用法页
        - 每枪把原始轴按设备量程 ScreenScale 到屏幕像素 (Computed_X/Y)
        - 可叠加 Act Labs 偏移、模拟轴手动量程、轴反转
                    |
        +-----------+----------------------------+
        | 老游戏/模拟器: 注入代码洞+写内存        | Unity 游戏: TCP 推给 BepInEx 插件
        v                                          v
[游戏进程: CodeCave + JMP hook]            [游戏内 BepInEx 插件 (TCP server :33610)]
```

- DemulShooter.exe 是 **x86**（另附 DemulShooterX64.exe 对应 64 位目标，游戏清单不同）。
  命令行形如 `DemulShooter.exe -target=lindbergh -rom=gsquad`，`-target` 选平台家族，
  `-rom` 选具体游戏；`-pname=` 可覆盖要 hook 的进程名（默认按已验证的 dump 硬编码，
  如 GSEVO hook 的是 TeknoParrot 的 `linuxloader`）。
- 运行后常驻托盘，定时器轮询目标进程，hook 成功后游戏画面角落显示红色 "DemulShooter" 水印，
  目标退出即自退。**必须以管理员运行**（wiki Usage 页首条警告）。
- 设备层：`DemulShooterWindow.cs` 里 `RegisterRawInputDevices` 注册
  Joystick/Mouse/Gamepad 三个 HID 用法页到隐藏消息窗口，WM_INPUT 里按
  设备路径区分每只枪（这是它能双枪而普通鼠标不能的根本原因——RawInput 带设备身份）。

### 4.2 三类注入手法的源码证据

**(a) DLL 注入 + 代码洞 + 内存写（绝大多数游戏）**
以幽灵特警进化为例，
[`DemulShooter/Games/Game_LindberghGsquadEvo.cs`](https://github.com/argonlefou/DemulShooter/blob/master/DemulShooter/Games/Game_LindberghGsquadEvo.cs)：

- 构造时指定目标进程名 `linuxloader`（即 TeknoParrot 的 Lindbergh 加载器），
  并等待窗口标题含 "FPS" 再动手；
- 定义一组硬编码地址：`_ComputedAxes_BaseAddress = 0x086553AC`（存 float 轴值）、
  `_JvsRawAxes_InjectionStruct = new InjectionStruct(0x08185951, 7)`（在 JVS 原始轴读取处
  打 JMP 进代码洞）、`_Buttons_InjectionStruct`、`_Recoil_InjectionStruct`（截获后坐力输出）等；
- `Apply_InputsMemoryHack()` → `SetHack_Axis()` 里 `Codecave.Alloc(0x800)` 后逐字节写汇编
  （`CaveMemory.Write_StrBytes("8B 15")...`），游戏每帧执行到被 patch 的位置就跳进代码洞，
  从 DemulShooter 写好的共享区取枪坐标。即"反汇编游戏 → 找输入读取点 → 抢过来"。

**(b) BepInEx 插件（Unity 引擎游戏，含 Point Blank X）**
仓库 [`UnityPlugins/`](https://github.com/argonlefou/DemulShooter/tree/master/UnityPlugins)
下 20 个游戏各有一个 BepInEx 插件工程（PBX、MIB、Tomb Raider、Wild West Shootout、
Haunted Museum 无——它是 Taito Type X 原生，不走 Unity）。

- 插件通过 DemulShooter_GUI 的 "Unity Plugin Installation" 页装到游戏目录；
  插件在游戏进程里起 **TCP 服务器（127.0.0.1:33610）**；
- DemulShooter 侧 [`Game__Unity.cs:72`](https://github.com/argonlefou/DemulShooter/blob/master/DemulShooter/Games/Game__Unity.cs)
  `new DsTcp_Client("127.0.0.1", DS_TCP_CLIENT_PORT /*33610*/)` 作为**客户端**连进去；
- 数据包格式见 [`DsCore/IPC/DsTcp_TcpPacket.cs`](https://github.com/argonlefou/DemulShooter/blob/master/DsCore/IPC/DsTcp_TcpPacket.cs)：
  `[4字节长度][1字节类型(1=Inputs/2=Outputs)][Payload]`，Payload 由
  [`DsTcpData.cs`](https://github.com/argonlefou/DemulShooter/blob/master/DsCore/IPC/DsTcpData.cs)
  按字段名字典序反射序列化；插件侧输入结构
  [`UnityPlugin_BepInEx_PBX/TcpInputData.cs`](https://github.com/argonlefou/DemulShooter/blob/master/UnityPlugins/UnityPlugin_BepInEx_PBX/TcpInputData.cs)
  字段为 `float[] Axis_X, Axis_Y, byte[] Trigger`（归一化浮点，每玩家一组）；
- 插件内用 Harmony 补丁替换游戏的输入函数（如 PBX 的 `patch/mBNUsioController.cs`、
  `mInputWrapper.cs`），把 DemulShooter 推来的轴/扳机当作 Namco USIO 板的读数返回给游戏。

**(c) 命名内存映射文件（MMF）**
`-ipcinputs` / `-ipcoutputs` 选项（`DemulShooterWindow.cs:135`）分别创建
`DemulShooter_MMF_Inputs` / `DemulShooter_MMF_Outputs` 两块 2048 字节 MMF
（`DemulShooterWindow.cs:61-64, 269-277`）。HotD Remake 等个别目标用 MMF 与外部插件交换
（[`MMFH_HotdRemakeArcade.cs`](https://github.com/argonlefou/DemulShooter/blob/master/DsCore/IPC/MMFH_HotdRemakeArcade.cs)，
数据结构 [`MMF_DataStruct.cs`](https://github.com/argonlefou/DemulShooter/blob/master/DsCore/IPC/MMF_DataStruct.cs)：
`RawValue_X/Y, ComputedValue_X/Y, ComputedButtonEvent`）。

### 4.3 支持的光枪硬件及数据读取方式

DemulShooter **只认 RawInput 设备**，GUI 设备列表只列支持 RawInput 的设备
（[wiki · Configuration](https://github.com/argonlefou/DemulShooter/wiki/Configuration) 原文：
"Only devices supporting RawInput data will be listed (such as Aimtrak, ArcadeGuns, Act Labs guns,
HID compatible devices....)"）：

| 设备 | 读取方式 | 备注 |
| --- | --- | --- |
| Ultimarc AimTrak | RawInput 绝对鼠标（0–65535 归一化）+ 鼠标键 | 设计基准：扳机=左键、屏外扳机=右键（换弹）、侧键=中键，见 `RawInputController.cs:242-245` 注释 |
| Sinden Lightgun | 同上（绝对鼠标）+ 串口用于后坐力 | 需要 Sinden 软件先显示边框 |
| GUN4IR | 绝对 HID 指针 + 串口反馈 | 社区主力 DIY 方案 |
| Act Labs（老 USB 枪） | 绝对 HID，可加像素级 X/Y 偏移校准 | 有专门校准页（Act_Labs_Offset） |
| GunCon 2/3（经驱动） | HID 摇杆/绝对轴 | GUI 里可选轴 |
| Wiimote | HID/特殊处理（`RawInputController.cs:559` 提到带 XInput 驱动的 DS3/Wiimote 特例）；Dolphin 游戏则直接装 Wiimote 配置 | |
| 普通鼠标/摇杆 | 相对模式 → 用 `GetCursorPos` 取系统光标位置（`DemulShooterWindow.cs:1040-1060`） | **多只相对设备共享同一光标**，故不能用于双枪（wiki 明确警告） |

统一换算：绝对轴 `ScreenScale(原始值, 设备轴量程, 0, 游戏画面宽/高)` 得屏幕像素坐标
（`DemulShooterWindow.cs:1070-1085`），再由各 Game 类换算成游戏原生坐标写进去。
模拟量（电位器枪）还支持"手动量程校准"。

### 4.4 网络 / 远程输入接口：结论是没有

逐项排查（对本项目最关键）：

- **`-ipcinputs` 不是入向接口**。代码里它只做一件事：在每次 WM_INPUT 处理后
  `_MMF_Inputs.UpdateComputedPlayerData(...)` + `WriteData()`——把 DemulShooter **自己算好的**
  每枪 X/Y/按键**发布**到 `DemulShooter_MMF_Inputs` 供本机其他程序读取
  （`DemulShooterWindow.cs:1061, 1162-1165`）。wiki 措辞 "read gun data from another program"
  指的是"别的程序来读"。
- **Unity TCP（33610）方向也是出向**：插件在游戏内当服务器，DemulShooter 连进去推输入。
  我们理论上可以写一个"假冒 DemulShooter"的 TCP 客户端直接给 PBX 的 BepInEx 插件推
  `TcpInputData`（协议就是上面 4.2(b) 的包格式，全部开源），**这是不 fork 代码就能把网络坐标
  送进 PBX 的一条暗道**——但仅限装了插件的 Unity 游戏，不通用。
- **Sinden 没有 network 模式**；Sinden 软件只在本机把枪呈现为绝对鼠标。
- 仓库与 wiki 中不存在 UDP/socket 入向输入；BYOAC 长帖
  [forum.arcadecontrols.com/index.php?topic=149714.0](http://forum.arcadecontrols.com/index.php?topic=149714.0.html)
  中亦无官方远程输入功能。社区倒是有 IntegrumRetro 等第三方"即插即用光枪工具"，但同样是
  本地设备桥接，不是网络协议。

**推论**：要让手机坐标进入 DemulShooter，只有两条现实路径——
(i) 在 PC 上做一个**虚拟绝对 HID 设备**（路线 b），DemulShooter 会把它当普通光枪；
(ii) **fork DemulShooter**，仿照 `RawInputController` 增加一个 UDP 设备源直接填
`Player.RIController.Computed_X/Y`（改动集中在设备层，注入层完全复用）。

### 4.5 对本项目游戏清单的覆盖情况

对照 [wiki · Usage](https://github.com/argonlefou/DemulShooter/wiki/Usage) 的目标/rom 表：

| 游戏 | DemulShooter 支持 | 命令 | 备注 |
| --- | --- | --- | --- |
| Ghost Squad Evolution（幽灵特警进化，Lindbergh） | ✅ `-target=lindbergh -rom=gsquad` | hook `linuxloader`（即 TP 的 Lindbergh 加载器）；左键扳机/中键换武器/右键换弹 | 但 argonlefou 本人在 BYOAC 说："TeknoParrot can handle multiple lightguns itself, DemulShooter is working for Lindbergh games (because it was done before TP added its own input support...)"——**现在更推荐直接用 TP 原生** |
| Point Blank X（Namco ES4） | ✅ `-target=es4 -rom=pblankx` | Unity 游戏，走 BepInEx 插件 + TCP 推送 | 插件另有 `-nocrosshair` 等选项 |
| Haunted Museum / Haunted Museum 2（Taito Type X） | ✅ `-target=ttx -rom=hmuseum` / `hmuseum2` | HM2 需先按其 wiki 打版本补丁；`game.exe` 内存写 | TP 原生对此游戏用相对输入，DS 是绝对注入，**手感通常 DS 更好** |
| Aliens Extermination（GlobalVR） | ✅ `-target=globalvr -rom=aliens` | 支持第二版 dump（x86/x64、去 dongle）；含后坐力输出（Gamoover 论坛有 FFB 配置长帖） | 若只要单/双枪，TP 原生亦可 |
| Big Buck Hunter Pro | ⚠️ 仅 X64 版支持 **Ultimate Trophy**（`-target=windows -rom=bbhut`）；Pro（Raw Thrills Linux 版）无 DS 支持 | — | BBH Pro 用 TP 原生（RawThrillsGUN + ElfLdr2）即可 |
| Vampire Night（吸血鬼之夜） | ❌ 不支持 | — | 该作是 Namco System 246（PS2 系），TP 经 **Play!** 模拟器改版运行（本地取证文档 05 已确认 `Play.exe + arcadedefs`，HookedWindows 含 VPNGAME），输入由 TP 注入 Play!，DS 无对应 target |
| Wartran Troopers / Cooper's 9 / Gashaaaan Refill / Akuma / Block King / Robin Hood / Wild West Shootout / Friction / Bug Busters / Medaru no Gunman / After Dark(→Night Hunter) 等 | ✅ 多数有（konami/gamewax/ttx/arcadepc/windows/ringwide 各 target） | 见 Usage 表 | 本项目游戏目录与 DS 支持列表重合度很高，说明这批游戏正是 DS 的目标用户群 |

### 4.6 输出力反馈（Recoil / 灯光）

DemulShooter 的另一半价值是输出（[wiki · Outputs](https://github.com/argonlefou/DemulShooter/wiki/Outputs)）：

- 注入时同步截获游戏的原生输出（灯光、电磁铁后坐、震动马达），并可计算**自定义输出**
  （统一 `Recoil` 脉冲、`Damaged`、弹药数、生命值）；
- 两种外发方式：**Window Message**（兼容 MameHooker / QMameHook / Hook of the Reaper /
  OutputHooker）和 **Network TCP `127.0.0.1:8000`**（明文 telnet 协议，先报 rom 名再逐条输出状态）；
- 后坐脉冲的 ON/OFF 时长可在 GUI 调，避免电磁铁过热或连发时粘连；
- **对我们的意义**：即使后坐机构在"枪"端（手机壳体/手柄），也可以让 PC 桥接程序连
  DS 的 8000 端口，把 Recoil/Damaged 事件经局域网回推给手机做震动反馈——
  这是 TP 原生路线给不了的能力（TP 的输出走 FFB Blaster/CabinetOutputSettings，
  面向力反馈手柄与机台硬件）。

### 4.7 DemulShooter vs TeknoParrot 原生光枪

| 维度 | TP 原生（RawInput） | DemulShooter |
| --- | --- | --- |
| 接入成本 | 零额外进程，profile 选好 Input API 即可 | 每游戏一条命令行，需管理员，依赖 dump 版本（MD5 校验，rom 改版即失效） |
| 精度 | 多数游戏 8 位（256 级），白名单游戏 16 位 | 注入点多为游戏原生 float/int 轴，精度取决于游戏本身，通常更好 |
| 双枪 | 原生支持最多 2 枪（官方 wiki） | 最多 4 枪 |
| 后坐力/灯光输出 | 仅 FFB Blaster/机台输出（面向 FFB 手柄） | MameHooker/网络输出 + 自定义 Recoil，生态成熟 |
| 游戏特定修复 | 无 | 修"原生输入有病"的游戏（HM 相对输入、OG 换弹逻辑、RPCS3 输入屏蔽等） |
| 维护风险 | 随 TP 更新 | 随 TP/游戏 dump 更新需等作者适配新地址 |

argonlefou 本人态度：TP 能自己搞定的（Lindbergh 双枪）就用 TP 原生；
DS 留着补 TP 的短板（双枪以上、输出、输入修复、非 TP 目标如 Demul/Model2）。

---

## 5. Sinden 光枪与 TeknoParrot 的完整链路

Sinden 与本项目手机光枪原理相同（摄像头拍屏幕白边框反解指向），它的落地细节可直接照搬：

1. **枪端**：Sinden 固件解算指向后，以 USB 绝对鼠标（RawInput 绝对坐标）上报；
   屏内扳机=左键，屏外扳机=右键。另有串口用于接收后坐力命令。
2. **边框**：Sinden 软件在本机显示白色边框。两种叠加方式——
   - 默认是**独立置顶窗口**画边框，要求游戏为窗口化/无边框（borderless）；
   - **独占全屏**游戏置顶窗口会被盖住，改用 **ReShade** 注入游戏，用 Border.fx
     画白色边框（[sindenwiki · Reshade](https://www.sindenwiki.org/wiki/Reshade)：启用 Border.fx，
     颜色改白、调边宽）。Sinden wiki 的 TeknoParrot 页明确："Use ReShade if you have issues
     displaying the Sinden border"，并建议单独一份 TP 安装来对 BudgieLoader.exe 挂 ReShade。
3. **TeknoParrot 内置边框/准星**：部分 profile 直接内置，无需 Sinden 软件/ReShade——
   典型是 [`PointBlankX.xml`](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfiles/PointBlankX.xml)：
   `Crosshair` 分类（Enable / Enable Alpha Blend）与 `Border` 分类
   （Enable，Hint "Place Border.png into game folder"；Scale 拉伸到分辨率）——
   即把 `Border.png` 放进游戏目录，TP 的游戏内渲染 hook 直接叠加白边框。
    HauntedMuseum profile 也有 "Custom Crosshairs" 分类。
   **对手机光枪方案，这正好是我们白边框的最优来源：由 TP 在游戏内画，独占全屏也不怕。**
4. **TP 侧绑定**：Game Settings 里 Input API=RawInput；Controller Setup 里
   "P1 Light Gun" 设备下拉选枪（TPUI 对 Sinden 有硬编码友好名
   "Sinden Lightgun Black/Blue/Red/Player 2"，见 JoystickControlRawInput.cs L146-152），
   再逐功能按枪键绑定（屏内/屏外状态区分见 §3.4）。
5. **常见坑**（均出自 sindenwiki TeknoParrot 页）：Windows 缩放必须 100%（否则瞄准偏移/
   准星只能在角落活动）；部分游戏 144Hz 显示器无法全屏（降 60Hz）；换 USB 口/固件升级后
   需在 Controller Setup 里把 Lightgun 设备切走再切回，或删 `UserProfiles\<游戏>.xml` 重建。

---

## 6. 双人双枪的实现方式

**TeknoParrot 原生**：

- 前提：Input API=RawInput。RawInput 报文带设备句柄/路径，TP 按设备路径区分
  P1LightGun/P2LightGun 绑定（InputListenerRawInput.cs 中 `btn.RawInputButton.DevicePath == path` 匹配），
  两只绝对鼠标类光枪可同时独立工作；官方上限 2 枪。
- 设备下拉中 "Windows Mouse Cursor" 伪设备只能给一个玩家用（系统光标只有一个）。
- 多只**相对模式**鼠标会共享光标，无法双枪——双枪必须是绝对模式设备。

**DemulShooter**：

- 设计目标即双枪/四枪（"up to 4 lightguns"）：GUI 的 P1–P4 Config 各选一只 RawInput 设备，
  按设备路径独立读取、独立校枪（Act Labs 偏移、模拟量程都是 per-player），
  注入时分别写 P1/P2 内存槽位（如 GSEVO 的 `_P1_* / _P2_*` 两套地址）。
- 相对设备（普通鼠标）同样不能用于多玩家（wiki 明确：所有相对设备共享 Windows 光标）。

**对我们的含义**：手机双枪 = 两部手机 → 桥接层必须呈现为**两个独立的绝对模式 HID 设备**
（各自稳定的设备路径/实例 ID），这是路线 (b) 的核心要求；SendInput 系统光标方案（a）
原生只能单枪。

---

## 7. 三条接入路线对比与推荐

手机侧输出已定：120Hz UDP 上报 1920×1080 规范坐标 + 扳机/按键事件。差别在 PC 桥接层形态。

### 路线 a：桥接成系统绝对鼠标（SendInput）→ TP 原生鼠标光枪

- 做法：PC 端小程序收 UDP，`SendInput(MOUSEEVENTF_ABSOLUTE|MOUSEEVENTF_VIRTUALDESK)`
  移系统光标，扳机发左键（屏外发右键实现换弹）。TP 选 RawInput + "Windows Mouse Cursor" 设备。
- 优点：**纯用户态、零驱动、一天能出 demo**；TP/DemulShooter 都天然支持
  （DS 对相对设备也是 GetCursorPos 取光标）。
- 缺点：独占系统光标（打游戏时鼠标被抢）；**原生只能单枪**；光标移动走 Windows 指针链路，
  可能受"提高指针精确度"加速度影响（需关）并多一跳延迟；8 位量化问题同其他原生方案。

### 路线 b：桥接成虚拟 HID 设备（驱动级绝对指针）【推荐】

- 做法：PC 端装虚拟 HID 驱动（如 [ViGEmBus](https://github.com/nefarius/ViGEmBus) 思路的
  KMDF 总线驱动，或 HidHide 作者系的 hidclass 方案；描述符声明为
  "Digitizer/Absolute Mouse"，X/Y 逻辑量程 0–32767），桥接程序把 UDP 坐标写进虚拟设备；
  每只手机枪一个设备实例（稳定设备路径），扳机/侧键作为按钮 1/2/3。
- 优点：**对 TP 和 DemulShooter 都呈现为"标准光枪"**（与 AimTrak/Sinden 完全同类），
  双枪、per-gun 绑定、屏外右键换弹、DS 校枪与后坐力输出全部免费获得；
  不抢系统光标；绝对坐标 15/16 位量程，精度远超 8 位枪轴；
  延迟链路最短（UDP→驱动→RawInput，无指针加速）。
- 缺点：需要驱动签名（开发期测试签名/HCK 或买 EV 证书，是唯一硬性成本）；
  驱动崩溃面比用户态大。

### 路线 c：对接 / 扩展 DemulShooter 输入层

- 做法：fork DemulShooter，新增 "Network/UDP device" 设备源（仿 `RawInputController`，
  收 UDP 包填 `Computed_X/Y` + Hid_Buttons），GUI 设备列表注册为可选设备；
  或直接改写其 MMF/TCP 层。对 PBX 这类 Unity 游戏，也可不 fork——
  写小程序直接按 §4.2(b) 协议向其 BepInEx 插件的 33610 端口推 `TcpInputData`。
- 优点：少一层驱动；可顺手做 120Hz 平滑/预测、per-game 坐标变换；
  直接复用 DS 的全部注入与输出生态。
- 缺点：DS **只覆盖其 rom 表内的游戏**（Vampire Night、BBH Pro 不在）；
  需长期维护 fork 跟随上游；TP 原生已覆盖的游戏等于重复造轮子。
- 定位：作为**路线 b 的补充**——先 b 落地，个别 DS 体验明显更好的游戏（HM、GSEVO 要输出时）
  走原版 DS 读我们的虚拟 HID 即可，多数情况连 fork 都不需要。

### 推荐组合

1. **POC（1 周）**：路线 a——SendInput 光标桥 + TP 原生 RawInput（Windows Mouse Cursor），
   单枪跑通 GSEVO / Haunted Museum / PBX。
2. **正式版**：路线 b——虚拟绝对 HID ×2，TP 原生为主；对需要后坐力/输出的游戏
   （Aliens Extermination、GSEVO）叠加原版 DemulShooter（它直接读我们的虚拟枪），
   桥接程序另连 DS 的 8000 端口把 Recoil 回推手机震动。
3. 路线 c 仅在前两者遇到具体游戏壁垒时再考虑；PBX 的"TCP 直推插件"技巧可留作备胎。

---

## 8. 可操作配置示例

**Haunted Museum（TP 原生，profile 关键字段）**——`TeknoParrotUi.Common/GameProfiles/HauntedMuseum.xml`：

```xml
<EmulationProfile>HauntedMuseum</EmulationProfile>
<InvertedMouseAxis>true</InvertedMouseAxis>
<GunGame>true</GunGame>
<xAxisMin>0</xAxisMin><xAxisMax>255</xAxisMax>
<yAxisMin>0</yAxisMin><yAxisMax>255</yAxisMax>
<!-- ConfigValues 中：Input API=RawInput；Windowed=0；
     Use Relative Input=0（打绝对枪时保持关，开了会变成相对移动模式）；
     Custom Crosshairs → Enable=1 可显示 TP 内置准星 -->
```

**Ghost Squad Evolution 用 DemulShooter（双枪 + 输出）**：

```bat
:: 先启动 TeknoParrot 游戏，再启动 DemulShooter（顺序反了可能 hook 不上，见 LaunchBox 论坛帖）
DemulShooter.exe -target=lindbergh -rom=gsquad -v
:: GUI 里 P1/P2 Config 各选一只枪；Outputs 页选 Window Message(MameHooker) 或 Network(127.0.0.1:8000)
```

**Point Blank X 用 DemulShooter（Unity/BepInEx 路线）**：

```bat
:: 1) DemulShooter_GUI → Unity Plugin Installation，给 PBX 装插件
:: 2) 启动游戏后：
DemulShooter.exe -target=es4 -rom=pblankx
:: TP profile 里同时可用内置 Border（把 Border.png 放游戏目录）给手机摄像头取景
```

**AHK 一键启动（Sinden wiki 示例模式）**：

```ahk
Run, D:\Teknoparrot\TeknoParrotUi.exe --profile=AliensExtermination.xml
Sleep, 8000
Run, D:\DemulShooter\DemulShooter.exe -target=globalvr -rom=aliens, , Hide
~Esc::
    Process,Close,TeknoParrotUi.exe
    Process,Close,DemulShooter.exe
    ExitApp
Return
```

**手机光枪桥接层的坐标约定（建议）**：

- 坐标：以游戏画面左上角为 (0,0)，归一化 float 或直接映射到虚拟 HID 逻辑量程 0–32767；
  桥接层负责"游戏窗口 ≠ 整个屏幕"时的画面区域换算（TP 全屏时即屏幕全幅，窗口化时读游戏窗口矩形）。
- 扳机状态机：屏内按下→按钮 1（左键语义）；屏外按下→按钮 2（右键语义=换弹），
  与 Sinden/AimTrak 固件行为对齐，TP/DS 双侧都不用特殊配置。
- 双枪：两部手机各绑一个虚拟 HID 实例，设备路径里编码枪号（P1/P2 固定不漂移）。

---

## 附：参考来源汇总

源码（本文引用的文件均来自以下仓库 master 分支，2026-10 检索）：

- TeknoParrotUI：<https://github.com/teknogods/TeknoParrotUI>
  - `TeknoParrotUi.Common/GameProfile.cs`（InputApi 枚举、GunGame/InvertedMouseAxis/xAxisMin 等字段、HighResolutionAxis 白名单）
  - `TeknoParrotUi.Common/InputListening/InputListenerRawInput.cs`（HandleRawInputGun 坐标映射、Windows Mouse Cursor 伪设备、鼠标五键映射）
  - `TeknoParrotUi.Common/InputListening/InputListenerDirectInput.cs`（DI 模式 GunGame 轴换算、Use16BitAnalog）
  - `TeknoParrotUi.Common/Jvs/JvsHelper.cs`（`TeknoParrot_JvsState` 共享内存）
  - `TeknoParrotUi.Common/InputCode.cs`（AnalogBytes/按键状态）
  - `TeknoParrotUi.Common/Pipes/ControlSender.cs`、`AliensExterminationPipe.cs`（每 15ms 按键位图管道）
  - `TeknoParrotUi.Common/GameProfiles/{HauntedMuseum,AliensExtermination,PointBlankX,BBHPro,HOTD4ELF2}.xml`
  - `TeknoParrotUi/Helpers/JoystickControlRawInput.cs`（Sinden 设备友好名）
- OpenParrot：<https://github.com/teknogods/OpenParrot>
  - `OpenParrot/src/Functions/Ring_amLib/amJvs.cpp`（游戏侧读 `TeknoParrot_JvsState`、JVS 串口仿真、ffbOffset 指针绑定）
  - `OpenParrot/src/Functions/Games/TypeX2/HauntedMuseumInputMisc.cpp`（HM 游戏内输入注入）
  - `OpenParrot/src/Functions/D3D9Misc.cpp`（准星渲染中枪轴→像素换算）
- DemulShooter：<https://github.com/argonlefou/DemulShooter> 及其 wiki <https://github.com/argonlefou/DemulShooter/wiki>
  - `DemulShooter/DemulShooterWindow.cs`（`-ipcinputs/-ipcoutputs` 解析、MMF 创建、RawInput 注册、GetCursorPos 回退、ScreenScale）
  - `DemulShooter/Games/Game_LindberghGsquadEvo.cs`（linuxloader 注入、代码洞、后坐力截获）
  - `DemulShooter/Games/Game__Unity.cs` + `DsCore/IPC/DsTcp_*.cs`（Unity TCP 客户端，33610）
  - `UnityPlugins/UnityPlugin_BepInEx_PBX/`（PBX 插件、TcpInputData 协议字段）
  - `DsCore/IPC/MMF_DataStruct.cs`、`DsCore/RawInput/RawInputController.cs`（AimTrak 按键约定、Wiimote 特例）
  - wiki：Usage（target/rom 全表）、Configuration（设备与校准）、Outputs（MameHooker/网络输出）、Taito-Type-X（HM 命令）、Lindbergh-Ghost-Squad-Evolution（按键表）

文档与社区：

- TeknoParrot 官方 Get Started（RawInput 光枪、双枪上限）：<https://teknoparrot.com/wiki/getstarted.html>
- Sinden wiki · TeknoParrot（逐游戏设置、ReShade 边框、DPI/刷新率坑）：<https://www.sindenwiki.org/wiki/TeknoParrot>
- Sinden wiki · DemulShooter：<https://www.sindenwiki.org/wiki/Demulshooter>
- Sinden wiki · Reshade（Border.fx 白边框）：<https://www.sindenwiki.org/wiki/Reshade>
- BYOAC · DemulShooter 长帖（argonlefou 关于 TP 原生 vs DS 的原话）：<http://forum.arcadecontrols.com/index.php/topic,149714.0.html>
- LaunchBox 论坛（DS 须在 TP 之后启动才能 hook）：<https://forums.launchbox-app.com/topic/93521-demulshooter-auto-launcher/>
- Gamoover（Aliens Extermination 后坐力配置）：<https://www.gamoover.net/Forums/index.php?topic=43649.80>
- RetroBat wiki · TeknoParrot 光枪映射（mouseleft/mouseright 约定）：<https://wiki.retrobat.org/controllers/specific_mapping/teknoparrot-controller-mapping>
- 本仓库前置文档：`docs/teknoparrot-android/04-光枪输入与电视产品形态.md`（光枪硬件方案、延迟链路）、`05-本地TeknoParrot安装取证分析.md`（本地安装结构、Play!/VPNGAME=吸血鬼之夜、GSEVO profile 与 coin.ini 校枪量程取证）
