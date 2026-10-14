class_name AudioManager
extends Node

## 音频管理器：统一音效/BGM 出口
##   play(name)  —— 优先播 res://assets/audio/<name>.wav（同名 _2.._8 变体随机 + pitch ±5%），
##                  文件不存在时回退到程序合成音（素材 Agent 交货前游戏也能响）
##   play_bgm(track) —— "menu"/"play"/"intense" 三轨交叉淡化，文件缺失静音不报错
##   blip(...)   —— 合成音兼容层（老调用直接可用）
##
## 文件名 ↔ play() 名映射（素材 Agent 按此交货）：
##   shot 开火 / empty 空仓 / reload 换弹 / coin 投币 / start 开始
##   hit 命中 / hostage 误伤人质 / escape 逃靶 / combo 连击里程碑
##   level_pass 过关 / level_fail 失败 / win 通关 / lose GAMEOVER
##   bgm_menu / bgm_play / bgm_intense （ogg|wav|mp3，循环）

@export var audio_dir := "res://assets/audio"

## 合成音 fallback 参数表：[频率 Hz, 时长 s, 音量]
const SYNTH := {
	"shot": [880.0, 0.06, 0.3], "empty": [160.0, 0.09, 0.3],
	"reload": [440.0, 0.05, 0.25], "coin": [990.0, 0.1, 0.3],
	"start": [1320.0, 0.12, 0.3], "hit": [660.0, 0.05, 0.25],
	"hostage": [140.0, 0.15, 0.35], "escape": [300.0, 0.18, 0.3],
	"combo": [1560.0, 0.15, 0.35], "level_pass": [660.0, 0.2, 0.3],
	"level_fail": [180.0, 0.2, 0.3], "win": [880.0, 0.4, 0.35],
	"lose": [150.0, 0.4, 0.35],
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
	# 音乐/音效独立总线（音量设置用）
	for bus in ["Music", "SFX"]:
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
	return AssetLib.pick_variant(audio_dir, clip_name, ["wav"], _rng)


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
	var p := AudioStreamPlayer.new()
	p.stream = stream
	p.bus = "SFX"
	p.pitch_scale = _rng.randf_range(0.95, 1.05)  # 同一音效反复播不单调
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
	return AssetLib.pick_variant(audio_dir, "bgm_" + track, ["ogg", "wav", "mp3"], _rng)


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


## 应用音量设置（0-1 线性 → dB）
func set_volumes(master: float, music: float, sfx: float) -> void:
	if _muted_headless:
		return
	AudioServer.set_bus_volume_db(0, linear_to_db(maxf(master, 0.0001)))
	for pair in [["Music", music], ["SFX", sfx]]:
		var idx := AudioServer.get_bus_index(pair[0])
		if idx >= 0:
			AudioServer.set_bus_volume_db(idx, linear_to_db(maxf(pair[1], 0.0001)))
