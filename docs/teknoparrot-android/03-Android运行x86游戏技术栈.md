# 在 ARM Android 设备（智能电视/电视盒子）上运行 Windows x86 游戏：完整技术栈调研

> 调研截止日期：2025 年底。目标场景：在 ARM 架构 Android 智能电视/电视盒子上运行一批街机光枪游戏（Windows x86/x64 + DirectX 9/11，靠 TeknoParrot 兼容层启动；另有少量 Linux ELF 游戏），要求能落地做成产品。

## 目录

- [0. 一句话结论](#0-一句话结论)
- [1. 技术栈总览：四层结构](#1-技术栈总览四层结构)
- [2. 指令集翻译层（x86 → ARM）](#2-指令集翻译层x86--arm)
  - [2.1 Box86 / Box64](#21-box86--box64)
  - [2.2 FEX-Emu](#22-fex-emu)
  - [2.3 ExaGear（已死）](#23-exagear已死)
  - [2.4 Rosetta 2（仅作参考基准）](#24-rosetta-2仅作参考基准)
  - [2.5 QEMU 全系统/用户态模拟（为何不可行）](#25-qemu-全系统用户态模拟为何不可行)
  - [2.6 Android x86 镜像路线（为何电视走不通）](#26-android-x86-镜像路线为何电视走不通)
- [3. Windows API 层（Wine 系）](#3-windows-api-层wine-系)
  - [3.1 官方 Wine for Android 现状](#31-官方-wine-for-android-现状)
  - [3.2 Wine 10.x 的 ARM64EC / WOW64 进展](#32-wine-10x-的-arm64ec--wow64-进展)
  - [3.3 Hangover](#33-hangover)
  - [3.4 Winlator](#34-winlator)
  - [3.5 GameHub / GameFusion（GameSir）](#35-gamehub--gamefusiongamesir)
  - [3.6 Mobox / Termux-box 等 Termux 系方案](#36-mobox--termux-box-等-termux-系方案)
  - [3.7 各方案横向对比](#37-各方案横向对比)
- [4. 图形 API 翻译层（DirectX → Vulkan/OpenGL）](#4-图形-api-翻译层directx--vulkanopengl)
  - [4.1 DXVK / D8VK / VKD3D](#41-dxvk--d8vk--vkd3d)
  - [4.2 Android 端 Vulkan 驱动现状：最大瓶颈](#42-android-端-vulkan-驱动现状最大瓶颈)
  - [4.3 Turnip（Adreno 开源 Vulkan 驱动）](#43-turnipadreno-开源-vulkan-驱动)
  - [4.4 Vortek、VirGL、Zink 等兜底方案](#44-vortekvirglzink-等兜底方案)
  - [4.5 老 DirectX 9/8 游戏的轻量路径](#45-老-directx-98-游戏的轻量路径)
- [5. 实测性能参考数据](#5-实测性能参考数据)
- [6. 备选路线：串流 / 云游戏](#6-备选路线串流--云游戏)
- [7. 结论与推荐技术栈](#7-结论与推荐技术栈)
- [8. 风险清单](#8-风险清单)
- [9. 参考来源](#9-参考来源)

---

## 0. 一句话结论

**在 ARM Android 上跑 Windows x86 游戏的完整技术栈已经成熟到"旗舰手机可玩"的程度（Box64 + Wine + DXVK + Turnip，骁龙 8 Gen 2 上 GTA V 可达 45–60 FPS），但电视盒子的主流芯片（Amlogic S905/S928 的 Mali-G31/G52、RK3588 的 Mali-G610）恰恰是整个链条中最薄弱的一环——GPU 驱动不支持或残血支持 Vulkan，DXVK 无法工作，性能常常跌到不可用。对本项目而言，风险最大的环节不是 CPU 翻译，而是电视芯片的 Vulkan 驱动。若电视硬件可自选，应选骁龙（Adreno）平台或直接采用串流方案。**

---

## 1. 技术栈总览：四层结构

在 ARM Android 上运行一个 Windows x86 DirectX 游戏，指令/API 要经过四层翻译，每一层都有独立的开源项目：

```
┌─────────────────────────────────────────────────────────┐
│  游戏 exe（x86/x64 机器码，调用 Direct3D 9/11 + Win32）   │
├─────────────────────────────────────────────────────────┤
│  第4层 图形 API 翻译：Direct3D → Vulkan                  │
│        DXVK / D8VK / VKD3D / WineD3D                    │
├─────────────────────────────────────────────────────────┤
│  第3层 Windows API 兼容层：Win32/NT API → Linux 系统调用 │
│        Wine（WOW64 模式）                                │
├─────────────────────────────────────────────────────────┤
│  第2层 指令集翻译：x86/x64 机器码 → AArch64 机器码       │
│        Box64(+Box32) / FEX-Emu / QEMU（慢）              │
├─────────────────────────────────────────────────────────┤
│  第1层 宿主环境：Android 上的 Linux 运行环境              │
│        原生 Bionic（Winlator/Mobox glibc）或 proot/chroot │
└─────────────────────────────────────────────────────────┘
         ↓ 最终落到 Android GPU 驱动（Turnip / 厂商 Vulkan / 软件渲染）
```

关键点：**这四层是解耦的**，可以按场景替换任意一层。TeknoParrot 游戏本质上是普通 Windows exe + 一些补丁/I/O 模拟，因此同样适用此栈；街机 I/O 板（JVS、光枪输入）的模拟需要在此栈之外自研一层（把 Android 输入事件转成游戏期望的 DirectInput/RawInput/JVS 数据）。

---

## 2. 指令集翻译层（x86 → ARM）

### 2.1 Box86 / Box64

- **仓库**：[github.com/ptitSeb/box64](https://github.com/ptitSeb/box64)、[github.com/ptitSeb/box86](https://github.com/ptitSeb/box86)
- **定位**：Linux 用户态 x86_64（Box64）/ x86 32 位（Box86）模拟器，是 Android 上跑 Windows 游戏事实上的标准翻译器（Winlator、Mobox、GameHub 全部内置它）。
- **原理**：
  - 核心是 **DynaRec（动态重编译器）**：把 x86/x64 基本块即时翻译成 AArch64（也支持 RV64、LA64），比纯解释器快 **5–10 倍**（[Box64 README](https://github.com/ptitseb/box64)）。
  - **Wrapped libs 机制**：libc、libm、SDL、OpenGL 等常用库不翻译，直接调用 ARM 原生库，大幅降低开销——这是它比 QEMU-user 快的核心原因之一。
  - v0.3.8（2025 年 10 月）新增 **DynaCache**：把翻译后的代码压缩缓存到 `~/.cache/box64`，二次启动游戏显著加快（[box86.org blog](https://box86.org/blog/)）。
- **版本与活跃度**：非常活跃，2024–2025 年密集发布 v0.3.2 → v0.3.8：
  - v0.3.2（2024-12）：引入 **Box32** 选项（让 Box64 直接跑 32 位 x86 程序，不再需要单独的 Box86）、Native Flags 优化（[Phoronix](https://www.phoronix.com/news/Box64-0.3.2-Released)）。
  - v0.3.4（2025-03）：Box32 在 ARM64 上可运行 Steam 客户端；Callret 优化在部分 Wine/WoW64 场景带来 **10–20% 提升**（[Boiling Steam](https://boilingsteam.com/new-box86-and-box64-releases-for-may-2024/)、[cnBeta](https://blog.wongcw.com/2025/03/11/box64-0-3-4-%e5%b7%b2%e7%99%bc%e5%b8%83%ef%bc%9a%e5%9c%a8arm64-%e4%b8%8a%e6%9b%b4%e5%bf%ab%e9%81%8b%e8%a1%8cbox32-%e5%92%8csteam/)）。
  - v0.3.6（2025-06）、v0.3.8（2025-10）：WOW64 支持成熟、DynaCache、大量 dynarec/wrapper 修复（[GitHub releases](https://api.github.com/repos/ptitSeb/box64/releases/latest)、[Ampere 社区实测帖](https://community.amperecomputing.com/t/windows-x86-steam-games-on-arm64-ampere-box64-0-3-6/3011)）。
- **性能损耗**（作者官方横评，RPi400 平台，[box86.org 横评](https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/)）：
  - 纯整数（7z）：Box64 约达原生 AArch64 的 **53%**；FEX 约 26%；QEMU-user 约 16%。
  - SSE 密集（dav1d 视频解码 4 线程）：Box64 约原生 **37%**。
  - GPU 受限场景（glmark2、WorldOfGoo x64）：Box64 ≈ **原生速度**（因为 OpenGL 走 wrapped 直通）。
  - 概括规律：**CPU 整数代码损耗约 40–50%，SSE/AVX 浮点密集代码损耗 60–70%，GPU 受限的游戏接近原生**。
- **SSE/SSE2/AVX 支持**：SSE/SSE2 完整支持（dynarec 直接映射到 NEON）；SSE3–SSE4.2 大部分支持；AVX/AVX2 有支持但仍在完善（部分指令回退到较慢路径）。强内存模型（x86 TSO）模拟通过 `BOX64_STRONGMEM` 等选项权衡性能与兼容性。
- **WOW64 支持**：Box64 v0.3.x 与 Wine WOW64 模式配合，可在纯 64 位 Wine 里跑 32 位 Windows 程序（这是 Winlator 7.1.5 之后 mod 版的主要玩法，见 3.4）。
- **对本项目的意义**：Box64 是必选项。它同时能解决"少量 Linux ELF 游戏"——Box64 本来就是跑 Linux x86 二进制的，ELF 游戏可以直接跑，不必套 Wine。

### 2.2 FEX-Emu

- **仓库**：[github.com/FEX-Emu/FEX](https://github.com/FEX-Emu/FEX)，官网 [fex-emu.com](https://fex-emu.com/)
- **原理**：用户态 x86/x86-64 → AArch64 二进制翻译器，自带 IR（中间表示）和优化 pass，设计上比 Box64 更"学院派"：强调正确性（x86 强内存模型、精确异常）和 32/64 位统一处理。
- **RootFS 方案**：FEX 需要一个 Ubuntu RootFS（首次安装下载约 930MB），内置大部分运行库，开箱即用；支持 OpenGL/Vulkan 直通（thunk 机制）。
- **与 Box64 对比**：
  - 官方横评（上文同一来源）：CPU 性能约为 Box64 的 **40–50%**（7z 1486 vs Box64 3084；dav1d 4t 52.67 vs 116.64）。
  - 第三方在 Cortex-A53 上的测试：原生为基准，Box64 慢 2.4–2.5 倍，**FEX 最慢，慢 3.2 倍**（[printserver.ink 测试](https://printserver.ink/blog/box64-vs-fex/)）。
  - 优势：兼容性更好（尤其强内存模型场景、多线程游戏崩溃更少）、支持 16K 页内核之外的更多内核配置、是 Valve 在 ARM 设备上跑 Proton 的选型方向（ROCKNIX/DroidDeck 等掌机发行版采用 FEX+Proton+DXVK，见 [Held Games 翻译层解析](https://heldgames.com/guides/translation-layers-explained)）。
- **结论**：Android 游戏场景 Box64 生态（调参选项、社区预设）完胜；FEX 更适合桌面级 Linux ARM 设备。**本项目选 Box64，不建议 FEX**。

### 2.3 ExaGear（已死）

- **历史**：俄罗斯 Eltechs 公司 2012 年创立、2014 年发布，是 Box86 出现前在 ARM 上跑 x86+Wine 的唯一可用方案（Android 上跑 32 位 x86 Windows 游戏）。**2019 年 2 月停止服务，2020 年 10 月公司清算，全部源码卖给华为**（[EmuGear Wiki](https://www.exagear.wiki/index.php?title=Eltechs(en))、[HN 讨论](https://news.ycombinator.com/item?id=25749490)）。
- **华为 ExaGear**（2020-09 发布）：仅面向 Linux（openEuler/鲲鹏），只跑 x86 Linux 程序，**不提供 Wine 环境**，闭源且后续无公开更新，不可用于本项目。
- 网上流传的"ExaGear APK"均为 2019 年前的破解改版（如 [XHYN-PH/exagear-302](https://github.com/XHYN-PH/exagear-302)），技术已全面落后于 Box64 方案。**仅作历史参考，不要使用。**

### 2.4 Rosetta 2（仅作参考基准）

苹果 M 系列芯片上的 x86_64 翻译器，是此类技术的天花板参照物：

- 官方横评（M1，7z）：Rosetta 2 达原生 **71%**，同机 Box64 on Asahi Linux 为原生 **57%**（[box86.org 对比](https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/)）。
- Rosetta 2 的优势来源：苹果在 M1 硬件里直接做了 TSO 内存模型开关等硬件级支持，且只翻译一次并持久化缓存。ARM Android 生态没有任何等价物，短期内不会有。
- 意义：证明"x86→ARM 翻译损耗控制在 30% 以内"在工程上可行，但需要芯片厂配合，电视盒子芯片不可能有。

### 2.5 QEMU 全系统/用户态模拟（为何不可行）

- **QEMU-user**（用户态）：无 wrapped-lib 直通机制，所有库都要翻译；无硬件 GL 直通（默认 llvmpipe 软渲染，还在模拟环境里软渲染）。官方横评数据：7z 只有原生 16%；openarena 跑出 **12 秒/帧**（0.083 FPS）；WorldOfGoo 无法启动（[box86.org 横评](https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/)）。
- **QEMU 全系统模拟**（qemu-system-x86_64 模拟整台 PC 装 Windows）：TCG 纯软件翻译 + 虚拟显卡无 3D 加速，x86-on-ARM 场景性能损耗通常 **10–20 倍**，跑 Windows XP 时代的 2D 程序尚可，跑 DirectX 9 3D 游戏完全不可行。KVM 加速只在宿主与客户机同架构时有效，ARM 宿主模拟 x86 客户机**无法使用 KVM**。
- 结论：QEMU 在本场景中仅适合作为"兼容性兜底验证工具"，不是产品方案。

### 2.6 Android x86 镜像路线（为何电视走不通）

- [Android-x86 项目](https://www.android-x86.org)/ Bliss OS 可以把 Android 装到 x86 PC 上，从而用"原生 x86 + Wine/兼容层"跑游戏。
- 但现实是：**市售智能电视和电视盒子几乎 100% 是 ARM 芯片**（Amlogic、瑞芯微、联发科、海思；联想/腾讯等少数 x86 盒子早已停产）。x86 路线意味着放弃电视形态、改用 mini PC——那就不再是"Android 电视方案"，而是串流方案的"主机端"（见第 6 节）。
- 例外参考：如果产品形态允许"盒子即主机"，直接选用 Intel N100 等廉价 x86 小主机跑 Windows/Bazzite 反而比任何翻译栈都稳——这实质上是把问题消灭了，属于第 6 节串流方案的变体。

---

## 3. Windows API 层（Wine 系）

### 3.1 官方 Wine for Android 现状

- WineHQ 官方仍提供 Android APK（[dl.winehq.org/wine-builds/android/](https://dl.winehq.org/wine-builds/android/)），但**基本处于无人维护状态**（[Hangover issue #128：'the Android port of Wine is unmaintained'](https://github.com/AndreRH/hangover/issues/128)）。
- 致命限制：**官方 Wine-Android 只能跑 Windows ARM/ARM64 程序，不能跑 x86 程序**（Wine 不是 CPU 模拟器），而 Windows ARM 软件生态几乎不存在（[WineHQ 论坛](https://forum.winehq.org/viewtopic.php?t=30071)）。
- 图形也残缺：Android 上 Wine 依赖的桌面 OpenGL 缺失，只能用 WineD3D 或外部方案。
- 结论：**官方 wine-android 不可用**，只作技术谱系参考。实际可用的全部是社区整合方案（3.4–3.6）。

### 3.2 Wine 10.x 的 ARM64EC / WOW64 进展

这是 2024–2025 年最重要的底层变化（[Wine 10.0 发布报道](https://soylentnews.org/article.pl?sid=25/01/24/1324218)、[ChicagoVPS 报道](https://www.chicagovps.net/blog/wine-10-0-released-introducing-linux-compatibility-for-arm-windows-applications/)）：

- **Wine 9.0（2024-01）**：完成 WoW64 重构，可在纯 64 位 Wine 中运行 32 位 Windows 程序（不再需要 32 位系统库），这为 ARM64 上"Box64 单翻译器通吃 32/64 位"奠定架构基础。
- **Wine 10.0（2025-01）**：正式支持 **ARM64EC** ABI——Windows 11 上 ARM 原生与 x64 混合代码的 ABI。Wine 现在可以把自身编译为 ARM64EC，在 ARM64 Linux + x86 模拟器（Box64/FEX）下运行 x86 Windows 程序，且**模拟器只需处理游戏 exe 本身，Wine 系统 DLL 全部原生 ARM 运行**，显著减少翻译量。
- **WOW64 模式 + Box64**：Wine 的 WOW64 层把 32 位 Windows 调用转到 64 位宿主，配合 Box64 的 Box32，32 位老游戏（2016 年前街机游戏几乎全是 32 位）不再需要完整 32 位 Linux 库环境——这正是 Winlator 7.1.5 mod 和 Mobox wow64 版的路线。
- 对本项目的意义：**优先选 WOW64 模式 Wine（9.x+ / 10.x），Box64 一个翻译器覆盖全部 x86/x64 目标**。

### 3.3 Hangover

- [github.com/AndreRH/hangover](https://github.com/AndreRH/hangover)：最早的"Wine on ARM 跑 x86"项目（Wine + QEMU/自研翻译混合），由 CodeWeavers 前员工维护，近年转向 ARM64EC 路线，活跃度低、性能落后 Box64 路线。**仅作谱系参考，不推荐。**

### 3.4 Winlator

- **仓库**：[github.com/brunodev85/winlator](https://github.com/brunodev85/winlator)（注意：7.1 之后源码不再更新，后续版本仅发布 APK；社区文档站 [brunodev85-winlator.mintlify.app](https://brunodev85-winlator.mintlify.app/graphics/dxwrappers)）。
- **架构**：Android App（Java/Kotlin UI + 容器管理）内嵌 Ubuntu RootFS（proot 或 glibc bionic 直跑），组件栈 = **Box86/Box64 + Wine + DXVK/D8VK/VKD3D/WineD3D + Mesa（Turnip/VirGL/Zink/Vortek）+ XServer/InputBridge**。
- **版本演进**（[GitHub Releases](https://github.com/brunodev85/winlator/releases)）：
  - v6.x–7.1（2023–2024-06）：基本可用；7.1 更新 Mesa Turnip、改进 DirectInput/XInput 稳定性（[Reddit 发布帖](https://www.reddit.com/r/EmulationOnAndroid/comments/1dlbyrv/winlator_71_released/)）。
  - v9.0（2025-01）：Ubuntu 环境 + Mesa + DXVK/D8VK/VKD3D/CNC-DDraw 全家桶（[发布报道](https://www.altusintel.com/public-yycqc2/)）。
  - v10.0（2025 年中）：任务管理器、GPUInfo、修复 gstreamer；社区 mod 引入 Box32/WOW64 双模式（[foxraing mod 说明](https://foxraing.tistory.com/982)）。
  - v11.x（2025-09 起）：内置 Mesa Turnip 25.x，性能和兼容性继续提升（[设置指南](https://tech-insider.org/ie/winlator-setup-2026/)）。
- **实测表现**：见第 5 节数据表。总体规律：骁龙 8 Gen 2 及以上 + Turnip 驱动，2008–2016 年 DX9 游戏多数可 30–60 FPS。
- **对本项目的意义**：Winlator 证明了完整栈的可行性，但它是"手机应用"形态，其 proot 容器、触摸 UI、授权模式不适合直接做成电视产品；**应借鉴其组件组合，自研 TV 形态整合层**。

### 3.5 GameHub / GameFusion（GameSir）

- 手柄厂商 GameSir（盖世小鸡）出品的商业方案：**GameFusion 是 Windows 模拟器引擎，GameHub 是前端启动器**（[官方说明](https://www.thememorycore.com/gamehub-feb-11-2026/)），累计下载量超 500 万。
- 技术栈与 Winlator 同源（Box64 + Wine + DXVK + Turnip），但**闭源**，社区有"使用了开源代码但未遵守 GPL 开源、强制付费/绑定手柄"的争议（[Reddit 讨论](https://www.reddit.com/r/EmulationOnAndroid/comments/1i9wx2g/gamehub_emulator_is_kind_of_a_disappointment/)）；社区出现去限制 mod（GameHub Lite）。
- 实测（4PDA 用户，SD870 + Adreno 650，wine9.13-x64 + turnip_v26.1.0 + DXVK 2.3.1-async + Box64-0.37）：游戏开局 **45–70 FPS**（[4PDA 帖子](https://4pda.to/forum/index.php?showtopic=1100913&st=18140)）。
- 对本项目的意义：证明"Box64+Wine+DXVK 栈可以做成商业产品"，但其闭源+争议模式不可复制；可作为竞品体验参考。

### 3.6 Mobox / Termux-box 等 Termux 系方案

- **Mobox**（[github.com/olegos2/mobox](https://github.com/olegos2/mobox)）：Termux 原生 glibc 环境（**无 proot 容器开销**）+ Box64 + Wine(WOW64) + Termux-X11 显示 + InputBridge 输入。因为省去 proot，性能比 Winlator 高（社区普遍认为同机高 20–60%，[Grokipedia 对比](https://grokipedia.com/page/Best_Emulators_for_PC_Games_on_Android)、[HN 讨论](https://news.ycombinator.com/item?id=39099387)）。**注意：官方仓库已于 2025-06-20 归档停更**（[Grokipedia Mobox 条目](https://grokipedia.com/page/Mobox)），wow64 分支是其最后的重要更新（[Reddit](https://www.reddit.com/r/EmulationOnAndroid/comments/1am9fli/mobox_wow64_released_32bit_games_performance/)）。
- **Termux-box**（[github.com/olegos2/termux-box](https://github.com/olegos2/termux-box)）：proot 容器里的 Box64+Wine，比 Mobox 慢，早于 Mobox，现已基本停止维护。
- 对本项目的意义：Mobox 的 **glibc bionic 直跑、无 proot** 思路是产品化的正确方向（少一层容器就是 10–30% 性能）；但其 Termux 形态不适合电视，需要把该思路移植进自有 APK。

### 3.7 各方案横向对比

| 方案 | 翻译器 | 容器开销 | 开源 | 维护状态 | 适合产品化？ |
|---|---|---|---|---|---|
| 官方 wine-android | 无（只能跑 ARM 程序） | 无 | 是 | 基本停维护 | 否（跑不了 x86） |
| Winlator | Box64/Box86 | proot/glibc | 7.1 后闭源 | 活跃（11.x） | 否（手机应用形态） |
| Mobox (wow64) | Box64 | 无（bionic glibc） | 是（脚本） | **2025-06 已归档** | 思路值得借鉴 |
| Termux-box | Box64 | proot | 是 | 停滞 | 否 |
| GameHub/GameFusion | Box64 | 闭源 | 否 | 活跃（商业） | 竞品参考 |
| Hangover | QEMU/混合 | 无 | 是 | 低活跃 | 否 |
| **自研整合（推荐）** | Box64+Box32 | 无 | — | — | 是 |

---

## 4. 图形 API 翻译层（DirectX → Vulkan/OpenGL）

### 4.1 DXVK / D8VK / VKD3D

- **DXVK**（[github.com/doitsujin/dxvk](https://github.com/doitsujin/dxvk)）：D3D 9/10/11 → Vulkan，Wine 游戏性能的事实标准（Steam Deck/Proton 同款）。**DXVK 2.0 起强制要求 Vulkan 1.3 驱动**（[PCGamingWiki](https://www.pcgamingwiki.com/wiki/DXVK)）；1.7.2 等旧版只需 Vulkan 1.1——这一点对电视芯片至关重要（见 4.2）。Winlator 内置 0.96 / 1.4.2 / 1.7.2 / 2.2–2.6.1 多个版本可选（[Winlator 文档](https://brunodev85-winlator.mintlify.app/graphics/dxwrappers)）。
- **D8VK**（DXVK 分支）：D3D8 → Vulkan，1.0 起多数 D3D8 游戏可用，**0.10 起同样要求 Vulkan 1.3**（[Wine 文档摘录](https://rootpages.lukeshort.cloud/virtualization/wine.html)）。
- **VKD3D / VKD3D-Proton**：D3D12 → Vulkan。本项目街机游戏几乎不涉及 D3D12，权重低。
- **WineD3D**：D3D 1–11 → OpenGL 的内置路径，性能差但兼容性广；在 Android 上因缺桌面 OpenGL 需配 gl4es/Zink（见 4.4/4.5）。

### 4.2 Android 端 Vulkan 驱动现状：最大瓶颈

DXVK 要工作，底层必须有**可用的 Vulkan 1.3（或至少 1.1 配旧版 DXVK）驱动**。各 GPU 阵营差异极大：

| GPU 阵营 | 典型电视芯片 | Vulkan 支持现状 | DXVK 可行性 |
|---|---|---|---|
| 高通 Adreno 6xx/7xx | 无电视芯片，仅手机/掌机 | **Turnip 开源驱动完整支持 Vulkan 1.3+** | ✅ 完整可行 |
| ARM Mali-G31/G52（Bifrost） | Amlogic S905X3/X4、S905Y4 | 厂商 Android 驱动仅 Vulkan 1.1 且 bug 多；**开源 PanVK 不支持 Bifrost** | ⚠️ 仅旧版 DXVK 1.7.2 勉强，残缺 |
| ARM Mali-G52/G57（Bifrost/Valhall 早期） | Amlogic S905X4/S928X | 同上，厂商驱动质量差 | ⚠️ 同上 |
| ARM Mali-G610/G710（Valhall） | RK3588/S | **开源 PanVK 已过 Vulkan 1.2 一致性（2025-06，Khronos 官方）**；Mesa 25.0 起快速改进 | 🟡 进展快但仍不成熟 |
| PowerVR / Imagination | 部分低端盒子 | 开源驱动刚起步 | ❌ 基本不可行 |

关键事实与来源：

- **PanVK（Mali 开源 Vulkan 驱动，Panfrost 项目的 Vulkan 部分）只支持 Valhall 架构及以后**——即 Mali-G52（部分）/G57/G610/G710 等；**Amlogic 中低端主力 Mali-G31（Bifrost）不在支持列表**。RK3588 的 Mali-G610 于 2025-04 过 Vulkan 1.1 一致性、2025-06 过 **Vulkan 1.2** 一致性（[Khronos 官方公告](https://www.khronos.org/news/archives/panvk-reaches-vulkan-1.2-conformance-on-mali-g610)、[LWN 讨论：实际 device 层面曾只有 1.1](https://lwn.net/Articles/1010975/)）。
- 电视盒子的**厂商闭源 Vulkan 驱动**（Android BSP 自带）普遍只为游戏 UI 验证过，跑 DXVK 这类桌面级 Vulkan 负载会暴露大量未实现的扩展和渲染 bug——这就是 Winlator 社区"非骁龙设备体验灾难"的根源。
- RK3588 是电视/盒子芯片中唯一的亮点：PanVK 活跃开发中，Armbian 社区实测"RK3588 是有能力的游戏芯片，Vulkan 随 PanVK 持续变好"（[Armbian 论坛](https://forum.armbian.com/topic/55217-gaming-experience-with-orange-pi-5-rk3588-on-armbian/)）；但 Mali GPU 的 TBDR 架构模拟传统立即模式渲染存在固有开销（[CSDN 技术分析](https://bbs.csdn.net/weixin_29168393/article/details/100192225)）。
- **结论：若产品允许指定硬件，唯一稳妥选择是骁龙（Adreno + Turnip）平台；若必须支持存量 Amlogic 盒子，DXVK 路线基本被堵死，只能走 WineD3D→gl4es（OpenGL ES）或软件渲染，老 DX9 游戏可玩性需逐款实测。**

### 4.3 Turnip（Adreno 开源 Vulkan 驱动）

- Mesa 项目内的高通 Adreno 开源 Vulkan 驱动，2019 年进入 Mesa（[Phoronix 报道](https://www.phoronix.com/forums/forum/linux-graphics-x-org-drivers/opengl-vulkan-mesa-gallium3d/1086158-turnip-an-open-source-vulkan-driver-for-qualcomm-adreno-hardware-now-in-mesa)），现为 Vulkan 1.3 驱动、支持 Adreno 6xx/7xx（[Mesa 文档](https://docs.mesa3d.org/drivers/freedreno.html)）。
- 在 Android 上，Turnip 以"可替换驱动包"形式被 Winlator/Mobox 直接 dlopen，**绕过手机厂商自带驱动**，对桌面级 Vulkan 负载（DXVK）比厂商驱动快得多且渲染 bug 少；2025 年已更新到 Mesa 25.2.0（[Turnip 25.2.0-RC1 发布记录](https://telemetr.io/en/channels/1686388687/posts?cursor=ICg=)）。
- **这是"只有骁龙设备体验好"的原因**：不是 CPU 差距，而是 Turnip 让 Adreno 成为 Android 上唯一有成熟开源桌面级 Vulkan 驱动的 GPU。

### 4.4 Vortek、VirGL、Zink 等兜底方案

- **Vortek**：Winlator 自研的**通用 Vulkan 驱动桥接器**（非软件渲染器）。Winlator 容器（proot/glibc）无法直接调用 Android bionic 厂商驱动，Vortek 将容器内 Vulkan 调用转发给 Android 原生 Vulkan 驱动，从而让**不支持 Turnip 的设备（包括 Mali、PowerVR）也能用硬件 Vulkan**（[DeepWiki 解析](https://deepwiki.com/winebox64/winlator/6.3-vortek-driver)、[百度贴吧逆向讨论](https://tieba.baidu.com/p/9404213625)）。局限：底层仍是厂商驱动的质量，Mali 上 DXVK 兼容性依旧看运气；它是"通道"不是"驱动修复器"。
- **VirGL / VirGL-overlay**：OpenGL ES 2.0 时代的虚拟化 GL 方案，Winlator/Mobox 早期用于"所有设备保底显示"，只支持到 OpenGL ES 3.x 级别，性能差，新游戏基本不可用；Mobox 文档称"多数手机可用 Mesa VirGL 跑 DirectX 9 游戏"（[Mobox 中文指南](https://blog.csdn.net/gitblog_00123/article/details/152202925)）——**注意这正好覆盖本项目的 DX9 街机游戏**，是 Mali 盒子上的重要兜底。
- **Zink**：OpenGL → Vulkan 的 Mesa 翻译层。方向相反（给只有 Vulkan 的硬件跑 GL），在本栈中用于"WineD3D→OpenGL→Zink→Vulkan"这种多重翻译兜底，叠加损耗大，仅调试用。
- **软件渲染（llvmpipe）**：在已被翻译的 x86 环境里再跑软渲染，双重损耗，openarena 跑出 0.083 FPS 的就是这种组合（[box86.org 横评](https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/)）。不可用于产品，仅验证功能。

### 4.5 老 DirectX 9/8 游戏的轻量路径

本项目游戏多为 2008–2016 年 DX9 游戏，存在几条比 DXVK 更轻的路径，**在 Mali 盒子上可能是唯一可行路径**：

1. **WineD3D → gl4es**：gl4es（Box86 作者 ptitSeb 的 OpenGL → OpenGL ES 翻译库）让 WineD3D 的桌面 GL 调用落到 Android GLES 驱动上，**完全绕开 Vulkan**。性能不如 DXVK，但兼容性在 Mali 盒子上远好。Box86/Box64 原生集成 gl4es。
2. **CNC-DDraw / DDrawCompat**：针对 DirectDraw 和极老 DX 版本的兼容层（Winlator 内置）。
3. **d3d8to9**（[github.com/crosire/d3d8to9](https://github.com/crosire/d3d8to9)）：把 D3D8 调用转成 D3D9 的开源包装器，配合 DXVK 的 D3D9 路径使用。
4. **dgVoodoo2**：著名的 Glide/D3D1–9 → D3D11/12 包装器，在 Wine 下可用来"把 DX9 抬成 DX11 再走 DXVK"，对修复老游戏在新驱动上的渲染 bug 有奇效——**对街机老游戏（常见奇葩渲染路径）特别有价值**，但它本身是 x86 Windows DLL，要在 Wine 内使用，闭源免费。
5. **DXVK 旧版本（1.7.2）**：只需 Vulkan 1.1，是 Mali-G31/G52 上唯一可能跑起来的 DXVK（需实测）。

---

## 5. 实测性能参考数据

### 5.1 翻译层效率基准（来源：[box86.org 官方横评](https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/)，RPi400 / Ubuntu 21.10）

| 测试 | QEMU-user x64 | FEX x64 | Box64 | 原生 AArch64 | Box64/原生 |
|---|---|---|---|---|---|
| 7z（整数） | 931 | 1486 | 3084 | 5787 | **53%** |
| dav1d 4线程（SSE 密集） | 15.65 | 52.67 | 116.64 | 312.23 | **37%** |
| glmark2（GPU 受限） | 4 | 164 | 178 | 181 | **98%** |
| openarena（fps） | 0.083 | 1.8 | 8.4 | 8.4 | **100%** |

另有 Cortex-A53 独立测试：Box64 慢 2.4–2.5 倍、FEX 慢 3.2 倍（[printserver.ink](https://printserver.ink/blog/box64-vs-fex/)）。Rosetta 2 参照：7z 达原生 71%。

### 5.2 Winlator / Mobox 在骁龙平台上的游戏实测

| 游戏 | 芯片 | 设置 | 帧率 | 来源 |
|---|---|---|---|---|
| GTA V | 骁龙 8 Gen 2 (12GB) | Winlator，高画质，DXVK 2.3+ | **45–60 FPS** | [punprime 横评](https://punprime.com/winlator-vs-mobox/) |
| GTA V | 骁龙 8 Gen 2 | Winlator 7.1.5 GPY | 稳定 30 FPS | [dtptips 指南](https://dtptips.com/a-complete-guide-to-winlator-gpy-7-1-5-for-android/) |
| GTA V | 骁龙 778G | Winlator，低画质 | 22–30 FPS | 同上 punprime |
| Fallout 4 | 骁龙 8 Gen 2 多机型 | Winlator | **30–40 FPS** | [Wccftech 报道](https://wccftech.com/fallout-4-running-on-an-android-device/) |
| The Witcher 3 | 骁龙 8 Gen 3 | 720p | 30–40 FPS | [tech-insider 汇总](https://tech-insider.org/android-emulators-play-store-banned-2026/) |
| Far Cry 4 | 骁龙 8 Gen 3 | 中画质 mod | 40–60 FPS | [r/winlator](https://pholder.com/r/winlator/%238/) |
| Ryse: Son of Rome | 骁龙 8+ Gen 1 (Y700 2023) | Mobox wow64，576p | 30 FPS 稳定 | [Reddit 实测](https://www.reddit.com/r/EmulationOnAndroid/comments/1b38tfe/ryse_son_of_rome_on_android_with_gamepad_mobox/) |
| CoD: Black Ops | 骁龙 865 | Winlator 10，600p 低 | 可玩 | [YouTube 实测](https://www.youtube.com/watch?v=skSmYWCn1FU) |
| 通用规律 | 骁龙 8 Gen 2/3 | — | 30–60 FPS；中端 720G 约 20–25 FPS | [Meta AI 汇总](https://www.facebook.com/fb-answers/winlator-android-gaming-snapdragon/) |

注意 Mobox 在重负载游戏中比 Winlator 高 20–60%（[Grokipedia](https://grokipedia.com/page/Best_Emulators_for_PC_Games_on_Android)），证明去掉 proot 容器收益明显。

### 5.3 ARM 电视/盒子芯片实测

- **RK3588（Mali-G610）**：Armbian 社区实测 PanVK 可用且快速改进，Box64+Zink 跑 GL 有性能损耗但可用（[YouTube: Testing Vulkan on Mali-G610 RK3588 Mesa 25.0](https://www.youtube.com/watch?v=vW0AyI70taM)、[Armbian 论坛](https://forum.armbian.com/topic/55217-gaming-experience-with-orange-pi-5-rk3588-on-armbian/)）。尚无公开的"RK3588 上 Winlator 类游戏实测帧率"权威数据——这是本项目需要自行实测补的空白。
- **Amlogic S905X4（Mali-G31）**：没有公开的可信 DXVK 游戏实测；其厂商 Vulkan 驱动仅 1.1 且质量差，PanVK 不支持 Bifrost。社区共识是该档位盒子只能走 VirGL/gl4es 兜底，适合 2D/极轻 3D。
- 旁证：连视频解码都有坑——Amlogic T982 上 Moonlight 的 HEVC 解码耗时 25–35ms（理想值 5–15ms），说明 Amlogic 平台的驱动质量整体堪忧（[moonlight-android issue #1355](https://github.com/moonlight-stream/moonlight-android/issues/1355)）。

---

## 6. 备选路线：串流 / 云游戏

**架构**：游戏在局域网内一台 x86 PC（Windows + TeknoParrot 原生环境）上原生运行，电视只负责解码显示和回传输入。这绕开了本文前 5 节的所有翻译层问题。

- **Sunshine（主机端，[github.com/LizardByte/Sunshine](https://github.com/LizardByte/Sunshine)）+ Moonlight（电视端，[github.com/moonlight-stream/moonlight-android](https://github.com/moonlight-stream/moonlight-android)）**：开源事实标准。支持 H.264/HEVC/AV1，最高 4K HDR 120FPS。
- **延迟数据**：
  - 局域网网络往返：有线 1–3ms、抖动约 1ms（[moonlight-ios issue #643](https://github.com/moonlight-stream/moonlight-ios/issues/643)）。
  - 端到端（编码+网络+解码+显示）最优 **20–35ms**（[百度百科 Moonlight 条目](https://baike.baidu.com/en/item/Moonlight/1684307)）；NVIDIA Shield TV 实测解码延迟可低至 2ms 以内、720p30 下 8–13ms（[NVIDIA 论坛实测](https://www.nvidia.com/en-us/geforce/forums/shield-tv/9/202365/nvidia-gamestream-and-moonlight-latency-test-wired/)、[33rd Square](https://www.33rdsquare.com/how-to-play-pc-games-on-nvidia-shield-tv/)）。
  - **坑在电视端解码器**：Amlogic 芯片上 HEVC 解码耗时可达 25–35ms（[issue #1355](https://github.com/moonlight-stream/moonlight-android/issues/1355)），手柄走蓝牙再加约 10ms。
- **光枪输入的特殊问题**：光枪是**绝对坐标**指向设备，对"按下扳机→画面响应"的延迟比摇杆敏感得多，且需要逐帧精确的光标位置回传。Moonlight 的鼠标回传在部分客户端上有 100ms 级延迟报告（[moonlight-android issue #1245](https://github.com/moonlight-stream/moonlight-android/issues/1245)），指针移动手感问题也持续存在（[moonlight-qt issue #1895](https://github.com/moonlight-stream/moonlight-qt/issues/1895)）。Sinden 等摄像式光枪依赖屏幕边框识别，串流引入的帧延迟会直接降低瞄准精度（[Sinden 评测提到 TV 是主要延迟来源](https://scathingaccuracy.com/sheptro/sinden-lightgun-thoughts/)）。**可行做法：光枪直接 USB/蓝牙接主机 PC（不走电视回传），电视只做纯显示**——此时串流延迟只影响画面（20–35ms，可接受），输入零延迟。
- **评估结论**：串流是**兼容性 100%、性能 100%** 的方案（游戏原生跑），代价是需要一台常驻 PC。对产品化而言，它是"保底旗舰体验"路线；本地翻译栈是"摆脱 PC"路线。两者可共存于同一产品。

---

## 7. 结论与推荐技术栈

针对"2008–2016 年 DirectX 9/11 街机光枪游戏 + ARM Android 电视/盒子"场景：

### 7.1 推荐栈（本地翻译路线，按硬件分两档）

**A 档：硬件可指定（推荐首选）——骁龙平台**

```
游戏 exe (x86 32/64, DX9/11) + TeknoParrot 补丁
  → Box64（WOW64 模式 + Box32，v0.3.6+，开 DynaCache）
  → Wine 10.x（ARM64EC/WOW64 构建）
  → DXVK 2.x（D3D9/11 → Vulkan；D3D8 走 D8VK 或 d3d8to9）
  → Turnip（Mesa 25.x）
  → 自研 I/O 层：Android 输入（光枪 HID/蓝牙/USB）→ Wine DirectInput/RawInput，
    街机 JVS 协议模拟在 Wine 层外实现（对应 TeknoParrot 的 I/O 模拟职能）
  → 自有 TV 启动器 APK（借鉴 Mobox 的 bionic glibc 无容器思路，不走 proot）
```

- 骁龙 8 Gen 2 级别的芯片（或掌机衍生 SoC）对 2008–2016 年 DX9 游戏有 3–6 倍性能余量（参考 GTA IV/ fallout 3 级别的负载可 60FPS），这批街机游戏原生需求远低于 GTA IV，**720p/1080p 满帧可期**。
- "少量 Linux ELF 游戏"直接 Box64 + gl4es/原生 GL 运行，不进 Wine。

**B 档：必须支持存量 Amlogic/RK 盒子（高风险）**

```
  → Box64 + Wine 10.x（同上）
  → 图形降级链（按游戏逐一适配）：
      DXVK 1.7.2（只需 Vulkan 1.1，Mali-G52+/RK3588 PanVK 上试）
      → 失败则 WineD3D → gl4es（OpenGL ES 直通，Mali-G31 保底）
      → 再失败则 VirGL / CNC-DDraw（2D 保底）
  → RK3588 机型优先跟进 PanVK 进展（2025 年已过 Vulkan 1.2 一致性，2026 年有望跑 DXVK 2.x）
```

- B 档必须建立**逐游戏的兼容性数据库**（每款游戏 × 每档芯片 × 图形路径 × 帧率），这是产品化工作量的大头。

### 7.2 串流路线（并行提供）

高端 SKU：Sunshine 主机（x86 PC/迷你主机）+ Moonlight TV 客户端，光枪直接接主机。兼容性、画质、延迟全面最优，作为"完美体验"产品线。

### 7.3 风险最大的环节（按风险排序）

1. **电视芯片 Vulkan 驱动（最高风险）**：Amlogic Mali-G31/G52 没有可用的桌面级 Vulkan 驱动，DXVK 基本不可用；这决定了"存量盒子"目标的可行性。缓解：降级到 gl4es 路径，或放弃低端盒子。
2. **强内存模型 / 多核并发 bug**：x86 TSO 模拟在 4 核以上弱内存 ARM 芯片上会引发随机崩溃（Box64 STRONGMEM 选项可调，但伤性能）。
3. **反作弊/DRM 与 TeknoParrot 补丁的兼容性**：街机 dump 常带自定义保护，Box64 的 SSE/自修改代码边界情况需逐款验证。
4. **光枪输入延迟与校准**：本地栈延迟低，但需自研 JVS/I/O 模拟层并逐游戏校准；这是差异化工作量，也是护城河。
5. **散热**：电视盒子无主动散热，Box64 翻译 + 游戏双负载下 S905X4 级芯片会降频，实测帧率需以 30 分钟持续运行为准。

---

## 8. 风险清单（速查表）

| 环节 | 风险等级 | 说明 |
|---|---|---|
| Box64 CPU 翻译 | 低 | 成熟，性能可预测（整数 ~50% 原生） |
| Wine 10 WOW64 | 低 | 官方主线已支持，社区验证充分 |
| DXVK on Adreno/Turnip | 低 | 事实标准路径 |
| DXVK on Mali（G31/G52） | **极高** | 厂商驱动不足，PanVK 不支持 Bifrost |
| DXVK on RK3588（PanVK） | 中高 | 进展快但未成熟（2025-06 过 Vulkan 1.2） |
| gl4es/WineD3D 兜底 | 中 | 可行但需逐游戏适配 |
| 光枪 I/O 自研层 | 中 | 工作量可控，需逐游戏校准 |
| 串流方案 | 低 | 成熟方案，光枪建议直连主机 |
| TeknoParrot 兼容性 | 中 | 需逐款验证补丁在 Wine 下行为 |

---

## 9. 参考来源

**指令集翻译层**
- Box64 仓库与 README：https://github.com/ptitSeb/box64
- Box64 v0.3.6 release（GitHub API）：https://api.github.com/repos/ptitSeb/box64/releases/latest
- Box64 v0.3.2 报道（Phoronix）：https://www.phoronix.com/news/Box64-0.3.2-Released
- Box64 v0.3.4 / Box32 / Steam 报道（cnBeta 转载）：https://blog.wongcw.com/2025/03/11/box64-0-3-4-%e5%b7%b2%e7%99%bc%e5%b8%83%ef%bc%9a%e5%9c%a8arm64-%e4%b8%8a%e6%9b%b4%e5%bf%ab%e9%81%8b%e8%a1%8cbox32-%e5%92%8csteam/
- Box64 v0.3.8 / DynaCache（box86.org blog）：https://box86.org/blog/
- Box86/Box64 vs QEMU vs FEX vs Rosetta2 官方横评：https://box86.org/2022/03/box86-box64-vs-qemu-vs-fex-vs-rosetta2/
- Box64 vs FEX（Cortex-A53 独立测试）：https://printserver.ink/blog/box64-vs-fex/
- FEX-Emu 官网：https://fex-emu.com/
- FEX-Emu 介绍（OSTechNix）：https://ostechnix.com/fex-emu-run-x86-and-x86-64-apps-on-arm64-linux-devices/
- ExaGear/Eltechs 历史（EmuGear Wiki）：https://www.exagear.wiki/index.php?title=Eltechs(en)
- ExaGear 停售与华为收购讨论（HN）：https://news.ycombinator.com/item?id=25749490
- 华为 ExaGear 分析（loongson.xyz）：http://loongson.xyz/2020/10/05/%e7%ae%80%e5%8d%95%e5%88%86%e6%9e%90%e5%8d%8e%e4%b8%ba%e7%9a%84exagear%e5%92%8celtechs%e5%85%ac%e5%8f%b8%e7%9a%84exagear%e6%98%af%e4%b8%8d%e6%98%af%e4%b8%80%e4%b8%aa%e4%b8%9c%e8%a5%bf/
- Ampere 平台 Box64 + Wine 跑 Windows 游戏实测：https://community.amperecomputing.com/t/windows-x86-steam-games-on-arm64-ampere-box64-0-3-6/3011

**Windows API 层**
- Wine 10.0 ARM64EC 报道（SoylentNews）：https://soylentnews.org/article.pl?sid=25/01/24/1324218
- Wine 10.0 发布报道（ChicagoVPS）：https://www.chicagovps.net/blog/wine-10-0-released-introducing-linux-compatibility-for-arm-windows-applications/
- Wine on Android 无人维护（Hangover issue #128）：https://github.com/AndreRH/hangover/issues/128
- WineHQ 论坛：Wine-Android 不能跑 x86：https://forum.winehq.org/viewtopic.php?t=30071
- Winlator 仓库与 Releases：https://github.com/brunodev85/winlator/releases
- Winlator 7.1 发布（Reddit）：https://www.reddit.com/r/EmulationOnAndroid/comments/1dlbyrv/winlator_71_released/
- Winlator 9.0 发布报道：https://www.altusintel.com/public-yycqc2/
- Winlator 图形包装器官方文档：https://brunodev85-winlator.mintlify.app/graphics/dxwrappers
- Winlator Vortek 驱动解析（DeepWiki）：https://deepwiki.com/winebox64/winlator/6.3-vortek-driver
- Winlator Box32 mod 说明（foxraing）：https://foxraing.tistory.com/982
- GameHub/GameFusion 官方访谈（The Memory Core）：https://www.thememorycore.com/gamehub-feb-11-2026/
- GameHub 争议（Reddit）：https://www.reddit.com/r/EmulationOnAndroid/comments/1i9wx2g/gamehub_emulator_is_kind_of_a_disappointment/
- GameHub 实测 45–70 FPS（4PDA）：https://4pda.to/forum/index.php?showtopic=1100913&st=18140
- Mobox 仓库（已归档）：https://github.com/olegos2/mobox
- Termux-box 仓库：https://github.com/olegos2/termux-box
- Mobox 归档停更说明（Grokipedia）：https://grokipedia.com/page/Mobox
- Mobox wow64 发布（Reddit）：https://www.reddit.com/r/EmulationOnAndroid/comments/1am9fli/mobox_wow64_released_32bit_games_performance/
- Mobox vs Winlator 性能（LibHunt）：https://www.libhunt.com/compare-mobox-vs-winlator

**图形层**
- DXVK 仓库：https://github.com/doitsujin/dxvk
- DXVK 2.0 需 Vulkan 1.3（PCGamingWiki）：https://www.pcgamingwiki.com/wiki/DXVK
- Turnip 进入 Mesa（Phoronix）：https://www.phoronix.com/forums/forum/linux-graphics-x-org-drivers/opengl-vulkan-mesa-gallium3d/1086158-turnip-an-open-source-vulkan-driver-for-qualcomm-adreno-hardware-now-in-mesa
- Mesa Freedreno/Turnip 文档：https://docs.mesa3d.org/drivers/freedreno.html
- PanVK 过 Vulkan 1.2 一致性（Khronos）：https://www.khronos.org/news/archives/panvk-reaches-vulkan-1.2-conformance-on-mali-g610
- PanVK 1.4 争议（LWN）：https://lwn.net/Articles/1010975/
- RK3588 PanVK 技术分析（CSDN）：https://bbs.csdn.net/weixin_29168393/article/details/100192225
- RK3588 游戏体验（Armbian 论坛）：https://forum.armbian.com/topic/55217-gaming-experience-with-orange-pi-5-rk3588-on-armbian/
- Vortek 逆向讨论（百度贴吧）：https://tieba.baidu.com/p/9404213625
- Turnip 25.2.0-RC1 发布（Telegram 镜像）：https://telemetr.io/en/channels/1686388687/posts?cursor=ICg=

**性能实测**
- Winlator vs Mobox FPS 横评（punprime）：https://punprime.com/winlator-vs-mobox/
- Fallout 4 on 骁龙 8 Gen 2（Wccftech）：https://wccftech.com/fallout-4-running-on-an-android-device/
- Winlator GPY 7.1.5 GTA V 30FPS（dtptips）：https://dtptips.com/a-complete-guide-to-winlator-gpy-7-1-5-for-android/
- Ryse on Mobox wow64（Reddit）：https://www.reddit.com/r/EmulationOnAndroid/comments/1b38tfe/ryse_son_of_rome_on_android_with_gamepad_mobox/
- Android 模拟器芯片横评（tech-insider）：https://tech-insider.org/android-emulators-play-store-banned-2026/
- 翻译层栈组成解析（Held Games）：https://heldgames.com/guides/translation-layers-explained

**串流**
- Moonlight Android 仓库：https://github.com/moonlight-stream/moonlight-android
- Sunshine 仓库：https://github.com/LizardByte/Sunshine
- Moonlight 延迟 1–3ms（moonlight-ios issue #643）：https://github.com/moonlight-stream/moonlight-ios/issues/643
- Moonlight 端到端 20–35ms（百度百科）：https://baike.baidu.com/en/item/Moonlight/1684307
- NVIDIA Gamestream/Moonlight 延迟实测（NVIDIA 论坛）：https://www.nvidia.com/en-us/geforce/forums/shield-tv/9/202365/nvidia-gamestream-and-moonlight-latency-test-wired/
- Amlogic 解码延迟问题（moonlight-android issue #1355）：https://github.com/moonlight-stream/moonlight-android/issues/1355
- Moonlight 鼠标延迟问题（issue #1245）：https://github.com/moonlight-stream/moonlight-android/issues/1245
- Sinden 光枪评测（TV 延迟）：https://scathingaccuracy.com/sheptro/sinden-lightgun-thoughts/
