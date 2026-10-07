# TeknoParrot / OpenParrot 实现原理深度调研

> 调研日期：2026 年。目标读者：资深工程师。
> 本文所有结论均基于公开源码（teknogods 组织 GitHub 仓库）、官方 Wiki 与本地安装实例（`D:\yinwu\1846\`）交叉验证。
> 主要信息来源 URL 汇总于文末「参考资料」一节；正文中以脚注式链接给出。

---

## 目录

1. [TeknoParrot 到底是什么：兼容层而非模拟器](#1-teknoparrot-到底是什么兼容层而非模拟器)
2. [仓库与组件全景](#2-仓库与组件全景)
3. [整体架构与启动链路](#3-整体架构与启动链路)
   - 3.1 [启动流程总览](#31-启动流程总览)
   - 3.2 [DLL 注入：OpenParrotLoader 与入口点劫持](#32-dll-注入openparrotloader-与入口点劫持)
   - 3.3 [游戏识别：GameDetect 与 CRC32](#33-游戏识别gamedetect-与-crc32)
   - 3.4 [进程内 Hook 框架：MinHook + injector](#34-进程内-hook-框架minhook--injector)
4. [OpenParrot 分模块实现详解](#4-openparrot-分模块实现详解)
   - 4.1 [JVS I/O 板模拟](#41-jvs-io-板模拟)
   - 4.2 [Fast I/O（iDmacDrv）模拟](#42-fast-ioidmacdrv模拟)
   - 4.3 [显卡 / 分辨率补丁与 Direct3D Hook](#43-显卡--分辨率补丁与-direct3d-hook)
   - 4.4 [网络模拟：NESYS / ALL.Net / 局域网联机](#44-网络模拟nesys--allnet--局域网联机)
   - 4.5 [读卡器模拟：RFID / BanaPass / Aime](#45-读卡器模拟rfid--banapass--aime)
   - 4.6 [加密狗与认证绕过](#46-加密狗与认证绕过)
   - 4.7 [音频子系统](#47-音频子系统)
   - 4.8 [存档 / NVRAM / 文件系统重定向](#48-存档--nvram--文件系统重定向)
   - 4.9 [Lindbergh（Linux 基板）的处理](#49-lindberghlinux-基板的处理)
5. [GameProfile XML 与补丁机制](#5-gameprofile-xml-与补丁机制)
6. [代码许可与法律边界](#6-代码许可与法律边界)
7. [对「移植到 Android」的启示](#7-对移植到-android的启示)
8. [附录 A：JVS 帧格式速查](#附录-ajvs-帧格式速查对应-jvspacketscs--jvspackageemulatorcs)
9. [附录 B：teknoparrot.ini 实例](#附录-bteknoparrotini-实例本地-dyinwu-实证)
10. [附录 C：构建与逆向工作流](#附录-c构建与逆向工作流openparrot-readme-官方流程)
11. [参考资料](#8-参考资料)

---

## 1. TeknoParrot 到底是什么：兼容层而非模拟器

TeknoParrot 自称"街机模拟器"，但从技术分类上讲它是一个 **兼容层（compatibility layer）/ 加载器（loader）**，与 Wine 之于 Windows 程序的关系类似：

- 目标游戏是 **原生 x86/x64 Windows PE 可执行文件**（Taito Type X/X2/X3/X4、Sega RingEdge/RingWide/Europa-R、Namco ES1/ES3、ex** Board**、Raw Thrills 等 PC 架构街机基板），CPU 指令无需翻译，直接由宿主机执行。
- 街机基板与家用 PC 的差异集中在 **周边硬件与系统服务** 上：JVS/Fast I/O 输入输出板、加密狗（dongle/keychip）、读卡器（RFID/BanaPass）、专用音频板、官方网络服务（NESYS/ALL.Net）、写死的分辨率/GPU 型号、专用数据盘盘符等。
- TeknoParrot 的全部工作就是：**把游戏注入一个自己编写的 DLL（OpenParrot.dll / TeknoParrot.dll），在进程内 Hook Windows API 和游戏私有 DLL，伪造上述硬件/服务的应答，并按需对游戏内存打补丁**。

Emulation General Wiki 对 TeknoParrot 的定义也是 "a closed-source compatibility layer for Windows PCs to run games originally made for Windows-based and some Linux-based arcade hardware"（[gametechwiki.com](https://emulation.gametechwiki.com/index.php/TeknoParrot)）。其开源核心 OpenParrot 的 README 自述为 "Open Source version of TeknoParrot by NTA, avail and Reaver. Works in collaboration with TeknoParrotUI"（[teknogods/OpenParrot](https://github.com/teknogods/OpenParrot)）。

---

## 2. 仓库与组件全景

teknogods 组织（[github.com/teknogods](https://github.com/orgs/teknogods/repositories)）下的关键仓库：

| 仓库 | 角色 | 许可 |
|---|---|---|
| [teknogods/OpenParrot](https://github.com/teknogods/OpenParrot) | 开源注入 DLL 核心（C++，进程内 Hook/模拟） | GPL-3.0（仓库 `LICENSE` 为 GPLv3 全文，经 GitHub License API 确认） |
| [teknogods/TeknoParrotUI](https://github.com/teknogods/TeknoParrotUI) | WPF 前端 + GameProfile/GameSetup 数据 + JVS 协议模拟器 + 各游戏 Pipe 协议 | GPL-3.0 |
| [teknogods/OpenSegaAPI](https://github.com/teknogods/OpenSegaAPI) | Sega 网络 API 模拟（ALL.Net 系） | 开源 |
| [teknogods/OpenSndVoyager](https://github.com/teknogods/OpenSndVoyager) / [OpenSndGaelco](https://github.com/teknogods/OpenSndGaelco) | 独立音频板模拟 | 开源 |
| [teknogods/JVSClient](https://github.com/teknogods/JVSClient) / [TeknoFfb](https://github.com/teknogods/TeknoFfb) | JVS 硬件对接 / 力反馈 | 开源 |
| [teknogods/DemulShooter](https://github.com/teknogods/DemulShooter) | 光枪输出对接 | 开源 |
| [teknogods/regal](https://github.com/teknogods/regal) | OpenGL 兼容层（老固定管线 GL 到现代驱动） | 开源 |

第三方关键项目：

- [lindbergh-loader/lindbergh-loader](https://github.com/lindbergh-loader/lindbergh-loader)：SEGA Lindbergh（Linux 基板）的开源加载器，CC BY-NC-SA 4.0，是理解 Lindbergh 处理思路的最佳公开材料。
- [segatools](https://gitea.endland.dev/Donnovan/segatools) 系（TeknoParrotUI 中集成于 `SegaTools/inject.exe` + `idzhook.dll`，见 `Library.xaml.cs` 启动器选择代码）：SEGA ALLS/初音/头文字D 系列的社区 Hook 方案。

本地安装实例（`D:\yinwu\1846\`，即 TeknoParrot 1.x build 1846 的发布包）的目录结构可以直接印证组件划分：

```
1846/
├── OpenParrotWin32/   OpenParrot.dll + OpenParrotLoader.exe + OpenParrotKonamiLoader.exe + iDmacDrv32.dll + bngrw.dll
├── OpenParrotx64/     （64 位对应物）
├── TeknoParrot/       TeknoParrot.dll / TeknoParrot64.dll（闭源核心）、BudgieLoader.exe、
│                      Opensegaapi.dll、OpenSndGaelco.dll、OpenSndVoyager.dll、regal32.dll、
│                      TeknoDraw.dll/TeknoDraw64.dll、ScoreSubmission.dll、msys-2.0.dll、FAudio.dll、SDL2.dll …
├── ElfLdr2/           BudgieLoader.exe + TeknoParrot.dll + msys-2.0.dll + libs/ + env/
├── N2/                BudgieLoader.exe（Namco N2 基板用）
├── GameProfiles/ GameSetup/ Icons/ Metadata/ MD5/   （TeknoParrotUI 的数据目录）
├── ParrotPatcher.exe  （自动更新器，源码在 TeknoParrotUI 仓库 ParrotPatcher/）
└── TeknoParrotUi.exe  （WPF 前端）
```

注意 `TeknoParrot/TeknoParrot.dll` 与 `OpenParrotWin32/OpenParrot.dll` 并存：**OpenParrot 是开源子集**，闭源的 `TeknoParrot.dll` 覆盖了更多游戏（尤其 Patreon 先行游戏与涉及敏感认证绕过的部分），两者共享同一套注入器与 ini/pipe 协议。

---

## 3. 整体架构与启动链路

### 3.1 启动流程总览

完整链路（源码位置：`TeknoParrotUi/Views/Library.xaml.cs` → `Views/GameRunning.xaml.cs` → `Views/GameRunningCode/ProcessManagement/GameProcessManager.cs`）：

```
TeknoParrotUi.exe (WPF)
   │  1. 读取 GameProfiles/<Game>.xml，用户在 UI 上配置按键映射与选项
   │  2. ConfigurationWriter.WriteConfigIni() 把 ConfigValues 序列化成
   │     游戏目录下的 teknoparrot.ini（[General]/[Network]/[GlobalHotkeys]… 分节）
   │  3. InputListener（DirectInput/XInput/RawInput）线程开始轮询手柄/键盘/光枪
   │  4. JvsPackageEmulator.Initialize(gameProfile) 按基板类型设定 JVS 参数
   │     （JvsVersion/JvsCommVersion/JvsIdentifier/JvsSwitchCount/Taito/Namco 标志…）
   │  5. Library.ValidateAndRun() 依 EmulatorType 选择 loaderExe / loaderDll：
   │       OpenParrot  → .\OpenParrotWin32\OpenParrotLoader.exe  + OpenParrot.dll
   │                     （64 位：.\OpenParrotx64\OpenParrotLoader64.exe + OpenParrot64.dll）
   │       Lindbergh   → .\TeknoParrot\BudgieLoader.exe
   │       ElfLdr2     → .\ElfLdr2\BudgieLoader.exe（64 位 BudgieLoader_x64.exe）
   │       SegaToolsIDZ→ .\SegaTools\inject.exe + idzhook.dll
   │       Dolphin/RPCS3/Play/cxbx… → 直接调外部模拟器 exe
   │  6. GameProcessManager.CreateGameProcess() 启动：
   │       OpenParrotLoader.exe -d -k OpenParrot.dll <game.exe> [args]
   │  7. 同时建立命名管道服务端 \\.\pipe\TeknoParrotPipe（ControlPipe.cs）与各游戏
   │     专用 Pipe（TeknoParrotUi.Common/Pipes/*.cs，50+ 个），把输入状态流式推给游戏进程
   ▼
游戏进程（被注入 OpenParrot.dll / TeknoParrot.dll）
   │  DllMain → Main_SetSafeInit() 劫持游戏入口点 → RunMain()
   │    → 读取 teknoparrot.ini → GameDetect::DetectCurrentGame()（CRC32 识别游戏）
   │    → InitFunction::RunFunctions(Global) + RunFunctions(当前游戏 ID)
   │      安装全部 MinHook/内存补丁
   ▼
游戏以为自己在真实街机上运行
```

`Library.xaml.cs` 中 loader 选择的真实代码（约 L699–L767，[源文件](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/Library.xaml.cs)）：

```csharp
loaderExe = is64Bit ? ".\\OpenParrotx64\\OpenParrotLoader64.exe"
                    : ".\\OpenParrotWin32\\OpenParrotLoader.exe";
...
case EmulatorType.Lindbergh: loaderExe = ".\\TeknoParrot\\BudgieLoader.exe"; break;
case EmulatorType.ElfLdr2:   loaderExe = ".\\ElfLdr2\\BudgieLoader.exe"; break;
case EmulatorType.OpenParrot:
    loaderDll = is64Bit ? ".\\OpenParrotx64\\OpenParrot64" : ".\\OpenParrotWin32\\OpenParrot";
    break;
case EmulatorType.SegaToolsIDZ:
    loaderExe = ".\\SegaTools\\inject.exe"; loaderDll = "idzhook"; break;
```

### 3.2 DLL 注入：OpenParrotLoader 与入口点劫持

注入动作由外部加载器 `OpenParrotLoader.exe` 完成（该 exe 二进制随发布包分发；TeknoParrotUI 仅以 `ProcessStartInfo(loaderExe, " -d -k {loaderDll}.dll {game.exe}")` 调用它，见 [GameProcessManager.cs L343](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/GameRunningCode/ProcessManagement/GameProcessManager.cs)）。命令行形态与经典的 `inject.exe -d -k dll target.exe`（如 segatools 的 inject）一致，即「创建挂起进程 → 加载 DLL → 恢复执行」。

DLL 侧如何拿到控制权，[OpenParrot/src/dllmain.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/dllmain.cpp) 写得很清楚——不是常规的在 `DllMain` 里直接干活，而是**劫持游戏主模块的入口点（Entry Point）**：

```c
static void Main_SetSafeInit()
{
    HMODULE hModule = GetModuleHandle(NULL);                 // 主模块基址
    PIMAGE_NT_HEADERS ntHeader = ...(hModule + e_lfanew);
    PBYTE ep = (PBYTE)(hModule + ntHeader->OptionalHeader.AddressOfEntryPoint);
    memcpy(originalCode, ep, 20);                            // 备份 EP 处原始 20 字节
    VirtualProtect(ep, 20, PAGE_EXECUTE_READWRITE, &oldProtect);
#if _M_IX86
    ep[0] = 0xE9;                                            // JMP rel32
    *(int*)(ep+1) = (int)Main_DoInit - ((int)ep + 5);
#elif defined(_M_AMD64)
    ep[0]=0x48; ep[1]=0xB8; *(uint64_t*)(ep+2)=(uint64_t)Main_DoInit;
    ep[10]=0xFF; ep[11]=0xE0;                                // movabs rax, Main_DoInit; jmp rax
#endif
    originalEP = ep;
}
```

游戏开始执行时先进入 `Main_DoInit()` → `RunMain()` 完成全部 Hook 安装，然后 `memcpy` 恢复 EP 处原始 20 字节、恢复页保护，再 `jmp originalEP` 把控制权还给游戏原始入口。这样做的好处是：**Hook 一定在游戏主函数运行前装好，且避开在 loader lock 内做重活**。

`dllmain.cpp` 还暴露了若干导出，对应不同宿主加载方式：

- `InitializeASI()`：作为 ASI 插件（如被其他 mod loader 加载）时的入口；
- `PrepareSafeInit()`：给"托管式 loader"用——`TP_LOADER_MANAGED_INIT` 环境变量存在时 `DllMain` 保持惰性，由 loader 在 LoadLibrary 返回后另行调用。源码注释明确写道 *"Wine/Box64 can load us from a remote thread after the game's real entry point has been parked by OpenParrotLoader"*；
- `InitLinux(makeCall)`（x86 专有导出）：供 Lindbergh/ELF 加载路径回调；
- `TP_DIRECTHOOK` / `TP_REMOTETHREAD` / `TP_ENTRYPOINT_REMOTETHREAD_MS` 等环境变量切换注入时序策略。

也就是说注入层同时支持：CreateRemoteThread 加载、`loadlib` 调试加载（README 教开发者用 x64dbg `loadlib OpenParrot`）、托管 loader 三种模式。没有用 SetWindowsHookEx 或 AppInit_DLLs。

### 3.3 游戏识别：GameDetect 与 CRC32

[OpenParrot/src/Utility/GameDetect.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Utility/GameDetect.cpp)：`DetectCurrentGame()` 对**主模块前 0x400 字节计算 CRC32**，然后在一个巨型 `switch` 中映射到 `GameID` 枚举与附加属性（`isNesica`、`NesicaKey`、`X2Type` 等），例如：

```c
crcResult = GetCRC32(moduleBase, 0x400);
switch (crcResult) {
case 0x4bcfbc4f: currentGame = GameID::GrooveCoaster2; isNesica = true; break;
case 0xcb4ab9d6: currentGame = GameID::Nesica; NesicaKey = NesicaKey::USF4; isNesica = true; break;
...
}
```

值得注意的细节：源码中已有 `TP_CRC_NORMALIZE_LAA` 分支，注释为 *"Android may launch a private Large Address Aware copy of an x86 executable so Wine can satisfy the cabinet's large early allocation"*——即在 Android/Wine 场景下先拷贝头部、清掉 PE 头的 `IMAGE_FILE_LARGE_ADDRESS_AWARE` 位再算 CRC，保证修改过的副本仍能匹配原 profile。这是官方 Android 适配正在进行的直接证据（详见第 7 节）。

### 3.4 进程内 Hook 框架：MinHook + injector

OpenParrot 进程内使用两套互补机制（均为仓库内 vendored 依赖，`deps/inc/`）：

1. **[MinHook](https://github.com/TsudaKageyu/minhook)**（`deps/inc/MinHook.h`、`deps/src/hook.c`）：经典的 x86/x64 inline trampoline hook。惯用法是 `MH_CreateHookApi(L"kernel32.dll", "CreateFileA", &Hook_CreateFileA, (void**)&__CreateFileA)` + `MH_EnableHook(MH_ALL_HOOKS)`。
2. **injector 库**（`deps/inc/injector/`，即 mod 圈常用的 `injector.hpp`，源自 GTA modding 生态）：提供 `injector::MakeJMP(addr, target)`、`MakeNOP`、`MakeRET`、`WriteMemory<T>`、`WriteMemoryRaw`、`MemoryFill`、`MakeCALL` 等内存补丁原语，配合 `Utility/Hooking.Patterns.h`（字节模式扫描，可在地址不固定时按 signature 找代码）。

每个功能/游戏的初始化通过 `InitFunction` 注册表统一调度：`static InitFunction initFunc([](){ ... })` 静态对象在 `RunMain()` 中被 `InitFunction::RunFunctions(GameID::Global)`（通用）和 `RunFunctions(currentGame)`（游戏专属）依次触发（[dllmain.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/dllmain.cpp)、[Utility/InitFunction.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Utility/InitFunction.cpp)）。

---

## 4. OpenParrot 分模块实现详解

### 4.1 JVS I/O 板模拟

JVS（JAMMA Video Standard）是街机主板与 I/O 板之间的 RS-485 串口协议。TeknoParrot 的实现分两半：

**UI 侧（TeknoParrotUI，C#）——协议应答端：**

- [TeknoParrotUi.Common/Jvs/JVSPackets.cs](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Jvs/JVSPackets.cs)：JVS 协议常量——同步字节 `SYNC_CODE=0xE0`、广播 `0xFF`、`OP_RESET=0xF0`、`OP_ADDRESS=0xF1`、功能命令 `ID_DATA=0x10 / DIGITAL=0x20 / COIN=0x21 / ANALOG=0x22 / ROTATORY=0x23` 等。
- [TeknoParrotUi.Common/Jvs/JvsPackageEmulator.cs](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Jvs/JvsPackageEmulator.cs)（约 2000 行）：完整实现一个虚拟 JVS I/O 板。`ParsePackage(bytesLeft, multiPackage, node)` 按首字节分发到 `JvsGetIdentifier / JvsGetCommandRev / JvsGetJvsVersion / JvsGetCommunicationVersion / JvsGetSlaveFeatures / JvsGetDigitalReply / JvsGetCoinReply / JvsGetAnalogReply / JvsGetMiscSwitchInput` 等处理器；还实现了 Taito 私有命令族（`JvsTaito01/03/04/05/23/25/26/65/6A/6B/6D`，如 `0x65` 返回看门狗秒数 `0xA0`）与 Namco 自定义命令（`JvsGetNamcoCustomCommands`）。按键状态由 `InputListenerDirectInput.cs / InputListenerXInput.cs / InputListenerRawInput.cs`（`InputListening/` 目录）实时填充到静态字段（`GetPlayerControls(index)` 等），投币计数由 `UpdateCoinCount` 维护。
- 每块基板的 JVS 个性通过 `GameRunning.xaml.cs` 里的 `JvsPackageEmulator.Initialize()` 之后的开关设置，例如 Pokken/MarioKart 设 `Namco=true, JvsIdentifier=JVSIdentifiers.NBGI_Pokken, JvsVersion=0x31`，Battle Gear 4 设 `TaitoBattleGear=true, DualJvsEmulation=true, JvsSwitchCount=0x18`。

**DLL 侧（OpenParrot，C++）——串口拦截端：**

[OpenParrot/src/Functions/Ring_amLib/amJvs.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Ring_amLib/amJvs.cpp) 是最典型的实现：

- `MH_CreateHookApi` hook 掉 `kernel32.dll` 的 `CreateFileA/CreateFileW`：游戏打开 JVS 串口（Ring 系为 `COM4`，TypeX 系为 `COM2`——`if (GameDetect::IsTypeX()) hookPort="COM2"`）时，返回值被替换为打开命名管道 **`\\.\pipe\teknoparrot_jvs`** 的句柄。游戏后续对这个"串口"的 `ReadFile/WriteFile` 实际上在与 TeknoParrotUI 的 JVS 模拟器收发 JVS 帧。
- 其余串口 API 全部造假：`SetCommState/GetCommState/SetCommTimeouts/GetCommMask/SetCommMask/SetupComm/EscapeCommFunction` 直接返回 TRUE；`PurgeComm` 映射为 `CancelIoEx`；`ClearCommError` 把 `cbInQue` 填 0x7FFF 让游戏认为缓冲区一直有数据；`GetCommModemStatus` 模拟 JVS 的 sense/addressed 线（共享内存 `TeknoParrot_JvsState` 的第一个 int 非零时返回 0x10/0x30）。
- 顺带在同一 Hook 里做文件系统重定向：`Y:\` 开头路径改写为 `.\`（Ring 基板的数据盘盘符，见 4.8）。
- 共享内存 `CreateFileMapping(..., L"TeknoParrot_JvsState", 64 字节)` 承载方向盘/FFB 等高速状态（`wheelSection`、`ffbOffset1..9`）。

另外大量游戏不走串口而各有私有 I/O 协议，TeknoParrotUI 用 **每游戏一个 Pipe 类**（`TeknoParrotUi.Common/Pipes/` 下 50+ 个文件：`FastIOPipe.cs / amJvsPipe.cs / Pokken.cs / SWDCPipe.cs / RawThrillsGUN.cs / GHA.cs / EuropaRPipe.cs / ExBoard.cs …`）对接，OpenParrot 侧相应地在对应游戏模块里拦截私有 DLL/API。UI 与 DLL 之间的通用控制通道是 `\\.\pipe\TeknoParrotPipe`（[ControlPipe.cs](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/Pipes/ControlPipe.cs)，`NamedPipeServerStream`；`ControlSender.cs` 15ms 周期推送）。TeknoParrotUI issue 里也明确说明：*"we send the controls over a named pipe from TPUI to OpenParrot"*（[issue #69](https://github.com/teknogods/TeknoParrotUI/issues/69)）。

光枪/方向盘等模拟量在 UI 侧由 RawInput（鼠标/光枪）与 XInput 扳机/摇杆归一化后塞进 JVS 模拟量应答（`JvsGetAnalogReply`）或游戏私有 pipe。

### 4.2 Fast I/O（iDmacDrv）模拟

NESiCAxLive 系（Taito Type X2/X3 后期）不用 JVS 串口，而用 Taito 的 Fast I/O 板，游戏通过 `idmacdrv32.dll` / `idmacdrv64.dll` 的导出函数访问。OpenParrot 的做法是**整表替换导出函数**（[Functions/Nesica_Libs/FastIoEmu.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/FastIoEmu.cpp)）：

```c
const char* libName = "idmacdrv32.dll";
LoadLibraryA(libName);
injector::MakeJMP(GetProcAddress(h, "iDmacDrvOpen"),        iDmacDrvOpen);
injector::MakeJMP(GetProcAddress(h, "iDmacDrvRegisterRead"),iDmacDrvRegisterRead);
... // x64 分支用 MH_CreateHookApi(L"idmacdrv64.dll", ...)
```

`iDmacDrvRegisterRead` 按 `CommandCode` 返回伪造寄存器值：`0x4120` = 1/2P 按键位图（来自 `g_fastIOValues[0..3]`）、`0x4124/0x4128` = 模拟量、`0x4140` = 投币（带 `coinPressed` 防卡币逻辑：一次按下只上报一次）、`0x41A0` = 3/4P 按键、`0x400/0x4000/0x4004` = 版本/状态常量。`g_fastIOValues[64]` 由一个线程从 `\\.\pipe\TeknoParrotPipe` 循环 `ReadFile` 填充——即 UI 侧 `FastIOPipe.cs` 每帧推送 64 字节输入快照。

### 4.3 显卡 / 分辨率补丁与 Direct3D Hook

三条互补路线：

1. **D3D9 vtable Hook**（[Functions/WindowedDx9.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/WindowedDx9.cpp)）：`MH_CreateHookApi(L"d3d9.dll", "Direct3DCreate9", Direct3DCreate9Wrap, ...)` → 在包装函数里改写 `IDirect3D9::CreateDevice` 的 vtable 项 → `CreateDeviceWrap` 中强制 `pPresentationParameters->Windowed = TRUE`、`FullScreen_RefreshRateInHz = 0`、修正 `BackBufferFormat`（如 TroubleWitches 需要 `D3DFMT_A8R8G8B8`）、为 CrazySpeed 这类会传入非法 BackBuffer 尺寸的游戏做 sanitize；创建设备后再挂 `Reset/Present/SetTexture` 的 vtable hook，`PresentWrap` 里跑 `FpsLimiter()`（帧率限制，部分老游戏在现代 CPU 上速度失控），`SetTextureWrap` 修复固定管线游戏"stage 0 置 NULL 后仍采样"的问题（伪造 1x1 白色纹理）。同族文件还有 `WindowedDx8.cpp`（D3D8，vendored dx8 头文件在 `OpenParrot/deps/inc/dx8/`）与 `WindowedDxgi.cpp`（DXGI/D3D11 系，如 ES3 游戏）。
2. **内存补丁改写死分辨率**：街机游戏大量把 1360×768、1366×768、1920×1080 写死在代码/数据段。OpenParrot 用 `injector::WriteMemory<DWORD>(imageBase + 0x1f4c6d, resWidth, true)` 这类语句直接改常量，并同步缩放字体/投影参数（如 Battle Gear 4 Tuned 自定义分辨率时写入 `resWidth/800.0f` 字体缩放系数，`TypeX2Generic.cpp` L1320–L1342）。UI 侧对应 GameProfile 里的 `ResolutionWidth/ResolutionHeight` ConfigValue。
3. **GPU 检测绕过**：部分游戏检查显卡型号/厂商（如 Type X 绑定 nVIDIA 特定 GPU），OpenParrot 按游戏 patch 掉检测代码（`MakeNOP`/`MakeRET`/跳过跳转）。OpenGL 游戏（Lindbergh 等）则用 `regal32.dll`（[teknogods/regal](https://github.com/teknogods/regal)，开源 GL 兼容层）替换系统 OpenGL 行为；`GameProcessManager.cs` 里可见 `SetChildEnvironmentVariable(info, "REGAL_LOAD_GL", "opengl32.dll")`。

### 4.4 网络模拟：NESYS / ALL.Net / 局域网联机

**NESiCA / NESYS（Taito）**：[Functions/Nesica_Libs/NesysEmu.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/NesysEmu.cpp) 在游戏进程内创建命名管道 `\\.\pipe\nesys_games`（DariusBurst 用 `\\.\pipe\nesystest`）伪装 NESYS 本地服务进程，按 `NesysCommandHeader{command,length}` 协议应答：店铺信息（`tenpo_id`）、会话 XML、卡数据存取、成绩上报、适配器信息（`SCOMMAND_ADAPTER_INFO_REPLY`，IP/网关/DNS 来自 teknoparrot.ini 的 `[Network]` 节，MAC 固定写 `DEADBABECAFE`）。同时 `MH_CreateHookApi(L"iphlpapi.dll", "GetIfEntry", ...)` 强制网卡 `dwOperStatus = IF_OPER_STATUS_CONNECTED`，让游戏认为网线插着。`CryptoPipe.cpp`（同目录）处理 NESiCA 的加密管道，`RegHooks.cpp` 伪造 NESiCA 在注册表里的安装/认证键值，`NesicaKey` 枚举区分各游戏的加密密钥变体。

**SEGA ALL.Net / amdaemon**：ALLS/RingEdge 系由 `amdaemon.exe` 提供认证与网络服务。TeknoParrot 的策略是启动社区替代实现：SegaTools 系用 `inject.exe -d -k idzhook.dll amdaemon.exe -c config*.json`（GameProcessManager.cs L750 注释中的真实命令行）；OpenParrot 侧则有 `Games/ES3X/AmAuthGame64.cpp`（Namco ES3 的认证模拟）与 [OpenSegaAPI](https://github.com/teknogods/OpenSegaAPI)（`Opensegaapi.dll`）。

**局域网对战**：双机/四机联机游戏的网络代码本身就在游戏 exe 里，OpenParrot 直接把**对手 IP 写进内存**，如 `TypeX2Generic.cpp` L1226–L1230：`injector::WriteMemory<DWORD>(imageBase+0x5D868, (DWORD)cab1IP, true)` ×4 台机，以及广播地址 patch（`imageBase+0xA1004 = BroadcastAddress`）。UI 侧提供 NetworkAdapterDropdown 与联机设置界面。互联网对战则由闭源的 TeknoParrot Online / lobby（`TeknoParrotOnline.xaml`、`TPOnline*.cs`、`tponline://` 深链）实现。

### 4.5 读卡器模拟：RFID / BanaPass / Aime

- **Taito NESiCA 读卡器**（[Functions/Nesica_Libs/RfidEmu.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/RfidEmu.cpp)）：文件头注明"ORIGINALLY BASED ON ttx_monitor, modified for RFID"（[zxmarcos/ttx_monitor](https://github.com/zxmarcos/ttx_monitor)）。它 hook `kernel32!CreateFileA/W`，把游戏打开的 RFID 串口重定向到内部虚拟设备，以 JVS 风格包格式应答，设备 ID 字符串伪装为 `"TAITO CORP.;RFID CTRL P.C.B.;Ver1.00;"`；`cardInserted` 标志 + 内置 24 字节示例卡数据（注释："Dumped from my own Japanese Lord Vermilion Nesica XLive card -Reaver"）模拟插卡。
- **Bandai Namco BanaPass**：[OpenBanapass/src/banapass.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenBanapass/src/banapass.cpp)（独立编译为 DLL，替换游戏的 `bngrw.dll`——本地 `OpenParrotWin32/bngrw.dll` 即是），实现 BanaPass 读卡 DLL 的导出函数。WMMT（湾岸）系列的 BanaPass 模拟见 `Games/ES3X/BanapassEmu.cpp`；TeknoParrotUI 侧有 `Pipes/BanapassButton.cs / BanapassButtonEXVS2.cs` 处理"刷卡"按钮与卡文件（`WMMT3Cards.cs` 生成卡数据文件）。
- **Sega Aime**：APM3（Sega 新基板）的 Aime 读卡见 `Games/APM3/Aime.cpp` + `Auth.cpp` + `Backup.cpp`（存档服务器模拟）。

### 4.6 加密狗与认证绕过

街机的"防盗版"手段与绕过方式：

- **Type X/X2 加密狗**：游戏轮询 USB 加密狗或检查特定签名。OpenParrot 做法是把检测函数 patch 掉——`TypeX2Generic.cpp` 中典型手法：`injector::MakeNOP(0x004DE4B4, 2)`（抹掉条件跳转）、`injector::MakeJMP(imageBase+0xFF0A0, ReturnsTrue)`（强制返回成功）、`injector::MakeRET(0x5F21B0, 4)`（整函数变 ret）、`injector::MemoryFill(imageBase+0x2EF470, 0, 48, true)`（注释："Remove dll injection routine"——去掉游戏自带的反外挂/注入例程）。GameProfile 里的 `DongleRegion`（JAPAN/USA/EXPORT/CHINA）选项则喂给游戏想要的区域字节。
- **RingEdge keychip / NESiCA 加密**：`GameDetect` 的 `NesicaKey` 枚举 + `Nesica_Libs/CryptoPipe.cpp`、`RegHooks.cpp` 伪造密钥派生与注册表项，让游戏的自解密/认证流程在缺少真实 keychip 时走完。
- **NESYS/ALL.Net 在线认证**：见 4.4，整个服务器被本地实现替换。
- 需要注意：**这类绕过代码正是 TeknoParrot 把部分功能留在闭源 `TeknoParrot.dll`、而 OpenParrot 仓库多年不更新主线的原因之一**（下节详述）。

### 4.7 音频子系统

- 大多数 PC 基板游戏直接走 Windows 音频（DirectSound/WASAPI），无需模拟；但有些基板有**独立音频板**（如 Gaelco、Star Trek Voyager 所用的），TeknoParrot 用单独的开源 DLL 模拟：[OpenSndGaelco](https://github.com/teknogods/OpenSndGaelco)、[OpenSndVoyager](https://github.com/teknogods/OpenSndVoyager)（本地 `TeknoParrot/OpenSndGaelco.dll / OpenSndVoyager.dll`），游戏对音频板串口/IO 的访问被重定向到这些 DLL 再混音到 Windows 音频。
- **DirectSound 包装**：[Games/TypeX2/DSoundWrapper.cpp](https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Games/TypeX2/DSoundWrapper.cpp) 实现完整的 `HookIDirectSound8` / `HookIDirectSoundBuffer8` COM 对象包装（vtable 逐项转发），用于修复 Battle Gear 4 等 Type X2 游戏的音频问题。
- 更硬核的案例是 BG4 英文版在 Wine/Android 下的 DSOUND 重采样循环补丁（`TypeX2Generic.cpp` 中 `InstallBG4EnglishDsoundLoopPatch()`）：对系统 `DSOUND.dll` 的 `0x476B5` 偏移做**字节签名比对**（`{0x3B,0x4D,0xF8,0x75,0x05}`），匹配后用 `VirtualProtect + E9 jmp` 把重采样循环跳到自实现的 `BG4EnglishDsoundResampleLoopAndroid`，`FlushInstructionCache` 收尾。日志串直接叫 `"BG4 Android: unsupported DSOUND resampler signature"`——又一个官方 Android 适配痕迹。

### 4.8 存档 / NVRAM / 文件系统重定向

- **数据盘盘符重定向**：Ring 系游戏读写 `Y:\`（基板第二块 DOM/硬盘），`amJvs.cpp` 的 `Hook_CreateFileA` 把 `"Y:\"` 前缀改写为 `".\"`，存档/排行数据直接落在游戏目录。
- **Type X 系**：游戏把 NVRAM/存档写在注册表或固定路径，OpenParrot 用 `RegHooks`（`GlobalRegHooks.cpp`、`Nesica_Libs/RegHooks.cpp`）把注册表访问重定向到伪造的键值视图；部分游戏存档路径被 patch 成相对路径（`WriteMemoryRaw(..., ".\\messagelog.dat\0", 18, true)` 这类字符串替换）。
- **NESiCA 卡数据/成绩**：由 NesysEmu 的管道协议落到本地文件（如 `.\OpenParrot\news.png`、卡数据文件）。
- **Lindbergh**：lindbergh-loader 用 `src/lindbergh/eeprom.c` 把基板 EEPROM 模拟为一个本地文件。
- 结论：所有"专用 EEPROM/硬盘镜像"都被翻译成**游戏目录下的普通文件**，这正是兼容层思路的自然结果。

### 4.9 Lindbergh（Linux 基板）的处理

SEGA Lindbergh 运行定制 Linux（MontaVista），游戏是 32 位 ELF。TeknoParrot 在 Windows 上通过 **ElfLdr2 / BudgieLoader.exe + TeknoParrot.dll + msys-2.0.dll**（Cygwin 派生 POSIX 兼容层）运行 ELF：本地 `ElfLdr2/` 目录（`BudgieLoader.exe、TeknoParrot.dll、msys-2.0.dll、libs/、env/`）即该方案实体。OpenParrot 侧对应的入口是 `dllmain.cpp` 中 x86 专有导出 `InitLinux(void* (*makeCall)(void*))`——ELF 加载器把"发起 Linux  syscall / 回调"的桥接函数指针传进来，随后跑 `GameDetect::DetectCurrentLinuxGame()` 与 `GameID::LinuxEmulation` 初始化组。TeknoParrotUI 对 ELF 游戏还有非 ASCII 路径限制（`GameRunning.xaml.cs` L411 注释："Elfloader 2 and the linux games can't handle non ascii paths"）。

**lindbergh-loader**（[github.com/lindbergh-loader/lindbergh-loader](https://github.com/lindbergh-loader/lindbergh-loader)）是 Linux 原生路线，原理文档价值极高：

- 游戏 ELF 动态链接 SEGA 私有库 `libsegaapi.so`、`libkswapapi.so` 等；loader **自己用 C 重新实现这些 .so**（`src/libsegaapi/libsegaapi.c`、`src/libkswapapi/libkswapapi.c`），放进游戏目录抢在原装库之前被动态链接器加载，从 ABI 层面接管所有基板服务调用。
- `src/lindbergh/` 下模块化实现：`hook.c`（ELF/GOT hook 与设备文件拦截，`HOOK_FILE_NAME "/dev/zero"`）、`jvs.c`（JVS 协议模拟，支持真 JVS 口 pass-through）、`eeprom.c`（EEPROM 文件化）、`securityBoard.c`（安全板/dongle 模拟）、`baseBoard.c / driveBoard.c / rideBoard.c / motionBoard.c`（各外设板）、`cardReader.c`、`glutHooks.c / glxHooks.c`（GLUT/GLX 拦截实现自定义分辨率与窗口化）、`gpuVendor.c`（GPU 厂商伪装）、`shaderWork/`（逐游戏 shader 补丁：hod4/gsevo/or2/vf5/rtuned/primeval/rambo… 每个游戏一个 .c）、`evdevInput.c / sdlInput.c`（输入映射）、`patch.c / patchNetwork.c`（内存补丁与联机补丁）。
- 配置为 `lindbergh.conf`，许可为 **CC BY-NC-SA 4.0**（明确禁止商业用途）。

这条路线证明了：**同一套"兼容层"方法论在 Linux 上同样成立，只是注入机制从 DLL+MinHook 换成了 .so 替换 + GOT hook**。

---

## 5. GameProfile XML 与补丁机制

**GameProfile XML**（`TeknoParrotUi.Common/GameProfiles/*.xml`，500+ 个文件）由 [GameProfile.cs](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/GameProfile.cs) 反序列化，核心字段：

| 字段 | 作用 |
|---|---|
| `EmulationProfile` | 选择 OpenParrot 内部哪套模拟逻辑（`SegaJvs / FastIo / SegaJvsGoldenTee / NamcoPokken …`） |
| `EmulatorType` | 选择启动器（`TeknoParrot / OpenParrot / ElfLdr2 / Lindbergh / SegaToolsIDZ / Dolphin / RPCS3 / N2 …`） |
| `GamePath / ExecutableName / Is64Bit` | 游戏 exe 位置与位数（决定用 OpenParrotLoader 还是 OpenParrotLoader64） |
| `TestMenuParameter / TestMenuIsExecutable / HasSeparateTestMode` | 测试模式进入方式 |
| `ConfigValues` | 键值选项（CategoryName/FieldName/FieldValue/FieldType/FieldOptions），启动时由 `ConfigurationWriter.WriteConfigIni()` 平铺成 `teknoparrot.ini` 分节（[ConfigurationWriter.cs L27–L88](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/GameRunningCode/Utilities/ConfigurationWriter.cs)），OpenParrot 用 `linb::ini` 读取（`dllmain.cpp` 中 `config.load_file("teknoparrot.ini")`） |
| `JoystickButtons` | 按键映射表，`InputMapping` 枚举（`P1ButtonStart / Coin1 / JvsTwoService1 / ExtensionOne1 …`）桥接物理输入与 JVS/私有协议字段 |
| `ValidMd5` | 指向 `MD5/<Game>.md5` 文件，内含关键文件的 MD5 清单（如 `DariusBurst.md5`：`b6ec71f7d253418a3d466b1bd071262b *game.exe` 等 5 行），UI 的 VerifyGame 功能据此校验游戏文件版本——**内存补丁是绝对地址/字节签名敏感的，版本不对 patch 必崩**，这就是 MD5 校验存在的意义 |
| `GameProfileRevision` | profile 自身版本号，自动迁移老配置 |
| `GunGame / InvertedMouseAxis / xAxisMin/Max / Use16BitAnalog …` | 输入设备特性开关 |

另有 **GameSetup XML**（`TeknoParrotUi.Common/GameSetup/*.xml`）描述游戏安装文件的期望目录结构（用于"验证游戏文件"向导），以及 `Metadata/*.xml`（显示名/简介/图标）。

**补丁机制**分三层：

1. **编译进 DLL 的硬补丁**：OpenParrot 源码里大量 `injector::WriteMemory/MakeNOP/MakeJMP/MakeRET(0x004XXXXX, ...)` 是绝对地址补丁，靠 `GameDetect` 的 CRC32 保证打的是对的版本；版本漂移时用 `Hooking.Patterns` 做字节模式扫描兜底。
2. **API/串口/DLL 导出 Hook**：MinHook（`MH_CreateHookApi`）+ `injector::MakeJMP(GetProcAddress(...))` + IAT hook（`NesicaGeneric.cpp` 中 `iatHook("kernel32.dll", EnEinsCreateFileAHook, "CreateFileA")` 直接改导入表）。
3. **运行时配置驱动**：teknoparrot.ini + 命名管道 + 共享内存（`TeknoParrot_JvsState`），把 UI 侧的实时输入/设置送进游戏进程。

## 6. 代码许可与法律边界

- **OpenParrot：GPL-3.0**（GitHub License API 确认 `spdx_id: GPL-3.0`）。**TeknoParrotUI：GPL-3.0**。两者均为社区开发（Reaver、NTA、avail、Boomslangnz 等）。
- **TeknoParrot 核心 DLL（TeknoParrot.dll/TeknoParrot64.dll）、OpenParrotLoader.exe、TeknoParrot Online/lobby：闭源**。发布包整体被 Emulation General Wiki 标注为 closed-source compatibility layer。开源的 OpenParrot 仓库代码停留在较早状态，新游戏支持先在 Patreon 抢先版闭源推送、后续逐步释放。
- **lindbergh-loader：CC BY-NC-SA 4.0**，明确禁止商业/收费用途，README 留有 takedown 联系方式。
- **游戏本体（ROM）**：TeknoParrot 不分发任何游戏数据，官网与 Wiki 明确禁止讨论 ROM 获取渠道；游戏版权归属 Taito/Sega/Namco/Raw Thrills 等厂商。内存补丁、dongle 绕过在多数司法辖区处于" interoperability / 维修权"与"规避技术保护措施（DMCA 1201 类条款）"的灰色地带；分发打补丁后的游戏镜像则明确侵权。做衍生方案时应只分发模拟器代码与 profile 数据，不碰游戏文件。
- OpenParrot 仓库 README 提供的开发流程（x64dbg 动态调试 + `loadlib` + 往 `GameDetect.cpp` 加 CRC/字节签名）也表明项目自定位为逆向/保护工具而非盗版工具链。

## 7. 对「移植到 Android」的启示

先把结论摆在前面：**TeknoParrot 的知识资产大部分可移植，但运行时机制绑定 Windows/x86；现实路径是在 Android 上重建"Wine/Box64 + 兼容层"栈，而不是把 TeknoParrot 原生移植过去**。官方代码里已经出现的 Android/Wine 痕迹（见下）证实官方自己也在走这条路。

**可直接复用（平台无关）的资产：**

- **协议与数据层**：`JvsPackageEmulator.cs`/`JVSPackets.cs`（JVS 协议完整应答逻辑，纯 C#，可移植到任何语言重写）、50+ 个 `Pipes/*.cs` 的每游戏 I/O 协议、GameProfile/GameSetup/Metadata/MD5 数据文件、`teknoparrot.ini` 配置模型、`NesysEmu` 的 NESYS 命令集应答逻辑、`RfidEmu/BanapassEmu` 的卡协议数据。这些是多年逆向积累的精华，与 OS 无关。
- **每游戏补丁知识**：OpenParrot 源码中的地址补丁/字节签名是每个游戏各自的逆向成果，移植时语义可照搬（在 Wine 环境下甚至地址都一致，因为跑的还是同一个 PE）。

**强依赖 Windows、需要替代实现的机制：**

| Windows 机制 | Android 替代 |
|---|---|
| `OpenParrotLoader.exe` + DLL 注入 + EP 劫持 | 在 Wine 进程模型内由 loader/环境变量（`WINEDLLPATH`、Wine 内建 DLL 覆写）加载；源码已有 `TP_LOADER_MANAGED_INIT`/`TP_REMOTETHREAD` 适配分支 |
| MinHook（x86/x64 inline hook） | 目标代码仍是 x86（经 Wine/Box64/Winlator 翻译执行），所以 hook 仍在 x86 层做、MinHook 可原样工作；若走纯原生 ARM 重实现（不现实，游戏是 x86 PE），则需 Dobby 等 ARM64 hook 框架 |
| Direct3D 8/9/11 vtable hook | Wine 下 D3D 由 WineD3D → OpenGL ES / DXVK → Vulkan 承接；hook 点不变（hook 的是游戏调用的 d3d9.dll，Wine 提供同名 DLL），但 Present/分辨率行为需按 WineD3D 重新调参 |
| Win32 串口 API（CreateFile("COM2")、GetCommModemStatus…）/ 命名管道 / 共享内存 / 注册表 / iphlpapi | Wine 全套实现这些 API，OpenParrot 的 hook 无需改动——这正是"跑在 Wine 上"路线最大的红利 |
| ELF/Lindbergh（ElfLdr2 + msys） | Android 即 Linux 内核，lindbergh-loader 的 .so 替换 + GOT hook 路线反而更自然，主要工作是 x86→ARM 指令翻译（Box86）与 GLES 适配（其 shaderWork 已是逐游戏补丁模式） |

**官方 Android 适配的直接证据（说明方向已被验证）：**

- `dllmain.cpp`：*"Wine/Box64 can load us from a remote thread after the game's real entry point has been parked by OpenParrotLoader"*，`TP_LOADER_MANAGED_INIT` 惰性 DllMain 模式；
- `GameDetect.cpp`：`TP_CRC_NORMALIZE_LAA`——*"Android may launch a private Large Address Aware copy of an x86 executable so Wine can satisfy the cabinet's large early allocation"*；
- `TypeX2Generic.cpp`：`BG4EnglishDsoundResampleLoopAndroid` 与 `"BG4 Android: unsupported DSOUND resampler signature"`；
- `NesysEmu.cpp`：`IsAndroidNesysDebugEnabled()` 诊断分支。

**落地建议（为后续方案铺垫）：** 以 Winlator/Wine + Box64 为底座运行原 x86 游戏与 OpenParrot 栈，UI/输入层（触屏映射、手柄）重写为 Android 原生并复刻 `TeknoParrotPipe`/`teknoparrot_jvs` 管道协议（Wine 支持命名管道）即可与未修改的 OpenParrot.dll 对接；JVS 模拟器与 profile 数据库按 GPL-3.0 合规地从 TeknoParrotUI 移植；Lindbergh 游戏参考 lindbergh-loader 的 Linux 原生方案。性能瓶颈会在 Box64 的 x86→ARM64 翻译与 WineD3D 的 D3D9→GLES/Vulkan 转换上，老基板（Type X/X2，D3D9 世代）可行性最高，ES3/RingEdge2（D3D11、重 3D）需要逐游戏评估。

---

## 附录 A：JVS 帧格式速查（对应 JVSPackets.cs / JvsPackageEmulator.cs）

JVS 在物理层是 RS-485 多 drop 串行总线（I/O 板为 slave），帧结构：

```
请求帧: [0xE0 SYNC] [节点地址] [数据长度] [命令码...] [校验和]
应答帧: [0xE0 SYNC] [0x00 主机] [数据长度] [状态] [报告] [数据...] [校验和]
```

关键命令码（`JvsPackageEmulator.ParsePackage()` 的分支表即按此实现）：

| 命令 | 含义 | OpenParrot/UI 实现要点 |
|---|---|---|
| 0xF0 / 0xF1 | 复位 / 地址分配 | `JvsGetAddress`，配合串口 sense 线（`GetCommModemStatus` 返回 0x10/0x30）完成握手 |
| 0x10 | 读 I/O 识别字符串 | `JvsGetIdentifier`，返回 `JVSIdentifiers.*`（如 `NBGI_Pokken`、`SegaLetsGoSafari`） |
| 0x11/0x12/0x13 | 命令/协议/通信版本 | `JvsGetCommandRev / JvsGetJvsVersion / JvsGetCommunicationVersion`（典型 0x30/0x31） |
| 0x14 | 从机功能描述 | `JvsGetSlaveFeatures`，声明按键数/模拟通道数（`JvsSwitchCount`，如 0x18） |
| 0x20 | 读数字输入 | `JvsGetDigitalReply`，由 `GetPlayerControls()` 从 UI 输入监听器取实时状态 |
| 0x21 | 读投币 | `JvsGetCoinReply` + `UpdateCoinCount`，含卡币/断线状态机（`JVSCoin`） |
| 0x22 / 0x23 | 读模拟量 / 旋转编码器 | `JvsGetAnalogReply`，方向盘/油门/光枪轴 |
| 0x32/0x33/0x35/0x36 | 通用输出/模拟输出/字符输出/扣币 | 驱动机台灯、牌器、FFB 前级 |
| 0x01/0x03/0x65/0x6A… | Taito 私有扩展 | `JvsTaitoXX` 系列（0x65 返回看门狗秒数 0xA0） |

Fast I/O（NESiCA 系）则是完全不同的寄存器读写模型（`iDmacDrvRegisterRead(CommandCode)`），不走 JVS 帧。

## 附录 B：teknoparrot.ini 实例（本地 D:\yinwu 实证）

`ConfigurationWriter.WriteConfigIni()` 生成的文件实例（`D:\yinwu\teknoparrot.ini`）：

```ini
[GlobalHotkeys]
ExitKey=0x1B
PauseKey=0x13
[General]
Input API=DirectInput
Windowed=1
```

OpenParrot `dllmain.cpp` 用 `linb::ini`（ini-parser 的 C++ binding）在进程内 `config.load_file("teknoparrot.ini")` 读取，各模块通过 `ToBool(config["General"]["Windowed"])`、`FetchDwordInformation("Graphics", "Resolution Width", 1360)` 这类helper 取值——UI 的图形选项与进程内行为之间**只通过这一个 ini 文件解耦**，这一设计对移植非常友好（Android 侧只要生成同格式 ini 即可驱动未经修改的 OpenParrot.dll）。

## 附录 C：构建与逆向工作流（OpenParrot README 官方流程）

1. `premake5.bat` 生成 VS2019 工程，编译 x86（OpenParrot.dll）与 x64（OpenParrot64.dll）两个目标；
2. 新游戏适配流程：`TeknoParrotUi.exe --profile=<相近profile>.xml` 启动 → x64dbg 附加游戏进程 → 命令行 `loadlib <路径>\OpenParrot.dll` 热加载 → 动态逆向定位 I/O 调用点 → 把游戏签名（PE 头 CRC32 或字节模式）加入 `GameDetect.cpp` → 编写对应 `Games/<基板>/<游戏>.cpp` 的 `InitFunction`；
3. 该流程本身就是一份"如何给新基板写兼容层"的实操手册，见 [README](https://github.com/teknogods/OpenParrot) 与官方 Twitch 录像（https://www.twitch.tv/videos/308359681）。

## 8. 参考资料

仓库与源码：

- OpenParrot 仓库：https://github.com/teknogods/OpenParrot （README、LICENSE=GPL-3.0）
  - dllmain.cpp（EP 劫持与加载模式）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/dllmain.cpp
  - GameDetect.cpp（CRC32 游戏识别）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Utility/GameDetect.cpp
  - amJvs.cpp（JVS 串口 hook）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Ring_amLib/amJvs.cpp
  - FastIoEmu.cpp（iDmacDrv 模拟）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/FastIoEmu.cpp
  - NesysEmu.cpp（NESYS 网络模拟）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/NesysEmu.cpp
  - RfidEmu.cpp（RFID 读卡器）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/Nesica_Libs/RfidEmu.cpp
  - WindowedDx9.cpp（D3D9 hook）：https://github.com/teknogods/OpenParrot/blob/master/OpenParrot/src/Functions/WindowedDx9.cpp
  - TypeX2Generic.cpp / NesicaGeneric.cpp / DSoundWrapper.cpp：https://github.com/teknogods/OpenParrot/tree/master/OpenParrot/src/Functions/Games
  - OpenBanapass：https://github.com/teknogods/OpenParrot/tree/master/OpenBanapass
- TeknoParrotUI 仓库：https://github.com/teknogods/TeknoParrotUI （LICENSE=GPL-3.0）
  - Library.xaml.cs（loader 选择）：https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/Library.xaml.cs
  - GameProcessManager.cs（进程创建与注入命令行）：https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/GameRunningCode/ProcessManagement/GameProcessManager.cs
  - ConfigurationWriter.cs（teknoparrot.ini 生成）：https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/Views/GameRunningCode/Utilities/ConfigurationWriter.cs
  - GameProfile.cs / GameProfiles/*.xml / MD5/*.md5：https://github.com/teknogods/TeknoParrotUI/tree/master/TeknoParrotUi.Common
  - JvsPackageEmulator.cs / JVSPackets.cs：https://github.com/teknogods/TeknoParrotUI/tree/master/TeknoParrotUi.Common/Jvs
  - Pipes/（每游戏 pipe 协议）：https://github.com/teknogods/TeknoParrotUI/tree/master/TeknoParrotUi.Common/Pipes
  - issue #69（命名管道输入通道说明）：https://github.com/teknogods/TeknoParrotUI/issues/69
- lindbergh-loader：https://github.com/lindbergh-loader/lindbergh-loader （README、docs/guide.md、src/lindbergh/、src/libsegaapi/）
- OpenSegaAPI / OpenSndGaelco / OpenSndVoyager / regal / JVSClient / TeknoFfb / DemulShooter：https://github.com/orgs/teknogods/repositories
- MinHook：https://github.com/TsudaKageyu/minhook ；injector/Hooks.Patterns（vendored 于 OpenParrot/deps）
- ttx_monitor（RfidEmu 前身）：https://github.com/zxmarcos/ttx_monitor

文档与文章：

- TeknoParrot 官方 Wiki：https://teknoparrot.com/wiki/getstarted.html
- Emulation General Wiki – TeknoParrot：https://emulation.gametechwiki.com/index.php/TeknoParrot
- Emuline 教程（TeknoParrot / BudgieLoader / GameLoader All RH 关系）：https://www.emuline.org/topic/1459-tuto-teknoparrot-budgieloader-gameloader-all-rh-explanation-and-how-to-install-and-such-things/
- teknoparrot-issues 追踪（如 #307 ElfLdr2/BudgieLoader/JVS pipe 问题）：https://github.com/teknogods/teknoparrot-issues

本地实证：

- `D:\yinwu\1846\`（TeknoParrot build 1846 完整发布包：OpenParrotWin32/OpenParrotx64/TeknoParrot/ElfLdr2/N2/GameProfiles 等目录与 `teknoparrot.ini` 实例）。
