# 05 - 本地 TeknoParrot 安装取证分析

> 调查对象：`D:\yinwu\1846`（TeknoParrot 1.0.0.1846 本体）+ `D:\yinwu\` 下 27 个游戏目录。
> 本报告基于对本地文件的直接取证（文件清单、配置字段、二进制格式验证），是与网络调研（文档 01）交叉验证的一手数据。

## 目录

- [1. 安装根目录组件分析](#1-安装根目录-dyinwu1846-组件分析)
- [2. GameProfiles 目录](#2-gameprofiles-目录)
- [3. 根目录 teknoparrot.ini](#3-根目录-teknoparrotini)
- [4. 游戏目录分析](#4-游戏目录分析)
- [5. 结论：整体工作流与组件-基板对应关系](#5-结论整体工作流与组件-基板对应关系)

---

## 1. 安装根目录 `D:\yinwu\1846` 组件分析

### 1.1 OpenParrotWin32 / OpenParrotx64（开源注入器，针对 Taito Type X / NESiCAxLive 等）

`OpenParrotWin32\`（32 位）：

| 文件 | 大小 | 作用 |
|---|---|---|
| `OpenParrot.dll` | 912,896 B | 核心 Hook DLL，注入游戏进程后模拟街机 I/O、JVS 输入、解除区域/加密狗检查 |
| `OpenParrotLoader.exe` | 161,280 B | 启动器：以挂起方式拉起游戏 exe，注入 OpenParrot.dll |
| `OpenParrotKonamiLoader.exe` | 136,704 B | 专用于 Konami PC 街机（e-Amusement 系）的加载器 |
| `iDmacDrv32.dll` | 75,776 B | 模拟 Initial D 系列读卡器（iDmac = Initial D Magnetic Card 驱动） |
| `bngrw.dll` | 260,608 B | Bandai Namco 系游戏的读卡/数据读写（BnG R/W）模拟 |

`OpenParrotx64\`：同构 64 位版 — `OpenParrot64.dll`（1,188,864 B）、`OpenParrotLoader64.exe`、`iDmacDrv64.dll`、`bngrw.dll`；注意 x64 下**没有** KonamiLoader。

机制：Loader 创建挂起的游戏进程 → 注入对应位数的 OpenParrot DLL → DLL 内部按 `EmulationProfile` 名选择补丁集，Hook DirectInput/窗口创建/JVS 串口/读卡器 API，并直接 patch 游戏代码跳过加密狗与联网验证。

### 1.2 TeknoParrotUi.exe 与 TeknoParrot\ 目录

- `TeknoParrotUi.exe`（11,744,256 B）— .NET Framework 4.6.2 的 WPF 前端；依赖 `libs\` 下的 SharpDX（DirectInput/XInput/RawInput）、RawInput.Sharp、Newtonsoft.Json、CefSharp（内嵌浏览器）、discord-rpc 等。
- `TeknoParrot\` — 闭源核心（"TeknoBudgie"线），用于 Sega Lindbergh、Gaelco、各种杂牌 PC 街机：

| 文件 | 大小 | 作用 |
|---|---|---|
| `TeknoParrot.dll` / `TeknoParrot64.dll` | 55MB / 52.9MB | 巨型 Hook/补丁 DLL，内含各游戏的硬编码补丁 |
| `BudgieLoader.exe` | 11.5MB | Lindbergh ELF 游戏的加载器 |
| `TeknoDraw.dll` / `TeknoDraw64.dll` | 19.9MB / 42.6MB | 渲染层（Direct3D 替换/包装） |
| `ScoreSubmission.dll` / `...64.dll` | 75.8MB / 87.1MB | 在线排行榜提交 |
| `Opensegaapi.dll` | — | 模拟 Sega 网络 API |
| `OpenSndGaelco.dll` / `OpenSndVoyager.dll` | — | 模拟 Gaelco 声音硬件 / Star Trek Voyager 声音 |
| `msys-2.0.dll` 等 | — | MSYS2 运行时（BudgieLoader 用它跑 Linux ELF） |
| `avcodec-58.dll`、`avutil-56.dll`、`swresample-3.dll` | — | FFmpeg 音视频解码 |
| `cg.dll`/`cgGL.dll`、`regal32.dll` | — | NVIDIA Cg / OpenGL 兼容层 |
| `SDL2.dll`、`openal32.dll`、`FAudio.dll`、`XAudio2_9.dll` | — | 音频兼容栈 |

### 1.3 Play\ 目录（PS2 基板）

`Play.exe`（4.8MB）+ Qt5 + `arcadedefs\`（66 个 `.arcadedef` JSON）。这是 **Play!**（PS2 模拟器）的街机改版，用于 Namco System 147/246/256（PS2 兼容基板）。示例 `akaievo.arcadedef`：`"id": "akaievo", "name": "Animal Kaiser Evolution", "driver": "sys147"`。HookedWindows.txt 中 `Play! - [ VPNGAME/TC3LOAD/TC4LOAD/CBRLOAD ]` 对应 Time Crisis 3/4、Vampire Night、Cobra 等。

### 1.4 ElfLdr2\ 目录（Sega Lindbergh Linux ELF 加载器，新一代）

- `BudgieLoader.exe`（10.7MB）+ `TeknoParrot.dll`（48.5MB，与 TeknoParrot\ 下的不同构建）
- `msys-2.0.dll`（MSYS2）— 在 Windows 上提供 POSIX/Linux 系统调用兼容，直接加载 Lindbergh 的 Linux ELF
- `libs\` — 一整套 Linux 共享库的 Windows 移植：`.so` 文件如 `libavcodec.so.51`、`libboost_*`、`libPhysXCore.so`、`libfmod.so.7`、`libcrypto.so.1.0.0`、`libGLU.so.1`、`libarcaderegistry.so.1` 等 40+ 个
- `hints.dat`（56,640 B）— 二进制数据，推测是 syscall/函数 hint 表
- `env\dev\` — 伪设备文件 `fd`、`stdin`、`stdout`、`stderr`（模拟 Linux `/dev`）

### 1.5 CrediarDolphin\（Triforce/GameCube/Wii）

crediar 的 Dolphin 改版：`Dolphin.exe`（20.7MB）+ `DolphinNoGUI.exe` + Qt6；`Sys\` 下有 **`Triforce\` 专用目录**、GameSettings、Shaders、`totaldb.dsy`（Triforce 游戏数据库）。用于 Mario Kart Arcade GP 1/2、F-Zero AX 等 Triforce 基板游戏（9 个 profile 用 `EmulatorType=Dolphin`）。

### 1.6 RPCS3\（PS3 基板 System 357/369）

`rpcs3.exe`（61MB）+ Qt6 + ffmpeg/OpenCV。有 6 个**按游戏隔离的虚拟文件系统目录**，各含 `dev_hdd0/dev_bdvd/dev_usb000`：
`akb48`、`darkescape4d`（Dark Escape 4D）、`dbzenkai`（龙珠 Zenkai Battle）、`RazingStorm`、`taikogreen`（太鼓达人绿色版）、`ttt2u`（铁拳 TT2U）。19 个 profile 用 `EmulatorType=RPCS3`。

### 1.7 N2\ 目录

精简版 Budgie 环境：`BudgieLoader.exe`（5.7MB）+ `TeknoParrot.dll`（28.7MB）+ MSYS 运行时 + AMD 显卡着色器修复 `cgGL.dll`。唯一使用它的 profile 是 `CSNEO.xml`（Counter-Strike NEO）：`ExecutableName=hlds_amd`、`msysType=3` —— 即跑 Linux 版 Half-Life Dedicated Server 二进制，对应 Namco N2（Linux PC）基板。

### 1.8 数据类目录

- `GameProfiles\` — **475 个 XML**（详见第 2 节）
- `GameSetup\` — 383 个 XML，极简，供 UI"设置游戏路径"向导用，如 `AfterDark2.xml`：`<GameExecutableLocation>qumo2_en.exe</GameExecutableLocation><DevOnly>false</DevOnly>`
- `Metadata\` — 473 个 JSON。示例 `GSEVO.json`：`{"game_name":"Ghost Squad Evolution","game_genre":"Shooter","platform":"SEGA Lindbergh Red","release_year":"2007","nvidia":"WITH_FIX","amd":"WITH_FIX","intel":"NO_INFO",...}` —— UI 展示 + 各 GPU 厂商兼容性标注
- `Icons\` — 364 个 PNG；`MD5\` — 40 个 `.md5` 校验清单（验证游戏 dump 完整性）
- `deps\` — 仅 `vcredist_2008_sp1_x86.exe`
- `FFBBlaster\x64/x86` — `FFBBlaster(64).dll` + SDL2/SDL3，力反馈输出（方向盘/枪械后坐力）
- `UserProfiles\` — 27 个 XML，即用户实际配置过的游戏的 profile **副本**，写入 `GamePath` 和用户选择的 FieldValue（如 `UserProfiles\GSEVO.xml` 的 `GamePath = D:\yinwu\Ghost Squad Evolution\Ghost Squad Evolution\vsg_l\vsg`）

### 1.9 HookedWindows.txt

118 行窗口标题名单，用于匹配/Hook 游戏窗口（设窗口大小、隐藏鼠标、置顶等）。分三类：原生窗口名（`gamewin`、`FREEGLUT`、`GLUT`、`SDL_app`、`UnrealEngine3`、`HoD4(LINDBERG)`）、TeknoParrot 自建窗口（`TeknoParrot - xxx`、`TeknoBudgie - xxx`、`OpenParrot - xxx`）、模拟器窗口（`Play! - [TC4LOAD]`、`RPCS3 via TeknoParrot`）。

### 1.10 ParrotData.xml（全局设置）摘要

- 热键：`ExitGameKey=0x1B`（ESC）、`PauseGameKey=0x13`、`ScoreCollapseGUIKey=0x79`（F10）
- 网络：`Elfldr2NetworkAdapterName=Radmin VPN`（Lindbergh 联机走 Radmin VPN 虚拟网卡）
- 其它：`FirstTimeSetupComplete=true`、`Language=zh-CN`、UI 主题/Discord RPC 开关等

### 1.11 ParrotPatcher（自动更新器）

`ParrotPatcher.exe`（313KB，.NET）。日志显示 2025/12/16 从 1.0.0.1815 升级到 1846：等 UI 关闭 → 解压更新包 → 逐文件解出 FFBBlaster、TeknoParrot、GameProfiles、Icons、Metadata → 最后更新 UI 与自身 → 重启 UI。

---

## 2. GameProfiles 目录

- **总数：475 个 XML**。
- EmulatorType 分布：

| EmulatorType | 数量 | 对应基板/加载链 |
|---|---|---|
| TeknoParrot | 217 | 闭源 DLL 注入，各种 PC 街机（Global VR、Raw Thrills、ES3、RingEdge 部分） |
| OpenParrot | 102 | 开源 DLL 注入，Taito Type X/X2/X3/X4、NESiCAxLive |
| ElfLdr2 | 72 | Lindbergh Linux ELF（新加载链） |
| Play | 36 | Namco System 147/246/256（PS2 基板） |
| RPCS3 | 19 | PS3 System 357/369 |
| Lindbergh | 17 | Lindbergh（旧加载链 BudgieLoader） |
| Dolphin | 9 | Triforce/GameCube |
| SegaTools | 2 | Initial D Zero |
| N2 | 1 | CS NEO（Linux HLDS） |

- **84 个 profile 标记 `<Patreon>true</Patreon>`**（付费支持者专属）。

### 代表 profile：`GameProfiles\GSEVO.xml`（Ghost Squad Evolution，revision 17）

| 字段 | 值 | 含义 |
|---|---|---|
| `GamePath` | （空） | 用户设定的游戏路径，写入 UserProfiles 副本 |
| `TestMenuParameter` | `-t` | 进测试菜单的命令行参数 |
| `EmulationProfile` | `GSEVO` | **关键字段**：TeknoParrot.dll 内部补丁集的代号 |
| `GameProfileRevision` | 17 | profile schema 版本号 |
| `HasSeparateTestMode` | true | UI 显示"进测试菜单"按钮 |
| `EmulatorType` | `Lindbergh` | 决定走哪条加载链（BudgieLoader） |
| `ExecutableName` | `vsg` | 游戏主程序文件名（此处是 Linux ELF） |
| `msysType` | 3 | MSYS 运行时变体选择 |
| `InvertedMouseAxis` / `GunGame` | true / true | 鼠标 Y 轴反转；光枪游戏（启用鼠标→枪坐标映射） |
| `xAxisMin/Max=2/218`、`yAxisMin/Max=1/236` | — | 光枪坐标原始量程，映射到屏幕用 |
| `ConfigValues/FieldInformation` | — | UI 设置页生成器：`CategoryName`/`FieldName`/`FieldValue`/`FieldType`（Dropdown/Bool/Slider）/`FieldOptions`/`FieldMin/Max`/`Hint`。GSEVO 含 Input API（RawInput）、DongleRegion/PcbRegion（JAPAN/USA/EXPORT，模拟加密狗与主板区域）、FreePlay、Windowed、EnableAmdFix、VgaMode、HideCursor、FFB Blaster、相对输入灵敏度 1–50 等 |
| `JoystickButtons` | — | 按键映射表：`ButtonName`、`InputMapping`（如 `P1LightGun`、`Analog0`、`P1RelativeUp`）、`AnalogType`、`HideWithDirectInput/XInput/RawInput/RelativeAxis` |

对比 `PointBlankX.xml`（rev 9）：`EmulatorType=TeknoParrot`、`ExecutableName=PBX100-2-NA-MPR0-A63.exe`、`Is64Bit=false`，ConfigValues 多了 `Render Hook`（EndScene/Present，D3D9 hook 点选择）、Crosshair（放 `P1.png/P2.png` 到游戏目录）等字段。

另有 `GSEVOELF2.xml`：同一游戏但 `EmulatorType=ElfLdr2` —— 同一 dump 可走新旧两条 Lindbergh 加载链。

---

## 3. 根目录 `teknoparrot.ini`

```ini
[GlobalHotkeys]
ExitKey=0x1B      ; ESC 退出游戏
PauseKey=0x13     ; Pause 键暂停
[General]
Input API=DirectInput   ; 全局默认输入 API
Windowed=1              ; 全局窗口化
```

每个游戏运行时还会在游戏目录写自己的 `teknoparrot.ini`（见下节）。

---

## 4. 游戏目录分析

### 4.1 `D:\yinwu\After Dark\`（Night Hunter: After Dark Chapter II，Virtools 引擎球幕游戏）

- `GamePlayer.exe`（4,814,336 B）+ **`GamePlayer.exe.original`（同字节数）**：两者从第 367 字节起不同 —— 大小不变、就地 patch，`.original` 是补丁前备份。
- 其余：`1.vmo`/`main.vmo`（Virtools 场景文件）、`CoFramework.dll`、`Data/Module/Res/Scene/Sounds/Textures/`。

游戏侧 `teknoparrot.ini`：

```ini
[GlobalHotkeys]
ExitKey=0x1B
PauseKey=0x13
[General]
Input API=RawInput
Windowed=1
HideCursor=1
Use Relative Input=0
Player 1 Relative Sensitivity=8
Player 2 Relative Sensitivity=8
CustomResolution=0
ResolutionWidth=1920
ResolutionHeight=1080
```

即 profile ConfigValues 序列化成 INI，注入 DLL 启动时读取。

`coin.ini`（游戏自己的数据，被 TeknoParrot 读写以模拟投币/光枪校准）：`[CoinSet] Coin=1`、`[CoinData] Num=1319`、`[Gun1xMin]=286 / [Gun1xMax]=3967 / [Gun1yMin]=1575 / [Gun1yMax]=4092`（P1 枪校准量程）、`[Difficulty] Diff=0`、`[Blood] Level=2`。

`GameConfig`（Virtools 引擎配置）：`FileName=main.vmo`、`StereoMode=0`、`FOV=6500`、`EyeWide=1600`（球幕参数）、`WindowedWidth=1920`。

对应 profile `AfterDark.xml`：`EmulationProfile=WartranTroopers`（复用同一补丁集）、`ExecutableName=GamePlayer.exe`。

### 4.2 `D:\yinwu\Ghost Squad Evolution\`（Sega Lindbergh Red，Linux 游戏）

- `vsg_l\` — 游戏本体（Linux）：`vsg`（ELF 主程序）、`librnalindbergh_jr.so`/`...so.1.46`、`snd_util.so`、`media\`（`adx`、`aeauth_*.bin` 认证/资源）、`shader\`（pixelshader/vertex shader 文件）
- `drv\openal\` — 音频驱动占位
- `Patches\` — 三个社区修复包：
  - `GSEvo_AMD_shaderfix\`（Nezarn 制作）：复制 shader 文件夹覆盖 + TeknoParrot 中打开 EnableAmdFix
  - `GSEvo_Nvidia_shaderfix\`：同构 Nvidia 版
  - `GSEvo_OpenAL_1.1_Windows\oalinst.exe`

此处 TeknoParrot 不 patch exe，而是 BudgieLoader 通过 MSYS2 直接把 `vsg` ELF 当 Linux 程序加载，TeknoParrot.dll 提供 Lindbergh 系统库（`.so`）的 Windows 实现 + I/O 模拟。

### 4.3 `D:\yinwu\Point Blank X\`（Namco ES3 系，原生 Windows）

- `PBX100-2-NA-MPR0-A63.exe`（16.1MB，文件名即 ROM 版本号）
- `PBX100-2-NA-MPR0-A63_Data\` — 522 个关卡/数据文件夹
- `init.ps1` — **街机系统启动脚本**（机台 Windows 的 init）：对各盘跑 `chkdsk`、脏盘置位重启、`Set-NetworkConfig`、死循环 `Start-Application`（看门狗）。TeknoParrot 接管后跳过此脚本直接拉起游戏 exe
- `sernum_regist_32.exe`（机台序列号注册）

对应 profile `EmulatorType=TeknoParrot` —— 用 `TeknoParrot.dll` 注入这个原生 x86 exe。

---

## 5. 结论：整体工作流与组件-基板对应关系

**工作流**（以一次启动为例）：

1. `TeknoParrotUi.exe`（WPF）列出游戏：GameProfiles（475，定义补丁集/输入/设置项）+ Metadata（473，展示名/平台/年份/GPU 兼容性）+ Icons + GameSetup（路径设置向导）
2. 用户选定游戏路径后，配置写入 `UserProfiles\<Game>.xml`，并序列化成游戏目录下的 `teknoparrot.ini`
3. 在线更新由 `ParrotPatcher.exe` 完成
4. 按 `EmulatorType` 分流启动（见第 2 节表格）
5. 游戏侧"接管"手段：
   - exe 就地 patch（备份 `.original`，大小不变仅改导入/校验字节，如 After Dark）
   - 注入 DLL 模拟 JVS I/O / 加密狗 / 读卡器 / 网络认证（aeauth、ALL.Net）
   - 用 teknoparrot.ini / coin.ini 注入设置与投币计数
   - 跳过机台启动脚本（init.ps1）
   - 社区 shader/OpenAL 修复包解决现代 GPU 兼容

**一句话总结**：TeknoParrot 不是传统 CPU 模拟器——对基于 x86 PC 的基板（Type X、Lindbergh、ES3、RingEdge 等）它是"加载器 + 补丁器 + I/O/加密狗/网络兼容层"；只有非 x86 基板（Triforce、PS3 357、System 246）才外包给真正的模拟器（Dolphin、RPCS3、Play!）并在其窗口上做统一 Hook。
