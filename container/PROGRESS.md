# ParrotDroid 容器线开发进度

> 方案来源：D:\yinwu\docs\teknoparrot-android\00-总体落地方案.md 第 3 章。
> 本阶段目标：Winlator 本机可构建 + 集成点调研（文档 09）+ 容器任务文档（文档 10）。

## 任务状态

- [x] C0.1 Winlator 源码克隆到 container\winlator-src 并本机构建出 APK
  - 2026-10-07 完成。主仓 commit `b6b2259`（v11.2）；产物 `winlator-src\app\app\build\outputs\apk\debug\app-debug.apk`（150.2 MB，包名 com.winlator，minSdk 26，arm64-v8a）。详见 BUILD.md。
- [x] C0.2 架构调研：文档 09（输入注入/游戏启动/图形栈集成点，源码级）
- [x] C0.3 容器任务文档：文档 10（分阶段任务/验收/硬件清单）
- [ ] C1（后续）ParrotDroid App 骨架：fork Winlator 裁剪 + ContainerSink 对接 + 边框叠加
- [ ] C2（后续，需骁龙 8 系设备）真机跑通首款游戏

## 工作日志

- 2026-10-07 容器线启动。目标与定时续跑已建立。
- 2026-10-07 C0.1 完成：Winlator 源码（含 3 个 submodule）克隆至 container\winlator-src，`gradlew assembleDebug` 构建成功，产出 app-debug.apk（150.2 MB，com.winlator 11.2，minSdk 26，arm64-v8a）。关键坑：Gradle 工程在 submodule `app/` 下；需完整 JDK 17（用 VS 自带的 `C:\Program Files (x86)\Android\openjdk\jdk-17.0.12`）；gradle.properties 需加 `org.gradle.jvmargs=-Xmx8g` 否则压缩 assets 时 OOM；NDK r24/CMake 3.22.1 由 AGP 自动安装。复跑命令见 BUILD.md。
