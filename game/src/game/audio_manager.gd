class_name AudioManager
extends Node

## 音频管理器：统一音效/语音/BGM 出口
##   play(name)  —— 按 CLIP_MAP 映射到 res://assets/audio/ 的 WAV（变体 _1.._8 随机 + pitch ±5%），
##                  文件缺失时回退程序合成音（fallback 永不缺席，素材丢了游戏也照跑）
##   voice(name) —— 中文语音播报（voice_<name>.wav），独立 Voice 总线，可与音效叠加
##   play_bgm(track) —— menu/play/intense 三轨 1s 交叉淡化；stop_bgm() 淡出静音
##   blip(...)   —— 合成音兼容层
##
## 事件点对照（详见 MANIFEST）：
##   shot 开火 / hit 命中 / hit_gold 金靶命中 / hostage 误伤 / reload 换弹 / empty 空仓
##   coin 投币 / start 开始 / pass 过关 / fail 失败 / gameover 总结算 / combo 连击里程碑
##   gold_spawn 金靶出现 / escape 逃靶 / ui_click 调试键 / stinger_win / stinger_lose

@export var audio_dir := "res://assets/audio"

## play() 事件名 → 文件名 base（空串 = 无素材常驻合成 fallback）
const CLIP_MAP := {
	"shot": "sfx_shot", "hit": "sfx_hit", "hit_gold": "sfx_hit_gold",
	"hostage": "sfx_hostage", "reload": "sfx_reload", "coin": "sfx_coin",
	"start": "sfx_start", "pass": "sfx_pass", "fail": "sfx_fail",
	"gameover": "sfx_gameover", "combo": "sfx_combo", "gold_spawn": "sfx_gold_spawn",
	"escape": "sfx_escape", "ui_click": "sfx_ui_click",
	"stinger_win": "stinger_win", "stinger_lose": "stinger_lose",
	"empty": "sfx_empty",
}

## 语音播报名 → voice_<name>.wav
const VOICE_NAMES := ["start", "pass", "fail", "gameover", "combo"]

## 合成音 fallback 参数表：[频率 Hz, 时长 s, 音量]
const SYNTH := {
	"shot": [880.0, 0.06, 0.3], "empty": [160.0, 0.09, 0.3],
	"reload": [440.0, 0.05, 0.25], "coin": [990.0, 0.1, 0.3],
	"start": [1320.0, 0.12, 0.3], "hit": [660.0, 0.05, 0.25],
	"hit_gold": [1200.0, 0.08, 0.3], "hostage": [140.0, 0.15, 0.35],
	"escape": [300.0, 0.18, 0.3], "combo": [1560.0, 0.15, 0.35],
	"gold_spawn": [1800.0, 0.1, 0.25], "ui_click": [700.0, 0.03, 0.2],
	"pass": [660.0, 0.2, 0.3], "fail": [180.0, 0.2, 0.3],
	"gameover": [150.0, 0.4, 0.35], "stinger_win": [880.0, 0.4, 0.35],
	"stinger_lose": [150.0, 0.4, 0.35],
}
const BGM_TRACKS := ["menu", "play", "intense"]
const BGM_FADE := 1.0  ## 交叉淡化秒数

var _rng := RandomNumberGenerator.new()
var _synth_player: AudioStreamPlayer
var _synth: AudioStreamGeneratorPlayback
var _bgm_players: Array[AudioStreamPlayer] = []
var _bgm_current := ""
var _muted_headless := false


func _ready() -> void:
	_rng.randomize()
	# headless 下 dummy 音频驱动的 playback 引用永不释放（引擎已知怪癖），
	# 测试环境不建任何播放器：静音运行 + 退出无泄漏警告
	if DisplayServer.get_name() == "headless":
		_muted_headless = true
		return
	# 音乐/音效/语音独立总线（音量设置与语音叠加用）
	for bus in ["Music", "SFX", "Voice"]:
		if AudioServer.get_bus_index(bus) < 0:
			AudioServer.add_bus()
			AudioServer.set_bus_name(AudioServer.bus_count - 1, bus)
	# 合成音发生器（fallback 用）
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = 22050.0
	gen.buffer_length = 0.1
	_synth_player = AudioStreamPlayer.new()
	_synth_player.stream = gen
	_synth_player.bus = "SFX"
	add_child(_synth_player)
	_synth_player.play()
	_synth = _synth_player.get_stream_playback()
	# 双 BGM 播放器交叉淡化
	for i in 2:
		var p := AudioStreamPlayer.new()
		p.bus = "Music"
		p.volume_db = -60.0
		add_child(p)
		_bgm_players.append(p)


func _exit_tree() -> void:
	# 释放 playback 引用，避免退出时 ObjectDB 泄漏警告
	if _synth_player:
		_synth_player.stop()
		_synth = null
		_synth_player.stream = null


# ---------- 音效 ----------

## 解析音效文件路径（纯查询，不进树也能测）；无文件返回 ""
func resolve_clip(clip_name: String) -> String:
	var base: String = CLIP_MAP.get(clip_name, clip_name)
	if base == "":
		return ""
	return AssetLib.pick_variant(audio_dir, base, ["wav"], _rng)


func play(clip_name: String) -> void:
	if _muted_headless:
		return
	var path := resolve_clip(clip_name)
	if path == "":
		_fallback(clip_name)
		return
	var stream: AudioStream = load(path)
	if stream == null:
		_fallback(clip_name)
		return
	_spawn_player(stream, "SFX")


## 语音播报：与音效叠加，不打断 BGM；无文件静默
func voice(voice_name: String) -> void:
	if _muted_headless or not voice_name in VOICE_NAMES:
		return
	var path := AssetLib.pick_variant(audio_dir, "voice_" + voice_name, ["wav"], _rng)
	if path == "":
		return
	var stream: AudioStream = load(path)
	if stream:
		_spawn_player(stream, "Voice")


func _spawn_player(stream: AudioStream, bus: String) -> void:
	var p := AudioStreamPlayer.new()
	p.stream = stream
	p.bus = bus
	p.pitch_scale = _rng.randf_range(0.95, 1.05)  # 反复播不单调
	add_child(p)
	p.finished.connect(p.queue_free)
	p.play()


func _fallback(clip_name: String) -> void:
	if SYNTH.has(clip_name):
		var s = SYNTH[clip_name]
		blip(s[0], s[1], s[2])


## 合成音兼容层：正弦短音（线性衰减防爆音）
func blip(freq := 880.0, dur := 0.07, volume := 0.25) -> void:
	if _synth == null:
		return  # headless / 无声卡环境下静默跳过
	var rate := 22050.0
	var frames := int(rate * dur)
	for i in frames:
		if not _synth.can_push_buffer(1):
			break
		var t := float(i) / rate
		var env := 1.0 - float(i) / frames
		var s := sin(t * freq * TAU) * volume * env
		_synth.push_frame(Vector2(s, s))


# ---------- BGM ----------

## 解析 BGM 文件路径；无文件返回 ""
func resolve_bgm(track: String) -> String:
	return AssetLib.pick_variant(audio_dir, "bgm_" + track, ["wav", "ogg", "mp3"], _rng)


## 切换 BGM（交叉淡化 1s）；track 需为 menu/play/intense，文件缺失则淡出后静音
func play_bgm(track: String) -> void:
	if _muted_headless or track == _bgm_current or not track in BGM_TRACKS:
		return
	_bgm_current = track
	var path := resolve_bgm(track)
	var next: AudioStreamPlayer = null
	if path != "":
		var stream: AudioStream = load(path)
		if stream:
			if stream is AudioStreamOggVorbis:
				stream.loop = true
			elif stream is AudioStreamWAV:
				stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
			next = _bgm_players[0] if _bgm_players[1].playing else _bgm_players[1]
			next.stream = stream
			next.volume_db = -60.0
			next.play()
	for p in _bgm_players:
		var tween := create_tween()
		if p == next:
			tween.tween_property(p, "volume_db", 0.0, BGM_FADE)
		else:
			tween.tween_property(p, "volume_db", -60.0, BGM_FADE)
			tween.tween_callback(p.stop)


## 总结算页：BGM 淡出（不回 menu，让 stinger 唱主角）
func stop_bgm() -> void:
	if _muted_headless:
		return
	_bgm_current = ""
	for p in _bgm_players:
		var tween := create_tween()
		tween.tween_property(p, "volume_db", -60.0, BGM_FADE)
		tween.tween_callback(p.stop)


## 应用音量设置（0-1 线性 → dB）
func set_volumes(master: float, music: float, sfx: float) -> void:
	if _muted_headless:
		return
	AudioServer.set_bus_volume_db(0, linear_to_db(maxf(master, 0.0001)))
	for pair in [["Music", music], ["SFX", sfx]]:
		var idx := AudioServer.get_bus_index(pair[0])
		if idx >= 0:
			AudioServer.set_bus_volume_db(idx, linear_to_db(maxf(pair[1], 0.0001)))
