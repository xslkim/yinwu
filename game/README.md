# ArcGun

手机当光枪的 3D 光枪射击游戏（Godot 4.7）。当前进度：Week 3 完整玩法 —— 6 关串烧 + 逃靶惩罚 + 连击里程碑 + 素材热插拔接口。

## 跑游戏

```bash
# 鼠标模式（默认）：移动瞄准，左键射击，R 换弹，C 投币，回车开始，Esc 退出本局
Godot_v4.7.2-stable_win64.exe --path game

# 手机光枪模式：监听 UDP 9000 端口
Godot_v4.7.2-stable_win64.exe --path game -- --udp
```

玩法：待机页投币（COIN/C）→ 开始（START/回车）→ 6 个微关卡顺序轮换，
每关 20-28 秒有目标分，达标过关、失败进总结算；总结算页显示得分/命中率/连击峰值/
误伤/逃靶，START 免投币再来一局。待机 30 秒无投币自动进 attract 演示（相机环绕 +
靶子自动弹缩），投币即打断。弹匣 6 发，打空按 R（或 RELOAD）换弹，每关开始自动满弹。

## 关卡表

| 关 | 主题 | 时长 | 目标分 | 靶速 | 逃靶惩罚 | 设计意图 |
|---|---|---|---|---|---|---|
| L1 | 固定靶练习 | 22s | 1200 | ×1.0 | 无 | 教学关，全固定靶 |
| L2 | 移动靶 | 25s | 2200 | ×1.0 | 无 | 移动靶入门 |
| L3 | 小心人质 | 25s | 3000 | ×1.0 | 无 | 人质混杂，误伤 -200 |
| L4 | 金靶限时 | 22s | 3400 | ×1.0 | -50 | 引入逃靶惩罚，金靶高分短停留 |
| L5 | 加速混合 | 25s | 4200 | ×1.3 | -50 | 提速 + 人质占比更高 |
| L6 | 最终混战 | 28s | 5200 | ×1.5 | -50 | 全靶型，金靶成对双出 |

一局 ≈ 2.8 分钟（开打 147s + 过场 21s）。靶型：红头黄身 = 普通靶（100×连击）；
绿身白头 = 人质（误伤 -200 且清连击）；金色缩头小靶 = 金靶（300×连击，快且停留短）。
逃靶（停留超时溜走）：-50 且断连击（L1-L3 豁免）。连击 ×5/×10/×15 有金色大字奖励跳字。

## 跑测试

```bash
# 单元测试（协议/滤波/计分/弹药/关卡流转/逃靶/里程碑/AssetLib/AudioManager/设置，381 断言）
Godot_v4.7.2-stable_win64_console.exe --headless --path game --script res://tests/run_tests.gd

# 冒烟：import + 测试 + 启动 5 秒无崩溃（含脚本解析错误检查）
bash game/tests/smoke.sh

# 完整冒烟：追加 e2e 命中率定量验证（约 20 秒）
bash game/tests/smoke.sh --full
```

## 素材目录约定（assets/ 已入库，来源与授权见 assets/ATTRIBUTION.md）

贴图 `assets/textures/`：`target_face_normal/hostage/gold.png`（靶脸面片）、
`sky.png`（全景天空）、`ground.png` / `wall.png`（四方连续平铺）、`muzzle_flash.png`（枪口火光）、
`particle_hit.png` / `particle_star.png`（粒子）、`ui_crosshair.png`（准星）、
`ui_coin.png`（待机页金币）、`ui_ammo.png`（HUD 弹药图标）、`poster.png`（待机页标题图）。

音频 `assets/audio/`：`play()` 事件名 → 文件名映射（变体 `_1.._3` 随机 + pitch ±5%）：
`shot→sfx_shot`、`hit→sfx_hit`、`hit_gold→sfx_hit_gold`、`hostage→sfx_hostage`、`reload→sfx_reload`、
`coin→sfx_coin`、`start→sfx_start`、`pass→sfx_pass`、`fail→sfx_fail`、`gameover→sfx_gameover`、
`combo→sfx_combo`、`gold_spawn→sfx_gold_spawn`、`escape→sfx_escape`、`ui_click→sfx_ui_click`、
`stinger_win/lose→同名`；`empty`（空仓）无素材，常驻合成 fallback。
语音播报 `voice()`：`voice_start/pass/fail/gameover/combo.wav`（独立 Voice 总线，与音效叠加）。
BGM：`bgm_menu/play/intense.wav`（无缝循环 + 1s 交叉淡化；attract/demo→menu，L1-L4→play，
L5-L6 与残局→intense，gameover 淡出静音）。

所有素材缺失时自动回退程序化占位（纯色/合成音），游戏永远能跑。

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
| `COIN` | 投币（待机页开始流程的第一步；演示中打断回待机页） |
| `START` | 开始 / 总结算页再来一局 |
| `EXIT` | 退出本局回待机页；待机页再按 = 退出游戏进程 |

事件包大小写不敏感。坐标越界或格式不符直接丢弃。游戏侧 0.6 秒无坐标包则准星停移并变灰。

## 调试模式

- 热键：F1 调试浮层、F2 命中判定圈、F3 主音量循环档（100→70→40→0%，存 user://settings.cfg）、
  F9 切输入源、F11 全屏。
- 鼠标源下：C = 投币，回车 = 开始，Esc = 退出本局（对应手机 COIN/START/EXIT）。
- 启动参数（跟在 `--` 后）：
  - `--udp` UDP 光枪源启动
  - `--freeze-target` 定量联调：不走关卡流程，钉死一个打不烂的固定靶并打印靶心屏幕坐标
  - `--auto-shots` 每 60 帧自截屏到 `temp/arcgun-shots/auto/`（引擎内读回，锁屏/遮挡也能验收）
  - `--level=N` 跳关调试（1-6）
- 退出时控制台打 `[Stats] shots=... hits=... escapes=... accuracy=...` 统计行。

详细工程规范见 [CLAUDE.md](CLAUDE.md)。
