# R1 调研：一人开发光枪游戏的引擎与技术栈选型

> 调研日期：2026 年（以实际执行为准）；调研方式：WebSearch + GitHub API 实时查询（star 数为查询当时值）
> 标注约定：**[已核实]** = 查到官方页面/GitHub 实时数据；**[估计]** = 经验判断或未找到一手来源
> 目标场景：一人独立开发，3D 光枪/rail shooter，一局 3-5 分钟，PC 先行 + 后续 Android 电视，手机当光枪（UDP 坐标流 + HTTP 事件），抖音直播演示 + 蓝牙枪套装绑售。

---

## 一、引擎选型：Godot 4 vs Unity vs Unreal

### 1.1 结论先行

**推荐：Godot 4（用 4.6 稳定版，GDScript 标准版）。** 备选：Unity（仅限你已有 C#/Unity 经验的情况）。不推荐 Unreal。

### 1.2 三引擎对比

| 维度 | Godot 4.6 | Unity 6 | Unreal Engine 5 |
|---|---|---|---|
| 授权费 | **完全免费，MIT 开源，无分成、无收入上限、无强制登录** [已核实] | Personal 免费（年收入/融资 < 20 万美元）；超限必须买 Pro，2025 价 2,200 美元/席/年，2026 起涨至 2,310 美元 [已核实] | 免费使用，游戏总营收超 100 万美元后抽成 5% [已核实] |
| 编辑器体积/启动 | 约 100 MB 单文件，秒开 [估计] | 数 GB，需 Unity Hub + 账号登录激活 | 数十 GB，需 Epic 账号 |
| 学习曲线 | GDScript 类似 Python，一人全栈迭代最快 [估计] | C#，教程和中文资源三家中最多 | 蓝图/C++，对一人做休闲小游戏明显过重 |
| Windows 导出 | 一键导出 exe，体积小（几十 MB 起）[已核实] | 成熟 | 成熟但包体大 |
| Android 导出 | 官方支持（需装 OpenJDK + Android SDK 导出模板）；4.4 起 Android 支持持续改进 [已核实] | 成熟，Android Build Support 模块 | 支持但配置繁琐 |
| 3D 画面上限 | 中高（4.4 起内置 Jolt 物理、AgX 色调映射；Forward+/Mobile/Compatibility 三渲染器）[已核实] | 中高（URP） | 最高（Nanite/Lumen），但直播小窗口里优势发挥不出来 |
| 光枪游戏需要的内置件 | Path3D/PathFollow3D 轨道、PacketPeerUDP、WebSocket 全内置 [已核实] | Cinemachine（官方免费包）做轨道；UDP 走 .NET 类库 | Rig Rail 内置；网络功能重 |
| 社区资源量 | 增长快但总量最小；光枪/rail shooter 参考项目少（见第二节）| 最大 | 大但偏 AAA |

### 1.3 为什么这个项目特别适合 Godot

1. **零成本零合规负担**：商业销售（绑套装卖）不用向任何人交钱、不用报收入，MIT 协议连版权声明都很宽松 [已核实]。
2. **离线可用、国内无障碍**：不需要账号登录激活。Unity Hub 需要登录，且国内渠道默认提供带 c1 后缀的"中国特供版"，全球版版本号对不上是常见痛点 [已核实，见 Unity 中国社区问答]。Unity 中国另推"团结引擎"（Tuanjie，unity.cn/tuanjie/releases），与全球版有分叉风险 [已核实]。
3. **本项目的两个关键技术点（轨道镜头、UDP 输入）Godot 全部内置**，不依赖任何第三方插件。
4. **画面够用**：光枪游戏的"直播间一眼惊艳"靠的是美术风格 + 运镜 + 特效（粒子、屏幕震动、命中反馈），不是引擎渲染上限。Godot 4 的 Forward+ 渲染 + 后期（辉光、调色）足够做出街机感 [估计]。
5. 编辑器本身轻，AI 辅助写 GDScript 的上下文成本低、报错易读 [估计]。

### 1.4 什么时候改选 Unity

- 你已经会 C#/Unity，不会 Python 系语言；
- 需要接入某些只提供 Unity SDK 的国内平台/广告/支付组件 [估计]；
- 团队扩张预期强（招人容易）。

### 1.5 为什么不选 Unreal

5% 分成（超 100 万美元后）对本项目不是问题，但：编辑器太重、迭代慢、一人维护 C++/蓝图工程成本高；休闲 3-5 分钟光枪游戏用 UE 是杀鸡用牛刀 [估计]。

---

## 二、GitHub 可参考的开源项目（star 数为 GitHub API 实时查询，已核实）

### 2.1 Godot rail shooter / on-rails

| 仓库 | Star | 说明 |
|---|---|---|
| [crogersdev/rail_shooter_v1](https://github.com/crogersdev/rail_shooter_v1) | 11 | Godot 做的 Star Fox 64 风格 rail shooter，最直接的参考 |
| [lunarcloud/ggj-2024-rail-shooter](https://github.com/lunarcloud/ggj-2024-rail-shooter) | 6 | Godot 4 on-rails shooter，Game Jam 原型，代码量小好读 |
| [BradGaddis/Godot-On-Rails-Shooter-Character-Controller](https://github.com/BradGaddis/Godot-On-Rails-Shooter-Character-Controller) | 2 | 轨道角色控制器思路参考 |
| [123mikeyd/hermes-wing-godot](https://github.com/123mikeyd/hermes-wing-godot) | 1 | Godot 4.5 程序化 rail-shooter demo，含编队/杂技动作/Boss |
| [JustinSpedding/senior-grad-project-game](https://github.com/JustinSpedding/senior-grad-project-game) | 2 | 音乐驱动的程序化生成 rail shooter（GDScript） |

### 2.2 Unity rail shooter

| 仓库 | Star | 说明 |
|---|---|---|
| [Zesix/rail-hunter](https://github.com/Zesix/rail-hunter) | 12 | Unity rail shooter，带自研框架 |
| [hacker-chad/SpaceBattle-railShooter](https://github.com/hacker-chad/SpaceBattle-railShooter) | 9 | 用 Unity Timeline 系统做轨道演出，和"镜头轨道"思路直接相关 |
| [frandyjayr/Rail-Shooter-Game](https://github.com/frandyjayr/Rail-Shooter-Game) | 3 | 3D rail shooter |
| [UnityProjectProgramming/RailShooter](https://github.com/UnityProjectProgramming/RailShooter) | 3 | 基础实现 |
| [yogykwan/ArgonAssault](https://github.com/yogykwan/ArgonAssault) | 1 | Star Fox 64 / 铁甲飞龙风格 |

### 2.3 "手机当光枪"直接相关

| 仓库 | Star | 说明 |
|---|---|---|
| [matiasbeckerle/doom-lgs](https://github.com/matiasbeckerle/doom-lgs) | 40 | Node.js 多人光枪射击，Doom 风，架构思路可参考 |
| [maximgladkov/gyro-aim](https://github.com/maximgladkov/gyro-aim) | 1 | Phone-as-light-gun 瞄准（浏览器游戏），和你的方案同源 |
| [brianlo06/reticle](https://github.com/brianlo06/reticle) | 0 | "手机就是枪、电视是屏幕"的射击游戏，瞄准点击射击 |
| [hugofvs/duck-hunt](https://github.com/hugofvs/duck-hunt) | 0 | 浏览器版打鸭子，手机当光枪 |
| [JeanJouliaCode/phone-light-gun](https://github.com/JeanJouliaCode/phone-light-gun) | 0 | 手机当光枪的 app |

### 2.4 光枪硬件生态（了解即可，你的方案绕开了硬件）

| 项目 | Star | 说明 |
|---|---|---|
| GUN4IR（搜 GitHub 话题 gun4ir） | — | 开源光枪固件生态（红外摄像头方案） |
| [gunmotelabs/Gunmote](https://github.com/gunmotelabs/Gunmote) | 22 | 把 Wii 遥控器变成 PC 高精度光枪 |
| [batocera-linux/lightguns-games-precalibrations](https://github.com/batocera-linux/lightguns-games-precalibrations) | 6 | 光枪校准数据，校准流程可参考 |
| [DR-89/time-crisis-vr](https://github.com/DR-89/time-crisis-vr) | 67 | Time Crisis VR 移植，研究"掩体/装弹节奏"的手感参考 |

**结论 [估计]**：光枪/rail shooter 开源项目普遍 star 很低（个位数到两位数），没有现成的高质量模板可直接套用——这既是机会（品类竞争少）也意味着核心玩法代码要自己写，以上仓库用于参考结构而非复用。

GitHub 国内访问不稳定时：用 `https://gitclone.com/github.com/<owner>/<repo>` 或 ghproxy 类镜像克隆，或在 gitee 搜同名镜像仓库 [估计]。

---

## 三、光枪游戏技术要点拆解与现成组件

### 3.1 准星输入（核心抽象）

**关键设计：第一天就抽象出 `InputProvider` 接口**，所有输入源（UDP 手机枪、鼠标、触屏、手柄）实现同一接口，游戏逻辑只消费 `aim_position: Vector2`（屏幕像素坐标）+ 事件信号（shot/reload/coin/start）。这样降级方案和联调零成本切换。

Godot 侧要点：
- UDP 坐标到达后用滤波平滑：简单方案 `pos = pos.lerp(target, 1 - exp(-k * delta))`；追求手感用 **One Euro Filter**（低延迟+防抖，论文开源，几十行可实现）[估计]。
- 手机端发来的若是归一化坐标（0~1），乘以 `get_viewport().get_visible_rect().size` 转像素。
- 校准：做 2 点（左上/右下）或 4 点校准界面，把手机的原始坐标范围映射到屏幕，存 `user://calibration.cfg`（ConfigFile）[估计，标准做法]。

### 3.2 命中判定：raycast vs 屏幕空间判定

两种方案，**建议混合**：

1. **射线法（raycast）**：从相机经过准星像素点发射线，打中第一个带碰撞体的敌人。
   - Godot：`camera.project_ray_origin(screen_pos)` + `camera.project_ray_normal(screen_pos)`，再用 `get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D...)` [已核实，官方 API]。
   - Unity：`Camera.ScreenPointToRay` + `Physics.Raycast` [已核实]。
   - 优点：精确；缺点：敌人小/移动快时太难打中，休闲玩家挫败感强。
2. **屏幕空间判定（光枪游戏的传统做法，推荐为主）**：把敌人的 3D 位置（或其碰撞球心）投影到屏幕 `camera.unproject_position(enemy_pos)`，算与准星的**像素距离**，小于判定半径（如 60-100px）即命中；多个候选取距离最近/层级最前者。
   - 优点：命中宽容度可调（街机手感）、天然支持"判定圈"难度分级；缺点：需自己处理遮挡（可叠加一条 raycast 验证遮挡）。
3. **混合 [估计，推荐]**：屏幕距离初筛（宽容）→ raycast 验证遮挡（防穿墙命中）。敌人给每个可命中部位挂 `Area3D`/碰撞球并设 metadata（部位伤害倍率），爆头=更小的屏幕判定圈+更高倍率。

### 3.3 镜头轨道运动（rail 的"轨"）

- **Godot（内置，零插件）**：`Path3D` 画轨道曲线（编辑器里可视化拖点）+ `PathFollow3D` 挂相机，`progress`/`progress_ratio` 随时间推进；到战斗节点暂停推进，清完波次继续；镜头朝向用第二个 Path3D 或 `look_at` 动态目标。震动用随机偏移叠加在相机 `h_offset/v_offset` 或子节点上 [已核实，均为官方节点]。
- **Unity**：官方免费包 **Cinemachine**（3.x），用 Spline/Dolly Track + Cart，配 Timeline 做演出（参考 hacker-chad/SpaceBattle-railShooter）[已核实]。
- 演出节奏参考：街机光枪（House of the Dead、Time Crisis）= 轨道行进段（运镜演出）→ 定点战斗段（镜头小幅度动态晃动保持生动）→ BOSS 段（环绕/推拉运镜）[估计]。

### 3.4 敌人波次管理

没有主流的"波次管理"开箱插件，**自制成本很低，建议数据驱动** [估计]：
- Godot：每个战斗段一个节点，内含 `Marker3D` 出生点数组；波次用自定义 `Resource`（WaveResource：敌人场景数组、出生点、间隔、触发条件）声明，WaveManager 读取并按时间/击杀进度 spawn；波次完成发信号 → 通知 PathFollow 继续。
- Unity：同理用 `ScriptableObject`。
- 敌人 AI 保持简单：出场走位（Tween/Path）→ 停留攻击（计时器+前摇动画给玩家反应窗口）→ 死亡特效。光枪游戏敌人本质是"定时冒出来的靶子"，别做过复杂的状态机 [估计]。

### 3.5 直播间"一眼惊艳"的低成本手段 [估计]

AgX 色调映射（Godot 4.4+ 内置）+ WorldEnvironment 辉光（Bloom）+ 命中时的屏幕震动/顿帧（hitstop 50-80ms）+ 大粒子数字跳分 + 结算 UI 动效。这些全在 Godot 内置范围内。

---

## 四、自定义输入源接入：UDP 坐标流 + HTTP 事件

### 4.1 强烈建议的协议简化 [估计]

**把 shot/reload/coin/start 事件也走 UDP**（如文本包 `SHOT`、`RELOAD`、`COIN`、`START`），而不是 HTTP。理由：
- UDP 一个端口搞定全部输入，代码量减半；
- HTTP 需要游戏侧起 HTTP server——Godot **没有内置 HTTP server**（只有 HTTPRequest 客户端）[已核实，Godot 官方论坛确认]；
- 局域网内 UDP 丢包对"按下射击"这类事件影响可忽略（玩家会再按一次），坐标流本身就是允许丢包的。

若手机端方案已定型必须用 HTTP，见 4.3。

### 4.2 Godot 接入 UDP（内置，已核实 API）

```
# 伪代码（GDScript, Godot 4.x）
var udp := PacketPeerUDP.new()
udp.bind(4444)                       # 监听端口
# 每帧轮询（_process 或 _physics_process）
while udp.get_available_packet_count() > 0:
    var msg := udp.get_packet().get_string_from_utf8()
    # 解析 "x,y" → 更新准星；"SHOT"/"RELOAD" → 发信号
```

要点：
- 只保留最新一帧坐标（队列里旧的坐标包直接丢弃，避免延迟累积）[估计]；
- 手机端 Android 发送用 `DatagramSocket`，坐标流频率 30-60Hz 足够 [估计]；
- Windows 防火墙：首次运行会弹"允许网络访问"授权框，给用户的安装说明里要写这一步 [估计]；
- 正式版建议加心跳：超过 500ms 无包则自动切回鼠标模式并在屏幕上提示"枪已断开" [估计]。

### 4.3 HTTP 事件的三种接法（按推荐度排序）

1. **WebSocketPeer（Godot 内置，推荐）**：手机端浏览器/App 起 WebSocket 连接，事件双向实时，Godot 原生支持 server [已核实]。如果手机端是你自己写的 App/网页，改 WebSocket 比 HTTP 更顺。
2. **godottpd 插件**：[deep-entertainment/godottpd](https://github.com/deep-entertainment/godottpd)，路由式 HTTP server 插件（`GET /shot` 这类路由可直接挂回调），MIT 协议 [已核实]。
3. **TCPServer 手写极简 HTTP 解析**：shot/reload 是固定几个 GET 请求，解析请求行第一行即可，约 50 行代码 [估计]。
4. Unity 路线（备选）：`System.Net.HttpListener`，但 Windows 下非管理员监听端口需要 `netsh http add urlacl` 注册，麻烦；UDP 用 `System.Net.Sockets.UdpClient`，在子线程收包 + `ConcurrentQueue` 回主线程消费 [已核实，.NET 标准类]。

### 4.4 延迟预算 [估计]

局域网 UDP 往返 <5ms 典型；60fps 下每帧 16.6ms，坐标流 60Hz 时延迟肉眼不可感知。Wi-Fi 建议电脑和手机同 5GHz 频段，直播推流机器注意上行带宽别打满。

---

## 五、降级输入方案（鼠标调试模式等）

基于 3.1 的 `InputProvider` 抽象，实现以下 Provider，设置界面可切换（auto/mouse/touch/udp）：

1. **鼠标模式（开发调试主力）**：
   - Godot：`Input.set_mouse_mode(Input.MOUSE_MODE_HIDDEN)` 隐藏系统光标，自绘准星 Sprite2D/TextureRect 跟随 `get_viewport().get_mouse_position()` [已核实 API]；
   - 左键=shot，右键或 R=reload，C=coin，Enter=start；
   - 鼠标移动天然映射准星，命中判定逻辑与 UDP 模式完全共用。
2. **触屏模式（Android 电视/平板自瞄）**：`_unhandled_input` 接 `InputEventScreenTouch/Drag`，触摸点即准星、点按即射击（适合手机直装 demo 版本）[已核实 API]。
3. **手柄/遥控器模式（电视降级）**：左摇杆移准星（给强辅助瞄准：准星靠近敌人判定圈时减速吸附），A=shot、B=reload；安卓电视遥控器方向键同理，配大判定圈 [估计]。
4. **调试增强**：F1 显示 UDP 收包状态/延迟、当前输入源、命中判定圈可视化开关 [估计]。

---

## 六、国内可用性与成本清单

| 工具 | 版本 | 价格/授权 | 国内访问 |
|---|---|---|---|
| Godot Engine | 4.6.3-stable（调研时最新维护版）[已核实] | 免费，MIT | 官网 godotengine.org 一般可直连 [估计]；慢则走 SourceForge 镜像、itch.io 或 Steam（国内教程推荐 SourceForge）[已核实有中文下载教程] |
| Godot 中文文档 | 随版本 | 免费 | docs.godotengine.org/zh-cn 可直连 [估计] |
| Unity | Unity 6 / 团结引擎 | Personal 免费（<$200K/年）；Pro $2,200/席/年（2026 起 $2,310）[已核实] | unity.cn 国内官网 + 团结 Hub 直连 [已核实]；注意国内渠道版本带 c1 后缀 |
| Unreal Engine | UE5 | 免费；营收超 $1M 后 5% [已核实] | Epic 启动器/商城国内访问不稳定 [估计] |
| GitHub（参考项目源码） | — | 免费 | 不稳定，用 gitclone.com/ghproxy 镜像 [估计] |
| godottpd（HTTP server 插件） | 最新 main | 免费，MIT | GitHub 托管，同上 |
| Cinemachine（仅 Unity 路线） | 3.x | 免费官方包 | Package Manager 国内可用 [估计] |

**本项目零翻墙可行**：推荐栈（Godot + 内置 UDP + godottpd 或 WebSocket）全部国内直连可获取 [估计]。

---

## 七、建议的起步步骤（照做即可）

1. SourceForge 或官网下载 Godot 4.6.3-stable 标准版（GDScript），解压即用，无需安装。
2. 克隆 [lunarcloud/ggj-2024-rail-shooter](https://github.com/lunarcloud/ggj-2024-rail-shooter) 和 [crogersdev/rail_shooter_v1](https://github.com/crogersdev/rail_shooter_v1)，跑起来看 Path3D 用法。
3. 建空项目，按顺序做：① Path3D+PathFollow3D 相机轨道 → ② InputProvider 接口 + 鼠标 Provider + 自绘准星 → ③ 屏幕空间命中判定（unproject_position + 像素距离）→ ④ 一个战斗段的 WaveResource + WaveManager → ⑤ PacketPeerUDP Provider + 手机端发坐标的最小程序 → ⑥ godottpd 或 WebSocket 接事件（或事件并入 UDP）。
4. 全程用鼠标模式开发，UDP 枪最后接入——输入抽象做好后这只是加一个 Provider。

---

## 附：链接清单

**引擎与定价**
- Godot 下载归档：https://godotengine.org/download/archive/ ；4.6 stable：https://godotengine.org/download/archive/4.6-stable/
- Godot 中文文档：https://docs.godotengine.org/zh-cn/
- Unity 定价页：https://unity.com/products ；定价更新说明：https://unity.com/products/pricing-updates ；取消 Runtime Fee 公告：https://unity.com/blog/unity-is-canceling-the-runtime-fee
- Unity 中国/团结引擎：https://unity.cn/tuanjie/releases
- 引擎对比参考：https://shattered.io/unreal-engine-vs-unity-vs-godot/ ；https://itch.io/blog/1067028/game-engine-showdown-2025-unity-vs-godot-vs-unreal-which-should-you-choose

**关键 API 文档**
- PacketPeerUDP：https://docs.godotengine.org/en/stable/classes/class_packetpeerudp.html
- TCPServer：https://docs.godotengine.org/en/stable/classes/class_tcpserver.html
- HTTPRequest（仅客户端）：https://docs.godotengine.org/en/stable/classes/class_httprequest.html
- Godot 无内置 HTTP server 的官方论坛确认：https://forum.godotengine.org/t/how-can-i-receive-http-requests-with-a-godot-4-game/55075
- Path3D/PathFollow3D：https://docs.godotengine.org/en/stable/classes/class_pathfollow3d.html
- godottpd：https://github.com/deep-entertainment/godottpd
- Cinemachine：https://unity.com/features/cinemachine

**开源参考项目**：见第二节表格内链接。
