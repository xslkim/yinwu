# 音频资产清单（街机光枪射击游戏）

生成日期：2026-09-15。格式：WAV 44.1kHz 16bit；音效/语音/stinger 为单声道，BGM 为立体声。
响度：统一归一到约 -16 LUFS（EBU R128 综合响度）；音效/语音/stinger 首尾各 20ms 静音防爆音；BGM 为无缝循环（首尾 3s acrossfade），不加 pad。

生成管线脚本与原始素材（未入库，可复现）：
- `/mnt/d/yinwu/temp/arcade-audio/gen_sfx.sh` — Stable Audio 3 small-sfx 批量生成（GPU 串行 flock）
- `/mnt/d/yinwu/temp/arcade-audio/gen_bgm.sh` — Mureka instrumental API（3 任务串行）
- `/mnt/d/yinwu/temp/arcade-audio/gen_voice.py` / `regen_combo.py` — Qwen3-TTS CustomVoice
- `/mnt/d/yinwu/temp/arcade-audio/post_sfx.sh` / `post_bgm.sh` / `verify.sh` — 后处理与校验
- 原始素材：`temp/arcade-audio/sfx_raw/`、`voice_raw/`、`bgm_raw/`（Mureka flac 原件含备选候选）

## 音效（Stable Audio 3 small-sfx）

| 文件 | prompt | 时长 | 响度 |
|---|---|---|---|
| sfx_shot_1.wav | cartoon laser gun pew, bright zappy blast, arcade game sound effect, short, dry | 0.84s | -15.9 |
| sfx_shot_2.wav | cartoon cork pop gun shot with playful zap, soft toy blaster, short, dry | 0.84s | -15.9 |
| sfx_shot_3.wav | retro arcade laser blaster shot, quick electronic pew, snappy, short, dry | 0.84s | -16.0 |
| sfx_hit_1.wav | wooden target bonk, crisp cartoon pop hit, arcade shooting gallery, short, dry | 0.84s | -16.0 |
| sfx_hit_2.wav | single knock on hollow wood block, cute cartoon hit, short, dry | 0.84s | -16.0 |
| sfx_hit_3.wav | bubble pop on wooden board, cartoon bop, playful hit, short, dry | 0.84s | -16.0 |
| sfx_hit_gold_1.wav | bright bell ding, golden cartoon hit, sparkling chime, short | 1.04s | -19.9* |
| sfx_hit_gold_2.wav | high pitched crystal ding with shimmer, cartoon jackpot reward, short | 1.04s | -17.7* |
| sfx_hostage.wav | low error buzz, cartoon wrong answer tone, descending wah, not harsh, short | 1.24s | -16.2 |
| sfx_reload.wav | two quick plastic clicks, toy gun reload click clack, short, dry | 1.04s | -21.4* |
| sfx_coin.wav | arcade coin inserted into slot, metallic clink then chime, retro arcade, short | 1.24s | -16.1 |
| sfx_start.wav | rising cartoon fanfare horn, upbeat arcade game start jingle, short | 2.04s | -16.6 |
| sfx_pass.wav | short cheerful victory jingle, cartoon kazoo fanfare, triumphant, arcade | 2.54s | -16.1 |
| sfx_fail.wav | descending cartoon fail sound, sad slide whistle going down, short | 1.54s | -16.1 |
| sfx_gameover.wav | arcade game over jingle, descending playful retro melody, short | 3.04s | -16.0 |
| sfx_combo.wav | sparkling glitter arpeggio chime, exciting cartoon combo reward, short | 1.24s | -16.2 |
| sfx_gold_spawn.wav | attention grabbing bell ring, magical shimmer sparkle, cartoon, short | 1.24s | -16.1 |
| sfx_escape.wav | cartoon slide whistle whoosh, quick slipping away escape sound, short | 1.04s | -16.5 |
| sfx_ui_click.wav | short clean UI click, soft cartoon button tap, dry | 0.54s | -16.2 |
| sfx_empty_1.wav | single dry toy gun trigger click, empty chamber snap, cartoon plastic click, very short | 0.54s | -16.2 |
| sfx_empty_2.wav | quick hollow click clack, cartoon empty gun trigger, light wooden snap, very short, dry | 0.54s | -16.0 |
| stinger_win.wav | cheerful cartoon victory fanfare, brass and xylophone, arcade game win stinger, upbeat ending | 4.04s | -16.2 |
| stinger_lose.wav | cartoon lose stinger, descending sad trombone wah wah, playful, gentle ending | 4.04s | -15.9 |

\* 瞬态主导音效（铃声/咔哒声）：综合响度被长尾或脉冲特性拉低，已加增益至峰值限幅（-2dBTP）为止，主观响度足够。Godot 侧如需更响可用 AudioStreamPlayer 的 volume_db 微调。

游戏层映射：sfx_shot 用 `_1/_2/_3` 随机轮播，sfx_hit 同理；sfx_hit_gold 两变体。

## 音乐（Mureka instrumental，mureka-7.6）

后处理：强 onset 起点（silenceremove -35dB）→ 裁剪 → 首尾 3s acrossfade(tri) 无缝循环 → loudnorm -16 → 44.1kHz 16bit 立体声。

| 文件 | prompt | task id / 选用候选 | 时长 | 响度 |
|---|---|---|---|---|
| bgm_menu.wav | Cheerful cartoon arcade attract mode theme, playful xylophone melody and bouncy synth bass, light chiptune sparkle, whimsical game show vibe, relaxed, steady 100 bpm, no vocals, seamless loop | 166324227604482 / 候选0（原 133.3s） | 45.00s 循环 | -16.1 |
| bgm_play.wav | Upbeat cartoon arcade shooting gallery theme, funky brass stabs and bouncy bass, playful tension, energetic drums, carnival energy, steady 128 bpm, no vocals, seamless loop | 166324854587394 / 候选0（原 123.3s） | 90.00s 循环 | -15.7 |
| bgm_intense.wav | High energy cartoon arcade climax theme, driving fast drums, frantic brass hits and rapid synth arpeggios, carnival excitement at maximum, steady 128 bpm, no vocals, seamless loop | 166325943009281 / 候选0（原 130.3s） | 90.00s 循环 | -15.6 |

候选选择规则：时长最接近目标者（SoundGame 先例）。备选 flac 在 `temp/arcade-audio/bgm_raw/<track>/`。
bgm_play 与 bgm_intense 同按 128 BPM 生成，便于游戏层交叉淡入切换高潮段；实际 BPM 未做梳状扫描校准（本游戏无节拍网格玩法，hunt/bridge 先例）。

## 语音（Qwen3-TTS-12Hz-1.7B-CustomVoice，speaker=Uncle_Fu 男声）

| 文件 | 台词 | instruct | 时长 | 响度 |
|---|---|---|---|---|
| voice_start.wav | 开始！ | 街机厅游戏播报员，洪亮兴奋地喊出来，节奏快 | 0.76s | -16.3 |
| voice_pass.wav | 过关！ | 街机厅游戏播报员，胜利喜悦地喊出来，上扬 | 0.68s | -17.1 |
| voice_fail.wav | 失败！ | 街机厅游戏播报员，惋惜但鼓励的语气，下沉 | 1.16s | -16.3 |
| voice_gameover.wav | 游戏结束！ | 街机厅游戏播报员，戏剧化地宣告，一字一顿 | 1.08s | -15.9 |
| voice_combo.wav | 太棒了！ | 只喊两个字「太棒了」，街机播报员兴奋大喊，说完立刻停（max_new_tokens=96） | 1.08s | -15.9 |

voice_combo 首轮生成 5.52s（TTS 连续复读），已用 max_new_tokens 限制重生成。

## 授权备注（详见主控汇报）

- Stable Audio 3：Stability AI Community License，年收入 < $1M 可商用。
- Mureka：付费 API 按平台条款获得生成内容使用权，task id 留存溯源。
- Qwen3-TTS：Apache-2.0，可商用；Uncle_Fu 为模型预置合成音色，非真人录音。
