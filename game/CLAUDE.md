# ArcGun 工程指南（给 AI 助手 / 新成员）

配合"手机当光枪"外设的 3D 光枪射击游戏。手机 App 经局域网 UDP 发瞄准坐标流（ASCII `"x,y"`，1920×1080 规范坐标，60-120Hz）与事件包（`SHOT`/`RELOAD`/`COIN`/`START`）。PC 先行，后续上 Android 电视。画面：低多边形卡通渲染，零外部素材（全部程序化搭建）。

## 工程结构

```
game/
├── project.godot          # 1920×1080, canvas_items 拉伸, gl_compatibility
├── scenes/main.tscn       # 唯一场景：Node3D + main.gd，全部内容代码搭建
├── src/
│   ├── core/              # 纯逻辑层（RefCounted，不碰场景树，可 headless 测试）
│   │   ├── protocol_parser.gd   # UDP 包字符串 → 结构化结果
│   │   ├── one_euro_filter.gd   # 坐标平滑（1€ Filter）
│   │   ├── score_keeper.gd      # 计分/连击
│   │   └── ammo_manager.gd      # 弹药/换弹
│   ├── input/             # 输入抽象层
│   │   ├── input_provider.gd    # 基类（Node）：get_aim() + shot/reloaded/coin/started 信号
│   │   ├── mouse_gun_provider.gd
│   │   └── udp_gun_provider.gd  # PacketPeerUDP :9000，坐标映射+滤波，0.6s 过期
│   └── game/              # 场景层
│       ├── main.gd        # 主循环：场景搭建/命中判定/反馈/输入源切换
│       ├── target.gd      # 往复移动标靶
│       ├── crosshair.gd / hud.gd / debug_overlay.gd / placeholder_audio.gd
└── tests/
    ├── run_tests.gd       # 极简测试运行器（断言失败退出码非零）
    ├── udp_sender.py      # 手工联调：模拟手机发包
    └── smoke.sh           # import + 测试 + 启动 5 秒
```

## 命名与代码规范

- GDScript，全文件 `class_name`，文件名与类名一致的 snake_case。
- 中文注释适度：只写"为什么"，不写"是什么"。
- 可调手感参数一律 `@export`（如 `hit_tolerance`、滤波器 min_cutoff/beta）。
- 场景能代码搭建就代码搭建，不新增 .tscn/.tres，除非确实需要编辑器可视化。

## 输入抽象规则（核心纪律）

**游戏逻辑禁止直接读 `Input` 单例**，必须走 `InputProvider` 接口（`get_aim()` + 信号）。
- 合法例外只有两个：① Provider 实现内部；② main.gd 的 `_unhandled_input` 里 F1/F2/F9/F11 调试热键（属系统层）。
- 新增输入源（如串口枪、第二把枪）继承 `InputProvider`，不改游戏逻辑。

## 测试怎么跑

```bash
# 单元测试（headless，无插件）
Godot_v4.7.2-stable_win64.exe --headless --path game --script res://tests/run_tests.gd
# 冒烟三连
bash game/tests/smoke.sh
# UDP 手工联调（游戏加 `-- --udp` 启动，另开终端发包）
D:/tvgun/.venv/Scripts/python.exe game/tests/udp_sender.py --n 300 --shot-every 30 --events
```

## 调试热键

| 键 | 功能 |
|---|---|
| F1 | 调试浮层（输入源 / UDP 收包计数 / 最后收包时间） |
| F2 | 命中判定圈可视化（靶心处宽容度半径） |
| F9 | 切换输入源（鼠标 ↔ UDP） |
| F11 | 全屏切换 |
| R | 换弹（鼠标源下） |
