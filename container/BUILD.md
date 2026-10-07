# Winlator 本机构建指南（C0.1）

> 记录日期：2026-10-07。源码位置：`D:\yinwu\container\winlator-src`。
> **构建已成功**：`app-debug.apk`（150.2 MB，com.winlator 11.2）。本文记录完整流程与踩坑。

## 1. 源码信息

- 主仓库：https://github.com/brunodev85/winlator （浅克隆，`--depth 1`）
  - commit：`b6b2259158cf38d06067c34430d840d56b46d220`（2026-09-22，main，版本 11.2）
- **注意：主仓库本身不含 Gradle 工程**，真正的 Android 工程在 git submodule 里，必须初始化：

  | submodule | 路径 | 仓库 | 锁定 commit |
  |---|---|---|---|
  | winlator-app | `winlator-src/app` | brunodev85/winlator-app | `a030f552` |
  | vortek | `winlator-src/vortek` | brunodev85/vortek | `b1730c5d` |
  | gladio | `winlator-src/gladio` | brunodev85/gladio | `116c0d14` |

  ```bash
  cd /d/yinwu/container/winlator-src
  git submodule update --init --depth 1
  ```

- Gradle 工程根目录是 **`winlator-src/app/`（即 winlator-app 子模块）**，不是 `winlator-src/`。
- Box64 / Wine / DXVK / rootfs 等大二进制已打包进 `app/src/main/assets/`（约 143MB，tzst 压缩包），**无需额外下载**。
- `installable_components/`（60MB）为可选组件，构建不依赖。

## 2. 构建配置要点（winlator-src/app）

| 项 | 值 | 来源 |
|---|---|---|
| Gradle | 8.14.5 | `gradle/wrapper/gradle-wrapper.properties` |
| AGP | 8.4.2 | `build.gradle`（顶层） |
| JDK 要求 | 17+（含 javac，JRE 不行） | AGP 8.x 硬性要求 |
| compileSdk | 35（AGP 8.4.2 只官方支持到 34，仅警告不报错） | `app/build.gradle` |
| minSdk 26 / targetSdk 28 / 仅 arm64-v8a / debug 也开 minify | 同上 | 同上 |
| NDK | 24.0.8215888（r24），`ndkVersion` 写死 | 同上 |
| CMake | 3.22.1，externalNativeBuild（`app/src/main/cpp/CMakeLists.txt`） | 同上 |
| 包名 | `com.winlator`，versionName 11.2，versionCode 33 | 同上 |
| Maven 仓库 | mavenCentral + google（直连可达，无需镜像） | 顶层 `build.gradle` |

native 部分编译 6 个模块：winlator、vortekrenderer、virglrenderer、midihandler、libadrenotools、gladiorenderer，是核心功能（Vulkan 包装/图形渲染），不能跳过。

## 3. 本机环境（关键发现）

| 依赖 | 本机情况 |
|---|---|
| Android SDK | `C:\Users\xsl\AppData\Local\Android\Sdk`（platforms 31/33/35，build-tools 30~36） |
| **JDK 17** | **`C:\Program Files (x86)\Android\openjdk\jdk-17.0.12`（VS/Xamarin 自带的 Microsoft OpenJDK 17，含 javac）**。注意 `C:\Program Files\Android\jdk\jdk-8.0.302.8-hotspot` 是 JDK 8、`D:\Tools\jdk-17.0.20.1+1-jre` 只是 JRE（无 javac），都不能用 |
| NDK r24 | **不用手动装**：AGP 构建时自动下载安装到 `Sdk\ndk\24.0.8215888`（dl.google.com 直连可达） |
| CMake 3.22.1 / Build-Tools 34 | 同样由 AGP 自动安装 |

## 4. 构建命令（复跑）

`winlator-src/app/local.properties` 已写好：

```
sdk.dir=C:\\Users\\xsl\\AppData\\Local\\Android\\Sdk
```

`winlator-src/app/gradle.properties` 已加堆内存（必需，见坑 2）：

```
org.gradle.jvmargs=-Xmx8g
```

```bash
cd /d/yinwu/container/winlator-src/app
export JAVA_HOME='C:\Program Files (x86)\Android\openjdk\jdk-17.0.12'
bash gradlew assembleDebug --console=plain
```

产物：`D:\yinwu\container\winlator-src\app\app\build\outputs\apk\debug\app-debug.apk`

验证：

```bash
"/c/Users/xsl/AppData/Local/Android/Sdk/build-tools/35.0.0/aapt2" dump badging \
  /d/yinwu/container/winlator-src/app/app/build/outputs/apk/debug/app-debug.apk | head -3
```

## 5. 本次构建结果（2026-10-07）

- **BUILD SUCCESSFUL**，增量后全量约 40s（首次含 Gradle/依赖下载约 18min）。
- APK：`app-debug.apk`，**157,549,207 字节（≈150.2 MB）**
- aapt2 确认：`package: name='com.winlator' versionCode='33' versionName='11.2'`，`minSdkVersion:'26'`，`targetSdkVersion:'28'`，`native-code: 'arm64-v8a'`，label `Winlator`。

## 6. 坑与解法

1. **克隆后 `app/` 是空目录**：Gradle 工程在 submodule 里，必须 `git submodule update --init --depth 1`；且工程根在 `winlator-src/app/` 而非 `winlator-src/`。
2. **`:app:compressDebugAssets` 报 `Java heap space`（OOM）**：assets 有 143MB 大文件，Gradle 默认堆 512MB 不够。解法：`gradle.properties` 加 `org.gradle.jvmargs=-Xmx8g`（本机 64GB 内存）。
3. **JRE 17 不能构建**：报 `Toolchain installation ... does not provide the required capabilities: [JAVA_COMPILER]`。必须指向含 javac 的完整 JDK 17（本机用 VS 自带的 `C:\Program Files (x86)\Android\openjdk\jdk-17.0.12`）。
4. **NDK/CMake 缺失不用慌**：`ndkVersion`/`cmake.version` 写死后 AGP 会自动从 dl.google.com 下载安装，无需 sdkmanager。
5. 无害警告可忽略：compileSdk=35 超 AGP 8.4.2 官方测试范围（可用 `android.suppressUnsupportedCompileSdk=35` 消除）；vortekrenderer 有 900+ 条 const 限定符 C 警告；debug 开 minify 的提示；`extractNativeLibs` 提示建议 `useLegacyPackaging`。
6. 若网络慢：Gradle 发行版可换 `https://mirrors.cloud.tencent.com/gradle/gradle-8.14.5-bin.zip`；Maven 可加 `https://maven.aliyun.com/repository/google` 和 `/central`。本次直连均可用。
