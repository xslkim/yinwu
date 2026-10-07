# ParrotDroid 开发用 Android 模拟器环境（EMULATOR.md）

> 本文档记录本机（Windows + Git Bash）Android 模拟器环境的搭建步骤、实测结论与复跑命令。
> 供后续开发者（AI Agent）照做。所有命令均在 **Git Bash** 下执行。
> 搭建日期：2026-10-07，全部步骤已在本机实测通过。

## 0. 结论速查

| 项 | 值 |
|---|---|
| 主 SDK | `C:\Users\xsl\AppData\Local\Android\Sdk`（Git Bash: `"$LOCALAPPDATA/Android/Sdk"`） |
| 系统镜像 | `system-images;android-34;google_apis;x86_64`（Android 14, API 34, 含 ARM 转译） |
| AVD 名 | `parrotdroid-dev`（pixel_6，RAM 4096MB，10G data 分区） |
| 加速方案 | **WHPX**（Windows Hypervisor Platform，已启用可用，`-accel-check` 通过） |
| 端口坑 | Hyper-V/winnat 占用了默认端口段 5554-5585，**必须用 `-ports 5612,5613` 启动**（见 §4） |
| adb 连接 | `adb connect localhost:5613`（非默认扫描端口，需显式 connect） |
| host→guest TCP | `adb forward tcp:18000 tcp:8000`（实测通过） |
| host→guest UDP | **adb forward 不支持 UDP**；用模拟器控制台 `redir add udp:18000:8000`（实测通过，见 §6） |

## 1. 环境前提（已具备）

- 主 SDK：`$LOCALAPPDATA/Android/Sdk`，含 build-tools / emulator 35.4.9 / ndk / platforms(35) / platform-tools / cmdline-tools(latest，本次补装) / system-images(android-34，本次安装)。
- 副 SDK（VS/Xamarin）：`C:\Program Files (x86)\Android\android-sdk`，其 `cmdline-tools/12.0/bin/sdkmanager.bat` 可用于给主 SDK 装包（`--sdk_root` 指定主 SDK）。
- JDK17：`C:\Program Files (x86)\Android\openjdk\jdk-17.0.12`。
  - **坑**：新版 cmdline-tools 的 avdmanager.bat 对含空格+括号路径的 JAVA_HOME 解析失败（报乱码错）。解法：建无空格 junction（只需建一次）：
    ```bat
    mklink /J C:\jdk17 "C:\Program Files (x86)\Android\openjdk\jdk-17.0.12"
    ```
    之后统一 `export JAVA_HOME='C:\jdk17'`。
- dl.google.com 直连可达，镜像下载（约 1GB，解压后 4.2GB）实测约 50 秒完成。

## 2. 一次性安装步骤（复跑命令）

```bash
export JAVA_HOME='C:\jdk17'
MAIN_SDK="$LOCALAPPDATA/Android/Sdk"
VSMGR="/c/Program Files (x86)/Android/android-sdk/cmdline-tools/12.0/bin/sdkmanager.bat"

# 2.1 接受 licenses（非交互）
yes | "$VSMGR" --sdk_root="C:\\Users\\xsl\\AppData\\Local\\Android\\Sdk" --licenses

# 2.2 安装系统镜像（1GB+，慢网放后台）
"$VSMGR" --sdk_root="C:\\Users\\xsl\\AppData\\Local\\Android\\Sdk" \
  "system-images;android-34;google_apis;x86_64"

# 2.3 给主 SDK 装 cmdline-tools（副 SDK 的 avdmanager 只认副 SDK，无法为主 SDK 建 AVD）
"$VSMGR" --sdk_root="C:\\Users\\xsl\\AppData\\Local\\Android\\Sdk" "cmdline-tools;latest"

# 2.4 创建 AVD（用主 SDK 自己的 avdmanager；echo no = 不定制硬件问答）
echo no | "$MAIN_SDK/cmdline-tools/latest/bin/avdmanager.bat" create avd \
  -n parrotdroid-dev \
  -k "system-images;android-34;google_apis;x86_64" \
  -d "pixel_6" -f
```

### 2.5 创建后必须手改 `~/.android/avd/parrotdroid-dev.avd/config.ini`

avdmanager 生成的配置有两处要修：

```ini
avd.id=parrotdroid-dev        # 生成的是 <build>，必须改，否则无法启动
avd.name=parrotdroid-dev      # 同上
hw.ramSize=4096               # 生成的是 2G；且必须写纯数字 MB，写 "4G" 会被回落到 2048
hw.initialOrientation=landscape  # 可选；实际开屏仍可能被 launcher 强制竖屏，横屏建议 App 侧强制
```

其他镜像变体选择说明：优先 `google_apis`（不要 `google_apis_playstore`，后者限制多、不可 root/remount）。android-34 不可得时可换 `android-35` 或 `android-31` 的 google_apis x86_64。

## 3. 加速方案结论：WHPX 可用

```bash
"$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe" -accel-check
# 输出：WHPX(10.0.26200) is installed and usable.
```

- 本机 WHPX（Windows 虚拟机监控程序平台）已启用，模拟器实测全速运行（冷启动到 boot_completed 约 30-60 秒）。
- **坑**：`-accel whpx` 不是合法参数（合法值只有 `on|off|auto`）。直接不传 `-accel`（auto）即自动选 WHPX，启动日志可见 `Windows Hypervisor Platform accelerator is operational`。
- 若他机 `-accel-check` 不支持任何加速器：在"启用或关闭 Windows 功能"中勾选 **Windows 虚拟机监控程序平台**（需管理员+重启）；或与 Hyper-V 二选一冲突时优先保 WHPX。

## 4. 关键坑：默认端口被 Hyper-V 占用 → 必须自定义端口

**现象**：`emulator -avd parrotdroid-dev` 直接启动，秒退，日志报
`ERROR | It seems too many emulator instances are running on this machine. Aborting.`
——这是**误导性报错**，真实原因是模拟器扫描的 console/adb 端口段（5554-5584）全部不可用。

**根因**：`netsh interface ipv4 show excludedportrange protocol=tcp` 可见 Hyper-V/winnat 保留了 `5511-5610` 段，把 5554-5585 整个盖住，模拟器无端口可绑。

**解法（本机采用）**：显式指定空闲端口对（console 必须偶数，adb=console+1）：

```bash
# 启动（窗口版）
"$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe" -avd parrotdroid-dev -no-snapshot-save -ports 5612,5613

# 启动（无头版，CI/验证用）
"$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe" -avd parrotdroid-dev -no-window -no-snapshot-save -no-audio -ports 5612,5613
```

adb 默认只扫描 5555-5585，**找不到 5613，必须显式连接**：

```bash
ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"
$ADB connect localhost:5613
$ADB devices        # 应看到 emulator-5612 device
```

**备选解法（需管理员，未采用）**：`net stop winnat && net start winnat` 重新洗牌排除段，可能让出 5554；或 `netsh int ipv4 add excludedportrange ...` 反向预留。重启后排除段会重排，此问题可能复发，因此推荐固定用 `-ports 5612,5613` 写进脚本。

**另一坑**：启动失败会在 AVD 目录留 `multiinstance.lock`，再次启动报同样错。清理：
`rm -f ~/.android/avd/parrotdroid-dev.avd/*.lock`

## 5. 启动验证流程（实测通过）

```bash
ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"
$ADB connect localhost:5613
$ADB -s emulator-5612 wait-for-device
# 轮询直到 sys.boot_completed=1（本机约 30-60 秒）
$ADB -s emulator-5612 shell getprop sys.boot_completed   # => 1
```

本机 2026-10-07 实测输出：

```
ro.build.version.release = 14
ro.build.version.sdk = 34
ro.product.cpu.abi = x86_64
ro.product.model = sdk_gphone64_x86_64
ro.build.fingerprint = google/sdk_gphone64_x86_64/emu64xa:14/UE1A.230829.050/12077443:userdebug/dev-keys
```

开屏截图留档：`container/emulator-boot.png`（Android 14 桌面）。
验证完关无头实例：`$ADB -s emulator-5612 emu kill`

**注意 ABI**：ParrotDroid APK 目前仅 arm64-v8a，本镜像为 x86_64。API 30+ 的 google_apis x86_64 镜像自带 ARM→x86 二进制转译（libndk_translation），理论可装 arm64-only APK，但**首次安装时务必实测确认**（`adb install` 看是否报 `INSTALL_FAILED_NO_MATCHING_ABIS`；若失败则需为 APK 补 x86_64 ABI 或换用 arm64 主机）。

## 6. 端口转发：闭环测试的关键通路

### 6.1 host→guest TCP：`adb forward`（首选，简单持久）

```bash
$ADB -s emulator-5612 forward tcp:18000 tcp:8000   # 宿主 18000 → 模拟器内 App 的 8000
$ADB -s emulator-5612 forward --list               # 查看
$ADB -s emulator-5612 forward --remove tcp:18000   # 移除
```

实测通过：模拟器内 `nc -l -p 8000`，宿主 PowerShell TcpClient 连 `127.0.0.1:18000`，收到 guest 数据。
**限制：adb forward 只支持 TCP**（以及 localabstract/localfilesystem 等 unix socket），**不支持 UDP**。

### 6.2 host→guest UDP：模拟器控制台 `redir`（实测通过）

`adb reverse` 方向相反（guest→host）不能用；guest 内 `10.0.2.2` 是 guest→host 的别名也不能用于 host→guest。正确做法是 **emulator console 的 redir**（同时支持 tcp 和 udp）：

1. 连接控制台：console 端口 = 启动 `-ports` 的第一个值（本方案 5612；默认方案是 5554）。
   认证 token 在 `C:\Users\xsl\.emulator_console_auth_token`（每次模拟器启动会重新生成）。
2. 发送命令：
   ```
   auth <token>
   redir add udp:18000:8000     # 宿主 UDP 18000 → guest UDP 8000
   redir add tcp:18001:8001
   redir list                   # 确认：ipv4 udp:18000 => 8000
   ```
3. 已封装成脚本 `container/emu-console.ps1`：
   ```bash
   TOKEN=$(cat ~/.emulator_console_auth_token)
   powershell -NoProfile -ExecutionPolicy Bypass -File emu-console.ps1 \
     -Port 5612 -Token "$TOKEN" -Commands "redir add udp:18000:8000;redir list;redir list"
   ```
   （脚本按命令逐条发，响应比命令滞后一拍，最后多发一条空命令/重复命令冲刷即可。）

实测通过：宿主 PowerShell UdpClient 发 `127.0.0.1:18000`，guest 内 `timeout 15 nc -u -l -p 8000 > /data/local/tmp/udpout` 收到原文。

**redir 注意事项**：
- redir 是**运行时状态，模拟器重启即失效**，每次启动模拟器后要重放。
- redir 的宿主端口不能与 `adb forward` 或其他程序已占端口冲突（报 `KO: can't setup redirection, port probably used`）。
- guest 侧监听必须绑 `0.0.0.0`（绑 127.0.0.1 收不到 redir 转发的包）。
- UDP 测试建议固定端口对 `udp:18000:8000`，与 TCP 的 `tcp:18000/tcp:8000` 命名习惯保持一致。
- 也可在启动时用 QEMU 参数一次配好（免控制台）：`emulator ... -qemu -redir udp:18000::8000`，等价于上面的 `redir add udp:18000:8000`。

### 6.3 guest→host（反向，顺便记录）

guest 内访问宿主机直接用别名 `10.0.2.2`；或 `adb reverse tcp:8000 tcp:8000`（guest 的 8000 → 宿主的 8000，仅 TCP）。

## 7. 常用命令速查

```bash
EMU="$LOCALAPPDATA/Android/Sdk/emulator/emulator.exe"
ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"

$EMU -list-avds                                  # 列出 AVD
$EMU -avd parrotdroid-dev -ports 5612,5613 -no-snapshot-save          # 窗口启动
$EMU -avd parrotdroid-dev -ports 5612,5613 -no-window -no-snapshot-save -no-audio  # 无头启动
$ADB connect localhost:5613
$ADB -s emulator-5612 shell getprop sys.boot_completed
$ADB -s emulator-5612 install path/to/app-debug.apk
$ADB -s emulator-5612 exec-out screencap -p > screen.png             # 截图（无头也可以）
$ADB -s emulator-5612 emu kill                                       # 关模拟器
rm -f ~/.android/avd/parrotdroid-dev.avd/*.lock                      # 清残留锁
```

## 8. 故障排查记录（本次踩坑汇总）

| 现象 | 根因 | 解法 |
|---|---|---|
| avdmanager 报 `Package path is not valid / null` | 用了副 SDK 的 avdmanager，它只认副 SDK | 给主 SDK 装 cmdline-tools;latest，用主 SDK 的 |
| avdmanager 乱码错、找不到 java | JAVA_HOME 含 `(x86)` 括号，bat 解析炸 | `mklink /J C:\jdk17 ...` 后用 C:\jdk17 |
| AVD 无法启动，config 里 `avd.id=<build>` | avdmanager 生成 bug | 手改成 AVD 名（§2.5） |
| 日志 `Increasing RAM size to 2048MB` | `hw.ramSize=4G` 不被识别 | 写纯数字 `hw.ramSize=4096` |
| `-accel whpx` 报错 | 合法值仅 on/off/auto | 不传 `-accel`，auto 自动选 WHPX |
| `too many emulator instances` 秒退 | Hyper-V 排除段 5511-5610 盖住 5554-5585 | `-ports 5612,5613` + `adb connect localhost:5613`（§4） |
| 二次启动仍报 too many | 上次失败留 `multiinstance.lock` | 删 AVD 目录下 *.lock |
| adb devices 为空但模拟器在跑 | adb 只扫 5555-5585 | `adb connect localhost:5613` |
| `redir add` 报 port probably used | 与 adb forward 占了同一宿主端口 | 换端口或先 `adb forward --remove` |
