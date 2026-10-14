# ArcGun

手机当光枪的 3D 光枪射击游戏（Godot 4.7）。当前进度：Week 0-1 —— 脚手架 + 30 秒核心循环。

## 跑游戏

```bash
# 鼠标模式（默认）：移动瞄准，左键射击，R 换弹
Godot_v4.7.2-stable_win64.exe --path game

# 手机光枪模式：监听 UDP 9000 端口
Godot_v4.7.2-stable_win64.exe --path game -- --udp
```

玩法：打中来回移动的标靶得分，连击有加成；弹匣 6 发，打空按 R（或手机发 RELOAD）换弹。

## 跑测试

```bash
# 单元测试（协议解析 / 滤波收敛 / 计分 / 弹药）
Godot_v4.7.2-stable_win64.exe --headless --path game --script res://tests/run_tests.gd

# 冒烟：import + 测试 + 启动 5 秒无崩溃
bash game/tests/smoke.sh
```

## 手机联调

```bash
# 模拟手机发 100Hz 圆形轨迹坐标流 + 每 30 包一个 SHOT + 末尾 RELOAD/COIN/START
D:/tvgun/.venv/Scripts/python.exe game/tests/udp_sender.py --host <PC的IP> --n 300 --shot-every 30 --events
```

## UDP 协议（端口 9000，可配）

| 包格式 | 说明 |
|---|---|
| `"x,y"` | 瞄准坐标，ASCII 浮点（一位小数），1920×1080 规范坐标系，60-120Hz |
| `SHOT` | 开火 |
| `RELOAD` | 换弹 |
| `COIN` | 投币 |
| `START` | 开始 |

事件包大小写不敏感。坐标越界或格式不符直接丢弃。游戏侧 0.6 秒无坐标包则准星停移并变灰。

## 调试模式

- F1 调试浮层：当前输入源、UDP 收包计数、最后收包时间。
- F2 命中判定圈可视化；F9 切换鼠标/UDP 输入源；F11 全屏。
- UDP 源每收 30 包在控制台打一次统计日志，事件包即时打日志。

详细工程规范见 [CLAUDE.md](CLAUDE.md)。
