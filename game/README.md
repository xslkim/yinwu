# ArcGun

手机当光枪的 3D 光枪射击游戏（Godot 4.7）。当前进度：Week 2 垂直切片 —— 微关卡串烧 + 三靶型系统。

## 跑游戏

```bash
# 鼠标模式（默认）：移动瞄准，左键射击，R 换弹，C 投币，回车开始
Godot_v4.7.2-stable_win64.exe --path game

# 手机光枪模式：监听 UDP 9000 端口
Godot_v4.7.2-stable_win64.exe --path game -- --udp
```

玩法：待机页投币（COIN/C）→ 开始（START/回车）→ 三个微关卡顺序轮换
（① 固定靶练习 ② 移动靶+人质混杂 ③ 金靶限时），每关 20-25 秒有目标分，
达标过关、失败进总结算；总结算页显示得分/命中率/连击峰值，START 免投币再来一局。
弹匣 6 发，打空按 R（或手机发 RELOAD）换弹，每关开始自动满弹。

靶型：红头黄身 = 普通靶（100×连击）；绿身白头 = 人质（误伤 -200 且清连击）；
金色小靶 = 金靶（300×连击，快且停留短）。

## 跑测试

```bash
# 单元测试（协议/滤波/计分/弹药/关卡流转/波次调度，181 断言）
Godot_v4.7.2-stable_win64_console.exe --headless --path game --script res://tests/run_tests.gd

# 冒烟：import + 测试 + 启动 5 秒无崩溃
bash game/tests/smoke.sh

# 完整冒烟：追加 e2e 命中率定量验证（约 20 秒）
bash game/tests/smoke.sh --full
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
| `COIN` | 投币（待机页开始流程的第一步） |
| `START` | 开始 / 总结算页再来一局 |

事件包大小写不敏感。坐标越界或格式不符直接丢弃。游戏侧 0.6 秒无坐标包则准星停移并变灰。

## 调试模式

- 热键：F1 调试浮层（输入源/UDP 收包统计）、F2 命中判定圈、F9 切输入源、F11 全屏。
- 鼠标源下：C = 投币，回车 = 开始（对应手机 COIN/START）。
- 启动参数（跟在 `--` 后）：
  - `--udp` UDP 光枪源启动
  - `--freeze-target` 定量联调：不走关卡流程，钉死一个打不烂的固定靶并打印靶心屏幕坐标
  - `--auto-shots` 每 60 帧自截屏到 `temp/arcgun-shots/auto/`（引擎内读回，锁屏/遮挡也能验收）
  - `--level=N` 跳关调试（1-3）
- 退出时控制台打 `[Stats] shots=... hits=... accuracy=...` 统计行。

详细工程规范见 [CLAUDE.md](CLAUDE.md)。
